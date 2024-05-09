using System;
using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Contracts.ProxyAccountContract;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using CreateInput = AElf.Contracts.MultiToken.CreateInput;

namespace Forest.Contracts.SymbolRegistrar
{
    /// <summary>
    /// The C# implementation of the contract defined in symbol_registrar_contract.proto that is located in the "protobuf"
    /// folder.
    /// Notice that it inherits from the protobuf generated code. 
    /// </summary>
    public partial class SymbolRegistrarContract : SymbolRegistrarContractContainer.SymbolRegistrarContractBase
    {
        
        public override Empty Buy(BuyInput input)
        {
            AssertContractInitialize();
            AssertSymbolPattern(input.Symbol);
            
            var specialSeed = State.SpecialSeedMap[input.Symbol];
            Assert(specialSeed == null, "Special seed " + input.Symbol + " not support deal.");
            CheckSymbolExisted(input.Symbol);

            var price = GetDealPrice(input.Symbol);
            Assert(price != null, "Symbol price not exits");

            var issueTo = input.IssueTo ?? Context.Sender;
            
            State.TokenContract.TransferFrom.Send(new TransferFromInput()
            {
                From = Context.Sender,
                To = State.ReceivingAccount.Value,
                Symbol = price.Symbol,
                Amount = price.Amount,
            });

            DoCreateSeed(issueTo, input.Symbol);

            Context.Fire(new Bought()
            {
                Buyer = Context.Sender,
                Symbol = input.Symbol,
                Price = new Price
                {
                    Symbol = price.Symbol,
                    Amount = price.Amount
                }
            });
            
            return new Empty();
        }
        
        public override Empty SetSeedExpirationConfig(SeedExpirationConfig input)
        {
            AssertInitialized();
            AssertAdmin();

            Assert(input != null, "Invalid input.");
            Assert(input.ExpirationTime > 0, "Invalid input expiration time.");

            if (State.SeedExpirationConfig.Value == input.ExpirationTime)
            {
                return new Empty();
            }

            State.SeedExpirationConfig.Value = input.ExpirationTime;

            Context.Fire(new SeedExpirationConfigChanged
            {
                SeedExpirationConfig = input
            });

            return new Empty();
        }

        public override Empty CreateSeed(CreateSeedInput input)
        {
            AssertSaleController();
            Assert(input.To != null && !input.To.Value.IsNullOrEmpty(), "To address is empty");
            AssertSymbolPattern(input.Symbol);
            var specialSeed = State.SpecialSeedMap[input.Symbol];
            Assert(specialSeed == null || specialSeed.SeedType != SeedType.Disable, "seed " + input.Symbol + " not support create.");
            CheckSymbolExisted(input.Symbol);
            DoCreateSeed(input.To, input.Symbol);
            return new Empty();
        }
        
        private void DoCreateSeed(Address to, string symbol, long expireTime = 0)
        {
            var createResult = CreateSeedToken(Context.Self, symbol, expireTime);
            if (!createResult)
            {
                return;
            }
            var seedSymbol = State.SymbolSeedMap[symbol];
            State.TokenContract.Issue.Send(
                new IssueInput
                {
                    Amount = 1,
                    Symbol = seedSymbol,
                    To = to
                });
            var seedInfo = State.SeedInfoMap[seedSymbol];
            seedInfo.To = to;
            State.SeedInfoMap[seedSymbol] = seedInfo;
            var specialSeed = State.SpecialSeedMap[symbol];
            Context.Fire(new SeedCreated
            {
                Symbol = seedSymbol,
                OwnedSymbol = symbol,
                ExpireTime = seedInfo.ExpireTime,
                SeedType = specialSeed?.SeedType ?? SeedType.Regular,
                To = to,
                ImageUrl = seedInfo.ImageUrl
            });
        }

        private bool CreateSeedToken(Address issuer, string symbol, long expireTime = 0)
        {
            var seedCollectionOwner = GetSeedCollectionOwner();
            State.LastSeedId.Value = State.LastSeedId.Value.Add(1);
            var seedSymbol = SymbolRegistrarContractConstants.SeedPrefix + State.LastSeedId.Value;
            var seedTokenInfo = GetTokenInfo(seedSymbol);
            for (var i = 1; i <= SymbolRegistrarContractConstants.MaxCycleCount; i++) {
                if (seedTokenInfo == null || string.IsNullOrWhiteSpace(seedTokenInfo.Symbol))
                {
                    break;
                }
                State.LastSeedId.Value = State.LastSeedId.Value.Add(1);
                seedSymbol = SymbolRegistrarContractConstants.SeedPrefix + State.LastSeedId.Value;
                seedTokenInfo = GetTokenInfo(seedSymbol);
            }
            if (seedTokenInfo != null && !string.IsNullOrWhiteSpace(seedTokenInfo.Symbol))
            {
                return false;
            }

            State.SymbolSeedMap[symbol] = seedSymbol;
            var createInput = new CreateInput
            {
                Symbol = seedSymbol,
                TokenName = SymbolRegistrarContractConstants.SeedPrefix + symbol,
                Decimals = 0,
                IsBurnable = true,
                TotalSupply = 1,
                Owner = seedCollectionOwner,
                Issuer = issuer,
                ExternalInfo = new ExternalInfo(),
                LockWhiteList = { State.TokenContract.Value }
            };
            
            createInput.ExternalInfo.Value[SymbolRegistrarContractConstants.SeedOwnedSymbolExternalInfoKey] = symbol;
            expireTime = expireTime == 0 ? Context.CurrentBlockTime.AddSeconds(State.SeedExpirationConfig.Value).Seconds : expireTime;
            createInput.ExternalInfo.Value[SymbolRegistrarContractConstants.SeedExpireTimeExternalInfoKey] = expireTime.ToString();
            
            if (!String.IsNullOrEmpty(State.SeedImageUrlPrefix.Value))
            {
                createInput.ExternalInfo.Value[SymbolRegistrarContractConstants.NftImageUrlExternalInfoKey] =
                    State.SeedImageUrlPrefix.Value + seedSymbol + SymbolRegistrarContractConstants.NftImageUrlSuffix;
            }

            State.ProxyAccountContract.ForwardCall.Send(
                new ForwardCallInput
                {
                    ContractAddress = State.TokenContract.Value,
                    MethodName = nameof(State.TokenContract.Create),
                    ProxyAccountHash = GetProxyAccountHash(),
                    Args = createInput.ToByteString()
                });
            State.SeedInfoMap[seedSymbol] = new SeedInfo
            {
                Symbol = seedSymbol,
                OwnedSymbol = symbol,
                ExpireTime = expireTime,
                ImageUrl = createInput.ExternalInfo.Value.GetValueOrDefault(SymbolRegistrarContractConstants.NftImageUrlExternalInfoKey, "")
            };
            return true;
        }

        private Address GetSeedCollectionOwner()
        {
            var owner = State.SeedCollectionOwner.Value;
            if (owner != null && !owner.Value.IsNullOrEmpty())
            {
                return owner;
            }
            var seedCollection = GetTokenInfo(SymbolRegistrarContractConstants.SeedPrefix +
                                              SymbolRegistrarContractConstants.CollectionSymbolSuffix);
            Assert(seedCollection?.Owner != null && !seedCollection.Owner.Value.IsNullOrEmpty(), "SeedCollection not existed.");
            State.SeedCollectionOwner.Value = seedCollection.Owner;
            return seedCollection.Owner;
        }

        private Hash GetProxyAccountHash()
        {
            var proxyAccountHash = State.ProxyAccountHash.Value;
            if (proxyAccountHash != null && !proxyAccountHash.Value.IsNullOrEmpty())
            {
                return proxyAccountHash;
            }
            var proxyAccount = State.ProxyAccountContract.GetProxyAccountByProxyAccountAddress.Call(GetSeedCollectionOwner());
            Assert(proxyAccount?.ProxyAccountHash != null, "ProxyAccountHash not existed.");
            State.ProxyAccountHash.Value = proxyAccount.ProxyAccountHash;
            return proxyAccount.ProxyAccountHash;
        }
    }
}
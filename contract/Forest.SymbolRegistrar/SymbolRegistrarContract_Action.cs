using AElf.Contracts.MultiToken;
using AElf.Contracts.ProxyAccountContract;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using CreateInput = AElf.Contracts.MultiToken.CreateInput;

namespace Forest.SymbolRegistrar
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
            AssertCanDeal(input.Symbol);

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

            CreateSeed(issueTo, input.Symbol);

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
            AssertSaleController();

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
            // Todo Assert(State.CreateSeedAdminMap[Context.Sender], "No permission.");
            CreateSeed(input.To, input.Symbol, 0);
            return new Empty();
        }

        private void CreateSeed(Address to, string symbol, long expireTime = 0)
        {
            var empty = new TokenInfo();
            Assert(State.SymbolSeedMap[symbol] == null, "symbol seed existed");
            var createTokenInfo = GetTokenInfo(symbol);
            Assert(createTokenInfo == null || string.IsNullOrWhiteSpace(createTokenInfo.Symbol), "symbol " + symbol + " existed");
            var seedCollection = GetTokenInfo(SymbolRegistrarContractConstants.SeedPrefix + 0);
            Assert(seedCollection.Symbol.Length > 0 && seedCollection.Symbol == SymbolRegistrarContractConstants.SeedPrefix + 0, "seedCollection not existed");
            
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
            if (seedTokenInfo != null && seedTokenInfo.Symbol == seedSymbol)
            {
                return;
            }

            State.SymbolSeedMap[symbol] = seedSymbol;
            var createInput = new CreateInput
            {
                Symbol = seedSymbol,
                TokenName = SymbolRegistrarContractConstants.SeedPrefix + symbol,
                Decimals = 0,
                IsBurnable = true,
                TotalSupply = 1,
                Owner = seedCollection.Owner,
                Issuer = Context.Self,
                IssueChainId = Context.ChainId,
                ExternalInfo = new ExternalInfo(),
                LockWhiteList = { State.TokenContract.Value }
            };
            
            createInput.ExternalInfo.Value[SymbolRegistrarContractConstants.SeedOwnedSymbolExternalInfoKey] = symbol;
            expireTime = expireTime == 0 ? Context.CurrentBlockTime.AddSeconds(State.SeedExpirationConfig.Value).Seconds : expireTime;
            createInput.ExternalInfo.Value[SymbolRegistrarContractConstants.SeedExpireTimeExternalInfoKey] = expireTime.ToString();
            
            State.ProxyAccountContract.ForwardCall.Send(
                new ForwardCallInput
                {
                    ContractAddress = State.TokenContract.Value,
                    MethodName = nameof(State.TokenContract.Create),
                    ProxyAccountHash = GetProxyAccountHash(),
                    Args = createInput.ToByteString()
                });
            
            State.TokenContract.Issue.Send(
                new IssueInput
                {
                    Amount = 1,
                    Symbol = createInput.Symbol,
                    To = to
                });

            State.SeedInfoMap[createInput.Symbol] = new SeedInfo
            {
                Symbol = createInput.Symbol,
                OwnedSymbol = symbol,
                ExpireTime = expireTime,
                To = to
            };
            
            Context.Fire(new SeedCreated
            {
                Symbol = createInput.Symbol,
                OwnedSymbol = symbol,
                ExpireTime = expireTime,
                To = to
            });
        }

        private Hash GetProxyAccountHash()
        {
            if (State.SeedNftCollectionProxyAccountHash.Value != null)
            {
                return State.SeedNftCollectionProxyAccountHash.Value;
            }

            var symbol = SymbolRegistrarContractConstants.SeedPrefix + 0;
            var tokenInfo = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
            {
                Symbol = symbol
            });
            Assert(tokenInfo != null && tokenInfo.Owner != null, "seed nft collection not existed");
            var proxyAccount = State.ProxyAccountContract.GetProxyAccountByProxyAccountAddress.Call(tokenInfo.Owner);
            State.SeedNftCollectionProxyAccountHash.Value = proxyAccount.ProxyAccountHash;
            return State.SeedNftCollectionProxyAccountHash.Value;
        }
        
        
    }
}
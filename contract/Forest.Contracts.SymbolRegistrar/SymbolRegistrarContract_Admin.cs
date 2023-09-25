using System;
using System.Collections.Generic;
using System.Linq;
using AElf;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Forest.Contracts.SymbolRegistrar
{
    /// <summary>
    /// The C# implementation of the contract defined in symbol_registrar_contract.proto that is located in the "protobuf"
    /// folder.
    /// Notice that it inherits from the protobuf generated code. 
    /// </summary>
    public partial class SymbolRegistrarContract : SymbolRegistrarContractContainer.SymbolRegistrarContractBase
    {
        public override Empty Initialize(InitializeInput input)
        {
            Assert(!State.Initialized.Value, "Contract has bean Initialized.");
            AssertContractAuthor();

            Assert(input.ReceivingAccount != null && !input.ReceivingAccount.Value.IsNullOrEmpty(),
                "PaymentReceiverAddress required.");

            State.Admin.Value = input.AdministratorAddress ?? Context.Sender;
            State.ReceivingAccount.Value = input.ReceivingAccount;
            State.SeedExpirationConfig.Value = SymbolRegistrarContractConstants.DefaultSeedExpirationTime;
            Assert(input.ProxyAccountAddress != null && !input.ProxyAccountAddress.Value.IsNullOrEmpty(),
                "ProxyAccountAddress required.");
            State.ProxyAccountContract.Value = input.ProxyAccountAddress;
            State.TokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
            State.ParliamentContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.ParliamentContractSystemName);

            InitializeAuctionConfig();
            if (input.SpecialSeeds != null)
                AddSpecialSeeds(input.SpecialSeeds);

            if (input.SeedsPrices != null)
                UpdateSeedsPrice(input.SeedsPrices);

            State.Initialized.Value = true;
            return new Empty();
        }

        public override Empty SetAdmin(Address input)
        {
            AssertAdmin();
            Assert(input != null && !input.Value.IsNullOrEmpty(), "Invalid param");
            State.Admin.Value = input;
            return new Empty();
        }

        public override Empty SetReceivingAccount(Address input)
        {
            AssertAdmin();
            Assert(input != null && !input.Value.IsNullOrEmpty(), "Invalid param");
            State.ReceivingAccount.Value = input;
            return new Empty();
        }

        public override Empty SetProxyAccountContract(Address input)
        {
            AssertAdmin();
            Assert(input != null && !input.Value.IsNullOrEmpty(), "Invalid param");
            State.ProxyAccountContract.Value = input;
            return new Empty();
        }

        public override Empty SetLastSeedId(Int64Value input)
        {
            AssertAdmin();
            Assert(input.Value > State.LastSeedId.Value, "Invalid param");
            State.LastSeedId.Value = input.Value;
            return new Empty();
        }

        public override Empty SetSeedImageUrlPrefix(StringValue input)
        {
            AssertAdmin();
            Assert(input != null && !String.IsNullOrWhiteSpace(input.Value), "Invalid param");
            State.SeedImageUrlPrefix.Value = input.Value;
            return new Empty();
        }

        public override Empty SetSeedsPrice(SeedsPriceInput input)
        {
            AssertAdmin();
            UpdateSeedsPrice(input);
            return new Empty();
        }

        private void UpdateSeedsPrice(SeedsPriceInput input)
        {
            var ftListEmpty = (input.FtPriceList?.Value?.Count ?? 0) == 0;
            var nftListEmpty = (input.NftPriceList?.Value?.Count ?? 0) == 0;
            
            if (ftListEmpty && nftListEmpty)
            {
                return;
            }
            var priceSymbolExists = new HashSet<string> { SymbolRegistrarContractConstants.ELFSymbol };
            if (!ftListEmpty)
            {
                AssertPriceList(input.FtPriceList);
                foreach (var ftPriceItem in input.FtPriceList.Value)
                {
                    if (!priceSymbolExists.Contains(ftPriceItem.Symbol))
                    {
                        var tokenInfo = GetTokenInfo(ftPriceItem.Symbol);
                        Assert(tokenInfo?.Symbol.Length > 0, "Price token " + ftPriceItem.Symbol + " not exists");
                        priceSymbolExists.Add(ftPriceItem.Symbol);
                    }
                    State.FTPrice[ftPriceItem.SymbolLength] = ftPriceItem;
                }
            }

            if (!nftListEmpty)
            {
                AssertPriceList(input.NftPriceList);
                foreach (var nftPriceItem in input.NftPriceList.Value)
                {
                    if (!priceSymbolExists.Contains(nftPriceItem.Symbol))
                    {
                        var tokenInfo = GetTokenInfo(nftPriceItem.Symbol);
                        Assert(tokenInfo?.Symbol.Length > 0, "Price token " + nftPriceItem.Symbol + " not exists");
                        priceSymbolExists.Add(nftPriceItem.Symbol);
                    }
                    State.NFTPrice[nftPriceItem.SymbolLength] = nftPriceItem;
                }
            }

            Context.Fire(new SeedsPriceChanged
            {
                FtPriceList = input.FtPriceList,
                NftPriceList = input.NftPriceList
            });
        }

        public override Empty AddSaleController(AddSaleControllerInput input)
        {
            AssertInitialized();
            AssertAdmin();
            Assert(input != null && input.Addresses != null, "Invalid input.");
            Assert(input.Addresses.Controllers != null && input.Addresses.Controllers.Count > 0,
                "Invalid input controllers");
            if (State.SaleController.Value == null)
            {
                State.SaleController.Value = new ControllerList();
            }

            if (State.SaleController.Value.Equals(input.Addresses))
            {
                return new Empty();
            }

            var controllerList = State.SaleController.Value.Clone();
            controllerList.Controllers.AddRange(input.Addresses.Controllers);

            State.SaleController.Value = new ControllerList
            {
                Controllers = { controllerList.Controllers.Distinct() }
            };

            Context.Fire(new SaleControllerAdded
            {
                Addresses = State.SaleController.Value
            });
            return new Empty();
        }

        public override Empty RemoveSaleController(RemoveSaleControllerInput input)
        {
            AssertInitialized();
            AssertAdmin();
            Assert(input != null && input.Addresses != null, "Invalid input.");
            Assert(input.Addresses.Controllers != null && input.Addresses.Controllers.Count > 0,
                "Invalid input controllers");

            var removeList = input.Addresses.Controllers.Intersect(State.SaleController.Value.Controllers)
                .ToList();
            if (removeList.Count == 0)
            {
                return new Empty();
            }

            var controllerList = State.SaleController.Value.Clone();
            foreach (var address in removeList)
            {
                controllerList.Controllers.Remove(address);
            }

            State.SaleController.Value = new ControllerList
            {
                Controllers = { controllerList.Controllers }
            };

            Context.Fire(new SaleControllerRemoved
            {
                Addresses = State.SaleController.Value
            });
            return new Empty();
        }

        public override Empty SetAuctionConfig(AuctionConfig input)
        {
            AssertInitialized();
            AssertSaleController();

            Assert(input != null, "Invalid input.");
            Assert(input.Duration > 0, "Invalid input duration.");
            Assert(input.CountdownTime >= 0, "Invalid input countdown time.");
            Assert(input.MaxExtensionTime >= 0, "Invalid input max extension time.");
            Assert(input.MinMarkup >= 0, "Invalid input min markup.");

            if (State.AuctionConfig.Value.Equals(input))
            {
                return new Empty();
            }

            State.AuctionConfig.Value = input;
            return new Empty();
        }
    }
}
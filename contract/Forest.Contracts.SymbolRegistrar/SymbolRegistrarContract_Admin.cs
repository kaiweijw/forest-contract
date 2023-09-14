using System;
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

            if (input.SpecialSeeds != null)
                AddSpecialSeeds(input.SpecialSeeds);

            if (input.SeedsPrices != null)
                SetSeedsPrice(input.SeedsPrices);

            State.TokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
            State.ParliamentContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.ParliamentContractSystemName);

            InitializeAuctionConfig();

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
            if (input.FtPriceList?.Value?.Count == 0 && input.NftPriceList?.Value?.Count == 0)
            {
                return new Empty();
            }

            if (input.FtPriceList.Value.Count > 0)
            {
                AssertPriceList(input.FtPriceList);
                foreach (var ftPriceItem in input.FtPriceList.Value)
                {
                    State.FTPrice[ftPriceItem.SymbolLength] = ftPriceItem;
                }
            }

            if (input.NftPriceList.Value.Count > 0)
            {
                AssertPriceList(input.NftPriceList);
                foreach (var nftPriceItem in input.NftPriceList.Value)
                {
                    State.NFTPrice[nftPriceItem.SymbolLength] = nftPriceItem;
                }
            }

            Context.Fire(new SeedsPriceChanged
            {
                FtPriceList = input.FtPriceList,
                NftPriceList = input.NftPriceList
            });

            return new Empty();
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

    
    }
}
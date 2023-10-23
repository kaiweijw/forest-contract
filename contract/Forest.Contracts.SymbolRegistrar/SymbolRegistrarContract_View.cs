using System;
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
        
        public override BizConfig GetBizConfig(Empty input)
        {
            return new BizConfig()
            {
                AdministratorAddress = State.Admin.Value,
                ReceivingAccount = State.ReceivingAccount.Value,
            };
        }

        public override ControllerList GetSaleController(Empty input)
        {
            return State.SaleController.Value;
        }

        public override GetSeedsPriceOutput GetSeedsPrice(Empty input)
        {
            var ftPriceList = new PriceList();
            var nftPriceList = new PriceList();
            for (var i = 0; i < SymbolRegistrarContractConstants.MaxSymbolLength; i++)
            {
                var ftPrice = State.FTPrice[i + 1];
                if (ftPrice != null)
                {
                    ftPriceList.Value.Add(ftPrice);
                }
                var nftPrice = State.NFTPrice[i + 1];
                if (nftPrice != null)
                {
                    nftPriceList.Value.Add(nftPrice);
                }
            }

            return new GetSeedsPriceOutput
            {
                FtPriceList = ftPriceList,
                NftPriceList = nftPriceList,
            };
        }

        public override SpecialSeed GetSpecialSeed(StringValue input)
        {
            var specialSeed = State.SpecialSeedMap[input.Value];
            if (specialSeed == null)
            {
                return new SpecialSeed();
            }

            if (specialSeed.SeedType == SeedType.Unique && specialSeed.PriceAmount == 0 && String.IsNullOrWhiteSpace(specialSeed.PriceSymbol))
            {
                var isNFT = input.Value.Contains(SymbolRegistrarContractConstants.NFTSymbolSeparator);
                var seedPrice = isNFT ? State.NFTPrice[input.Value.Length] : State.FTPrice[input.Value.Length];
                var uniqueSeedPrice = isNFT ? State.UniqueExternalNFTPrice[input.Value.Length] : State.UniqueExternalFTPrice[input.Value.Length];
                if (seedPrice != null && uniqueSeedPrice != null && seedPrice.Symbol == uniqueSeedPrice.Symbol)
                {
                    specialSeed.PriceSymbol = seedPrice.Symbol;
                    specialSeed.PriceAmount = seedPrice.Amount + uniqueSeedPrice.Amount;
                }
            }
            return specialSeed;
        }
        
        public override AuctionConfig GetAuctionConfig(Empty input)
        {
            return State.AuctionConfig.Value;
        }

        public override SeedExpirationConfig GetSeedExpirationConfig(Empty input)
        {
            return new SeedExpirationConfig
            {
                ExpirationTime = State.SeedExpirationConfig.Value
            };
        }

        public override Address GetProxyAccountContract(Empty input)
        {
            return State.ProxyAccountContract.Value;
        }

        public override Int64Value GetLastSeedId(Empty input)
        {
            return new Int64Value { Value = State.LastSeedId.Value };
        }

        public override StringValue GetSeedImageUrlPrefix(Empty input)
        {
            return new StringValue { Value = State.SeedImageUrlPrefix.Value };
        }
    }
}
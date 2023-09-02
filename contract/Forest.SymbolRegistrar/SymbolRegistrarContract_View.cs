using Google.Protobuf.WellKnownTypes;

namespace Forest.SymbolRegistrar
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
                ftPriceList.Value.Add(State.FTPrice[i + 1]);
                nftPriceList.Value.Add(State.NFTPrice[i + 1]);
            }

            return new GetSeedsPriceOutput
            {
                FtPriceList = ftPriceList,
                NftPriceList = nftPriceList,
            };
        }

        public override SpecialSeed GetSpecialSeed(StringValue input)
        {
            return input.Value.Length == 0 ? null : State.SpecialSeedMap[input.Value];
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

    }
}
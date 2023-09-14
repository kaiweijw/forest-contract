using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace Forest.Contracts.SymbolRegistrar
{
    /// <summary>
    /// The state class of the contract, it inherits from the AElf.Sdk.CSharp.State.ContractState type. 
    /// </summary>
    public partial class SymbolRegistrarContractState : ContractState
    {
        // state definitions go here.
        public SingletonState<AuctionConfig> AuctionConfig { get; set; }
        public SingletonState<long> SeedExpirationConfig { get; set; }


        // symbol -> seed-x
        public MappedState<string, string> SymbolSeedMap { get; set; }
        // seed-x -> seedInfo
        public MappedState<string, SeedInfo> SeedInfoMap { get; set; }
        public SingletonState<long> LastSeedId { get; set; }
        public SingletonState<string> SeedImageUrlPrefix { get; set; }
        public SingletonState<ControllerList> SaleController { get; set; }
        public SingletonState<bool> Initialized { get; set; }
        public SingletonState<Address> Admin { get; set; }
        public SingletonState<Address> ReceivingAccount { get; set; }
        
        // specialSymbol -> authorAccount
        public MappedState<string, Address> SymbolAuthorAccountMap { get; set; }

        // length -> Price, length from 1 to 30
        public MappedState<int, PriceItem> FTPrice { get; set; }

        // length -> Price, length from 1 to 30
        public MappedState<int, PriceItem> NFTPrice { get; set; }
        public MappedState<string, SpecialSeed> SpecialSeedMap { get; set; }
    }
}
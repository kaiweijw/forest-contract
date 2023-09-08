using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace Forest.Contracts.Auction
{
    public partial class AuctionContractState : ContractState
    {
        public SingletonState<bool> Initialized { get; set; }
        public SingletonState<Address> Admin { get; set; }
        
        // Auction Id -> Auction
        public MappedState<Hash, AuctionInfo> AuctionInfoMap { get; set; }
    }
}
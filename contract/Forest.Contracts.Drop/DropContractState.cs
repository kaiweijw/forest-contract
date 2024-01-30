using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace Forest.Contracts.Drop
{
    public partial class DropContractState : ContractState
    {
        public SingletonState<bool> Initialized { get; set; }
        public SingletonState<Address> Admin { get; set; }
        public SingletonState<int> MaxDropDetailListCount { get; set; }
        
        public SingletonState<int> MaxDropDetailIndexCount { get; set; }

        public MappedState<Hash, DropInfo> DropInfoMap { get; set; }
        public MappedState<Hash, int, DropDetailList> DropDetailListMap { get; set; }
        public MappedState<Hash, Address, ClaimDropDetail> ClaimDropMap { get; set; }
        
        public MappedState<Hash, string, int> DropSymbolMap { get; set; } 

        
    }
}
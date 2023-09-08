using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp.State;
using Forest.Contracts.SymbolRegistrar;

namespace Forest.Contracts.MockProxyAccountContract
{
    /// <summary>
    /// The state class of the contract, it inherits from the AElf.Sdk.CSharp.State.ContractState type. 
    /// </summary>
    public class MockProxyAccountContractState : ContractState
    {
        // state definitions go here.
        
        
        internal SymbolRegistrarContractContainer.SymbolRegistrarContractReferenceState SymbolMarketContract { get; set; }
        internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
        
    }
}
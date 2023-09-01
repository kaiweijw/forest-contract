using AElf.Contracts.MultiToken;
using AElf.Contracts.Parliament;
using AElf.Sdk.CSharp.State;
using AElf.Standards.ACS0;
using AElf.Types;

namespace Forest.SymbolRegistrar
{
    /// <summary>
    /// The state class of the contract, it inherits from the AElf.Sdk.CSharp.State.ContractState type. 
    /// </summary>
    public partial class SymbolRegistrarContractState : ContractState
    {
        
        internal ACS0Container.ACS0ReferenceState GenesisContract { get; set; }
        internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
        internal ParliamentContractContainer.ParliamentContractReferenceState ParliamentContract { get; set; }
        internal AElf.Contracts.ProxyAccountContract.ProxyAccountContractContainer.ProxyAccountContractReferenceState ProxyAccountContract
        {
            get;
            set;
        }
    }
}
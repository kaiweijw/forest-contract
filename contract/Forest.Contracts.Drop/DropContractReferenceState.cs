using AElf.Contracts.MultiToken;
using AElf.Standards.ACS0;

namespace Forest.Contracts.Drop;

public partial class DropContractState
{
    internal ACS0Container.ACS0ReferenceState GenesisContract { get; set; }
    internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
    internal AElf.Contracts.ProxyAccountContract.ProxyAccountContractContainer.ProxyAccountContractReferenceState ProxyAccountContract { get; set; }
}
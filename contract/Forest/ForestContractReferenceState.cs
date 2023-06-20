using AElf.Contracts.MultiToken;
using Forest.Whitelist;

namespace Forest;

public partial class ForestContractState
{
    internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
    
    internal WhitelistContractContainer.WhitelistContractReferenceState WhitelistContract { get; set; }
}
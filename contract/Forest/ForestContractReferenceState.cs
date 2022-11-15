using AElf.Contracts.MultiToken;
using AElf.Contracts.NFT;
using Forest.Whitelist;

namespace Forest;

public partial class ForestContractState
{
    internal NFTContractContainer.NFTContractReferenceState NFTContract { get; set; }
    internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
    
    internal WhitelistContractContainer.WhitelistContractReferenceState WhitelistContract { get; set; }
}
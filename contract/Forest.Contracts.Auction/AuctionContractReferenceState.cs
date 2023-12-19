using AElf.Contracts.MultiToken;
using AElf.Standards.ACS0;

namespace Forest.Contracts.Auction;

public partial class AuctionContractState
{
    internal ACS0Container.ACS0ReferenceState GenesisContract { get; set; }
    internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
}
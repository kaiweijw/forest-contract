using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Forest.Contracts.Auction;

public partial class AuctionContract
{
    public override AuctionInfo GetAuctionInfo(GetAuctionInfoInput input)
    {
        return State.AuctionInfoMap[input.AuctionId];
    }

    public override Address GetAdmin(Empty input)
    {
        return State.Admin.Value;
    }
}
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

    public override Int64Value GetCurrentCounter(StringValue input)
    {
        return new Int64Value { Value = State.SymbolCounter[input.Value] };
    }

    public override ControllerList GetAuctionController(Empty input)
    {
        return State.AuctionController.Value;
    }
}
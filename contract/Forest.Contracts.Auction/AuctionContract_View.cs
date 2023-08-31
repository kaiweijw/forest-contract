using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Forest.Contracts.Auction;

public partial class AuctionContract
{
    public override AuctionInfo GetAuctionInfo(GetAuctionInfoInput input)
    {
        return base.GetAuctionInfo(input);
    }

    public override Address GetPaymentReceiverAddress(Empty input)
    {
        return base.GetPaymentReceiverAddress(input);
    }
}
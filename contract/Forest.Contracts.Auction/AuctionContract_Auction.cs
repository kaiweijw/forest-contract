using Google.Protobuf.WellKnownTypes;

namespace Forest.Contracts.Auction;

public partial class AuctionContract
{
    public override Empty CreateAuction(CreateAuctionInput input)
    {
        return base.CreateAuction(input);
    }

    public override Empty PlaceBid(PlaceBidInput input)
    {
        return base.PlaceBid(input);
    }

    public override Empty Claim(ClaimInput input)
    {
        return base.Claim(input);
    }
}
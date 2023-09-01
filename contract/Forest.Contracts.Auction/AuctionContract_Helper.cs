using AElf.Types;

namespace Forest.Contracts.Auction;

public partial class AuctionContract
{
    private void AssertAdmin()
    {
        Assert(State.Admin.Value != null, "Not initialized.");
        Assert(Context.Sender == State.Admin.Value, "No permission.");
    }
}
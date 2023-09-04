using AElf.Types;

namespace Forest.Contracts.Auction;

public partial class AuctionContract
{
    private void AssertAdmin()
    {
        AssertInitialize();
        Assert(Context.Sender == State.Admin.Value, "No permission.");
    }

    private void AssertInitialize()
    {
        Assert(State.Initialized.Value, "Not initialized.");
    }
}
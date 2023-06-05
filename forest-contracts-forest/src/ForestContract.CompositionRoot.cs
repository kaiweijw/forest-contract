using Forest.Managers;
using Forest.Services;

namespace Forest;

public partial class ForestContract
{
    private WhitelistManager GetWhitelistManager()
    {
        return new WhitelistManager(Context, State.WhitelistIdMap, State.WhitelistContract);
    }

    private MakeOfferService GetMakeOfferService(WhitelistManager whitelistManager = null)
    {
        return new MakeOfferService(State.NFTContract, State.WhitelistIdMap, State.ListedNFTInfoListMap,
            whitelistManager ?? GetWhitelistManager(), Context);
    }

    private DealService GetDealService()
    {
        return new DealService(Context);
    }
}
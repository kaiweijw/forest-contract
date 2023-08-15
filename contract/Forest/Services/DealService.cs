using System.Collections.Generic;
using System.Linq;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;

namespace Forest.Services;

public class DealService
{
    private readonly CSharpSmartContractContext _context;

    public DealService(CSharpSmartContractContext context)
    {
        _context = context;
    }

    public IEnumerable<DealResult> GetDealResultList(GetDealResultListInput input)
    {
        var dealResultList = new List<DealResult>();
        var needToDealQuantity = input.MakeOfferInput.Quantity;
        var currentIndex = 0;
        var filteredListedNftInfos = new List<ListedNFTInfo>();
        foreach (var listedNftInfo in input.ListedNftInfoList.Value)
        {
            if (listedNftInfo.Price.Symbol != input.MakeOfferInput.Price.Symbol) continue;
            var isInTime = _context.CurrentBlockTime >= listedNftInfo.Duration.StartTime && 
                           _context.CurrentBlockTime >= listedNftInfo.Duration.PublicTime;
            if (!isInTime) continue;
            filteredListedNftInfos.Add(listedNftInfo);
        }
        filteredListedNftInfos.Sort(new PriceAmountComparer());

        foreach (var listedNftInfo in filteredListedNftInfos)        {
            if (listedNftInfo.Quantity >= needToDealQuantity)
            {
                var dealResult = new DealResult
                {
                    Symbol = input.MakeOfferInput.Symbol,
                    Quantity = needToDealQuantity,
                    PurchaseSymbol = input.MakeOfferInput.Price.Symbol,
                    PurchaseAmount = listedNftInfo.Price.Amount,
                    Duration = listedNftInfo.Duration,
                    Index = currentIndex
                };
                // Fulfill demands.
                dealResultList.Add(dealResult);
                needToDealQuantity = 0;
            }
            else
            {
                var dealResult = new DealResult
                {
                    Symbol = input.MakeOfferInput.Symbol,
                    Quantity = needToDealQuantity,
                    PurchaseSymbol = input.MakeOfferInput.Price.Symbol,
                    PurchaseAmount = listedNftInfo.Price.Amount,
                    Duration = listedNftInfo.Duration,
                    Index = currentIndex
                };
                dealResultList.Add(dealResult);
                needToDealQuantity = needToDealQuantity.Sub(listedNftInfo.Quantity);
            }

            if (needToDealQuantity == 0)
            {
                break;
            }

            currentIndex = currentIndex.Add(1);
        }

        return dealResultList;
    }

}

public class PriceAmountComparer : IComparer<ListedNFTInfo>
{
    public int Compare(ListedNFTInfo info1, ListedNFTInfo info2)
    {
        return info1.Price.Amount.CompareTo(info2.Price.Amount);
    }
}
public class StartTimeComparer : IComparer<ListedNFTInfo>
{
    public int Compare(ListedNFTInfo info1, ListedNFTInfo info2)
    {
        return info1.Duration.StartTime.CompareTo(info2.Duration.StartTime);
    }
}

public class GetDealResultListInput
{
    internal MakeOfferInput MakeOfferInput { get; set; }
    internal ListedNFTInfoList ListedNftInfoList{ get; set; }
}


public class DealResult
{
    internal string Symbol { get; set; }
    internal long Quantity{ get; set; }
    internal string PurchaseSymbol{ get; set; }
    internal long PurchaseAmount{ get; set; }
    internal ListDuration Duration { get; set; }
    internal int Index { get; set; }
}
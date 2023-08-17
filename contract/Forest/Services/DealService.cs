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

    public List<DealResult> GetDealResultList(GetDealResultListInput input)
    {
        var dealResultList = new List<DealResult>();
        var needToDealQuantity = input.MakeOfferInput.Quantity;
        var currentIndex = 0;
        var filteredListedNftInfos = new List<ListedNFTInfo>();
        var blockTime = _context.CurrentBlockTime;
        foreach (var listedNftInfo in input.ListedNftInfoList.Value)
        {
            if (listedNftInfo.Price.Symbol != input.MakeOfferInput.Price.Symbol) continue;
            var isInTime = blockTime >= listedNftInfo.Duration.StartTime && 
                           blockTime >= listedNftInfo.Duration.PublicTime;
            if (!isInTime) continue;
            filteredListedNftInfos.Add(listedNftInfo);
        }

        SortListedNftInfosByAmount(filteredListedNftInfos);

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
    
    public static void SortListedNftInfosByAmount(List<ListedNFTInfo> infos)
    {
        for (var i = 1; i < infos.Count; i++)
        {
            var key = infos[i];
            var j = i - 1;

            while (j >= 0 && infos[j].Price.Amount > key.Price.Amount)
            {
                infos[j + 1] = infos[j];
                j = j - 1;
            }
            infos[j + 1] = key;
        }
    }
    public static void SortListedNftInfosByStartTime(List<ListedNFTInfo> infos)
    {
        for (var i = 1; i < infos.Count; i++)
        {
            var key = infos[i];
            var j = i - 1;

            while (j >= 0 && infos[j].Duration.StartTime > key.Duration.StartTime)
            {
                infos[j + 1] = infos[j];
                j = j - 1;
            }
            infos[j + 1] = key;
        }
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
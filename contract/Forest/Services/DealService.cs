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
        var blockTime = _context.CurrentBlockTime;
        foreach (var listedNftInfo in input.ListedNftInfoList.Value.Where(i =>
                     i.Price.Symbol == input.MakeOfferInput.Price.Symbol
                     && blockTime >= i.Duration.StartTime
                     && blockTime >= i.Duration.PublicTime).OrderBy(i => i.Price.Amount))
        {
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

    public IEnumerable<DealResult> GetDealResultListForBatchBuy(string symbol, FixPriceList fixPriceList
        ,ListedNFTInfoList listedNftInfoList, out long needToDealQuantity)
    {
        var dealResultList = new List<DealResult>();
        needToDealQuantity = fixPriceList.Quantity;
        var currentIndex = 0;
        var blockTime = _context.CurrentBlockTime;
        foreach (var listedNftInfo in listedNftInfoList.Value.Where(i =>
                     i.Price.Symbol == fixPriceList.Price.Symbol
                     && blockTime >= i.Duration.StartTime
                     && blockTime >= i.Duration.PublicTime
                     ).OrderByDescending(i => i.Duration.PublicTime))
        {
            if (listedNftInfo.Quantity >= needToDealQuantity)
            {
                var dealResult = new DealResult
                {
                    Symbol = symbol,
                    Quantity = needToDealQuantity,
                    PurchaseSymbol = fixPriceList.Price.Symbol,
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
                    Symbol = symbol,
                    Quantity = needToDealQuantity,
                    PurchaseSymbol = fixPriceList.Price.Symbol,
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

public class GetDealResultListInput
{
    internal MakeOfferInput MakeOfferInput { get; set; }
    internal ListedNFTInfoList ListedNftInfoList { get; set; }
}

public class DealResult
{
    internal string Symbol { get; set; }
    internal long Quantity { get; set; }
    internal string PurchaseSymbol { get; set; }
    internal long PurchaseAmount { get; set; }
    internal ListDuration Duration { get; set; }
    internal int Index { get; set; }
}
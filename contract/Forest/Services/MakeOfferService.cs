using System.Collections.Generic;
using System.Linq;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core.Extension;
using AElf.Sdk.CSharp;
using AElf.Sdk.CSharp.State;
using AElf.Types;
using Forest.Helpers;
using Forest.Managers;
using Forest.Whitelist;

namespace Forest.Services;

internal class MakeOfferService
{
    private readonly TokenContractContainer.TokenContractReferenceState _tokenContract;
    private readonly WhitelistContractContainer.WhitelistContractReferenceState _whitelistContract;
    private readonly MappedState<Hash, Hash> _whitelistIdMap;
    private readonly MappedState<string, Address, ListedNFTInfoList> _listedNFTInfoListMap;
    private readonly WhitelistManager _whitelistManager;
    private readonly CSharpSmartContractContext _context;
    private readonly SingletonState<BizConfig> _bizConfig;

    public MakeOfferService(
        TokenContractContainer.TokenContractReferenceState tokenContract,
        MappedState<Hash, Hash> whitelistIdMap,
        MappedState<string, Address, ListedNFTInfoList> listedNFTInfoListMap,
        WhitelistManager whitelistManager,
        SingletonState<BizConfig> bizConfig,
        CSharpSmartContractContext context)
    {
        _tokenContract = tokenContract;
        _whitelistIdMap = whitelistIdMap;
        _listedNFTInfoListMap = listedNFTInfoListMap;
        _whitelistManager = whitelistManager;
        _context = context;
        _bizConfig = bizConfig;
    }

    public void ValidateOffer(MakeOfferInput makeOfferInput)
    {
        if (_context.Sender == makeOfferInput.OfferTo)
        {
            throw new AssertionException("Origin owner cannot be sender himself.");
        }
    }
    
    public void ValidateFixPriceList(FixPriceList input)
    {
        if (_context.Sender == input.OfferTo)
        {
            throw new AssertionException("Origin owner cannot be sender himself.");
        }
    }

    public bool IsSenderInWhitelist(MakeOfferInput makeOfferInput, out Hash whitelistId)
    {
        var projectId = WhitelistHelper.CalculateProjectId(makeOfferInput.Symbol, makeOfferInput.OfferTo);
        whitelistId = _whitelistIdMap[projectId];
        return whitelistId != null && _whitelistManager.IsAddressInWhitelist(_context.Sender, whitelistId);
    }

    public DealStatus GetDealStatus(MakeOfferInput makeOfferInput, out List<ListedNFTInfo> affordableNftInfoList)
    {
        affordableNftInfoList = new List<ListedNFTInfo>();
        var nftInfo = _tokenContract.GetTokenInfo.Call(new GetTokenInfoInput
        {
            Symbol = makeOfferInput.Symbol,
        });
        if (nftInfo.Supply == 0 && makeOfferInput.Quantity == 1)
        {
            // NFT not minted.
            return DealStatus.NFTNotMined;
        }

        if (nftInfo.Supply <= 0)
        {
            throw new AssertionException("NFT does not exist.");
        }

        var listedNftInfoList = _listedNFTInfoListMap[makeOfferInput.Symbol][makeOfferInput.OfferTo ?? nftInfo.Issuer];

        if (listedNftInfoList == null || listedNftInfoList.Value.All(i => i.ListType == ListType.NotListed))
        {
            // NFT not listed.
            return DealStatus.NotDeal;
        }

        affordableNftInfoList = GetAffordableNftInfoList(makeOfferInput, listedNftInfoList);
        if (!affordableNftInfoList.Any())
        {
            return DealStatus.NotDeal;
        }

        if (affordableNftInfoList.Count == 1 || affordableNftInfoList.First().Quantity >= makeOfferInput.Quantity)
        {
            return DealStatus.DealWithOnePrice;
        }

        return DealStatus.DealWithMultiPrice;
    }

    public void GetAffordableNftInfoList(string symbol, FixPriceList input,
        out List<ListedNFTInfo> affordableNftInfoList)
    {
        affordableNftInfoList = new List<ListedNFTInfo>();
        var nftInfo = _tokenContract.GetTokenInfo.Call(new GetTokenInfoInput
        {
            Symbol = symbol,
        });

        if (nftInfo.Supply <= 0)
        {
            throw new AssertionException("NFT does not exist.");
        }

        var listedNftInfoList = _listedNFTInfoListMap[symbol][input.OfferTo];

        if (listedNftInfoList == null)
        {
            return;
        }

        affordableNftInfoList = GetAffordableNftInfoListFixPrice(symbol, input, listedNftInfoList);
    }

    private List<ListedNFTInfo> GetAffordableNftInfoList(MakeOfferInput makeOfferInput,
        ListedNFTInfoList listedNftInfoList)
    {
        var blockTime = _context.CurrentBlockTime;
        var maxDealCount = _bizConfig.Value.MaxOfferDealCount;
        return listedNftInfoList.Value.Where(i =>
                (i.Price.Symbol == makeOfferInput.Price.Symbol && i.Price.Amount <= makeOfferInput.Price.Amount ||
                 i.ListType != ListType.FixedPrice)
                && blockTime <= i.Duration.StartTime.AddHours(i.Duration.DurationHours))
            .OrderBy(i => i.Price.Amount)
            .Take(maxDealCount > 0 ? maxDealCount : ForestContract.DefaultMaxOfferDealCount)
            .ToList();
    }

    private List<ListedNFTInfo> GetAffordableNftInfoListFixPrice(string symbol, FixPriceList input,
        ListedNFTInfoList listedNftInfoList)
    {
        var blockTime = _context.CurrentBlockTime;
        var maxDealCount = _bizConfig.Value.MaxOfferDealCount;
        return listedNftInfoList.Value.Where(i =>
                i.Price.Symbol == symbol && i.Price.Amount == input.Price.Amount
                                         && blockTime <= i.Duration.StartTime.AddHours(i.Duration.DurationHours)
                                         && input.StartTime == i.Duration.StartTime)
            .OrderBy(i => i.Duration.StartTime)
            .Take(maxDealCount > 0 ? maxDealCount : ForestContract.DefaultMaxOfferDealCount)
            .ToList();
    }
}

public enum DealStatus
{
    NFTNotMined,
    NotDeal,
    DealWithOnePrice,
    DealWithMultiPrice,
}
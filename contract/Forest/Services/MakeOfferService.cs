using System.Collections.Generic;
using System.Linq;
using AElf.Contracts.MultiToken;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core.Extension;
using AElf.Sdk.CSharp;
using AElf.Sdk.CSharp.State;
using AElf.Types;
using Forest.Helpers;
using Forest.Managers;
using Forest.Whitelist;
using Google.Protobuf.WellKnownTypes;

namespace Forest.Services;

internal class MakeOfferService
{
    private readonly TokenContractContainer.TokenContractReferenceState _tokenContract;
    private readonly WhitelistContractContainer.WhitelistContractReferenceState _whitelistContract;
    private readonly MappedState<Hash, Hash> _whitelistIdMap;
    private readonly MappedState<string, Address, ListedNFTInfoList> _listedNFTInfoListMap;
    private readonly WhitelistManager _whitelistManager;
    private readonly CSharpSmartContractContext _context;

    public MakeOfferService(
        TokenContractContainer.TokenContractReferenceState tokenContract,
        MappedState<Hash, Hash> whitelistIdMap,
        MappedState<string, Address, ListedNFTInfoList> listedNFTInfoListMap,
        WhitelistManager whitelistManager,
        CSharpSmartContractContext context)
    {
        _tokenContract = tokenContract;
        _whitelistIdMap = whitelistIdMap;
        _listedNFTInfoListMap = listedNFTInfoListMap;
        _whitelistManager = whitelistManager;
        _context = context;
    }

    public void ValidateOffer(MakeOfferInput makeOfferInput)
    {
        if (_context.Sender == makeOfferInput.OfferTo)
        {
            throw new AssertionException("Origin owner cannot be sender himself.");
        }
    }

    public bool IsSenderInWhitelist(MakeOfferInput makeOfferInput,out Hash whitelistId)
    {
        var projectId =
            WhitelistHelper.CalculateProjectId(makeOfferInput.Symbol, makeOfferInput.OfferTo);
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

        var listedNftInfoList =
            _listedNFTInfoListMap[makeOfferInput.Symbol][
                makeOfferInput.OfferTo ?? nftInfo.Issuer];
        if (listedNftInfoList == null)
        {
            return DealStatus.NotDeal;
        }

        var allNotListed = true;
        foreach (var info in listedNftInfoList.Value)
        {
            if (info.ListType == ListType.NotListed) continue;
            allNotListed = false;
            break;
        }
        if (allNotListed)
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

    private List<ListedNFTInfo> GetAffordableNftInfoList(MakeOfferInput makeOfferInput,
        ListedNFTInfoList listedNftInfoList)
    {
        var affordableList = new List<ListedNFTInfo>();

        foreach (var info in listedNftInfoList.Value)
        {
            var isAffordable = (info.Price.Symbol == makeOfferInput.Price.Symbol && info.Price.Amount <= makeOfferInput.Price.Amount) ||
                               info.ListType != ListType.FixedPrice;
            var isTimedOut = _context.CurrentBlockTime > info.Duration.StartTime.AddHours(info.Duration.DurationHours);
            if (isAffordable && !isTimedOut)
            {
                affordableList.Add(info);
            }
        }
        affordableList.Sort(new PriceAmountComparer());
        return affordableList;
    }
    
}

public enum DealStatus
{
    NFTNotMined,
    NotDeal,
    DealWithOnePrice,
    DealWithMultiPrice,
}
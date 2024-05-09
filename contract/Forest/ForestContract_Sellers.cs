using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Forest.Helpers;
using Forest.Whitelist;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;

namespace Forest;

public partial class ForestContract
{
    public override Empty ListWithFixedPrice(ListWithFixedPriceInput input)
    {
        AssertContractInitialized();
        AssertWhitelistContractInitialized();
        Assert(input.Price.Amount > 0, "Incorrect listing price.");
        Assert(input.Quantity > 0, "Incorrect quantity.");

        var tokenInfo = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
        {
            Symbol = input.Symbol
        });
        Assert(!string.IsNullOrWhiteSpace(tokenInfo.Symbol), "this NFT Info not exists.");

        var balance = State.TokenContract.GetBalance.Call(new GetBalanceInput
        {
            Symbol = input.Symbol,
            Owner = Context.Sender
        });
        Assert(balance.Balance >= input.Quantity, "Check sender NFT balance failed.");
        
        AssertAllowanceInsufficient(input.Symbol, Context.Sender, input.Quantity);
        
        var duration = AdjustListDuration(input.Duration);
        var whitelists = input.Whitelists;
        var projectId = CalculateProjectId(input.Symbol, Context.Sender);
        var whitelistId = new Hash();
        var whitelistManager = GetWhitelistManager();

        var listedNftInfoList = State.ListedNFTInfoListMap[input.Symbol][Context.Sender] ?? new ListedNFTInfoList();
        Assert(listedNftInfoList.Value.Count < State.BizConfig.Value.MaxListCount,
            $"The current listings have reached the maximum ({State.BizConfig.Value.MaxListCount}).");
        Assert(listedNftInfoList.Value.All(i => i.Duration.StartTime.Seconds != duration.StartTime.Seconds),
            "List info already exists");

        var tokenWhiteList = GetTokenWhiteList(input.Symbol).Value;
        Assert(tokenWhiteList.Contains(input.Price.Symbol), $"{input.Price.Symbol} is not in token white list.");

        if (input.IsWhitelistAvailable)
        {
            foreach (var whitelistInfo in input.Whitelists?.Whitelists ?? new RepeatedField<WhitelistInfo>())
            {
                Assert(tokenWhiteList.Contains(whitelistInfo.PriceTag.Price.Symbol),
                    $"Invalid price symbol {whitelistInfo.PriceTag.Price.Symbol} in whitelist priceTag");
            }

            var extraInfoList = ConvertToExtraInfo(whitelists);
            //Listed for the first time, create whitelist.
            if (State.WhitelistIdMap[projectId] == null)
            {
                whitelistId = Context.GenerateId(State.WhitelistContract.Value,
                    ByteArrayHelper.ConcatArrays(Context.Self.ToByteArray(), projectId.ToByteArray()));

                whitelistManager.CreateWhitelist(new CreateWhitelistInput
                {
                    ProjectId = projectId,
                    StrategyType = StrategyType.Price,
                    Creator = Context.Self,
                    ExtraInfoList = extraInfoList,
                    IsCloneable = true,
                    Remark = $"{input.Symbol}",
                    ManagerList = new Whitelist.AddressList
                    {
                        Value = { Context.Sender }
                    }
                });
                State.WhitelistIdMap[projectId] = whitelistId;
            }
            else
            {
                whitelistId = State.WhitelistIdMap[projectId];
                if (!whitelistManager.IsWhitelistAvailable(whitelistId))
                {
                    State.WhitelistContract.EnableWhitelist.Send(whitelistId);
                }

                //Add address list to the existing whitelist.
                whitelistId = ExistWhitelist(projectId, whitelists, extraInfoList);
            }
        }
        else
        {
            whitelistId = State.WhitelistIdMap[projectId];
            if (whitelistId != null && whitelistManager.IsWhitelistAvailable(whitelistId))
            {
                State.WhitelistContract.DisableWhitelist.Send(whitelistId);
            }
        }

        var listedNftInfo = new ListedNFTInfo
        {
            ListType = ListType.FixedPrice,
            Owner = Context.Sender,
            Price = input.Price,
            Quantity = input.Quantity,
            Symbol = input.Symbol,
            Duration = duration,
        };
        listedNftInfoList.Value.Add(listedNftInfo);
        Context.Fire(new ListedNFTAdded
        {
            Symbol = input.Symbol,
            Duration = duration,
            Owner = Context.Sender,
            Price = input.Price,
            Quantity = input.Quantity,
            WhitelistId = whitelistId
        });

        State.ListedNFTInfoListMap[input.Symbol][Context.Sender] = listedNftInfoList;

        Context.Fire(new FixedPriceNFTListed
        {
            Owner = listedNftInfo.Owner,
            Price = listedNftInfo.Price,
            Quantity = input.Quantity,
            Symbol = listedNftInfo.Symbol,
            Duration = listedNftInfo.Duration,
            //IsMergedToPreviousListedInfo = isMergedToPreviousListedInfo,
            WhitelistId = whitelistId
        });

        return new Empty();
    }

    public override Empty Delist(DelistInput input)
    {
        Assert(input.Quantity > 0, "Quantity must be a positive integer.");
        var listedNftInfoList = State.ListedNFTInfoListMap[input.Symbol][Context.Sender];
        if (listedNftInfoList == null || listedNftInfoList.Value.All(i => i.ListType == ListType.NotListed))
        {
            throw new AssertionException("Listed NFT Info not exists. (Or already delisted.)");
        }

        Assert(input.Price != null, "Need to specific list record.");
        var listedNftInfo = listedNftInfoList.Value.FirstOrDefault(i =>
            i.Price.Amount == input.Price.Amount && i.Price.Symbol == input.Price.Symbol &&
            i.Owner == Context.Sender &&
            (input.StartTime == null ? true : input.StartTime.Seconds == i.Duration.StartTime.Seconds));
        
        if (listedNftInfo == null)
        {
            throw new AssertionException("Listed NFT Info not exists. (Or already delisted.)");
        }

        input.Quantity = input.Quantity > listedNftInfo.Quantity
            ? listedNftInfo.Quantity
            : input.Quantity;

        var projectId = CalculateProjectId(input.Symbol, Context.Sender);
        var whitelistId = State.WhitelistIdMap[projectId];

        switch (listedNftInfo.ListType)
        {
            case ListType.FixedPrice when input.Quantity >= listedNftInfo.Quantity:
                State.ListedNFTInfoListMap[input.Symbol][Context.Sender].Value.Remove(listedNftInfo);
                Context.Fire(new ListedNFTRemoved
                {
                    Symbol = listedNftInfo.Symbol,
                    Duration = listedNftInfo.Duration,
                    Owner = listedNftInfo.Owner,
                    Price = listedNftInfo.Price
                });
                break;
            case ListType.FixedPrice:
                listedNftInfo.Quantity = listedNftInfo.Quantity.Sub(input.Quantity);
                State.ListedNFTInfoListMap[input.Symbol][Context.Sender] = listedNftInfoList;
                Context.Fire(new ListedNFTChanged
                {
                    Symbol = listedNftInfo.Symbol,
                    Duration = listedNftInfo.Duration,
                    Owner = listedNftInfo.Owner,
                    Price = listedNftInfo.Price,
                    Quantity = listedNftInfo.Quantity,
                    WhitelistId = whitelistId
                });
                break;
        }

        Context.Fire(new NFTDelisted
        {
            Symbol = input.Symbol,
            Owner = Context.Sender,
            Quantity = input.Quantity
        });

        return new Empty();
    }

    /// <summary>
    /// Batch delete 
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    /// <exception cref="AssertionException"></exception>
    public override Empty BatchDeList(BatchDeListInput input)
    {
        AssertContractInitialized();
        Assert(input.Price != null, "Need to specific list price.");
        Assert(input.Price.Amount > 0, "Incorrect listing price.");
        Assert(input.BatchDelistType != null, "Incorrect listing batchDelistType.");
        var tokenInfo = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
        {
            Symbol = input.Symbol
        });
        Assert(!string.IsNullOrWhiteSpace(tokenInfo.Symbol), "this NFT Info not exists.");

        var listedNftInfoList = State.ListedNFTInfoListMap[input.Symbol][Context.Sender];
        if (listedNftInfoList == null)
        {
            return new Empty();
        }

        var fixedPriceListedNftInfoList =
            listedNftInfoList.Value.Where(i => i.ListType == ListType.FixedPrice 
                                               && i.Price.Symbol == input.Price.Symbol).ToList();

        if (fixedPriceListedNftInfoList == null || !fixedPriceListedNftInfoList.Any())
        {
            return new Empty();
        }

        switch (input.BatchDelistType)
        {
            case BatchDeListTypeGreaterThan:
                fixedPriceListedNftInfoList = fixedPriceListedNftInfoList
                    .Where(i => (i.Price.Amount > input.Price.Amount)).ToList();
                break;
            case BatchDeListTypeGreaterThanOrEquals:
                fixedPriceListedNftInfoList = fixedPriceListedNftInfoList
                    .Where(i => (i.Price.Amount >= input.Price.Amount)).ToList();
                break;
            case BatchDeListTypeLessThan:
                fixedPriceListedNftInfoList = fixedPriceListedNftInfoList
                    .Where(i => (i.Price.Amount < input.Price.Amount)).ToList();
                break;
            case BatchDeListTypeLessThanOrEquals:
                fixedPriceListedNftInfoList = fixedPriceListedNftInfoList
                    .Where(i => (i.Price.Amount <= input.Price.Amount)).ToList();
                break;
            default:
                throw new AssertionException("BatchDeListType not exists.");
        }

        if (fixedPriceListedNftInfoList == null || !fixedPriceListedNftInfoList.Any())
        {
            return new Empty();
        }

        foreach (var listedNftInfo in fixedPriceListedNftInfoList)
        {
            var projectId = CalculateProjectId(input.Symbol, Context.Sender);
            var whitelistId = State.WhitelistIdMap[projectId];
            State.ListedNFTInfoListMap[input.Symbol][Context.Sender].Value.Remove(listedNftInfo);
            Context.Fire(new ListedNFTRemoved
            {
                Symbol = listedNftInfo.Symbol,
                Duration = listedNftInfo.Duration,
                Owner = listedNftInfo.Owner,
                Price = listedNftInfo.Price
            });
        }

        return new Empty();
    }

    /// <summary>
    /// Sender is the seller.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    /// <exception cref="AssertionException"></exception>
    public override Empty Deal(DealInput input)
    {
        AssertContractInitialized();

        Assert(input.Symbol != null, "Incorrect symbol.");
        Assert(input.OfferFrom != null, "Incorrect offer maker.");
        if (input.Price?.Symbol == null)
        {
            throw new AssertionException("Incorrect price.");
        }

        var balance = State.TokenContract.GetBalance.Call(new GetBalanceInput
        {
            Symbol = input.Symbol,
            Owner = Context.Sender
        });
        Assert(balance.Balance >= input.Quantity, "Insufficient NFT balance.");
        
        AssertAllowanceInsufficient(input.Symbol, Context.Sender, input.Quantity);
        var nftInfo = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
        {
            Symbol = input.Symbol,
        });
        Assert(nftInfo != null && !string.IsNullOrWhiteSpace(nftInfo.Symbol), "Invalid symbol data");

        var offer = State.OfferListMap[input.Symbol][input.OfferFrom]?.Value
            .FirstOrDefault(o => o.From == input.OfferFrom
                                 && (o.To == Context.Sender || o.To == nftInfo?.Issuer)
                                 && o.Price.Symbol == input.Price.Symbol
                                 && o.Price.Amount == input.Price.Amount
                                 && o.ExpireTime >= Context.CurrentBlockTime);
        
        if (offer == null)
        {
            Assert(false, "offer is empty");
            return new Empty();
        }

        Assert(offer.Quantity >= input.Quantity, "Deal quantity exceeded.");
        offer.Quantity = offer.Quantity.Sub(input.Quantity);
        ModifyOfferTotalAmount(input.OfferFrom, input.Price.Symbol, -NumberHelper
            .DivideByPowerOfTen(input.Quantity, nftInfo.Decimals)
            .Mul(input.Price.Amount));
        if (offer.Quantity == 0)
        {
            State.OfferListMap[input.Symbol][input.OfferFrom].Value.Remove(offer);
            Context.Fire(new OfferRemoved
            {
                Symbol = input.Symbol,
                OfferFrom = input.OfferFrom,
                OfferTo = offer.To,
                ExpireTime = offer.ExpireTime,
                Price = new Price()
                {
                    Amount = offer.Price.Amount,
                    Symbol = offer.Price.Symbol
                }
            });
        }
        else
        {
            Context.Fire(new OfferChanged
            {
                Symbol = input.Symbol,
                OfferFrom = input.OfferFrom,
                OfferTo = offer.To,
                Quantity = offer.Quantity,
                Price = offer.Price,
                ExpireTime = offer.ExpireTime
            });
        }

        var price = offer.Price;
        var totalAmount = price.Amount.Mul(NumberHelper.DivideByPowerOfTen(input.Quantity, nftInfo.Decimals));

        PerformDeal(new PerformDealInput
        {
            NFTFrom = Context.Sender,
            NFTTo = offer?.From,
            NFTSymbol = input.Symbol,
            NFTQuantity = input.Quantity,
            PurchaseSymbol = price.Symbol,
            PurchaseAmount = totalAmount,
        });
        return new Empty();
    }
}
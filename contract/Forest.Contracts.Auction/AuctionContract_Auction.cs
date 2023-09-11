using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElf.Sdk.CSharp;
using Google.Protobuf.WellKnownTypes;

namespace Forest.Contracts.Auction;

public partial class AuctionContract
{
    public override Empty CreateAuction(CreateAuctionInput input)
    {
        AssertInitialize();
        AssertAuctionController();
        Assert(input != null, "Invalid input.");
        Assert(!string.IsNullOrWhiteSpace(input.Symbol), "Invalid input symbol.");
        AssertSymbol(input.Symbol);
        Assert(input.ReceivingAddress == null || !input.ReceivingAddress.Value.IsNullOrEmpty(),
            "Invalid input receiving address.");
        Assert(input.AuctionType == AuctionType.English, "Invalid input auction type.");
        AssertInputPrice(input.StartPrice);
        AssertAuctionConfig(input.AuctionConfig);

        var auctionConfig = input.AuctionConfig;

        var auctionId = GenerateAuctionId(input.Symbol);
        
        Assert(State.AuctionInfoMap[auctionId] == null, "Auction already exist.");

        var auctionInfo = new AuctionInfo
        {
            AuctionId = auctionId,
            AuctionConfig = auctionConfig,
            AuctionType = input.AuctionType,
            Symbol = input.Symbol,
            StartPrice = input.StartPrice,
            ReceivingAddress = input.ReceivingAddress ?? Context.Sender,
            Creator = Context.Sender
        };

        if (input.AuctionConfig.StartImmediately)
        {
            InitAuctionTime(auctionInfo);
        }

        State.AuctionInfoMap[auctionId] = auctionInfo;

        TransferTokenFromCreator(new Price
        {
            Amount = AuctionContractConstants.DefaultAmount,
            Symbol = auctionInfo.Symbol
        });

        Context.Fire(new AuctionCreated
        {
            Creator = auctionInfo.Creator,
            AuctionId = auctionInfo.AuctionId,
            StartPrice = auctionInfo.StartPrice,
            StartTime = auctionInfo.StartTime,
            EndTime = auctionInfo.EndTime,
            MaxEndTime = auctionInfo.MaxEndTime,
            AuctionType = auctionInfo.AuctionType,
            Symbol = auctionInfo.Symbol,
            AuctionConfig = auctionInfo.AuctionConfig,
            ReceivingAddress = auctionInfo.ReceivingAddress
        });

        return new Empty();
    }

    public override Empty PlaceBid(PlaceBidInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(input.AuctionId != null && !input.AuctionId.Value.IsNullOrEmpty(), "Invalid input auction id.");
        AssertInputPrice(input.Price);

        var auctionInfo = State.AuctionInfoMap[input.AuctionId];

        Assert(auctionInfo != null, "Auction not exist.");

        switch (auctionInfo.AuctionType)
        {
            case AuctionType.English:
                PlaceBidForEnglishAuction(input, auctionInfo);
                break;
        }

        auctionInfo = State.AuctionInfoMap[input.AuctionId];

        Context.Fire(new BidPlaced
        {
            AuctionId = auctionInfo.AuctionId,
            Bidder = auctionInfo.LastBidInfo.Bidder,
            Price = auctionInfo.LastBidInfo.Price,
            BidTime = auctionInfo.LastBidInfo.BidTime
        });

        return new Empty();
    }

    public override Empty Claim(ClaimInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(input.AuctionId != null && !input.AuctionId.Value.IsNullOrEmpty(), "Invalid input auction id.");

        var auctionInfo = State.AuctionInfoMap[input.AuctionId];
        Assert(auctionInfo != null, "Auction not exist.");
        Assert(auctionInfo.StartTime != null, "Auction not start yet.");

        var currentBlockTime = Context.CurrentBlockTime;
        Assert(currentBlockTime >= auctionInfo.EndTime, "Auction not end yet.");
        
        auctionInfo.FinishTime = currentBlockTime;

        TransferTokenToBidder(auctionInfo);
        TransferToReceivingAccount(auctionInfo);

        Context.Fire(new Claimed
        {
            AuctionId = auctionInfo.AuctionId,
            Bidder = auctionInfo.LastBidInfo.Bidder,
            FinishTime = currentBlockTime
        });

        return new Empty();
    }

    private void PlaceBidForEnglishAuction(PlaceBidInput input, AuctionInfo auctionInfo)
    {
        var auctionConfig = auctionInfo.AuctionConfig;

        var currentBlockTime = Context.CurrentBlockTime;

        if (auctionInfo.StartTime == null)
        {
            InitAuctionTime(auctionInfo);

            FireAuctionTimeUpdated(auctionInfo);
        }

        Assert(currentBlockTime < auctionInfo.EndTime, "Auction finished. Bid failed.");

        var bidInfo = auctionInfo.LastBidInfo;

        AssertBidPrice(
            bidInfo?.Bidder == null ? auctionInfo.StartPrice : bidInfo.Price, input.Price, auctionConfig.MinMarkup);

        Refund(bidInfo);

        bidInfo = new BidInfo
        {
            Bidder = Context.Sender,
            Price = input.Price,
            BidTime = currentBlockTime
        };

        TransferFromBidder(bidInfo);

        // Extend auction end time when bid in countdown time
        if (currentBlockTime.AddSeconds(auctionConfig.CountdownTime) >= auctionInfo.EndTime)
        {
            var newEndTime = auctionInfo.EndTime.AddSeconds(auctionConfig.CountdownTime);
            auctionInfo.EndTime = newEndTime > auctionInfo.MaxEndTime ? auctionInfo.MaxEndTime : newEndTime;

            FireAuctionTimeUpdated(auctionInfo);
        }

        auctionInfo.LastBidInfo = bidInfo;
        State.AuctionInfoMap[input.AuctionId] = auctionInfo;
    }

    private void Refund(BidInfo bidInfo)
    {
        if (bidInfo != null)
        {
            State.TokenContract.Transfer.Send(new TransferInput
            {
                Symbol = bidInfo.Price.Symbol,
                Amount = bidInfo.Price.Amount,
                To = bidInfo.Bidder,
                Memo = "Refund"
            });
        }
    }

    private void TransferTokenFromCreator(Price price)
    {
        State.TokenContract.TransferFrom.Send(new TransferFromInput
        {
            From = Context.Sender,
            To = Context.Self,
            Amount = price.Amount,
            Symbol = price.Symbol,
            Memo = "Auction"
        });
    }

    private void TransferFromBidder(BidInfo bidInfo)
    {
        State.TokenContract.TransferFrom.Send(new TransferFromInput
        {
            From = bidInfo.Bidder,
            To = Context.Self,
            Amount = bidInfo.Price.Amount,
            Symbol = bidInfo.Price.Symbol,
            Memo = "Auction"
        });
    }

    private void TransferTokenToBidder(AuctionInfo auctionInfo)
    {
        State.TokenContract.Transfer.Send(new TransferInput
        {
            Symbol = auctionInfo.Symbol,
            Amount = AuctionContractConstants.DefaultAmount,
            To = auctionInfo.LastBidInfo.Bidder,
            Memo = "Auction"
        });
    }

    private void TransferToReceivingAccount(AuctionInfo auctionInfo)
    {
        State.TokenContract.Transfer.Send(new TransferInput
        {
            Symbol = auctionInfo.LastBidInfo.Price.Symbol,
            Amount = auctionInfo.LastBidInfo.Price.Amount,
            To = auctionInfo.ReceivingAddress,
            Memo = "Auction"
        });
    }

    private void AssertBidPrice(Price lastPrice, Price inputPrice, int minMarkup)
    {
        Assert(inputPrice.Symbol == lastPrice.Symbol, "Invalid input price symbol.");
        Assert(inputPrice.Amount > lastPrice.Amount, "Bid price not high enough.");

        var threshold = lastPrice.Amount.Mul(100 + minMarkup).Div(100);
        Assert(inputPrice.Amount >= threshold, "Bid price not high enough.");
    }

    private void FireAuctionTimeUpdated(AuctionInfo auctionInfo)
    {
        Context.Fire(new AuctionTimeUpdated
        {
            AuctionId = auctionInfo.AuctionId,
            StartTime = auctionInfo.StartTime,
            EndTime = auctionInfo.EndTime,
            MaxEndTime = auctionInfo.MaxEndTime
        });
    }

    private void AssertAuctionConfig(AuctionConfig input)
    {
        Assert(input != null, "Invalid input auction config.");
        Assert(input.Duration > 0, "Invalid input duration.");
        Assert(input.CountdownTime >= 0, "Invalid input countdown time.");
        Assert(input.MaxExtensionTime >= 0, "Invalid input max extension time.");
        Assert(input.MinMarkup >= 0, "Invalid input min markup.");
    }

    private void InitAuctionTime(AuctionInfo auctionInfo)
    {
        var currentBlockTime = Context.CurrentBlockTime;
        var auctionConfig = auctionInfo.AuctionConfig;
        
        auctionInfo.StartTime = currentBlockTime;
        auctionInfo.EndTime = currentBlockTime.AddSeconds(auctionConfig.Duration);
        auctionInfo.MaxEndTime =
            currentBlockTime.AddSeconds(auctionConfig.Duration.Add(auctionConfig.MaxExtensionTime));
    }
}
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Forest.Contracts.Auction;

public partial class AuctionContract
{
    public override Empty CreateAuction(CreateAuctionInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(!string.IsNullOrWhiteSpace(input.Symbol), "Invalid input symbol.");
        Assert(input.Amount > 0, "Invalid input amount");
        Assert(input.ReceivingAddress == null || !input.ReceivingAddress.Value.IsNullOrEmpty(),
            "Invalid input receiving address.");
        AssertInputPrice(input.StartPrice);
        AssertAuctionConfig(input.AuctionConfig);
        
        var auctionConfig = input.AuctionConfig;
        var currentBlockTime = Context.CurrentBlockTime;

        var auctionId = HashHelper.ConcatAndCompute(Context.TransactionId, HashHelper.ComputeFrom(input.Symbol));
        
        var auctionInfo = new AuctionInfo
        {
            AuctionId = auctionId,
            AuctionConfig = auctionConfig,
            AuctionType = input.AuctionType,
            Symbol = input.Symbol,
            Amount = input.Amount,
            StartPrice = input.StartPrice,
            ReceivingAddress = input.ReceivingAddress ?? Context.Sender
        };

        if (input.AuctionConfig.StartImmediately)
        {
            auctionInfo.StartTime = currentBlockTime;
            auctionInfo.EndTime = currentBlockTime.AddSeconds(auctionConfig.Duration);
            auctionInfo.MaxEndTime = currentBlockTime.AddSeconds(auctionConfig.Duration.Add(auctionConfig.MaxExtensionTime));
        }

        State.AuctionInfoMap[auctionId] = auctionInfo;
        
        TransferFromCreator(new Price
        {
            Amount = auctionInfo.Amount,
            Symbol = auctionInfo.Symbol
        });

        Context.Fire(new AuctionCreated
        {
            Creator = Context.Sender,
            AuctionId = auctionInfo.AuctionId,
            StartPrice = auctionInfo.StartPrice,
            StartTime = auctionInfo.StartTime,
            EndTime = auctionInfo.EndTime,
            MaxEndTime = auctionInfo.MaxEndTime,
            AuctionType = auctionInfo.AuctionType,
            Symbol = auctionInfo.Symbol,
            Amount = auctionInfo.Amount,
            AuctionConfig = auctionInfo.AuctionConfig,
            ReceivingAddress = auctionInfo.ReceivingAddress
        });

        return new Empty();
    }

    public override Empty PlaceBid(PlaceBidInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(input.AuctionId != null && !input.AuctionId.Value.IsNullOrEmpty() , "Invalid input auction id.");
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
            AuctionId = input.AuctionId,
            Bidder = Context.Sender,
            Price = input.Price,
            BidTime = Context.CurrentBlockTime
        });

        return new Empty();
    }

    public override Empty Claim(ClaimInput input)
    {
        Assert(input != null, "Invalid input.");
        Assert(input.AuctionId != null && !input.AuctionId.Value.IsNullOrEmpty(), "Invalid input auction id.");

        var auctionInfo = State.AuctionInfoMap[input.AuctionId];
        Assert(auctionInfo != null, "Auction not exist.");

        var currentBlockTime = Context.CurrentBlockTime;
        auctionInfo.FinishTime = currentBlockTime;
        
        TransferToBidder(auctionInfo);
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
        Assert(currentBlockTime < auctionInfo.EndTime, "Auction finished. Bid failed.");

        if (auctionInfo.StartTime == null)
        {
            auctionInfo.StartTime = currentBlockTime;
            auctionInfo.EndTime = currentBlockTime.AddSeconds(auctionConfig.Duration);
            auctionInfo.MaxEndTime =
                currentBlockTime.AddSeconds(auctionConfig.Duration.Add(auctionConfig.MaxExtensionTime));

            FireAuctionTimeUpdated(auctionInfo);
        }

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

    private void TransferFromCreator(Price price)
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

    private void TransferToBidder(AuctionInfo auctionInfo)
    {
        State.TokenContract.Transfer.Send(new TransferInput
        {
            Symbol = auctionInfo.Symbol,
            Amount = auctionInfo.Amount,
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

    private void AssertInputPrice(Price input)
    {
        Assert(input != null && !string.IsNullOrWhiteSpace(input.Symbol) &&
               input.Amount > 0, "Invalid input price.");
    }
    
    private void AssertAuctionConfig(AuctionConfig input)
    {
        Assert(input != null, "Invalid input auction config");
        Assert(input.Duration > 0, "Invalid input duration.");
        Assert(input.CountdownTime >= 0, "Invalid input countdown time.");
        Assert(input.MaxExtensionTime >= 0, "Invalid input max extension time.");
        Assert(input.MinMarkup > 0, "Invalid input min markup.");
    }
}
using AElf.CSharp.Core.Extension;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Forest.Contracts.Auction;

public static class Extensions
{
    public static AuctionCreated CreateAuctionCreatedEvent(this AuctionInfo self)
    {
        return new AuctionCreated
        {
            Creator = self.Creator,
            AuctionId = self.AuctionId,
            StartPrice = self.StartPrice,
            StartTime = self.StartTime,
            EndTime = self.EndTime,
            MaxEndTime = self.MaxEndTime,
            AuctionType = self.AuctionType,
            Symbol = self.Symbol,
            AuctionConfig = self.AuctionConfig,
            ReceivingAddress = self.ReceivingAddress
        };
    }

    public static BidPlaced CreateBidPlacedEvent(this AuctionInfo self)
    {
        return new BidPlaced
        {
            AuctionId = self.AuctionId,
            Bidder = self.LastBidInfo.Bidder,
            Price = self.LastBidInfo.Price,
            BidTime = self.LastBidInfo.BidTime
        };
    }

    public static Claimed CreateClaimedEvent(this AuctionInfo self)
    {
        return new Claimed
        {
            AuctionId = self.AuctionId,
            Bidder = self.LastBidInfo.Bidder,
            FinishTime = self.FinishTime
        };
    }

    public static AuctionTimeUpdated CreateAuctionTimeUpdatedEvent(this AuctionInfo self)
    {
        return new AuctionTimeUpdated
        {
            AuctionId = self.AuctionId,
            StartTime = self.StartTime,
            EndTime = self.EndTime,
            MaxEndTime = self.MaxEndTime
        };
    }

    public static AuctionInfo CreateAuctionInfo(CreateAuctionInput input, Address sender)
    {
        return new AuctionInfo
        {
            AuctionConfig = input.AuctionConfig,
            AuctionType = input.AuctionType,
            Symbol = input.Symbol,
            StartPrice = input.StartPrice,
            ReceivingAddress = input.ReceivingAddress ?? sender,
            Creator = sender
        };
    }

    public static bool IsStartImmediately(this AuctionInfo self)
    {
        return self.AuctionConfig.StartImmediately;
    }

    public static bool IsAuctionStarted(this AuctionInfo self)
    {
        return self.StartTime != null;
    }

    public static AuctionInfo SetAuctionId(this AuctionInfo self, Hash auctionId)
    {
        self.AuctionId = auctionId;
        return self;
    }

    public static AuctionInfo SetAuctionTime(this AuctionInfo self, Timestamp currentBlockTime)
    {
        self.StartTime = currentBlockTime;
        self.EndTime = currentBlockTime.AddSeconds(self.AuctionConfig.Duration);
        self.MaxEndTime = self.EndTime.AddSeconds(self.AuctionConfig.MaxExtensionTime);

        return self;
    }

    public static AuctionInfo SetFinishTime(this AuctionInfo self, Timestamp currentBlockTime)
    {
        self.FinishTime = currentBlockTime;

        return self;
    }

    public static void ExtendEndTime(this AuctionInfo self)
    {
        var newEndTime = self.EndTime.AddSeconds(self.AuctionConfig.CountdownTime);
        self.EndTime = newEndTime > self.MaxEndTime ? self.MaxEndTime : newEndTime;
    }

    public static AuctionInfo UpdateBidInfo(this AuctionInfo self, BidInfo bidInfo)
    {
        self.LastBidInfo = bidInfo;

        return self;
    }

    public static bool IsInCountdownTime(this AuctionInfo self, Timestamp currentBlockTime)
    {
        return currentBlockTime.AddSeconds(self.AuctionConfig.CountdownTime) >= self.EndTime;
    }
    
    public static bool IsAuctionFinished(this AuctionInfo self, Timestamp currentBlockTime)
    {
        return self.EndTime <= currentBlockTime;
    }
}
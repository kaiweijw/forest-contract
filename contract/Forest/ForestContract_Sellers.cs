using System.Linq;
using AElf;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Forest.Whitelist;
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
            var duration = AdjustListDuration(input.Duration);
            var whitelists = input.Whitelists;
            var projectId = CalculateProjectId(input.Symbol,Context.Sender);
            var whitelistId = new Hash();
            var whitelistManager = GetWhitelistManager();
            if (input.IsWhitelistAvailable)
            {
                var extraInfoList = ConvertToExtraInfo(whitelists);
                //Listed for the first time, create whitelist.
                if (State.WhitelistIdMap[projectId] == null)
                {
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
                            Value = {Context.Sender}
                        }
                    });
                    whitelistId =
                        Context.GenerateId(State.WhitelistContract.Value,
                            ByteArrayHelper.ConcatArrays(Context.Self.ToByteArray(), projectId.ToByteArray()));
                    State.WhitelistIdMap[projectId] = whitelistId;
                }
                else
                {
                    //Add address list to the existing whitelist.
                    whitelistId = ExistWhitelist(projectId,whitelists,extraInfoList);
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

            Assert(GetTokenWhiteList(input.Symbol).Value.Contains(input.Price.Symbol),
                $"{input.Price.Symbol} is not in token white list.");

            var listedNftInfoList = State.ListedNFTInfoListMap[input.Symbol][Context.Sender] ??
                                    new ListedNFTInfoList();
            ListedNFTInfo listedNftInfo;
            listedNftInfo = listedNftInfoList.Value.FirstOrDefault(i =>
                i.Price.Symbol == input.Price.Symbol && i.Price.Amount == input.Price.Amount &&
                i.Owner == Context.Sender && i.Duration.StartTime == input.Duration.StartTime &&
                i.Duration.PublicTime == input.Duration.PublicTime &&
                i.Duration.DurationHours == input.Duration.DurationHours);

            bool isMergedToPreviousListedInfo;
            if (listedNftInfo == null)
            {
                listedNftInfo = new ListedNFTInfo
                {
                    ListType = ListType.FixedPrice,
                    Owner = Context.Sender,
                    Price = input.Price,
                    Quantity = input.Quantity,
                    Symbol = input.Symbol,
                    Duration = duration,
                };
                listedNftInfoList.Value.Add(listedNftInfo);
                isMergedToPreviousListedInfo = false;
                Context.Fire(new ListedNFTAdded
                {
                    Symbol = input.Symbol,
                    Duration = duration,
                    Owner = Context.Sender,
                    Price = input.Price,
                    Quantity = input.Quantity,
                    WhitelistId = whitelistId
                });
            }
            else
            {
                listedNftInfo.Quantity = listedNftInfo.Quantity.Add(input.Quantity);
                var previousDuration = listedNftInfo.Duration.Clone();
                listedNftInfo.Duration = duration;
                isMergedToPreviousListedInfo = true;
                Context.Fire(new ListedNFTChanged
                {
                    Symbol = input.Symbol,
                    Duration = duration,
                    Owner = Context.Sender,
                    Price = input.Price,
                    Quantity = listedNftInfo.Quantity,
                    PreviousDuration = previousDuration,
                    WhitelistId = whitelistId
                });
            }

            State.ListedNFTInfoListMap[input.Symbol][Context.Sender] = listedNftInfoList;

            var totalQuantity = listedNftInfoList.Value.Where(i => i.Owner == Context.Sender).Sum(i => i.Quantity);

            Context.Fire(new FixedPriceNFTListed
            {
                Owner = listedNftInfo.Owner,
                Price = listedNftInfo.Price,
                Quantity = input.Quantity,
                Symbol = listedNftInfo.Symbol,
                Duration = listedNftInfo.Duration,
                IsMergedToPreviousListedInfo = isMergedToPreviousListedInfo,
                WhitelistId = whitelistId
            });

            return new Empty();
        }

        // public override Empty ListForFree(ListForFreeInput input)
        // {
        //     //TODO:List price is 0.
        //     return base.ListForFree(input);
        // }
        
        public override Empty Delist(DelistInput input)
        {
            var listedNftInfoList = State.ListedNFTInfoListMap[input.Symbol][Context.Sender];
            if (listedNftInfoList == null || listedNftInfoList.Value.All(i => i.ListType == ListType.NotListed))
            {
                throw new AssertionException("Listed NFT Info not exists. (Or already delisted.)");
            }

            Assert(input.Price != null, "Need to specific list record.");
            var listedNftInfo = listedNftInfoList.Value.FirstOrDefault(i =>
                i.Price.Amount == input.Price.Amount && i.Price.Symbol == input.Price.Symbol &&
                i.Owner == Context.Sender);
            if (listedNftInfo == null)
            {
                throw new AssertionException("Listed NFT Info not exists. (Or already delisted.)");
            }

            var projectId = CalculateProjectId(input.Symbol, Context.Sender);
            var whitelistId = State.WhitelistIdMap[projectId];
            
            switch (listedNftInfo.ListType)
            {
                case ListType.FixedPrice when input.Quantity >= listedNftInfo.Quantity:
                    State.ListedNFTInfoListMap[input.Symbol][Context.Sender].Value.Remove(listedNftInfo);
                    // if (State.WhitelistIdMap[input.Symbol][input.TokenId][Context.Sender] != null)
                    // {
                    //     var whitelistId = State.WhitelistIdMap[projectId];
                    //     State.WhitelistContract.DisableWhitelist.Send(whitelistId);
                    // }
                    Context.Fire(new ListedNFTRemoved
                    {
                        Symbol = listedNftInfo.Symbol,
                        Duration = listedNftInfo.Duration,
                        Owner = listedNftInfo.Owner
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
            
            var balance = State.TokenContract.GetBalance.Call(new AElf.Contracts.MultiToken.GetBalanceInput
            {
                Symbol = input.Symbol,
                Owner = Context.Sender
            });
            Assert(balance.Balance >= input.Quantity, "Insufficient NFT balance.");

            var offer = State.OfferListMap[input.Symbol][input.OfferFrom]?.Value
                .FirstOrDefault(o => o.From == input.OfferFrom 
                                     && o.To == Context.Sender 
                                     && o.Price.Symbol == input.Price.Symbol 
                                     && o.Price.Amount == input.Price.Amount 
                                     && o.ExpireTime >= Context.CurrentBlockTime);
            Price price;
            long totalAmount;
            if (offer == null)
            {
                Assert(false,"offer is empty");
                return new Empty();
            }
            else
            {
                Assert(offer.Quantity >= input.Quantity, "Deal quantity exceeded.");
                offer.Quantity = offer.Quantity.Sub(input.Quantity);
                if (offer.Quantity == 0)
                {
                    State.OfferListMap[input.Symbol][input.OfferFrom].Value.Remove(offer);
                    Context.Fire(new OfferRemoved
                    {
                        Symbol = input.Symbol,
                        OfferFrom = input.OfferFrom,
                        OfferTo = offer.To,
                        ExpireTime = offer.ExpireTime
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
                price = offer.Price;
                totalAmount = price.Amount.Mul(input.Quantity);
            }

            var listedNftInfoList = State.ListedNFTInfoListMap[input.Symbol][Context.Sender];
            if (listedNftInfoList != null && listedNftInfoList.Value.Any())
            {
                var firstListedNftInfo = listedNftInfoList.Value.First();
                // Listed with fixed price.
                var nftBalance = State.TokenContract.GetBalance.Call(new AElf.Contracts.MultiToken.GetBalanceInput
                {
                    Symbol = input.Symbol,
                    Owner = Context.Sender,
                }).Balance;
                var listedQuantity = listedNftInfoList.Value.Where(i => i.Owner == Context.Sender).Sum(i => i.Quantity);
                Assert(nftBalance >= listedQuantity.Add(input.Quantity),
                    $"Need to delist at least {listedQuantity.Add(input.Quantity).Sub(nftBalance)} NFT(s) before deal.");
            }

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
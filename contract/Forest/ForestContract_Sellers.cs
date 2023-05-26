using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Forest.Whitelist;
using Google.Protobuf.WellKnownTypes;
using GetBalanceInput = AElf.Contracts.NFT.GetBalanceInput;

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
            var requestInfo = State.RequestInfoMap[input.Symbol];
            var projectId = CalculateProjectId(input.Symbol,Context.Sender);
            var whitelistId = new Hash();
            var whitelistManager = GetWhitelistManager();
            if (input.IsWhitelistAvailable)
            {
                if (requestInfo != null)
                {
                    DealRequestInfoInWhitelist(input, duration, requestInfo);
                }
                else
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
        
        public override Empty ListWithEnglishAuction(ListWithEnglishAuctionInput input)
        {
            AssertContractInitialized();
            Assert(input.StartingPrice > 0, "Incorrect listing price.");
            Assert(input.EarnestMoney <= input.StartingPrice, "Earnest money too high.");
            if (CanBeListedWithAuction(input.Symbol, input.TokenId, out var requestInfo))
            {
                MaybeReceiveRemainDeposit(requestInfo);
            }
            else
            {
                throw new AssertionException("This NFT cannot be listed with auction for now.");
            }
            
            Assert(GetTokenWhiteList(input.Symbol).Value.Contains(input.PurchaseSymbol),
                $"{input.PurchaseSymbol} is not in token white list.");
            Assert(
                string.IsNullOrEmpty(State.NFTContract.GetNFTProtocolInfo
                    .Call(new StringValue {Value = input.PurchaseSymbol}).Symbol),
                $"Token {input.PurchaseSymbol} not support purchase for auction.");
            
            var duration = AdjustListDuration(input.Duration);

            var englishAuctionInfo = new EnglishAuctionInfo
            {
                Symbol = input.Symbol,
                TokenId = input.TokenId,
                Duration = duration,
                PurchaseSymbol = input.PurchaseSymbol,
                StartingPrice = input.StartingPrice,
                Owner = Context.Sender,
                EarnestMoney = input.EarnestMoney
            };
            State.EnglishAuctionInfoMap[input.Symbol] = englishAuctionInfo;

            State.ListedNFTInfoListMap[input.Symbol][Context.Sender] = new ListedNFTInfoList
            {
                Value =
                {
                    new ListedNFTInfo
                    {
                        Symbol = input.Symbol,
                        TokenId = input.TokenId,
                        Duration = duration,
                        ListType = ListType.EnglishAuction,
                        Owner = Context.Sender,
                        Price = new Price
                        {
                            Symbol = input.PurchaseSymbol,
                            Amount = input.StartingPrice
                        },
                        Quantity = 1
                    }
                }
            };

            State.DutchAuctionInfoMap[input.Symbol].Remove(input.TokenId);

            Context.Fire(new EnglishAuctionNFTListed
            {
                Owner = englishAuctionInfo.Owner,
                Symbol = englishAuctionInfo.Symbol,
                PurchaseSymbol = englishAuctionInfo.PurchaseSymbol,
                StartingPrice = englishAuctionInfo.StartingPrice,
                TokenId = englishAuctionInfo.TokenId,
                Duration = englishAuctionInfo.Duration,
                EarnestMoney = englishAuctionInfo.EarnestMoney
            });

            Context.Fire(new ListedNFTAdded
            {
                Symbol = input.Symbol,
                TokenId = input.TokenId,
                Duration = englishAuctionInfo.Duration,
                Owner = englishAuctionInfo.Owner,
                Price = new Price
                {
                    Symbol = englishAuctionInfo.PurchaseSymbol,
                    Amount = englishAuctionInfo.StartingPrice
                },
                Quantity = 1
            });

            return new Empty();
        }

        public override Empty ListWithDutchAuction(ListWithDutchAuctionInput input)
        {
            AssertContractInitialized();
            Assert(input.StartingPrice > 0 && input.EndingPrice > 0 && input.StartingPrice > input.EndingPrice,
                "Incorrect listing price.");
            if (CanBeListedWithAuction(input.Symbol, input.TokenId, out var requestInfo))
            {
                MaybeReceiveRemainDeposit(requestInfo);
            }
            else
            {
                throw new AssertionException("This NFT cannot be listed with auction for now.");
            }
            
            Assert(GetTokenWhiteList(input.Symbol).Value.Contains(input.PurchaseSymbol),
                $"{input.PurchaseSymbol} is not in token white list.");
            Assert(
                string.IsNullOrEmpty(State.NFTContract.GetNFTProtocolInfo
                    .Call(new StringValue {Value = input.PurchaseSymbol}).Symbol),
                $"Token {input.PurchaseSymbol} not support purchase for auction.");

            var duration = AdjustListDuration(input.Duration);

            var dutchAuctionInfo = new DutchAuctionInfo
            {
                Symbol = input.Symbol,
                TokenId = input.TokenId,
                Duration = duration,
                PurchaseSymbol = input.PurchaseSymbol,
                StartingPrice = input.StartingPrice,
                EndingPrice = input.EndingPrice,
                Owner = Context.Sender
            };
            State.DutchAuctionInfoMap[input.Symbol][input.TokenId] = dutchAuctionInfo;

            State.ListedNFTInfoListMap[input.Symbol][Context.Sender] = new ListedNFTInfoList
            {
                Value =
                {
                    new ListedNFTInfo
                    {
                        Symbol = input.Symbol,
                        TokenId = input.TokenId,
                        Duration = duration,
                        ListType = ListType.DutchAuction,
                        Owner = Context.Sender,
                        Price = new Price
                        {
                            Symbol = input.PurchaseSymbol,
                            Amount = input.StartingPrice
                        },
                        Quantity = 1
                    }
                }
            };

            Context.Fire(new DutchAuctionNFTListed
            {
                Owner = dutchAuctionInfo.Owner,
                PurchaseSymbol = dutchAuctionInfo.PurchaseSymbol,
                StartingPrice = dutchAuctionInfo.StartingPrice,
                EndingPrice = dutchAuctionInfo.EndingPrice,
                Symbol = dutchAuctionInfo.Symbol,
                TokenId = dutchAuctionInfo.TokenId,
                Duration = dutchAuctionInfo.Duration
            });

            Context.Fire(new ListedNFTAdded
            {
                Symbol = input.Symbol,
                TokenId = input.TokenId,
                Duration = dutchAuctionInfo.Duration,
                Owner = dutchAuctionInfo.Owner,
                Price = new Price
                {
                    Symbol = dutchAuctionInfo.PurchaseSymbol,
                    Amount = dutchAuctionInfo.StartingPrice
                },
                Quantity = 1
            });

            return new Empty();
        }

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

            var requestInfo = State.RequestInfoMap[input.Symbol];
            if (requestInfo != null)
            {
                requestInfo.ListTime = null;
                State.RequestInfoMap[input.Symbol] = requestInfo;
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
                        TokenId = listedNftInfo.TokenId,
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
                        TokenId = listedNftInfo.TokenId,
                        Duration = listedNftInfo.Duration,
                        Owner = listedNftInfo.Owner,
                        Price = listedNftInfo.Price,
                        Quantity = listedNftInfo.Quantity,
                        WhitelistId = whitelistId
                    });
                    break;
                case ListType.EnglishAuction:
                    var englishAuctionInfo = State.EnglishAuctionInfoMap[input.Symbol];
                    var bidAddressList = State.BidAddressListMap[input.Symbol];
                    if (bidAddressList != null && bidAddressList.Value.Any())
                    {
                        // Charge service fee if anyone placed a bid.
                        ChargeSenderServiceFee(englishAuctionInfo.PurchaseSymbol, englishAuctionInfo.StartingPrice);
                    }
                    State.ListedNFTInfoListMap[input.Symbol][Context.Sender].Value.Remove(listedNftInfo);
                    Context.Fire(new ListedNFTRemoved
                    {
                        Symbol = listedNftInfo.Symbol,
                        TokenId = listedNftInfo.TokenId,
                        Duration = listedNftInfo.Duration,
                        Owner = listedNftInfo.Owner
                    });
                    break;
                case ListType.DutchAuction:
                    var dutchAuctionInfo = State.DutchAuctionInfoMap[input.Symbol][input.TokenId];
                    State.ListedNFTInfoListMap[input.Symbol][Context.Sender].Value.Remove(listedNftInfo);
                    State.DutchAuctionInfoMap[input.Symbol].Remove(input.TokenId);
                    ChargeSenderServiceFee(dutchAuctionInfo.PurchaseSymbol, dutchAuctionInfo.StartingPrice);
                    Context.Fire(new ListedNFTRemoved
                    {
                        Symbol = listedNftInfo.Symbol,
                        TokenId = listedNftInfo.TokenId,
                        Duration = listedNftInfo.Duration,
                        Owner = listedNftInfo.Owner
                    });
                    break;
            }

            Context.Fire(new NFTDelisted
            {
                Symbol = input.Symbol,
                TokenId = input.TokenId,
                Owner = Context.Sender,
                Quantity = input.Quantity
            });

            return new Empty();
        }

        private void ChargeSenderServiceFee(string symbol, long baseAmount)
        {
            var amount = baseAmount.Mul(State.ServiceFeeRate.Value).Div(FeeDenominator);
            if (amount > 0)
            {
                State.TokenContract.TransferFrom.Send(new TransferFromInput
                {
                    Symbol = symbol,
                    Amount = amount,
                    From = Context.Sender,
                    To = State.ServiceFeeReceiver.Value ?? State.Admin.Value
                });
            }
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
                .FirstOrDefault(o =>
                    o.From == input.OfferFrom && o.Price.Symbol == input.Price.Symbol &&
                    o.Price.Amount == input.Price.Amount && o.ExpireTime >= Context.CurrentBlockTime);
            var bid = State.BidMap[input.Symbol][input.OfferFrom];
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
                NFTTo = offer?.From ?? bid.From,
                NFTSymbol = input.Symbol,
                NFTQuantity = input.Quantity,
                PurchaseSymbol = price.Symbol,
                PurchaseAmount = totalAmount,
                PurchaseTokenId = price.TokenId
            });
            return new Empty();
        }
}
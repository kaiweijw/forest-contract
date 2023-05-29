using System;
using System.Linq;
using AElf.Contracts.MultiToken;
using AElf.Contracts.NFT;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElf.Sdk.CSharp;
using AElf.Types;
using Forest.Services;
using Forest.Whitelist;
using Google.Protobuf.WellKnownTypes;
using GetAllowanceInput = AElf.Contracts.MultiToken.GetAllowanceInput;
using GetBalanceInput = AElf.Contracts.MultiToken.GetBalanceInput;
using TransferFromInput = AElf.Contracts.MultiToken.TransferFromInput;
using TransferInput = AElf.Contracts.MultiToken.TransferInput;


namespace Forest;

public partial class ForestContract
{
   /// <summary>
        /// There are 2 types of making offer.
        /// 1. Aiming a owner.
        /// 2. Only aiming nft. Owner will be the nft protocol creator.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public override Empty MakeOffer(MakeOfferInput input)
        {
            AssertContractInitialized();
            var nftInfo = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
            {
                Symbol = input.Symbol,
            });

            var makeOfferService = GetMakeOfferService();
            makeOfferService.ValidateOffer(input);

            if (nftInfo.Supply != 0 && input.OfferTo == null)
            {
                input.OfferTo = nftInfo.Issuer;
            }

            var listedNftInfoList =
                State.ListedNFTInfoListMap[input.Symbol][input.OfferTo];
            
            var whitelistManager = GetWhitelistManager();
            
            if (makeOfferService.IsSenderInWhitelist(input,out var whitelistId) && whitelistManager.IsWhitelistAvailable(whitelistId))
            {
                // Deal one NFT with whitelist price.
                var price = whitelistManager.GetExtraInfoByAddress(whitelistId);
                if (price != null && price.Amount <= input.Price.Amount && price.Symbol == input.Price.Symbol)
                {
                    var minStartList = listedNftInfoList.Value.OrderBy(i => i.Duration.StartTime).ToList();
                    if (minStartList.Count == 0)
                    {
                        PerformMakeOffer(input);
                        return new Empty();
                    }
                    if (Context.CurrentBlockTime < minStartList[0].Duration.StartTime)
                    {
                        PerformMakeOffer(input);
                        return new Empty();
                    }
                    if (TryDealWithFixedPriceWhitelist(input,price,whitelistId))
                    {
                        // MaybeRemoveRequest(input.Symbol, input.TokenId);
                        minStartList[0].Quantity = minStartList[0].Quantity.Sub(1);
                        if (minStartList[0].Quantity == 0)
                        {
                            listedNftInfoList.Value.Remove(minStartList[0]);
                            Context.Fire(new ListedNFTRemoved
                            {
                                Symbol = minStartList[0].Symbol,
                                Duration = minStartList[0].Duration,
                                Owner = minStartList[0].Owner,
                                Price = minStartList[0].Price
                            });
                        }
                        else
                        {
                            Context.Fire(new ListedNFTChanged
                            {
                                Symbol = minStartList[0].Symbol,
                                Duration = minStartList[0].Duration,
                                Owner = minStartList[0].Owner,
                                PreviousDuration = minStartList[0].Duration,
                                Quantity = minStartList[0].Quantity,
                                Price = minStartList[0].Price,
                                WhitelistId = whitelistId
                            });
                        }
                    }
                    input.Quantity = input.Quantity.Sub(1);
                    if (input.Quantity == 0)
                    {
                        return new Empty();
                    }
                }
            }
            
            var dealStatus = makeOfferService.GetDealStatus(input, out var affordableNftInfoList);
            switch(dealStatus)
            {
                case DealStatus.NFTNotMined:
                    PerformRequestNewItem(input.Symbol, input.Price, input.ExpireTime);
                    return new Empty();
                case DealStatus.NotDeal:
                    PerformMakeOffer(input);
                    return new Empty();
            }
            Assert(nftInfo.Supply > 0, "NFT does not exist.");

            if (listedNftInfoList.Value.All(i => i.ListType != ListType.FixedPrice))
            {
                var auctionInfo = listedNftInfoList.Value.FirstOrDefault();
                if (auctionInfo == null || IsListedNftTimedOut(auctionInfo))
                {
                    PerformMakeOffer(input);
                }
                State.ListedNFTInfoListMap[input.Symbol][input.OfferTo] = listedNftInfoList;

                return new Empty();
            }
            
            var dealService = GetDealService();
            var getDealResultListInput = new GetDealResultListInput
            {
                MakeOfferInput = input,
                ListedNftInfoList = new ListedNFTInfoList
                {
                    Value = {affordableNftInfoList}
                }
            };
            var normalPriceDealResultList = dealService.GetDealResultList(getDealResultListInput).ToList();
            if (normalPriceDealResultList.Count == 0)
            {
                PerformMakeOffer(input);
                return new Empty();
            }
            var toRemove = new ListedNFTInfoList();
            foreach (var dealResult in normalPriceDealResultList)
            {
                if (!TryDealWithFixedPrice(input, dealResult, listedNftInfoList.Value[dealResult.Index],out var dealQuantity)) continue;
                dealResult.Quantity = dealResult.Quantity.Sub(dealQuantity);
                var listedNftInfo = listedNftInfoList.Value[dealResult.Index];
                listedNftInfo.Quantity = listedNftInfoList.Value[dealResult.Index].Quantity.Sub(dealQuantity);
                input.Quantity = input.Quantity.Sub(dealQuantity);
                if (listedNftInfoList.Value[dealResult.Index].Quantity == 0)
                {
                    toRemove.Value.Add(listedNftInfoList.Value[dealResult.Index]);
                }
                else
                {
                    Context.Fire(new ListedNFTChanged
                    {
                        Symbol = listedNftInfo.Symbol,
                        Duration = listedNftInfo.Duration,
                        Owner = listedNftInfo.Owner,
                        PreviousDuration = listedNftInfo.Duration,
                        Quantity = listedNftInfo.Quantity,
                        Price = listedNftInfo.Price,
                        WhitelistId = whitelistId
                    });
                }
            }

            if (toRemove.Value.Count != 0)
            {
                foreach (var info in toRemove.Value)
                {
                    listedNftInfoList.Value.Remove(info);
                    Context.Fire(new ListedNFTRemoved
                    {
                        Symbol = info.Symbol,
                        Duration = info.Duration,
                        Owner = info.Owner,
                        Price = info.Price
                    });
                }
            }

            if (input.Quantity > 0)
            {
                PerformMakeOffer(input);
            }

            State.ListedNFTInfoListMap[input.Symbol][input.OfferTo] = listedNftInfoList;

            return new Empty();
        }

        private bool TryDealWithFixedPriceWhitelist(MakeOfferInput input,Price price,Hash whitelistId)
        {
            Assert(input.Price.Symbol == price.Symbol,
                $"Need to use token {price.Symbol}, not {input.Price.Symbol}");
            //Get extraInfoId according to the sender.
            var extraInfoId = State.WhitelistContract.GetTagIdByAddress.Call(new GetTagIdByAddressInput()
            {
                WhitelistId = whitelistId,
                Address = Context.Sender
            });
            State.WhitelistContract.RemoveAddressInfoListFromWhitelist.Send(new RemoveAddressInfoListFromWhitelistInput()
            {
                WhitelistId = whitelistId,
                ExtraInfoIdList = new ExtraInfoIdList()
                {
                    Value = { new ExtraInfoId
                    {
                        AddressList = new Whitelist.AddressList {Value = {Context.Sender}},
                        Id = extraInfoId
                    } }
                }
            });
            var totalAmount = price.Amount.Mul(1);
            PerformDeal(new PerformDealInput
            {
                NFTFrom = input.OfferTo,
                NFTTo = Context.Sender,
                NFTSymbol = input.Symbol,
                NFTQuantity = 1,
                PurchaseSymbol = price.Symbol,
                PurchaseAmount = totalAmount,
                PurchaseTokenId = input.Price.TokenId
            });
            return true;
        }
        public override Empty CancelOffer(CancelOfferInput input)
        {
            AssertContractInitialized();

            OfferList offerList;
            var newOfferList = new OfferList();

            // Admin can remove expired offer.
            if (input.OfferFrom != null && input.OfferFrom != Context.Sender)
            {
                AssertSenderIsAdmin();

                offerList = State.OfferListMap[input.Symbol][input.OfferFrom];

                if (offerList != null)
                {
                    foreach (var offer in offerList.Value)
                    {
                        if (offer.ExpireTime >= Context.CurrentBlockTime)
                        {
                            newOfferList.Value.Add(offer);
                        }
                        else
                        {
                            Context.Fire(new OfferRemoved
                            {
                                Symbol = input.Symbol,
                                OfferFrom = offer.From,
                                OfferTo = offer.To,
                                ExpireTime = offer.ExpireTime
                            });
                        }
                    }

                    State.OfferListMap[input.Symbol][input.OfferFrom] = newOfferList;
                }
                return new Empty();
            }

            //owner can remove select offer.

            offerList = State.OfferListMap[input.Symbol][Context.Sender];

            var nftInfo = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
            {
                Symbol = input.Symbol,
            });
            if (nftInfo.Issuer == null)
            {
                // This nft does not exist.
                State.OfferListMap[input.Symbol].Remove(Context.Sender);
            }

            if (input.IndexList != null && input.IndexList.Value.Any())
            {
                for (var i = 0; i < offerList.Value.Count; i++)
                {
                    if (!input.IndexList.Value.Contains(i))
                    {
                        newOfferList.Value.Add(offerList.Value[i]);
                    }
                    else
                    {
                        Context.Fire(new OfferRemoved
                        {
                            Symbol = input.Symbol,
                            OfferFrom = Context.Sender,
                            OfferTo = offerList.Value[i].To,
                            ExpireTime = offerList.Value[i].ExpireTime
                        });
                    }
                }

                Context.Fire(new OfferCanceled
                {
                    Symbol = input.Symbol,
                    OfferFrom = Context.Sender,
                    IndexList = input.IndexList
                });
            }
            else
            {
                newOfferList.Value.Add(offerList.Value);
            }

            State.OfferListMap[input.Symbol][Context.Sender] = newOfferList;

            return new Empty();
        }

        private void PerformCancelRequest(CancelOfferInput input, RequestInfo requestInfo)
        {
            Assert(requestInfo.Requester == Context.Sender, "No permission.");
            var virtualAddress = CalculateNFTVirtuaAddress(input.Symbol);
            var balanceOfNftVirtualAddress = State.TokenContract.GetBalance.Call(new GetBalanceInput
            {
                Symbol = requestInfo.Price.Symbol,
                Owner = virtualAddress
            }).Balance;

            var depositReceiver = requestInfo.Requester;

            if (requestInfo.IsConfirmed)
            {
                if (requestInfo.ConfirmTime.AddHours(requestInfo.WorkHours) < Context.CurrentBlockTime)
                {
                    // Creator missed the deadline.

                    var protocolVirtualAddressFrom = CalculateTokenHash(input.Symbol);
                    var protocolVirtualAddress =
                        Context.ConvertVirtualAddressToContractAddress(protocolVirtualAddressFrom);
                    var balanceOfNftProtocolVirtualAddress = State.TokenContract.GetBalance.Call(new GetBalanceInput
                    {
                        Symbol = requestInfo.Price.Symbol,
                        Owner = protocolVirtualAddress
                    }).Balance;
                    var deposit = balanceOfNftVirtualAddress.Mul(FeeDenominator).Div(DefaultDepositConfirmRate)
                        .Sub(balanceOfNftVirtualAddress);
                    if (balanceOfNftProtocolVirtualAddress > 0)
                    {
                        State.TokenContract.Transfer.VirtualSend(protocolVirtualAddressFrom, new TransferInput
                        {
                            To = requestInfo.Requester,
                            Symbol = requestInfo.Price.Symbol,
                            Amount = Math.Min(balanceOfNftProtocolVirtualAddress, deposit)
                        });
                    }
                }
                else
                {
                    depositReceiver = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
                    {
                        Symbol = input.Symbol,
                    }).Issuer;
                }
            }

            var virtualAddressFrom = CalculateTokenHash(input.Symbol);

            if (balanceOfNftVirtualAddress > 0)
            {
                State.TokenContract.Transfer.VirtualSend(virtualAddressFrom, new TransferInput
                {
                    To = depositReceiver,
                    Symbol = requestInfo.Price.Symbol,
                    Amount = balanceOfNftVirtualAddress
                });
            }

            // MaybeRemoveRequest(input.Symbol, input.TokenId);

            Context.Fire(new NFTRequestCancelled
            {
                Symbol = input.Symbol,
                Requester = Context.Sender
            });
        }

        /// <summary>
        /// Sender is buyer.
        /// </summary>
        private bool TryDealWithFixedPrice(MakeOfferInput input, DealResult dealResult ,ListedNFTInfo listedNftInfo,out long actualQuantity)
        {
            var usePrice = input.Price.Clone();
            usePrice.Amount = Math.Min(input.Price.Amount, dealResult.PurchaseAmount);
            actualQuantity = Math.Min(input.Quantity, listedNftInfo.Quantity);
            
            var totalAmount = usePrice.Amount.Mul(actualQuantity);
            PerformDeal(new PerformDealInput
            {
                NFTFrom = input.OfferTo,
                NFTTo = Context.Sender,
                NFTSymbol = input.Symbol,
                NFTQuantity = actualQuantity,
                PurchaseSymbol = usePrice.Symbol,
                PurchaseAmount = totalAmount,
                PurchaseTokenId = input.Price.TokenId
            });

            return true;
        }

        /// <summary>
        /// Will go to Offer List.
        /// </summary>
        /// <param name="input"></param>
        private void PerformMakeOffer(MakeOfferInput input)
        {
            var offerList = State.OfferListMap[input.Symbol][Context.Sender] ?? new OfferList();
            var expireTime = input.ExpireTime ?? Context.CurrentBlockTime.AddDays(DefaultExpireDays);
            var maybeSameOffer = offerList.Value.SingleOrDefault(o =>
                o.Price.Symbol == input.Price.Symbol && o.Price.Amount == input.Price.Amount &&
                o.ExpireTime == expireTime && o.To == input.OfferTo && o.From == Context.Sender);
            if (maybeSameOffer == null)
            {
                offerList.Value.Add(new Offer
                {
                    From = Context.Sender,
                    To = input.OfferTo,
                    Price = input.Price,
                    ExpireTime = expireTime,
                    Quantity = input.Quantity
                });
                Context.Fire(new OfferAdded
                {
                    Symbol = input.Symbol,
                    OfferFrom = Context.Sender,
                    OfferTo = input.OfferTo,
                    ExpireTime = expireTime,
                    Price = input.Price,
                    Quantity = input.Quantity
                });
            }
            else
            {
                maybeSameOffer.Quantity = maybeSameOffer.Quantity.Add(input.Quantity);
                Context.Fire(new OfferChanged
                {
                    Symbol = input.Symbol,
                    OfferFrom = Context.Sender,
                    OfferTo = input.OfferTo,
                    Price = input.Price,
                    ExpireTime = expireTime,
                    Quantity = maybeSameOffer.Quantity
                });
            }

            State.OfferListMap[input.Symbol][Context.Sender] = offerList;

            var addressList = State.OfferAddressListMap[input.Symbol] ?? new AddressList();

            if (!addressList.Value.Contains(Context.Sender))
            {
                addressList.Value.Add(Context.Sender);
                State.OfferAddressListMap[input.Symbol] = addressList;
            }

            Context.Fire(new OfferMade
            {
                Symbol = input.Symbol,
                OfferFrom = Context.Sender,
                OfferTo = input.OfferTo,
                ExpireTime = expireTime,
                Price = input.Price,
                Quantity = input.Quantity
            });
        }

        private void TryPlaceBidForEnglishAuction(MakeOfferInput input)
        {
            var auctionInfo = State.EnglishAuctionInfoMap[input.Symbol];
            if (auctionInfo == null)
            {
                throw new AssertionException($"Auction info of {input.Symbol} not found.");
            }

            var duration = auctionInfo.Duration;
            if (Context.CurrentBlockTime < duration.StartTime)
            {
                PerformMakeOffer(input);
                return;
            }

            Assert(Context.CurrentBlockTime <= duration.StartTime.AddHours(duration.DurationHours),
                "Auction already finished.");
            Assert(input.Price.Symbol == auctionInfo.PurchaseSymbol, "Incorrect symbol.");

            if (input.Price.Amount < auctionInfo.StartingPrice)
            {
                PerformMakeOffer(input);
                return;
            }

            var bidList = GetBidList(new GetBidListInput
            {
                Symbol = input.Symbol,
            });
            var sortedBitList = new BidList
            {
                Value =
                {
                    bidList.Value.OrderByDescending(o => o.Price.Amount)
                }
            };
            if (sortedBitList.Value.Any() && input.Price.Amount <= sortedBitList.Value.First().Price.Amount)
            {
                PerformMakeOffer(input);
                return;
            }

            var bid = new Bid
            {
                From = Context.Sender,
                To = input.OfferTo,
                Price = new Price
                {
                    Symbol = input.Price.Symbol,
                    Amount = input.Price.Amount
                },
                ExpireTime = input.ExpireTime ?? Context.CurrentBlockTime.AddDays(DefaultExpireDays)
            };

            var bidAddressList = State.BidAddressListMap[input.Symbol] ?? new AddressList();
            if (!bidAddressList.Value.Contains(Context.Sender))
            {
                bidAddressList.Value.Add(Context.Sender);
                State.BidAddressListMap[input.Symbol] = bidAddressList;
                // Charge earnest if the Sender is the first time to place a bid.
                ChargeEarnestMoney(input.Symbol, auctionInfo.PurchaseSymbol, auctionInfo.EarnestMoney);
            }

            State.BidMap[input.Symbol][Context.Sender] = bid;

            var remainAmount = input.Price.Amount.Sub(auctionInfo.EarnestMoney);
            Assert(
                State.TokenContract.GetBalance.Call(new GetBalanceInput
                {
                    Symbol = auctionInfo.PurchaseSymbol,
                    Owner = Context.Sender
                }).Balance >= remainAmount,
                "Insufficient balance to bid.");
            Assert(
                State.TokenContract.GetAllowance.Call(new GetAllowanceInput
                {
                    Symbol = auctionInfo.PurchaseSymbol,
                    Owner = Context.Sender,
                    Spender = Context.Self
                }).Allowance >= remainAmount,
                "Insufficient allowance to bid.");

            Context.Fire(new BidPlaced
            {
                Symbol = input.Symbol,
                Price = bid.Price,
                ExpireTime = bid.ExpireTime,
                OfferFrom = bid.From,
                OfferTo = input.OfferTo
            });
        }

        private void ChargeEarnestMoney(string nftSymbol, string purchaseSymbol, long earnestMoney)
        {
            if (earnestMoney > 0)
            {
                var virtualAddress = CalculateNFTVirtuaAddress(nftSymbol);
                State.TokenContract.TransferFrom.Send(new TransferFromInput
                {
                    From = Context.Sender,
                    To = virtualAddress,
                    Symbol = purchaseSymbol,
                    Amount = earnestMoney
                });
            }
        }

        private bool PerformMakeOfferToDutchAuction(MakeOfferInput input)
        {
            var auctionInfo = State.DutchAuctionInfoMap[input.Symbol];
            if (auctionInfo == null)
            {
                throw new AssertionException($"Auction info of {input.Symbol} not found.");
            }

            var duration = auctionInfo.Duration;
            if (Context.CurrentBlockTime < duration.StartTime)
            {
                PerformMakeOffer(input);
                return false;
            }

            Assert(Context.CurrentBlockTime <= duration.StartTime.AddHours(duration.DurationHours),
                "Auction already finished.");
            Assert(input.Price.Symbol == auctionInfo.PurchaseSymbol, "Incorrect symbol");
            var currentBiddingPrice = CalculateCurrentBiddingPrice(auctionInfo.StartingPrice, auctionInfo.EndingPrice,
                auctionInfo.Duration);
            if (input.Price.Amount < currentBiddingPrice)
            {
                PerformMakeOffer(input);
                return false;
            }

            PerformDeal(new PerformDealInput
            {
                NFTFrom = auctionInfo.Owner,
                NFTTo = Context.Sender,
                NFTQuantity = 1,
                NFTSymbol = input.Symbol,
                PurchaseSymbol = input.Price.Symbol,
                PurchaseAmount = input.Price.Amount,
                PurchaseTokenId = 0
            });
            return true;
        }

        private long CalculateCurrentBiddingPrice(long startingPrice, long endingPrice, ListDuration duration)
        {
            var passedSeconds = (Context.CurrentBlockTime - duration.StartTime).Seconds;
            var durationSeconds = duration.DurationHours.Mul(3600);
            if (passedSeconds == 0)
            {
                return startingPrice;
            }

            var diffPrice = endingPrice.Sub(startingPrice);
            return Math.Max(startingPrice.Sub(diffPrice.Mul(durationSeconds).Div(passedSeconds)), endingPrice);
        }

        private void MaybeReceiveRemainDeposit(RequestInfo requestInfo)
        {
            if (requestInfo == null) return;
            Assert(Context.CurrentBlockTime > requestInfo.WhiteListDueTime, "Due time not passed.");
            var nftProtocolInfo =
                State.NFTContract.GetNFTProtocolInfo.Call((new StringValue {Value = requestInfo.Symbol}));
            Assert(nftProtocolInfo.Creator == Context.Sender, "Only NFT Protocol Creator can claim remain deposit.");

            var nftVirtualAddressFrom = CalculateTokenHash(requestInfo.Symbol);
            var nftVirtualAddress = Context.ConvertVirtualAddressToContractAddress(nftVirtualAddressFrom);
            var balance = State.TokenContract.GetBalance.Call(new GetBalanceInput
            {
                Symbol = requestInfo.Price.Symbol,
                Owner = nftVirtualAddress
            }).Balance;
            if (balance > 0)
            {
                State.TokenContract.Transfer.VirtualSend(nftVirtualAddressFrom, new TransferInput
                {
                    To = nftProtocolInfo.Creator,
                    Symbol = requestInfo.Price.Symbol,
                    Amount = balance
                });
            }

            // MaybeRemoveRequest(requestInfo.Symbol, requestInfo.TokenId);
        }

        public override Empty MintBadge(MintBadgeInput input)
        {
            var protocol = State.NFTContract.GetNFTProtocolInfo.Call(new StringValue {Value = input.Symbol});
            Assert(!string.IsNullOrWhiteSpace(protocol.Symbol), $"Protocol {input.Symbol} not found.");
            Assert(protocol.NftType.ToUpper() == NFTType.Badges.ToString().ToUpper(),
                "This method is only for badges.");
            var nftInfo = State.NFTContract.GetNFTInfo.Call(new GetNFTInfoInput
            {
                Symbol = input.Symbol,
                TokenId = input.TokenId
            });
            Assert(nftInfo.TokenId > 0, "Badge not found.");
            Assert(nftInfo.Metadata.Value.ContainsKey(BadgeMintWhitelistIdMetadataKey),
                $"Metadata {BadgeMintWhitelistIdMetadataKey} not found.");
            var whitelistIdHex = nftInfo.Metadata.Value[BadgeMintWhitelistIdMetadataKey];
            Assert(!string.IsNullOrWhiteSpace(whitelistIdHex),$"No whitelist.{whitelistIdHex}");
            var whitelistId = Hash.LoadFromHex(whitelistIdHex);
            //Whether NFT Market Contract is the manager.
            var isManager = State.WhitelistContract.GetManagerExistFromWhitelist.Call(new GetManagerExistFromWhitelistInput
            {
                WhitelistId = whitelistId,
                Manager = Context.Self
            });
            Assert(isManager.Value == true,"NFT Market Contract does not in the manager list.");
            // Is Context.Sender in whitelist
            var ifExist = State.WhitelistContract.GetAddressFromWhitelist.Call(new GetAddressFromWhitelistInput
            {
                WhitelistId = whitelistId,
                Address = Context.Sender
            });
            Assert(ifExist.Value,$"No permission.{Context.Sender}");
            State.NFTContract.Mint.Send(new MintInput
            {
                Symbol = input.Symbol,
                Owner = Context.Sender,
                Quantity = 1
            });
            State.WhitelistContract.RemoveAddressInfoListFromWhitelist.Send(new RemoveAddressInfoListFromWhitelistInput
            {
                WhitelistId = whitelistId,
                ExtraInfoIdList = new ExtraInfoIdList
                {
                    Value = { new ExtraInfoId
                    {
                        AddressList = new Whitelist.AddressList{Value = { Context.Sender }}
                    } }
                }
            });
            return new Empty();
        }
}
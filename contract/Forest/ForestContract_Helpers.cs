using System.Linq;
using AElf;
using AElf.CSharp.Core;
using AElf.CSharp.Core.Extension;
using AElf.Sdk.CSharp;
using AElf.Types;
using Forest.Whitelist;
using Google.Protobuf;
using TransferFromInput = AElf.Contracts.MultiToken.TransferFromInput;

namespace Forest;

public partial class ForestContract
{
    private void PerformDeal(PerformDealInput performDealInput)
        {
            Assert(performDealInput.NFTFrom != performDealInput.NFTTo, "NFT From address cannot be NFT To address.");
            var serviceFee = performDealInput.PurchaseAmount.Mul(State.ServiceFeeRate.Value).Div(FeeDenominator);
            var actualAmount = performDealInput.PurchaseAmount.Sub(serviceFee);
            if (actualAmount != 0)
            {
                State.TokenContract.TransferFrom.Send(new TransferFromInput
                {
                    From = performDealInput.NFTTo,
                    To = performDealInput.NFTFrom,
                    Symbol = performDealInput.PurchaseSymbol,
                    Amount = actualAmount
                });
                if (serviceFee > 0 && performDealInput.NFTTo != State.ServiceFeeReceiver.Value)
                {
                    State.TokenContract.TransferFrom.Send(new TransferFromInput
                    {
                        From = performDealInput.NFTTo,
                        To = State.ServiceFeeReceiver.Value,
                        Symbol = performDealInput.PurchaseSymbol,
                        Amount = serviceFee
                    });
                }
            }
            
            State.TokenContract.TransferFrom.Send(new TransferFromInput
            {
                From = performDealInput.NFTFrom,
                To = performDealInput.NFTTo,
                Symbol = performDealInput.NFTSymbol,
                Amount = performDealInput.NFTQuantity
            });
            Context.Fire(new Sold
            {
                NftFrom = performDealInput.NFTFrom,
                NftTo = performDealInput.NFTTo,
                NftSymbol = performDealInput.NFTSymbol,
                NftTokenId = performDealInput.NFTTokenId,
                NftQuantity = performDealInput.NFTQuantity,
                PurchaseSymbol = performDealInput.PurchaseSymbol,
                PurchaseAmount = performDealInput.PurchaseAmount
            });
        }

        private struct PerformDealInput
        {
            public Address NFTFrom { get; set; }
            public Address NFTTo { get; set; }
            public string NFTSymbol { get; set; }
            public long NFTTokenId { get; set; }
            public long NFTQuantity { get; set; }
            public string PurchaseSymbol { get; set; }

            /// <summary>
            /// If PurchaseSymbol is a Fungible token, PurchaseTokenIs shall always be 0.
            /// </summary>
            public long PurchaseTokenId { get; set; }

            /// <summary>
            /// Be aware of that this stands for total amount.
            /// </summary>
            public long PurchaseAmount { get; set; }
        }

        private StringList GetTokenWhiteList(string symbol)
        {
            var tokenWhiteList = State.TokenWhiteListMap[symbol] ?? State.GlobalTokenWhiteList.Value;
            foreach (var globalWhiteListSymbol in State.GlobalTokenWhiteList.Value.Value)
            {
                if (!tokenWhiteList.Value.Contains(globalWhiteListSymbol))
                {
                    tokenWhiteList.Value.Add(globalWhiteListSymbol);
                }
            }

            return tokenWhiteList;
        }
        
        private ListDuration AdjustListDuration(ListDuration duration)
        {
            const int SIX_MONTH_HOURS = 4392;
            if (duration == null)
            {
                duration = new ListDuration
                {
                    StartTime = Context.CurrentBlockTime,
                    PublicTime = Context.CurrentBlockTime,
                    DurationHours = SIX_MONTH_HOURS
                };
            }
            else
            {
                if (duration.StartTime == null || duration.StartTime < Context.CurrentBlockTime)
                {
                    duration.StartTime = Context.CurrentBlockTime;
                }

                if (duration.PublicTime == null || duration.PublicTime < duration.StartTime)
                {
                    duration.PublicTime = duration.StartTime;
                }

                if (duration.DurationHours == 0)
                {
                    duration.DurationHours = SIX_MONTH_HOURS;
                }
            }

            return duration;
        }

        private bool IsListedNftTimedOut(ListedNFTInfo listedNftInfo)
        {
            var expireTime = listedNftInfo.Duration.StartTime.AddHours(listedNftInfo.Duration.DurationHours);
            return Context.CurrentBlockTime > expireTime;
        }

        private Price DeserializedInfo(TagInfo tagInfo)
        {
            var deserializedInfo = new PriceTag();
            deserializedInfo.MergeFrom(tagInfo.Info);
            return new Price
            {
                Symbol = deserializedInfo.Symbol,
                Amount = deserializedInfo.Amount
            };
        }

        private Hash CalculateProjectId(string symbol,Address sender)
        {
            return HashHelper.ComputeFrom($"{symbol}{sender}");
        }

        private ExtraInfoList ConvertToExtraInfo(WhitelistInfoList input)
        {
            var extraInfoList = new ExtraInfoList();
            if (input == null)
            {
                return extraInfoList;
            }
            foreach (var whitelist in input.Whitelists)
            {
                var extraInfo = new ExtraInfo
                {
                    AddressList = new Whitelist.AddressList {Value = {whitelist.AddressList.Value}},
                    Info = new TagInfo
                    {
                        TagName = whitelist.PriceTag.TagName,
                        Info = new PriceTag
                        {
                            Symbol = whitelist.PriceTag.Price.Symbol,
                            Amount = whitelist.PriceTag.Price.Amount
                        }.ToByteString()
                    }
                };
                extraInfoList.Value.Add(extraInfo);
            }

            return extraInfoList;
        }

        private Hash ExistWhitelist(Hash projectId, WhitelistInfoList whitelistInfoList, ExtraInfoList extraInfoList)
        {
            var whitelistManager = GetWhitelistManager();
            var whitelistId = State.WhitelistIdMap[projectId];
            //Format data.
            var extraInfoIdList = whitelistInfoList?.Whitelists.GroupBy(p => p.PriceTag)
                                .ToDictionary(e => e.Key, e => e.ToList())
                                .Select(extra =>
                                {
                                    //Whether price tag already exists.
                                    var ifExist = whitelistManager.GetTagInfoFromWhitelist(
                                        new GetTagInfoFromWhitelistInput()
                                        {
                                            ProjectId = projectId,
                                            WhitelistId = whitelistId,
                                            TagInfo = new TagInfo
                                            {
                                                TagName = extra.Key.TagName,
                                                Info = new PriceTag
                                                {
                                                    Symbol = extra.Key.Price.Symbol,
                                                    Amount = extra.Key.Price.Amount
                                                }.ToByteString()
                                            }
                                        });
                                    if (!ifExist)
                                    {
                                        //Doesn't exist,add tag info.
                                        whitelistManager.AddExtraInfo(new AddExtraInfoInput()
                                        {
                                            ProjectId = projectId,
                                            WhitelistId = whitelistId,
                                            TagInfo = new TagInfo
                                            {
                                                TagName = extra.Key.TagName,
                                                Info = new PriceTag
                                                {
                                                    Symbol = extra.Key.Price.Symbol,
                                                    Amount = extra.Key.Price.Amount
                                                }.ToByteString()
                                            }
                                        });
                                    }
                                    var tagId =
                                        HashHelper.ComputeFrom(
                                            $"{whitelistId}{projectId}{extra.Key.TagName}");
                                    var toAddExtraInfoIdList = new ExtraInfoIdList();
                                    foreach (var whitelistInfo in extra.Value.Where(whitelistInfo =>
                                                 whitelistInfo.AddressList.Value.Any()))
                                    {
                                        toAddExtraInfoIdList.Value.Add(new ExtraInfoId()
                                        {
                                            AddressList = new Whitelist.AddressList
                                            {
                                                Value = {whitelistInfo.AddressList.Value}
                                            },
                                            Id = tagId
                                        });
                                    }
                                    return toAddExtraInfoIdList;
                                }).ToList();
            if (extraInfoList == null || extraInfoIdList == null || extraInfoIdList.Count == 0) return whitelistId;
            {
                var toAdd = new ExtraInfoIdList();
                foreach (var extra in extraInfoIdList)
                {
                    toAdd.Value.Add(extra.Value);
                }
                whitelistManager.AddAddressInfoListToWhitelist(
                    new AddAddressInfoListToWhitelistInput()
                    {
                        WhitelistId = whitelistId,
                        ExtraInfoIdList = toAdd
                    });
            }
            return whitelistId;
        }
}
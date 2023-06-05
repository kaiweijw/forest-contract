using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.NFT;
using Shouldly;
using Xunit;
using ApproveInput = AElf.Contracts.NFT.ApproveInput;
using GetBalanceInput = AElf.Contracts.NFT.GetBalanceInput;
using TransferInput = AElf.Contracts.MultiToken.TransferInput;

namespace Forest;

public partial class ForestContractTests
{
    [Fact]
        public async Task<string> ListWithEnglishAuctionTest()
        {
            await AdminForestContractStub.Initialize.SendAsync(new InitializeInput
            {
                NftContractAddress = NFTContractAddress,
                ServiceFeeReceiver = MarketServiceFeeReceiverAddress
            });

            var symbol = await CreateArtistsTest();

            // await TokenContractStub.Issue.SendAsync(new IssueInput
            // {
            //     Symbol = "ELF",
            //     Amount = InitialELFAmount,
            //     To = DefaultAddress,
            // });
            await TokenContractStub.Transfer.SendAsync(new TransferInput
            {
                Symbol = "ELF",
                Amount = InitialELFAmount,
                To = User2Address,
            });
            await TokenContractStub.Transfer.SendAsync(new TransferInput
            {
                Symbol = "ELF",
                Amount = InitialELFAmount,
                To = User3Address,
            });

            await NFTContractStub.Mint.SendAsync(new MintInput
            {
                Symbol = symbol,
                TokenId = 2,
                Quantity = 1,
                Alias = "Gift2"
            });

            await NFTContractStub.Approve.SendAsync(new ApproveInput
            {
                Symbol = symbol,
                TokenId = 2,
                Amount = 1,
                Spender = ForestContractAddress
            });

            await SellerForestContractStub.ListWithEnglishAuction.SendAsync(new ListWithEnglishAuctionInput
            {
                Symbol = symbol,
                TokenId = 2,
                Duration = new ListDuration
                {
                    DurationHours = 100
                },
                PurchaseSymbol = "ELF",
                StartingPrice = 100_00000000,
                EarnestMoney = 10_00000000
            });

            var auctionInfo = await SellerForestContractStub.GetEnglishAuctionInfo.CallAsync(
                new GetEnglishAuctionInfoInput
                {
                    Symbol = symbol,
                    TokenId = 2
                });
            auctionInfo.Owner.ShouldBe(DefaultAddress);
            auctionInfo.PurchaseSymbol.ShouldBe("ELF");
            auctionInfo.StartingPrice.ShouldBe(100_00000000);
            auctionInfo.Duration.DurationHours.ShouldBe(100);

            return symbol;
        }

        [Fact]
        public async Task<string> PlaceBidForEnglishAuctionTest()
        {
            var symbol = await ListWithEnglishAuctionTest();

            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput
            {
                Symbol = symbol,
                TokenId = 2,
                Quantity = 1,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 90_00000000
                }
            });

            {
                var offerList = await BuyerForestContractStub.GetOfferList.CallAsync(new GetOfferListInput
                {
                    Symbol = symbol,
                    TokenId = 2
                });
                offerList.Value.Count.ShouldBe(1);
                offerList.Value.First().From.ShouldBe(User2Address);
                offerList.Value.First().Price.Amount.ShouldBe(90_00000000);
            }

            await NFTBuyerTokenContractStub.Approve.SendAsync(new AElf.Contracts.MultiToken.ApproveInput
            {
                Symbol = "ELF",
                Amount = long.MaxValue,
                Spender = ForestContractAddress
            });
            await NFTBuyer2TokenContractStub.Approve.SendAsync(new AElf.Contracts.MultiToken.ApproveInput
            {
                Symbol = "ELF",
                Amount = long.MaxValue,
                Spender = ForestContractAddress
            });

            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput
            {
                Symbol = symbol,
                TokenId = 2,
                Quantity = 1,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 110_00000000
                }
            });

            {
                var offerList = await BuyerForestContractStub.GetOfferList.CallAsync(new GetOfferListInput
                {
                    Symbol = symbol,
                    TokenId = 2
                });
                offerList.Value.Count.ShouldBe(1);
            }

            {
                var bidList = await BuyerForestContractStub.GetBidList.CallAsync(new GetBidListInput
                {
                    Symbol = symbol,
                    TokenId = 2
                });
                bidList.Value.Count.ShouldBe(1);
            }

            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput
            {
                Symbol = symbol,
                TokenId = 2,
                Quantity = 1,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 109_00000000
                }
            });

            {
                var offerList = await BuyerForestContractStub.GetOfferList.CallAsync(new GetOfferListInput
                {
                    Symbol = symbol,
                    TokenId = 2
                });
                offerList.Value.Count.ShouldBe(2);
            }

            {
                var bidList = await BuyerForestContractStub.GetBidList.CallAsync(new GetBidListInput
                {
                    Symbol = symbol,
                    TokenId = 2
                });
                bidList.Value.Count.ShouldBe(1);
            }

            return symbol;
        }

        [Fact]
        public async Task DealToEnglishAuctionTest()
        {
            var symbol = await PlaceBidForEnglishAuctionTest();

            await NFTBuyerTokenContractStub.Approve.SendAsync(new AElf.Contracts.MultiToken.ApproveInput
            {
                Symbol = "ELF",
                Amount = long.MaxValue,
                Spender = ForestContractAddress
            });

            await Buyer2ForestContractStub.MakeOffer.SendAsync(new MakeOfferInput
            {
                Symbol = symbol,
                TokenId = 2,
                Quantity = 1,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 105_00000000
                }
            });

            {
                var balance = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput
                {
                    Symbol = "ELF",
                    Owner = User2Address
                });
                balance.Balance.ShouldBe(InitialELFAmount - 10_00000000);
            }

            await SellerForestContractStub.Deal.SendAsync(new DealInput
            {
                Symbol = symbol,
                TokenId = 2,
                OfferFrom = User2Address,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 110_00000000
                },
                Quantity = 1
            });

            {
                var balance = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput
                {
                    Symbol = "ELF",
                    Owner = User2Address
                });
                balance.Balance.ShouldBe(InitialELFAmount - 110_00000000);
            }

            {
                var balance = await NFTContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Symbol = symbol,
                    TokenId = 2,
                    Owner = User2Address
                });
                balance.Balance.ShouldBe(1);
            }

            {
                var balance = await NFTContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Symbol = symbol,
                    TokenId = 2,
                    Owner = DefaultAddress
                });
                balance.Balance.ShouldBe(0);
            }

            {
                var balance = await TokenContractStub.GetBalance.CallAsync(new AElf.Contracts.MultiToken.GetBalanceInput
                {
                    Symbol = "ELF",
                    Owner = User3Address
                });
                balance.Balance.ShouldBe(InitialELFAmount);
            }
        }

        [Fact]
        public async Task<string> ListWithDutchAuctionTest()
        {
            await AdminForestContractStub.Initialize.SendAsync(new InitializeInput
            {
                NftContractAddress = NFTContractAddress,
                ServiceFeeReceiver = MarketServiceFeeReceiverAddress
            });

            var symbol = await CreateArtistsTest();

            await NFTContractStub.Mint.SendAsync(new MintInput
            {
                Symbol = symbol,
                TokenId = 2,
                Quantity = 1,
                Alias = "Gift2"
            });

            // await TokenContractStub.Issue.SendAsync(new IssueInput
            // {
            //     Symbol = "ELF",
            //     Amount = InitialELFAmount,
            //     To = DefaultAddress,
            // });
            await TokenContractStub.Transfer.SendAsync(new TransferInput
            {
                Symbol = "ELF",
                Amount = InitialELFAmount,
                To = User2Address,
            });

            await NFTContractStub.Approve.SendAsync(new ApproveInput
            {
                Symbol = symbol,
                TokenId = 2,
                Amount = 1,
                Spender = ForestContractAddress
            });

            await SellerForestContractStub.ListWithDutchAuction.SendAsync(new ListWithDutchAuctionInput
            {
                Symbol = symbol,
                TokenId = 2,
                Duration = new ListDuration
                {
                    DurationHours = 100
                },
                PurchaseSymbol = "ELF",
                StartingPrice = 100_00000000,
                EndingPrice = 50_00000000
            });

            var auctionInfo = await SellerForestContractStub.GetDutchAuctionInfo.CallAsync(
                new GetDutchAuctionInfoInput
                {
                    Symbol = symbol,
                    TokenId = 2
                });
            auctionInfo.Owner.ShouldBe(DefaultAddress);
            auctionInfo.PurchaseSymbol.ShouldBe("ELF");
            auctionInfo.StartingPrice.ShouldBe(100_00000000);
            auctionInfo.EndingPrice.ShouldBe(50_00000000);
            auctionInfo.Duration.DurationHours.ShouldBe(100);

            return symbol;
        }

        [Fact]
        public async Task PlaceBidForDutchAuctionTest()
        {
            var symbol = await ListWithDutchAuctionTest();
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput
            {
                Symbol = symbol,
                TokenId = 2,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 49_00000000
                },
                Quantity = 1
            });

            {
                var offerList = await BuyerForestContractStub.GetOfferList.CallAsync(new GetOfferListInput
                {
                    Symbol = symbol,
                    TokenId = 2
                });
                offerList.Value.Count.ShouldBe(1);
            }

            await NFTBuyerTokenContractStub.Approve.SendAsync(new AElf.Contracts.MultiToken.ApproveInput
            {
                Symbol = "ELF",
                Amount = long.MaxValue,
                Spender = ForestContractAddress
            });

            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput
            {
                Symbol = symbol,
                TokenId = 2,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 100_00000000
                },
                Quantity = 1
            });

            {
                var balance = await NFTContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Symbol = symbol,
                    TokenId = 2,
                    Owner = User2Address
                });
                balance.Balance.ShouldBe(1);
            }

            {
                var offerList = await BuyerForestContractStub.GetOfferList.CallAsync(new GetOfferListInput
                {
                    Symbol = symbol,
                    TokenId = 2
                });
                offerList.Value.Count.ShouldBe(1);
            }

            {
                var balance = await NFTContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Symbol = symbol,
                    TokenId = 2,
                    Owner = DefaultAddress
                });
                balance.Balance.ShouldBe(0);
            }
        }

        [Fact]
        public async Task DelistEnglishAuctionNFTTest()
        {
            var symbol = await ListWithEnglishAuctionTest();

            await NFTBuyerTokenContractStub.Approve.SendAsync(new AElf.Contracts.MultiToken.ApproveInput
            {
                Symbol = "ELF",
                Amount = long.MaxValue,
                Spender = ForestContractAddress
            });
            await NFTBuyer2TokenContractStub.Approve.SendAsync(new AElf.Contracts.MultiToken.ApproveInput
            {
                Symbol = "ELF",
                Amount = long.MaxValue,
                Spender = ForestContractAddress
            });

            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput
            {
                Symbol = symbol,
                TokenId = 2,
                Quantity = 1,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 200_00000000
                }
            });
            
            await Buyer2ForestContractStub.MakeOffer.SendAsync(new MakeOfferInput
            {
                Symbol = symbol,
                TokenId = 2,
                Quantity = 1,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 300_00000000
                }
            });

            await TokenContractStub.Approve.SendAsync(new AElf.Contracts.MultiToken.ApproveInput
            {
                Symbol = "ELF",
                Spender = ForestContractAddress,
                Amount = 1_00000000
            });

            await SellerForestContractStub.Delist.SendAsync(new DelistInput
            {
                Symbol = symbol,
                TokenId = 2,
                Quantity = 1,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 100_00000000
                }
            });

            var listedNftList = (await SellerForestContractStub.GetListedNFTInfoList.CallAsync(
                new GetListedNFTInfoListInput
                {
                    Symbol = symbol,
                    TokenId = 2,
                    Owner = DefaultAddress
                }));
            listedNftList.Value.Count.ShouldBe(0);
        }
    
}
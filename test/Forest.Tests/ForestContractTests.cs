using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Contracts.NFT;
using Forest.Whitelist;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;
using ApproveInput = AElf.Contracts.MultiToken.ApproveInput;
using CreateInput = AElf.Contracts.NFT.CreateInput;
using GetBalanceInput = AElf.Contracts.MultiToken.GetBalanceInput;
using TransferInput = AElf.Contracts.MultiToken.TransferInput;

namespace Forest
{
    public partial class ForestContractTests : ForestContractTestBase
    {
        private const long InitialELFAmount = 1_00000000_00000000;

        private const long DefaultSenderInitialELFAmount = 780000000_00000000;

        [Fact]
        public async Task<string> CreateArtistsTest()
        {
            var executionResult = await NFTContractStub.Create.SendAsync(new CreateInput
            {
                // ProtocolName = "aelf Art",
                NftType = NFTType.Art.ToString(),
                TotalSupply = 1000,
                IsBurnable = false,
                IsTokenIdReuse = false
            });
            var symbol = executionResult.Output.Value;

            // var nftProtocolInfo = await NFTContractStub.GetNFTProtocolInfo.CallAsync(new StringValue {Value = symbol});
            // nftProtocolInfo.TotalSupply.ShouldBe(1000);

            return symbol;
        }

        private async Task InitializeForestContract()
        {
            await AdminForestContractStub.Initialize.SendAsync(new InitializeInput
            {
                NftContractAddress = NFTContractAddress,
                ServiceFeeReceiver = MarketServiceFeeReceiverAddress
            });

            await AdminForestContractStub.SetWhitelistContract.SendAsync(WhitelistContractAddress);
        }

        private async Task<string> MintBadgeTest()
        {
            await InitializeForestContract();
            var createWhitelistInput = new CreateWhitelistInput
            {
                ProjectId = HashHelper.ComputeFrom("Badge Test"),
                Creator = ForestContractAddress,
                ExtraInfoList = new ExtraInfoList()
                {
                    Value =
                    {
                        new ExtraInfo()
                        {
                            AddressList = new Whitelist.AddressList
                            {
                                Value = {DefaultAddress}
                            }
                        }
                    }
                },
                IsCloneable = true,
                StrategyType = StrategyType.Basic,
                Remark = "Badge Test"
            };
            var createWhitelistResult = await WhitelistContractStub.CreateWhitelist.SendAsync(createWhitelistInput);
            var whitelistId = createWhitelistResult.Output;

            var executionResult = await NFTContractStub.Create.SendAsync(new CreateInput
            {
                BaseUri = BaseUri,
                Creator = DefaultAddress,
                IsBurnable = true,
                Metadata = new Metadata
                {
                    Value =
                    {
                        {"Description", "Stands for the human race."}
                    }
                },
                NftType = NFTType.Badges.ToString(),
                // ProtocolName = "Badge",
                IsTokenIdReuse = true,
                MinterList = new MinterList
                {
                    Value = {ForestContractAddress}
                },
                TotalSupply = 1_000_000_000 // One billion
            });
            var symbol = executionResult.Output.Value;
            await NFTContractStub.Mint.SendAsync(new MintInput
            {
                Symbol = symbol,
                Alias = "badge",
                Metadata = new Metadata
                {
                    Value =
                    {
                        {"Special Property", "A Value"},
                        {"aelf_badge_whitelist", whitelistId.ToHex()}
                    }
                },
                Owner = DefaultAddress,
                Uri = $"{BaseUri}foo"
            });
            await AdminForestContractStub.MintBadge.SendAsync(new MintBadgeInput()
            {
                Symbol = symbol,
                // // TokenId = 233
            });
            return symbol;
        }

        [Fact(Skip = "Badge not found")]
        public async Task<string> ListWithFixedPriceTest()
        {
            
            var symbol = await MintBadgeTest();

            await TokenContractStub.Issue.SendAsync(new IssueInput
            {
                Symbol = "ELF",
                Amount = InitialELFAmount,
                To = DefaultAddress,
            });
            await TokenContractStub.Issue.SendAsync(new IssueInput
            {
                Symbol = "ELF",
                Amount = InitialELFAmount,
                To = User2Address,
            });

            await NFTContractStub.Approve.SendAsync(new AElf.Contracts.NFT.ApproveInput
            {
                Symbol = symbol,
                // TokenId = 233,
                Amount = 100,
                Spender = ForestContractAddress
            });

            await SellerForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = symbol,
                // // TokenId = 233,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 100_00000000
                },
                Duration = new ListDuration
                {
                    DurationHours = 24
                },
                Quantity = 1,
                IsWhitelistAvailable = false
            });

            var listedNftInfo = (await SellerForestContractStub.GetListedNFTInfoList.CallAsync(
                new GetListedNFTInfoListInput
                {
                    Symbol = symbol,
                    // // TokenId = 233,
                    Owner = DefaultAddress
                })).Value.First();
            listedNftInfo.Price.Symbol.ShouldBe("ELF");
            listedNftInfo.Price.Amount.ShouldBe(100_00000000);
            listedNftInfo.Quantity.ShouldBe(1);
            listedNftInfo.ListType.ShouldBe(ListType.FixedPrice);
            listedNftInfo.Duration.StartTime.ShouldNotBeNull();
            listedNftInfo.Duration.DurationHours.ShouldBe(24);

            {
                var executionResult = await SellerForestContractStub.ListWithFixedPrice.SendWithExceptionAsync(
                    new ListWithFixedPriceInput
                    {
                        Symbol = symbol,
                        // // TokenId = 233,
                        Price = new Price
                        {
                            Symbol = "ELF",
                            Amount = 100_00000000
                        },
                        Duration = new ListDuration
                        {
                            DurationHours = 24
                        },
                        Quantity = 1
                    });
                executionResult.TransactionResult.Error.ShouldContain("Check sender NFT balance failed.");
            }

            return symbol;
        }

        [Fact]
        public async Task<string> ListWithFixedPriceTest_WithWhitelist()
        {
            await InitializeForestContract();

            var executionResult = await NFTContractStub.Create.SendAsync(new CreateInput
            {
                // ProtocolName = "aelf Collections",
                NftType = NFTType.Collectables.ToString(),
                TotalSupply = 1000,
                IsBurnable = false,
                IsTokenIdReuse = true
            });
            var symbol = executionResult.Output.Value;

            await NFTContractStub.Mint.SendAsync(new MintInput
            {
                Symbol = symbol,
                Alias = "test",
                Quantity = 20,
                // TokenId = 233
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

            await NFTContractStub.Approve.SendAsync(new AElf.Contracts.NFT.ApproveInput
            {
                Symbol = symbol,
                // TokenId = 233,
                Amount = 100,
                Spender = ForestContractAddress
            });

            await SellerForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = symbol,
                // // TokenId = 233,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 100_00000000
                },
                Duration = new ListDuration
                {
                    DurationHours = 24
                },
                Quantity = 1,
                IsWhitelistAvailable = false
            });

            return symbol;
        }

        [Fact(Skip = "Badge not found.")]
        public async Task DealWithFixedPriceTest()
        {
            var symbol = await ListWithFixedPriceTest();
        
            await NFTBuyerTokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Symbol = "ELF",
                Amount = long.MaxValue,
                Spender = ForestContractAddress
            });
        
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput
            {
                Symbol = symbol,
                // // TokenId = 233,
                OfferTo = DefaultAddress,
                Quantity = 1,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 200_00000000
                },
            });
        
            {
                var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Symbol = "ELF",
                    Owner = User2Address
                });
                balance.Balance.ShouldBe(InitialELFAmount - 100_00000000);
            }
        
            {
                var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Symbol = "ELF",
                    Owner = DefaultAddress
                });
                // Because of 10/10000 service fee.
                balance.Balance.ShouldBe(InitialELFAmount + 100_00000000 - 100_00000000 / 1000);
            }
        
            {
                var nftBalance = await NFTContractStub.GetBalance.CallAsync(new AElf.Contracts.NFT.GetBalanceInput
                {
                    Symbol = symbol,
                    // TokenId = 233,
                    Owner = User2Address
                });
                nftBalance.Balance.ShouldBe(1);
            }
        
            {
                var nftBalance = await NFTContractStub.GetBalance.CallAsync(new AElf.Contracts.NFT.GetBalanceInput
                {
                    Symbol = symbol,
                    // TokenId = 233,
                    Owner = DefaultAddress
                });
                nftBalance.Balance.ShouldBe(0);
            }
        }

        [Fact(Skip = "Badge not found.")]
        public async Task<string> MakeOfferToFixedPrice()
        {
            var symbol = await ListWithFixedPriceTest();
        
            await NFTBuyerTokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Symbol = "ELF",
                Amount = long.MaxValue,
                Spender = ForestContractAddress
            });
        
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput
            {
                Symbol = symbol,
                // // TokenId = 233,
                OfferTo = DefaultAddress,
                Quantity = 2,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 90_00000000
                },
            });
        
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput
            {
                Symbol = symbol,
                // // TokenId = 233,
                OfferTo = DefaultAddress,
                Quantity = 1,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 99_00000000
                },
            });
        
            var offerAddressList = await BuyerForestContractStub.GetOfferAddressList.CallAsync(
                new GetAddressListInput
                {
                    Symbol = symbol,
                    // // TokenId = 233
                });
            offerAddressList.Value.ShouldContain(User2Address);
        
            var offerList = await BuyerForestContractStub.GetOfferList.CallAsync(new GetOfferListInput
            {
                Symbol = symbol,
                // // TokenId = 233,
                Address = User2Address
            });
            offerList.Value.Count.ShouldBe(2);
            offerList.Value.First().Quantity.ShouldBe(2);
            offerList.Value.Last().Quantity.ShouldBe(1);
        
            return symbol;
        }

        [Fact(Skip = "Badge not found.")]
        public async Task DealToOfferWhenFixedPrice()
        {
            var symbol = await MakeOfferToFixedPrice();
        
            // Set royalty.
            await CreatorForestContractStub.SetRoyalty.SendAsync(new SetRoyaltyInput
            {
                Symbol = symbol,
                // // TokenId = 233,
                Royalty = 10,
                RoyaltyFeeReceiver = MarketServiceFeeReceiverAddress
            });
        
            var offerList = await BuyerForestContractStub.GetOfferList.CallAsync(new GetOfferListInput
            {
                Symbol = symbol,
                // // TokenId = 233
            });
            offerList.Value.Count.ShouldBe(2);
        
            var offer = offerList.Value.First();
            var executionResult = await SellerForestContractStub.Deal.SendWithExceptionAsync(new DealInput
            {
                Symbol = symbol,
                // // TokenId = 233,
                OfferFrom = offer.From,
                Quantity = 1,
                Price = offer.Price
            });
            executionResult.TransactionResult.Error.ShouldContain("Need to delist");
        
            await SellerForestContractStub.Delist.SendAsync(new DelistInput
            {
                Symbol = symbol,
                // // TokenId = 233,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 100_00000000
                },
                Quantity = 1
            });
        
            var executionResult1 = await SellerForestContractStub.Deal.SendAsync(new DealInput
            {
                Symbol = symbol,
                // // TokenId = 233,
                OfferFrom = offer.From,
                Quantity = 1,
                Price = offer.Price
            });
            var log = OfferChanged.Parser.ParseFrom(executionResult1.TransactionResult.Logs
                .First(l => l.Name == nameof(OfferChanged)).NonIndexed);
            log.Quantity.ShouldBe(1);
            log.Price.Amount.ShouldBe(90_00000000);
            offer.From.ShouldBe(User2Address);
            {
                var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Symbol = "ELF",
                    Owner = User2Address
                });
                balance.Balance.ShouldBe(InitialELFAmount - 90_00000000);
            }
        
            {
                var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Symbol = "ELF",
                    Owner = DefaultAddress
                });
                // Because of 10/10000 service fee.
                balance.Balance.ShouldBe(InitialELFAmount + 90_00000000 - 90_00000000 / 1000 - 90_00000000 / 1000);
            }
        
            {
                var nftBalance = await NFTContractStub.GetBalance.CallAsync(new AElf.Contracts.NFT.GetBalanceInput
                {
                    Symbol = symbol,
                    // TokenId = 233,
                    Owner = User2Address
                });
                nftBalance.Balance.ShouldBe(1);
            }
        
            {
                var nftBalance = await NFTContractStub.GetBalance.CallAsync(new AElf.Contracts.NFT.GetBalanceInput
                {
                    Symbol = symbol,
                    // TokenId = 233,
                    Owner = DefaultAddress
                });
                nftBalance.Balance.ShouldBe(0);
            }
        }

        [Fact(Skip = "Badge not found.")]
        public async Task DealToOfferWhenNotListed()
        {
            
            var symbol = await MintBadgeTest();
            
            await TokenContractStub.Issue.SendAsync(new IssueInput
            {
                Symbol = "ELF",
                Amount = InitialELFAmount,
                To = DefaultAddress,
            });
            await TokenContractStub.Issue.SendAsync(new IssueInput
            {
                Symbol = "ELF",
                Amount = InitialELFAmount,
                To = User2Address,
            });
            
            await NFTContractStub.Approve.SendAsync(new AElf.Contracts.NFT.ApproveInput
            {
                Symbol = symbol,
                // TokenId = 233,
                Amount = 1,
                Spender = ForestContractAddress
            });
            
            await NFTBuyerTokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Symbol = "ELF",
                Amount = long.MaxValue,
                Spender = ForestContractAddress
            });
            
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput
            {
                Symbol = symbol,
                // // TokenId = 233,
                OfferTo = DefaultAddress,
                Quantity = 1,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 1000_00000000
                },
            });
            
            var offerList = await BuyerForestContractStub.GetOfferList.CallAsync(new GetOfferListInput
            {
                Symbol = symbol,
                // // TokenId = 233
            });
            offerList.Value.Count.ShouldBe(1);
            
            var offer = offerList.Value.First();
            await SellerForestContractStub.Deal.SendAsync(new DealInput
            {
                Symbol = symbol,
                // // TokenId = 233,
                OfferFrom = offer.From,
                Quantity = 1,
                Price = offer.Price
            });
            
            {
                var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Symbol = "ELF",
                    Owner = User2Address
                });
                balance.Balance.ShouldBe(InitialELFAmount - 1000_00000000);
            }
            
            {
                var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Symbol = "ELF",
                    Owner = DefaultAddress
                });
                // Because of 10/10000 service fee.
                balance.Balance.ShouldBe(InitialELFAmount + 1000_00000000 - 1000_00000000 / 1000);
            }
            
            {
                var nftBalance = await NFTContractStub.GetBalance.CallAsync(new AElf.Contracts.NFT.GetBalanceInput
                {
                    Symbol = symbol,
                    // TokenId = 233,
                    Owner = User2Address
                });
                nftBalance.Balance.ShouldBe(1);
            }
            
            {
                var nftBalance = await NFTContractStub.GetBalance.CallAsync(new AElf.Contracts.NFT.GetBalanceInput
                {
                    Symbol = symbol,
                    // TokenId = 233,
                    Owner = DefaultAddress
                });
                nftBalance.Balance.ShouldBe(0);
            }
        }

        [Fact]
        public async Task TokenWhiteListTest()
        {
            await AdminForestContractStub.Initialize.SendAsync(new InitializeInput
            {
                NftContractAddress = NFTContractAddress,
                ServiceFeeReceiver = MarketServiceFeeReceiverAddress
            });

            await AdminForestContractStub.SetGlobalTokenWhiteList.SendAsync(new StringList
            {
                Value = {"USDT", "EAN"}
            });

            var globalTokenWhiteList = await AdminForestContractStub.GetGlobalTokenWhiteList.CallAsync(new Empty());
            globalTokenWhiteList.Value.Count.ShouldBe(3);
            globalTokenWhiteList.Value.ShouldContain("EAN");
            globalTokenWhiteList.Value.ShouldContain("ELF");
            globalTokenWhiteList.Value.ShouldContain("USDT");

            var symbol = await CreateArtistsTest();

            await CreatorForestContractStub.SetTokenWhiteList.SendAsync(new SetTokenWhiteListInput
            {
                Symbol = symbol,
                TokenWhiteList = new StringList
                {
                    Value = {"TEST"}
                }
            });

            {
                var tokenWhiteList =
                    await CreatorForestContractStub.GetTokenWhiteList.CallAsync(new StringValue {Value = symbol});
                tokenWhiteList.Value.Count.ShouldBe(4);
                tokenWhiteList.Value.ShouldContain("ELF");
                tokenWhiteList.Value.ShouldContain("TEST");
            }

            await AdminForestContractStub.SetGlobalTokenWhiteList.SendAsync(new StringList
            {
                Value = {"USDT", "EAN", "NEW"}
            });

            {
                var tokenWhiteList =
                    await CreatorForestContractStub.GetTokenWhiteList.CallAsync(new StringValue {Value = symbol});
                tokenWhiteList.Value.Count.ShouldBe(5);
                tokenWhiteList.Value.ShouldContain("NEW");
                tokenWhiteList.Value.ShouldContain("TEST");
            }

            await AdminForestContractStub.SetGlobalTokenWhiteList.SendAsync(new StringList
            {
                Value = {"ELF"}
            });

            {
                var tokenWhiteList =
                    await CreatorForestContractStub.GetTokenWhiteList.CallAsync(new StringValue {Value = symbol});
                tokenWhiteList.Value.Count.ShouldBe(2);
                tokenWhiteList.Value.ShouldContain("ELF");
                tokenWhiteList.Value.ShouldContain("TEST");
            }
        }

        [Fact]
        public async Task MakeOfferListedFixedPrice_Normal()
        {
            await InitializeForestContract();

            var executionResult = await NFTContractStub.Create.SendAsync(new CreateInput
            {
                // ProtocolName = "aelf Collections",
                NftType = NFTType.Collectables.ToString(),
                TotalSupply = 1000,
                IsBurnable = false,
                IsTokenIdReuse = true
            });
            var symbol = executionResult.Output.Value;

            await NFTContractStub.Mint.SendAsync(new MintInput
            {
                Symbol = symbol,
                Alias = "test",
                Quantity = 20,
                // TokenId = 233
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

            await NFTContractStub.Approve.SendAsync(new AElf.Contracts.NFT.ApproveInput
            {
                Symbol = symbol,
                // TokenId = 233,
                Amount = 100,
                Spender = ForestContractAddress
            });

            await SellerForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = symbol,
                // TokenId = 233,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 100_00000000
                },
                Duration = new ListDuration
                {
                    DurationHours = 24
                },
                Quantity = 5,
                IsWhitelistAvailable = false,
                IsMergeToPreviousListedInfo = false
            });
            await SellerForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = symbol,
                // TokenId = 233,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 200_00000000
                },
                Duration = new ListDuration
                {
                    DurationHours = 24
                },
                Quantity = 7,
                IsWhitelistAvailable = false,
                IsMergeToPreviousListedInfo = false
            });
            await SellerForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = symbol,
                // TokenId = 233,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 300_00000000
                },
                Duration = new ListDuration
                {
                    DurationHours = 24
                },
                Quantity = 8,
                IsWhitelistAvailable = false,
                IsMergeToPreviousListedInfo = false
            });
            await NFTBuyerTokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Symbol = "ELF",
                Amount = long.MaxValue,
                Spender = ForestContractAddress
            });
            var executionResult1 = await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput
            {
                Symbol = symbol,
                // TokenId = 233,
                OfferTo = DefaultAddress,
                Quantity = 4,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 100_00000000
                },
            });
            var log = ListedNFTChanged.Parser.ParseFrom(executionResult1.TransactionResult.Logs
                .First(l => l.Name == nameof(ListedNFTChanged)).NonIndexed);
            // log.TokenId.ShouldBe(233);
            var executionResult2 = await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput
            {
                Symbol = symbol,
                // TokenId = 233,
                OfferTo = DefaultAddress,
                Quantity = 2,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 200_00000000
                },
            });
            var log2 = ListedNFTRemoved.Parser.ParseFrom(executionResult2.TransactionResult.Logs
                .First(l => l.Name == nameof(ListedNFTRemoved)).NonIndexed);
            log2.Price.Amount.ShouldBe(100_00000000);
            var log1 = ListedNFTChanged.Parser.ParseFrom(executionResult2.TransactionResult.Logs
                .First(l => l.Name == nameof(ListedNFTChanged)).NonIndexed);
            log1.Quantity.ShouldBe(6);
            {
                var nftBalance = await NFTContractStub.GetBalance.CallAsync(new AElf.Contracts.NFT.GetBalanceInput
                {
                    Symbol = symbol,
                    // TokenId = 233,
                    Owner = User2Address
                });
                nftBalance.Balance.ShouldBe(6);
            }
            {
                var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Symbol = "ELF",
                    Owner = User2Address
                });
                balance.Balance.ShouldBe(InitialELFAmount - 400_00000000 - 100_00000000 - 200_00000000);
            }
            var executionResult3 = await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput
            {
                Symbol = symbol,
                // TokenId = 233,
                OfferTo = DefaultAddress,
                Quantity = 1,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 100_00000000
                },
            });
            {
                var nftBalance = await NFTContractStub.GetBalance.CallAsync(new AElf.Contracts.NFT.GetBalanceInput
                {
                    Symbol = symbol,
                    // TokenId = 233,
                    Owner = User2Address
                });
                nftBalance.Balance.ShouldBe(6);
            }
            {
                var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Symbol = "ELF",
                    Owner = User2Address
                });
                balance.Balance.ShouldBe(InitialELFAmount - 500_00000000 - 200_00000000);
            }
        }

        [Fact]
        public async Task MakeOffer()
        {
            await InitializeForestContract();

            var executionResult = await NFTContractStub.Create.SendAsync(new CreateInput
            {
                // ProtocolName = "aelf Collections",
                NftType = NFTType.Collectables.ToString(),
                TotalSupply = 1000,
                IsBurnable = false,
                IsTokenIdReuse = true
            });
            var symbol = executionResult.Output.Value;

            await NFTContractStub.Mint.SendAsync(new MintInput
            {
                Symbol = symbol,
                Alias = "test",
                Quantity = 20,
                // TokenId = 233
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

            await NFTContractStub.Approve.SendAsync(new AElf.Contracts.NFT.ApproveInput
            {
                Symbol = symbol,
                // TokenId = 233,
                Amount = 100,
                Spender = ForestContractAddress
            });

            await SellerForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = symbol,
                // TokenId = 233,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 100_00000000
                },
                Duration = new ListDuration
                {
                    DurationHours = 24
                },
                Quantity = 5,
                IsWhitelistAvailable = false,
                IsMergeToPreviousListedInfo = false
            });
            await SellerForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = symbol,
                // TokenId = 233,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 200_00000000
                },
                Duration = new ListDuration
                {
                    DurationHours = 24
                },
                Quantity = 7,
                IsWhitelistAvailable = false,
                IsMergeToPreviousListedInfo = false
            });
            await SellerForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = symbol,
                // TokenId = 233,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 300_00000000
                },
                Duration = new ListDuration
                {
                    DurationHours = 24
                },
                Quantity = 8,
                IsWhitelistAvailable = false,
                IsMergeToPreviousListedInfo = false
            });
            await NFTBuyerTokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Symbol = "ELF",
                Amount = long.MaxValue,
                Spender = ForestContractAddress
            });
            var executionResult1 = await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput
            {
                Symbol = symbol,
                // TokenId = 233,
                OfferTo = DefaultAddress,
                Quantity = 21,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 300_00000000
                },
            });
            {
                var nftBalance = await NFTContractStub.GetBalance.CallAsync(new AElf.Contracts.NFT.GetBalanceInput
                {
                    Symbol = symbol,
                    // TokenId = 233,
                    Owner = User2Address
                });
                nftBalance.Balance.ShouldBe(20);
            }
            {
                var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Symbol = "ELF",
                    Owner = User2Address
                });
                balance.Balance.ShouldBe(InitialELFAmount - 100_00000000 * 5 - 200_00000000 * 7 - 300_00000000 * 8);
            }
            {
                var offerList = await BuyerForestContractStub.GetOfferList.CallAsync(new GetOfferListInput
                {
                    Symbol = symbol,
                    Address = User2Address,
                    // TokenId = 233
                });
                offerList.Value.Count.ShouldBe(1);
                offerList.Value[0].Quantity.ShouldBe(1);
            }
        }

        [Fact]
        public async Task MakeOffer_Merge_Normal()
        {
            await InitializeForestContract();

            var executionResult = await NFTContractStub.Create.SendAsync(new CreateInput
            {
                // ProtocolName = "aelf Collections",
                NftType = NFTType.Collectables.ToString(),
                TotalSupply = 1000,
                IsBurnable = false,
                IsTokenIdReuse = true
            });
            var symbol = executionResult.Output.Value;

            await NFTContractStub.Mint.SendAsync(new MintInput
            {
                Symbol = symbol,
                Alias = "test",
                Quantity = 20,
                // TokenId = 233
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

            await NFTContractStub.Approve.SendAsync(new AElf.Contracts.NFT.ApproveInput
            {
                Symbol = symbol,
                // TokenId = 233,
                Amount = 100,
                Spender = ForestContractAddress
            });

            await SellerForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = symbol,
                // TokenId = 233,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 100_00000000
                },
                Duration = new ListDuration
                {
                    DurationHours = 24
                },
                Quantity = 5,
                IsWhitelistAvailable = true,
                Whitelists = new WhitelistInfoList
                {
                    Whitelists =
                    {
                        new WhitelistInfo
                        {
                            AddressList = new AddressList
                            {
                                Value = {User3Address}
                            },
                            PriceTag = new PriceTagInfo
                            {
                                TagName = "90_00000000 ELF",
                                Price = new Price
                                {
                                    Symbol = "ELF",
                                    Amount = 90_00000000
                                }
                            }
                        }
                    }
                },
                IsMergeToPreviousListedInfo = true
            });
            await SellerForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = symbol,
                // TokenId = 233,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 100_00000000
                },
                Duration = new ListDuration
                {
                    DurationHours = 24
                },
                Quantity = 7,
                IsWhitelistAvailable = false,
                IsMergeToPreviousListedInfo = true
            });
            await NFTBuyerTokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Symbol = "ELF",
                Amount = long.MaxValue,
                Spender = ForestContractAddress
            });
            var executionResult1 = await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput
            {
                Symbol = symbol,
                // TokenId = 233,
                OfferTo = DefaultAddress,
                Quantity = 7,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 100_00000000
                },
            });
            {
                var nftBalance = await NFTContractStub.GetBalance.CallAsync(new AElf.Contracts.NFT.GetBalanceInput
                {
                    Symbol = symbol,
                    // TokenId = 233,
                    Owner = User2Address
                });
                nftBalance.Balance.ShouldBe(7);
            }
            {
                var info = await SellerForestContractStub.GetListedNFTInfoList.CallAsync(
                    new GetListedNFTInfoListInput
                    {
                        Symbol = symbol,
                        // TokenId = 233,
                        Owner = DefaultAddress
                    });
                info.Value.Count.ShouldBe(1);
                info.Value[0].Quantity.ShouldBe(5);
            }
            {
                var log = ListedNFTChanged.Parser.ParseFrom(executionResult1.TransactionResult.Logs
                    .First(l => l.Name == nameof(ListedNFTChanged)).NonIndexed);
                log.Quantity.ShouldBe(5);
            }
            {
                var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Symbol = "ELF",
                    Owner = User2Address
                });
                balance.Balance.ShouldBe(InitialELFAmount - 100_00000000 * 7);
            }
            {
                var offerList = await BuyerForestContractStub.GetOfferList.CallAsync(new GetOfferListInput
                {
                    Symbol = symbol,
                    Address = User2Address,
                    // TokenId = 233
                });
                offerList.Value.Count.ShouldBe(0);
            }
        }

        [Fact]
        public async Task MakeOffer_Merge_Whitelist()
        {
            await InitializeForestContract();

            var executionResult = await NFTContractStub.Create.SendAsync(new CreateInput
            {
                // ProtocolName = "aelf Collections",
                NftType = NFTType.Collectables.ToString(),
                TotalSupply = 1000,
                IsBurnable = false,
                IsTokenIdReuse = true
            });
            var symbol = executionResult.Output.Value;

            await NFTContractStub.Mint.SendAsync(new MintInput
            {
                Symbol = symbol,
                Alias = "test",
                Quantity = 20,
                // TokenId = 233
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

            await NFTContractStub.Approve.SendAsync(new AElf.Contracts.NFT.ApproveInput
            {
                Symbol = symbol,
                // TokenId = 233,
                Amount = 100,
                Spender = ForestContractAddress
            });

            await SellerForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = symbol,
                // TokenId = 233,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 100_00000000
                },
                Duration = new ListDuration
                {
                    DurationHours = 24
                },
                Quantity = 5,
                IsWhitelistAvailable = true,
                Whitelists = new WhitelistInfoList
                {
                    Whitelists =
                    {
                        new WhitelistInfo()
                        {
                            AddressList = new AddressList
                            {
                                Value = {User2Address}
                            },
                            PriceTag = new PriceTagInfo
                            {
                                TagName = "90_00000000 ELF",
                                Price = new Price
                                {
                                    Symbol = "ELF",
                                    Amount = 90_00000000
                                }
                            }
                        }
                    }
                },
                IsMergeToPreviousListedInfo = true
            });
            await SellerForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = symbol,
                // TokenId = 233,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 100_00000000
                },
                Duration = new ListDuration
                {
                    DurationHours = 24
                },
                Quantity = 7,
                IsWhitelistAvailable = true,
                IsMergeToPreviousListedInfo = true
            });
            await NFTBuyerTokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Symbol = "ELF",
                Amount = long.MaxValue,
                Spender = ForestContractAddress
            });
            var executionResult1 = await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput
            {
                Symbol = symbol,
                // TokenId = 233,
                OfferTo = DefaultAddress,
                Quantity = 7,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 100_00000000
                },
            });
            {
                var nftBalance = await NFTContractStub.GetBalance.CallAsync(new AElf.Contracts.NFT.GetBalanceInput
                {
                    Symbol = symbol,
                    // TokenId = 233,
                    Owner = User2Address
                });
                nftBalance.Balance.ShouldBe(7);
            }
            {
                var info = await SellerForestContractStub.GetListedNFTInfoList.CallAsync(
                    new GetListedNFTInfoListInput
                    {
                        Symbol = symbol,
                        // TokenId = 233,
                        Owner = DefaultAddress
                    });
                info.Value.Count.ShouldBe(1);
                info.Value[0].Quantity.ShouldBe(5);
            }
            {
                var log1 = ListedNFTChanged.Parser.ParseFrom(executionResult1.TransactionResult.Logs
                    .First(l => l.Name == nameof(ListedNFTChanged)).NonIndexed);
                log1.Quantity.ShouldBe(11);
                var log = ListedNFTChanged.Parser.ParseFrom(executionResult1.TransactionResult.Logs
                    .Last(l => l.Name == nameof(ListedNFTChanged)).NonIndexed);
                log.Quantity.ShouldBe(5);
            }
            {
                var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Symbol = "ELF",
                    Owner = User2Address
                });
                balance.Balance.ShouldBe(InitialELFAmount - 100_00000000 * 6 - 90_00000000);
            }
            {
                var offerList = await BuyerForestContractStub.GetOfferList.CallAsync(new GetOfferListInput
                {
                    Symbol = symbol,
                    Address = User2Address,
                    // TokenId = 233
                });
                offerList.Value.Count.ShouldBe(0);
            }
        }

        [Fact]
        public async Task MakeOffer_ServiceFee()
        {
            await AdminForestContractStub.Initialize.SendAsync(new InitializeInput
            {
                NftContractAddress = NFTContractAddress,
                ServiceFeeReceiver = MarketServiceFeeReceiverAddress
            });

            await AdminForestContractStub.SetWhitelistContract.SendAsync(WhitelistContractAddress);
            await AdminForestContractStub.SetServiceFee.SendAsync(new SetServiceFeeInput
            {
                ServiceFeeRate = 10,
                ServiceFeeReceiver = DefaultAddress
            });

            var executionResult = await NFTContractStub.Create.SendAsync(new CreateInput
            {
                // ProtocolName = "aelf Collections",
                NftType = NFTType.Collectables.ToString(),
                TotalSupply = 1000,
                IsBurnable = false,
                IsTokenIdReuse = true
            });
            var symbol = executionResult.Output.Value;

            await NFTContractStub.Mint.SendAsync(new MintInput
            {
                Symbol = symbol,
                Alias = "test",
                Quantity = 20,
                // TokenId = 233
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

            await NFTContractStub.Approve.SendAsync(new AElf.Contracts.NFT.ApproveInput
            {
                Symbol = symbol,
                // TokenId = 233,
                Amount = 100,
                Spender = ForestContractAddress
            });
            await NFT2ContractStub.Approve.SendAsync(new AElf.Contracts.NFT.ApproveInput
            {
                Symbol = symbol,
                // TokenId = 233,
                Amount = 100,
                Spender = ForestContractAddress
            });

            await SellerForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = symbol,
                // TokenId = 233,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 100_00000000
                },
                Duration = new ListDuration
                {
                    DurationHours = 24
                },
                Quantity = 5,
                IsWhitelistAvailable = false,
                IsMergeToPreviousListedInfo = false
            });
            {
                var balance = await NFTContractStub.GetBalance.CallAsync(new AElf.Contracts.NFT.GetBalanceInput
                {
                    Symbol = symbol,
                    // TokenId = 233,
                    Owner = DefaultAddress
                });
                balance.Balance.ShouldBe(20);
            }

            await NFTBuyerTokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Symbol = "ELF",
                Amount = long.MaxValue,
                Spender = ForestContractAddress
            });
            var executionResult1 = await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput
            {
                Symbol = symbol,
                // TokenId = 233,
                OfferTo = DefaultAddress,
                Quantity = 2,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 100_00000000
                },
            });
            {
                var nftBalance = await NFTContractStub.GetBalance.CallAsync(new AElf.Contracts.NFT.GetBalanceInput
                {
                    Symbol = symbol,
                    // TokenId = 233,
                    Owner = User2Address
                });
                nftBalance.Balance.ShouldBe(2);

                await NFTContractStub.Approve.SendAsync(new AElf.Contracts.NFT.ApproveInput
                {
                    Symbol = symbol,
                    // TokenId = 233,
                    Amount = 2,
                    Spender = ForestContractAddress
                });
            }
            {
                var balance = await NFTContractStub.GetBalance.CallAsync(new AElf.Contracts.NFT.GetBalanceInput
                {
                    Symbol = symbol,
                    // TokenId = 233,
                    Owner = DefaultAddress
                });
                balance.Balance.ShouldBe(18);
            }
            {
                var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Symbol = "ELF",
                    Owner = User2Address
                });
                balance.Balance.ShouldBe(InitialELFAmount - 200_00000000);
            }
            {
                var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Symbol = "ELF",
                    Owner = DefaultAddress
                });
                balance.Balance.ShouldBe(DefaultSenderInitialELFAmount + 200_00000000);
            }
            await NFTBuyerTokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Symbol = "ELF",
                Amount = long.MaxValue,
                Spender = ForestContractAddress
            });
            {
                await Seller2ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
                {
                    Symbol = symbol,
                    // TokenId = 233,
                    Price = new Price
                    {
                        Symbol = "ELF",
                        Amount = 200_00000000
                    },
                    Duration = new ListDuration
                    {
                        DurationHours = 24
                    },
                    Quantity = 2,
                    IsWhitelistAvailable = false,
                    IsMergeToPreviousListedInfo = false
                });
            }
            await TokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Symbol = "ELF",
                Amount = long.MaxValue,
                Spender = ForestContractAddress
            });
            {
                await Buyer3ForestContractStub.MakeOffer.SendAsync(new MakeOfferInput
                {
                    Symbol = symbol,
                    // TokenId = 233,
                    OfferTo = User2Address,
                    Quantity = 1,
                    Price = new Price
                    {
                        Symbol = "ELF",
                        Amount = 200_00000000
                    },
                });
            }
            {
                var nftBalance = await NFTContractStub.GetBalance.CallAsync(new AElf.Contracts.NFT.GetBalanceInput
                {
                    Symbol = symbol,
                    // TokenId = 233,
                    Owner = DefaultAddress
                });
                nftBalance.Balance.ShouldBe(19);
            }
            {
                var balance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
                {
                    Symbol = "ELF",
                    Owner = DefaultAddress
                });
                balance.Balance.ShouldBe(DefaultSenderInitialELFAmount + 200_00000000 + 200_00000000 / 1000 - 200_00000000);
            }
        }
    }
}
using System;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Forest;

public partial class ForestContractMakeOfferTests : ForestContractTestBase
{
    private const string NftSymbol = "TESTNFT-1";
    private const string ElfSymbol = "ELF";
    private const int ServiceFeeRate = 1000; // 10%
    private const long InitializeElfAmount = 10000_0000_0000;

    private async Task InitializeForestContract()
    {
        await AdminForestContractStub.Initialize.SendAsync(new InitializeInput
        {
            NftContractAddress = NFTContractAddress,
            ServiceFeeReceiver = MarketServiceFeeReceiverAddress,
            ServiceFeeRate = ServiceFeeRate,
        });

        await AdminForestContractStub.SetWhitelistContract.SendAsync(WhitelistContractAddress);
    }

    private static Price Elf(long amunt)
    {
        return new Price()
        {
            Symbol = ElfSymbol,
            Amount = amunt
        };
    }

    private async Task PrepareNftData()
    {
        #region create NFTs

        {
            
            // create collections via MULTI-TOKEN-CONTRACT
            await UserTokenContractStub.Create.SendAsync(new CreateInput
            {
                Symbol = "TESTNFT-0",
                TokenName = "TESTNFT—collection",
                TotalSupply = 100,
                Decimals = 0,
                Issuer = User1Address,
                IsBurnable = false,
                IssueChainId = 0,
                ExternalInfo = new ExternalInfo()
            });

            // create NFT via MULTI-TOKEN-CONTRACT
            await UserTokenContractStub.Create.SendAsync(new CreateInput
            {
                Symbol = NftSymbol,
                TokenName = NftSymbol,
                TotalSupply = 100,
                Decimals = 0,
                Issuer = User1Address,
                IsBurnable = false,
                IssueChainId = 0,
                ExternalInfo = new ExternalInfo()
            });
        }

        #endregion

        #region issue NFTs and check

        {
            // issue 10 NFTs to self
            await UserTokenContractStub.Issue.SendAsync(new IssueInput()
            {
                Symbol = NftSymbol,
                Amount = 10,
                To = User1Address
            });

            // got 100-totalSupply and 10-supply
            var tokenInfo = await UserTokenContractStub.GetTokenInfo.SendAsync(new GetTokenInfoInput()
            {
                Symbol = NftSymbol,
            });

            tokenInfo.Output.TotalSupply.ShouldBe(100);
            tokenInfo.Output.Supply.ShouldBe(10);


            var nftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User1Address
            });
            nftBalance.Output.Balance.ShouldBe(10);
        }

        #endregion

        #region prepare ELF token

        {
            // transfer thousand ELF to seller
            await TokenContractStub.Transfer.SendAsync(new TransferInput()
            {
                To = User1Address,
                Symbol = ElfSymbol,
                Amount = InitializeElfAmount
            });

            // transfer thousand ELF to buyer
            await TokenContractStub.Transfer.SendAsync(new TransferInput()
            {
                To = User2Address,
                Symbol = ElfSymbol,
                Amount = InitializeElfAmount
            });
        }

        #endregion

        #region approve transfer

        {
            // approve contract handle NFT of seller   
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Symbol = NftSymbol,
                Amount = 5,
                Spender = ForestContractAddress
            });

            // approve contract handle ELF of buyer   
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Symbol = ElfSymbol,
                Amount = InitializeElfAmount,
                Spender = ForestContractAddress
            });
        }

        #endregion
    }


    [Fact]
    public async void MakeOffer_Case21_beforeOnShelf_deal()
    {
        await InitializeForestContract();
        await PrepareNftData();

        // user2 make offer to user1
        await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
        {
            Symbol = NftSymbol,
            OfferTo = User1Address,
            Quantity = 1,
            Price = Elf(5),
            ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(30))
        });

        // list offers just sent
        var offerList = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
        {
            Symbol = NftSymbol,
            Address = User2Address,
        }).Result.Output;
        offerList.Value.Count.ShouldBeGreaterThan(0);
        offerList.Value[0].To.ShouldBe(User1Address);
        offerList.Value[0].From.ShouldBe(User2Address);
    }

    [Fact]
    public async void MakeOffer_Case22_whiteListUser_beforeStartTime_notDeal()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var sellPrice = Elf(1000_0000_0000);
        var whitePrice = Elf(1_0000_0000);

        #region ListWithFixedPrice

        {
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
            {
                Symbol = NftSymbol,
                Quantity = 5,
                IsWhitelistAvailable = true,
                Price = sellPrice,
                Whitelists = new WhitelistInfoList()
                {
                    Whitelists =
                    {
                        new WhitelistInfo()
                        {
                            PriceTag = new PriceTagInfo()
                            {
                                TagName = "WHITELIST_TAG",
                                Price = whitePrice
                            },
                            AddressList = new AddressList()
                            {
                                Value = { User2Address, User3Address },
                            }
                        },
                        // other WhitelistInfo here
                        // new WhitelistInfo() {}
                    }
                },
                Duration = new ListDuration()
                {
                    // start 5min ago
                    StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
                    // public 10min after
                    PublicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(10)),
                    DurationHours = 1,
                },
            });
        }

        #endregion

        #region user2 make offer to user1

        {
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = 1,
                Price = whitePrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(30))
            });
        }

        #endregion

        #region check offer list

        {
            // list offers
            var offerList = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
            {
                Symbol = NftSymbol,
                Address = User2Address,
            }).Result.Output;
            offerList.Value.Count.ShouldBeGreaterThan(0);
            offerList.Value[0].To.ShouldBe(User1Address);
            offerList.Value[0].From.ShouldBe(User2Address);
        }

        #endregion
    }

    [Fact]
    public async void MakeOffer_Case23_whiteListUser_afterStartTime_deal()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var sellPrice = Elf(1000_0000_0000);
        var whitePrice = Elf(1_0000_0000);

        #region ListWithFixedPrice

        {
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
            {
                Symbol = NftSymbol,
                Quantity = 5,
                IsWhitelistAvailable = true,
                Price = sellPrice,
                Whitelists = new WhitelistInfoList()
                {
                    Whitelists =
                    {
                        new WhitelistInfo()
                        {
                            PriceTag = new PriceTagInfo()
                            {
                                TagName = "WHITELIST_TAG",
                                Price = whitePrice
                            },
                            AddressList = new AddressList()
                            {
                                Value = { User2Address, User3Address },
                            }
                        },
                        // other WhitelistInfo here
                        // new WhitelistInfo() {}
                    }
                },
                Duration = new ListDuration()
                {
                    // start 1sec ago
                    StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddSeconds(-1)),
                    // public 10min after
                    PublicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(10)),
                    DurationHours = 1,
                },
            });
        }

        #endregion

        #region whitelist user buy

        {
            // check buyer ELF balance
            var elfBalance = await User2TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = ElfSymbol,
                Owner = User2Address
            });
            elfBalance.Output.Balance.ShouldBe(InitializeElfAmount);

            // check seller ELF balance
            var nftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User1Address
            });
            nftBalance.Output.Balance.ShouldBe(10);

            // user2 make offer to user1
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = 1,
                Price = whitePrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });
        }

        #endregion

        #region check seller NFT

        {
            var nftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User1Address
            });
            nftBalance.Output.Balance.ShouldBe(9);
        }

        #endregion

        #region check buyer NFT

        {
            var nftBalance = await User2TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User2Address
            });
            nftBalance.Output.Balance.ShouldBe(1);
        }

        #endregion

        #region check service fee

        {
            // check buyer ELF balance
            var user1ElfBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = ElfSymbol,
                Owner = User1Address
            });
            var serviceFee = whitePrice.Amount * ServiceFeeRate / 10000;
            user1ElfBalance.Output.Balance.ShouldBe(InitializeElfAmount + whitePrice.Amount - serviceFee);
        }

        #endregion
    }

    [Fact]
    public async void MakeOffer_Case24_whiteListUser_afterStartTime_morePrice_deal()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var sellPrice = Elf(1000_0000_0000);
        var whitePrice = Elf(1_0000_0000);
        var offerPrice = Elf(2_0000_0000);

        #region ListWithFixedPrice

        {
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
            {
                Symbol = NftSymbol,
                Quantity = 5,
                IsWhitelistAvailable = true,
                Price = sellPrice,
                Whitelists = new WhitelistInfoList()
                {
                    Whitelists =
                    {
                        new WhitelistInfo()
                        {
                            PriceTag = new PriceTagInfo()
                            {
                                TagName = "WHITELIST_TAG",
                                Price = whitePrice
                            },
                            AddressList = new AddressList()
                            {
                                Value = { User2Address, User3Address },
                            }
                        },
                        // other WhitelistInfo here
                        // new WhitelistInfo() {}
                    }
                },
                Duration = new ListDuration()
                {
                    // start 1sec ago
                    StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddSeconds(-1)),
                    // public 10min after
                    PublicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(10)),
                    DurationHours = 1,
                },
            });
        }

        #endregion

        #region whitelist user buy

        {
            // user2 make offer to user1
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = 1,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });
        }

        #endregion

        #region check seller NFT

        {
            var nftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User1Address
            });
            nftBalance.Output.Balance.ShouldBe(9);
        }

        #endregion

        #region check buyer NFT

        {
            var nftBalance = await User2TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User2Address
            });
            nftBalance.Output.Balance.ShouldBe(1);
        }

        #endregion

        #region check service fee

        {
            // check buyer ELF balance
            var user1ElfBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = ElfSymbol,
                Owner = User1Address
            });
            var serviceFee = whitePrice.Amount * ServiceFeeRate / 10000;
            user1ElfBalance.Output.Balance.ShouldBe(InitializeElfAmount + whitePrice.Amount - serviceFee);
        }

        #endregion
    }
    
    [Fact]
    public async void MakeOffer_Case25_whiteListUser_afterStartTime_notDeal()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var sellPrice = Elf(1000_0000_0000);
        var whitePrice = Elf(1_0000_0000);
        var offerPrice = Elf(0_5000_0000);

        #region ListWithFixedPrice

        {
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
            {
                Symbol = NftSymbol,
                Quantity = 5,
                IsWhitelistAvailable = true,
                Price = sellPrice,
                Whitelists = new WhitelistInfoList()
                {
                    Whitelists =
                    {
                        new WhitelistInfo()
                        {
                            PriceTag = new PriceTagInfo()
                            {
                                TagName = "WHITELIST_TAG",
                                Price = whitePrice
                            },
                            AddressList = new AddressList()
                            {
                                Value = { User2Address, User3Address },
                            }
                        },
                        // other WhitelistInfo here
                        // new WhitelistInfo() {}
                    }
                },
                Duration = new ListDuration()
                {
                    // start 1sec ago
                    StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddSeconds(-1)),
                    // public 10min after
                    PublicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(10)),
                    DurationHours = 1,
                },
            });
        }

        #endregion

        #region whitelist user buy

        {
            // user2 make offer to user1
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = 1,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });
        }

        #endregion

        #region check offer list

        {
            // list offers just sent
            var offerList = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
            {
                Symbol = NftSymbol,
                Address = User2Address,
            }).Result.Output;
            offerList.Value.Count.ShouldBeGreaterThan(0);
            offerList.Value[0].To.ShouldBe(User1Address);
            offerList.Value[0].From.ShouldBe(User2Address);
        }

        #endregion
    }
    
    [Fact]
    public async void MakeOffer_Case26_whiteListUser_afterStartTime_deal()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var sellPrice = Elf(1_0000_0000);
        var whitePrice = Elf(2_0000_0000);
        var offerPrice = Elf(3_0000_0000);

        #region ListWithFixedPrice

        {
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
            {
                Symbol = NftSymbol,
                Quantity = 5,
                IsWhitelistAvailable = true,
                Price = sellPrice,
                Whitelists = new WhitelistInfoList()
                {
                    Whitelists =
                    {
                        new WhitelistInfo()
                        {
                            PriceTag = new PriceTagInfo()
                            {
                                TagName = "WHITELIST_TAG",
                                Price = whitePrice
                            },
                            AddressList = new AddressList()
                            {
                                Value = { User2Address, User3Address },
                            }
                        },
                        // other WhitelistInfo here
                        // new WhitelistInfo() {}
                    }
                },
                Duration = new ListDuration()
                {
                    // start 1sec ago
                    StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddSeconds(-1)),
                    // public 10min after
                    PublicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(10)),
                    DurationHours = 1,
                },
            });
        }

        #endregion
        
        #region whitelist user buy

        {
            // user2 make offer to user1
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = 1,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });
        }

        #endregion
        
        #region check seller NFT

        {
            var nftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User1Address
            });
            nftBalance.Output.Balance.ShouldBe(9);
        }

        #endregion

        #region check buyer NFT

        {
            var nftBalance = await User2TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User2Address
            });
            nftBalance.Output.Balance.ShouldBe(1);
        }

        #endregion

        #region check service fee

        {
            // check buyer ELF balance
            var user1ElfBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = ElfSymbol,
                Owner = User1Address
            });
            var serviceFee = whitePrice.Amount * ServiceFeeRate / 10000;
            user1ElfBalance.Output.Balance.ShouldBe(InitializeElfAmount + whitePrice.Amount - serviceFee);
        }

        #endregion
    }
    
    [Fact]
    public async void MakeOffer_Case27_whiteListUser_afterStartTime_notDeal()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var sellPrice = Elf(1000_0000_0000);
        var whitePrice = Elf(0);
        var offerPrice = Elf(1_0000_0000);

        #region ListWithFixedPrice with zero whitePrice

        {
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
            {
                Symbol = NftSymbol,
                Quantity = 5,
                IsWhitelistAvailable = true,
                Price = sellPrice,
                Whitelists = new WhitelistInfoList()
                {
                    Whitelists =
                    {
                        new WhitelistInfo()
                        {
                            PriceTag = new PriceTagInfo()
                            {
                                TagName = "WHITELIST_TAG",
                                Price = whitePrice
                            },
                            AddressList = new AddressList()
                            {
                                Value = { User2Address, User3Address },
                            }
                        },
                        // other WhitelistInfo here
                        // new WhitelistInfo() {}
                    }
                },
                Duration = new ListDuration()
                {
                    // start 1sec ago
                    StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddSeconds(-1)),
                    // public 10min after
                    PublicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(10)),
                    DurationHours = 1,
                },
            });
        }
        
        #endregion

        #region whitelist user buy

        {
            // user2 make offer to user1
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = 1,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });
        }

        #endregion
        
        #region check seller NFT

        {
            var nftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User1Address
            });
            nftBalance.Output.Balance.ShouldBe(9);
        }

        #endregion

        #region check buyer NFT

        {
            var nftBalance = await User2TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User2Address
            });
            nftBalance.Output.Balance.ShouldBe(1);
        }

        #endregion

    }
    
    [Fact]
    public async void MakeOffer_Case28_whiteListUser_afterStartTime_notDeal()
    {
        await InitializeForestContract();
        await PrepareNftData();
        
        // sellPrice <= offerPrice < whitePrice ==> to offerList
        var sellPrice = Elf(1_0000_0000);
        var whitePrice = Elf(5_0000_0000);
        var offerPrice = Elf(2_0000_0000);

        #region ListWithFixedPrice

        {
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
            {
                Symbol = NftSymbol,
                Quantity = 5,
                IsWhitelistAvailable = true,
                Price = sellPrice,
                Whitelists = new WhitelistInfoList()
                {
                    Whitelists =
                    {
                        new WhitelistInfo()
                        {
                            PriceTag = new PriceTagInfo()
                            {
                                TagName = "WHITELIST_TAG",
                                Price = whitePrice
                            },
                            AddressList = new AddressList()
                            {
                                Value = { User2Address, User3Address },
                            }
                        },
                        // other WhitelistInfo here
                        // new WhitelistInfo() {}
                    }
                },
                Duration = new ListDuration()
                {
                    // start 1sec ago
                    StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddSeconds(-1)),
                    // public 10min after
                    PublicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(10)),
                    DurationHours = 1,
                },
            });
        }
        
        #endregion

        #region whitelist user buy, FAIL

        {
            // user2 make offer to user1
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = 1,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });
        }

        #endregion
        
        #region check seller NFT, NOT buy

        {
            var nftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User1Address
            });
            nftBalance.Output.Balance.ShouldBe(10);
        }

        #endregion

        #region check offer list

        {
            // list offers just sent
            var offerList = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
            {
                Symbol = NftSymbol,
                Address = User2Address,
            }).Result.Output;
            offerList.Value.Count.ShouldBeGreaterThan(0);
            offerList.Value[0].To.ShouldBe(User1Address);
            offerList.Value[0].From.ShouldBe(User2Address);
        }

        #endregion

    }
    
    [Fact]
    public async void MakeOffer_Case29_whiteListUser_afterPublicTime_notDeal()
    {
        await InitializeForestContract();
        await PrepareNftData();
        
        // offerPrice < whitePrice < sellPrice
        var sellPrice = Elf(5_0000_0000);
        var whitePrice = Elf(2_0000_0000);
        var offerPrice = Elf(1_0000_0000);

        // after publicTime
        var startTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-10));
        var publicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-5));

        #region ListWithFixedPrice

        {
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
            {
                Symbol = NftSymbol,
                Quantity = 5,
                IsWhitelistAvailable = true,
                Price = sellPrice,
                Whitelists = new WhitelistInfoList()
                {
                    Whitelists =
                    {
                        new WhitelistInfo()
                        {
                            PriceTag = new PriceTagInfo()
                            {
                                TagName = "WHITELIST_TAG",
                                Price = whitePrice
                            },
                            AddressList = new AddressList()
                            {
                                Value = { User2Address, User3Address },
                            }
                        },
                        // other WhitelistInfo here
                        // new WhitelistInfo() {}
                    }
                },
                Duration = new ListDuration()
                {
                    // start 1sec ago
                    StartTime = startTime,
                    // public 10min after
                    PublicTime = publicTime,
                    DurationHours = 1,
                },
            });
        }
        
        #endregion

        #region whitelist user buy, FAIL

        {
            // user2 make offer to user1
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = 1,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });
        }

        #endregion
        
        #region check seller NFT, NOT buy

        {
            var nftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User1Address
            });
            nftBalance.Output.Balance.ShouldBe(10);
        }

        #endregion

        #region check offer list

        {
            // list offers just sent
            var offerList = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
            {
                Symbol = NftSymbol,
                Address = User2Address,
            }).Result.Output;
            offerList.Value.Count.ShouldBeGreaterThan(0);
            offerList.Value[0].To.ShouldBe(User1Address);
            offerList.Value[0].From.ShouldBe(User2Address);
        }

        #endregion

    }
    
    [Fact]
    public async void MakeOffer_Case30_whiteListUser_afterPublicTime_deal()
    {
        await InitializeForestContract();
        await PrepareNftData();
        
        // offerPrice = whitePrice < sellPrice
        var sellPrice = Elf(5_0000_0000);
        var whitePrice = Elf(1_0000_0000);
        var offerPrice = Elf(1_0000_0000);
        
        // after publicTime
        var startTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-10));
        var publicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-5));

        #region ListWithFixedPrice

        {
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
            {
                Symbol = NftSymbol,
                Quantity = 5,
                IsWhitelistAvailable = true,
                Price = sellPrice,
                Whitelists = new WhitelistInfoList()
                {
                    Whitelists =
                    {
                        new WhitelistInfo()
                        {
                            PriceTag = new PriceTagInfo()
                            {
                                TagName = "WHITELIST_TAG",
                                Price = whitePrice
                            },
                            AddressList = new AddressList()
                            {
                                Value = { User2Address, User3Address },
                            }
                        },
                        // other WhitelistInfo here
                        // new WhitelistInfo() {}
                    }
                },
                Duration = new ListDuration()
                {
                    // start 1sec ago
                    StartTime = startTime,
                    // public 10min after
                    PublicTime = publicTime,
                    DurationHours = 1,
                },
            });
        }

        #endregion
        
        #region whitelist user buy

        {
            // check buyer ELF balance
            var elfBalance = await User2TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = ElfSymbol,
                Owner = User2Address
            });
            elfBalance.Output.Balance.ShouldBe(InitializeElfAmount);

            // check seller ELF balance
            var nftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User1Address
            });
            nftBalance.Output.Balance.ShouldBe(10);

            // user2 make offer to user1
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = 1,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });
        }

        #endregion

        #region check seller NFT

        {
            var nftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User1Address
            });
            nftBalance.Output.Balance.ShouldBe(9);
        }

        #endregion

        #region check buyer NFT

        {
            var nftBalance = await User2TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User2Address
            });
            nftBalance.Output.Balance.ShouldBe(1);
        }

        #endregion

        #region check service fee

        {
            // check buyer ELF balance
            var user1ElfBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = ElfSymbol,
                Owner = User1Address
            });
            var serviceFee = whitePrice.Amount * ServiceFeeRate / 10000;
            user1ElfBalance.Output.Balance.ShouldBe(InitializeElfAmount + whitePrice.Amount - serviceFee);
        }

        #endregion
    }
    
    [Fact]
    public async void MakeOffer_Case31_whiteListUser_afterPublicTime_deal()
    {
        await InitializeForestContract();
        await PrepareNftData();
        
        // whitePrice < offerPrice < sellPrice
        var sellPrice = Elf(5_0000_0000);
        var whitePrice = Elf(1_0000_0000);
        var offerPrice = Elf(2_0000_0000);
        
        // after publicTime
        var startTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-10));
        var publicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-5));

        #region ListWithFixedPrice

        {
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
            {
                Symbol = NftSymbol,
                Quantity = 5,
                IsWhitelistAvailable = true,
                Price = sellPrice,
                Whitelists = new WhitelistInfoList()
                {
                    Whitelists =
                    {
                        new WhitelistInfo()
                        {
                            PriceTag = new PriceTagInfo()
                            {
                                TagName = "WHITELIST_TAG",
                                Price = whitePrice
                            },
                            AddressList = new AddressList()
                            {
                                Value = { User2Address, User3Address },
                            }
                        },
                        // other WhitelistInfo here
                        // new WhitelistInfo() {}
                    }
                },
                Duration = new ListDuration()
                {
                    // start 1sec ago
                    StartTime = startTime,
                    // public 10min after
                    PublicTime = publicTime,
                    DurationHours = 1,
                },
            });
        }

        #endregion

        #region whitelist user buy

        {
            // check buyer ELF balance
            var elfBalance = await User2TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = ElfSymbol,
                Owner = User2Address
            });
            elfBalance.Output.Balance.ShouldBe(InitializeElfAmount);

            // check seller ELF balance
            var nftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User1Address
            });
            nftBalance.Output.Balance.ShouldBe(10);

            // user2 make offer to user1
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = 1,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });
        }

        #endregion

        #region check seller NFT

        {
            var nftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User1Address
            });
            nftBalance.Output.Balance.ShouldBe(9);
        }

        #endregion

        #region check buyer NFT

        {
            var nftBalance = await User2TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User2Address
            });
            nftBalance.Output.Balance.ShouldBe(1);
        }

        #endregion

        #region check service fee

        {
            // check buyer ELF balance
            var user1ElfBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = ElfSymbol,
                Owner = User1Address
            });
            var serviceFee = whitePrice.Amount * ServiceFeeRate / 10000;
            user1ElfBalance.Output.Balance.ShouldBe(InitializeElfAmount + whitePrice.Amount - serviceFee);
        }

        #endregion
    }
    
    [Fact]
    public async void MakeOffer_Case32_whiteListUser_afterPublicTime_notDeal()
    {
        await InitializeForestContract();
        await PrepareNftData();
        
        // offerPrice < whitePrice < sellPrice
        var sellPrice = Elf(5_0000_0000);
        var whitePrice = Elf(2_0000_0000);
        var offerPrice = Elf(1_0000_0000);
        
        // after publicTime
        var startTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-10));
        var publicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-5));

        #region ListWithFixedPrice

        {
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
            {
                Symbol = NftSymbol,
                Quantity = 5,
                IsWhitelistAvailable = true,
                Price = sellPrice,
                Whitelists = new WhitelistInfoList()
                {
                    Whitelists =
                    {
                        new WhitelistInfo()
                        {
                            PriceTag = new PriceTagInfo()
                            {
                                TagName = "WHITELIST_TAG",
                                Price = whitePrice
                            },
                            AddressList = new AddressList()
                            {
                                Value = { User2Address, User3Address },
                            }
                        },
                        // other WhitelistInfo here
                        // new WhitelistInfo() {}
                    }
                },
                Duration = new ListDuration()
                {
                    // start 1sec ago
                    StartTime = startTime,
                    // public 10min after
                    PublicTime = publicTime,
                    DurationHours = 1,
                },
            });
        }

        #endregion
        
        #region whitelist user buy

        {
            // check buyer ELF balance
            var elfBalance = await User2TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = ElfSymbol,
                Owner = User2Address
            });
            elfBalance.Output.Balance.ShouldBe(InitializeElfAmount);

            // check seller ELF balance
            var nftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User1Address
            });
            nftBalance.Output.Balance.ShouldBe(10);

            // user2 make offer to user1
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = 1,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });
        }

        #endregion

        #region check seller NFT, not deal

        {
            var nftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User1Address
            });
            nftBalance.Output.Balance.ShouldBe(10);
        }

        #endregion

    }
    
    [Fact]
    public async void MakeOffer_Case33_whiteListUser_afterPublicTime_notDeal()
    {
        await InitializeForestContract();
        await PrepareNftData();
        
        // offerPrice < whitePrice < sellPrice
        var sellPrice = Elf(5_0000_0000);
        var whitePrice = Elf(2_0000_0000);
        var offerPrice = Elf(3_0000_0000);
        
        // after publicTime
        var startTime = Timestamp.FromDateTime(DateTime.UtcNow.AddHours(-10));
        var publicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-5));

        #region ListWithFixedPrice

        {
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
            {
                Symbol = NftSymbol,
                Quantity = 5,
                IsWhitelistAvailable = true,
                Price = sellPrice,
                Whitelists = new WhitelistInfoList()
                {
                    Whitelists =
                    {
                        new WhitelistInfo()
                        {
                            PriceTag = new PriceTagInfo()
                            {
                                TagName = "WHITELIST_TAG",
                                Price = whitePrice
                            },
                            AddressList = new AddressList()
                            {
                                Value = { User2Address, User3Address },
                            }
                        },
                        // other WhitelistInfo here
                        // new WhitelistInfo() {}
                    }
                },
                Duration = new ListDuration()
                {
                    // start 1sec ago
                    StartTime = startTime,
                    // public 10min after
                    PublicTime = publicTime,
                    DurationHours = 1,
                },
            });
        }

        #endregion

        #region whitelist user buy

        {
            // check buyer ELF balance
            var elfBalance = await User2TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = ElfSymbol,
                Owner = User2Address
            });
            elfBalance.Output.Balance.ShouldBe(InitializeElfAmount);

            // check seller ELF balance
            var nftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User1Address
            });
            nftBalance.Output.Balance.ShouldBe(10);
            
            // user2 make offer to user1
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = 1,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });
        }

        #endregion
        
        
        #region check seller NFT
        
        {
            var nftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User1Address
            });
            nftBalance.Output.Balance.ShouldBe(9);
        }
        
        #endregion
        
        #region check buyer NFT
        
        {
            var nftBalance = await User2TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User2Address
            });
            nftBalance.Output.Balance.ShouldBe(1);
        }
        
        #endregion

        // contract NOT support timeout of fix-price-list NFT
        //
        // #region check offer list
        //
        // {
        //     // list offers just sent
        //     var offerList = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
        //     {
        //         Symbol = NftSymbol,
        //         Address = User2Address,
        //     }).Result.Output;
        //     offerList.Value.Count.ShouldBeGreaterThan(0);
        //     offerList.Value[0].To.ShouldBe(User1Address);
        //     offerList.Value[0].From.ShouldBe(User2Address);
        // }
        //
        // #endregion
    }
        
    [Fact]
    public async void MakeOffer_Case34_buySellerSelf_fail()
    {
        await InitializeForestContract();
        await PrepareNftData();
        
        // offerPrice < whitePrice < sellPrice
        var sellPrice = Elf(5_0000_0000);
        var whitePrice = Elf(2_0000_0000);
        var offerPrice = Elf(3_0000_0000);
        
        // after publicTime
        var startTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-10));
        var publicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(10));

        #region ListWithFixedPrice

        {
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
            {
                Symbol = NftSymbol,
                Quantity = 5,
                IsWhitelistAvailable = true,
                Price = sellPrice,
                Whitelists = new WhitelistInfoList()
                {
                    Whitelists =
                    {
                        new WhitelistInfo()
                        {
                            PriceTag = new PriceTagInfo()
                            {
                                TagName = "WHITELIST_TAG",
                                Price = whitePrice
                            },
                            AddressList = new AddressList()
                            {
                                Value = { User2Address, User3Address },
                            }
                        },
                        // other WhitelistInfo here
                        // new WhitelistInfo() {}
                    }
                },
                Duration = new ListDuration()
                {
                    // start 1sec ago
                    StartTime = startTime,
                    // public 10min after
                    PublicTime = publicTime,
                    DurationHours = 1,
                },
            });
            await Task.Delay(1000);
        }

        #endregion

        #region whitelist user buy

        {
            // check seller ELF balance
            var elfBalance = await User2TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = ElfSymbol,
                Owner = User2Address
            });
            elfBalance.Output.Balance.ShouldBe(InitializeElfAmount);

            // check seller NFT balance
            var nftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User1Address
            });
            nftBalance.Output.Balance.ShouldBe(10);

            try
            {
                // user1 make offer to user1 self
                await Buyer1ForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
                {
                    Symbol = NftSymbol,
                    OfferTo = User1Address,
                    Quantity = 1,
                    Price = offerPrice,
                    ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
                });
                
                // never run this line
                true.ShouldBe(false);
            }
            catch (ShouldAssertException e)
            {
                throw;
            }
            catch (Exception e)
            {
                // should throw "Origin owner cannot be sender himself." error
                e.ShouldNotBeNull();
            }
        }

        #endregion
    }
    
    
}
using System;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Forest;

public class ForestContractTests_Security : ForestContractTestBase
{
    private const string NftSymbol = "TESTNFT-1";
    private const string NftSymbol2 = "TESTNFT-2";
    private const string ElfSymbol = "ELF";
    private const int ServiceFeeRate = 1000; // 10%
    private const long InitializeElfAmount = 10000_0000_0000;

    private async Task InitializeForestContract()
    {
        await AdminForestContractStub.Initialize.SendAsync(new InitializeInput
        {
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
                IsBurnable = true,
                IssueChainId = 0,
                ExternalInfo = new ExternalInfo()
            });

            // create NFT via MULTI-TOKEN-CONTRACT
            await UserTokenContractStub.Create.SendAsync(new CreateInput
            {
                Symbol = NftSymbol2,
                TokenName = NftSymbol2,
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

            // issue 10 NFTs to self
            await UserTokenContractStub.Issue.SendAsync(new IssueInput()
            {
                Symbol = NftSymbol2,
                Amount = 5,
                To = User1Address
            });

            // issue 10 NFTs to user3
            await UserTokenContractStub.Issue.SendAsync(new IssueInput()
            {
                Symbol = NftSymbol,
                Amount = 10,
                To = User3Address
            });


            // got 100-totalSupply and 10-supply
            var tokenInfo = await UserTokenContractStub.GetTokenInfo.SendAsync(new GetTokenInfoInput()
            {
                Symbol = NftSymbol,
            });

            tokenInfo.Output.TotalSupply.ShouldBe(100);
            tokenInfo.Output.Supply.ShouldBe(20);


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

            // approve contract handle NFT2 of seller   
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Symbol = NftSymbol2,
                Amount = 5,
                Spender = ForestContractAddress
            });

            // approve contract handle NFT of user3   
            await User3TokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Symbol = NftSymbol,
                Amount = 10,
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

    private PriceTagInfo GenPriceTag(string name, Price price)
    {
        return new PriceTagInfo()
        {
            TagName = name,
            Price = price
        };
    }

    private WhitelistInfo GenWhiteList(PriceTagInfo priceTagInfo, params Address[] add)
    {
        var address = new AddressList();
        address.Value.AddRange(add);
        return new WhitelistInfo()
        {
            PriceTag = priceTagInfo,
            AddressList = address
        };
    }

    private WhitelistInfoList GenWhiteInfoList(params WhitelistInfo[] whitelists)
    {
        WhitelistInfoList res = new WhitelistInfoList();
        res.Whitelists.AddRange(whitelists);
        return res;
    }


    [Fact]
    public async void Security_Case03_Deal_noPerm()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 1;
        var dealQuantity = 1;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;

        #region user2 make offer to user3

        {
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User3Address,
                Quantity = offerQuantity,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });
        }

        #endregion

        #region check offer list

        {
            var offerList = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
            {
                Symbol = NftSymbol,
                Address = User2Address,
            }).Result.Output;
            offerList.Value.Count.ShouldBeGreaterThan(0);
            offerList.Value[0].To.ShouldBe(User3Address);
            offerList.Value[0].From.ShouldBe(User2Address);
            offerList.Value[0].Quantity.ShouldBe(offerQuantity);
        }

        #endregion

        #region user1 deal, NO PERM

        {
            var dealFunc = () => Seller1ForestContractStub.Deal.SendAsync(new DealInput()
            {
                Symbol = NftSymbol,
                Price = offerPrice,
                OfferFrom = User2Address,
                Quantity = dealQuantity
            });
            var exception = await Assert.ThrowsAsync<Exception>(dealFunc);
            exception.Message.ShouldContain("offer is empty");
        }

        #endregion

        #region user2 deal, SUCCESS

        {
            await Seller3ForestContractStub.Deal.SendAsync(new DealInput()
            {
                Symbol = NftSymbol,
                Price = offerPrice,
                OfferFrom = User2Address,
                Quantity = dealQuantity
            });
        }

        #endregion
    }

    [Fact]
    public async void Security_Case04_Delist_noPerm()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var whitePrice = Elf(1_0000_0000);
        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 2;
        var dealQuantity = 2;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;

        // before startTime
        var startTime = Timestamp.FromDateTime(DateTime.UtcNow);
        var publicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(10));


        #region user1 ListWithFixedPrice

        {
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
            {
                Symbol = NftSymbol,
                Quantity = 5,
                IsWhitelistAvailable = true,
                Price = sellPrice,
                Whitelists = GenWhiteInfoList(GenWhiteList(GenPriceTag("WHITELIST_TAG", whitePrice), User3Address)),
                Duration = new ListDuration()
                {
                    StartTime = startTime,
                    PublicTime = publicTime,
                    DurationHours = 1,
                },
            });

            var res = await Seller1ForestContractStub.GetListedNFTInfoList.SendAsync(new GetListedNFTInfoListInput()
            {
                Symbol = NftSymbol,
                Owner = User1Address
            });

            res.Output.Value.Count.ShouldBe(1);
        }

        #endregion

        #region user3 Delist, noPerm

        {
            try
            {
                await Buyer3ForestContractStub.Delist.SendAsync(new DelistInput()
                {
                    Symbol = NftSymbol,
                    Quantity = 2,
                    Price = sellPrice
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
                e.Message.ShouldContain("not exists");
            }
        }

        #endregion
    }

    [Fact]
    public async void Security_Case05_MakeOffer_success()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 1;
        var dealQuantity = 1;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;

        #region user2 make offer to user1

        {
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = offerQuantity,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });
        }

        #endregion

        #region check offer list

        {
            var offerList = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
            {
                Symbol = NftSymbol,
                Address = User2Address,
            }).Result.Output;
            offerList.Value.Count.ShouldBe(1);
            offerList.Value[0].To.ShouldBe(User1Address);
            offerList.Value[0].From.ShouldBe(User2Address);
            offerList.Value[0].Quantity.ShouldBe(offerQuantity);
        }

        #endregion

        #region user3 make offer to user1

        {
            await Buyer2ForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = offerQuantity,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });
        }

        #endregion

        #region check offer list

        {
            var offerList = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
            {
                Symbol = NftSymbol,
                Address = User3Address,
            }).Result.Output;
            offerList.Value.Count.ShouldBe(1);
            offerList.Value[0].To.ShouldBe(User1Address);
            offerList.Value[0].From.ShouldBe(User3Address);
            offerList.Value[0].Quantity.ShouldBe(offerQuantity);
        }

        #endregion
    }

    [Fact]
    public async void Security_Case06_CancelOffer_noPerm()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 1;
        var dealQuantity = 1;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;

        #region user2 make EXPIRED offer to user1

        {
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = offerQuantity,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-5)),
            });
        }

        #endregion

        #region user3 cancelOffer

        {
            try
            {
                await Buyer2ForestContractStub.CancelOffer.SendAsync(new CancelOfferInput()
                {
                    Symbol = NftSymbol,
                    IndexList = new Int32List()
                    {
                        Value = { 0 }
                    },
                    OfferFrom = User2Address,
                    IsCancelBid = false
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
                e.Message.ShouldContain("No permission");
            }
        }

        #endregion
    }

    [Fact]
    public async void Security_Case07_SetTokenWhiteList_noPerm()
    {
        await InitializeForestContract();
        await PrepareNftData();

        await Seller1ForestContractStub.SetTokenWhiteList.SendAsync(new SetTokenWhiteListInput()
        {
            Symbol = NftSymbol,
            TokenWhiteList = new StringList()
            {
                Value = { "ELF", "USDT" }
            }
        });

        try
        {
            await Seller2ForestContractStub.SetTokenWhiteList.SendAsync(new SetTokenWhiteListInput()
            {
                Symbol = NftSymbol,
                TokenWhiteList = new StringList()
                {
                    Value = { "ELF", "USDT" }
                }
            });
        }
        catch (ShouldAssertException e)
        {
            throw;
        }
        catch (Exception e)
        {
            e.Message.ShouldContain("Only NFT Collection Creator");
        }
    }

    [Fact]
    public async void Security_Case08_SetServiceFee_noPerm()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 1;
        var dealQuantity = 1;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;

        #region admin set success

        {
            await AdminForestContractStub.SetServiceFee.SendAsync(new SetServiceFeeInput()
            {
                ServiceFeeRate = 1000,
                ServiceFeeReceiver = User1Address
            });
        }

        #endregion

        #region user3 set success

        {
            try
            {
                await Seller2ForestContractStub.SetServiceFee.SendAsync(new SetServiceFeeInput()
                {
                    ServiceFeeRate = 1000,
                    ServiceFeeReceiver = User1Address
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
                e.Message.ShouldContain("No permission");
            }
        }

        #endregion
    }

    [Fact]
    public async void Security_Case09_SetGlobalTokenWhiteList_noPerm()
    {
        await InitializeForestContract();
        await PrepareNftData();

        await AdminForestContractStub.SetGlobalTokenWhiteList.SendAsync(new StringList()
        {
            Value = { "ELF", "USDT" }
        });

        try
        {
            await Seller1ForestContractStub.SetGlobalTokenWhiteList.SendAsync(new StringList()
            {
                Value = { "ELF", "USDT" }
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
            e.Message.ShouldContain("No permission");
        }
    }

    [Fact]
    public async void Security_Case10_SetWhitelistContract_noPerm()
    {
        await InitializeForestContract();
        await PrepareNftData();

        await AdminForestContractStub.SetWhitelistContract.SendAsync(WhitelistContractAddress);

        try
        {
            await Seller1ForestContractStub.SetWhitelistContract.SendAsync(WhitelistContractAddress);

            // never run this line
            true.ShouldBe(false);
        }
        catch (ShouldAssertException e)
        {
            throw;
        }
        catch (Exception e)
        {
            e.Message.ShouldContain("No permission");
        }
    }

    [Fact]
    public async void Security_Case12_ListFixedPrice_InvalidQty_fail()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var whitePrice = Elf(1_0000_0000);
        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 2;
        var dealQuantity = 2;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;

        // before startTime
        var startTime = Timestamp.FromDateTime(DateTime.UtcNow);
        var publicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(10));

        try
        {
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
            {
                Symbol = NftSymbol,
                Quantity = -5,
                IsWhitelistAvailable = true,
                Price = sellPrice,
                Whitelists = GenWhiteInfoList(GenWhiteList(GenPriceTag("WHITELIST_TAG", whitePrice), User3Address)),
                Duration = new ListDuration()
                {
                    StartTime = startTime,
                    PublicTime = publicTime,
                    DurationHours = 1,
                },
            });

            true.ShouldBe(false);
        }
        catch (ShouldAssertException e)
        {
            throw;
        }
        catch (Exception e)
        {
            e.Message.ShouldContain("Incorrect quantity");
        }
    }

    [Fact]
    public async void Security_Case13_ListFixedPrice_nftNotEnough_fail()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var whitePrice = Elf(1_0000_0000);
        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 2;
        var dealQuantity = 2;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;

        // before startTime
        var startTime = Timestamp.FromDateTime(DateTime.UtcNow);
        var publicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(10));

        #region listWithFixedPrice many-many-qty

        try
        {
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
            {
                Symbol = NftSymbol,
                Quantity = 500,
                IsWhitelistAvailable = true,
                Price = sellPrice,
                Whitelists = GenWhiteInfoList(GenWhiteList(GenPriceTag("WHITELIST_TAG", whitePrice), User3Address)),
                Duration = new ListDuration()
                {
                    StartTime = startTime,
                    PublicTime = publicTime,
                    DurationHours = 1,
                },
            });

            true.ShouldBe(false);
        }
        catch (ShouldAssertException e)
        {
            throw;
        }
        catch (Exception e)
        {
            e.Message.ShouldContain("Check sender NFT balance failed");
        }

        #endregion
    }

    [Fact]
    public async void Security_Case14_ListFixedPrice_InvalidPrice_fail()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var whitePrice = Elf(1_0000_0000);
        var sellPrice = Elf(-5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 2;
        var dealQuantity = 2;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;

        // before startTime
        var startTime = Timestamp.FromDateTime(DateTime.UtcNow);
        var publicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(10));

        #region ListWithFixedPrice invalid-sell-price

        try
        {
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
            {
                Symbol = NftSymbol,
                Quantity = 500,
                IsWhitelistAvailable = true,
                Price = sellPrice,
                Whitelists = GenWhiteInfoList(GenWhiteList(GenPriceTag("WHITELIST_TAG", whitePrice), User3Address)),
                Duration = new ListDuration()
                {
                    StartTime = startTime,
                    PublicTime = publicTime,
                    DurationHours = 1,
                },
            });

            true.ShouldBe(false);
        }
        catch (ShouldAssertException e)
        {
            throw;
        }
        catch (Exception e)
        {
            e.Message.ShouldContain("Incorrect listing price");
        }

        #endregion
    }

    [Fact]
    public async void Security_Case15_Deal_nftNotEnough_fail()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var whitePrice = Elf(1_0000_0000);
        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 200;
        var dealQuantity = 200;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;

        // before startTime
        var startTime = Timestamp.FromDateTime(DateTime.UtcNow);
        var publicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(10));

        #region user2 makeOfer to user1

        await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
        {
            Symbol = NftSymbol,
            OfferTo = User1Address,
            Quantity = offerQuantity,
            Price = offerPrice
        });

        #endregion

        #region check offerList

        var offerList = await BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
        {
            Symbol = NftSymbol,
            Address = User2Address
        });
        offerList.Output.Value.Count.ShouldBe(1);

        #endregion

        #region User1 deal offer, FAILED

        try
        {
            await SellerForestContractStub.Deal.SendAsync(new DealInput()
            {
                Symbol = NftSymbol,
                OfferFrom = User2Address,
                Price = offerPrice,
                Quantity = dealQuantity
            });
            true.ShouldBe(false);
        }
        catch (ShouldAssertException e)
        {
            throw;
        }
        catch (Exception e)
        {
            e.Message.ShouldContain("Insufficient NFT balance");
        }

        #endregion
    }

    [Fact]
    public async void Security_Case16_Deal_invalidDealQty_fail()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var whitePrice = Elf(1_0000_0000);
        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 2;
        var dealQuantity = 3;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;

        // before startTime
        var startTime = Timestamp.FromDateTime(DateTime.UtcNow);
        var publicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(10));

        #region user2 makeOfer to user1

        await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
        {
            Symbol = NftSymbol,
            OfferTo = User1Address,
            Quantity = offerQuantity,
            Price = offerPrice
        });

        #endregion

        #region check offerList

        var offerList = await BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
        {
            Symbol = NftSymbol,
            Address = User2Address
        });
        offerList.Output.Value.Count.ShouldBe(1);

        #endregion

        #region User1 deal offer, FAILED

        try
        {
            await Seller1ForestContractStub.Deal.SendAsync(new DealInput()
            {
                Symbol = NftSymbol,
                OfferFrom = User2Address,
                Price = offerPrice,
                Quantity = dealQuantity
            });
            true.ShouldBe(false);
        }
        catch (ShouldAssertException e)
        {
            throw;
        }
        catch (Exception e)
        {
            e.Message.ShouldContain("Deal quantity exceeded");
        }

        #endregion
    }

    [Fact]
    public async void Security_Case17_Deal_needToDelist_fail()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var whitePrice = Elf(1_0000_0000);
        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var listQuantity = 8;
        var offerQuantity = 5;
        var dealQuantity = 5;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;

        // before startTime
        var startTime = Timestamp.FromDateTime(DateTime.UtcNow);
        var publicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(10));

        #region user1 ListWithFixedPrice qty=8

        await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
        {
            Symbol = NftSymbol,
            Quantity = listQuantity,
            IsWhitelistAvailable = true,
            Price = sellPrice,
            Whitelists = GenWhiteInfoList(GenWhiteList(GenPriceTag("WHITELIST_TAG", whitePrice), User3Address)),
            Duration = new ListDuration()
            {
                StartTime = startTime,
                PublicTime = publicTime,
                DurationHours = 1,
            },
        });

        #endregion

        #region user1 nft balance qty = 10

        var nft = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
        {
            Symbol = NftSymbol,
            Owner = User1Address
        });
        nft.Output.Balance.ShouldBe(10);

        #endregion

        #region user2 MakeOffer to user1 qty = 5

        await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
        {
            Symbol = NftSymbol,
            OfferTo = User1Address,
            Quantity = offerQuantity,
            Price = offerPrice
        });

        #endregion

        #region User1 deal offer, FAILED

        try
        {
            await Seller1ForestContractStub.Deal.SendAsync(new DealInput()
            {
                Symbol = NftSymbol,
                OfferFrom = User2Address,
                Price = offerPrice,
                Quantity = dealQuantity
            });
            true.ShouldBe(false);
        }
        catch (ShouldAssertException e)
        {
            throw;
        }
        catch (Exception e)
        {
            e.Message.ShouldContain("Need to delist");
        }

        #endregion
    }

    [Fact]
    public async void Security_Case18_Delist_invalidQty_fail()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var whitePrice = Elf(1_0000_0000);
        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var listQuantity = 8;
        var offerQuantity = 5;
        var dealQuantity = 5;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;

        // before startTime
        var startTime = Timestamp.FromDateTime(DateTime.UtcNow);
        var publicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(10));

        #region user1 ListWithFixedPrice qty=8

        await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
        {
            Symbol = NftSymbol,
            Quantity = listQuantity,
            IsWhitelistAvailable = true,
            Price = sellPrice,
            Whitelists = GenWhiteInfoList(GenWhiteList(GenPriceTag("WHITELIST_TAG", whitePrice), User3Address)),
            Duration = new ListDuration()
            {
                StartTime = startTime,
                PublicTime = publicTime,
                DurationHours = 1,
            },
        });

        #endregion

        #region Delist beyound qty

        await Seller1ForestContractStub.Delist.SendAsync(new DelistInput()
        {
            Symbol = NftSymbol,
            Price = sellPrice,
            Quantity = 11
        });

        #endregion

        #region check list empty

        var list = await Seller1ForestContractStub.GetListedNFTInfoList.SendAsync(new GetListedNFTInfoListInput()
        {
            Symbol = NftSymbol,
            Owner = User1Address,
        });
        list.Output.Value.Count.ShouldBe(0);

        #endregion
    }

    [Fact]
    public async void Security_Case19_MakeOffer_invalidQty_fail()
    {
        await InitializeForestContract();
        await PrepareNftData();

        #region MakeOffer invalid qty

        try
        {
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = -1,
                Price = Elf(1_0000_0000)
            });
            true.ShouldBe(false);
        }
        catch (ShouldAssertException e)
        {
            throw;
        }
        catch (Exception e)
        {
            e.Message.ShouldContain("Invalid param Quantity");
        }

        #endregion

        #region MakeOffer invalid amount

        try
        {
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = 1,
                Price = Elf(-1_0000_0000)
            });
            true.ShouldBe(false);
        }
        catch (ShouldAssertException e)
        {
            throw;
        }
        catch (Exception e)
        {
            e.Message.ShouldContain("Invalid price amount");
        }

        #endregion
    }
}
using System;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core.Extension;
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
            WhitelistContractAddress = WhitelistContractAddress
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
        
        #region prepare SEED
        
        await CreateSeedCollection();
        await CreateSeed("SEED-1", "TESTNFT-0");
        await TokenContractStub.Issue.SendAsync(new IssueInput() { Symbol = "SEED-1", To = User1Address, Amount = 1 });
        await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = TokenContractAddress, Symbol = "SEED-1", Amount = 1 });

        #endregion

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
                TotalSupply = 1000,
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
            //nftBalance.Output.Balance.ShouldBe(10);
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
            // transfer thousand ELF to buyer
            await TokenContractStub.Transfer.SendAsync(new TransferInput()
            {
                To = User3Address,
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
                Duration = new ListWithFixedPriceDuration()
                {
                    StartTime = startTime,
                    PublicTime = publicTime,
                    DurationMinutes = 1 * 60,
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
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Spender = ForestContractAddress, Symbol = offerPrice.Symbol,
                Amount = offerQuantity * offerPrice.Amount
            });
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
            await User3TokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Spender = ForestContractAddress, Symbol = offerPrice.Symbol,
                Amount = offerQuantity * offerPrice.Amount
            });
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
                Value = { "ELF", "CPU" }
            }
        });

        try
        {
            await Seller2ForestContractStub.SetTokenWhiteList.SendAsync(new SetTokenWhiteListInput()
            {
                Symbol = NftSymbol,
                TokenWhiteList = new StringList()
                {
                    Value = { "ELF", "CPU" }
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

            var serviceFeeResp = await AdminForestContractStub.GetServiceFeeInfo.SendAsync(new Empty());
            serviceFeeResp?.Output.ShouldNotBeNull();
            serviceFeeResp?.Output.ServiceFeeRate.ShouldBe(1000);
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
            Value = { "ELF", "CPU" }
        });

        try
        {
            await Seller1ForestContractStub.SetGlobalTokenWhiteList.SendAsync(new StringList()
            {
                Value = { "ELF", "CPU" }
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
                Duration = new ListWithFixedPriceDuration()
                {
                    StartTime = startTime,
                    PublicTime = publicTime,
                    DurationMinutes = 1 * 60,
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
                Duration = new ListWithFixedPriceDuration()
                {
                    StartTime = startTime,
                    PublicTime = publicTime,
                    DurationMinutes = 1 * 60,
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
                Duration = new ListWithFixedPriceDuration()
                {
                    StartTime = startTime,
                    PublicTime = publicTime,
                    DurationMinutes = 1 * 60,
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
        var offerQuantity = 20;
        var dealQuantity = 20;
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
    public async void Security_Case17_Deal_needToDelist_success()
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

        await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
            { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = listQuantity });
        await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
        {
            Symbol = NftSymbol,
            Quantity = listQuantity,
            IsWhitelistAvailable = true,
            Price = sellPrice,
            Whitelists = GenWhiteInfoList(GenWhiteList(GenPriceTag("WHITELIST_TAG", whitePrice), User3Address)),
            Duration = new ListWithFixedPriceDuration()
            {
                StartTime = startTime,
                PublicTime = publicTime,
                DurationMinutes = 1 * 60,
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

        #region User1 deal offer, SUCCESS
        
        await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
            { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = dealQuantity+listQuantity });
        await Seller1ForestContractStub.Deal.SendAsync(new DealInput()
        {
            Symbol = NftSymbol,
            OfferFrom = User2Address,
            Price = offerPrice,
            Quantity = dealQuantity
        });

        #endregion
        
        #region check offer list

        {
            var offerList = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
            {
                Symbol = NftSymbol,
                Address = User1Address,
            }).Result.Output;
            offerList.Value.Count.ShouldBe(0);
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
        await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
            { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = listQuantity });
        await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
        {
            Symbol = NftSymbol,
            Quantity = listQuantity,
            IsWhitelistAvailable = true,
            Price = sellPrice,
            Whitelists = GenWhiteInfoList(GenWhiteList(GenPriceTag("WHITELIST_TAG", whitePrice), User3Address)),
            Duration = new ListWithFixedPriceDuration()
            {
                StartTime = startTime,
                PublicTime = publicTime,
                DurationMinutes = 1 * 60,
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

    [Fact]
    public async void Security_SetTokenWhitelist_fail()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var exception = await Assert.ThrowsAsync<Exception>(() => AdminForestContractStub.SetGlobalTokenWhiteList.SendAsync(new StringList()
        {
            Value = { "ERROR" }
        }));
        exception.Message.ShouldContain("Invalid token");

        var userException = await Assert.ThrowsAsync<Exception>(() =>
            Seller1ForestContractStub.SetTokenWhiteList.SendAsync(new SetTokenWhiteListInput()
            {
                Symbol = NftSymbol,
                TokenWhiteList = new StringList() { Value = { } }
            }));
        userException.Message.ShouldContain("length should be between");
        
        userException = await Assert.ThrowsAsync<Exception>(() =>
            Seller1ForestContractStub.SetTokenWhiteList.SendAsync(new SetTokenWhiteListInput()
            {
                Symbol = NftSymbol,
                TokenWhiteList = new StringList()
                {
                    Value =
                    {
                        "ERROR", "ERROR", "ERROR", "ERROR", "ERROR", 
                        "ERROR", "ERROR", "ERROR", "ERROR", "ERROR", 
                        "ERROR", "ERROR", "ERROR", "ERROR", "ERROR", 
                        "ERROR", "ERROR", "ERROR", "ERROR", "ERROR", "ERROR"
                    }
                }
            }));
        userException.Message.ShouldContain("length should be between");
        
        userException = await Assert.ThrowsAsync<Exception>(() =>
            Seller1ForestContractStub.SetTokenWhiteList.SendAsync(new SetTokenWhiteListInput()
            {
                Symbol = NftSymbol,
                TokenWhiteList = new StringList() { Value = { "ERROR" } }
            }));
        userException.Message.ShouldContain("Invalid token");
    }

    [Fact]
    public async void Security_SetWhitelistContract_fail()
    {
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            AdminForestContractStub.SetWhitelistContract.SendAsync(null));
        exception.Message.ShouldContain("Value cannot be null");
    }

    [Fact]
    public async void Security_SetAdministrator_fail()
    {
        await InitializeForestContract();
        var admin = await AdminForestContractStub.GetAdministrator.SendAsync(new Empty());
        admin.Output.ShouldBe(DefaultAddress);

        await AdminForestContractStub.SetAdministrator.SendAsync(User1Address);
        admin = await AdminForestContractStub.GetAdministrator.SendAsync(new Empty());
        admin.Output.ShouldBe(User1Address);
        
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            AdminForestContractStub.SetAdministrator.SendAsync(null));
        exception.Message.ShouldContain("Value cannot be null");
    }
    
    [Fact]
    public async void ListNFT_forManyListings_fail()
    {
        await InitializeForestContract();
        await PrepareNftData();
        
        // whitePrice < sellPrice < offerPrice
        var sellPrice = Elf(1_0000_0000);
        var offerPrice = Elf(0_5000_0000);

        // after publicTime
        var startTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(0));
        var publicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-1));
        var issueAmount = 900;
        // issue 10 NFTs to self
        await UserTokenContractStub.Issue.SendAsync(new IssueInput()
        {
            Symbol = NftSymbol2,
            Amount = issueAmount,
            To = User1Address
        });

        await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
        {
            Symbol = NftSymbol2,
            Amount = issueAmount,
            Spender = ForestContractAddress
        });

        var bizConfig = await Seller1ForestContractStub.GetBizConfig.SendAsync(new Empty());

        #region create list, reach maxCount
        
        for (var i = 0; i < bizConfig.Output.MaxListCount; i++)
        {
            startTime = startTime.AddSeconds(1);
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
            {
                Symbol = NftSymbol2,
                Quantity = 1,
                IsWhitelistAvailable = false,
                Price = sellPrice,
                Duration = new ListWithFixedPriceDuration()
                {
                    StartTime = startTime,
                    PublicTime = publicTime,
                    DurationMinutes = 1 * 60,
                }
            });
        }

        var lists = await Seller1ForestContractStub.GetListedNFTInfoList.SendAsync(new GetListedNFTInfoListInput()
        {
            Owner = User1Address,
            Symbol = NftSymbol2
        });
        lists.Output.Value.Count.ShouldBe(bizConfig.Output.MaxListCount);


        #endregion

        #region  create one more list data, will thorw exception

        var exception = await Assert.ThrowsAsync<Exception>(() => Seller1ForestContractStub.ListWithFixedPrice.SendAsync(
            new ListWithFixedPriceInput()
            {
                Symbol = NftSymbol2,
                Quantity = 1,
                IsWhitelistAvailable = false,
                Price = sellPrice,
                Duration = new ListWithFixedPriceDuration()
                {
                    StartTime = startTime.AddSeconds(1),
                    PublicTime = publicTime,
                    DurationMinutes = 1 * 60,
                }
            }));
        exception.Message.ShouldContain("reached the maximum");
        
        #endregion
        
    }

    [Fact]
    public async void MakeOffer_forManyOffers_fail()
    {
        
        await InitializeForestContract();
        await PrepareNftData();
        
        // whitePrice < sellPrice < offerPrice
        var sellPrice = Elf(1_0000_0000);
        var offerPrice = Elf(0_5000_0000);
        var issueAmount = 900;

        // issue 10 NFTs to self
        await UserTokenContractStub.Issue.SendAsync(new IssueInput()
        {
            Symbol = NftSymbol2,
            Amount = issueAmount,
            To = User3Address
        });

        #region create offer reach maxCount

        var bizConfig = await Seller1ForestContractStub.GetBizConfig.SendAsync(new Empty());

        await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
        {
            Spender = ForestContractAddress, Symbol = offerPrice.Symbol,
            Amount = bizConfig.Output.MaxListCount*offerPrice.Amount
        });
        for (var i = 0; i < bizConfig.Output.MaxListCount; i++)
        {
            // user2 make offer to user1
            await Buyer1ForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol2,
                OfferTo = User3Address,
                Quantity = 1,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });
        }

      
        var offerList = await BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
        {
            Symbol = NftSymbol2,
            Address = User1Address,
        });
        offerList.Output.Value.Count.ShouldBe(bizConfig.Output.MaxListCount);
        #endregion

        await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
        {
            Spender = ForestContractAddress, Symbol = offerPrice.Symbol,
            Amount = bizConfig.Output.MaxListCount*offerPrice.Amount + offerPrice.Amount
        });
        #region create one more offer, got exception
        var exception = await Assert.ThrowsAsync<Exception>(() => Buyer1ForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
        {
            Symbol = NftSymbol2,
            OfferTo = User3Address,
            Quantity = 1,
            Price = offerPrice,
            ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
        }));
        exception.Message.ShouldContain("reached the maximum");
        #endregion
    }
}
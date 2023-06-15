using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Forest;

public class ForestContractTests_Deal : ForestContractTestBase
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
                IsBurnable = false,
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

            // approve contract handle NFT2 of seller   
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Symbol = NftSymbol2,
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
    public async void Deal_Case44_Deal_Offer_deal()
    {
        await InitializeForestContract();
        await PrepareNftData();

        
        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 2;
        var dealQuantity = 2;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;

        #region user buy

        {
            // user2 make offer to user1
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
            // list offers just sent
            var offerList = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
            {
                Symbol = NftSymbol,
                Address = User2Address,
            }).Result.Output;
            offerList.Value.Count.ShouldBeGreaterThan(0);
            offerList.Value[0].To.ShouldBe(User1Address);
            offerList.Value[0].From.ShouldBe(User2Address);
            offerList.Value[0].Quantity.ShouldBe(offerQuantity);
        }

        #endregion

        #region deal

        {
            await Seller1ForestContractStub.Deal.SendAsync(new DealInput()
            {
                Symbol = NftSymbol,
                Price = offerPrice,
                OfferFrom = User2Address,
                Quantity = dealQuantity
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
            nftBalance.Output.Balance.ShouldBe(8);
        }

        #endregion

        #region check buyer NFT

        {
            var nftBalance = await User2TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User2Address
            });
            nftBalance.Output.Balance.ShouldBe(2);
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
            user1ElfBalance.Output.Balance.ShouldBe(InitializeElfAmount + offerPrice.Amount * dealQuantity - serviceFee);
        }

        #endregion
        
    }
    
    [Fact]
    public async void Deal_Case45_Deal_Offer_fail()
    {
        await InitializeForestContract();
        await PrepareNftData();

        
        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 2;
        var dealQuantity = 3;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;


        #region user buy

        {
            // user2 make offer to user1
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
            // list offers just sent
            var offerList = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
            {
                Symbol = NftSymbol,
                Address = User2Address,
            }).Result.Output;
            offerList.Value.Count.ShouldBeGreaterThan(0);
            offerList.Value[0].To.ShouldBe(User1Address);
            offerList.Value[0].From.ShouldBe(User2Address);
            offerList.Value[0].Quantity.ShouldBe(offerQuantity);
        }

        #endregion

        #region deal

        {
            try
            {
                await Seller1ForestContractStub.Deal.SendAsync(new DealInput()
                {
                    Symbol = NftSymbol2,
                    Price = offerPrice,
                    OfferFrom = User2Address,
                    Quantity = 1
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
                // shoud throw "Neither related offer nor bid are found." exception
                e.ShouldNotBeNull();
            }

        }

        #endregion

    }
    
    [Fact]
    public async void Deal_Case46_Deal_Offer_beyondQuantity_fail()
    {
        await InitializeForestContract();
        await PrepareNftData();

        
        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 2;
        var dealQuantity = 3;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;

        #region user buy

        {
            // user2 make offer to user1
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
            // list offers just sent
            var offerList = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
            {
                Symbol = NftSymbol,
                Address = User2Address,
            }).Result.Output;
            offerList.Value.Count.ShouldBeGreaterThan(0);
            offerList.Value[0].To.ShouldBe(User1Address);
            offerList.Value[0].From.ShouldBe(User2Address);
            offerList.Value[0].Quantity.ShouldBe(offerQuantity);
        }

        #endregion

        #region deal

        {
            try
            {
                await Seller1ForestContractStub.Deal.SendAsync(new DealInput()
                {
                    Symbol = NftSymbol,
                    Price = offerPrice,
                    OfferFrom = User2Address,
                    Quantity = 3
                });
            }
            catch (ShouldAssertException e)
            {
                throw;
            }
            catch (Exception e)
            {
                // shoud throw "Neither related offer nor bid are found." exception
                e.ShouldNotBeNull();
            }

        }

        #endregion
     
    }
    
    [Fact]
    public async void Deal_Case46_Deal_Offer_lessQuantity_deal()
    {
        await InitializeForestContract();
        await PrepareNftData();

        
        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 2;
        var dealQuantity = 1;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;

        #region user buy

        {
            // user2 make offer to user1
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
            // list offers just sent
            var offerList = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
            {
                Symbol = NftSymbol,
                Address = User2Address,
            }).Result.Output;
            offerList.Value.Count.ShouldBeGreaterThan(0);
            offerList.Value[0].To.ShouldBe(User1Address);
            offerList.Value[0].From.ShouldBe(User2Address);
            offerList.Value[0].Quantity.ShouldBe(offerQuantity);
        }

        #endregion

        #region deal

        {
            await Seller1ForestContractStub.Deal.SendAsync(new DealInput()
            {
                Symbol = NftSymbol,
                Price = offerPrice,
                OfferFrom = User2Address,
                Quantity = dealQuantity
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
            user1ElfBalance.Output.Balance.ShouldBe(InitializeElfAmount + offerPrice.Amount * dealQuantity - serviceFee);
        }

        #endregion
     
    }

    [Fact]
    public async void Deal_Case47_Deal_OfferList_afterOnShelf_deal()
    {
        await InitializeForestContract();
        await PrepareNftData();
        
        // whitePrice <= offerPrice < sellPrice
        var sellPrice = Elf(5_0000_0000);
        var whitePrice = Elf(2_0000_0000);
        var offerPrice = Elf(3_0000_0000);
        var offerQuantity = 1;
        var dealQuantity = 1;
        var serviceFee = dealQuantity * offerPrice.Amount * ServiceFeeRate / 10000;
        
        // after publicTime
        var startTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-5));
        var publicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-1));

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
                                Value =
                                {
                                    // User2Address, 
                                    User3Address
                                },
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
        
        #region common user buy

        {
            await MineAsync(new List<Transaction>(), Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(1)));
            
            // user2 make offer to user1
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
            offerList.Value[0].Quantity.ShouldBe(offerQuantity);
            offerList.Value[0].Price.ShouldBe(offerPrice);
        }

        #endregion
        
        #region deal

        {
            await Seller1ForestContractStub.Deal.SendAsync(new DealInput()
            {
                Symbol = NftSymbol,
                Price = offerPrice,
                OfferFrom = User2Address,
                Quantity = dealQuantity
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
            nftBalance.Output.Balance.ShouldBe(10 - dealQuantity);
        }

        #endregion

        #region check buyer NFT

        {
            var nftBalance = await User2TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User2Address
            });
            nftBalance.Output.Balance.ShouldBe(dealQuantity);
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
            user1ElfBalance.Output.Balance.ShouldBe(InitializeElfAmount + offerPrice.Amount * dealQuantity - serviceFee);
        }

        #endregion
        
    }

    [Fact]
    public async void Deal_Case48_Nft_notEnough_fail()
    {
        await InitializeForestContract();
        await PrepareNftData();

        
        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 50;
        var dealQuantity = 50;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;

        #region user buy

        {
            // user2 make offer to user1
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
            // list offers just sent
            var offerList = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
            {
                Symbol = NftSymbol,
                Address = User2Address,
            }).Result.Output;
            offerList.Value.Count.ShouldBeGreaterThan(0);
            offerList.Value[0].To.ShouldBe(User1Address);
            offerList.Value[0].From.ShouldBe(User2Address);
            offerList.Value[0].Quantity.ShouldBe(offerQuantity);
        }

        #endregion

        #region deal

        {
            try
            {
                await Seller1ForestContractStub.Deal.SendAsync(new DealInput()
                {
                    Symbol = NftSymbol,
                    Price = offerPrice,
                    OfferFrom = User2Address,
                    Quantity = dealQuantity
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
                // shoud throw "Insufficient NFT balance." exception
                e.ShouldNotBeNull();
            }

        }

        #endregion
    }
    
    [Fact]
    public async void Deal_Case49_Nft_offerExpired_fail()
    {
        await InitializeForestContract();
        await PrepareNftData();

        
        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 1;
        var dealQuantity = 1;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;

        #region user buy

        {
            // user2 make offer to user1
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
            offerList.Value[0].Quantity.ShouldBe(offerQuantity);
        }

        #endregion

        #region deal

        {
            try
            {
                await Seller1ForestContractStub.Deal.SendAsync(new DealInput()
                {
                    Symbol = NftSymbol,
                    Price = offerPrice,
                    OfferFrom = User2Address,
                    Quantity = dealQuantity
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
                // shoud throw "Insufficient NFT balance." exception
                e.ShouldNotBeNull();
            }

        }

        #endregion
    }
    
    [Fact]
    public async void Deal_Case50_Nft_invalidSymbol_fail()
    {
        await InitializeForestContract();
        await PrepareNftData();

        
        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 1;
        var dealQuantity = 1;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;

        #region user buy

        {
            // user2 make offer to user1
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
            offerList.Value[0].Quantity.ShouldBe(offerQuantity);
        }

        #endregion

        #region deal

        {
            try
            {
                await Seller1ForestContractStub.Deal.SendAsync(new DealInput()
                {
                    Symbol = "NOT_EXISTS",
                    Price = offerPrice,
                    OfferFrom = User2Address,
                    Quantity = dealQuantity
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
                // shoud throw "Insufficient NFT balance." exception
                e.ShouldNotBeNull();
            }

        }

        #endregion
    }
    
    [Fact]
    public async void Deal_Case51_Nft_invalidOfferFrom_fail()
    {
        await InitializeForestContract();
        await PrepareNftData();

        
        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 1;
        var dealQuantity = 1;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;

        #region user buy

        {
            // user2 make offer to user1
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
            // list offers just sent
            var offerList = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
            {
                Symbol = NftSymbol,
                Address = User2Address,
            }).Result.Output;
            offerList.Value.Count.ShouldBeGreaterThan(0);
            offerList.Value[0].To.ShouldBe(User1Address);
            offerList.Value[0].From.ShouldBe(User2Address);
            offerList.Value[0].Quantity.ShouldBe(offerQuantity);
        }

        #endregion

        #region deal

        {
            try
            {
                await Seller1ForestContractStub.Deal.SendAsync(new DealInput()
                {
                    Symbol = NftSymbol,
                    Price = offerPrice,
                    OfferFrom = User4Address,
                    Quantity = dealQuantity
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
                // shoud throw "Insufficient NFT balance." exception
                e.ShouldNotBeNull();
            }

        }

        #endregion
    }

    [Fact]
    public async void Deal_Case52_Nft_invalidDealQuantity_fail()
    {
        await InitializeForestContract();
        await PrepareNftData();

        
        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 1;
        var dealQuantity = 2;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;

        #region user buy

        {
            // user2 make offer to user1
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
            // list offers just sent
            var offerList = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
            {
                Symbol = NftSymbol,
                Address = User2Address,
            }).Result.Output;
            offerList.Value.Count.ShouldBeGreaterThan(0);
            offerList.Value[0].To.ShouldBe(User1Address);
            offerList.Value[0].From.ShouldBe(User2Address);
            offerList.Value[0].Quantity.ShouldBe(offerQuantity);
        }

        #endregion

        #region deal

        {
            try
            {
                await Seller1ForestContractStub.Deal.SendAsync(new DealInput()
                {
                    Symbol = NftSymbol,
                    Price = offerPrice,
                    OfferFrom = User2Address,
                    Quantity = dealQuantity
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
                // shoud throw "Insufficient NFT balance." exception
                e.ShouldNotBeNull();
            }

        }

        #endregion
    }
    
    [Fact]
    public async void Deal_Case53_Nft_ElfNotEnough_fail()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(50000_0000_0000);
        var offerQuantity = 1;
        var dealQuantity = 1;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;

        #region user buy

        {
            // user2 make offer to user1
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
            // list offers just sent
            var offerList = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
            {
                Symbol = NftSymbol,
                Address = User2Address,
            }).Result.Output;
            offerList.Value.Count.ShouldBeGreaterThan(0);
            offerList.Value[0].To.ShouldBe(User1Address);
            offerList.Value[0].From.ShouldBe(User2Address);
            offerList.Value[0].Quantity.ShouldBe(offerQuantity);
        }

        #endregion

        #region deal

        {
            try
            {
                await Seller1ForestContractStub.Deal.SendAsync(new DealInput()
                {
                    Symbol = NftSymbol,
                    Price = offerPrice,
                    OfferFrom = User2Address,
                    Quantity = dealQuantity
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
                // shoud throw "Insufficient NFT balance." exception
                e.ShouldNotBeNull();
            }

        }

        #endregion
    }
    
    [Fact]
    public async void Deal_Case54_Deal_afterOnShelf_fail()
    {
        await InitializeForestContract();
        await PrepareNftData();
        
        // whitePrice <= offerPrice < sellPrice
        var sellPrice = Elf(5_0000_0000);
        var whitePrice = Elf(2_0000_0000);
        var offerPrice = Elf(3_0000_0000);
        var offerQuantity = 8;
        var dealQuantity = 7;
        var serviceFee = dealQuantity * offerPrice.Amount * ServiceFeeRate / 10000;
        
        // after publicTime
        var startTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-5));
        var publicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-1));

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
                                Value =
                                {
                                    // User2Address, 
                                    User3Address
                                },
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
        
        #region common user buy

        {
            await MineAsync(new List<Transaction>(), Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(1)));
            
            // user2 make offer to user1
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
            offerList.Value[0].Quantity.ShouldBe(offerQuantity);
            offerList.Value[0].Price.ShouldBe(offerPrice);
        }

        #endregion
        
        #region deal

        {
            try
            {
                await Seller1ForestContractStub.Deal.SendAsync(new DealInput()
                {
                    Symbol = NftSymbol,
                    Price = offerPrice,
                    OfferFrom = User2Address,
                    Quantity = dealQuantity
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
                e.ShouldNotBeNull();
            }
            
        }

        #endregion
        
    }




}
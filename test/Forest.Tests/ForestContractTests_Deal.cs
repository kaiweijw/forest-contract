using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
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
            var executionResult = await Seller1ForestContractStub.Deal.SendAsync(new DealInput()
            {
                Symbol = NftSymbol,
                Price = offerPrice,
                OfferFrom = User2Address,
                Quantity = dealQuantity
            });

            var log1 = OfferRemoved.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .First(l => l.Name == nameof(OfferRemoved))
                .NonIndexed);
            log1.OfferFrom.ShouldBe(User2Address);
            log1.Symbol.ShouldBe(NftSymbol);
            log1.ExpireTime.ShouldNotBeNull();
            log1.OfferTo.ShouldBe(User1Address);

            var log2 = Sold.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .First(l => l.Name == nameof(Sold))
                .NonIndexed);
            log2.NftSymbol.ShouldBe(NftSymbol);
            log2.NftFrom.ShouldBe(User1Address);
            log2.NftQuantity.ShouldBe(2);
            log2.PurchaseAmount.ShouldBe(1000000000);
            log2.NftTo.ShouldBe(User2Address);
            log2.PurchaseSymbol.ShouldBe("ELF");


            var log3 = Transferred.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .First(l => l.Name == nameof(Transferred))
                .NonIndexed);
            log3.Amount.ShouldBe(900000000);
        }

        #endregion

        #region check seller NFT

        {
            var nftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User1Address
            });
            //nftBalance.Output.Balance.ShouldBe(8);
        }

        #endregion

        #region check buyer NFT

        {
            var nftBalance = await User2TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User2Address
            });
            //nftBalance.Output.Balance.ShouldBe(2);
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
            user1ElfBalance.Output.Balance.ShouldBe(InitializeElfAmount + offerPrice.Amount * dealQuantity -
                                                    serviceFee);
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
                e.Message.ShouldContain("offer is empty");
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
                
                // never run this line
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
            //nftBalance.Output.Balance.ShouldBe(9);
        }

        #endregion

        #region check buyer NFT

        {
            var nftBalance = await User2TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User2Address
            });
            //nftBalance.Output.Balance.ShouldBe(1);
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
            user1ElfBalance.Output.Balance.ShouldBe(InitializeElfAmount + offerPrice.Amount * dealQuantity -
                                                    serviceFee);
        }

        #endregion
    }

    
    [Fact]
    public async void Deal_Case46_Deal_Offer_amountNotMatch_fail()
    {
        await InitializeForestContract();
        await PrepareNftData();
        
        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var dealPrice = Elf(15_0000_0000);
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
                    Price = dealPrice,
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
                e.Message.ShouldContain("offer is empty");
            }

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
        var listQuantity = 5;
        
        #region ListWithFixedPrice

        {
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
            {
                Symbol = NftSymbol,
                Quantity = listQuantity,
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
            //nftBalance.Output.Balance.ShouldBe(10);
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
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = listQuantity + dealQuantity });
            var executionResult = await Seller1ForestContractStub.Deal.SendAsync(new DealInput()
            {
                Symbol = NftSymbol,
                Price = offerPrice,
                OfferFrom = User2Address,
                Quantity = dealQuantity
            });

            var log1 = OfferRemoved.Parser
                .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name == nameof(OfferRemoved))
                    .NonIndexed);
            log1.OfferFrom.ShouldBe(User2Address);
            log1.Symbol.ShouldBe(NftSymbol);
            log1.ExpireTime.ShouldNotBeNull();
            log1.OfferTo.ShouldBe(User1Address);

            var log2 = Sold.Parser
                .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name == nameof(Sold))
                    .NonIndexed);
            log2.NftSymbol.ShouldBe(NftSymbol);
            log2.NftFrom.ShouldBe(User1Address);
            log2.NftQuantity.ShouldBe(1);
            log2.PurchaseAmount.ShouldBe(300000000);
            log2.NftTo.ShouldBe(User2Address);
            log2.PurchaseSymbol.ShouldBe("ELF");

            var log3 = Transferred.Parser
                .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name == nameof(Transferred))
                    .NonIndexed);
            log3.Amount.ShouldBe(270000000);
        }

        #endregion

        #region check seller NFT

        {
            var nftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User1Address
            });
            //nftBalance.Output.Balance.ShouldBe(10 - dealQuantity);
        }

        #endregion

        #region check buyer NFT

        {
            var nftBalance = await User2TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User2Address
            });
            //nftBalance.Output.Balance.ShouldBe(dealQuantity);
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
            user1ElfBalance.Output.Balance.ShouldBe(InitializeElfAmount + offerPrice.Amount * dealQuantity -
                                                    serviceFee);
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
                e.Message.ShouldContain("Insufficient NFT balance");
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
                e.Message.ShouldContain("offer is empty");
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
                e.Message.ShouldContain("Insufficient NFT balance");
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
        var dealQuantity = 2;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;

        #region user buy

        {
            await User3TokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = offerPrice.Symbol, Amount = offerPrice.Amount * offerQuantity });
            // user3 make offer to user1
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
            // list offers just sent
            var offerList = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
            {
                Symbol = NftSymbol,
                Address = User3Address,
            }).Result.Output;
            offerList.Value.Count.ShouldBeGreaterThan(0);
            offerList.Value[0].To.ShouldBe(User1Address);
            offerList.Value[0].From.ShouldBe(User3Address);
            offerList.Value[0].Quantity.ShouldBe(offerQuantity);
        }

        #endregion

        #region deal

        {
            try
            {
                await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = dealQuantity-1 });
                await Seller1ForestContractStub.Deal.SendAsync(new DealInput()
                {
                    Symbol = NftSymbol,
                    Price = offerPrice,
                    OfferFrom = User3Address,
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
                e.Message.ShouldContain("The allowance you set is less than required. Please reset it.");
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
                e.Message.ShouldContain("Deal quantity exceeded");
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
        var offerPrice = Elf(10000_0000_0000);
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
            
            // user2 transfer ELF away
            await User2TokenContractStub.Transfer.SendAsync(new TransferInput()
            {
                Symbol = ElfSymbol,
                To = User1Address,
                Amount = InitializeElfAmount,
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
            var exception = await Assert.ThrowsAsync<Exception>(() => Seller1ForestContractStub.Deal.SendAsync(new DealInput()
            {
                Symbol = NftSymbol,
                Price = offerPrice,
                OfferFrom = User2Address,
                Quantity = dealQuantity
            }));
            exception.Message.ShouldContain("Insufficient balance");
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
        var listQuantity = 5;
        var serviceFee = dealQuantity * offerPrice.Amount * ServiceFeeRate / 10000;

        // after publicTime
        var startTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-5));
        var publicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-1));

        #region ListWithFixedPrice

        {
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
            {
                Symbol = NftSymbol,
                Quantity = listQuantity,
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
            //nftBalance.Output.Balance.ShouldBe(10);
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
                await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = listQuantity + dealQuantity });

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
                e.Message.ShouldContain("Need to delist at least");
            }
        }

        #endregion
    }

    [Fact]
    //deal nft allowance gretter enough
    public async void Deal_Allowance_Case1()
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

        var sellerNftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
        {
            Symbol = NftSymbol,
            Owner = User1Address
        });
        
        #region deal

        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = dealQuantity+1 });
            var executionResult = await Seller1ForestContractStub.Deal.SendAsync(new DealInput()
            {
                Symbol = NftSymbol,
                Price = offerPrice,
                OfferFrom = User2Address,
                Quantity = dealQuantity
            });

            var log1 = OfferRemoved.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .First(l => l.Name == nameof(OfferRemoved))
                .NonIndexed);
            log1.OfferFrom.ShouldBe(User2Address);
            log1.Symbol.ShouldBe(NftSymbol);
            log1.ExpireTime.ShouldNotBeNull();
            log1.OfferTo.ShouldBe(User1Address);

            var log2 = Sold.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .First(l => l.Name == nameof(Sold))
                .NonIndexed);
            log2.NftSymbol.ShouldBe(NftSymbol);
            log2.NftFrom.ShouldBe(User1Address);
            log2.NftQuantity.ShouldBe(dealQuantity);
            log2.PurchaseAmount.ShouldBe(1000000000);
            log2.NftTo.ShouldBe(User2Address);
            log2.PurchaseSymbol.ShouldBe("ELF");
            
        }

        #endregion

        #region check seller NFT

        {
            var nftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User1Address
            });
            nftBalance.Output.Balance.ShouldBe(sellerNftBalance.Output.Balance - dealQuantity);
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
        
    }
    
     [Fact]
    //deal nft allowance not enough
    public async void Deal_Allowance_Case2()
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

        var sellerNftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
        {
            Symbol = NftSymbol,
            Owner = User1Address
        });
        
        #region deal

        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = dealQuantity-1 });
            var errorMessage = "";
            try
            {
                var executionResult = await Seller1ForestContractStub.Deal.SendAsync(new DealInput()
                {
                    Symbol = NftSymbol,
                    Price = offerPrice,
                    OfferFrom = User2Address,
                    Quantity = dealQuantity
                });
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
            }
            errorMessage.ShouldContain("The allowance you set is less than required. Please reset it.");
        }

        #endregion
    }
    
       [Fact]
    //deal equal allowance enough
    public async void Deal_Allowance_Case3()
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

        var sellerNftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
        {
            Symbol = NftSymbol,
            Owner = User1Address
        });
        
        #region deal

        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = dealQuantity });
            var executionResult = await Seller1ForestContractStub.Deal.SendAsync(new DealInput()
            {
                Symbol = NftSymbol,
                Price = offerPrice,
                OfferFrom = User2Address,
                Quantity = dealQuantity
            });

            var log1 = OfferRemoved.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .First(l => l.Name == nameof(OfferRemoved))
                .NonIndexed);
            log1.OfferFrom.ShouldBe(User2Address);
            log1.Symbol.ShouldBe(NftSymbol);
            log1.ExpireTime.ShouldNotBeNull();
            log1.OfferTo.ShouldBe(User1Address);

            var log2 = Sold.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .First(l => l.Name == nameof(Sold))
                .NonIndexed);
            log2.NftSymbol.ShouldBe(NftSymbol);
            log2.NftFrom.ShouldBe(User1Address);
            log2.NftQuantity.ShouldBe(dealQuantity);
            log2.PurchaseAmount.ShouldBe(1000000000);
            log2.NftTo.ShouldBe(User2Address);
            log2.PurchaseSymbol.ShouldBe("ELF");
            
        }

        #endregion

        #region check seller NFT

        {
            var nftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User1Address
            });
            nftBalance.Output.Balance.ShouldBe(sellerNftBalance.Output.Balance - dealQuantity);
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
        
    }
    
     [Fact]
    //deal buyer allowance not enough
    public async void Deal_Allowance_Case4()
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
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = offerPrice.Symbol, Amount = offerPrice.Amount*offerQuantity });
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

        var sellerNftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
        {
            Symbol = NftSymbol,
            Owner = User1Address
        });
        
        #region deal

        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = dealQuantity });
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = offerPrice.Symbol, Amount = offerPrice.Amount*offerQuantity-1 });

            var errorMessage = "";
            try
            {
                var executionResult = await Seller1ForestContractStub.Deal.SendAsync(new DealInput()
                {
                    Symbol = NftSymbol,
                    Price = offerPrice,
                    OfferFrom = User2Address,
                    Quantity = dealQuantity
                });
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
            }
            errorMessage.ShouldContain("[TransferFrom]Insufficient allowance");
        }

        #endregion
    } 
    
    [Fact]
    //deal equal allowance enough
    public async void Deal_Allowance_Case5()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var offerPrice = Elf(1);
        var offerQuantity = 5;
        var dealQuantity = 5;

        #region user buy

        {
            // user2 make offer to user1
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = offerPrice.Symbol, Amount = offerPrice.Amount*offerQuantity*2 });
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = offerQuantity,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol2,
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
            var offerList1 = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
            {
                Symbol = NftSymbol,
                Address = User2Address,
            }).Result.Output;
            offerList1.Value.Count.ShouldBe(1);
            offerList1.Value[0].To.ShouldBe(User1Address);
            offerList1.Value[0].From.ShouldBe(User2Address);
            offerList1.Value[0].Quantity.ShouldBe(offerQuantity);
            
            var offerList2 = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
            {
                Symbol = NftSymbol2,
                Address = User2Address,
            }).Result.Output;
            offerList2.Value.Count.ShouldBe(1);
            offerList2.Value[0].To.ShouldBe(User1Address);
            offerList2.Value[0].From.ShouldBe(User2Address);
            offerList2.Value[0].Quantity.ShouldBe(offerQuantity);
        }

        #endregion
        
        #region check totalAmount
        {
            var totalOfferAmount = await BuyerForestContractStub.GetTotalOfferAmount.CallAsync(
                new GetTotalOfferAmountInput()
                {
                    Address = User2Address,
                    PriceSymbol = offerPrice.Symbol,
                });
            totalOfferAmount.TotalAmount.ShouldBe(offerPrice.Amount*offerQuantity*2);
            totalOfferAmount.Allowance.ShouldBe(offerPrice.Amount*offerQuantity*2);
        }
        #endregion
        
        #region deal

        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = dealQuantity });
            var executionResult = await Seller1ForestContractStub.Deal.SendAsync(new DealInput()
            {
                Symbol = NftSymbol,
                Price = offerPrice,
                OfferFrom = User2Address,
                Quantity = dealQuantity
            });

            var log1 = OfferRemoved.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .First(l => l.Name == nameof(OfferRemoved))
                .NonIndexed);
            log1.OfferFrom.ShouldBe(User2Address);
            log1.Symbol.ShouldBe(NftSymbol);
            log1.ExpireTime.ShouldNotBeNull();
            log1.OfferTo.ShouldBe(User1Address);

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
        #region check totalAmount
        {
            var totalOfferAmount = await BuyerForestContractStub.GetTotalOfferAmount.CallAsync(
                new GetTotalOfferAmountInput()
                {
                    Address = User2Address,
                    PriceSymbol = offerPrice.Symbol,
                });
            totalOfferAmount.TotalAmount.ShouldBe(offerPrice.Amount*offerQuantity);
            totalOfferAmount.Allowance.ShouldBe(offerPrice.Amount*offerQuantity);
        }
        #endregion
    }
}
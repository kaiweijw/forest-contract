using System;
using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Forest;

public class ForestContractTests_CancelOffer : ForestContractTestBase
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
        }

        #endregion

        #region approve transfer

        {
            // approve contract handle NFT of seller   
            var executionResult = await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Symbol = NftSymbol,
                Amount = 5,
                Spender = ForestContractAddress
            });
            var log = Approved.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .First(l => l.Name == nameof(Approved))
                .NonIndexed);
            log.Amount.ShouldBe(5);

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
    public async void CancelOffer_Case2_Admin_CancelExpiredOffer()
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
            // user2 make offer VALID
            var executionResult = await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = offerQuantity,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });
            var log = OfferAdded.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .First(l => l.Name == nameof(OfferAdded))
                .NonIndexed);
            log.OfferFrom.ShouldBe(User2Address);
            log.Quantity.ShouldBe(2);
            log.Symbol.ShouldBe(NftSymbol);
            log.Price.Symbol.ShouldBe(ElfSymbol);
            log.Price.Amount.ShouldBe(500000000);
            log.ExpireTime.ShouldNotBeNull();
            log.OfferTo.ShouldBe(User1Address);
            //OfferMade
            // user2 make offer EXPIRE1
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = offerQuantity,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-5)),
            });

            // user2 make offer EXPIRE2
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = offerQuantity,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-10)),
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
            offerList.Value.Count.ShouldBe(3);
        }

        #endregion

        #region Admin Cancel order

        {
            var executionResult = await AdminForestContractStub.CancelOffer.SendAsync(new CancelOfferInput()
            {
                Symbol = NftSymbol,
                OfferFrom = User2Address,
                IndexList = new Int32List()
                {
                    Value = { 0 }
                }
            });
            var log = OfferRemoved.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .First(l => l.Name == nameof(OfferRemoved))
                .NonIndexed);
            log.OfferFrom.ShouldBe(User2Address);
            log.Symbol.ShouldBe(NftSymbol);
            log.ExpireTime.ShouldNotBeNull();
            log.OfferTo.ShouldBe(User1Address);
            log.Price.ShouldNotBeNull();
            log.Price.Amount.ShouldBe(5_0000_0000);
            log.Price.Symbol.ShouldBe("ELF");
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
            offerList.Value.Count.ShouldBe(1);
        }

        #endregion
    }

    
     [Fact]
    public async void CancelOfferListByExpireTime_Case_CancelOfferSuccess()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var offerPrice2 = Elf(5_0000_0000 * 2);
        var offerQuantity = 2;
        var dealQuantity = 2;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;

        #region user buy
        var expireTime1 = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5));
        {
            // user2 make offer VALID
            var executionResult = await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = offerQuantity,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });
            var log = OfferAdded.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .First(l => l.Name == nameof(OfferAdded))
                .NonIndexed);
            log.OfferFrom.ShouldBe(User2Address);
            log.Quantity.ShouldBe(2);
            log.Symbol.ShouldBe(NftSymbol);
            log.Price.Symbol.ShouldBe(ElfSymbol);
            log.Price.Amount.ShouldBe(500000000);
            log.ExpireTime.ShouldNotBeNull();
            log.OfferTo.ShouldBe(User1Address);
            //OfferMade
            // user2 make offer EXPIRE1
            
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = offerQuantity,
                Price = offerPrice,
                ExpireTime = expireTime1,
            });

            // user2 make offer EXPIRE2
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = offerQuantity,
                Price = offerPrice2,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(10)),
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
            offerList.Value.Count.ShouldBe(3);
        }

        #endregion

        #region Cancel order

        {
            var cancelOfferList = new RepeatedField<CancelOffer>(); 
            cancelOfferList.Add(new CancelOffer
            {
                OfferTo = User1Address,
                ExpireTime = expireTime1,
                Price = offerPrice
            });
            
            var cancelOfferListByExpireTimeInput = new CancelOfferListByExpireTimeInput();
            cancelOfferListByExpireTimeInput.Symbol = NftSymbol;
            cancelOfferListByExpireTimeInput.CancelOfferList.AddRange(cancelOfferList);
            var executionResult = await BuyerForestContractStub.CancelOfferListByExpireTime.SendAsync(cancelOfferListByExpireTimeInput);
            
            var log = OfferRemoved.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .First(l => l.Name == nameof(OfferRemoved))
                .NonIndexed);
            log.OfferFrom.ShouldBe(User2Address);
            log.Symbol.ShouldBe(NftSymbol);
            log.ExpireTime.ShouldNotBeNull();
            log.OfferTo.ShouldBe(User1Address);
            log.Price.ShouldNotBeNull();
            log.Price.Amount.ShouldBe(5_0000_0000);
            log.Price.Symbol.ShouldBe("ELF");
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
            offerList.Value.Count.ShouldBe(1);
            offerList.Value.First().Price.Amount.ShouldBe(offerPrice2.Amount);
        }

        #endregion
    }
    
    [Fact]
    public async void CancelOffer_Case3_ContractNotInitialize_fail()
    {
        // await InitializeForestContract();
        await PrepareNftData();

        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 2;
        var dealQuantity = 2;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;

        #region Cancel order

        {
            try
            {
                await AdminForestContractStub.CancelOffer.SendAsync(new CancelOfferInput()
                {
                    Symbol = NftSymbol,
                    OfferFrom = User2Address,
                    IndexList = new Int32List()
                    {
                        Value = { 0 }
                    }
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
                e.Message.ShouldContain("Contract not initialized");
            }
        }

        #endregion
    }

    [Fact]
    public async void CancelOffer_Case4_commonUser_cancelExpiredOffer()
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

        #region common user Cancel order

        {
            try
            {
                await Seller1ForestContractStub.CancelOffer.SendAsync(new CancelOfferInput()
                {
                    Symbol = NftSymbol,
                    OfferFrom = User2Address,
                    IndexList = new Int32List()
                    {
                        Value = { 0 }
                    }
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
    public async void CancelOffer_Case5_offerUser_cancelValidOffer()
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

        #region common user Cancel order

        {
            await BuyerForestContractStub.CancelOffer.SendAsync(new CancelOfferInput()
            {
                Symbol = NftSymbol,
                OfferFrom = User2Address,
                IndexList = new Int32List()
                {
                    Value = { 0 }
                }
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
            offerList.Value.Count.ShouldBe(0);
        }

        #endregion
    }

    [Fact]
    public async void CancelOffer_Case6_commonUser_cancelValidOffer()
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

        #region common user Cancel order

        {
            try
            {
                await Seller1ForestContractStub.CancelOffer.SendAsync(new CancelOfferInput()
                {
                    Symbol = NftSymbol,
                    OfferFrom = User2Address,
                    IndexList = new Int32List()
                    {
                        Value = { 0 }
                    }
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
    public async void CancelOffer_Case7_Admin_CancelValidOffer()
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

        #region Admin Cancel order

        {
            await AdminForestContractStub.CancelOffer.SendAsync(new CancelOfferInput()
            {
                Symbol = NftSymbol,
                OfferFrom = User2Address,
                IndexList = new Int32List()
                {
                    Value = { 0 }
                }
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
    }

    [Fact]
    public async void CancelOffer_Case8_Admin_CancelExpiredOffer()
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

        #region Admin Cancel order

        {
            await AdminForestContractStub.CancelOffer.SendAsync(new CancelOfferInput()
            {
                Symbol = NftSymbol,
                OfferFrom = User2Address,
                IndexList = new Int32List()
                {
                    Value = { 0 }
                }
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
            offerList.Value.Count.ShouldBe(0);
        }

        #endregion
    }

    [Fact]
    public async void CancelOffer_Case9_commonUser_cancelOfferTwice()
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

        #region common user Cancel order

        {
            await BuyerForestContractStub.CancelOffer.SendAsync(new CancelOfferInput()
            {
                Symbol = NftSymbol,
                OfferFrom = User2Address,
                IndexList = new Int32List()
                {
                    Value = { 0 }
                }
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
            offerList.Value.Count.ShouldBe(0);
        }

        #endregion

        #region common user Cancel order again

        {
            try
            {
                await BuyerForestContractStub.CancelOffer.SendAsync(new CancelOfferInput()
                {
                    Symbol = NftSymbol,
                    OfferFrom = User2Address,
                    IndexList = new Int32List()
                    {
                        Value = { 0 }
                    }
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
                e.Message.ShouldContain("Offer not exists");
            }
        }

        #endregion
    }


    [Fact]
    public async void CancelOffer_Case10_commonUser_cancelAfterBurned()
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


        #region Burned NFT

        {
            var executionResult = await UserTokenContractStub.Burn.SendAsync(new BurnInput()
            {
                Symbol = NftSymbol,
                Amount = 10
            });
            var log = Burned.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .First(l => l.Name == nameof(Burned))
                .NonIndexed);
            log.Amount.ShouldBe(10);
        }

        #endregion

        #region common user Cancel order

        {
            await BuyerForestContractStub.CancelOffer.SendAsync(new CancelOfferInput()
            {
                Symbol = NftSymbol,
                OfferFrom = User2Address,
                IndexList = new Int32List()
                {
                    Value = { 0 }
                }
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
            offerList.Value.Count.ShouldBe(0);
        }

        #endregion
    }    
    [Fact]
    public async void CancelOffer_Issue_8416299_Cancel_Multi_Offer()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var sellPrice = Elf(5_0000_0000);
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 1;
        var dealQuantity = 1;
        var serviceFee = dealQuantity * sellPrice.Amount * ServiceFeeRate / 10000;
        var ExpireTime0 = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(1));
        var ExpireTime1 = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(2));
        var ExpireTime2 = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(3));

        #region user buy

        {
            // user2 make offer to user1
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = offerQuantity,
                Price = offerPrice,
                ExpireTime = ExpireTime0,
            });
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = offerQuantity,
                Price = offerPrice,
                ExpireTime = ExpireTime1,
            });
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = offerQuantity,
                Price = offerPrice,
                ExpireTime = ExpireTime2,
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
            offerList.Value.Count.ShouldBe(3);
        }

        #endregion

        #region common user Cancel order

        {
            await BuyerForestContractStub.CancelOffer.SendAsync(new CancelOfferInput()
            {
                Symbol = NftSymbol,
                OfferFrom = User2Address,
                IndexList = new Int32List()
                {
                    Value = { 0, 2 }
                }
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
            offerList.Value.Count.ShouldBe(1);
            offerList.Value[0].ExpireTime.ShouldBe(ExpireTime1);
        }

        #endregion

    }
    [Fact]
    //cancel offer
    public async void CancelOffer_Case11_Allowance()
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
        
        #region cancel

        {
            var executionResult = await BuyerForestContractStub.CancelOffer.SendAsync(new CancelOfferInput()
            {
                Symbol = NftSymbol,
                OfferFrom = User2Address,
                IndexList = new Int32List()
                {
                    Value = { 0 }
                }
            });
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
            totalOfferAmount.Allowance.ShouldBe(offerPrice.Amount*offerQuantity*2);
        }
        #endregion
    }
    
}
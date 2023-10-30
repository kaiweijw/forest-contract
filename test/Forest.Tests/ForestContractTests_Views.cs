using System;
using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Forest;

public class ForestContractTests_Views : ForestContractTestBase
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
        await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
            { Spender = TokenContractAddress, Symbol = "SEED-1", Amount = 1 });

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
            /*var executionResult = await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
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
            });*/

            /*// approve contract handle ELF of buyer   
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Symbol = ElfSymbol,
                Amount = InitializeElfAmount,
                Spender = ForestContractAddress
            });*/
        }

        #endregion
    }

    [Fact]
    //case1:no approve,no MakeOffer
    public async void GetTotalOfferAmountTest_Case1()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var offerPrice = Elf(5_0000_0000);

        #region get

        {
            var totalOfferAmount = await BuyerForestContractStub.GetTotalOfferAmount.CallAsync(
                new GetTotalOfferAmountInput()
                {
                    Address = User2Address,
                    PriceSymbol = offerPrice.Symbol,
                });
            totalOfferAmount.TotalAmount.ShouldBe(0);
            totalOfferAmount.Allowance.ShouldBe(0);
        }

        #endregion
    }

    [Fact]
    //case1:have approve,no MakeOffer
    public async void GetTotalOfferAmountTest_Case2()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 2;

        #region approve

        {
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Spender = ForestContractAddress, Symbol = offerPrice.Symbol,
                Amount = offerPrice.Amount * offerQuantity + 1
            });
        }

        #endregion

        #region get

        {
            var totalOfferAmount = await BuyerForestContractStub.GetTotalOfferAmount.CallAsync(
                new GetTotalOfferAmountInput()
                {
                    Address = User2Address,
                    PriceSymbol = offerPrice.Symbol,
                });
            totalOfferAmount.TotalAmount.ShouldBe(0);
            totalOfferAmount.Allowance.ShouldBe(offerPrice.Amount * offerQuantity + 1);
        }

        #endregion
    }

    [Fact]
    //case1:have approve,have MakeOffer
    public async void GetTotalOfferAmountTest_Case3()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 2;

        #region makeOffer

        {
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Spender = ForestContractAddress, Symbol = offerPrice.Symbol,
                Amount = offerPrice.Amount * offerQuantity + 1
            });
            var executionResult = await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = offerQuantity,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });
        }

        #endregion

        #region get

        {
            var totalOfferAmount = await BuyerForestContractStub.GetTotalOfferAmount.CallAsync(
                new GetTotalOfferAmountInput()
                {
                    Address = User2Address,
                    PriceSymbol = offerPrice.Symbol,
                });
            totalOfferAmount.TotalAmount.ShouldBe(offerPrice.Amount * offerQuantity);
            totalOfferAmount.Allowance.ShouldBe(offerPrice.Amount * offerQuantity + 1);
        }

        #endregion
    }

    [Fact]
    //admin:SetOfferTotalAmount
    public async void GetTotalOfferAmountTest_Case6()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var offerPrice = Elf(5_0000_0000);
        var totalAmount = 1500;
        #region Admin set
        {
            await AdminForestContractStub.SetOfferTotalAmount.SendAsync(
                new SetOfferTotalAmountInput()
                {
                    Address = User2Address,
                    PriceSymbol = offerPrice.Symbol,
                    TotalAmount = totalAmount
                });
        }

        #endregion
        #region get
        {
            var totalOfferAmount = await BuyerForestContractStub.GetTotalOfferAmount.CallAsync(
                new GetTotalOfferAmountInput()
                {
                    Address = User2Address,
                    PriceSymbol = offerPrice.Symbol,
                });
            totalOfferAmount.TotalAmount.ShouldBe(totalAmount);
            totalOfferAmount.Allowance.ShouldBe(0);
        }

        #endregion
    }
    [Fact]
    //seller no approve,no listWithPrice
    public async void GetTotalEffectiveListedNFTAmount_Case1()
    {
        await InitializeForestContract();
        await PrepareNftData();

        #region get

        {
            var getTotalEffectiveListedNftAmount =
                (await Seller1ForestContractStub.GetTotalEffectiveListedNFTAmount.CallAsync(
                    new GetTotalEffectiveListedNFTAmountInput()
                    {
                        Symbol = NftSymbol,
                        Address = User1Address
                    }));
            getTotalEffectiveListedNftAmount.Allowance.ShouldBe(0);
            getTotalEffectiveListedNftAmount.TotalAmount.ShouldBe(0);
        }

        #endregion
    }

    [Fact]
    //seller hava approve,no listWithPrice
    public async void GetTotalEffectiveListedNFTAmount_Case2()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var allowanceQuanlity = 1;

        #region get

        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
                { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = allowanceQuanlity });
            var getTotalEffectiveListedNftAmount =
                (await Seller1ForestContractStub.GetTotalEffectiveListedNFTAmount.CallAsync(
                    new GetTotalEffectiveListedNFTAmountInput()
                    {
                        Symbol = NftSymbol,
                        Address = User1Address
                    }));
            getTotalEffectiveListedNftAmount.Allowance.ShouldBe(allowanceQuanlity);
            getTotalEffectiveListedNftAmount.TotalAmount.ShouldBe(0);
        }

        #endregion
    }

    [Fact]
    //seller hava approve,hava listWithPrice
    public async void GetTotalEffectiveListedNFTAmount_Case3()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        var sellQuantity = 3;

        #region listWithPrice

        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
                { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = sellQuantity });
            var executionResult = await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(
                new ListWithFixedPriceInput
                {
                    Symbol = NftSymbol,
                    Quantity = sellQuantity,
                    IsWhitelistAvailable = true,
                    Price = sellPrice,
                    Whitelists = new WhitelistInfoList()
                    {
                    },
                    Duration = new ListDuration
                    {
                        StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(2)),
                        DurationHours = 24
                    }
                });
        }

        #endregion

        #region get

        {
            var getTotalEffectiveListedNftAmount =
                (await Seller1ForestContractStub.GetTotalEffectiveListedNFTAmount.CallAsync(
                    new GetTotalEffectiveListedNFTAmountInput()
                    {
                        Symbol = NftSymbol,
                        Address = User1Address
                    }));
            getTotalEffectiveListedNftAmount.Allowance.ShouldBe(sellQuantity);
            getTotalEffectiveListedNftAmount.TotalAmount.ShouldBe(sellQuantity);
        }

        #endregion
    }

    [Fact]
    //makeOffer 3 times, check GetTotalOfferAmount result
    public async void GetTotalOfferAmountTest_Case4()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 2;
        var dealQuantity = 2;
        var approveQuantity = 500000000;

        #region makeOffer

        {
            // makeOffer1
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Spender = ForestContractAddress, Symbol = offerPrice.Symbol, Amount = approveQuantity * dealQuantity
            });
            var executionResult = await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = offerQuantity,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });

            //makeOffer2
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Spender = ForestContractAddress, Symbol = offerPrice.Symbol, Amount = approveQuantity * dealQuantity * 2
            });
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = offerQuantity,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });

            //makeOffer3
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Spender = ForestContractAddress, Symbol = offerPrice.Symbol,
                Amount = approveQuantity * dealQuantity * 3L
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
            var offerList = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
            {
                Symbol = NftSymbol,
                Address = User2Address,
            }).Result.Output;
            offerList.Value.Count.ShouldBe(2);

            offerList = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
            {
                Symbol = NftSymbol2,
                Address = User2Address,
            }).Result.Output;
            offerList.Value.Count.ShouldBe(1);

            //query view:GetTotalOfferAmountTest
            var totalOfferAmount = await BuyerForestContractStub.GetTotalOfferAmount.CallAsync(
                new GetTotalOfferAmountInput()
                {
                    Address = User2Address,
                    PriceSymbol = offerPrice.Symbol,
                });
            totalOfferAmount.TotalAmount.ShouldBe(offerPrice.Amount * offerQuantity * 3);
            totalOfferAmount.Allowance.ShouldBe(approveQuantity * dealQuantity * 3L);
        }

        #endregion
    }

    [Fact]
    //cancel offer
    public async void GetTotalOfferAmountTest_Case5()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 2;
        var dealQuantity = 2;
        var approveQuantity = 500000000;

        #region makeOffer

        {
            // makeOffer1
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Spender = ForestContractAddress, Symbol = offerPrice.Symbol, Amount = approveQuantity * dealQuantity
            });
            var executionResult = await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = offerQuantity,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });

            //makeOffer2
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Spender = ForestContractAddress, Symbol = offerPrice.Symbol, Amount = approveQuantity * dealQuantity * 2
            });
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = offerQuantity,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });

            //makeOffer3
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Spender = ForestContractAddress, Symbol = offerPrice.Symbol,
                Amount = approveQuantity * dealQuantity * 3L
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
            var offerList = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
            {
                Symbol = NftSymbol,
                Address = User2Address,
            }).Result.Output;
            offerList.Value.Count.ShouldBe(2);

            offerList = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
            {
                Symbol = NftSymbol2,
                Address = User2Address,
            }).Result.Output;
            offerList.Value.Count.ShouldBe(1);

            //query view:GetTotalOfferAmountTest
            var totalOfferAmount = await BuyerForestContractStub.GetTotalOfferAmount.CallAsync(
                new GetTotalOfferAmountInput()
                {
                    Address = User2Address,
                    PriceSymbol = offerPrice.Symbol,
                });
            totalOfferAmount.TotalAmount.ShouldBe(offerPrice.Amount * offerQuantity * 3);
            totalOfferAmount.Allowance.ShouldBe(approveQuantity * dealQuantity * 3L);
        }

        #endregion

        #region cancel offer

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
            offerList.Value.Count.ShouldBe(1);

            offerList = BuyerForestContractStub.GetOfferList.SendAsync(new GetOfferListInput()
            {
                Symbol = NftSymbol2,
                Address = User2Address,
            }).Result.Output;
            offerList.Value.Count.ShouldBe(1);

            //query view:GetTotalOfferAmountTest
            var totalOfferAmount = await BuyerForestContractStub.GetTotalOfferAmount.CallAsync(
                new GetTotalOfferAmountInput()
                {
                    Address = User2Address,
                    PriceSymbol = offerPrice.Symbol,
                });
            totalOfferAmount.TotalAmount.ShouldBe(offerPrice.Amount * offerQuantity * 2);
            totalOfferAmount.Allowance.ShouldBe(approveQuantity * dealQuantity * 3L);
        }

        #endregion
    }

    [Fact]
    public async void GetTotalEffectiveListedNFTAmount_Case4()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        var sellQuantity = 2;

        #region listWithPrice

        {
            //list 1
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
                { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = sellQuantity });
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(
                new ListWithFixedPriceInput
                {
                    Symbol = NftSymbol,
                    Quantity = sellQuantity,
                    IsWhitelistAvailable = true,
                    Price = sellPrice,
                    Whitelists = new WhitelistInfoList()
                    {
                    },
                    Duration = new ListDuration
                    {
                        StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(1)),
                        DurationHours = 24
                    }
                });

            await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
                { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = sellQuantity * 2 });
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(
                new ListWithFixedPriceInput
                {
                    Symbol = NftSymbol,
                    Quantity = 2,
                    IsWhitelistAvailable = true,
                    Price = sellPrice,
                    Whitelists = new WhitelistInfoList()
                    {
                    },
                    Duration = new ListDuration
                    {
                        StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(2)),
                        DurationHours = 24
                    }
                });
        }

        #endregion

        {
            // query check
            var getTotalEffectiveListedNftAmount =
                (await Seller1ForestContractStub.GetTotalEffectiveListedNFTAmount.CallAsync(
                    new GetTotalEffectiveListedNFTAmountInput()
                    {
                        Symbol = NftSymbol,
                        Address = User1Address
                    }));
            getTotalEffectiveListedNftAmount.Allowance.ShouldBe(sellQuantity * 2);
            getTotalEffectiveListedNftAmount.TotalAmount.ShouldBe(sellQuantity * 2);
        }
    }

    [Fact]
    public async void GetTotalEffectiveListedNFTAmount_Case5()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        var sellQuantity = 2;

        #region listWithPrice

        {
            var errorMessgae = "";
            try
            {
                await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(
                    new ListWithFixedPriceInput
                    {
                        Symbol = NftSymbol,
                        Quantity = sellQuantity,
                        IsWhitelistAvailable = true,
                        Price = sellPrice,
                        Whitelists = new WhitelistInfoList()
                        {
                        },
                        Duration = new ListDuration
                        {
                            DurationHours = 24
                        }
                    });
            }
            catch (Exception e)
            {
                errorMessgae = e.Message;
            }

            errorMessgae.ShouldContain("Operation failed. The seller");

            var listedNftInfo = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
                new GetListedNFTInfoListInput
                {
                    Symbol = NftSymbol,
                    Owner = User1Address
                }));
            listedNftInfo.Value.Count.ShouldBe(0);

            await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
                { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = sellQuantity });
            try
            {
                await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(
                    new ListWithFixedPriceInput
                    {
                        Symbol = NftSymbol,
                        Quantity = sellQuantity,
                        IsWhitelistAvailable = true,
                        Price = sellPrice,
                        Whitelists = new WhitelistInfoList()
                        {
                        },
                        Duration = new ListDuration
                        {
                            DurationHours = 24
                        }
                    });
            }
            catch (Exception e)
            {
                e.Message.ShouldContain("Operation failed. The seller");
            }
        }

        #endregion

        #region query

        {
            // query check
            var getTotalEffectiveListedNftAmount =
                (await Seller1ForestContractStub.GetTotalEffectiveListedNFTAmount.CallAsync(
                    new GetTotalEffectiveListedNFTAmountInput()
                    {
                        Symbol = NftSymbol,
                        Address = User1Address
                    }));
            getTotalEffectiveListedNftAmount.Allowance.ShouldBe(sellQuantity);
            getTotalEffectiveListedNftAmount.TotalAmount.ShouldBe(sellQuantity);
        }

        #endregion
    }


    [Fact]
    public async void GetTotalEffectiveListedNFTAmount_Case6()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        var sellQuantity = 2;

        #region listWithPrice

        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
                { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = sellQuantity });
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(
                new ListWithFixedPriceInput
                {
                    Symbol = NftSymbol,
                    Quantity = sellQuantity,
                    IsWhitelistAvailable = true,
                    Price = sellPrice,
                    Whitelists = new WhitelistInfoList()
                    {
                    },
                    Duration = new ListDuration
                    {
                        DurationHours = 24
                    }
                });
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
                { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = sellQuantity * 2 });
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(
                new ListWithFixedPriceInput
                {
                    Symbol = NftSymbol,
                    Quantity = sellQuantity,
                    IsWhitelistAvailable = true,
                    Price = sellPrice,
                    Whitelists = new WhitelistInfoList()
                    {
                    },
                    Duration = new ListDuration
                    {
                        StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
                        DurationHours = 24
                    }
                });
        }

        #endregion

        #region query

        {
            //query list
            var listedNftInfo = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
                new GetListedNFTInfoListInput
                {
                    Symbol = NftSymbol,
                    Owner = User1Address
                }));
            listedNftInfo.Value.Count.ShouldBe(2);

            // query check
            var getTotalEffectiveListedNftAmount =
                (await Seller1ForestContractStub.GetTotalEffectiveListedNFTAmount.CallAsync(
                    new GetTotalEffectiveListedNFTAmountInput()
                    {
                        Symbol = NftSymbol,
                        Address = User1Address
                    }));
            getTotalEffectiveListedNftAmount.Allowance.ShouldBe(sellQuantity * 2);
            getTotalEffectiveListedNftAmount.TotalAmount.ShouldBe(sellQuantity * 2);
        }

        #endregion

        #region Delist

        {
            await Seller1ForestContractStub.Delist.SendAsync(new DelistInput
            {
                Symbol = NftSymbol,
                Price = sellPrice,
                Quantity = sellQuantity
            });
        }

        #endregion

        #region query

        {
            //query list
            var listedNftInfo = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
                new GetListedNFTInfoListInput
                {
                    Symbol = NftSymbol,
                    Owner = User1Address
                }));
            listedNftInfo.Value.Count.ShouldBe(1);

            // query check
            var getTotalEffectiveListedNftAmount =
                (await Seller1ForestContractStub.GetTotalEffectiveListedNFTAmount.CallAsync(
                    new GetTotalEffectiveListedNFTAmountInput()
                    {
                        Symbol = NftSymbol,
                        Address = User1Address
                    }));
            getTotalEffectiveListedNftAmount.Allowance.ShouldBe(sellQuantity*2);
            getTotalEffectiveListedNftAmount.TotalAmount.ShouldBe(sellQuantity);
        }

        #endregion
    }
    
     [Fact]
    //allowance=0, totalAmount>0
    public async void GetTotalOfferAmount_Case7()
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
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = offerPrice.Symbol, Amount = offerPrice.Amount*offerQuantity });

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
            totalOfferAmount.Allowance.ShouldBe(0);
        }
        #endregion
    }
     [Fact]
    public async void GetTotalEffectiveListedNFTAmount_Case8()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        var sellQuantity = 2;

        #region listWithPrice

        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
                { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = sellQuantity });
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(
                new ListWithFixedPriceInput
                {
                    Symbol = NftSymbol,
                    Quantity = sellQuantity,
                    IsWhitelistAvailable = true,
                    Price = sellPrice,
                    Whitelists = new WhitelistInfoList()
                    {
                    },
                    Duration = new ListDuration
                    {
                        DurationHours = 24
                    }
                });
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
                { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = sellQuantity * 2 });
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(
                new ListWithFixedPriceInput
                {
                    Symbol = NftSymbol,
                    Quantity = sellQuantity,
                    IsWhitelistAvailable = true,
                    Price = sellPrice,
                    Whitelists = new WhitelistInfoList()
                    {
                    },
                    Duration = new ListDuration
                    {
                        StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
                        DurationHours = 24
                    }
                });
        }

        #endregion

        #region query

        {
            //query list
            var listedNftInfo = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
                new GetListedNFTInfoListInput
                {
                    Symbol = NftSymbol,
                    Owner = User1Address
                }));
            listedNftInfo.Value.Count.ShouldBe(2);

            // query check
            var getTotalEffectiveListedNftAmount =
                (await Seller1ForestContractStub.GetTotalEffectiveListedNFTAmount.CallAsync(
                    new GetTotalEffectiveListedNFTAmountInput()
                    {
                        Symbol = NftSymbol,
                        Address = User1Address
                    }));
            getTotalEffectiveListedNftAmount.Allowance.ShouldBe(sellQuantity * 2);
            getTotalEffectiveListedNftAmount.TotalAmount.ShouldBe(sellQuantity * 2);
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
                { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = sellQuantity  });
        }

        #endregion

        #region user buy

        {
            // user2 make offer to user1
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = sellPrice.Symbol, Amount = sellPrice.Amount*sellQuantity });
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = sellQuantity,
                Price = sellPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });
        }

        #endregion

        #region query

        {
            //query list
            var listedNftInfo = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
                new GetListedNFTInfoListInput
                {
                    Symbol = NftSymbol,
                    Owner = User1Address
                }));
            listedNftInfo.Value.Count.ShouldBe(1);

            // query check
            var getTotalEffectiveListedNftAmount =
                (await Seller1ForestContractStub.GetTotalEffectiveListedNFTAmount.CallAsync(
                    new GetTotalEffectiveListedNFTAmountInput()
                    {
                        Symbol = NftSymbol,
                        Address = User1Address
                    }));
            getTotalEffectiveListedNftAmount.Allowance.ShouldBe(0);
            getTotalEffectiveListedNftAmount.TotalAmount.ShouldBe(sellQuantity);
        }
        #endregion
        
    }
    
}
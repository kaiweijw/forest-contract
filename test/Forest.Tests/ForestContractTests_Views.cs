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
    public async void GetTotalEffectiveOfferAmountTest_Case1()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 2;
        var dealQuantity = 2;
        var approveQuantity = 500000000;

        #region user buy
        {
            // user2 make offer VALID--first
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = offerPrice.Symbol, Amount = approveQuantity*dealQuantity });
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
            // user2 make offer EXPIRE1--senond
            //approve
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = offerPrice.Symbol, Amount = approveQuantity*dealQuantity*2 });
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = offerQuantity,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
            });

            // user2 make offer EXPIRE2--third
            //approve
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = offerPrice.Symbol, Amount = approveQuantity*dealQuantity*3L });
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
            offerList.Value.Count.ShouldBe(3);
            
            //query view:GetTotalEffectiveOfferAmountTest
            var totalOfferAmount = await BuyerForestContractStub.GetTotalOfferAmount.CallAsync(new GetTotalOfferAmountInput()
            {
                Address = User2Address,
                PriceSymbol = offerPrice.Symbol,
            });
            totalOfferAmount.TotalAmount.ShouldBe(offerPrice.Amount*offerQuantity*3);
            totalOfferAmount.Allowance.ShouldBe(approveQuantity*dealQuantity*3L);
        }
        #endregion
    }
    
    [Fact]
    public async void GetTotalEffectiveOfferAmountTest_Case2()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 2;
        var dealQuantity = 2;
        var approveQuantity = 500000000;

        #region user buy
        {
            // user2 make offer VALID--first
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = offerPrice.Symbol, Amount = approveQuantity*dealQuantity });
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
            // user2 make offer EXPIRE1--senond
            //approve
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = offerPrice.Symbol, Amount = approveQuantity*dealQuantity*2 });
            await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = offerQuantity,
                Price = offerPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-5)),
            });

            // user2 make offer EXPIRE2--third
            //approve
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = offerPrice.Symbol, Amount = approveQuantity*dealQuantity*3L });
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
            
            //query view:GetTotalEffectiveOfferAmountTest
            var totalEffectiveOfferAmount = await BuyerForestContractStub.GetTotalOfferAmount.CallAsync(new GetTotalOfferAmountInput()
            {
                Address = User2Address,
                PriceSymbol = offerPrice.Symbol,
            });
            //contain expire offer 
            totalEffectiveOfferAmount.TotalAmount.ShouldBe(offerPrice.Amount*offerQuantity*3L);
            totalEffectiveOfferAmount.Allowance.ShouldBe(approveQuantity*dealQuantity*3L);
        }
        #endregion
        
    }
    
    [Fact]
    public async void GetTotalEffectiveOfferAmountTest_Case3()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 2;
        var dealQuantity = 2;
        var approveQuantity = 500000000;

        #region user buy
        {
            // Insufficient allowance case
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput() {Spender = ForestContractAddress, Symbol = offerPrice.Symbol, Amount = 1 });
            IExecutionResult<Empty> executionResult;
            try
            {
                executionResult = await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
                {
                    Symbol = NftSymbol,
                    OfferTo = User1Address,
                    Quantity = offerQuantity,
                    Price = offerPrice,
                    ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
                });
            }catch(Exception e)
            {
                e.Message.ShouldContain("Insufficient allowance");
            }

            // user2 make offer
            {
                await User2TokenContractStub.Approve.SendAsync(new ApproveInput() {Spender = ForestContractAddress, Symbol = offerPrice.Symbol, Amount = approveQuantity * dealQuantity });}
                executionResult = await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
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
            }
        //invalid param case
        {
            //Insufficient param
            try
            {
                //query view:GetTotalEffectiveOfferAmountTest
                var totalEffectiveOfferAmount = await BuyerForestContractStub.GetTotalOfferAmount.CallAsync(new GetTotalOfferAmountInput()
                {
                    //Address = User2Address,
                    PriceSymbol = offerPrice.Symbol,
                });
            }catch(Exception e)
            {
                e.Message.ShouldContain("Invalid param Address");
            }
        }

        #endregion
    }

    [Fact]
    public async void GetTotalEffectiveListedNFTAmount_Case1()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        //list 1
        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() {Spender = ForestContractAddress, Symbol = NftSymbol, Amount = 2 });
            var executionResult1 = await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(
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
                        StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(1)),
                        DurationHours = 24
                    }
                });
            var log1 = ListedNFTAdded.Parser
                .ParseFrom(executionResult1.TransactionResult.Logs.First(l => l.Name == nameof(ListedNFTAdded))
                    .NonIndexed);
            log1.Owner.ShouldBe(User1Address);
            log1.Quantity.ShouldBe(2);
            log1.Symbol.ShouldBe(NftSymbol);
            log1.Price.Symbol.ShouldBe(ElfSymbol);
            log1.Price.Amount.ShouldBe(3);
            log1.Duration.StartTime.ShouldNotBeNull();
            log1.Duration.DurationHours.ShouldBe(24);
            
            var lo2 = FixedPriceNFTListed.Parser
                .ParseFrom(executionResult1.TransactionResult.Logs.First(l => l.Name == nameof(FixedPriceNFTListed))
                    .NonIndexed);
            lo2.Owner.ShouldBe(User1Address);
            lo2.Quantity.ShouldBe(2);
            lo2.Symbol.ShouldBe(NftSymbol);
            lo2.Price.Symbol.ShouldBe(ElfSymbol);
            lo2.Price.Amount.ShouldBe(3);
            lo2.Duration.StartTime.ShouldNotBeNull();
            lo2.Duration.DurationHours.ShouldBe(24);
        }
        //list 2
        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() {Spender = ForestContractAddress, Symbol = NftSymbol, Amount = 4 });
            var executionResult1 = await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(
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
        // query check
        {
            var getTotalEffectiveListedNftAmount =
                (await Seller1ForestContractStub.GetTotalEffectiveListedNFTAmount.CallAsync(
                    new GetTotalEffectiveListedNFTAmountInput()
                    {
                        Symbol = NftSymbol,
                        Address = User1Address
                    }));
            getTotalEffectiveListedNftAmount.Allowance.ShouldBe(4);
            getTotalEffectiveListedNftAmount.TotalAmount.ShouldBe(4);
        }
    }
    
    [Fact]
    public async void GetTotalEffectiveListedNFTAmount_Case2()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() {Spender = ForestContractAddress, Symbol = NftSymbol, Amount = 1 });
            try
            {
                var executionResult1 = await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(
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
                            DurationHours = 24
                        }
                    });
            }
            catch (Exception e)
            {
                e.Message.ShouldContain("Insufficient allowance of "+NftSymbol);
            }

            var listedNftInfo = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
                new GetListedNFTInfoListInput
                {
                    Symbol = NftSymbol,
                    Owner = User1Address
                }));
           listedNftInfo.Value.Count.ShouldBe(0);
           
           await UserTokenContractStub.Approve.SendAsync(new ApproveInput() {Spender = ForestContractAddress, Symbol = NftSymbol, Amount = 2 });
           try
           {
               var executionResult1 = await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(
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
                           DurationHours = 24
                       }
                   });
           }
           catch (Exception e)
           {
               e.Message.ShouldContain("Insufficient allowance of "+NftSymbol);
           }
           listedNftInfo = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
               new GetListedNFTInfoListInput
               {
                   Symbol = NftSymbol,
                   Owner = User1Address
               }));
           listedNftInfo.Value.Count.ShouldBe(1);
        }
    }
}
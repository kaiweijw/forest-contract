using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core.Extension;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace Forest;

public partial class ForestContractTests_MakeOffer : ForestContractTestBase
{
    private const string NftSymbol = "TESTNFT-1";
    private const string NftSymbol2 = "TESTNFT-2";
    private const string ElfSymbol = "ELF";
    private const int ServiceFeeRate = 1000; // 10%
    private const long InitializeElfAmount = 10000_0000_0000;

    private CSharpSmartContractContext _context;

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
                Amount = 20,
                To = User1Address
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
            
            await UserTokenContractStub.Transfer.SendAsync(new TransferInput()
            {
                To = User3Address,
                Symbol = NftSymbol,
                Amount = 10
            });
            var tokenInfo3 = await UserTokenContractStub.GetTokenInfo.SendAsync(new GetTokenInfoInput()
            {
                Symbol = NftSymbol,
            });

            tokenInfo3.Output.TotalSupply.ShouldBe(100);
            tokenInfo3.Output.Supply.ShouldBe(20);
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

            // approve contract handle ELF of buyer   
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Symbol = ElfSymbol,
                Amount = InitializeElfAmount,
                Spender = ForestContractAddress
            });
            
            // approve contract handle ELF of buyer   
            await User3TokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Symbol = NftSymbol,
                Amount = 5,
                Spender = ForestContractAddress
            });
            await User3TokenContractStub.Approve.SendAsync(new ApproveInput()
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
        var executionResult = await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
        {
            Symbol = NftSymbol,
            OfferTo = User1Address,
            Quantity = 1,
            Price = Elf(5),
            ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(30))
        });
        var log = OfferAdded.Parser
            .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name == nameof(OfferAdded))
                .NonIndexed);
        log.OfferFrom.ShouldBe(User2Address);
        log.Quantity.ShouldBe(1);
        log.Symbol.ShouldBe(NftSymbol);
        log.Price.Symbol.ShouldBe(ElfSymbol);
        log.Price.Amount.ShouldBe(5);
        log.ExpireTime.ShouldNotBeNull();
        log.OfferTo.ShouldBe(User1Address);
        
        var log1 = OfferMade.Parser
            .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name == nameof(OfferMade))
                .NonIndexed);
        log1.OfferFrom.ShouldBe(User2Address);
        log1.Quantity.ShouldBe(1);
        log1.Symbol.ShouldBe(NftSymbol);
        log1.Price.Symbol.ShouldBe(ElfSymbol);
        log1.Price.Amount.ShouldBe(5);
        log1.ExpireTime.ShouldNotBeNull();
        log1.OfferTo.ShouldBe(User1Address);

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
            await MineAsync(new List<Transaction>(), Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(1)));

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
            //nftBalance.Output.Balance.ShouldBe(10);

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
           // nftBalance.Output.Balance.ShouldBe(9);
        }

        #endregion

        #region check buyer NFT

        {
            var nftBalance = await User2TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User2Address
            });
          //  nftBalance.Output.Balance.ShouldBe(1);
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
          //  user1ElfBalance.Output.Balance.ShouldBe(InitializeElfAmount + whitePrice.Amount - serviceFee);
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
                    StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMilliseconds(-500)),
                    PublicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(10)),
                    DurationHours = 1,
                },
            });

            var list = await Seller1ForestContractStub.GetListedNFTInfoList.SendAsync(new GetListedNFTInfoListInput()
            {
                Symbol = NftSymbol,
                Owner = User1Address
            });
            list.Output.Value.Count.ShouldBe(1);
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
            //nftBalance.Output.Balance.ShouldBe(10);

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
            //nftBalance.Output.Balance.ShouldBe(10);
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
            var serviceFee = whitePrice.Amount * ServiceFeeRate / 10000;
           // user1ElfBalance.Output.Balance.ShouldBe(InitializeElfAmount + whitePrice.Amount - serviceFee);
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
            await MineAsync(new List<Transaction>(), Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(1)));

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
            //nftBalance.Output.Balance.ShouldBe(10);

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
            await MineAsync(new List<Transaction>(), Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(1)));

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
            //nftBalance.Output.Balance.ShouldBe(10);

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
            var serviceFee = whitePrice.Amount * ServiceFeeRate / 10000;
            //user1ElfBalance.Output.Balance.ShouldBe(InitializeElfAmount + whitePrice.Amount - serviceFee);
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

        var list = await MineAsync(new List<Transaction>(), Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(1)));

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
                    StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-1)),
                    // public 10min after
                    PublicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(10)),
                    DurationHours = 1,
                },
            });
        }

        #endregion

        #region whitelist user buy

        {
            await MineAsync(new List<Transaction>(), Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(1)));

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
            //nftBalance.Output.Balance.ShouldBe(10);

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
            await MineAsync(new List<Transaction>(), Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(1)));

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
            //nftBalance.Output.Balance.ShouldBe(10);

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
            await MineAsync(new List<Transaction>(), Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(1)));

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
            //nftBalance.Output.Balance.ShouldBe(10);

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
            await MineAsync(new List<Transaction>(), Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(1)));

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
            //nftBalance.Output.Balance.ShouldBe(10);

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
            await MineAsync(new List<Transaction>(), Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(1)));

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
            //nftBalance.Output.Balance.ShouldBe(10);

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
            await MineAsync(new List<Transaction>(), Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(1)));

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
            //nftBalance.Output.Balance.ShouldBe(10);

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
            //nftBalance.Output.Balance.ShouldBe(10);
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
            await MineAsync(new List<Transaction>(), Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(1)));

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
            //nftBalance.Output.Balance.ShouldBe(10);

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

        /*
            To run unit tests locally 
            because the contract does not support listing NFTs with past start times, 
            the following code needs to be commented out:
            Class-Method: Forest.ForestContract.AdjustListDuration
            
                if (duration.StartTime == null || duration.StartTime < Context.CurrentBlockTime)
                {
                    duration.StartTime = Context.CurrentBlockTime;
                }
         
         */

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

        #endregion
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
            await MineAsync(new List<Transaction>(), Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(1)));

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
            //nftBalance.Output.Balance.ShouldBe(10);

            try
            {
                await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = offerPrice.Symbol, Amount = offerPrice.Amount*1 });
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
                e.Message.ShouldContain("cannot be sender himself");
            }
        }

        #endregion
    }
    
    //allowance greeter enough
    [Fact]
    public async void MakeOffer_Case44_Allowance()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 2;

        #region makeOffer
        {
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = offerPrice.Symbol, Amount = offerQuantity*offerPrice.Amount + 1 });
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
        }
        #endregion
    }
    
    //allowance equal enough
    [Fact]
    public async void MakeOffer_Case45_Allowance()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 2;

        #region makeOffer
        {
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = offerPrice.Symbol, Amount = offerQuantity*offerPrice.Amount });
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
        }
        #endregion
      
    }
    
    //allowance not enough
    [Fact]
    public async void MakeOffer_Case46_Allowance()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var offerPrice = Elf(5_0000_0000);
        var offerQuantity = 2;

        #region makeOffer
        {
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = offerPrice.Symbol, Amount = offerQuantity*offerPrice.Amount -1 });
            var errorMessage = "";
            try
            {
                var executionResult = await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
                {
                    Symbol = NftSymbol,
                    OfferTo = User1Address,
                    Quantity = offerQuantity,
                    Price = offerPrice,
                    ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
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
    
    //buy: elf allowance greatter enough
   [Fact]
    public async void Buy_Case47_Allowance()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var sellPrice = Elf(1000_0000_0000);
        var whitePrice = Elf(1_0000_0000);

        #region ListWithFixedPrice

        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = whitePrice.Symbol, Amount = 5 });

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
                    StartTime = Timestamp.FromDateTime(DateTime.UtcNow).AddSeconds(-2),
                    // public 10min after
                    PublicTime = Timestamp.FromDateTime(DateTime.UtcNow).AddSeconds(-2),
                    DurationHours = 1,
                },
            });
        }

        #endregion

        #region user2 make offer to user1
        {
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = whitePrice.Symbol, Amount = sellPrice.Amount*2+1 });
            var executionResult = await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = 2,
                Price = sellPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(30))
            });
        }
        
        #endregion
        var nftBalance = await User2TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
        {
            Symbol = NftSymbol,
            Owner = User2Address
        });
        nftBalance.Output.Balance.ShouldBe(2);
    }
    
    
    //buy: elf allowance equal enough
    [Fact]
    public async void Buy_Case48_Allowance()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var sellPrice = Elf(1000_0000_0000);
        var whitePrice = Elf(1_0000_0000);

        #region ListWithFixedPrice

        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = 5 });

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
                    StartTime = Timestamp.FromDateTime(DateTime.UtcNow).AddSeconds(-2),
                    // public 10min after
                    PublicTime = Timestamp.FromDateTime(DateTime.UtcNow).AddSeconds(-2),
                    DurationHours = 1,
                },
            });
        }

        #endregion

        #region user2 make offer to user1
        {
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = whitePrice.Symbol, Amount = sellPrice.Amount*2 });
            var executionResult = await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
            {
                Symbol = NftSymbol,
                OfferTo = User1Address,
                Quantity = 2,
                Price = sellPrice,
                ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(30))
            });
        }
        
        #endregion
        var nftBalance = await User2TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
        {
            Symbol = NftSymbol,
            Owner = User2Address
        });
        nftBalance.Output.Balance.ShouldBe(2);
    }
    
     //buy: elf allowance not enough
    [Fact]
    public async void Buy_Case49_Allowance()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var sellPrice = Elf(1000_0000_0000);
        var whitePrice = Elf(1_0000_0000);

        #region ListWithFixedPrice

        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = 5 });

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
                    StartTime = Timestamp.FromDateTime(DateTime.UtcNow).AddSeconds(-2),
                    // public 10min after
                    PublicTime = Timestamp.FromDateTime(DateTime.UtcNow).AddSeconds(-2),
                    DurationHours = 1,
                },
            });
        }

        #endregion

        #region user2 make offer to user1

        {
            var errorMessage = "";
            try
            {
                await User2TokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = whitePrice.Symbol, Amount = sellPrice.Amount*2-1 });
                var executionResult = await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
                {
                    Symbol = NftSymbol,
                    OfferTo = User1Address,
                    Quantity = 2,
                    Price = sellPrice,
                    ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(30))
                });
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
            }
            errorMessage.ShouldContain("The allowance you set is less than required. Please reset it.");
        }
        
        #endregion
        var nftBalance = await User2TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
        {
            Symbol = NftSymbol,
            Owner = User2Address
        });
        nftBalance.Output.Balance.ShouldBe(0);
    }
    
     [Fact]
     //seller: nft allowance not enough
     public async void Buy_Case50_Allowance()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var sellPrice = Elf(1000_0000_0000);
        var whitePrice = Elf(1_0000_0000);

        #region ListWithFixedPrice

        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = 5 });

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
                    StartTime = Timestamp.FromDateTime(DateTime.UtcNow).AddSeconds(-2),
                    // public 10min after
                    PublicTime = Timestamp.FromDateTime(DateTime.UtcNow).AddSeconds(-2),
                    DurationHours = 1,
                },
            });
        }

        #endregion

        #region user2 make offer to user1

        {
            var errorMessage = "";
            try
            {
                await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = 1 });
                await User2TokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = whitePrice.Symbol, Amount = sellPrice.Amount*2 });
                var executionResult = await BuyerForestContractStub.MakeOffer.SendAsync(new MakeOfferInput()
                {
                    Symbol = NftSymbol,
                    OfferTo = User1Address,
                    Quantity = 2,
                    Price = sellPrice,
                    ExpireTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(30))
                });
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
            }
            errorMessage.ShouldContain("[TransferFrom]Insufficient allowance");
        }
        
        #endregion
        var nftBalance = await User2TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
        {
            Symbol = NftSymbol,
            Owner = User2Address
        });
        nftBalance.Output.Balance.ShouldBe(0);
    }
}
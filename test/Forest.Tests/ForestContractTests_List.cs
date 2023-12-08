using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core.Extension;
using Forest.Whitelist;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;
using IssueInput = AElf.Contracts.MultiToken.IssueInput;

namespace Forest;

public class ForestContractListTests : ForestContractTestBase
{
    private const string NftSymbol = "TESTNFT-1";
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

    private static Price Elf(long amount)
    {
        return new Price()
        {
            Symbol = ElfSymbol,
            Amount = amount
        };
    }

    private async Task PrepareNftData()
    {

        await CreateSeedCollection();
        await CreateSeed("SEED-1", "TESTNFT-0");
        await TokenContractStub.Issue.SendAsync(new IssueInput() { Symbol = "SEED-1", To = User1Address, Amount = 1 });
        await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = TokenContractAddress, Symbol = "SEED-1", Amount = 1 });
        
        // create collections via MULTI-TOKEN-CONTRACT
        var executionResult = await UserTokenContractStub.Create.SendAsync(new CreateInput
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
        var log = TokenCreated.Parser
            .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name == nameof(TokenCreated))
                .NonIndexed);
        log.Symbol.ShouldBe("TESTNFT-0");
        log.Decimals.ShouldBe(0);
        log.TotalSupply.ShouldBe(100);
        log.TokenName.ShouldBe("TESTNFT—collection");
        log.Issuer.ShouldBe(User1Address);
        log.IsBurnable.ShouldBe(false);
        log.IssueChainId.ShouldBe(9992731);

        // create NFT via MULTI-TOKEN-CONTRACT
        var executionResult1 = await UserTokenContractStub.Create.SendAsync(new CreateInput
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
        var log1 = TokenCreated.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.First(l => l.Name == nameof(TokenCreated))
                .NonIndexed);
        log1.Symbol.ShouldBe(NftSymbol);
        log1.Decimals.ShouldBe(0);
        log1.TotalSupply.ShouldBe(100);
        log1.TokenName.ShouldBe(NftSymbol);
        log1.Issuer.ShouldBe(User1Address);
        log1.IsBurnable.ShouldBe(false);
        log1.IssueChainId.ShouldBe(9992731);

        // issue 10 NFTs to self
        var executionResult2 = await UserTokenContractStub.Issue.SendAsync(new IssueInput()
        {
            Symbol = NftSymbol,
            Amount = 10,
            To = User1Address
        });
        var log2 = Issued.Parser
            .ParseFrom(executionResult2.TransactionResult.Logs.First(l => l.Name == nameof(Issued))
                .NonIndexed);
        log2.Symbol.ShouldBe(NftSymbol);
        log2.Amount.ShouldBe(10);
        log2.Memo.ShouldBe("");
        log2.To.ShouldBe(User1Address);
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


        // transfer thousand ELF to seller
        var executionResult3 =  await TokenContractStub.Transfer.SendAsync(new TransferInput()
        {
            To = User1Address,
            Symbol = ElfSymbol,
            Amount = 9999999
        });
        var log3 = Transferred.Parser
            .ParseFrom(executionResult3.TransactionResult.Logs.First(l => l.Name == nameof(Transferred))
                .NonIndexed); 
        log3.Amount.ShouldBe(9999999);
        
        // transfer thousand ELF to buyer
        await TokenContractStub.Transfer.SendAsync(new TransferInput()
        {
            To = User2Address,
            Symbol = ElfSymbol,
            Amount = 9999999
        });
    }

    [Fact]
    public async void ListWithFixedPrice1Test()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        var whitePrice = Elf(3);
        var listQuantity = 2;
        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = listQuantity });
            var executionResult1 = await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(
                new ListWithFixedPriceInput
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
                                    Value = { User2Address, User3Address },
                                }
                            },
                        }
                    },
                    Duration = new ListDuration
                    {
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
            

            var listedNftInfo = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
                new GetListedNFTInfoListInput
                {
                    Symbol = NftSymbol,
                    Owner = User1Address
                })).Value.First();
            listedNftInfo.Price.Symbol.ShouldBe("ELF");
            listedNftInfo.Price.Amount.ShouldBe(3);
            listedNftInfo.Quantity.ShouldBe(2);
            listedNftInfo.ListType.ShouldBe(ListType.FixedPrice);
            listedNftInfo.Duration.StartTime.ShouldNotBeNull();
            listedNftInfo.Duration.DurationHours.ShouldBe(24);
        }

        //GetWhitelistId
        var whitelistId = (await Seller1ForestContractStub.GetWhitelistId.CallAsync(new GetWhitelistIdInput()
        {
            Symbol = NftSymbol,
            Owner = User1Address
        })).WhitelistId;
        var whitelistPrice = await WhitelistContractStub.GetExtraInfoByAddress.CallAsync(
            new GetExtraInfoByAddressInput
            {
                Address = User2Address,
                WhitelistId = whitelistId
            });
        whitelistPrice.TagName.ShouldBe("WHITELIST_TAG");

        {
            var whitelistInfo = await WhitelistContractStub.GetWhitelist.CallAsync(whitelistId);
            whitelistInfo.ExtraInfoIdList.Value.Single().AddressList.Value.Count.ShouldBe(2);
        }
    }

    [Fact]
    public async void ListWithFixedPrice2Test()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        var whitePrice = Elf(3);
        var listQuantity = 2;
        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = listQuantity });

            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = NftSymbol,
                Quantity = listQuantity,
                IsWhitelistAvailable = false,
                Price = sellPrice,
                Whitelists = null,
                Duration = new ListDuration
                {
                    DurationHours = 24
                }
            });

            var listedNftInfo = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
                new GetListedNFTInfoListInput
                {
                    Symbol = NftSymbol,
                    Owner = User1Address
                })).Value.First();
            listedNftInfo.Price.Symbol.ShouldBe("ELF");
            listedNftInfo.Price.Amount.ShouldBe(3);
            listedNftInfo.Quantity.ShouldBe(2);
            listedNftInfo.ListType.ShouldBe(ListType.FixedPrice);
            listedNftInfo.Duration.StartTime.ShouldNotBeNull();
            listedNftInfo.Duration.DurationHours.ShouldBe(24);
        }

        //GetWhitelistId
        Func<Task> act = () => Seller1ForestContractStub.GetWhitelistId.CallAsync(new GetWhitelistIdInput()
        {
            Symbol = NftSymbol,
            Owner = User1Address
        });
        var exception = await Assert.ThrowsAsync<Exception>(act);
        exception.Message.ShouldContain("Failed to call GetWhitelistId");
    }

    [Fact]
    public async void ListWithFixedPrice3Test()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        var whitePrice = Elf(3);
        var listQuantity = 2;
        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = listQuantity });

            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = NftSymbol,
                Quantity = listQuantity,
                IsWhitelistAvailable = false,
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
                Duration = new ListDuration
                {
                    DurationHours = 24
                }
            });

            var listedNftInfo = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
                new GetListedNFTInfoListInput
                {
                    Symbol = NftSymbol,
                    Owner = User1Address
                })).Value.First();
            listedNftInfo.Price.Symbol.ShouldBe("ELF");
            listedNftInfo.Price.Amount.ShouldBe(3);
            listedNftInfo.Quantity.ShouldBe(2);
            listedNftInfo.ListType.ShouldBe(ListType.FixedPrice);
            listedNftInfo.Duration.StartTime.ShouldNotBeNull();
            listedNftInfo.Duration.DurationHours.ShouldBe(24);
        }

        //GetWhitelistId
        Func<Task> act = () => Seller1ForestContractStub.GetWhitelistId.CallAsync(new GetWhitelistIdInput()
        {
            Symbol = NftSymbol,
            Owner = User1Address
        });
        var exception = await Assert.ThrowsAsync<Exception>(act);
        exception.Message.ShouldContain("Failed to call GetWhitelistId");
    }

    [Fact]
    public async void ListWithFixedPrice4Test()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        var listQuantity = 2;
        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = listQuantity });

            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = NftSymbol,
                Quantity = listQuantity,
                IsWhitelistAvailable = true,
                Price = sellPrice,
                Duration = new ListDuration
                {
                    DurationHours = 24
                }
            });

            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = listQuantity*2 });
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = NftSymbol,
                Quantity = listQuantity,
                IsWhitelistAvailable = true,
                Price = sellPrice,
                Duration = new ListDuration
                {
                    StartTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(5)),
                    DurationHours = 24
                }
            });

            var listedNftInfo = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
                new GetListedNFTInfoListInput
                {
                    Symbol = NftSymbol,
                    Owner = User1Address
                })).Value.First();
            listedNftInfo.Price.Symbol.ShouldBe("ELF");
            listedNftInfo.Price.Amount.ShouldBe(3);
            listedNftInfo.Quantity.ShouldBe(2);
            listedNftInfo.ListType.ShouldBe(ListType.FixedPrice);
            listedNftInfo.Duration.StartTime.ShouldNotBeNull();
            listedNftInfo.Duration.DurationHours.ShouldBe(24);
        }
    }

    [Fact]
    public async void ListWithFixedPrice5Test()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        var listQuantity = 2;
        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = listQuantity });
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = NftSymbol,
                Quantity = listQuantity,
                IsWhitelistAvailable = true,
                Price = sellPrice
            });

            var listedNftInfo = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
                new GetListedNFTInfoListInput
                {
                    Symbol = NftSymbol,
                    Owner = User1Address
                })).Value.First();
            listedNftInfo.Price.Symbol.ShouldBe("ELF");
            listedNftInfo.Price.Amount.ShouldBe(3);
            listedNftInfo.Quantity.ShouldBe(2);
            listedNftInfo.ListType.ShouldBe(ListType.FixedPrice);
            listedNftInfo.Duration.StartTime.ShouldNotBeNull();
            listedNftInfo.Duration.DurationHours.ShouldBe(4392L);
        }
    }

    [Fact]
    public async void ListWithFixedPrice6Test()
    {
        await InitializeForestContract();
        //await PrepareNftData();
        var sellPrice = Elf(3);
        {
            Func<Task> act = () => Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = "oiii-1",
                Quantity = 2,
                IsWhitelistAvailable = true,
                Price = sellPrice
            });
            var exception = await Assert.ThrowsAsync<Exception>(act);
            exception.Message.ShouldContain("this NFT Info not exists.");
        }
        //Insufficient NFT balance.
    }

    [Fact]
    public async void ListWithFixedPrice7Test()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var listQuantity = 2;
        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = listQuantity });
            Func<Task> act = () => Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = NftSymbol,
                Quantity = 2,
                IsWhitelistAvailable = true,
                Price = new Price()
                {
                    Symbol = "usdt",
                    Amount = 33
                },
            });
            var exception = await Assert.ThrowsAsync<Exception>(act);
            exception.Message.ShouldContain("usdt is not in token white list.");
        }
    }

    [Fact]
    public async void ListWithFixedPrice8Test()
    {
        await InitializeForestContract();
        await PrepareNftData();
        {
            Func<Task> act = () => Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = NftSymbol,
                Quantity = 2,
                IsWhitelistAvailable = true,
                Price = new Price()
                {
                    Symbol = ElfSymbol,
                    Amount = -33
                },
            });
            var exception = await Assert.ThrowsAsync<Exception>(act);
            exception.Message.ShouldContain("Incorrect listing price.");
            //
        }
    }

    [Fact]
    public async void ListWithFixedPrice9Test()
    {
        await InitializeForestContract();
        await PrepareNftData();
        {
            Func<Task> act = () => Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = NftSymbol,
                Quantity = 2,
                IsWhitelistAvailable = true,
                Price = new Price()
                {
                    Symbol = ElfSymbol,
                    Amount = 0
                },
            });
            var exception = await Assert.ThrowsAsync<Exception>(act);
            exception.Message.ShouldContain("Incorrect listing price.");
            //
        }
    }

    [Fact]
    public async void ListWithFixedPrice10Test()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        {
            Func<Task> act = () => Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = NftSymbol,
                Quantity = 2000,
                IsWhitelistAvailable = true,
                Price = new Price()
                {
                    Symbol = ElfSymbol,
                    Amount = 5
                },
            });
            var exception = await Assert.ThrowsAsync<Exception>(act);
            exception.Message.ShouldContain("Check sender NFT balance failed.");
        }
    }

    [Fact]
    public async void ListWithFixedPrice11Test()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        {
            Func<Task> act = () => Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = NftSymbol,
                Quantity = -10,
                IsWhitelistAvailable = true,
                Price = sellPrice
            });
            var exception = await Assert.ThrowsAsync<Exception>(act);
            exception.Message.ShouldContain("Incorrect quantity.");
        }
    }

    [Fact]
    public async void ListWithFixedPrice12Test()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        {
            Func<Task> act = () => Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = NftSymbol,
                Quantity = 0,
                IsWhitelistAvailable = true,
                Price = sellPrice
            });
            var exception = await Assert.ThrowsAsync<Exception>(act);
            exception.Message.ShouldContain("Incorrect quantity.");
        }
    }

    [Fact]
    public async void ListWithFixedPrice13Test()
    {
        //await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        {
            Func<Task> act = () => Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = NftSymbol,
                Quantity = 1,
                IsWhitelistAvailable = true,
                Price = sellPrice
            });
            var exception = await Assert.ThrowsAsync<Exception>(act);
            exception.Message.ShouldContain("Contract not initialized.");
        }
    }

    [Fact]
    public async void Delist15Test()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var listQuantity = 4;
        var sellPrice = Elf(3);
        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = listQuantity });
            var executionResult = await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(
                new ListWithFixedPriceInput
                {
                    Symbol = NftSymbol,
                    Quantity = listQuantity,
                    IsWhitelistAvailable = true,
                    Price = sellPrice
                });
            var log = ListedNFTAdded.Parser
                .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name == nameof(ListedNFTAdded))
                    .NonIndexed);
            log.Owner.ShouldBe(User1Address);
            log.Quantity.ShouldBe(4);
            log.Symbol.ShouldBe(NftSymbol);
            log.Price.Symbol.ShouldBe(ElfSymbol);
            log.Price.Amount.ShouldBe(3);
            log.Duration.StartTime.ShouldNotBeNull();
            log.Duration.DurationHours.ShouldBe(4392L);

            var listedNftInfo = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
                new GetListedNFTInfoListInput
                {
                    Symbol = NftSymbol,
                    Owner = User1Address
                })).Value.First();
            listedNftInfo.Price.Symbol.ShouldBe("ELF");
            listedNftInfo.Price.Amount.ShouldBe(3);
            listedNftInfo.Quantity.ShouldBe(4);
            listedNftInfo.ListType.ShouldBe(ListType.FixedPrice);
            listedNftInfo.Duration.StartTime.ShouldNotBeNull();
            listedNftInfo.Duration.DurationHours.ShouldBe(4392L);
        }

        var executionResult1 = await Seller1ForestContractStub.Delist.SendAsync(new DelistInput
        {
            Symbol = NftSymbol,
            Price = sellPrice,
            Quantity = 1
        });
        var log1 = ListedNFTChanged.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.First(l => l.Name == nameof(ListedNFTChanged))
                .NonIndexed);
        log1.Owner.ShouldBe(User1Address);
        log1.Quantity.ShouldBe(3);
        log1.Symbol.ShouldBe(NftSymbol);
        log1.Price.Symbol.ShouldBe(ElfSymbol);
        log1.Price.Amount.ShouldBe(3);
        log1.Duration.StartTime.ShouldNotBeNull();
        log1.Duration.DurationHours.ShouldBe(4392L);
        
        var log2 = NFTDelisted.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.Last(l => l.Name == nameof(NFTDelisted))
                .NonIndexed);
        log2.Owner.ShouldBe(User1Address);
        log2.Quantity.ShouldBe(1);
        log2.Symbol.ShouldBe(NftSymbol);

        var listedNftInfo1 = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
            new GetListedNFTInfoListInput
            {
                Symbol = NftSymbol,
                Owner = User1Address
            })).Value.First();
        listedNftInfo1.Price.Symbol.ShouldBe("ELF");
        listedNftInfo1.Price.Amount.ShouldBe(3);
        listedNftInfo1.Quantity.ShouldBe(3);
        listedNftInfo1.ListType.ShouldBe(ListType.FixedPrice);
        listedNftInfo1.Duration.StartTime.ShouldNotBeNull();
        listedNftInfo1.Duration.DurationHours.ShouldBe(4392L);
    }


    [Fact]
    public async void Delist16Test()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        var listQuantity = 1;
        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = listQuantity });
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = NftSymbol,
                Quantity = listQuantity,
                IsWhitelistAvailable = true,
                Price = sellPrice
            });

            var listedNftInfo = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
                new GetListedNFTInfoListInput
                {
                    Symbol = NftSymbol,
                    Owner = User1Address
                })).Value.First();
            listedNftInfo.Price.Symbol.ShouldBe("ELF");
            listedNftInfo.Price.Amount.ShouldBe(3);
            listedNftInfo.Quantity.ShouldBe(1);
            listedNftInfo.ListType.ShouldBe(ListType.FixedPrice);
            listedNftInfo.Duration.StartTime.ShouldNotBeNull();
            listedNftInfo.Duration.DurationHours.ShouldBe(4392L);
        }

        await Seller1ForestContractStub.Delist.SendAsync(new DelistInput
        {
            Symbol = NftSymbol,
            Price = sellPrice,
            Quantity = 1
        });
        {
            Func<Task> act = () => Seller1ForestContractStub.Delist.SendAsync(new DelistInput
            {
                Symbol = NftSymbol,
                Price = sellPrice,
                Quantity = 1
            });
            var exception = await Assert.ThrowsAsync<Exception>(act);
            exception.Message.ShouldContain("Listed NFT Info not exists. (Or already delisted.");
        }
    }

    [Fact]
    public async void Delist17Test()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        var listQuantity = 1;
        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = listQuantity });
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = NftSymbol,
                Quantity = listQuantity,
                IsWhitelistAvailable = true,
                Price = sellPrice
            });

            var listedNftInfo = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
                new GetListedNFTInfoListInput
                {
                    Symbol = NftSymbol,
                    Owner = User1Address
                })).Value.First();
            listedNftInfo.Price.Symbol.ShouldBe("ELF");
            listedNftInfo.Price.Amount.ShouldBe(3);
            listedNftInfo.Quantity.ShouldBe(1);
            listedNftInfo.ListType.ShouldBe(ListType.FixedPrice);
            listedNftInfo.Duration.StartTime.ShouldNotBeNull();
            listedNftInfo.Duration.DurationHours.ShouldBe(4392L);
        }
        {
            Func<Task> act = () => Seller1ForestContractStub.Delist.SendAsync(new DelistInput
            {
                Symbol = "abkd-1",
                Price = sellPrice,
                Quantity = 10000
            });
            var exception = await Assert.ThrowsAsync<Exception>(act);
            exception.Message.ShouldContain("Listed NFT Info not exists. (Or already delisted.");
        }
    }

    [Fact]
    public async void Delist18Test()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        var listQuantity = 1;
        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = listQuantity });
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = NftSymbol,
                Quantity = listQuantity,
                IsWhitelistAvailable = true,
                Price = sellPrice
            });

            var listedNftInfo = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
                new GetListedNFTInfoListInput
                {
                    Symbol = NftSymbol,
                    Owner = User1Address
                })).Value.First();
            listedNftInfo.Price.Symbol.ShouldBe("ELF");
            listedNftInfo.Price.Amount.ShouldBe(3);
            listedNftInfo.Quantity.ShouldBe(1);
            listedNftInfo.ListType.ShouldBe(ListType.FixedPrice);
            listedNftInfo.Duration.StartTime.ShouldNotBeNull();
            listedNftInfo.Duration.DurationHours.ShouldBe(4392L);
        }
        {
            Func<Task> act = () => Seller1ForestContractStub.Delist.SendAsync(new DelistInput
            {
                Symbol = NftSymbol,
                Price = null,
                Quantity = 1
            });
            var exception = await Assert.ThrowsAsync<Exception>(act);
            exception.Message.ShouldContain("Need to specific list record.");
        }
    }

    [Fact]
    public async void Delist19Test()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        var listQuantity = 1;
        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = listQuantity });
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = NftSymbol,
                Quantity = listQuantity,
                IsWhitelistAvailable = true,
                Price = sellPrice
            });

            var listedNftInfo = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
                new GetListedNFTInfoListInput
                {
                    Symbol = NftSymbol,
                    Owner = User1Address
                })).Value.First();
            listedNftInfo.Price.Symbol.ShouldBe("ELF");
            listedNftInfo.Price.Amount.ShouldBe(3);
            listedNftInfo.Quantity.ShouldBe(1);
            listedNftInfo.ListType.ShouldBe(ListType.FixedPrice);
            listedNftInfo.Duration.StartTime.ShouldNotBeNull();
            listedNftInfo.Duration.DurationHours.ShouldBe(4392L);
        }
        {
            Func<Task> act = () => Seller1ForestContractStub.Delist.SendAsync(new DelistInput
            {
                Symbol = NftSymbol,
                Price = new Price
                {
                    Symbol = "ELF",
                    Amount = 1000
                },
                Quantity = 1
            });
            var exception = await Assert.ThrowsAsync<Exception>(act);
            exception.Message.ShouldContain("Listed NFT Info not exists. (Or already delisted.");
        }
    }

    [Fact]
    public async void Delist20Test()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        var listQuantity = 1;
        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = listQuantity });
            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = NftSymbol,
                Quantity = listQuantity,
                IsWhitelistAvailable = true,
                Price = sellPrice
            });

            var listedNftInfo = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
                new GetListedNFTInfoListInput
                {
                    Symbol = NftSymbol,
                    Owner = User1Address
                })).Value.First();
            listedNftInfo.Price.Symbol.ShouldBe("ELF");
            listedNftInfo.Price.Amount.ShouldBe(3);
            listedNftInfo.Quantity.ShouldBe(1);
            listedNftInfo.ListType.ShouldBe(ListType.FixedPrice);
            listedNftInfo.Duration.StartTime.ShouldNotBeNull();
            listedNftInfo.Duration.DurationHours.ShouldBe(4392L);
        }
        {
            Func<Task> act = () => Seller1ForestContractStub.Delist.SendAsync(new DelistInput
            {
                Symbol = NftSymbol,
                Price = sellPrice,
                Quantity = -9000
            });
            var exception = await Assert.ThrowsAsync<Exception>(act);
            exception.Message.ShouldContain("Quantity must be a positive integer.");
        }
    }


    [Fact]
    public async void DelistAllTest()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        var listQuantity = 4;
        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = listQuantity });

            var executionResult = await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(
                new ListWithFixedPriceInput
                {
                    Symbol = NftSymbol,
                    Quantity = listQuantity,
                    IsWhitelistAvailable = true,
                    Price = sellPrice
                });
            var log = ListedNFTAdded.Parser
                .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name == nameof(ListedNFTAdded))
                    .NonIndexed);
            log.Owner.ShouldBe(User1Address);
            log.Quantity.ShouldBe(4);
            log.Symbol.ShouldBe(NftSymbol);
            log.Price.Symbol.ShouldBe(ElfSymbol);
            log.Price.Amount.ShouldBe(3);
            log.Duration.StartTime.ShouldNotBeNull();
            log.Duration.DurationHours.ShouldBe(4392L);

            var listedNftInfo = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
                new GetListedNFTInfoListInput
                {
                    Symbol = NftSymbol,
                    Owner = User1Address
                })).Value.First();
            listedNftInfo.Price.Symbol.ShouldBe("ELF");
            listedNftInfo.Price.Amount.ShouldBe(3);
            listedNftInfo.Quantity.ShouldBe(4);
            listedNftInfo.ListType.ShouldBe(ListType.FixedPrice);
            listedNftInfo.Duration.StartTime.ShouldNotBeNull();
            listedNftInfo.Duration.DurationHours.ShouldBe(4392L);
        }

        var executionResult1 = await Seller1ForestContractStub.Delist.SendAsync(new DelistInput
        {
            Symbol = NftSymbol,
            Price = sellPrice,
            Quantity = 4
        });
        var log1 = ListedNFTRemoved.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.First(l => l.Name == nameof(ListedNFTRemoved))
                .NonIndexed);
        log1.Owner.ShouldBe(User1Address);
        log1.Symbol.ShouldBe(NftSymbol);
        log1.Price.Symbol.ShouldBe(ElfSymbol);
        log1.Price.Amount.ShouldBe(3);
        log1.Duration.StartTime.ShouldNotBeNull();
        log1.Duration.DurationHours.ShouldBe(4392L);

        var log2 = NFTDelisted.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.Last(l => l.Name == nameof(NFTDelisted))
                .NonIndexed);
        log2.Owner.ShouldBe(User1Address);
        log2.Quantity.ShouldBe(4);
        log2.Symbol.ShouldBe(NftSymbol);

        var listedNftInfo1 = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
            new GetListedNFTInfoListInput
            {
                Symbol = NftSymbol,
                Owner = User1Address
            }));
        listedNftInfo1.Value.Count.ShouldBe(0);
    }

    [Fact]
    public async void Delist21Test()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        var listQuantity = 1;
        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = listQuantity });

            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = NftSymbol,
                Quantity = listQuantity,
                IsWhitelistAvailable = true,
                Price = sellPrice
            });

            var listedNftInfo = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
                new GetListedNFTInfoListInput
                {
                    Symbol = NftSymbol,
                    Owner = User1Address
                })).Value.First();
            listedNftInfo.Price.Symbol.ShouldBe("ELF");
            listedNftInfo.Price.Amount.ShouldBe(3);
            listedNftInfo.Quantity.ShouldBe(1);
            listedNftInfo.ListType.ShouldBe(ListType.FixedPrice);
            listedNftInfo.Duration.StartTime.ShouldNotBeNull();
            listedNftInfo.Duration.DurationHours.ShouldBe(4392L);
        }
        await Seller1ForestContractStub.Delist.SendAsync(new DelistInput
        {
            Symbol = NftSymbol,
            Price = sellPrice,
            Quantity = 9000
        });

        var listedNftInfo1 = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
            new GetListedNFTInfoListInput
            {
                Symbol = NftSymbol,
                Owner = User1Address
            }));
        listedNftInfo1.Value.Count.ShouldBe(0);
    }

    
    private async Task InitListInfo(int listQuantity, int inputSellPrice, int approveQuantity)
    {
        var sellPrice = Elf(inputSellPrice);
        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
                { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = approveQuantity });
            var executionResult = await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(
                new ListWithFixedPriceInput
                {
                    Symbol = NftSymbol,
                    Quantity = listQuantity,
                    IsWhitelistAvailable = true,
                    Price = sellPrice,
                    Duration = new ListDuration()
                    {
                        StartTime = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow.AddSeconds(approveQuantity)),
                        PublicTime = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow.AddSeconds(approveQuantity)),
                    }
                });
            var log = ListedNFTAdded.Parser
                .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name == nameof(ListedNFTAdded))
                    .NonIndexed);
            log.Owner.ShouldBe(User1Address);
            log.Quantity.ShouldBe(listQuantity);
            log.Symbol.ShouldBe(NftSymbol);
            log.Price.Symbol.ShouldBe(ElfSymbol);
            log.Price.Amount.ShouldBe(inputSellPrice);
            log.Duration.StartTime.ShouldNotBeNull();
            log.Duration.DurationHours.ShouldBe(4392L);

            var listedNftInfo = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
                new GetListedNFTInfoListInput
                {
                    Symbol = NftSymbol,
                    Owner = User1Address
                })).Value.Last();
            listedNftInfo.Price.Symbol.ShouldBe("ELF");
            listedNftInfo.Price.Amount.ShouldBe(inputSellPrice);
            listedNftInfo.Quantity.ShouldBe(listQuantity);
            listedNftInfo.ListType.ShouldBe(ListType.FixedPrice);
            listedNftInfo.Duration.StartTime.ShouldNotBeNull();
            listedNftInfo.Duration.DurationHours.ShouldBe(4392L);
        }
    }

    private async Task QueryLastByStartAscListInfo(int intpuListQuantity, int inputSellPrice)
    {
        {
            var listedNftInfo = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
                new GetListedNFTInfoListInput
                {
                    Symbol = NftSymbol,
                    Owner = User1Address
                })).Value.Last();
            listedNftInfo.Price.Symbol.ShouldBe("ELF");
            listedNftInfo.Price.Amount.ShouldBe(inputSellPrice);
            listedNftInfo.Quantity.ShouldBe(intpuListQuantity);
            listedNftInfo.ListType.ShouldBe(ListType.FixedPrice);
            listedNftInfo.Duration.StartTime.ShouldNotBeNull();
            listedNftInfo.Duration.DurationHours.ShouldBe(4392L);
        }
    }
    
    private async Task QueryFirstByStartAscListInfo(int intpuListQuantity, int inputSellPrice)
    {
        {
            var listedNftInfo = (await Seller1ForestContractStub.GetListedNFTInfoList.CallAsync(
                new GetListedNFTInfoListInput
                {
                    Symbol = NftSymbol,
                    Owner = User1Address
                })).Value.First();
            listedNftInfo.Price.Symbol.ShouldBe("ELF");
            listedNftInfo.Price.Amount.ShouldBe(inputSellPrice);
            listedNftInfo.Quantity.ShouldBe(intpuListQuantity);
            listedNftInfo.ListType.ShouldBe(ListType.FixedPrice);
            listedNftInfo.Duration.StartTime.ShouldNotBeNull();
            listedNftInfo.Duration.DurationHours.ShouldBe(4392L);
        }
    }
    
    [Fact]
    public async void Delist22Test()
    {
        //basic begin
        int approveQuantity = 0;
        await InitializeForestContract();
        await PrepareNftData();

        int inputListQuantity1 = 1;
        int inputSellPrice1 = 2;
        approveQuantity += inputListQuantity1;
        await InitListInfo(inputListQuantity1, inputSellPrice1, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity2 = 2;
        int inputSellPrice2 = 2;
        approveQuantity += inputListQuantity2;
        await InitListInfo(inputListQuantity2, inputSellPrice2, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity2, inputSellPrice2);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity3 = 3;
        int inputSellPrice3 = 4;
        approveQuantity += inputListQuantity3;
        await InitListInfo(inputListQuantity3, inputSellPrice3, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity3, inputSellPrice3);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        
        int inputListQuantity4 = 5;
        int inputSellPrice4 = 5;
        approveQuantity += inputListQuantity4;
        await InitListInfo(inputListQuantity4, inputSellPrice4, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity4, inputSellPrice4);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        //basic end
        
        Func<Task> act = () => Seller1ForestContractStub.BatchDeList.SendAsync(new BatchDeListInput
        {
            Symbol = NftSymbol,
            Price = new Price()
            {
                Symbol = "ELF",
                Amount = inputSellPrice1
            },
            BatchDelistType = ForestContract.BatchDeListTypeLessThan
        });

        var exception = await Assert.ThrowsAsync<Exception>(act);
        exception.Message.ShouldContain("Listed NFT Info not exists. (Or already delisted.)");
    }

    [Fact]
    public async void Delist23Test()
    {
        //basic begin
        int approveQuantity = 0;
        await InitializeForestContract();
        await PrepareNftData();

        int inputListQuantity1 = 1;
        int inputSellPrice1 = 2;
        approveQuantity += inputListQuantity1;
        await InitListInfo(inputListQuantity1, inputSellPrice1, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity2 = 2;
        int inputSellPrice2 = 2;
        approveQuantity += inputListQuantity2;
        await InitListInfo(inputListQuantity2, inputSellPrice2, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity2, inputSellPrice2);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity3 = 3;
        int inputSellPrice3 = 4;
        approveQuantity += inputListQuantity3;
        await InitListInfo(inputListQuantity3, inputSellPrice3, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity3, inputSellPrice3);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        
        int inputListQuantity4 = 5;
        int inputSellPrice4 = 5;
        approveQuantity += inputListQuantity4;
        await InitListInfo(inputListQuantity4, inputSellPrice4, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity4, inputSellPrice4);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        //basic end
        
        var executionResult1 = await Seller1ForestContractStub.BatchDeList.SendAsync(new BatchDeListInput
        {
            Symbol = NftSymbol,
            Price = new Price()
            {
                Symbol = "ELF",
                Amount = inputSellPrice3
            },
            BatchDelistType = ForestContract.BatchDeListTypeLessThan
        });
        await QueryLastByStartAscListInfo(inputListQuantity4, inputSellPrice4);
        await QueryFirstByStartAscListInfo(inputListQuantity3, inputSellPrice3);
        
        executionResult1.TransactionResult.Logs.Count.ShouldBe(2);
        
        var log1 = ListedNFTRemoved.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.First(l => l.Name == nameof(ListedNFTRemoved))
                .NonIndexed);
        log1.Owner.ShouldBe(User1Address);
        //log1.Quantity.ShouldBe(inputListQuantity1);
        log1.Symbol.ShouldBe(NftSymbol);
        log1.Duration.ShouldNotBeNull();
        log1.Duration.DurationHours.ShouldBe(4392L);
        log1.Duration.StartTime.ShouldNotBeNull();
        log1.Duration.PublicTime.ShouldNotBeNull();
        log1.Price.Amount.ShouldBe(inputSellPrice1);
        
        var log2 = ListedNFTRemoved.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.Skip(1).First(l => l.Name == nameof(ListedNFTRemoved))
                .NonIndexed);
        log2.Owner.ShouldBe(User1Address);
        log2.Symbol.ShouldBe(NftSymbol);
        log2.Duration.ShouldNotBeNull();
        log2.Duration.DurationHours.ShouldBe(4392L);
        log2.Duration.StartTime.ShouldNotBeNull();
        log2.Duration.PublicTime.ShouldNotBeNull();
        log2.Price.Amount.ShouldBe(inputSellPrice1);
    }

    [Fact]
    public async void Delist24Test()
    {
        //basic begin
        int approveQuantity = 0;
        await InitializeForestContract();
        await PrepareNftData();

        int inputListQuantity1 = 1;
        int inputSellPrice1 = 2;
        approveQuantity += inputListQuantity1;
        await InitListInfo(inputListQuantity1, inputSellPrice1, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity2 = 2;
        int inputSellPrice2 = 2;
        approveQuantity += inputListQuantity2;
        await InitListInfo(inputListQuantity2, inputSellPrice2, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity2, inputSellPrice2);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity3 = 3;
        int inputSellPrice3 = 4;
        approveQuantity += inputListQuantity3;
        await InitListInfo(inputListQuantity3, inputSellPrice3, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity3, inputSellPrice3);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        
        int inputListQuantity4 = 5;
        int inputSellPrice4 = 5;
        approveQuantity += inputListQuantity4;
        await InitListInfo(inputListQuantity4, inputSellPrice4, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity4, inputSellPrice4);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        //basic end
        
        var executionResult1 = await Seller1ForestContractStub.BatchDeList.SendAsync(new BatchDeListInput
        {
            Symbol = NftSymbol,
            Price = new Price()
            {
                Symbol = "ELF",
                Amount = inputSellPrice1
            },
            BatchDelistType = ForestContract.BatchDeListTypeLessThanOrEquals
        });
        await QueryLastByStartAscListInfo(inputListQuantity4, inputSellPrice4);
        await QueryFirstByStartAscListInfo(inputListQuantity3, inputSellPrice3);
        
        executionResult1.TransactionResult.Logs.Count.ShouldBe(2);
        
        var log1 = ListedNFTRemoved.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.First(l => l.Name == nameof(ListedNFTRemoved))
                .NonIndexed);
        log1.Owner.ShouldBe(User1Address);
        //log1.Quantity.ShouldBe(inputListQuantity1);
        log1.Symbol.ShouldBe(NftSymbol);
        log1.Duration.ShouldNotBeNull();
        log1.Duration.DurationHours.ShouldBe(4392L);
        log1.Duration.StartTime.ShouldNotBeNull();
        log1.Duration.PublicTime.ShouldNotBeNull();
        log1.Price.Amount.ShouldBe(inputSellPrice1);
        
        var log2 = ListedNFTRemoved.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.Skip(1).First(l => l.Name == nameof(ListedNFTRemoved))
                .NonIndexed);
        log2.Owner.ShouldBe(User1Address);
        log2.Symbol.ShouldBe(NftSymbol);
        log2.Duration.ShouldNotBeNull();
        log2.Duration.DurationHours.ShouldBe(4392L);
        log2.Duration.StartTime.ShouldNotBeNull();
        log2.Duration.PublicTime.ShouldNotBeNull();
        log2.Price.Amount.ShouldBe(inputSellPrice1);
    }

    [Fact]
    public async void Delist25Test()
    {
        //basic begin
        int approveQuantity = 0;
        await InitializeForestContract();
        await PrepareNftData();

        int inputListQuantity1 = 1;
        int inputSellPrice1 = 2;
        approveQuantity += inputListQuantity1;
        await InitListInfo(inputListQuantity1, inputSellPrice1, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity2 = 2;
        int inputSellPrice2 = 2;
        approveQuantity += inputListQuantity2;
        await InitListInfo(inputListQuantity2, inputSellPrice2, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity2, inputSellPrice2);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity3 = 3;
        int inputSellPrice3 = 4;
        approveQuantity += inputListQuantity3;
        await InitListInfo(inputListQuantity3, inputSellPrice3, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity3, inputSellPrice3);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        
        int inputListQuantity4 = 5;
        int inputSellPrice4 = 5;
        approveQuantity += inputListQuantity4;
        await InitListInfo(inputListQuantity4, inputSellPrice4, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity4, inputSellPrice4);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        //basic end
        
        var executionResult1 = await Seller1ForestContractStub.BatchDeList.SendAsync(new BatchDeListInput
        {
            Symbol = NftSymbol,
            Price = new Price()
            {
                Symbol = "ELF",
                Amount = inputSellPrice2
            },
            BatchDelistType = ForestContract.BatchDeListTypeLessThanOrEquals
        });
        await QueryLastByStartAscListInfo(inputListQuantity4, inputSellPrice4);
        await QueryFirstByStartAscListInfo(inputListQuantity3, inputSellPrice3);
        
        executionResult1.TransactionResult.Logs.Count.ShouldBe(2);
        
        var log1 = ListedNFTRemoved.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.First(l => l.Name == nameof(ListedNFTRemoved))
                .NonIndexed);
        log1.Owner.ShouldBe(User1Address);
        //log1.Quantity.ShouldBe(inputListQuantity1);
        log1.Symbol.ShouldBe(NftSymbol);
        log1.Duration.ShouldNotBeNull();
        log1.Duration.DurationHours.ShouldBe(4392L);
        log1.Duration.StartTime.ShouldNotBeNull();
        log1.Duration.PublicTime.ShouldNotBeNull();
        log1.Price.Amount.ShouldBe(inputSellPrice1);
        
        var log2 = ListedNFTRemoved.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.Skip(1).First(l => l.Name == nameof(ListedNFTRemoved))
                .NonIndexed);
        log2.Owner.ShouldBe(User1Address);
        log2.Symbol.ShouldBe(NftSymbol);
        log2.Duration.ShouldNotBeNull();
        log2.Duration.DurationHours.ShouldBe(4392L);
        log2.Duration.StartTime.ShouldNotBeNull();
        log2.Duration.PublicTime.ShouldNotBeNull();
        log2.Price.Amount.ShouldBe(inputSellPrice1);
    }
    
    [Fact]
    public async void Delist26Test()
    {
        //basic begin
        int approveQuantity = 0;
        await InitializeForestContract();
        await PrepareNftData();

        int inputListQuantity1 = 1;
        int inputSellPrice1 = 2;
        approveQuantity += inputListQuantity1;
        await InitListInfo(inputListQuantity1, inputSellPrice1, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity2 = 2;
        int inputSellPrice2 = 2;
        approveQuantity += inputListQuantity2;
        await InitListInfo(inputListQuantity2, inputSellPrice2, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity2, inputSellPrice2);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity3 = 3;
        int inputSellPrice3 = 4;
        approveQuantity += inputListQuantity3;
        await InitListInfo(inputListQuantity3, inputSellPrice3, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity3, inputSellPrice3);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        
        int inputListQuantity4 = 5;
        int inputSellPrice4 = 5;
        approveQuantity += inputListQuantity4;
        await InitListInfo(inputListQuantity4, inputSellPrice4, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity4, inputSellPrice4);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        //basic end
        
        var executionResult1 = await Seller1ForestContractStub.BatchDeList.SendAsync(new BatchDeListInput
        {
            Symbol = NftSymbol,
            Price = new Price()
            {
                Symbol = "ELF",
                Amount = inputSellPrice3
            },
            BatchDelistType = ForestContract.BatchDeListTypeLessThanOrEquals
        });
        await QueryLastByStartAscListInfo(inputListQuantity4, inputSellPrice4);
        await QueryFirstByStartAscListInfo(inputListQuantity4, inputSellPrice4);
        
        executionResult1.TransactionResult.Logs.Count.ShouldBe(3);
        
        var log1 = ListedNFTRemoved.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.First(l => l.Name == nameof(ListedNFTRemoved))
                .NonIndexed);
        log1.Owner.ShouldBe(User1Address);
        //log1.Quantity.ShouldBe(inputListQuantity1);
        log1.Symbol.ShouldBe(NftSymbol);
        log1.Duration.ShouldNotBeNull();
        log1.Duration.DurationHours.ShouldBe(4392L);
        log1.Duration.StartTime.ShouldNotBeNull();
        log1.Duration.PublicTime.ShouldNotBeNull();
        log1.Price.Amount.ShouldBe(inputSellPrice1);
        
        var log2 = ListedNFTRemoved.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.Skip(1).First(l => l.Name == nameof(ListedNFTRemoved))
                .NonIndexed);
        log2.Owner.ShouldBe(User1Address);
        log2.Symbol.ShouldBe(NftSymbol);
        log2.Duration.ShouldNotBeNull();
        log2.Duration.DurationHours.ShouldBe(4392L);
        log2.Duration.StartTime.ShouldNotBeNull();
        log2.Duration.PublicTime.ShouldNotBeNull();
        log2.Price.Amount.ShouldBe(inputSellPrice1);
        
        var log3 = ListedNFTRemoved.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.Skip(2).First(l => l.Name == nameof(ListedNFTRemoved))
                .NonIndexed);
        log3.Owner.ShouldBe(User1Address);
        log3.Symbol.ShouldBe(NftSymbol);
        log3.Duration.ShouldNotBeNull();
        log3.Duration.DurationHours.ShouldBe(4392L);
        log3.Duration.StartTime.ShouldNotBeNull();
        log3.Duration.PublicTime.ShouldNotBeNull();
        log3.Price.Amount.ShouldBe(inputSellPrice3);
    }

    [Fact]
    public async void Delist27Test()
    {
        //basic begin
        int approveQuantity = 0;
        await InitializeForestContract();
        await PrepareNftData();

        int inputListQuantity1 = 1;
        int inputSellPrice1 = 2;
        approveQuantity += inputListQuantity1;
        await InitListInfo(inputListQuantity1, inputSellPrice1, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity2 = 2;
        int inputSellPrice2 = 2;
        approveQuantity += inputListQuantity2;
        await InitListInfo(inputListQuantity2, inputSellPrice2, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity2, inputSellPrice2);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity3 = 3;
        int inputSellPrice3 = 4;
        approveQuantity += inputListQuantity3;
        await InitListInfo(inputListQuantity3, inputSellPrice3, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity3, inputSellPrice3);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        
        int inputListQuantity4 = 5;
        int inputSellPrice4 = 5;
        approveQuantity += inputListQuantity4;
        await InitListInfo(inputListQuantity4, inputSellPrice4, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity4, inputSellPrice4);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        //basic end
        
        var executionResult1 = await Seller1ForestContractStub.BatchDeList.SendAsync(new BatchDeListInput
        {
            Symbol = NftSymbol,
            Price = new Price()
            {
                Symbol = "ELF",
                Amount = inputSellPrice1
            },
            BatchDelistType = ForestContract.BatchDeListTypeGreaterThanOrEquals
        });
        
        executionResult1.TransactionResult.Logs.Count.ShouldBe(4);
       
        var log1 = ListedNFTRemoved.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.First(l => l.Name == nameof(ListedNFTRemoved))
                .NonIndexed);
        log1.Owner.ShouldBe(User1Address);
        //log1.Quantity.ShouldBe(inputListQuantity1);
        log1.Symbol.ShouldBe(NftSymbol);
        log1.Duration.ShouldNotBeNull();
        log1.Duration.DurationHours.ShouldBe(4392L);
        log1.Duration.StartTime.ShouldNotBeNull();
        log1.Duration.PublicTime.ShouldNotBeNull();
        log1.Price.Amount.ShouldBe(inputSellPrice1);
        
        var log2 = ListedNFTRemoved.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.Skip(1).First(l => l.Name == nameof(ListedNFTRemoved))
                .NonIndexed);
        log2.Owner.ShouldBe(User1Address);
        log2.Symbol.ShouldBe(NftSymbol);
        log2.Duration.ShouldNotBeNull();
        log2.Duration.DurationHours.ShouldBe(4392L);
        log2.Duration.StartTime.ShouldNotBeNull();
        log2.Duration.PublicTime.ShouldNotBeNull();
        log2.Price.Amount.ShouldBe(inputSellPrice1);
        
        var log3 = ListedNFTRemoved.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.Skip(2).First(l => l.Name == nameof(ListedNFTRemoved))
                .NonIndexed);
        log3.Owner.ShouldBe(User1Address);
        log3.Symbol.ShouldBe(NftSymbol);
        log3.Duration.ShouldNotBeNull();
        log3.Duration.DurationHours.ShouldBe(4392L);
        log3.Duration.StartTime.ShouldNotBeNull();
        log3.Duration.PublicTime.ShouldNotBeNull();
        log3.Price.Amount.ShouldBe(inputSellPrice3);
        
        var log4 = ListedNFTRemoved.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.Skip(3).First(l => l.Name == nameof(ListedNFTRemoved))
                .NonIndexed);
        log4.Owner.ShouldBe(User1Address);
        log4.Symbol.ShouldBe(NftSymbol);
        log4.Duration.ShouldNotBeNull();
        log4.Duration.DurationHours.ShouldBe(4392L);
        log4.Duration.StartTime.ShouldNotBeNull();
        log4.Duration.PublicTime.ShouldNotBeNull();
        log4.Price.Amount.ShouldBe(inputSellPrice4);
    }
    
    [Fact]
    public async void Delist28Test()
    {
        //basic begin
        int approveQuantity = 0;
        await InitializeForestContract();
        await PrepareNftData();

        int inputListQuantity1 = 1;
        int inputSellPrice1 = 2;
        approveQuantity += inputListQuantity1;
        await InitListInfo(inputListQuantity1, inputSellPrice1, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity2 = 2;
        int inputSellPrice2 = 2;
        approveQuantity += inputListQuantity2;
        await InitListInfo(inputListQuantity2, inputSellPrice2, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity2, inputSellPrice2);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity3 = 3;
        int inputSellPrice3 = 4;
        approveQuantity += inputListQuantity3;
        await InitListInfo(inputListQuantity3, inputSellPrice3, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity3, inputSellPrice3);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        
        int inputListQuantity4 = 5;
        int inputSellPrice4 = 5;
        approveQuantity += inputListQuantity4;
        await InitListInfo(inputListQuantity4, inputSellPrice4, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity4, inputSellPrice4);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        //basic end
        
        var executionResult1 = await Seller1ForestContractStub.BatchDeList.SendAsync(new BatchDeListInput
        {
            Symbol = NftSymbol,
            Price = new Price()
            {
                Symbol = "ELF",
                Amount = inputSellPrice1
            },
            BatchDelistType = ForestContract.BatchDeListTypeGreaterThan
        });
        
        executionResult1.TransactionResult.Logs.Count.ShouldBe(2);
        await QueryLastByStartAscListInfo(inputListQuantity2, inputSellPrice2);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        var log1 = ListedNFTRemoved.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.First(l => l.Name == nameof(ListedNFTRemoved))
                .NonIndexed);
        log1.Owner.ShouldBe(User1Address);
        //log1.Quantity.ShouldBe(inputListQuantity1);
        log1.Symbol.ShouldBe(NftSymbol);
        log1.Duration.ShouldNotBeNull();
        log1.Duration.DurationHours.ShouldBe(4392L);
        log1.Duration.StartTime.ShouldNotBeNull();
        log1.Duration.PublicTime.ShouldNotBeNull();
        log1.Price.Amount.ShouldBe(inputSellPrice3);
        
        var log2 = ListedNFTRemoved.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.Skip(1).First(l => l.Name == nameof(ListedNFTRemoved))
                .NonIndexed);
        log2.Owner.ShouldBe(User1Address);
        log2.Symbol.ShouldBe(NftSymbol);
        log2.Duration.ShouldNotBeNull();
        log2.Duration.DurationHours.ShouldBe(4392L);
        log2.Duration.StartTime.ShouldNotBeNull();
        log2.Duration.PublicTime.ShouldNotBeNull();
        log2.Price.Amount.ShouldBe(inputSellPrice4);
        
    }
    
    [Fact]
    public async void Delist29Test()
    {
        //basic begin
        int approveQuantity = 0;
        await InitializeForestContract();
        await PrepareNftData();

        int inputListQuantity1 = 1;
        int inputSellPrice1 = 2;
        approveQuantity += inputListQuantity1;
        await InitListInfo(inputListQuantity1, inputSellPrice1, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity2 = 2;
        int inputSellPrice2 = 2;
        approveQuantity += inputListQuantity2;
        await InitListInfo(inputListQuantity2, inputSellPrice2, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity2, inputSellPrice2);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity3 = 3;
        int inputSellPrice3 = 4;
        approveQuantity += inputListQuantity3;
        await InitListInfo(inputListQuantity3, inputSellPrice3, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity3, inputSellPrice3);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        
        int inputListQuantity4 = 5;
        int inputSellPrice4 = 5;
        approveQuantity += inputListQuantity4;
        await InitListInfo(inputListQuantity4, inputSellPrice4, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity4, inputSellPrice4);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        //basic end
        
        var executionResult1 = await Seller1ForestContractStub.BatchDeList.SendAsync(new BatchDeListInput
        {
            Symbol = NftSymbol,
            Price = new Price()
            {
                Symbol = "ELF",
                Amount = inputSellPrice2
            },
            BatchDelistType = ForestContract.BatchDeListTypeGreaterThan
        });
        
        executionResult1.TransactionResult.Logs.Count.ShouldBe(2);
        await QueryLastByStartAscListInfo(inputListQuantity2, inputSellPrice2);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        var log1 = ListedNFTRemoved.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.First(l => l.Name == nameof(ListedNFTRemoved))
                .NonIndexed);
        log1.Owner.ShouldBe(User1Address);
        //log1.Quantity.ShouldBe(inputListQuantity1);
        log1.Symbol.ShouldBe(NftSymbol);
        log1.Duration.ShouldNotBeNull();
        log1.Duration.DurationHours.ShouldBe(4392L);
        log1.Duration.StartTime.ShouldNotBeNull();
        log1.Duration.PublicTime.ShouldNotBeNull();
        log1.Price.Amount.ShouldBe(inputSellPrice3);
        
        var log2 = ListedNFTRemoved.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.Skip(1).First(l => l.Name == nameof(ListedNFTRemoved))
                .NonIndexed);
        log2.Owner.ShouldBe(User1Address);
        log2.Symbol.ShouldBe(NftSymbol);
        log2.Duration.ShouldNotBeNull();
        log2.Duration.DurationHours.ShouldBe(4392L);
        log2.Duration.StartTime.ShouldNotBeNull();
        log2.Duration.PublicTime.ShouldNotBeNull();
        log2.Price.Amount.ShouldBe(inputSellPrice4);
        
    }

    
    [Fact]
    public async void Delist30Test()
    {
        //basic begin
        int approveQuantity = 0;
        await InitializeForestContract();
        await PrepareNftData();

        int inputListQuantity1 = 1;
        int inputSellPrice1 = 2;
        approveQuantity += inputListQuantity1;
        await InitListInfo(inputListQuantity1, inputSellPrice1, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity2 = 2;
        int inputSellPrice2 = 2;
        approveQuantity += inputListQuantity2;
        await InitListInfo(inputListQuantity2, inputSellPrice2, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity2, inputSellPrice2);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity3 = 3;
        int inputSellPrice3 = 4;
        approveQuantity += inputListQuantity3;
        await InitListInfo(inputListQuantity3, inputSellPrice3, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity3, inputSellPrice3);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        
        int inputListQuantity4 = 5;
        int inputSellPrice4 = 5;
        approveQuantity += inputListQuantity4;
        await InitListInfo(inputListQuantity4, inputSellPrice4, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity4, inputSellPrice4);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        //basic end
        
        var executionResult1 = await Seller1ForestContractStub.BatchDeList.SendAsync(new BatchDeListInput
        {
            Symbol = NftSymbol,
            Price = new Price()
            {
                Symbol = "ELF",
                Amount = inputSellPrice3
            },
            BatchDelistType = ForestContract.BatchDeListTypeGreaterThan
        });
        
        executionResult1.TransactionResult.Logs.Count.ShouldBe(1);
        await QueryLastByStartAscListInfo(inputListQuantity3, inputSellPrice3);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        var log1 = ListedNFTRemoved.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.First(l => l.Name == nameof(ListedNFTRemoved))
                .NonIndexed);
        log1.Owner.ShouldBe(User1Address);
        //log1.Quantity.ShouldBe(inputListQuantity1);
        log1.Symbol.ShouldBe(NftSymbol);
        log1.Duration.ShouldNotBeNull();
        log1.Duration.DurationHours.ShouldBe(4392L);
        log1.Duration.StartTime.ShouldNotBeNull();
        log1.Duration.PublicTime.ShouldNotBeNull();
        log1.Price.Amount.ShouldBe(inputSellPrice4);

    }
    
    [Fact]
    public async void Delist31Test()
    {
        //basic begin
        int approveQuantity = 0;
        await InitializeForestContract();
        await PrepareNftData();

        int inputListQuantity1 = 1;
        int inputSellPrice1 = 2;
        approveQuantity += inputListQuantity1;
        await InitListInfo(inputListQuantity1, inputSellPrice1, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity2 = 2;
        int inputSellPrice2 = 2;
        approveQuantity += inputListQuantity2;
        await InitListInfo(inputListQuantity2, inputSellPrice2, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity2, inputSellPrice2);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity3 = 3;
        int inputSellPrice3 = 4;
        approveQuantity += inputListQuantity3;
        await InitListInfo(inputListQuantity3, inputSellPrice3, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity3, inputSellPrice3);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        
        int inputListQuantity4 = 5;
        int inputSellPrice4 = 5;
        approveQuantity += inputListQuantity4;
        await InitListInfo(inputListQuantity4, inputSellPrice4, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity4, inputSellPrice4);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        //basic end
        
        var executionResult1 = await Seller1ForestContractStub.BatchDeList.SendAsync(new BatchDeListInput
        {
            Symbol = NftSymbol,
            Price = new Price()
            {
                Symbol = "ELF",
                Amount = inputSellPrice4
            },
            BatchDelistType = ForestContract.BatchDeListTypeGreaterThanOrEquals
        });
        
        executionResult1.TransactionResult.Logs.Count.ShouldBe(1);
        await QueryLastByStartAscListInfo(inputListQuantity3, inputSellPrice3);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        var log1 = ListedNFTRemoved.Parser
            .ParseFrom(executionResult1.TransactionResult.Logs.First(l => l.Name == nameof(ListedNFTRemoved))
                .NonIndexed);
        log1.Owner.ShouldBe(User1Address);
        //log1.Quantity.ShouldBe(inputListQuantity1);
        log1.Symbol.ShouldBe(NftSymbol);
        log1.Duration.ShouldNotBeNull();
        log1.Duration.DurationHours.ShouldBe(4392L);
        log1.Duration.StartTime.ShouldNotBeNull();
        log1.Duration.PublicTime.ShouldNotBeNull();
        log1.Price.Amount.ShouldBe(inputSellPrice4);

    }

    [Fact]
    public async void Delist32Test()
    {
        //basic begin
        int approveQuantity = 0;
        await InitializeForestContract();
        await PrepareNftData();

        int inputListQuantity1 = 1;
        int inputSellPrice1 = 2;
        approveQuantity += inputListQuantity1;
        await InitListInfo(inputListQuantity1, inputSellPrice1, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity2 = 2;
        int inputSellPrice2 = 2;
        approveQuantity += inputListQuantity2;
        await InitListInfo(inputListQuantity2, inputSellPrice2, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity2, inputSellPrice2);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity3 = 3;
        int inputSellPrice3 = 4;
        approveQuantity += inputListQuantity3;
        await InitListInfo(inputListQuantity3, inputSellPrice3, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity3, inputSellPrice3);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        
        int inputListQuantity4 = 5;
        int inputSellPrice4 = 5;
        approveQuantity += inputListQuantity4;
        await InitListInfo(inputListQuantity4, inputSellPrice4, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity4, inputSellPrice4);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        //basic end
        
        
        Func<Task> act = () => Seller1ForestContractStub.BatchDeList.SendAsync(new BatchDeListInput
        {
            Symbol = NftSymbol,
            Price = new Price()
            {
                Symbol = "ELF",
                Amount = inputSellPrice4
            },
            BatchDelistType = ForestContract.BatchDeListTypeGreaterThan
        });

        var exception = await Assert.ThrowsAsync<Exception>(act);
        exception.Message.ShouldContain("Listed NFT Info not exists. (Or already delisted.)");
    }
    
    [Fact]
    public async void Delist33Test()
    {
        //basic begin
        int approveQuantity = 0;
        await InitializeForestContract();
        await PrepareNftData();

        int inputListQuantity1 = 1;
        int inputSellPrice1 = 2;
        approveQuantity += inputListQuantity1;
        await InitListInfo(inputListQuantity1, inputSellPrice1, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity2 = 2;
        int inputSellPrice2 = 2;
        approveQuantity += inputListQuantity2;
        await InitListInfo(inputListQuantity2, inputSellPrice2, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity2, inputSellPrice2);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity3 = 3;
        int inputSellPrice3 = 4;
        approveQuantity += inputListQuantity3;
        await InitListInfo(inputListQuantity3, inputSellPrice3, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity3, inputSellPrice3);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        
        int inputListQuantity4 = 5;
        int inputSellPrice4 = 5;
        approveQuantity += inputListQuantity4;
        await InitListInfo(inputListQuantity4, inputSellPrice4, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity4, inputSellPrice4);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        //basic end
        
        Func<Task> act = () => Seller1ForestContractStub.BatchDeList.SendAsync(new BatchDeListInput
        {
            Symbol = NftSymbol+"A",
            Price = new Price()
            {
                Symbol = "ELF",
                Amount = inputSellPrice4
            },
            BatchDelistType = ForestContract.BatchDeListTypeGreaterThan
        });

        var exception = await Assert.ThrowsAsync<Exception>(act);
        exception.Message.ShouldContain("this NFT Info not exists.");
        
    }
    
    [Fact]
    public async void Delist34Test()
    {
        //basic begin
        int approveQuantity = 0;
        await InitializeForestContract();
        await PrepareNftData();

        int inputListQuantity1 = 1;
        int inputSellPrice1 = 2;
        approveQuantity += inputListQuantity1;
        await InitListInfo(inputListQuantity1, inputSellPrice1, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity2 = 2;
        int inputSellPrice2 = 2;
        approveQuantity += inputListQuantity2;
        await InitListInfo(inputListQuantity2, inputSellPrice2, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity2, inputSellPrice2);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity3 = 3;
        int inputSellPrice3 = 4;
        approveQuantity += inputListQuantity3;
        await InitListInfo(inputListQuantity3, inputSellPrice3, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity3, inputSellPrice3);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        
        int inputListQuantity4 = 5;
        int inputSellPrice4 = 5;
        approveQuantity += inputListQuantity4;
        await InitListInfo(inputListQuantity4, inputSellPrice4, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity4, inputSellPrice4);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        //basic end
        
        Func<Task> act = () => Seller1ForestContractStub.BatchDeList.SendAsync(new BatchDeListInput
        {
            Symbol = NftSymbol,
            Price = new Price()
            {
                Symbol = "ELF",
                Amount = -1
            },
            BatchDelistType = ForestContract.BatchDeListTypeGreaterThan
        });

        var exception = await Assert.ThrowsAsync<Exception>(act);
        exception.Message.ShouldContain("Incorrect listing price.");
        
    }
    
    [Fact]
    public async void Delist35Test()
    {
        //basic begin
        int approveQuantity = 0;
        await InitializeForestContract();
        await PrepareNftData();

        int inputListQuantity1 = 1;
        int inputSellPrice1 = 2;
        approveQuantity += inputListQuantity1;
        await InitListInfo(inputListQuantity1, inputSellPrice1, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity2 = 2;
        int inputSellPrice2 = 2;
        approveQuantity += inputListQuantity2;
        await InitListInfo(inputListQuantity2, inputSellPrice2, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity2, inputSellPrice2);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity3 = 3;
        int inputSellPrice3 = 4;
        approveQuantity += inputListQuantity3;
        await InitListInfo(inputListQuantity3, inputSellPrice3, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity3, inputSellPrice3);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        
        int inputListQuantity4 = 5;
        int inputSellPrice4 = 5;
        approveQuantity += inputListQuantity4;
        await InitListInfo(inputListQuantity4, inputSellPrice4, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity4, inputSellPrice4);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        //basic end
        
        Func<Task> act = () => Seller1ForestContractStub.BatchDeList.SendAsync(new BatchDeListInput
        {
            Symbol = NftSymbol,
            Price = new Price()
            {
                Symbol = "ELFA",
                Amount = inputListQuantity1
            },
            BatchDelistType = ForestContract.BatchDeListTypeGreaterThan
        });

        var exception = await Assert.ThrowsAsync<Exception>(act);
        exception.Message.ShouldContain("The same price symbol listed NFT Info not exists. (Or already delisted.)");
        
    }

    [Fact]
    public async void Delist36Test()
    {
        //basic begin
        int approveQuantity = 0;
        await InitializeForestContract();
        await PrepareNftData();

        int inputListQuantity1 = 1;
        int inputSellPrice1 = 2;
        approveQuantity += inputListQuantity1;
        await InitListInfo(inputListQuantity1, inputSellPrice1, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity2 = 2;
        int inputSellPrice2 = 2;
        approveQuantity += inputListQuantity2;
        await InitListInfo(inputListQuantity2, inputSellPrice2, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity2, inputSellPrice2);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);

        int inputListQuantity3 = 3;
        int inputSellPrice3 = 4;
        approveQuantity += inputListQuantity3;
        await InitListInfo(inputListQuantity3, inputSellPrice3, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity3, inputSellPrice3);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        
        int inputListQuantity4 = 5;
        int inputSellPrice4 = 5;
        approveQuantity += inputListQuantity4;
        await InitListInfo(inputListQuantity4, inputSellPrice4, approveQuantity);
        await QueryLastByStartAscListInfo(inputListQuantity4, inputSellPrice4);
        await QueryFirstByStartAscListInfo(inputListQuantity1, inputSellPrice1);
        //basic end
        
        Func<Task> act = () => Seller1ForestContractStub.BatchDeList.SendAsync(new BatchDeListInput
        {
            Symbol = NftSymbol,
            Price = new Price()
            {
                Symbol = "ELF",
                Amount = inputListQuantity1
            },
            BatchDelistType = ForestContract.BatchDeListTypeGreaterThan+1
        });

        var exception = await Assert.ThrowsAsync<Exception>(act);
        exception.Message.ShouldContain("BatchDeListType not exists.");
        
    }

    
    [Fact]
    public async void TransferTest()
    {
        await InitializeForestContract();
        await PrepareNftData();
        {
            {
                var balance1 = await TokenContractStub.GetBalance.CallAsync(
                    new AElf.Contracts.MultiToken.GetBalanceInput
                    {
                        Symbol = NftSymbol,
                        Owner = User1Address
                    });

                var balance2 = await TokenContractStub.GetBalance.CallAsync(
                    new AElf.Contracts.MultiToken.GetBalanceInput
                    {
                        Symbol = NftSymbol,
                        Owner = User2Address
                    });

                var executionResult = await UserTokenContractStub.Transfer.SendAsync(new TransferInput()
                {
                    To = User2Address,
                    Symbol = NftSymbol,
                    Amount = 2,
                    Memo = "for you 2 nft ..."
                });
                var log1 = Transferred.Parser
                    .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name == nameof(Transferred))
                        .NonIndexed);
                log1.Amount.ShouldBe(2);
                log1.Memo.ShouldBe("for you 2 nft ...");
        

                var balance3 = await TokenContractStub.GetBalance.CallAsync(
                    new AElf.Contracts.MultiToken.GetBalanceInput
                    {
                        Symbol = NftSymbol,
                        Owner = User2Address
                    });

                balance3.Balance.ShouldBe(2);
            }
        }
    }


    [Fact]
    public async void DuplicateList()
    {
        await InitializeForestContract();
        await PrepareNftData();
        
        // whitePrice < sellPrice < offerPrice
        var sellPrice = Elf(5_0000_0000);
        var whitePrice = Elf(2_0000_0000);

        // after publicTime
        var startTime = Timestamp.FromDateTime(DateTime.UtcNow.AddHours(-5));
        var publicTime = Timestamp.FromDateTime(DateTime.UtcNow.AddMinutes(-1));
        var listQuantity = 5;
        #region ListWithFixedPrice

        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = listQuantity });

            await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
            {
                Symbol = NftSymbol,
                Quantity = listQuantity,
                IsWhitelistAvailable = false,
                Price = sellPrice,
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
        
        #region ListWithFixedPrice twice

        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = listQuantity*2 });

            var exception = await Assert.ThrowsAsync<Exception>(
                () => Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput()
                {
                    Symbol = NftSymbol,
                    Quantity = listQuantity,
                    IsWhitelistAvailable = false,
                    Price = sellPrice,
                    Duration = new ListDuration()
                    {
                        // start 1sec ago
                        StartTime = startTime,
                        // public 10min after
                        PublicTime = publicTime,
                        DurationHours = 1,
                    },
                })
            );
            exception.Message.ShouldContain("already exists");
        }

        #endregion
        
    }
    
    [Fact]
    //seller nft allownce not enough
    public async void ListWithFixedPrice14Test()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        var listQuantity = 2;
        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = listQuantity-1 });
            var errorMessage = "";
            try
            {
                await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
                {
                    Symbol = NftSymbol,
                    Quantity = listQuantity,
                    IsWhitelistAvailable = true,
                    Price = sellPrice
                });

            }catch (Exception e)
            {
                errorMessage = e.Message;
            }
            errorMessage.ShouldContain("The allowance you set is less than required. Please reset it.");
        }
    }
    
    [Fact]
    //seller nft allownce equal enough
    public async void ListWithFixedPrice15Test()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        var listQuantity = 2;
        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = listQuantity});
            var executionResult = await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = NftSymbol,
                Quantity = listQuantity,
                IsWhitelistAvailable = true,
                Price = sellPrice
            });
            var log1 = ListedNFTAdded.Parser
                .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name == nameof(ListedNFTAdded))
                    .NonIndexed);
            log1.Owner.ShouldBe(User1Address);
            log1.Quantity.ShouldBe(listQuantity);
            log1.Symbol.ShouldBe(NftSymbol);
            log1.Price.Symbol.ShouldBe(ElfSymbol);
            log1.Price.Amount.ShouldBe(sellPrice.Amount);
            log1.Duration.StartTime.ShouldNotBeNull();
        }
    }
    [Fact]
    //seller nft allownce gretter enough
    public async void ListWithFixedPrice16Test()
    {
        await InitializeForestContract();
        await PrepareNftData();
        var sellPrice = Elf(3);
        var listQuantity = 2;
        {
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = ForestContractAddress, Symbol = NftSymbol, Amount = listQuantity+1});
            var executionResult = await Seller1ForestContractStub.ListWithFixedPrice.SendAsync(new ListWithFixedPriceInput
            {
                Symbol = NftSymbol,
                Quantity = listQuantity,
                IsWhitelistAvailable = true,
                Price = sellPrice
            });
            var log1 = ListedNFTAdded.Parser
                .ParseFrom(executionResult.TransactionResult.Logs.First(l => l.Name == nameof(ListedNFTAdded))
                    .NonIndexed);
            log1.Owner.ShouldBe(User1Address);
            log1.Quantity.ShouldBe(listQuantity);
            log1.Symbol.ShouldBe(NftSymbol);
            log1.Price.Symbol.ShouldBe(ElfSymbol);
            log1.Price.Amount.ShouldBe(sellPrice.Amount);
            log1.Duration.StartTime.ShouldNotBeNull();
        }
    }
}
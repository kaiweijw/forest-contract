using System;
using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using Shouldly;
using Xunit;

namespace Forest.Contracts.SymbolRegistrar
{
    public class SymbolRegistrarContractTests_Buy : SymbolRegistrarContractTests
    {
        
        [Fact]
        public async Task Buy_success()
        {
            await SetSeedsPrice_success();
            await InitSeed0();
            await InitElfBalance(User1.Address);
            await ApproveMaxElfBalance(User1TokenContractStub, SymbolRegistrarContractAddress);

            // To buy 
            var symbol = "LUCK";
            var res = await User1SymbolRegistrarContractStub.Buy.SendAsync(new BuyInput
            {
                Symbol  = symbol,
                IssueTo = User1.Address
            });
            
            var seedCreatedLog = res.TransactionResult.Logs.First(log => log.Name.Contains(nameof(SeedCreated)));
            seedCreatedLog.ShouldNotBeNull();
            var seedCreated = SeedCreated.Parser.ParseFrom(seedCreatedLog.NonIndexed);
            seedCreated.To.ShouldBe(User1.Address);
            seedCreated.OwnedSymbol.ShouldBe(symbol);
            var seedSymbol = seedCreated.Symbol;
            seedSymbol.ShouldNotBeEmpty();

            var boughtLog = res.TransactionResult.Logs.First(log => log.Name.Contains(nameof(Bought)));
            boughtLog.ShouldNotBeNull();
            var bought = Bought.Parser.ParseFrom(boughtLog.NonIndexed);
            bought.Symbol.ShouldBe(symbol);
            bought.Price.Symbol.ShouldBe("ELF");
            bought.Price.Amount.ShouldBe(47_0000_0000);
            bought.Buyer.ShouldBe(User1.Address);

            var token = await User1TokenContractStub.GetTokenInfo.CallAsync(new GetTokenInfoInput
            {
                Symbol = seedSymbol
            });
            token.ShouldNotBeNull();
            token.ExternalInfo.Value[SymbolRegistrarContractConstants.SeedOwnedSymbolExternalInfoKey].ShouldBe(symbol);

            var balance = await User1TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = User1.Address,
                Symbol = "SEED-1"
            });
            balance.Balance.ShouldBeGreaterThan(0);

            var elfBalance = await User1TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = User1.Address,
                Symbol = "ELF"
            });
            elfBalance.Balance.ShouldBe(9953_0000_0000);
        }


        [Fact]
        public async Task Buy_emptyIssueTo_success()
        {
            await SetSeedsPrice_success();
            await InitSeed0();
            await InitElfBalance(User1.Address);
            await ApproveMaxElfBalance(User1TokenContractStub, SymbolRegistrarContractAddress);

            // To buy 
            var symbol = "LUCK";
            var res = await User1SymbolRegistrarContractStub.Buy.SendAsync(new BuyInput
            {
                Symbol = symbol
            });

            var seedCreatedLog = res.TransactionResult.Logs.First(log => log.Name.Contains(nameof(SeedCreated)));
            seedCreatedLog.ShouldNotBeNull();
            var seedCreated = SeedCreated.Parser.ParseFrom(seedCreatedLog.NonIndexed);
            seedCreated.To.ShouldBe(User1.Address);
            seedCreated.OwnedSymbol.ShouldBe(symbol);
            var seedSymbol = seedCreated.Symbol;
            seedSymbol.ShouldNotBeEmpty();
        }

        [Fact]
        public async Task Buy_InsufficientBalanceOfELF()
        {
            await SetSeedsPrice_success();
            await InitSeed0();
            // await InitElfBalance(User1.Address);
            // await ApproveMaxElfBalance(User1TokenContractStub, SymbolRegistrarContractAddress);

            // To buy 
            var symbol = "LUCK";
            var res = await Assert.ThrowsAsync<Exception>(() => User1SymbolRegistrarContractStub.Buy.SendAsync(new BuyInput
            {
                Symbol = symbol,
                IssueTo = User1.Address
            }));
            res.ShouldNotBeNull();
            res.Message.ShouldContain("Insufficient allowance");
        }


        [Fact]
        public async Task Buy_NotInitialized()
        {
            // To buy 
            var symbol = "LUCK";
            var res = await Assert.ThrowsAsync<Exception>(() => User1SymbolRegistrarContractStub.Buy.SendAsync(new BuyInput
            {
                Symbol = symbol,
                IssueTo = User1.Address
            }));
            res.ShouldNotBeNull();
            res.Message.ShouldContain("Contract not Initialized");
        }
        
        [Fact]
        public async Task Buy_InvalidSeed_Fail()
        {
            // buy LUCK
            await Buy_success();
            await SetSpecialSeed_byProposal();
            
            var symbolExists = await Assert.ThrowsAsync<Exception>(() => User1SymbolRegistrarContractStub.Buy.SendAsync(new BuyInput
            {
                Symbol  = "SEED-0",
                IssueTo = User1.Address
            }));
            symbolExists.Message.ShouldContain("Symbol exists");
            
            var seedExists = await Assert.ThrowsAsync<Exception>(() => User1SymbolRegistrarContractStub.Buy.SendAsync(new BuyInput
            {
                Symbol  = "LUCK",
                IssueTo = User1.Address
            }));
            seedExists.Message.ShouldContain("Seed exists");

            var specialSeedNotSupport = await Assert.ThrowsAsync<Exception>(() => User1SymbolRegistrarContractStub.Buy.SendAsync(new BuyInput
            {
                Symbol  = _specialUsd.Symbol,
                IssueTo = User1.Address
            }));
            specialSeedNotSupport.Message.ShouldContain("not support deal");

            // token exits
            var newSymbol = await User1TokenContractStub.Create.SendAsync(new CreateInput
            {
                Symbol = "LUCK",
                TokenName = "LUCK symbol",
                TotalSupply = 100,
                Decimals = 8,
                Issuer = User1.Address,
                IsBurnable = false,
                IssueChainId = 0,
                ExternalInfo = new ExternalInfo()
            });
            
            var tokenExists = await Assert.ThrowsAsync<Exception>(() => User1SymbolRegistrarContractStub.Buy.SendAsync(new BuyInput
            {
                Symbol  = "LUCK",
                IssueTo = User1.Address
            }));
            tokenExists.Message.ShouldContain("Symbol exists");
        }
        
        
        [Fact]
        public async Task Buy_InvalidSymbol_Fail()
        {
            await InitSeed0();
            await InitElfBalance(User1.Address);

            // approve amount to SymbolRegistrarContract
            await User1TokenContractStub.Approve.SendAsync(new ApproveInput
            {
                Spender = SymbolRegistrarContractAddress,
                Symbol = "ELF",
                Amount = long.MaxValue
            });
            
            
            var symbol = "LUCK";
            var contractNotInit = await Assert.ThrowsAsync<Exception>(() => User1SymbolRegistrarContractStub.Buy.SendAsync(new BuyInput
            {
                Symbol  = symbol,
                IssueTo = User1.Address
            }));
            contractNotInit.Message.ShouldContain("Contract not Initialized");

            await SetSeedsPrice_success();
            
            var invalidSymbolLength = await Assert.ThrowsAsync<Exception>(() => User1SymbolRegistrarContractStub.Buy.SendAsync(new BuyInput
            {
                Symbol  = "abcdefghijklmnopqrstuvwxyzABCDE",
                IssueTo = User1.Address
            }));
            invalidSymbolLength.Message.ShouldContain("Invalid symbol length");

            var invalidFtSymbolPattern = await Assert.ThrowsAsync<Exception>(() => User1SymbolRegistrarContractStub.Buy.SendAsync(new BuyInput
            {
                Symbol  = "aaa-bbb-000",
                IssueTo = User1.Address
            }));
            invalidFtSymbolPattern.Message.ShouldContain("Invalid symbol");

            var invalidFtSymbol = await Assert.ThrowsAsync<Exception>(() => User1SymbolRegistrarContractStub.Buy.SendAsync(new BuyInput
            {
                Symbol  = "aaa",
                IssueTo = User1.Address
            }));
            invalidFtSymbol.Message.ShouldContain("Invalid symbol");

            var invalidNftSymbol = await Assert.ThrowsAsync<Exception>(() => User1SymbolRegistrarContractStub.Buy.SendAsync(new BuyInput
            {
                Symbol  = "AAA-bbb",
                IssueTo = User1.Address
            }));
            invalidNftSymbol.Message.ShouldContain("Invalid nft symbol");

        }


        [Fact]
        public async Task BuyNFT_success()
        {
            await SetSeedsPrice_success();
            await InitSeed0();
            await InitElfBalance(User1.Address);
            await ApproveMaxElfBalance(User1TokenContractStub, SymbolRegistrarContractAddress);

            // To buy 
            var symbol = "LUCK-0";
            var res = await User1SymbolRegistrarContractStub.Buy.SendAsync(new BuyInput
            {
                Symbol = symbol,
                IssueTo = User1.Address
            });
                        
            var seedCreatedLog = res.TransactionResult.Logs.First(log => log.Name.Contains(nameof(SeedCreated)));
            seedCreatedLog.ShouldNotBeNull();
            var seedCreated = SeedCreated.Parser.ParseFrom(seedCreatedLog.NonIndexed);
            seedCreated.To.ShouldBe(User1.Address);
            seedCreated.OwnedSymbol.ShouldBe(symbol);
            var seedSymbol = seedCreated.Symbol;
            seedSymbol.ShouldNotBeEmpty();

            var boughtLog = res.TransactionResult.Logs.First(log => log.Name.Contains(nameof(Bought)));
            boughtLog.ShouldNotBeNull();
            var bought = Bought.Parser.ParseFrom(boughtLog.NonIndexed);
            bought.Symbol.ShouldBe(symbol);
            bought.Price.Symbol.ShouldBe("ELF");
            bought.Price.Amount.ShouldBe(45_0000_0000);
            bought.Buyer.ShouldBe(User1.Address);

            var token = await User1TokenContractStub.GetTokenInfo.CallAsync(new GetTokenInfoInput
            {
                Symbol = seedSymbol
            });
            token.ShouldNotBeNull();
            token.ExternalInfo.Value[SymbolRegistrarContractConstants.SeedOwnedSymbolExternalInfoKey].ShouldBe(symbol);

            var balance = await User1TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = User1.Address,
                Symbol = "SEED-1"
            });
            balance.Balance.ShouldBeGreaterThan(0);
            
            var elfBalance = await User1TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = User1.Address,
                Symbol = "ELF"
            });
            elfBalance.Balance.ShouldBe(9955_0000_0000);
        }


        [Fact]
        public async Task BuyNFT_fail()
        {
            await SetSeedsPrice_success();
            await InitSeed0();
            await InitElfBalance(User1.Address);
            await ApproveMaxElfBalance(User1TokenContractStub, SymbolRegistrarContractAddress);

            // To buy 
            var symbol = "LUCK-1";
            var res = await Assert.ThrowsAsync<Exception>(() => User1SymbolRegistrarContractStub.Buy.SendAsync(new BuyInput
            {
                Symbol = symbol,
                IssueTo = User1.Address
            }));
            res.ShouldNotBeNull();
            res.Message.ShouldContain("Invalid NFT Symbol");
        }

        [Fact]
        public async Task Buy_existsFT_fail()
        {
            await SetSeedsPrice_success();
            await InitSeed0();
            await InitElfBalance(User1.Address);
            await ApproveMaxElfBalance(User1TokenContractStub, SymbolRegistrarContractAddress);

            // To buy 
            var symbol = "ELF";
            var res = await Assert.ThrowsAsync<Exception>(() => User1SymbolRegistrarContractStub.Buy.SendAsync(new BuyInput
            {
                Symbol = symbol,
                IssueTo = User1.Address
            }));
            res.ShouldNotBeNull();
            res.Message.ShouldContain("Symbol exists");
        }

    }
}
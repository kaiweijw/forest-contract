using System;
using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.Kernel.Token;
using AElf.Types;
using Shouldly;
using Xunit;

namespace Forest.SymbolRegistrar
{
    public class SymbolRegistrarContractTests_Buy : SymbolRegistrarContractTests
    {
        
        [Fact]
        public async Task Buy_success()
        {
            await SetSeedsPrice_success();
            await InitSeed();
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

            var token = await User1TokenContractStub.GetTokenInfo.CallAsync(new GetTokenInfoInput
            {
                Symbol = seedSymbol
            });
            token.ShouldNotBeNull();
            token.ExternalInfo.Value[SymbolRegistrarContractConstants.SeedOwnedSymbolExternalInfoKey].ShouldBe(symbol);
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

        }
        
        
        [Fact]
        public async Task Buy_InvalidSymbol_Fail()
        {
            await InitSeed();
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
        
        
    }
}
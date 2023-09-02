using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.Types;
using Shouldly;
using Xunit;

namespace Forest.SymbolRegistrar
{
    public class SymbolRegistrarContractTests_Seed : SymbolRegistrarContractTests
    {
        
        private async Task InitSeed()
        {
            await AdminTokenContractStub.Create.SendAsync(
                new CreateInput
                {
                    Owner = ProxyAccountAddress,
                    Issuer = ProxyAccountAddress,
                    Symbol = "SEED-0",
                    TokenName = "TOKEN SEED-0",
                    TotalSupply = 1,
                    Decimals = 0,
                    IsBurnable = false,
                    LockWhiteList = { TokenContractAddress }
                });
        }
    
        [Fact]
        public async Task CreateSeedTest_Success()
        {
            await InitializeContract();
            var result = await AdminSaleContractStub.CreateSeed.SendWithExceptionAsync(new CreateSeedInput
            {
                Symbol = "LUCK",
                To = User1.Address
            });
            result.TransactionResult.Error.ShouldContain("seedCollection not existed");
            await InitSeed();
            result = await AdminSaleContractStub.CreateSeed.SendAsync(new CreateSeedInput
            {
                Symbol = "LUCK",
                To = User1.Address
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var seedCreated =
                SeedCreated.Parser.ParseFrom(result.TransactionResult.Logs.First(e => e.Name == nameof(SeedCreated))
                    .NonIndexed);
            seedCreated.Symbol.ShouldBe("SEED-1");
            seedCreated.To.ShouldBe(User1.Address);
            seedCreated.OwnedSymbol.ShouldBe("LUCK");
        
            result = await AdminSaleContractStub.CreateSeed.SendWithExceptionAsync(new CreateSeedInput
            {
                Symbol = "LUCK",
                To = User1.Address
            });
            result.TransactionResult.Error.ShouldContain("symbol seed existed");
        }
        
    }
}
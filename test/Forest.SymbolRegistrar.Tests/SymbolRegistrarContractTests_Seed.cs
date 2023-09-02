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
        
        [Fact]
        public async Task CreateSeedTest_Success()
        {
            await InitializeContract();
            var result = await AdminSymbolRegistrarContractStub.CreateSeed.SendWithExceptionAsync(new CreateSeedInput
            {
                Symbol = "LUCK",
                To = User1.Address
            });
            result.TransactionResult.Error.ShouldContain("seedCollection not existed");
            await InitSeed();
            result = await AdminSymbolRegistrarContractStub.CreateSeed.SendAsync(new CreateSeedInput
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
        
            result = await AdminSymbolRegistrarContractStub.CreateSeed.SendWithExceptionAsync(new CreateSeedInput
            {
                Symbol = "LUCK",
                To = User1.Address
            });
            result.TransactionResult.Error.ShouldContain("symbol seed existed");
        }
        
    }
}
using System.Threading.Tasks;
using AElf.Types;
using Xunit;

namespace Forest.SymbolRegistrar
{
    public class SymbolRegistrarContractTests : SymbolRegistrarContractTestBase
    {
        [Fact]
        public async Task InitializeContract()
        {
            await AdminSaleContractStub.Initialize.SendAsync(new InitializeInput()
            {
                ReceivingAccount = Admin.Address,
                ProxyAccountAddress = ProxyAccountAddress,
                AuctionContractAddress = Admin.Address
            });
        }

        public async Task InitSaleController(Address address)
        {
            await AdminSaleContractStub.AddSaleController.SendAsync(new AddSaleControllerInput()
            {
                Addresses = new ControllerList
                {
                    Controllers = { address }
                }
            });
        }

        internal SpecialSeed _specialUsd = new()
        {
            SeedType = SeedType.Unique,
            Symbol = "USD",
            PriceSymbol = "ELF",
            PriceAmount = 100_0000_0000,
        };

        internal SpecialSeed _specialUsd_errorPrice = new()
        {
            SeedType = SeedType.Unique,
            Symbol = "USD",
            PriceSymbol = "ELFF",
            PriceAmount = 100_0000_0000,
        };

        internal SpecialSeed _specialBtc = new()
        {
            SeedType = SeedType.Notable,
            Symbol = "BTC",
            PriceSymbol = "ELF",
            PriceAmount = 10_0000_0000_0000,
            IssueChain = "BTC",
            IssueChainContractAddress = "0x0000000000000000000000",
        };

        internal SpecialSeed _specialEth = new()
        {
            SeedType = SeedType.Notable,
            Symbol = "ETH",
            PriceSymbol = "ELF",
            PriceAmount = 1000_0000_0000,
            IssueChain = "ETH",
            IssueChainContractAddress = "0x0000000000000000000000",
            ExternalInfo = { ["aaa"] = "bbb" }
        };

        internal SpecialSeed _specialEth_noIssueChainId = new()
        {
            SeedType = SeedType.Notable,
            Symbol = "ETH",
            PriceSymbol = "ELF",
            PriceAmount = 1000_0000_0000,
            IssueChainContractAddress = "0x0000000000000000000000",
            ExternalInfo = { ["aaa"] = "bbb" }
        };

        internal SpecialSeed _specialEth_noIssueChainContractAddress = new()
        {
            SeedType = SeedType.Notable,
            Symbol = "ETH",
            PriceSymbol = "ELF",
            PriceAmount = 1000_0000_0000,
            IssueChain = "ETH",
            ExternalInfo = { ["aaa"] = "bbb" }
        };
    }
}
using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Forest.Contracts.SymbolRegistrar
{
    public class SymbolRegistrarContractTests : SymbolRegistrarContractTestBase
    {
        public async Task InitSaleController(Address address)
        {
            await AdminSymbolRegistrarContractStub.AddSaleController.SendAsync(new AddSaleControllerInput()
            {
                Addresses = new ControllerList
                {
                    Controllers = { address }
                }
            });
        }
        
        [Fact]
        public async Task InitializeContract()
        {
            await AdminSymbolRegistrarContractStub.Initialize.SendAsync(new InitializeInput()
            {
                ReceivingAccount = Admin.Address,
                ProxyAccountAddress = ProxyAccountAddress
            });
        }
        
        [Fact]
        public async Task SetSeedsPrice_success()
        {
            await InitializeContractIfNecessary();
            
            var result = await AdminSymbolRegistrarContractStub.SetSeedsPrice.SendAsync(new SeedsPriceInput
            {
                FtPriceList = MockPriceList(),
                NftPriceList = MockPriceList()
            });
            
            var log = result.TransactionResult.Logs.First(log => log.Name.Contains(nameof(SeedsPriceChanged)));
            var seedsPriceChanged = SeedsPriceChanged.Parser.ParseFrom(log.NonIndexed);
            seedsPriceChanged.NftPriceList.Value.Count.ShouldBe(30);
            seedsPriceChanged.FtPriceList.Value.Count.ShouldBe(30);
            
            var priceList = await AdminSymbolRegistrarContractStub.GetSeedsPrice.CallAsync(new Empty());
            priceList.FtPriceList.Value.Count.ShouldBe(30);
            priceList.NftPriceList.Value.Count.ShouldBe(30);

        }
        
        [Fact]
        public async Task SetSpecialSeed_byProposal()
        {
            await InitializeContractIfNecessary();

            // create proposal and approve
            var result = await SubmitAndApproveProposalOfDefaultParliament(SymbolRegistrarContractAddress, "AddSpecialSeeds",
                new SpecialSeedList
                {
                    Value = { _specialUsd, _specialEth }
                });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

            // logs
            var logEvent = result.TransactionResult.Logs.First(log => log.Name.Contains(nameof(SpecialSeedAdded)));
            var specialSeedAdded = SpecialSeedAdded.Parser.ParseFrom(logEvent.NonIndexed);
            specialSeedAdded.AddList.Value.Count.ShouldBe(2);

            // query seed list and verify
            var seedUsd = await AdminSymbolRegistrarContractStub.GetSpecialSeed.CallAsync(new StringValue
            {
                Value = _specialUsd.Symbol
            });
            seedUsd.Symbol.ShouldBe(_specialUsd.Symbol);


            var seedEth = await AdminSymbolRegistrarContractStub.GetSpecialSeed.CallAsync(new StringValue
            {
                Value = _specialEth.Symbol
            });
            seedEth.Symbol.ShouldBe(_specialEth.Symbol);
        }


        internal async Task InitElfBalance(Address to, long amount = 10000_0000_0000)
        {
            await AdminTokenContractStub.Transfer.SendAsync(new TransferInput
            {
                Symbol = "ELF",
                Amount = amount,
                To = to
            });
            
        }

        internal async Task InitializeContractIfNecessary()
        {
            var config = await AdminSymbolRegistrarContractStub.GetBizConfig.CallAsync(new Empty());
            if (config.AdministratorAddress == null) await InitializeContract();
        }

        internal async Task ApproveMaxElfBalance(TokenContractContainer.TokenContractStub userTokenStub, Address spender)
        {
            // approve amount to SymbolRegistrarContract
            await userTokenStub.Approve.SendAsync(new ApproveInput
            {
                Spender = spender,
                Symbol = "ELF",
                Amount = long.MaxValue
            });
        }

        internal async Task InitSeed0()
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

        
        internal static PriceList MockPriceList()
        {
            var priceList = new PriceList();
            for (var i = 0 ; i < 30 ; i ++)
            {
                priceList.Value.Add(new PriceItem
                {
                    SymbolLength = i + 1,
                    Symbol = "ELF",
                    Amount = 50_0000_0000 - i * 1_0000_0000
                });
            }
            return priceList;
        }

        internal SpecialSeed SpecialSeed(string symbol, SeedType seedType, string priceSymbol, long priceAmount)
        {
            return new SpecialSeed
            {
                SeedType = seedType,
                Symbol = symbol,
                PriceSymbol = priceSymbol,
                PriceAmount = priceAmount,
                AuctionType = AuctionType.English
            };
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
        
        internal SpecialSeed _specialLongName = new()
        {
            SeedType = SeedType.Notable,
            Symbol = "ABCDEFGHIJKLMNOPQRSTUVWXYZABCDE",
            PriceSymbol = "ELF",
            PriceAmount = 1000_0000_0000,
            IssueChain = "ETH",
            ExternalInfo = { ["aaa"] = "bbb" }
        };
        
        internal SpecialSeed _specialInvalidSymbol = new()
        {
            SeedType = SeedType.Notable,
            Symbol = "abcabc",
            PriceSymbol = "ELF",
            PriceAmount = 1000_0000_0000,
            IssueChain = "ETH",
            ExternalInfo = { ["aaa"] = "bbb" }
        };
        
        internal SpecialSeed _specialInvalidNftSymbol = new()
        {
            SeedType = SeedType.Notable,
            Symbol = "ABC-abc",
            PriceSymbol = "ELF",
            PriceAmount = 1000_0000_0000,
            IssueChain = "ETH",
            ExternalInfo = { ["aaa"] = "bbb" }
        };
        internal SpecialSeed _specialInvalidPriceAmount = new()
        {
            SeedType = SeedType.Notable,
            Symbol = "ABC",
            PriceSymbol = "ELF",
            PriceAmount = -1,
            IssueChain = "ETH",
            ExternalInfo = { ["aaa"] = "bbb" }
        };
    }
}
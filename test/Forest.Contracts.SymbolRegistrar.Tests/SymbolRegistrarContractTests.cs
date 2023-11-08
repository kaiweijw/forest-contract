using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.Association;
using AElf.Contracts.MultiToken;
using AElf.Standards.ACS3;
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
            var organizationMemberList = new OrganizationMemberList();
            var proposerWhiteList = new ProposerWhiteList();
            organizationMemberList.OrganizationMembers.Add(User1.Address);
            organizationMemberList.OrganizationMembers.Add(User2.Address);
            organizationMemberList.OrganizationMembers.Add(User3.Address);
            proposerWhiteList.Proposers.Add(User1.Address);
            proposerWhiteList.Proposers.Add(User2.Address);
            proposerWhiteList.Proposers.Add(User3.Address);
            var createOrganizationInput = new CreateOrganizationInput
            {
                OrganizationMemberList = organizationMemberList,
                ProposerWhiteList = proposerWhiteList,
                CreationToken = HashHelper.ComputeFrom("InitializeContract"),
                ProposalReleaseThreshold =  new ProposalReleaseThreshold
                {
                    MaximalRejectionThreshold = 1,
                    MinimalApprovalThreshold = 2,
                    MaximalAbstentionThreshold = 0,
                    MinimalVoteThreshold = 2
                },
            };
            var organizationAddress = await User1AssociationContractStub.CreateOrganization.SendAsync(createOrganizationInput);
            await AdminSymbolRegistrarContractStub.Initialize.SendAsync(new InitializeInput()
            {
                ReceivingAccount = Admin.Address,
                ProxyAccountContractAddress = ProxyAccountContractAddress,
                AdministratorAddress = Admin.Address,
                AssociateOrganizationAddress = organizationAddress.Output
            });
            await AdminSymbolRegistrarContractStub.AddIssueChain.SendAsync(new IssueChainList
            {
                IssueChain = { "ETH", "BTC" }
            });
        }
        
        async Task<Dictionary<AElf.Types.Address, List<AssociationContractImplContainer.AssociationContractImplStub>>> InitializeAssociateOrganizationAsync()
        {
            var dictionary = new Dictionary<Address, List<AssociationContractImplContainer.AssociationContractImplStub>>();
            await InitializeContract(); 
            var associateOrganization = await User1SymbolRegistrarContractStub.GetAssociateOrganization.CallAsync(new Empty());
            var stubs = new List<AssociationContractImplContainer.AssociationContractImplStub>();
            stubs.Add(User1AssociationContractStub); 
            stubs.Add(User2AssociationContractStub); 
            stubs.Add(User3AssociationContractStub);
            dictionary.Add(associateOrganization, stubs);
            return dictionary;
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
            var config = await AdminSymbolRegistrarContractStub.GetAdministratorAddress.CallAsync(new Empty());
            if (config.Value.IsEmpty) await InitializeContract();
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
                    Owner = ProxyAccountContractAddress,
                    Issuer = ProxyAccountContractAddress,
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

        
        internal SpecialSeed _specialEur = new()
        {
            SeedType = SeedType.Unique,
            Symbol = "EUR",
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
            IssueChainContractAddress = "0x0000000000000000000000",
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
            IssueChainContractAddress = "0x0000000000000000000000",
            ExternalInfo = { ["aaa"] = "bbb" }
        };
        internal SpecialSeed _specialInvalidPriceAmount = new()
        {
            SeedType = SeedType.Notable,
            Symbol = "ABC",
            PriceSymbol = "ELF",
            PriceAmount = -1,
            IssueChain = "ETH",
            IssueChainContractAddress = "0x0000000000000000000000",
            ExternalInfo = { ["aaa"] = "bbb" }
        };
    }
}
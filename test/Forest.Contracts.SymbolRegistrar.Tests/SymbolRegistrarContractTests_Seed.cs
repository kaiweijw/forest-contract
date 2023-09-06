using System;
using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.Contracts.ProxyAccountContract;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;
using CreateInput = AElf.Contracts.MultiToken.CreateInput;

namespace Forest.Contracts.SymbolRegistrar
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
            await InitSeed();
            var result = await AdminSymbolRegistrarContractStub.CreateSeed.SendWithExceptionAsync(new CreateSeedInput()
            {
                Symbol = "LUCK",
                Issuer = Admin.Address
            });
            result.TransactionResult.Error.ShouldContain("No sale controller permission.");
            await InitSaleController(Admin.Address);
            result = await AdminSymbolRegistrarContractStub.CreateSeed.SendAsync(new CreateSeedInput()
            {
                Symbol = "LUCK",
                Issuer = Admin.Address
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var tokenCreated =
                TokenCreated.Parser.ParseFrom(result.TransactionResult.Logs.First(e => e.Name == nameof(TokenCreated))
                    .NonIndexed);
            tokenCreated.Symbol.ShouldBe("SEED-1");
        }
        
        [Fact]
        public async Task IssueSeedTest_Success()
        {
            await InitializeContract();
            await InitSeed();
            var result = await AdminSymbolRegistrarContractStub.IssueSeed.SendWithExceptionAsync(new IssueSeedInput
            {
                Symbol = "LUCK",
                To = User1.Address
            });
            result.TransactionResult.Error.ShouldContain("No sale controller permission.");
            await InitSaleController(Admin.Address);
            result = await AdminSymbolRegistrarContractStub.IssueSeed.SendAsync(new IssueSeedInput
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
        }
        
        [Fact]
        public async Task IssueSeedTest_Fail()
        {
            await InitializeContract();
            await InitSaleController(Admin.Address);
            var result = await AdminSymbolRegistrarContractStub.IssueSeed.SendWithExceptionAsync(new IssueSeedInput()
            {
                Symbol = "LUCK",
                To = User1.Address
            });
            result.TransactionResult.Error.ShouldContain("seedCollection not existed");
            await InitSeed();
            await AdminSymbolRegistrarContractStub.IssueSeed.SendAsync(new IssueSeedInput
            {
                Symbol = "LUCK",
                To = User1.Address
            });

            result = await AdminSymbolRegistrarContractStub.IssueSeed.SendWithExceptionAsync(new IssueSeedInput
            {
                Symbol = "LUCK",
                To = User1.Address
            });
            result.TransactionResult.Error.ShouldContain("symbol seed existed");
            result = await AdminSymbolRegistrarContractStub.IssueSeed.SendWithExceptionAsync(new IssueSeedInput
            {
                Symbol = "LUCK-0",
                To = User1.Address
            });
            result.TransactionResult.Error.ShouldContain("symbol seed existed");
        }
        
        [Fact]
        public async Task IssueSeedTest_TokenExisted_Fail()
        {
            await InitializeContract();
            await InitSaleController(Admin.Address);
            await InitSeed();
            var createInput = new CreateInput
            {
                Symbol = "SEED-1",
                TokenName = "TOKEN SEED-1",
                Decimals = 0,
                IsBurnable = true,
                TotalSupply = 1,
                Owner = ProxyAccountAddress,
                Issuer = Admin.Address,
                ExternalInfo = new ExternalInfo(),
                LockWhiteList = { TokenContractAddress }
            };
            
            createInput.ExternalInfo.Value[SymbolRegistrarContractConstants.SeedOwnedSymbolExternalInfoKey] = "LUCK";
            var expireTime = DateTime.UtcNow.ToTimestamp().Seconds + 1000000000;
            createInput.ExternalInfo.Value[SymbolRegistrarContractConstants.SeedExpireTimeExternalInfoKey] = expireTime.ToString();
            
            await AdminProxyAccountContractStubContractStub.ForwardCall.SendAsync(
                new ForwardCallInput()
                {
                    ContractAddress = TokenContractAddress,
                    MethodName = nameof(AdminTokenContractStub.Create),
                    Args = createInput.ToByteString()
                });
            await AdminTokenContractStub.Issue.SendAsync(
                new IssueInput
                {
                    Amount = 1,
                    Symbol = "SEED-1",
                    To = User1.Address
                });
            await User1TokenContractStub.Create.SendAsync(
                new CreateInput
                {
                    Symbol = "LUCK",
                    TokenName = "TOKEN LUCK",
                    Decimals = 0,
                    IsBurnable = true,
                    TotalSupply = 1,
                    Owner = Admin.Address,
                    Issuer = Admin.Address,
                    ExternalInfo = new ExternalInfo(),
                    LockWhiteList = { TokenContractAddress }
                });
            var result = await AdminSymbolRegistrarContractStub.IssueSeed.SendWithExceptionAsync(new IssueSeedInput
            {
                Symbol = "LUCK",
                To = User1.Address
            });
            result.TransactionResult.Error.ShouldContain("Token already exists.");
        }
        
         [Fact]
        public async Task IssueSeedTest_SeedIndex()
        {
            await InitializeContract();
            await InitSeed();
            await InitSaleController(Admin.Address);
            for (int i = 1; i <= 31; i++)
            {
                var createInput = new CreateInput
                {
                    Symbol = "SEED-" + i,
                    TokenName = "TOKEN SEED-" + i,
                    Decimals = 0,
                    IsBurnable = true,
                    TotalSupply = 1,
                    Owner = ProxyAccountAddress,
                    Issuer = Admin.Address,
                    ExternalInfo = new ExternalInfo(),
                    LockWhiteList = { TokenContractAddress }
                };
                var c = 'A' + (i-1) % 26;

                if (i > 26)
                {
                    createInput.ExternalInfo.Value[SymbolRegistrarContractConstants.SeedOwnedSymbolExternalInfoKey] = "LUCKA" + (char)c;
                }
                else
                {
                    createInput.ExternalInfo.Value[SymbolRegistrarContractConstants.SeedOwnedSymbolExternalInfoKey] = "LUCK" + (char)c;
                }
                
                var expireTime = DateTime.UtcNow.ToTimestamp().Seconds + 1000000000;
                createInput.ExternalInfo.Value[SymbolRegistrarContractConstants.SeedExpireTimeExternalInfoKey] = expireTime.ToString();
            
                await AdminProxyAccountContractStubContractStub.ForwardCall.SendAsync(
                    new ForwardCallInput()
                    {
                        ContractAddress = TokenContractAddress,
                        MethodName = nameof(AdminTokenContractStub.Create),
                        Args = createInput.ToByteString()
                    });
            }
            
            var result = await AdminSymbolRegistrarContractStub.IssueSeed.SendAsync(new IssueSeedInput
            {
                Symbol = "LUCK",
                To = User1.Address
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            result.TransactionResult.Logs.Count.ShouldBe(0);
            result = await AdminSymbolRegistrarContractStub.IssueSeed.SendAsync(new IssueSeedInput
            {
                Symbol = "LUCK",
                To = User1.Address
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var seedCreated =
                SeedCreated.Parser.ParseFrom(result.TransactionResult.Logs.First(e => e.Name == nameof(SeedCreated))
                    .NonIndexed);
            seedCreated.Symbol.ShouldBe("SEED-32");
            seedCreated.To.ShouldBe(User1.Address);
            seedCreated.OwnedSymbol.ShouldBe("LUCK");
        }
        
    }
}
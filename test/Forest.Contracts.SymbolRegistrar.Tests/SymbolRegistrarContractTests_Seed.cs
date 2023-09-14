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

        private async Task InitializeContractWithSpecialSeed()
        {
            await AdminSymbolRegistrarContractStub.Initialize.SendAsync(new InitializeInput()
            {
                ReceivingAccount = Admin.Address,
                ProxyAccountAddress = ProxyAccountAddress,
                SpecialSeeds = new SpecialSeedList()
                {
                    Value = { new SpecialSeed()
                    {
                        Symbol = "LUCK",
                        SeedType = SeedType.Disable,
                        PriceSymbol = "ELF",
                        PriceAmount = 100
                    } }
                }
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
                To = User1.Address
            });
            result.TransactionResult.Error.ShouldContain("No sale controller permission.");
            await InitSaleController(Admin.Address);
            await AdminSymbolRegistrarContractStub.SetSeedImageUrlPrefix.SendAsync(new StringValue()
            {
                Value = "http://www.aws.com/"
            });
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
            seedCreated.ImageUrl.ShouldBe("http://www.aws.com/SEED-1.svg");
        }

        [Fact]
        public async Task LastSeedId_Success()
        {
            await InitializeContract();
            await InitSeed();
            var lastSeedId = await AdminSymbolRegistrarContractStub.GetLastSeedId.CallAsync(new Empty());
            lastSeedId.Value.ShouldBe(0);
            await AdminSymbolRegistrarContractStub.SetLastSeedId.SendAsync(new Int64Value()
            {
                Value = 1
            });
            lastSeedId = await AdminSymbolRegistrarContractStub.GetLastSeedId.CallAsync(new Empty());
            lastSeedId.Value.ShouldBe(1);
            await InitSaleController(Admin.Address);
            await AdminSymbolRegistrarContractStub.CreateSeed.SendAsync(new CreateSeedInput
            {
                Symbol = "LUCK",
                To = User1.Address
            });
            lastSeedId = await AdminSymbolRegistrarContractStub.GetLastSeedId.CallAsync(new Empty());
            lastSeedId.Value.ShouldBe(2);
        }

        [Fact]
        public async Task CreateSeedTest_SeedNotSupport_Fail()
        {
            await InitializeContractWithSpecialSeed();
            await InitSaleController(Admin.Address);
            await InitSeed();
            var result = await AdminSymbolRegistrarContractStub.CreateSeed.SendWithExceptionAsync(new CreateSeedInput()
            {
                Symbol = "LUCK",
                To = User1.Address
            });
            result.TransactionResult.Error.ShouldContain("Seed LUCK not support create.");
        }

        [Fact]
        public async Task CreateSeedTest_InvalidParam_Fail()
        {
            await InitializeContract();
            await InitSaleController(Admin.Address);
            var result = await AdminSymbolRegistrarContractStub.CreateSeed.SendWithExceptionAsync(new CreateSeedInput
            {
                To = User1.Address
            });
            result.TransactionResult.Error.ShouldContain("Invalid Seed Symbol input");

            result = await AdminSymbolRegistrarContractStub.CreateSeed.SendWithExceptionAsync(new CreateSeedInput
            {
                Symbol = "",
                To = User1.Address
            });
            result.TransactionResult.Error.ShouldContain("Invalid Seed Symbol input");

            result = await AdminSymbolRegistrarContractStub.CreateSeed.SendWithExceptionAsync(new CreateSeedInput
            {
                Symbol = "LUCK-2",
                To = User1.Address
            });
            result.TransactionResult.Error.ShouldContain("Invalid Seed NFT Symbol input");

            result = await AdminSymbolRegistrarContractStub.CreateSeed.SendWithExceptionAsync(new CreateSeedInput
            {
                Symbol = "LUCK"
            });
            result.TransactionResult.Error.ShouldContain("To address is empty");
        }

        [Fact]
        public async Task CreateSeedTest_Fail()
        {
            await InitializeContract();
            await InitSaleController(Admin.Address);
            var result = await AdminSymbolRegistrarContractStub.CreateSeed.SendWithExceptionAsync(new CreateSeedInput()
            {
                Symbol = "LUCK",
                To = User1.Address
            });
            result.TransactionResult.Error.ShouldContain("seedCollection not existed");
            await InitSeed();
            await AdminSymbolRegistrarContractStub.CreateSeed.SendAsync(new CreateSeedInput
            {
                Symbol = "LUCK",
                To = User1.Address
            });

            result = await AdminSymbolRegistrarContractStub.CreateSeed.SendWithExceptionAsync(new CreateSeedInput
            {
                Symbol = "LUCK",
                To = User1.Address
            });
            result.TransactionResult.Error.ShouldContain("symbol seed existed");
            result = await AdminSymbolRegistrarContractStub.CreateSeed.SendWithExceptionAsync(new CreateSeedInput
            {
                Symbol = "LUCK-0",
                To = User1.Address
            });
            result.TransactionResult.Error.ShouldContain("symbol seed existed");
        }
        
        [Fact]
        public async Task CreateSeedTest_TokenExisted_Fail()
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
            var result = await AdminSymbolRegistrarContractStub.CreateSeed.SendWithExceptionAsync(new CreateSeedInput
            {
                Symbol = "LUCK",
                To = User1.Address
            });
            result.TransactionResult.Error.ShouldContain("Token already exists.");
        }
        
         [Fact]
        public async Task CreateSeedTest_SeedIndex()
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
            
            var result = await AdminSymbolRegistrarContractStub.CreateSeed.SendAsync(new CreateSeedInput
            {
                Symbol = "LUCK",
                To = User1.Address
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            result.TransactionResult.Logs.Count.ShouldBe(0);
            result = await AdminSymbolRegistrarContractStub.CreateSeed.SendAsync(new CreateSeedInput
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

        [Fact]
        public async Task SetProxyAccountContract_Test()
        {
            await InitializeContract();
            var result = await User1SymbolRegistrarContractStub.SetProxyAccountContract.SendWithExceptionAsync(ProxyAccountAddress);
            result.TransactionResult.Error.ShouldContain("No permission.");
            result = await AdminSymbolRegistrarContractStub.SetProxyAccountContract.SendWithExceptionAsync(new Address());
            result.TransactionResult.Error.ShouldContain("Invalid param");
            result = await AdminSymbolRegistrarContractStub.SetProxyAccountContract.SendAsync(User2.Address);
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var address = await AdminSymbolRegistrarContractStub.GetProxyAccountContract.CallAsync(new Empty());
            address.ShouldBe(User2.Address);
        }

        [Fact]
        public async Task SetSeedImageUrlPrefix_Test()
        {
            await InitializeContract();
            var result = await User1SymbolRegistrarContractStub.SetSeedImageUrlPrefix.SendWithExceptionAsync(new StringValue()
            {
                Value = "http://www.aws.com"
            });
            result.TransactionResult.Error.ShouldContain("No permission.");
            result = await AdminSymbolRegistrarContractStub.SetSeedImageUrlPrefix.SendWithExceptionAsync(new StringValue());
            result.TransactionResult.Error.ShouldContain("Invalid param");
            result = await AdminSymbolRegistrarContractStub.SetSeedImageUrlPrefix.SendAsync(new StringValue()
            {
                Value = "http://www.aws.com"
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var seedImageUrlPrefix = await AdminSymbolRegistrarContractStub.GetSeedImageUrlPrefix.CallAsync(new Empty());
            seedImageUrlPrefix.Value.ShouldBe("http://www.aws.com");
        }
    }
}
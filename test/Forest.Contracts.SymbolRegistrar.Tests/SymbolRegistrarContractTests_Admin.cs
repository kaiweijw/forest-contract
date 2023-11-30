using System;
using System.Linq;
using System.Threading.Tasks;
using AElf.Types;
using Forest.Contracts.SymbolRegistrar.Helper;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Forest.Contracts.SymbolRegistrar
{
    public class SymbolRegistrarContractTests_Admin : SymbolRegistrarContractTests
    {
        [Fact]
        public async Task InitTest_view()
        {
            await InitializeContract();

            var adminAddress = await AdminSymbolRegistrarContractStub.GetAdministratorAddress.CallAsync(new Empty());
            adminAddress.ShouldBe(Admin.Address);
            
            var receivingAddress = await AdminSymbolRegistrarContractStub.GetReceivingAccountAddress.CallAsync(new Empty());
            receivingAddress.ShouldBe(Admin.Address);
        }

        [Fact]
        public async Task InitTest_Fail()
        {
            // Invalid administrator address
            var emptyAdminAddressException = await Assert.ThrowsAsync<Exception>(() =>
                AdminSymbolRegistrarContractStub.Initialize.SendAsync(
                    new InitializeInput()
                    {
                        ReceivingAccount = Admin.Address,
                        AdministratorAddress = new Address()
                    }));
            emptyAdminAddressException.ShouldNotBeNull();
            emptyAdminAddressException.Message.ShouldContain("Invalid administrator address");

            // invalid param
            var exception2 = await Assert.ThrowsAsync<Exception>(() =>
                AdminSymbolRegistrarContractStub.Initialize.SendAsync(
                    new InitializeInput()
                    {
                        // no param
                    }));
            exception2.ShouldNotBeNull();
            exception2.Message.ShouldContain("PaymentReceiverAddress required");

            // invalid param
            var exception3 = await Assert.ThrowsAsync<Exception>(() =>
                AdminSymbolRegistrarContractStub.Initialize.SendAsync(
                    new InitializeInput()
                    {
                        ReceivingAccount = new Address()
                    }));
            exception3.ShouldNotBeNull();
            exception3.Message.ShouldContain("PaymentReceiverAddress required");
            
            // invalid param
            var result = await AdminSymbolRegistrarContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
            {
                ReceivingAccount = Admin.Address
            });
            result.TransactionResult.Error.ShouldContain("ProxyAccountContractAddress required.");

            // invalid param
            result = await AdminSymbolRegistrarContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
            {
                ReceivingAccount = Admin.Address,
                ProxyAccountContractAddress = new Address()
            });
            result.TransactionResult.Error.ShouldContain("ProxyAccountContractAddress required.");

            // success
            await InitializeContract();

            // has bean init
            var exception4 = await Assert.ThrowsAsync<Exception>(InitializeContract);
            exception4.ShouldNotBeNull();
            exception4.Message.ShouldContain("has bean Initialized");
        }

        [Fact]
        public async Task SetTest_success()
        {
            var exception1 = await Assert.ThrowsAsync<Exception>(() =>
                AdminSymbolRegistrarContractStub.SetReceivingAccount.SendAsync(User2.Address));
            exception1.ShouldNotBeNull();
            exception1.Message.ShouldContain("Contract not Initialized");

            await InitTest_view();

            var exception2 = await Assert.ThrowsAsync<Exception>(() =>
                AdminSymbolRegistrarContractStub.SetAdmin.SendAsync(new Address()));
            exception2.ShouldNotBeNull();
            exception2.Message.ShouldContain("Invalid param");

            var exception3 = await Assert.ThrowsAsync<Exception>(() =>
                AdminSymbolRegistrarContractStub.SetReceivingAccount.SendAsync(new Address()));
            exception3.ShouldNotBeNull();
            exception3.Message.ShouldContain("Invalid param");

            var exception4 = await User1SymbolRegistrarContractStub.SetReceivingAccount.SendWithExceptionAsync(new Address(User1.Address));
            exception4.TransactionResult.Error.ShouldContain("No permission");

            await AdminSymbolRegistrarContractStub.SetReceivingAccount.SendAsync(User2.Address);
            await AdminSymbolRegistrarContractStub.SetAdmin.SendAsync(User1.Address);

            var adminAddress = await AdminSymbolRegistrarContractStub.GetAdministratorAddress.CallAsync(new Empty());
            adminAddress.ShouldBe(User1.Address);

            var receivingAddress = await AdminSymbolRegistrarContractStub.GetReceivingAccountAddress.CallAsync(new Empty());
            receivingAddress.ShouldBe(User2.Address);
        }

        [Fact]
        public async Task InitWithSpecialSeed_success()
        {
            var result = await AdminSymbolRegistrarContractStub.Initialize.SendAsync(new InitializeInput()
            {
                ReceivingAccount = Admin.Address,
                ProxyAccountContractAddress = Admin.Address,
                SpecialSeeds = new SpecialSeedList
                {
                    Value = { _specialUsd, _specialEur }
                }
            });

            await AdminSymbolRegistrarContractStub.AddIssueChain.SendAsync(new IssueChainList
            {
                IssueChain = { "BTC", "ETH" }
            });

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


            var seedEru = await AdminSymbolRegistrarContractStub.GetSpecialSeed.CallAsync(new StringValue
            {
                Value = _specialEur.Symbol
            });
            seedEru.Symbol.ShouldBe(_specialEur.Symbol);
        }
        
        [Fact]
        public async Task InitWithSpecialSeed_Fail()
        {
            var result = await AdminSymbolRegistrarContractStub.Initialize.SendWithExceptionAsync(new InitializeInput()
            {
                ReceivingAccount = Admin.Address,
                ProxyAccountContractAddress = Admin.Address,
                SpecialSeeds = new SpecialSeedList
                {
                    Value = { _specialUsd, _specialUsd }
                }
            });
            result.TransactionResult.Error.ShouldContain("Duplicate symbol");

            result = await AdminSymbolRegistrarContractStub.Initialize.SendWithExceptionAsync(new InitializeInput()
            {
                ReceivingAccount = Admin.Address,
                ProxyAccountContractAddress = Admin.Address,
                SpecialSeeds = new SpecialSeedList
                {
                    Value = { _specialInvalidPriceAmount }
                }
            });
            result.TransactionResult.Error.ShouldContain("Invalid price amount");
            result = await AdminSymbolRegistrarContractStub.Initialize.SendWithExceptionAsync(new InitializeInput()
            {
                ReceivingAccount = Admin.Address,
                ProxyAccountContractAddress = Admin.Address,
                SpecialSeeds = new SpecialSeedList
                {
                    Value = { _specialUsd_errorPrice }
                }
            });
            result.TransactionResult.Error.ShouldContain("Price token " + _specialUsd_errorPrice.PriceSymbol + " not exists");
            
            const int length = 501;
            var batchSpecialSeedList = new SpecialSeedList();
            for (var i = 0; i < length; i++)
            {
                batchSpecialSeedList.Value.Add(SpecialSeed(BaseEncodeHelper.Base26(i), SeedType.Unique, "ELF",
                    100_0000_0000));
            }

            var maxLimitExceeded = await AdminSymbolRegistrarContractStub.Initialize.SendWithExceptionAsync(new InitializeInput()
            {
                ReceivingAccount = Admin.Address,
                ProxyAccountContractAddress = Admin.Address,
                SpecialSeeds = batchSpecialSeedList
            });
            maxLimitExceeded.TransactionResult.Error.ShouldContain("max limit exceeded");
            
            batchSpecialSeedList.Value.RemoveAt(500);
            await AdminSymbolRegistrarContractStub.Initialize.SendAsync(new InitializeInput()
            {
                ReceivingAccount = Admin.Address,
                ProxyAccountContractAddress = Admin.Address,
                SpecialSeeds = batchSpecialSeedList
            });
        }

        [Fact]
        public async Task InitWithSeedPriceTests()
        {
            var result = await AdminSymbolRegistrarContractStub.Initialize.SendAsync(new InitializeInput()
            {
                ReceivingAccount = Admin.Address,
                ProxyAccountContractAddress = Admin.Address,
                SeedsPrices = new SeedsPriceInput
                {
                    NftPriceList = MockPriceList(),
                    FtPriceList = MockPriceList()
                }
            });
            var log = result.TransactionResult.Logs.First(log => log.Name.Contains(nameof(SeedsPriceChanged)));
            var seedsPriceChanged = SeedsPriceChanged.Parser.ParseFrom(log.NonIndexed);
            seedsPriceChanged.NftPriceList?.Value?.Count.ShouldBe(30);
            seedsPriceChanged.FtPriceList?.Value?.Count.ShouldBe(30);
        }

        [Fact]
        public async Task SetTest_fail()
        {
            await InitTest_view();

            var exception1 = await Assert.ThrowsAsync<Exception>(() =>
                User1SymbolRegistrarContractStub.SetAdmin.SendAsync(User1.Address));
            exception1.ShouldNotBeNull();
            exception1.Message.ShouldContain("No permission");

            var exception2 = await Assert.ThrowsAsync<Exception>(() =>
                User1SymbolRegistrarContractStub.SetReceivingAccount.SendAsync(User2.Address));
            exception2.ShouldNotBeNull();
            exception2.Message.ShouldContain("No permission");
        }

        [Fact]
        public async Task SetAuctionConfigTests()
        {
            await InitializeContract();
            await InitSaleController(Admin.Address);
            await AdminSymbolRegistrarContractStub.SetAuctionConfig.SendAsync(new AuctionConfig
            {
                CountdownTime = 100,
                Duration = 100,
                MaxExtensionTime = 100,
                MinMarkup = 100
            });
            await AdminSymbolRegistrarContractStub.SetAuctionConfig.SendAsync(new AuctionConfig
            {
                CountdownTime = 100,
                Duration = 100,
                MaxExtensionTime = 100,
                MinMarkup = 100
            });

            var output = await AdminSymbolRegistrarContractStub.GetAuctionConfig.CallAsync(new Empty());
            output.Duration.ShouldBe(100);
            output.CountdownTime.ShouldBe(100);
            output.MaxExtensionTime.ShouldBe(100);
            output.MinMarkup.ShouldBe(100);
        }

        [Fact]
        public async Task SetAuctionConfigTests_Fail()
        {
            var result =
                await AdminSymbolRegistrarContractStub.SetAuctionConfig.SendWithExceptionAsync(new AuctionConfig());
            result.TransactionResult.Error.ShouldContain("Contract not initialized.");

            await InitializeContract();
            result = await User1SymbolRegistrarContractStub.SetAuctionConfig
                .SendWithExceptionAsync(new AuctionConfig());
            result.TransactionResult.Error.ShouldContain("No permission");

            await InitSaleController(Admin.Address);
            result = await AdminSymbolRegistrarContractStub.SetAuctionConfig.SendWithExceptionAsync(new AuctionConfig
            {
                Duration = -1
            });
            result.TransactionResult.Error.ShouldContain("Invalid input duration.");

            result = await AdminSymbolRegistrarContractStub.SetAuctionConfig.SendWithExceptionAsync(new AuctionConfig
            {
                Duration = 100,
                CountdownTime = -1
            });
            result.TransactionResult.Error.ShouldContain("Invalid input countdown time.");

            result = await AdminSymbolRegistrarContractStub.SetAuctionConfig.SendWithExceptionAsync(new AuctionConfig
            {
                Duration = 100,
                CountdownTime = 100,
                MaxExtensionTime = -1
            });
            result.TransactionResult.Error.ShouldContain("Invalid input max extension time.");

            result = await AdminSymbolRegistrarContractStub.SetAuctionConfig.SendWithExceptionAsync(new AuctionConfig
            {
                Duration = 100,
                CountdownTime = 100,
                MaxExtensionTime = 100,
                MinMarkup = -1
            });
            result.TransactionResult.Error.ShouldContain("Invalid input min markup.");
        }

        [Fact]
        public async Task AddSaleControllerTests()
        {
            await InitializeContract();
            var controllerList = await AdminSymbolRegistrarContractStub.GetSaleController.CallAsync(new Empty());
            controllerList.Controllers.Count.ShouldBe(0);
            var result = await User1SymbolRegistrarContractStub.AddSaleController.SendWithExceptionAsync(new AddSaleControllerInput
            {
                Addresses = new ControllerList
                {
                    Controllers = { Admin.Address }
                }
            });
            result.TransactionResult.Error.ShouldContain("No permission.");
            result = await AdminSymbolRegistrarContractStub.AddSaleController.SendAsync(new AddSaleControllerInput
            {
                Addresses = new ControllerList
                {
                    Controllers = { Admin.Address }
                }
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var saleControllerAdded = SaleControllerAdded.Parser.ParseFrom(result.TransactionResult.Logs.First(e => e.Name == nameof(SaleControllerAdded)).NonIndexed);
            saleControllerAdded.Addresses.Controllers.ShouldContain(Admin.Address);
            
            result = await AdminSymbolRegistrarContractStub.AddSaleController.SendWithExceptionAsync(new AddSaleControllerInput
            {
            });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
            result = await AdminSymbolRegistrarContractStub.AddSaleController.SendWithExceptionAsync(new AddSaleControllerInput
            {
                Addresses = new ControllerList()
            });
            result.TransactionResult.Error.ShouldContain("Invalid input controllers");
        }
        
        [Fact]
        public async Task RemoveSaleControllerTests()
        {
            await InitializeContract();
            var controllerList = await AdminSymbolRegistrarContractStub.GetSaleController.CallAsync(new Empty());
            controllerList.Controllers.Count.ShouldBe(0);
            var result = await User1SymbolRegistrarContractStub.RemoveSaleController.SendWithExceptionAsync(new RemoveSaleControllerInput()
            {
                Addresses = new ControllerList
                {
                    Controllers = { Admin.Address , User2.Address}
                }
            });
            result.TransactionResult.Error.ShouldContain("No permission.");
            
            await AdminSymbolRegistrarContractStub.AddSaleController.SendAsync(new AddSaleControllerInput
            {
                Addresses = new ControllerList
                {
                    Controllers = { Admin.Address , User2.Address }
                }
            });
            result = await AdminSymbolRegistrarContractStub.RemoveSaleController.SendAsync(new RemoveSaleControllerInput()
            {
                Addresses = new ControllerList
                {
                    Controllers = { User1.Address }
                }
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            
            result = await AdminSymbolRegistrarContractStub.RemoveSaleController.SendAsync(new RemoveSaleControllerInput()
            {
                Addresses = new ControllerList
                {
                    Controllers = { Admin.Address }
                }
            });
            var saleControllerRemoved = SaleControllerRemoved.Parser.ParseFrom(result.TransactionResult.Logs.First(e => e.Name == nameof(SaleControllerRemoved)).NonIndexed);
            saleControllerRemoved.Addresses.Controllers.ShouldContain(Admin.Address);
            
            result = await AdminSymbolRegistrarContractStub.RemoveSaleController.SendWithExceptionAsync(new RemoveSaleControllerInput()
            {
            });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
            result = await AdminSymbolRegistrarContractStub.RemoveSaleController.SendWithExceptionAsync(new RemoveSaleControllerInput()
            {
                Addresses = new ControllerList()
            });
            result.TransactionResult.Error.ShouldContain("Invalid input controllers");
        }

        [Fact]
        public async Task SetSeedExpirationConfigTests()
        {
            await InitializeContract();
            var result = await User1SymbolRegistrarContractStub.SetSeedExpirationConfig.SendWithExceptionAsync(new SeedExpirationConfig()
            {
                ExpirationTime = 1000
            });
            
            result.TransactionResult.Error.ShouldContain("No permission.");
            result = await AdminSymbolRegistrarContractStub.SetSeedExpirationConfig.SendAsync(new SeedExpirationConfig()
            {
                ExpirationTime = 1000
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var seedExpirationConfigChanged = SeedExpirationConfigChanged.Parser.ParseFrom(result.TransactionResult.Logs.First(e => e.Name == nameof(SeedExpirationConfigChanged)).NonIndexed);
            seedExpirationConfigChanged.SeedExpirationConfig.ExpirationTime.ShouldBe(1000);
            var seedExpirationConfig = await User1SymbolRegistrarContractStub.GetSeedExpirationConfig.CallAsync(new Empty());
            seedExpirationConfig.ExpirationTime.ShouldBe(1000);
            result = await AdminSymbolRegistrarContractStub.SetSeedExpirationConfig.SendWithExceptionAsync(new SeedExpirationConfig()
            {
                ExpirationTime = -1000
            });
            result.TransactionResult.Error.ShouldContain("Invalid input expiration time.");
        }
        
        [Fact]
        public async Task SetLastSeedIdTests()
        {
            await InitializeContract();
            var result = await User1SymbolRegistrarContractStub.SetLastSeedId.SendWithExceptionAsync(new Int64Value()
            {
                Value = 10
            });
            result.TransactionResult.Error.ShouldContain("No permission.");
            result = await AdminSymbolRegistrarContractStub.SetLastSeedId.SendWithExceptionAsync(new Int64Value()
            {
                Value = -1
            });
            result.TransactionResult.Error.ShouldContain("Invalid param");
            result = await AdminSymbolRegistrarContractStub.SetLastSeedId.SendAsync(new Int64Value()
            {
                Value = 10
            });
            result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            var lastSeedId = await AdminSymbolRegistrarContractStub.GetLastSeedId.CallAsync(new Empty());
            lastSeedId.Value.ShouldBe(10);
            result = await AdminSymbolRegistrarContractStub.SetLastSeedId.SendWithExceptionAsync(new Int64Value()
            {
                Value = 9
            });
            result.TransactionResult.Error.ShouldContain("Invalid param");
        }

        [Fact]
        public async Task IssueChainListTests()
        {
            await InitializeContract();

            // add 3 seeds （BTC、ETH already added by “InitializeContract”）
            var res = await AdminSymbolRegistrarContractStub.AddIssueChain.SendAsync(new IssueChainList
            {
                IssueChain = { "BTC", "ETH", "USDT" }
            });
            var addLogs = res.TransactionResult.Logs.Where(log => log.Name.Equals(nameof(IssueChainAdded))).ToList();
            addLogs.Count.ShouldBe(1);
            var addLog = IssueChainAdded.Parser.ParseFrom(addLogs[0].NonIndexed);
            addLog.IssueChainList.IssueChain.Count.ShouldBe(1);
            addLog.IssueChainList.IssueChain.ShouldContain("USDT");
            
            var result =  await AdminSymbolRegistrarContractStub.GetIssueChainList.CallAsync(new Empty());
            result.IssueChain.Count.ShouldBe(3);
            result.IssueChain.Contains("BTC").ShouldBe(true);
            result.IssueChain.Contains("ETH").ShouldBe(true);
            result.IssueChain.Contains("USDT").ShouldBe(true);
            
            
            // remove 2 seeds
            var removeRes = await AdminSymbolRegistrarContractStub.RemoveIssueChain.SendAsync(new IssueChainList
            {
                IssueChain = { "BTC", "ETH"}
            });
            var removeLogs = removeRes.TransactionResult.Logs.Where(log => log.Name.Equals(nameof(IssueChainRemoved))).ToList();
            removeLogs.Count.ShouldBe(1);
            var removeLog = IssueChainRemoved.Parser.ParseFrom(removeLogs[0].NonIndexed);
            removeLog.IssueChainList.IssueChain.Count.ShouldBe(2);
            removeLog.IssueChainList.IssueChain.ShouldContain("BTC");
            removeLog.IssueChainList.IssueChain.ShouldContain("ETH");
            
            result =  await AdminSymbolRegistrarContractStub.GetIssueChainList.CallAsync(new Empty());
            result.IssueChain.Count.ShouldBe(1);
            result.IssueChain.Contains("USDT").ShouldBe(true);
            
            // add permission
            var addNoPerm = await Assert.ThrowsAsync<Exception>( () => User1SymbolRegistrarContractStub.AddIssueChain.SendAsync(new IssueChainList
            {
                IssueChain = { "BTC", "ETH", "USDT" }
            }));
            addNoPerm.Message.ShouldContain("No permission");

            // remove permission
            var removeNoPerm = await Assert.ThrowsAsync<Exception>( () => User1SymbolRegistrarContractStub.RemoveIssueChain.SendAsync(new IssueChainList
            {
                IssueChain = { "BTC", "ETH", "USDT" }
            }));
            removeNoPerm.Message.ShouldContain("No permission");


        }
    }
}
using System;
using System.Linq;
using System.Threading.Tasks;
using AElf.Types;
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

            var bizConfig = AdminSymbolRegistrarContractStub.GetBizConfig.CallAsync(new Empty());
            bizConfig.Result.ReceivingAccount.ShouldBe(Admin.Address);
            bizConfig.Result.AdministratorAddress.ShouldBe(Admin.Address);
        }

        [Fact]
        public async Task InitTest_Fail()
        {
            // no permission
            var exception = await Assert.ThrowsAsync<Exception>(() =>
                User1SymbolRegistrarContractStub.Initialize.SendAsync(
                    new InitializeInput()
                    {
                        ReceivingAccount = Admin.Address
                    }));
            exception.ShouldNotBeNull();
            exception.Message.ShouldContain("No permission");

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

            await AdminSymbolRegistrarContractStub.SetReceivingAccount.SendAsync(User2.Address);
            await AdminSymbolRegistrarContractStub.SetAdmin.SendAsync(User1.Address);

            var bizConfig = AdminSymbolRegistrarContractStub.GetBizConfig.CallAsync(new Empty());
            bizConfig.Result.ReceivingAccount.ShouldBe(User2.Address);
            bizConfig.Result.AdministratorAddress.ShouldBe(User1.Address);
        }

        [Fact]
        public async Task InitWithSpecialSeed_success()
        {
            var result = await AdminSymbolRegistrarContractStub.Initialize.SendAsync(new InitializeInput()
            {
                ReceivingAccount = Admin.Address,
                ProxyAccountAddress = Admin.Address,
                SpecialSeeds = new SpecialSeedList
                {
                    Value = { _specialUsd, _specialEth }
                }
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


            var seedEth = await AdminSymbolRegistrarContractStub.GetSpecialSeed.CallAsync(new StringValue
            {
                Value = _specialEth.Symbol
            });
            seedEth.Symbol.ShouldBe(_specialEth.Symbol);
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
            result = await AdminSymbolRegistrarContractStub.SetAuctionConfig
                .SendWithExceptionAsync(new AuctionConfig());
            result.TransactionResult.Error.ShouldContain("No sale controller permission.");

            await InitSaleController(Admin.Address);
            result = await AdminSymbolRegistrarContractStub.SetAuctionConfig
                .SendWithExceptionAsync(new AuctionConfig());
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
    }
}
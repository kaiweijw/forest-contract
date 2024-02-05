using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Forest.Contracts.Drop
{
    public class DropContractTests_Admin : DropContractTestBase
    {
        [Fact]
        public async Task InitializeTests()
        {
            // initialize is deployer
            await Initialize();
            var admin = await DropContractStub.GetAdmin.CallAsync(new Empty());
            admin.ShouldBe(DefaultAddress);
            var maxDetailIndexCount = await DropContractStub.GetMaxDropDetailIndexCount.CallAsync(new Empty());
            maxDetailIndexCount.Value.ShouldBe(10);
            var maxDropDetailListCount = await DropContractStub.GetMaxDropDetailListCount.CallAsync(new Empty());
            maxDropDetailListCount.Value.ShouldBe(100);
            var proxyAccountAddress = await DropContractStub.GetProxyAccountContractAddress.CallAsync(new Empty());
            proxyAccountAddress.ShouldBe(DefaultAddress);
            
            // initialize twice
            var result = await DropContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
            {
                MaxDropDetailIndexCount = 10
            });
            result.TransactionResult.Error.ShouldContain("Already initialized.");
        }
        [Fact]
        public async Task InitializeTests_Exception()
        {
            // initialize twice
            var result = await DropContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
            {
            });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
            
            var result2 = await DropContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
            {
                MaxDropDetailIndexCount = -1,
                MaxDropDetailListCount = -1,
                ProxyAccountAddress = null
            });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
            
            var result3 = await DropContractStub.Initialize.SendAsync(new InitializeInput
            {
                MaxDropDetailIndexCount = 10,
                MaxDropDetailListCount = 10,
                ProxyAccountAddress = DefaultAddress
            });
            var admin = await DropContractStub.GetAdmin.CallAsync(new Empty());
            admin.ShouldBe(DefaultAddress);
            var maxDropDetailIndexCount = await DropContractStub.GetMaxDropDetailIndexCount.CallAsync(new Empty());
            maxDropDetailIndexCount.Value.ShouldBe(10);
            var maxDropDetailListCount = await DropContractStub.GetMaxDropDetailListCount.CallAsync(new Empty());
            maxDropDetailListCount.Value.ShouldBe(10);
            var proxyAccountAddress = await DropContractStub.GetProxyAccountContractAddress.CallAsync(new Empty());
            proxyAccountAddress.ShouldBe(DefaultAddress);
        }
        [Fact]
        public async Task InitializeTests_Deployer()
        {
            // initialize not deployer
            var result = await DropContractUserStub.Initialize.SendWithExceptionAsync(new InitializeInput
            {
                MaxDropDetailIndexCount = 10,
                MaxDropDetailListCount = 100,
                ProxyAccountAddress = DefaultAddress
            });
            result.TransactionResult.Error.ShouldContain("No permission.");
            
            var result2 = await DropContractStub.Initialize.SendAsync(new InitializeInput
            {
                MaxDropDetailIndexCount = 10,
                MaxDropDetailListCount = 100,
                ProxyAccountAddress = DefaultAddress
            });
            result2.TransactionResult.Error.ShouldBeNullOrEmpty();
        }
        private async Task Initialize()
        {
            var init = new InitializeInput
            {
                MaxDropDetailIndexCount = 10,
                MaxDropDetailListCount = 100,
                ProxyAccountAddress = DefaultAddress
            };
            await DropContractStub.Initialize.SendAsync(init);
        }
        
        [Fact]
        public async Task SetAdmin()
        {
            await Initialize();
            var result = await DropContractUserStub.SetAdmin.SendWithExceptionAsync(DefaultAddress);
            result.TransactionResult.Error.ShouldContain("No permission.");
            
            try
            {
                result = await DropContractStub.SetAdmin.SendAsync(null);
            }catch (Exception e)
            {
                Assert.Contains("Value cannot be null", e.Message);
            }
            
            await DropContractStub.SetAdmin.SendAsync(UserAddress);
            var admin = await DropContractStub.GetAdmin.CallAsync(new Empty());
            admin.ShouldBe(UserAddress);
        }

        [Fact]
        public async Task SetProxyAccountContractAddress()
        {
            await Initialize();
            var result = await DropContractUserStub.SetProxyAccountContractAddress.SendWithExceptionAsync(DefaultAddress);
        }

        [Fact]
        public async Task SetMaxDropDetailListCount()
        {
            await Initialize();
            var result = await DropContractUserStub.SetMaxDropDetailListCount.SendWithExceptionAsync(new Int32Value
            {
                Value = 11
            });
            result.TransactionResult.Error.ShouldContain("No permission.");
            
            result = await DropContractStub.SetMaxDropDetailListCount.SendWithExceptionAsync(new Int32Value
            {
                Value = -11
            });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
            
            await DropContractStub.SetMaxDropDetailListCount.SendAsync(new Int32Value
            {
                Value = 11
            });
            var maxDropDetailListCount = await DropContractStub.GetMaxDropDetailListCount.CallAsync(new Empty());
            maxDropDetailListCount.Value.ShouldBe(11);
        }
        [Fact]
        public async Task SetMaxDropDetailIndexCount()
        {
            await Initialize();
            var result = await DropContractUserStub.SetMaxDropDetailIndexCount.SendWithExceptionAsync(new Int32Value
            {
                Value = 11
            });
            result.TransactionResult.Error.ShouldContain("No permission.");
            
            result = await DropContractStub.SetMaxDropDetailIndexCount.SendWithExceptionAsync(new Int32Value
            {
                Value = -11
            });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
            
            await DropContractStub.SetMaxDropDetailIndexCount.SendAsync(new Int32Value
            {
                Value = 11
            });
            var maxDropDetailListCount = await DropContractStub.GetMaxDropDetailIndexCount.CallAsync(new Empty());
            maxDropDetailListCount.Value.ShouldBe(11);
        }
    }
}
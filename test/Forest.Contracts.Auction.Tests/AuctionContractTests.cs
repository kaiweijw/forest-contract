using System.Threading.Tasks;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Forest.Contracts.Auction
{
    public class AuctionContractTests : AuctionContractTestBase
    {
        [Fact]
        public async Task InitializeTests()
        {
            await Initialize();
            var admin = await AuctionContractStub.GetAdmin.CallAsync(new Empty());
            admin.ShouldBe(DefaultAddress);

            var result = await AuctionContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
            {
                Admin = DefaultAddress
            });
            result.TransactionResult.Error.ShouldContain("Already initialized.");
        }
        
        [Fact]
        public async Task InitializeTests_Fail()
        {
            var result = await AuctionContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
            {
                Admin = new Address()
            });
            result.TransactionResult.Error.ShouldContain("Invalid input admin.");
            
            result = await AuctionContractUserStub.Initialize.SendWithExceptionAsync(new InitializeInput
            {
                Admin = UserAddress
            });
            result.TransactionResult.Error.ShouldContain("No permission.");
        }

        [Fact]
        public async Task CreateAuctionTests()
        {
            await Initialize();
            
            
        }
        
        private async Task Initialize()
        {
            await AuctionContractStub.Initialize.SendAsync(new InitializeInput
            {
                Admin = DefaultAddress
            });
        }
    }
}
using System.Threading.Tasks;
using Xunit;

namespace AElf.Contracts.MarketNFTContract
{
    public class MarketNFTContractTests : MarketNFTContractTestBase
    {
        [Fact]
        public async Task TestCreate()
        {
             // Get a stub for testing.
            // var keyPair = SampleAccount.Accounts.First().KeyPair;
            //var stub = GetMarketNFTContractStub(keyPair);
            //var createInput = new CreateInput();
            var executionResult =  await UserTokenContractStub.Create.SendAsync(new CreateInput
            {
                Symbol = "Marketnft-11",
                TokenName = "Marketnft—11",
                TotalSupply = 100,
                Decimals = 0,
                Issuer = User1Address,
                IsBurnable = true,
                IssueChainId = 0,
                ExternalInfo = new ExternalInfo(),
                Memo = "marketnft-11 ok",
                To = User1Address
            });
            var symbol = executionResult.Output;
        }
    }
}
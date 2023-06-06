using System.Linq;
using System.Threading.Tasks;
using AElf.ContractTestBase.ContractTestKit;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace AElf.Contracts.MarketNFTContract
{
    public class MarketNFTContractTests : MarketNFTContractTestBase
    {
        [Fact]
        public async Task TestCreate()
        {
            // Get a stub for testing.
            var keyPair = SampleAccount.Accounts.First().KeyPair;
            var stub = GetMarketNFTContractStub(keyPair);
            
            var createInput = new CreateInput();
            var transactionResult = (await stub.Create.SendAsync(createInput)).TransactionResult;
        }
    }
}
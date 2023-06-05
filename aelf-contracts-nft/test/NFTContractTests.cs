using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace AElf.Contracts.NFT
{
    public partial class NFTContractTests : TestBase
    {
        [Fact]
        public async Task PlayTests()
        {
            // Prepare awards.
            await TokenContractStub.Transfer.SendAsync(new MultiToken.TransferInput
            {
                To = DAppContractAddress,
                Symbol = "ELF",
                Amount = 100
            });

            var result = await TokenContractStub.GetBalance.CallAsync(new MultiToken.GetBalanceInput
            {
                Symbol = "ELF",
                Owner = DefaultAddress
            });
            result.Balance.ShouldNotBe(100);
        }
    }
    
}
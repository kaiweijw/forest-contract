using System.IO;
using AElf.Boilerplate.TestBase;
using AElf.Contracts.MultiToken;
using AElf.ContractTestBase.ContractTestKit;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Standards.ACS0;
using AElf.Types;
using Google.Protobuf;
using Volo.Abp.Threading;

namespace Forest.Contracts.Auction
{
    public class AuctionContractTestBase : DAppContractTestBase<AuctionContractTestModule>
    {
        internal Address AuctionContractAddress { get; set; }
        
        internal TokenContractContainer.TokenContractStub TokenContractStub { get; set; }
        internal TokenContractContainer.TokenContractStub TokenContractUserStub { get; set; }
        internal TokenContractContainer.TokenContractStub TokenContractUser2Stub { get; set; }
        internal TokenContractContainer.TokenContractStub TokenContractReceivingStub { get; set; }
        internal ACS0Container.ACS0Stub ZeroContractStub { get; set; }
        internal AuctionContractContainer.AuctionContractStub AuctionContractStub { get; set; }
        internal AuctionContractContainer.AuctionContractStub AuctionContractUserStub { get; set; }
        internal AuctionContractContainer.AuctionContractStub AuctionContractUser2Stub { get; set; }
        
        protected ECKeyPair DefaultKeyPair => Accounts[0].KeyPair;
        protected Address DefaultAddress => Accounts[0].Address;

        protected ECKeyPair UserKeyPair => Accounts[1].KeyPair;
        protected Address UserAddress => Accounts[1].Address;
        
        protected ECKeyPair User2KeyPair => Accounts[2].KeyPair;
        protected Address User2Address => Accounts[2].Address;
        
        protected ECKeyPair ReceivingKeyPair => Accounts[5].KeyPair;
        protected Address ReceivingAddress => Accounts[5].Address;
        
        protected readonly IBlockTimeProvider BlockTimeProvider;

        protected AuctionContractTestBase()
        {
            BlockTimeProvider = GetRequiredService<IBlockTimeProvider>();
            
            ZeroContractStub = GetContractZeroTester(DefaultKeyPair);
            var result = AsyncHelper.RunSync(async () => await ZeroContractStub.DeploySmartContract.SendAsync(
                new ContractDeploymentInput
                {
                    Category = KernelConstants.CodeCoverageRunnerCategory,
                    Code = ByteString.CopyFrom(
                        File.ReadAllBytes(typeof(AuctionContract).Assembly.Location))
                }));

            AuctionContractAddress = Address.Parser.ParseFrom(result.TransactionResult.ReturnValue);

            AuctionContractStub = GetAuctionAccountContractStub(DefaultKeyPair);
            AuctionContractUserStub = GetAuctionAccountContractStub(UserKeyPair);
            AuctionContractUser2Stub = GetAuctionAccountContractStub(User2KeyPair);
            TokenContractStub = GetTokenContractStub(DefaultKeyPair);
            TokenContractUserStub = GetTokenContractStub(UserKeyPair);
            TokenContractUser2Stub = GetTokenContractStub(User2KeyPair);
            TokenContractReceivingStub = GetTokenContractStub(ReceivingKeyPair);
        }
        
        internal AuctionContractContainer.AuctionContractStub GetAuctionAccountContractStub(ECKeyPair senderKeyPair)
        {
            return GetTester<AuctionContractContainer.AuctionContractStub>(AuctionContractAddress, senderKeyPair);
        }
        
        internal TokenContractContainer.TokenContractStub GetTokenContractStub(ECKeyPair senderKeyPair)
        {
            return GetTester<TokenContractContainer.TokenContractStub>(TokenContractAddress, senderKeyPair);
        }

        internal ACS0Container.ACS0Stub GetContractZeroTester(ECKeyPair senderKeyPair)
        {
            return GetTester<ACS0Container.ACS0Stub>(BasicContractZeroAddress, senderKeyPair);
        }
    }
}
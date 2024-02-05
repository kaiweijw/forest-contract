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

namespace Forest.Contracts.Drop
{
    public class DropContractTestBase : DAppContractTestBase<DropContractTestModule>
    {
        internal Address DropContractAddress { get; set; }

        internal TokenContractContainer.TokenContractStub TokenContractStub { get; set; }
        
        internal TokenContractContainer.TokenContractStub TokenContractUserStub { get; set; }
        internal TokenContractContainer.TokenContractStub TokenContractUser2Stub { get; set; }
        internal TokenContractContainer.TokenContractStub TokenContractReceivingStub { get; set; }
        
        internal ACS0Container.ACS0Stub ZeroContractStub { get; set; }

        internal DropContractContainer.DropContractStub DropContractStub { get; set; }
        internal DropContractContainer.DropContractStub DropContractUserStub { get; set; }
        internal DropContractContainer.DropContractStub DropContractUser2Stub { get; set; }
        protected ECKeyPair DefaultKeyPair => Accounts[0].KeyPair;
        protected Address DefaultAddress => Accounts[0].Address;

        protected ECKeyPair UserKeyPair => Accounts[1].KeyPair;
        protected Address UserAddress => Accounts[1].Address;

        protected ECKeyPair User2KeyPair => Accounts[2].KeyPair;
        protected Address User2Address => Accounts[2].Address;

        protected ECKeyPair ReceivingKeyPair => Accounts[5].KeyPair;
        protected Address ReceivingAddress => Accounts[5].Address;

        protected readonly IBlockTimeProvider BlockTimeProvider;

        protected DropContractTestBase()
        {
            BlockTimeProvider = GetRequiredService<IBlockTimeProvider>();

            ZeroContractStub = GetContractZeroTester(DefaultKeyPair);
            var result = AsyncHelper.RunSync(async () => await ZeroContractStub.DeploySmartContract.SendAsync(
                new ContractDeploymentInput
                {
                    Category = KernelConstants.CodeCoverageRunnerCategory,
                    Code = ByteString.CopyFrom(
                        File.ReadAllBytes(typeof(DropContract).Assembly.Location))
                }));

            DropContractAddress = Address.Parser.ParseFrom(result.TransactionResult.ReturnValue);

            DropContractStub = GetDropContractContainerStub(DefaultKeyPair);
            DropContractUserStub = GetDropContractContainerStub(UserKeyPair);
            DropContractUser2Stub = GetDropContractContainerStub(User2KeyPair);
            DropContractStub = GetDropContractContainerStub(DefaultKeyPair);
            TokenContractStub = GetTokenContractStub(DefaultKeyPair);
            TokenContractUserStub = GetTokenContractStub(UserKeyPair);
            TokenContractUser2Stub = GetTokenContractStub(User2KeyPair);
            TokenContractReceivingStub = GetTokenContractStub(ReceivingKeyPair);
        }

        internal DropContractContainer.DropContractStub GetDropContractContainerStub(ECKeyPair senderKeyPair)
        {
            return GetTester<DropContractContainer.DropContractStub>(DropContractAddress, senderKeyPair);
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
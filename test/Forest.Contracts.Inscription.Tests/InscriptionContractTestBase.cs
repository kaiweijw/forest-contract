using System.IO;
using System.Threading.Tasks;
using AElf;
using AElf.Boilerplate.TestBase;
using AElf.Contracts.CrossChain;
using AElf.Contracts.MultiToken;
using AElf.Cryptography;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Standards.ACS0;
using AElf.Standards.ACS2;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Volo.Abp.Threading;

namespace Forest.Contracts.Inscription
{
    public class InscriptionContractTestBase : DAppContractTestBase<InscriptionContractTestModule>
    {
        internal Address InscriptionContractAddress { get; set; }
        
        internal ACS0Container.ACS0Stub ZeroContractStub { get; set; }

        internal TokenContractContainer.TokenContractStub TokenContractStub { get; set; }
        internal TokenContractContainer.TokenContractStub TokenContractUserStub { get; set; }
        internal TokenContractImplContainer.TokenContractImplStub TokenContractImplStub { get; set; }
        internal TokenContractImplContainer.TokenContractImplStub TokenContractImplUserStub { get; set; }
        internal TokenContractImplContainer.TokenContractImplStub TokenContractImplUser2Stub { get; set; }

        internal InscriptionContractContainer.InscriptionContractStub InscriptionContractStub { get; set; }
        
        internal InscriptionContractContainer.InscriptionContractStub InscriptionContractAccount1Stub { get; set; }
        
        internal ACS2BaseContainer.ACS2BaseStub Acs2BaseStub;

        protected ECKeyPair DefaultKeyPair => Accounts[0].KeyPair;
        protected Address DefaultAddress => Accounts[0].Address;

        protected ECKeyPair UserKeyPair => Accounts[1].KeyPair;
        protected Address UserAddress => Accounts[1].Address;

        protected ECKeyPair User2KeyPair => Accounts[2].KeyPair;
        protected Address User2Address => Accounts[2].Address;
        
        protected ECKeyPair User3KeyPair => Accounts[3].KeyPair;
        protected Address User3Address => Accounts[3].Address;

        
        protected InscriptionContractTestBase()
        {
            
            ZeroContractStub = GetContractZeroTester(DefaultKeyPair);
            
            var code = ByteString.CopyFrom(File.ReadAllBytes(typeof(InscriptionContract).Assembly.Location));
            var contractOperation = new ContractOperation
            {
                ChainId = 9992731,
                CodeHash = HashHelper.ComputeFrom(code.ToByteArray()),
                Deployer = DefaultAddress,
                Salt = HashHelper.ComputeFrom("inscription"),
                Version = 1
            };
            contractOperation.Signature = GenerateContractSignature(DefaultKeyPair.PrivateKey, contractOperation);

            var result = AsyncHelper.RunSync(async () => await ZeroContractStub.DeploySmartContract.SendAsync(
                new ContractDeploymentInput
                {
                    Category = KernelConstants.CodeCoverageRunnerCategory,
                    Code = code,
                    ContractOperation = contractOperation
                }));

            InscriptionContractAddress = Address.Parser.ParseFrom(result.TransactionResult.ReturnValue);
            
            InscriptionContractStub = GetInscriptionContractStub(DefaultKeyPair);
            TokenContractStub = GetTokenContractStub(DefaultKeyPair);
            TokenContractUserStub = GetTokenContractStub(UserKeyPair);
            TokenContractImplStub = GetTokenImplContractStub(User2KeyPair);
            TokenContractImplUserStub = GetTokenImplContractStub(UserKeyPair);
            TokenContractImplUser2Stub = GetTokenImplContractStub(User3KeyPair);
            InscriptionContractAccount1Stub = GetInscriptionContractStub(User2KeyPair);
            Acs2BaseStub = GetTester<ACS2BaseContainer.ACS2BaseStub>(InscriptionContractAddress, DefaultKeyPair);

        }

        internal InscriptionContractContainer.InscriptionContractStub GetInscriptionContractStub(
            ECKeyPair senderKeyPair)
        {
            return GetTester<InscriptionContractContainer.InscriptionContractStub>(InscriptionContractAddress,
                senderKeyPair);
        }
        
        internal ACS0Container.ACS0Stub GetContractZeroTester(ECKeyPair senderKeyPair)
        {
            return GetTester<ACS0Container.ACS0Stub>(BasicContractZeroAddress, senderKeyPair);
        }

        internal TokenContractContainer.TokenContractStub GetTokenContractStub(ECKeyPair senderKeyPair)
        {
            return GetTester<TokenContractContainer.TokenContractStub>(TokenContractAddress, senderKeyPair);
        }
        
        internal TokenContractImplContainer.TokenContractImplStub GetTokenImplContractStub(ECKeyPair senderKeyPair)
        {
            return GetTester<TokenContractImplContainer.TokenContractImplStub>(TokenContractAddress, senderKeyPair);
        }
        internal async Task<long> GetParentChainHeight(
            CrossChainContractImplContainer.CrossChainContractImplStub crossChainContractStub)
        {
            return (await crossChainContractStub.GetParentChainHeight.CallAsync(new Empty())).Value;
        }
        
        internal ByteString GenerateContractSignature(byte[] privateKey, ContractOperation contractOperation)
        {
            var dataHash = HashHelper.ComputeFrom(contractOperation);
            var signature = CryptoHelper.SignWithPrivateKey(privateKey, dataHash.ToByteArray());
            return ByteStringHelper.FromHexString(signature.ToHex());
        }
    }
}
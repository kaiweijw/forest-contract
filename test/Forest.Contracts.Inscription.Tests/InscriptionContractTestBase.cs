using System.Threading.Tasks;
using AElf.Boilerplate.TestBase;
using AElf.Boilerplate.TestBase.SmartContractNameProviders;
using AElf.Contracts.CrossChain;
using AElf.Contracts.MultiToken;
using AElf.Cryptography.ECDSA;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Forest.Contracts.Inscription
{
    public class InscriptionContractTestBase : DAppContractTestBase<InscriptionContractTestModule>
    {
        internal Address InscriptionContractAddress =>
            GetAddress(InscriptionSmartContractAddressNameProvider.StringName);


        internal TokenContractContainer.TokenContractStub TokenContractStub { get; set; }
        internal TokenContractContainer.TokenContractStub TokenContractUserStub { get; set; }
        internal TokenContractContainer.TokenContractStub TokenContractUser2Stub { get; set; }
        
        internal InscriptionContractContainer.InscriptionContractStub InscriptionContractStub { get; set; }
        
        protected ECKeyPair DefaultKeyPair => Accounts[0].KeyPair;
        protected Address DefaultAddress => Accounts[0].Address;

        protected ECKeyPair UserKeyPair => Accounts[1].KeyPair;
        protected Address UserAddress => Accounts[1].Address;

        protected ECKeyPair User2KeyPair => Accounts[2].KeyPair;
        protected Address User2Address => Accounts[2].Address;

        
        protected InscriptionContractTestBase()
        {
            InscriptionContractStub = GetInscriptionContractStub(DefaultKeyPair);
            TokenContractStub = GetTokenContractStub(DefaultKeyPair);
            TokenContractUserStub = GetTokenContractStub(UserKeyPair);
            TokenContractUser2Stub = GetTokenContractStub(User2KeyPair);
        }

        internal InscriptionContractContainer.InscriptionContractStub GetInscriptionContractStub(
            ECKeyPair senderKeyPair)
        {
            return GetTester<InscriptionContractContainer.InscriptionContractStub>(InscriptionContractAddress,
                senderKeyPair);
        }

        internal TokenContractContainer.TokenContractStub GetTokenContractStub(ECKeyPair senderKeyPair)
        {
            return GetTester<TokenContractContainer.TokenContractStub>(TokenContractAddress, senderKeyPair);
        }
        internal async Task<long> GetParentChainHeight(
            CrossChainContractImplContainer.CrossChainContractImplStub crossChainContractStub)
        {
            return (await crossChainContractStub.GetParentChainHeight.CallAsync(new Empty())).Value;
        }
    }
}
using AElf.Boilerplate.TestBase;
using AElf.Cryptography.ECDSA;

namespace Forest.SymbolRegistrar
{
    public class SymbolRegistrarContractTestBase : DAppContractTestBase<SymbolRegistrarContractTestModule>
    {
        // You can get address of any contract via GetAddress method, for example:
        // internal Address DAppContractAddress => GetAddress(DAppSmartContractAddressNameProvider.StringName);

        internal SymbolRegistrarContractContainer.SymbolRegistrarContractStub GetSymbolRegistrarContractStub(ECKeyPair senderKeyPair)
        {
            return GetTester<SymbolRegistrarContractContainer.SymbolRegistrarContractStub>(DAppContractAddress, senderKeyPair);
        }
    }
}
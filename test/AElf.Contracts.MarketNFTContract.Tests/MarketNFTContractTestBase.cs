using AElf.Boilerplate.TestBase;
using AElf.Cryptography.ECDSA;

namespace AElf.Contracts.MarketNFTContract
{
    public class MarketNFTContractTestBase : DAppContractTestBase<MarketNFTContractTestModule>
    {
        // You can get address of any contract via GetAddress method, for example:
        // internal Address DAppContractAddress => GetAddress(DAppSmartContractAddressNameProvider.StringName);

        internal MarketNFTContractContainer.MarketNFTContractStub GetMarketNFTContractStub(ECKeyPair senderKeyPair)
        {
            return GetTester<MarketNFTContractContainer.MarketNFTContractStub>(DAppContractAddress, senderKeyPair);
        }
    }
}
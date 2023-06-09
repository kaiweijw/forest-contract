using AElf.Boilerplate.TestBase;
using AElf.Cryptography.ECDSA;
using AElf.Types;

namespace AElf.Contracts.MarketNFTContract
{
    public class MarketNFTContractTestBase : DAppContractTestBase<MarketNFTContractTestModule>
    {
        // You can get address of any contract via GetAddress method, for example:
        // internal Address DAppContractAddress => GetAddress(DAppSmartContractAddressNameProvider.StringName);
        internal MarketNFTContractContainer.MarketNFTContractStub UserTokenContractStub;
        protected ECKeyPair User1KeyPair => Accounts[10].KeyPair;
        protected Address User1Address => Accounts[10].Address;
        protected Address User2Address => Accounts[11].Address;
        protected Address User3Address => Accounts[14].Address;
        protected Address User4Address => Accounts[15].Address;
        protected Address User5Address => Accounts[16].Address;
        protected Address User6Address => Accounts[17].Address;
        public MarketNFTContractTestBase()
        {
            UserTokenContractStub =
                GetTester<MarketNFTContractContainer.MarketNFTContractStub>(TokenContractAddress, User1KeyPair);
        }

        internal MarketNFTContractContainer.MarketNFTContractStub GetMarketNFTContractStub(ECKeyPair senderKeyPair)
        {
            return GetTester<MarketNFTContractContainer.MarketNFTContractStub>(DAppContractAddress, senderKeyPair);
        }
    }
}
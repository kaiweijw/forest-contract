using AElf.Contracts.MultiToken;
using AElf.Cryptography.ECDSA;
using AElf.Kernel.Token;
using AElf.Types;

namespace AElf.Contracts.NFT
{
    public partial class NFTContractTests
    {
        // private readonly ECKeyPair KeyPair;
        private readonly NFTContractContainer.NFTContractStub NFTContractStub;
        private readonly TokenContractContainer.TokenContractStub TokenContractStub;
        protected ECKeyPair DefaultKeyPair => Accounts[0].KeyPair;
        protected Address DefaultAddress => Accounts[0].Address;
        protected ECKeyPair UserKeyPair => Accounts[1].KeyPair;
        protected Address UserAddress => Accounts[1].Address;

        public NFTContractTests()
        {
            // KeyPair = SampleAccount.Accounts.First().KeyPair;
            NFTContractStub = GetContractStub<NFTContractContainer.NFTContractStub>(DefaultKeyPair);
            TokenContractStub = GetTester<TokenContractContainer.TokenContractStub>(
                GetAddress(TokenSmartContractAddressNameProvider.StringName), DefaultKeyPair);
        }
    }
    
}
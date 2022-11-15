using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Boilerplate.TestBase;
using AElf.Boilerplate.TestBase.SmartContractNameProviders;
using AElf.Contracts.Consensus.AEDPoS;
using AElf.Contracts.Election;
using AElf.Contracts.MultiToken;
using AElf.Contracts.NFT;
using AElf.Contracts.Parliament;
using AElf.Cryptography.ECDSA;
using AElf.CSharp.Core.Extension;
using AElf.GovernmentSystem;
using AElf.Standards.ACS3;
using AElf.Types;
using Forest.Whitelist;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Volo.Abp.Threading;
using TimestampHelper = AElf.Kernel.TimestampHelper;

namespace Forest
{
    public class ForestContractTestBase : DAppContractTestBase<ForestContractTestModule>
    {
        protected ECKeyPair DefaultKeyPair => Accounts[0].KeyPair;
        protected Address DefaultAddress => Accounts[0].Address;
        
        protected ECKeyPair MinterKeyPair => Accounts[1].KeyPair;
        protected Address MinterAddress => Accounts[1].Address;
        
        protected ECKeyPair User1KeyPair => Accounts[10].KeyPair;
        protected ECKeyPair User2KeyPair => Accounts[11].KeyPair;
        protected ECKeyPair User3KeyPair => Accounts[14].KeyPair;
        protected Address User1Address => Accounts[10].Address;
        protected Address User2Address => Accounts[11].Address;
        protected Address User3Address => Accounts[14].Address;
        protected Address User4Address => Accounts[15].Address;
        protected Address User5Address => Accounts[16].Address;
        protected Address User6Address => Accounts[17].Address;
        
        protected ECKeyPair MarketServiceFeeReceiverKeyPair => Accounts[12].KeyPair;
        protected Address MarketServiceFeeReceiverAddress => Accounts[12].Address;
        
        protected List<ECKeyPair> InitialCoreDataCenterKeyPairs =>
            Accounts.Take(InitialCoreDataCenterCount).Select(a => a.KeyPair).ToList();
        
        
        // You can get address of any contract via GetAddress method, for example:
        // internal Address DAppContractAddress => GetAddress(DAppSmartContractAddressNameProvider.StringName);
        internal ParliamentContractImplContainer.ParliamentContractImplStub ParliamentContractStub;

        internal ElectionContractImplContainer.ElectionContractImplStub ElectionContractStub;
        internal AEDPoSContractImplContainer.AEDPoSContractImplStub ConsensusContractStub;
        
        internal TokenContractImplContainer.TokenContractImplStub TokenContractStub;
        internal TokenContractImplContainer.TokenContractImplStub UserTokenContractStub;
        internal TokenContractImplContainer.TokenContractImplStub NFTBuyerTokenContractStub;
        internal TokenContractImplContainer.TokenContractImplStub NFTBuyer2TokenContractStub;
        
        internal NFTContractContainer.NFTContractStub NFTContractStub { get; set; }
        internal ForestContractContainer.ForestContractStub ForestContractStub { get; set; }
        internal WhitelistContractContainer.WhitelistContractStub WhitelistContractStub { get; set; }
        
        internal ForestContractContainer.ForestContractStub SellerForestContractStub { get; set; }
    
        internal ForestContractContainer.ForestContractStub Seller2ForestContractStub { get; set; }
        internal ForestContractContainer.ForestContractStub BuyerForestContractStub { get; set; }
        internal ForestContractContainer.ForestContractStub Buyer2ForestContractStub { get; set; }
        internal ForestContractContainer.ForestContractStub Buyer3ForestContractStub { get; set; }
        internal ForestContractContainer.ForestContractStub CreatorForestContractStub { get; set; }
        internal ForestContractContainer.ForestContractStub AdminForestContractStub { get; set; }
        
        internal NFTContractContainer.NFTContractStub MinterNFTContractStub { get; set; }
        internal NFTContractContainer.NFTContractStub NFT2ContractStub { get; set; }
        
        
        internal Address NFTContractAddress => GetAddress(NFTSmartContractAddressNameProvider.StringName);
        internal Address ForestContractAddress => GetAddress(ForestSmartContractAddressNameProvider.StringName);
        internal Address WhitelistContractAddress => GetAddress(WhitelistSmartContractAddressNameProvider.StringName);

        internal Address ElectionContractAddress => GetAddress(ElectionSmartContractAddressNameProvider.StringName);

        public ForestContractTestBase()
        {
            TokenContractStub =
                GetTester<TokenContractImplContainer.TokenContractImplStub>(TokenContractAddress, DefaultKeyPair);
            UserTokenContractStub =
                GetTester<TokenContractImplContainer.TokenContractImplStub>(TokenContractAddress, User1KeyPair);
            NFTBuyerTokenContractStub =
                GetTester<TokenContractImplContainer.TokenContractImplStub>(TokenContractAddress, User2KeyPair);
            NFTBuyer2TokenContractStub =
                GetTester<TokenContractImplContainer.TokenContractImplStub>(TokenContractAddress, User3KeyPair);
            MinterNFTContractStub = GetTester<NFTContractContainer.NFTContractStub>(NFTContractAddress, MinterKeyPair);

            AdminForestContractStub =GetForestContractStub(DefaultKeyPair);
            ForestContractStub = GetForestContractStub(DefaultKeyPair);
            SellerForestContractStub = GetForestContractStub(DefaultKeyPair);
            Seller2ForestContractStub = GetForestContractStub(User2KeyPair);
            BuyerForestContractStub = GetForestContractStub(User2KeyPair);
            Buyer2ForestContractStub = GetForestContractStub(User3KeyPair);
            Buyer3ForestContractStub = GetForestContractStub(DefaultKeyPair);
            CreatorForestContractStub = GetForestContractStub(DefaultKeyPair);
            NFTContractStub = GetNFTContractStub(DefaultKeyPair);
            NFT2ContractStub = GetTester<NFTContractContainer.NFTContractStub>(NFTContractAddress, User2KeyPair);
            WhitelistContractStub = GetWhitelistContractStub(DefaultKeyPair);
            ParliamentContractStub = GetTester<ParliamentContractImplContainer.ParliamentContractImplStub>(
                ParliamentContractAddress, DefaultKeyPair);
            ElectionContractStub = GetTester<ElectionContractImplContainer.ElectionContractImplStub>(
                ElectionContractAddress, DefaultKeyPair);
            ConsensusContractStub = GetTester<AEDPoSContractImplContainer.AEDPoSContractImplStub>(
                ConsensusContractAddress, DefaultKeyPair);
            AsyncHelper.RunSync(SetNFTContractAddress);

        }

        internal ParliamentContractImplContainer.ParliamentContractImplStub GetParliamentContractTester(
            ECKeyPair keyPair)
        {
            return GetTester<ParliamentContractImplContainer.ParliamentContractImplStub>(ParliamentContractAddress,
                keyPair);
        }
        
        internal ForestContractContainer.ForestContractStub GetForestContractStub(ECKeyPair senderKeyPair)
        {
            return GetTester<ForestContractContainer.ForestContractStub>(ForestContractAddress, senderKeyPair);
        }
        internal NFTContractContainer.NFTContractStub GetNFTContractStub(ECKeyPair senderKeyPair)
        {
            return GetTester<NFTContractContainer.NFTContractStub>(NFTContractAddress, senderKeyPair);
        }
        internal WhitelistContractContainer.WhitelistContractStub GetWhitelistContractStub(ECKeyPair senderKeyPair)
        {
            return GetTester<WhitelistContractContainer.WhitelistContractStub>(WhitelistContractAddress, senderKeyPair);
        }
        
        private async Task SetNFTContractAddress()
        {
            var defaultParliament = await ParliamentContractStub.GetDefaultOrganizationAddress.CallAsync(new Empty());
            var proposalId = await CreateProposalAsync(TokenContractAddress,
                defaultParliament, nameof(TokenContractStub.AddAddressToCreateTokenWhiteList),
                NFTContractAddress);
            await ApproveWithMinersAsync(proposalId);
            await ParliamentContractStub.Release.SendAsync(proposalId);
        }
        private async Task<Hash> CreateProposalAsync(Address contractAddress, Address organizationAddress,
            string methodName, IMessage input)
        {
            var proposal = new CreateProposalInput
            {
                OrganizationAddress = organizationAddress,
                ContractMethodName = methodName,
                ExpiredTime = TimestampHelper.GetUtcNow().AddHours(1),
                Params = input.ToByteString(),
                ToAddress = contractAddress
            };

            var createResult = await ParliamentContractStub.CreateProposal.SendAsync(proposal);
            var proposalId = createResult.Output;

            return proposalId;
        }
        
        private async Task ApproveWithMinersAsync(Hash proposalId)
        {
            var miner = GetParliamentContractTester(DefaultKeyPair);
            await miner.Approve.SendAsync(proposalId);
            // foreach (var bp in InitialCoreDataCenterKeyPairs)
            // {
            //     var tester = GetParliamentContractTester(bp);
            //     await tester.Approve.SendAsync(proposalId);
            // }
        }
    }
    
}
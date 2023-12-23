using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Boilerplate.TestBase;
using AElf.Boilerplate.TestBase.SmartContractNameProviders;
using AElf.Contracts.Consensus.AEDPoS;
using AElf.Contracts.CrossChain;
using AElf.Contracts.Genesis;
using AElf.Contracts.MultiToken;
using AElf.Contracts.Parliament;
using AElf.ContractTestBase.ContractTestKit;
using AElf.CrossChain;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.Consensus;
using AElf.Kernel.Proposal;
using AElf.Kernel.SmartContract;
using AElf.Kernel.Token;
using AElf.Standards.ACS0;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.Threading;

namespace Forest.Inscription
{
    public class InscriptionContractTestBase : DAppContractTestBase<InscriptionContractTestModule>
    {
        internal Address InscriptionContractAddress =>
            GetAddress(InscriptionSmartContractAddressNameProvider.StringName);


        internal TokenContractContainer.TokenContractStub TokenContractStub { get; set; }
        internal TokenContractContainer.TokenContractStub TokenContractUserStub { get; set; }
        internal TokenContractContainer.TokenContractStub TokenContractUser2Stub { get; set; }

        // internal CrossChainContractImplContainer.CrossChainContractImplStub CrossChainContractStub { get; set; }
        //
        //
        // internal ACS0Container.ACS0Stub ZeroContractStub { get; set; }
        internal InscriptionContractContainer.InscriptionContractStub InscriptionContractStub { get; set; }
        // protected ContractTestKit<InscriptionContractTestModule> SideChainTestKit;
        // protected Address SideBasicContractZeroAddress;
        // internal AEDPoSContractContainer.AEDPoSContractStub SideChain2AEDPoSContractStub;
        // internal CrossChainContractImplContainer.CrossChainContractImplStub SideChain2CrossChainContractStub;
        // internal ParliamentContractImplContainer.ParliamentContractImplStub SideChain2ParliamentContractStub;
        // internal TokenContractImplContainer.TokenContractImplStub SideChain2TokenContractStub;
        // internal AEDPoSContractContainer.AEDPoSContractStub SideChainAEDPoSContractStub;
        // internal BasicContractZeroImplContainer.BasicContractZeroImplStub SideChainBasicContractZeroStub;
        // internal CrossChainContractImplContainer.CrossChainContractImplStub SideChainCrossChainContractStub;
        // internal ParliamentContractImplContainer.ParliamentContractImplStub SideChainParliamentContractStub;
        // internal TokenContractImplContainer.TokenContractImplStub SideChainTokenContractStub;
        //
        // protected Address SideConsensusAddress;
        //
        // protected Address SideCrossChainContractAddress;
        //
        // protected Address SideParliamentAddress;
        //
        // protected Address SideTokenContractAddress;
        // protected readonly IBlockchainService BlockchainService;


        protected ECKeyPair DefaultKeyPair => Accounts[0].KeyPair;
        protected Address DefaultAddress => Accounts[0].Address;

        protected ECKeyPair UserKeyPair => Accounts[1].KeyPair;
        protected Address UserAddress => Accounts[1].Address;

        protected ECKeyPair User2KeyPair => Accounts[2].KeyPair;
        protected Address User2Address => Accounts[2].Address;
        // protected readonly List<string> ResourceTokenSymbolList;
        // protected Timestamp BlockchainStartTimestamp => TimestampHelper.GetUtcNow();



        protected InscriptionContractTestBase()
        {
            InscriptionContractStub = GetInscriptionContractStub(DefaultKeyPair);
            TokenContractStub = GetTokenContractStub(DefaultKeyPair);
            TokenContractUserStub = GetTokenContractStub(UserKeyPair);
            TokenContractUser2Stub = GetTokenContractStub(User2KeyPair);
            // ResourceTokenSymbolList = Application.ServiceProvider
            //     .GetRequiredService<IOptionsSnapshot<HostSmartContractBridgeContextOptions>>()
            //     .Value.ContextVariables["SymbolListToPayRental"].Split(",").ToList();
            // BlockchainService = Application.ServiceProvider.GetRequiredService<IBlockchainService>();

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

        // protected void StartSideChain(int chainId, long height, string symbol,
        //     bool registerParentChainTokenContractAddress)
        // {
        //     SideChainTestKit = CreateContractTestKit<InscriptionContractTestModule>(
        //         new ChainInitializationDto
        //         {
        //             ChainId = chainId,
        //             Symbol = symbol,
        //             ParentChainTokenContractAddress = TokenContractAddress,
        //             ParentChainId = ChainHelper.ConvertBase58ToChainId("AELF"),
        //             CreationHeightOnParentChain = height,
        //             RegisterParentChainTokenContractAddress = registerParentChainTokenContractAddress
        //         });
        //     SideBasicContractZeroAddress = SideChainTestKit.ContractZeroAddress;
        //     SideChainBasicContractZeroStub =
        //         SideChainTestKit.GetTester<BasicContractZeroImplContainer.BasicContractZeroImplStub>(
        //             SideBasicContractZeroAddress);
        //
        //     SideCrossChainContractAddress =
        //         SideChainTestKit.SystemContractAddresses[CrossChainSmartContractAddressNameProvider.Name];
        //     SideChainCrossChainContractStub =
        //         SideChainTestKit.GetTester<CrossChainContractImplContainer.CrossChainContractImplStub>(
        //             SideCrossChainContractAddress);
        //
        //     SideTokenContractAddress =
        //         SideChainTestKit.SystemContractAddresses[TokenSmartContractAddressNameProvider.Name];
        //     SideChainTokenContractStub =
        //         SideChainTestKit.GetTester<TokenContractImplContainer.TokenContractImplStub>(SideTokenContractAddress);
        //     SideParliamentAddress =
        //         SideChainTestKit.SystemContractAddresses[ParliamentSmartContractAddressNameProvider.Name];
        //     SideChainParliamentContractStub =
        //         SideChainTestKit.GetTester<ParliamentContractImplContainer.ParliamentContractImplStub>(
        //             SideParliamentAddress);
        //
        //     SideConsensusAddress =
        //         SideChainTestKit.SystemContractAddresses[ConsensusSmartContractAddressNameProvider.Name];
        //     SideChainAEDPoSContractStub =
        //         SideChainTestKit.GetTester<AEDPoSContractContainer.AEDPoSContractStub>(SideConsensusAddress);
        // }
    }
}
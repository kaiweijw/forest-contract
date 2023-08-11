using System;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using AElf.Standards.ACS1;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Forest;

public class ForestContractTest_Init : ForestContractTestBase
{
    private const string NftSymbol = "TESTNFT-1";
    private const string NftSymbol2 = "TESTNFT-2";
    private const string ElfSymbol = "ELF";
    private const int ServiceFeeRate = 1000; // 10%
    private const long InitializeElfAmount = 10000_0000_0000;

    private async Task InitializeForestContract()
    {
        await AdminForestContractStub.Initialize.SendAsync(new InitializeInput
        {
            ServiceFeeReceiver = MarketServiceFeeReceiverAddress,
            ServiceFeeRate = ServiceFeeRate,
        });

        await AdminForestContractStub.SetWhitelistContract.SendAsync(WhitelistContractAddress);
    }
    
    
    private async Task PrepareNftData()
    {
        
        #region prepare SEED
        
        await CreateSeedCollection();
        await CreateSeed("SEED-1", "TESTNFT-0");
        await TokenContractStub.Issue.SendAsync(new IssueInput() { Symbol = "SEED-1", To = User1Address, Amount = 1 });
        await UserTokenContractStub.Approve.SendAsync(new ApproveInput() { Spender = TokenContractAddress, Symbol = "SEED-1", Amount = 1 });

        #endregion

        #region create NFTs

        {
            // create collections via MULTI-TOKEN-CONTRACT
            await UserTokenContractStub.Create.SendAsync(new CreateInput
            {
                Symbol = "TESTNFT-0",
                TokenName = "TESTNFT—collection",
                TotalSupply = 100,
                Decimals = 0,
                Issuer = User1Address,
                IsBurnable = false,
                IssueChainId = 0,
                ExternalInfo = new ExternalInfo()
            });

            // create NFT via MULTI-TOKEN-CONTRACT
            await UserTokenContractStub.Create.SendAsync(new CreateInput
            {
                Symbol = NftSymbol,
                TokenName = NftSymbol,
                TotalSupply = 100,
                Decimals = 0,
                Issuer = User1Address,
                IsBurnable = false,
                IssueChainId = 0,
                ExternalInfo = new ExternalInfo()
            });

            // create NFT via MULTI-TOKEN-CONTRACT
            await UserTokenContractStub.Create.SendAsync(new CreateInput
            {
                Symbol = NftSymbol2,
                TokenName = NftSymbol2,
                TotalSupply = 100,
                Decimals = 0,
                Issuer = User1Address,
                IsBurnable = false,
                IssueChainId = 0,
                ExternalInfo = new ExternalInfo()
            });
        }

        #endregion

        #region issue NFTs and check

        {
            // issue 10 NFTs to self
            await UserTokenContractStub.Issue.SendAsync(new IssueInput()
            {
                Symbol = NftSymbol,
                Amount = 10,
                To = User1Address
            });

            // issue 10 NFTs to self
            await UserTokenContractStub.Issue.SendAsync(new IssueInput()
            {
                Symbol = NftSymbol2,
                Amount = 5,
                To = User1Address
            });

            // got 100-totalSupply and 10-supply
            var tokenInfo = await UserTokenContractStub.GetTokenInfo.SendAsync(new GetTokenInfoInput()
            {
                Symbol = NftSymbol,
            });

            tokenInfo.Output.TotalSupply.ShouldBe(100);
            tokenInfo.Output.Supply.ShouldBe(10);


            var nftBalance = await UserTokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
            {
                Symbol = NftSymbol,
                Owner = User1Address
            });
            nftBalance.Output.Balance.ShouldBe(10);
        }

        #endregion

        #region prepare ELF token

        {
            // transfer thousand ELF to seller
            await TokenContractStub.Transfer.SendAsync(new TransferInput()
            {
                To = User1Address,
                Symbol = ElfSymbol,
                Amount = InitializeElfAmount
            });

            // transfer thousand ELF to buyer
            await TokenContractStub.Transfer.SendAsync(new TransferInput()
            {
                To = User2Address,
                Symbol = ElfSymbol,
                Amount = InitializeElfAmount
            });
        }

        #endregion

        #region approve transfer

        {
            // approve contract handle NFT of seller   
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Symbol = NftSymbol,
                Amount = 5,
                Spender = ForestContractAddress
            });

            // approve contract handle NFT2 of seller   
            await UserTokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Symbol = NftSymbol2,
                Amount = 5,
                Spender = ForestContractAddress
            });

            // approve contract handle ELF of buyer   
            await User2TokenContractStub.Approve.SendAsync(new ApproveInput()
            {
                Symbol = ElfSymbol,
                Amount = InitializeElfAmount,
                Spender = ForestContractAddress
            });
        }

        #endregion
    }

    [Fact]
    public async Task InitTest()
    {
        var exception = await Assert.ThrowsAsync<Exception>(() => AdminForestContractStub.Initialize.SendAsync(new InitializeInput
        {
            ServiceFeeReceiver = MarketServiceFeeReceiverAddress,
            ServiceFeeRate = ServiceFeeRate,
            WhitelistContractAddress = null
        }));
        exception.Message.ShouldContain("Empty WhitelistContractAddress");
        
        var res = await AdminForestContractStub.Initialize.SendAsync(new InitializeInput
        {
            ServiceFeeReceiver = MarketServiceFeeReceiverAddress,
            ServiceFeeRate = ServiceFeeRate,
            WhitelistContractAddress = WhitelistContractAddress
        });
        res.ShouldNotBeNull();
    }
    

    [Fact]
    public async Task MethodFeeTest()
    {
        await InitializeForestContract();
        await AdminForestContractStub.SetMethodFee.SendAsync(new MethodFees());
        await AdminForestContractStub.ChangeMethodFeeController.SendAsync(new AuthorityInfo());
        var methodFee = await AdminForestContractStub.GetMethodFee.SendAsync(new StringValue());
        methodFee.ShouldNotBeNull();
        var methodFeeController = await AdminForestContractStub.GetMethodFeeController.SendAsync(new Empty());
        methodFeeController.ShouldNotBeNull();
    }

    [Fact]
    public async Task GlobalTokenWhiteListTest()
    {
        await InitializeForestContract();
        
        await AdminForestContractStub.SetGlobalTokenWhiteList.SendAsync(new StringList()
        {
            Value = { ElfSymbol }
        });

        var list = await AdminForestContractStub.GetGlobalTokenWhiteList.SendAsync(new Empty());
        list?.Output?.Value.ShouldNotBeNull();
        list?.Output?.Value.Count.ShouldBe(1);
        list?.Output?.Value[0].ShouldBe(ElfSymbol);
    }
    

    [Fact]
    public async Task NFTTokenWhiteListTest()
    {
        await InitializeForestContract();
        await PrepareNftData();
        
        await Seller1ForestContractStub.SetTokenWhiteList.SendAsync(new() 
        {
            Symbol = "TESTNFT-0",
            TokenWhiteList = new StringList()
            {
                Value = { ElfSymbol }
            }
        });

        var list = await Seller1ForestContractStub.GetTokenWhiteList.SendAsync(new StringValue()
        {
            Value = "TESTNFT-0"
        });
        list?.Output?.Value.ShouldNotBeNull();
        list?.Output?.Value.Count.ShouldBe(1);
        list?.Output?.Value[0].ShouldBe(ElfSymbol);
    }
    

    [Fact]
    public async Task RoyaltyTest()
    {
        await InitializeForestContract();
        await PrepareNftData();

        var res = await Seller1ForestContractStub.SetRoyalty.SendAsync(new SetRoyaltyInput()
        {
            Symbol = NftSymbol,
            Royalty = 100,
            RoyaltyFeeReceiver = User1Address
        });

        var royalty = await Seller1ForestContractStub.GetRoyalty.SendAsync(new GetRoyaltyInput()
        {
            Symbol = NftSymbol
        });

        royalty?.Output.ShouldNotBeNull();
        royalty?.Output.Royalty.ShouldBe(100);
    }

    [Fact]
    public async Task BizConfigTest()
    {
        await InitializeForestContract();

        var bizConfig = await Seller1ForestContractStub.GetBizConfig.SendAsync(new Empty());
        bizConfig?.Output.ShouldNotBeNull();
        bizConfig?.Output.MaxListCount.ShouldBe(100);
        bizConfig?.Output.MaxOfferCount.ShouldBe(100);
        bizConfig?.Output.MaxTokenWhitelistCount.ShouldBe(20);

        await AdminForestContractStub.SetBizConfig.SendAsync(new BizConfig()
        {
            MaxListCount = 1,
            MaxOfferCount = 1,
            MaxTokenWhitelistCount = 1
        });
        bizConfig = await Seller1ForestContractStub.GetBizConfig.SendAsync(new Empty());
        bizConfig?.Output.ShouldNotBeNull();
        bizConfig?.Output.MaxListCount.ShouldBe(1);
        bizConfig?.Output.MaxOfferCount.ShouldBe(1);
        bizConfig?.Output.MaxTokenWhitelistCount.ShouldBe(1);
        
        var exception = await Assert.ThrowsAsync<Exception>(() => AdminForestContractStub.SetBizConfig.SendAsync(new BizConfig()
        {
            //EMPTY
        }));
        exception.Message.ShouldContain("should greater than 0");
        
        exception = await Assert.ThrowsAsync<Exception>(() => AdminForestContractStub.SetBizConfig.SendAsync(new BizConfig()
        {
            MaxListCount = 0,
            MaxOfferCount = 1,
            MaxTokenWhitelistCount = 1
        }));
        exception.Message.ShouldContain("should greater than 0");
        
        exception = await Assert.ThrowsAsync<Exception>(() => Seller1ForestContractStub.SetBizConfig.SendAsync(new BizConfig()
        {
            MaxListCount = 0,
            MaxOfferCount = 1,
            MaxTokenWhitelistCount = 1
        }));
        exception.Message.ShouldContain("No permission");
        

    }
    
}
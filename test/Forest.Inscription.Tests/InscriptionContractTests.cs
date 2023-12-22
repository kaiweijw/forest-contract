using System;
using System.Text;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using Shouldly;
using Xunit;

namespace Forest.Inscription;

public class InscriptionContractTests : InscriptionContractTestBase
{
    private readonly string _tick = "ELFS";

    [Fact]
    public async Task DeployInscriptionTest()
    {
        await InscriptionContractStub.Initialize.SendAsync(new InitializeInput());
        await BuySeed();
        await InscriptionContractStub.DeployInscription.SendAsync(new DeployInscriptionInput
        {
            Tick = _tick,
            SeedSymbol = "SEED-1",
            IssueChainId = 1866392,
            Max = 21000000,
            Limit = 1000,
            Image = ""
        });
    }

    private async Task BuySeed()
    {
        await TokenContractStub.Create.SendAsync(new CreateInput
        {
            Symbol = "SEED-0",
            TokenName = "SEED-0 token",
            TotalSupply = 1,
            Decimals = 0,
            Issuer = DefaultAddress,
            IsBurnable = true,
            IssueChainId = 0,
        });

        var seedOwnedSymbol = "ELFS" + "-0";
        var seedExpTime = "1720590467";
        await TokenContractStub.Create.SendAsync(new CreateInput
        {
            Symbol = "SEED-1",
            TokenName = "SEED-1 token",
            TotalSupply = 1,
            Decimals = 0,
            Issuer = DefaultAddress,
            IsBurnable = true,
            IssueChainId = 0,
            LockWhiteList = { TokenContractAddress },
            ExternalInfo = new ExternalInfo()
            {
                Value =
                {
                    {
                        "__seed_owned_symbol",
                        seedOwnedSymbol
                    },
                    {
                        "__seed_exp_time",
                        seedExpTime
                    }
                }
            }
        });

        await TokenContractStub.Issue.SendAsync(new IssueInput
        {
            Symbol = "SEED-1",
            Amount = 1,
            To = DefaultAddress,
            Memo = ""
        });

        var balance = await TokenContractStub.GetBalance.SendAsync(new GetBalanceInput()
        {
            Owner = DefaultAddress,
            Symbol = "SEED-1"
        });
        balance.Output.Balance.ShouldBe(1);
        await TokenContractStub.Approve.SendAsync(new ApproveInput()
        {
            Symbol = "SEED-1",
            Amount = 1,
            Spender = InscriptionContractAddress
        });
    }
}
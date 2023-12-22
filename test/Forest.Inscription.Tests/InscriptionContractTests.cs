using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AElf.Contracts.MultiToken;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace Forest.Inscription;

public class InscriptionContractTests : InscriptionContractTestBase
{
    private readonly string _tick = "ELFS";

    private readonly string _image =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAADfklEQVR4nO2Yy2sTQRzHv7t5lTa2HtRjaGhJpfRSkFAPtiJBL3rz0iBWkC6ilLTE+ijYg0p9hlaLoCko9pDc/Av2UFvBEq+12lgIBk9WkEJqk5rselh3O9nsYybbood+IWTnN7uzn/nOY3+7XCopyfiPxf9rADvtATrVHqBTuZ1cLAx5tePk9JZjGCPVDSgMedEvFgAA6Yif6nxVLJ2pC5CEm569hTQFWM/EMoLhAFVnHAEKQ170TCxrcLRg9coNAJGpkBYQh7PaMRkn64xuSA6hEZjaGSu3DQH1EPqyHsLMPTWuanGsE4sA+sWCpdO2gKwXGLn34/wIDox1avMSAIJiAblMHumIH2n1nNlJ5tXOfe9slw9eX9cCaw9atGMy7rqwhovzRabGSb051YRShWMGdJMQeihV+4SfOPE4WxNnUfedT3g/1sV8HdWTpFAyHloWBcMBHJ1YQlRgm1XK2b2cUprXpYa9HHxta4gkVhzBqaqnk5ycO2SZsPra1jDwdpOp0VwmXwVElnOZPMT4YaSSZaq2diVZGAicRv75yaqyGA9pkE1ejrotHgDGW4sYb61doUYxGp05m7Usb2zRv2W4SYh6gXZTOzLEuUxem2cAMPquiL6XnwEAsW4fvm3IWCpsu9bc6KFu2w0AT6aUvS823FJVqcTt+2C0OmPdPgBA+8i17djkQwDAK5YhVuFIUFIuSXHIbIvQr1gSzkzlMt0KBijs8UxKmB/tMK0PhgOWcKt/XVP/WcUDwNWnyk8vMk7OMb30c7DL76qqb5hJaMdHml3UeyAAcLeDMtWEuPuVR++jFdungeogCblUqAAAhl98YdqkmQDrgdSry8/mHgBw6reZ1Xu1u3v7Tbnq4d7i9WCzrKRLdqAkZD3O1QCaSZ99qDe5HHPhV8kYlJyPH26EsP4bSCUlZjgqQAA4d8kNiWifdELtgJeXDZMKJTkwf42wE1VyJll0vgr2OI/BuerHZTAcwOBC0XC/pBGVg4D5UJMauOJD3/2Ptps6i5jSWy/nwpZcMa1//ayEqMBjcME46QiGA0hF9mOjRJ+UUDsI0LkYFXhEElnHrwiqHH08MlIqKVm6yDrMTA4CO+PizLEG6vvtuIPAtotmkOqqFuMh2/2ROWHVO2b2GplKSpiLd5gOqbr9RAVrBMcZdaPHHHI2WYEYD1nOu0giawn5B8F9gRyqFJDiAAAAAElFTkSuQmCC";

    [Fact]
    public async Task DeployInscriptionTest()
    {
        await InscriptionContractStub.Initialize.SendAsync(new InitializeInput());
        await BuySeed();
        var result = await InscriptionContractStub.DeployInscription.SendAsync(new DeployInscriptionInput
        {
            Tick = _tick,
            SeedSymbol = "SEED-1",
            IssueChainId = 1866392,
            Max = 21000000,
            Limit = 1000,
            Image = _image
        });
        {
            var tokenInfo = await TokenContractStub.GetTokenInfo.CallAsync(new GetTokenInfoInput
            {
                Symbol = "ELFS-0",
            });
            var info = @"{""p"":""aelf"",""op"":""deploy"",""tick"":""ELFS"",""max"":""21000000"",""lim"":""1000""}";
            tokenInfo.ExternalInfo.Value["inscription_deploy"].ShouldBe(info);
            tokenInfo.ExternalInfo.Value["inscription_image"].ShouldBe(_image);
            tokenInfo.Owner.ShouldBe(InscriptionContractAddress);
            tokenInfo.IssueChainId.ShouldBe(1866392);
            tokenInfo.Issued.ShouldBe(0);
            tokenInfo.TotalSupply.ShouldBe(21000000);
        }
        {
            var tokenInfo = await TokenContractStub.GetTokenInfo.CallAsync(new GetTokenInfoInput
            {
                Symbol = "ELFS-1",
            });
            var info = @"{""p"":""aelf"",""op"":""mint"",""tick"":""ELFS"",""amt"":""1""}";
            tokenInfo.ExternalInfo.Value["inscription_mint"].ShouldBe(info);
            tokenInfo.ExternalInfo.Value["inscription_image"].ShouldBe(_image);
            tokenInfo.Owner.ShouldBe(InscriptionContractAddress);
            tokenInfo.IssueChainId.ShouldBe(1866392);
            tokenInfo.Issued.ShouldBe(0);
            tokenInfo.TotalSupply.ShouldBe(21000000);
        }
        {
            var l = InscriptionCreated.Parser.ParseFrom(result.TransactionResult.Logs
                .FirstOrDefault(l => l.Name == nameof(InscriptionCreated))?.NonIndexed);
            l.TotalSupply.ShouldBe(21000000);
            l.Decimals.ShouldBe(0);
            l.Deployer.ShouldBe(DefaultAddress);
            l.Tick.ShouldBe("ELFS");
            l.Limit.ShouldBe(1000);
            var info = @"{""p"":""aelf"",""op"":""mint"",""tick"":""ELFS"",""amt"":""1""}";
            l.ItemExternalInfo.Value["inscription_mint"].ShouldBe(info);
            var info1 = @"{""p"":""aelf"",""op"":""deploy"",""tick"":""ELFS"",""max"":""21000000"",""lim"":""1000""}";
            l.CollectionExternalInfo.Value["inscription_deploy"].ShouldBe(info1);
        }
    }

    [Fact]
    public async Task IssueTest()
    {
        await DeployInscriptionTest();
        await InscriptionContractStub.IssueInscription.SendAsync(new IssueInscriptionInput
        {
            Tick = "ELFS"
        });
        var list = await InscriptionContractStub.GetDistributorList.CallAsync(new StringValue()
        {
            Value = "ELFS"
        });
        foreach (var distributor in list.Values)
        {
            var balanceOutput = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = distributor,
                Symbol = "ELFS-1"
            });
            balanceOutput.Balance.ShouldBe(2100000);
        }
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
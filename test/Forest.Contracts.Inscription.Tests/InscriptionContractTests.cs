using System;
using System.Linq;
using System.Threading.Tasks;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;


namespace Forest.Contracts.Inscription;

public partial class InscriptionContractTests : InscriptionContractTestBase
{
    private readonly string _tick = "ELFS";

    private readonly string _image =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAACgAAAAoCAYAAACM/rhtAAADfklEQVR4nO2Yy2sTQRzHv7t5lTa2HtRjaGhJpfRSkFAPtiJBL3rz0iBWkC6ilLTE+ijYg0p9hlaLoCko9pDc/Av2UFvBEq+12lgIBk9WkEJqk5rselh3O9nsYybbood+IWTnN7uzn/nOY3+7XCopyfiPxf9rADvtATrVHqBTuZ1cLAx5tePk9JZjGCPVDSgMedEvFgAA6Yif6nxVLJ2pC5CEm569hTQFWM/EMoLhAFVnHAEKQ170TCxrcLRg9coNAJGpkBYQh7PaMRkn64xuSA6hEZjaGSu3DQH1EPqyHsLMPTWuanGsE4sA+sWCpdO2gKwXGLn34/wIDox1avMSAIJiAblMHumIH2n1nNlJ5tXOfe9slw9eX9cCaw9atGMy7rqwhovzRabGSb051YRShWMGdJMQeihV+4SfOPE4WxNnUfedT3g/1sV8HdWTpFAyHloWBcMBHJ1YQlRgm1XK2b2cUprXpYa9HHxta4gkVhzBqaqnk5ycO2SZsPra1jDwdpOp0VwmXwVElnOZPMT4YaSSZaq2diVZGAicRv75yaqyGA9pkE1ejrotHgDGW4sYb61doUYxGp05m7Usb2zRv2W4SYh6gXZTOzLEuUxem2cAMPquiL6XnwEAsW4fvm3IWCpsu9bc6KFu2w0AT6aUvS823FJVqcTt+2C0OmPdPgBA+8i17djkQwDAK5YhVuFIUFIuSXHIbIvQr1gSzkzlMt0KBijs8UxKmB/tMK0PhgOWcKt/XVP/WcUDwNWnyk8vMk7OMb30c7DL76qqb5hJaMdHml3UeyAAcLeDMtWEuPuVR++jFdungeogCblUqAAAhl98YdqkmQDrgdSry8/mHgBw6reZ1Xu1u3v7Tbnq4d7i9WCzrKRLdqAkZD3O1QCaSZ99qDe5HHPhV8kYlJyPH26EsP4bSCUlZjgqQAA4d8kNiWifdELtgJeXDZMKJTkwf42wE1VyJll0vgr2OI/BuerHZTAcwOBC0XC/pBGVg4D5UJMauOJD3/2Ptps6i5jSWy/nwpZcMa1//ayEqMBjcME46QiGA0hF9mOjRJ+UUDsI0LkYFXhEElnHrwiqHH08MlIqKVm6yDrMTA4CO+PizLEG6vvtuIPAtotmkOqqFuMh2/2ROWHVO2b2GplKSpiLd5gOqbr9RAVrBMcZdaPHHHI2WYEYD1nOu0giawn5B8F9gRyqFJDiAAAAAElFTkSuQmCC";

    private readonly int _mainChainId = 9992731;
    private readonly int _sideChainId = 1866392;


    [Fact]
    public async Task InitializeTest_Success()
    {
        await InscriptionContractStub.Initialize.SendAsync(new InitializeInput
        {
            Admin = DefaultAddress,
            IssueChainId = _mainChainId
        });
        var admin = await InscriptionContractStub.GetAdmin.CallAsync(new Empty());
        admin.ShouldBe(DefaultAddress);
        var issueChainId = await InscriptionContractStub.GetIssueChainId.CallAsync(new Empty());
        issueChainId.Value.ShouldBe(_mainChainId);
    }

    [Fact]
    public async Task InitializeTest_Failed_AlreadyInitialized()
    {
        await InitializeTest_Success();
        var result = await InscriptionContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
        {
            Admin = DefaultAddress,
            IssueChainId = _mainChainId
        });
        result.TransactionResult.Error.ShouldContain("Already initialized.");
    }

    [Fact]
    public async Task InitializeTest_Failed_NoPermission()
    {
        var result = await InscriptionContractAccount1Stub.Initialize.SendWithExceptionAsync(new InitializeInput
        {
            Admin = DefaultAddress,
            IssueChainId = _mainChainId
        });
        result.TransactionResult.Error.ShouldContain("No permission.");
    }

    [Fact]
    public async Task InitializeTest_Failed_InvalidInput()
    {
        var result = await InscriptionContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
        {
            IssueChainId = 1
        });
        result.TransactionResult.Error.ShouldContain("Invalid input.");
        var result1 = await InscriptionContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
        {
            Admin = new Address(),
            IssueChainId = 1
        });
        result1.TransactionResult.Error.ShouldContain("Invalid input.");
        var result2 = await InscriptionContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
        {
            Admin = DefaultAddress
        });
        result2.TransactionResult.Error.ShouldContain("Invalid input.");
        var result3 = await InscriptionContractStub.Initialize.SendWithExceptionAsync(new InitializeInput
        {
            Admin = DefaultAddress,
            IssueChainId = -1
        });
        result3.TransactionResult.Error.ShouldContain("Invalid input.");
    }

    [Fact]
    public async Task ChangeAdmin_Success()
    {
        await InitializeTest_Success();
        await InscriptionContractStub.ChangeAdmin.SendAsync(UserAddress);
        var admin = await InscriptionContractStub.GetAdmin.CallAsync(new Empty());
        admin.ShouldBe(UserAddress);
    }

    [Fact]
    public async Task ChangeAdmin_Failed_NoPermission()
    {
        await InitializeTest_Success();
        var result = await InscriptionContractAccount1Stub.ChangeAdmin.SendWithExceptionAsync(User2Address);
        result.TransactionResult.Error.ShouldContain("No permission.");
        var admin = await InscriptionContractStub.GetAdmin.CallAsync(new Empty());
        admin.ShouldBe(DefaultAddress);
    }

    [Fact]
    public async Task ChangeAdmin_Failed_InvalidInput()
    {
        await InitializeTest_Success();
        var result = await InscriptionContractStub.ChangeAdmin.SendWithExceptionAsync(new Address());
        result.TransactionResult.Error.ShouldContain("Invalid input.");
    }


    [Fact]
    public async Task DeployInscriptionTest_Success()
    {
        await InitializeTest_Success();
        await BuySeed();
        var result = await InscriptionContractStub.DeployInscription.SendAsync(new DeployInscriptionInput
        {
            Tick = _tick,
            SeedSymbol = "SEED-1",
            Max = 21000000,
            Limit = 1000,
            Image = _image
        });
        {
            var tokenInfo = await TokenContractStub.GetTokenInfo.CallAsync(new GetTokenInfoInput
            {
                Symbol = "ELFS-0",
            });
            var info =
                @"{ ""p"": ""aelf"", ""op"": ""deploy"", ""tick"": ""ELFS"", ""max"": ""21000000"", ""lim"": ""1000"" }";
            tokenInfo.ExternalInfo.Value["inscription_deploy"].ShouldBe(info);
            tokenInfo.ExternalInfo.Value["inscription_image"].ShouldBe(_image);
            tokenInfo.Owner.ShouldBe(InscriptionContractAddress);
            tokenInfo.IssueChainId.ShouldBe(_mainChainId);
            tokenInfo.Issued.ShouldBe(0);
            tokenInfo.TotalSupply.ShouldBe(21000000);
        }
        {
            var tokenInfo = await TokenContractStub.GetTokenInfo.CallAsync(new GetTokenInfoInput
            {
                Symbol = "ELFS-1",
            });
            var info = @"{ ""p"": ""aelf"", ""op"": ""mint"", ""tick"": ""ELFS"", ""amt"": ""1"" }";
            tokenInfo.ExternalInfo.Value["inscription_mint"].ShouldBe(info);
            tokenInfo.ExternalInfo.Value["inscription_image"].ShouldBe(_image);
            tokenInfo.Owner.ShouldBe(InscriptionContractAddress);
            tokenInfo.IssueChainId.ShouldBe(_mainChainId);
            tokenInfo.Issued.ShouldBe(0);
            tokenInfo.TotalSupply.ShouldBe(21000000);
        }
        {
            var l = InscriptionCreated.Parser.ParseFrom(result.TransactionResult.Logs
                .FirstOrDefault(l => l.Name == nameof(InscriptionCreated))?.NonIndexed);
            l.TotalSupply.ShouldBe(21000000);
            l.Deployer.ShouldBe(DefaultAddress);
            l.Tick.ShouldBe("ELFS");
            l.Limit.ShouldBe(1000);
            var info = @"{ ""p"": ""aelf"", ""op"": ""mint"", ""tick"": ""ELFS"", ""amt"": ""1"" }";
            l.ItemExternalInfo.Value["inscription_mint"].ShouldBe(info);
            var info1 =
                @"{ ""p"": ""aelf"", ""op"": ""deploy"", ""tick"": ""ELFS"", ""max"": ""21000000"", ""lim"": ""1000"" }";
            l.CollectionExternalInfo.Value["inscription_deploy"].ShouldBe(info1);
        }
    }

    [Fact]
    public async Task DeployInscriptionTest_Failed_NotInitialized()
    {
        await BuySeed();
        var result = await InscriptionContractStub.DeployInscription.SendWithExceptionAsync(new DeployInscriptionInput
        {
            Tick = _tick,
            SeedSymbol = "SEED-1",
            Max = 21000000,
            Limit = 1000,
            Image = _image
        });
        result.TransactionResult.Error.ShouldContain("Not initialized yet.");
    }

    [Fact]
    public async Task DeployInscriptionTest_Failed_InvalidInput()
    {
        await InitializeTest_Success();
        await BuySeed();
        {
            var result = await InscriptionContractStub.DeployInscription.SendWithExceptionAsync(
                new DeployInscriptionInput
                {
                    Tick = _tick,
                    Max = 21000000,
                    Limit = 1000,
                    Image = _image
                });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var result = await InscriptionContractStub.DeployInscription.SendWithExceptionAsync(
                new DeployInscriptionInput
                {
                    Tick = _tick,
                    SeedSymbol = "",
                    Max = 21000000,
                    Limit = 1000,
                    Image = _image
                });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var result = await InscriptionContractStub.DeployInscription.SendWithExceptionAsync(
                new DeployInscriptionInput
                {
                    Tick = _tick,
                    SeedSymbol = "SEED-2",
                    Max = 21000000,
                    Limit = 1000,
                    Image = _image
                });
            result.TransactionResult.Error.ShouldContain("Token is not found.");
        }
        {
            var result = await InscriptionContractStub.DeployInscription.SendWithExceptionAsync(
                new DeployInscriptionInput
                {
                    SeedSymbol = "SEED-1",
                    Max = 21000000,
                    Limit = 1000,
                    Image = _image
                });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            {
                var result = await InscriptionContractStub.DeployInscription.SendWithExceptionAsync(
                    new DeployInscriptionInput
                    {
                        Tick = "",
                        SeedSymbol = "SEED-1",
                        Max = 21000000,
                        Limit = 1000,
                        Image = _image
                    });
                result.TransactionResult.Error.ShouldContain("Invalid input.");
            }
        }
        {
            var result = await InscriptionContractStub.DeployInscription.SendWithExceptionAsync(
                new DeployInscriptionInput
                {
                    Tick = _tick,
                    SeedSymbol = "SEED-1",
                    Max = 0,
                    Limit = 1000,
                    Image = _image
                });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var result = await InscriptionContractStub.DeployInscription.SendWithExceptionAsync(
                new DeployInscriptionInput
                {
                    Tick = _tick,
                    SeedSymbol = "SEED-1",
                    Max = -1,
                    Limit = 1000,
                    Image = _image
                });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var result = await InscriptionContractStub.DeployInscription.SendWithExceptionAsync(
                new DeployInscriptionInput
                {
                    Tick = _tick,
                    SeedSymbol = "SEED-1",
                    Max = 21000000,
                    Limit = 0,
                    Image = _image
                });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var result = await InscriptionContractStub.DeployInscription.SendWithExceptionAsync(
                new DeployInscriptionInput
                {
                    Tick = _tick,
                    SeedSymbol = "SEED-1",
                    Max = 21000000,
                    Limit = -1,
                    Image = _image
                });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var result = await InscriptionContractStub.DeployInscription.SendWithExceptionAsync(
                new DeployInscriptionInput
                {
                    Tick = _tick,
                    SeedSymbol = "SEED-1",
                    Max = 21000000,
                    Limit = 21000001,
                    Image = _image
                });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var result = await InscriptionContractStub.DeployInscription.SendWithExceptionAsync(
                new DeployInscriptionInput
                {
                    Tick = _tick,
                    SeedSymbol = "SEED-1",
                    Max = 21000000,
                    Limit = 1000,
                    Image = ""
                });
            result.TransactionResult.Error.ShouldContain("Invalid image data.");
        }
        {
            var result = await InscriptionContractStub.DeployInscription.SendWithExceptionAsync(
                new DeployInscriptionInput
                {
                    Tick = _tick,
                    SeedSymbol = "SEED-1",
                    Max = 21000000,
                    Limit = 1000
                });
            result.TransactionResult.Error.ShouldContain("Invalid image data.");
        }

    }

    [Fact]
    public async Task DeployInscriptionTest_Failed_Repeat()
    {
        await DeployInscriptionTest_Success();
        var result1 = await InscriptionContractStub.DeployInscription.SendWithExceptionAsync(new DeployInscriptionInput
        {
            Tick = _tick,
            SeedSymbol = "SEED-1",
            Max = 21000000,
            Limit = 1000,
            Image = _image
        });
        result1.TransactionResult.Error.ShouldContain("Insufficient allowance.");
    }
    
    [Fact]
    public async Task DeployInscriptionTest_Failed_TokenExist()
    {
        await InitializeTest_Success();
        await BuySeed();
        var result1 = await InscriptionContractStub.DeployInscription.SendWithExceptionAsync(new DeployInscriptionInput
        {
            Tick = "ELFSS",
            SeedSymbol = "SEED-1",
            Max = 21000000,
            Limit = 1000,
            Image = _image
        });
        result1.TransactionResult.Error.ShouldContain("Seed NFT does not exist.");
    }

    [Fact]
    public async Task IssueTest_Success()
    {
        await DeployInscriptionTest_Success();
        var result = await InscriptionContractStub.IssueInscription.SendAsync(new IssueInscriptionInput
        {
            Tick = "ELFS"
        });
        {
            var log = InscriptionIssued.Parser.ParseFrom(result.TransactionResult.Logs
                .FirstOrDefault(l => l.Name == nameof(InscriptionIssued))?.NonIndexed);
            log.Tick.ShouldBe("ELFS");
            log.To.ShouldBe(InscriptionContractAddress);
            var info =
                @"{ ""p"": ""aelf"", ""op"": ""deploy"", ""tick"": ""ELFS"", ""max"": ""21000000"", ""lim"": ""1000"" }";
            log.InscriptionInfo.ShouldBe(info);
            log.Amt.ShouldBe(21000000);
        }
        var list = await InscriptionContractStub.GetDistributorList.CallAsync(new StringValue
        {
            Value = "ELFS"
        });
        list.Values.Count.ShouldBe(10);
        foreach (var distributor in list.Values)
        {
            var balanceOutput = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = distributor,
                Symbol = "ELFS-1"
            });
            balanceOutput.Balance.ShouldBe(2100000);
        }
        {
            var balanceList = await InscriptionContractStub.GetDistributorBalance.CallAsync(new StringValue
            {
                Value = "ELFS"
            });
            for (var i = 0; i < balanceList.Values.Count; i++)
            {
                balanceList.Values[i].Distributor.ShouldBe(list.Values[i]);
                balanceList.Values[i].Balance.ShouldBe(2100000);
            }
        }
        {
            var check = await InscriptionContractStub.CheckDistributorBalance.CallAsync(new CheckDistributorBalanceInput
            {
                Sender = DefaultAddress,
                Tick = "ELFS",
                Amt = 1000
            });
            check.Value.ShouldBe(true);
        }
    }

    [Fact]
    public async Task IssueTest_Success_NotAverage()
    {
        await InitializeTest_Success();
        await BuySeed();
        await InscriptionContractStub.SetDistributorCount.SendAsync(new Int32Value
        {
            Value = 5
        });
        var count = await InscriptionContractStub.GetDistributorCount.CallAsync(new Empty());
        count.Value.ShouldBe(5);
        await InscriptionContractStub.DeployInscription.SendAsync(new DeployInscriptionInput
        {
            Tick = _tick,
            SeedSymbol = "SEED-1",
            Max = 21000001,
            Limit = 1000,
            Image = _image
        });
        var result = await InscriptionContractStub.IssueInscription.SendAsync(new IssueInscriptionInput
        {
            Tick = "ELFS"
        });
        {
            var limit = await InscriptionContractStub.GetInscribedLimit.CallAsync(new StringValue
            {
                Value = "ELFS"
            });
            limit.Value.ShouldBe(1000);
        }
        {
            var log = InscriptionIssued.Parser.ParseFrom(result.TransactionResult.Logs
                .FirstOrDefault(l => l.Name == nameof(InscriptionIssued))?.NonIndexed);
            log.Tick.ShouldBe("ELFS");
            log.To.ShouldBe(InscriptionContractAddress);
            var info =
                @"{ ""p"": ""aelf"", ""op"": ""deploy"", ""tick"": ""ELFS"", ""max"": ""21000001"", ""lim"": ""1000"" }";
            log.InscriptionInfo.ShouldBe(info);
            log.Amt.ShouldBe(21000001);
        }
        var list = await InscriptionContractStub.GetDistributorList.CallAsync(new StringValue
        {
            Value = "ELFS"
        });
        list.Values.Count.ShouldBe(5);
        foreach (var distributor in list.Values)
        {
            var balanceOutput = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = distributor,
                Symbol = "ELFS-1"
            });
            balanceOutput.Balance.ShouldBe(list.Values.IndexOf(distributor) == 0 ? 4200001 : 4200000);
        }
        {
            var balanceList = await InscriptionContractStub.GetDistributorBalance.CallAsync(new StringValue
            {
                Value = "ELFS"
            });
            balanceList.Values[0].Distributor.ShouldBe(list.Values[0]);
            balanceList.Values[0].Balance.ShouldBe(4200001);
            for (var i = 1; i < balanceList.Values.Count; i++)
            {
                balanceList.Values[i].Distributor.ShouldBe(list.Values[i]);
                balanceList.Values[i].Balance.ShouldBe(4200000);
            }
        }
    }
    
    [Fact]
    public async Task IssueTest_Success_TotalSupplyLessThanCount()
    {
        await InitializeTest_Success();
        await BuySeed();
        await InscriptionContractStub.DeployInscription.SendAsync(new DeployInscriptionInput
        {
            Tick = _tick,
            SeedSymbol = "SEED-1",
            Max = 1,
            Limit = 1,
            Image = _image
        });
        var result = await InscriptionContractStub.IssueInscription.SendAsync(new IssueInscriptionInput
        {
            Tick = "ELFS"
        });
        {
            var limit = await InscriptionContractStub.GetInscribedLimit.CallAsync(new StringValue
            {
                Value = "ELFS"
            });
            limit.Value.ShouldBe(1);
        }
        {
            var log = InscriptionIssued.Parser.ParseFrom(result.TransactionResult.Logs
                .FirstOrDefault(l => l.Name == nameof(InscriptionIssued))?.NonIndexed);
            log.Tick.ShouldBe("ELFS");
            log.To.ShouldBe(InscriptionContractAddress);
            var info =
                @"{ ""p"": ""aelf"", ""op"": ""deploy"", ""tick"": ""ELFS"", ""max"": ""1"", ""lim"": ""1"" }";
            log.InscriptionInfo.ShouldBe(info);
            log.Amt.ShouldBe(1);
        }
        var list = await InscriptionContractStub.GetDistributorList.CallAsync(new StringValue
        {
            Value = "ELFS"
        });
        list.Values.Count.ShouldBe(1);
        foreach (var distributor in list.Values)
        {
            var balanceOutput = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = distributor,
                Symbol = "ELFS-1"
            });
            balanceOutput.Balance.ShouldBe(list.Values.IndexOf(distributor) == 0 ? 1 : 0);
        }
        {
            var balanceList = await InscriptionContractStub.GetDistributorBalance.CallAsync(new StringValue
            {
                Value = "ELFS"
            });
            balanceList.Values.Count.ShouldBe(1);
            balanceList.Values[0].Distributor.ShouldBe(list.Values[0]);
            balanceList.Values[0].Balance.ShouldBe(1);
        }
    }
    
    [Fact]
    public async Task IssueTest_Success_TotalSupplyLessThanCount_9()
    {
        await InitializeTest_Success();
        await BuySeed();
        await InscriptionContractStub.DeployInscription.SendAsync(new DeployInscriptionInput
        {
            Tick = _tick,
            SeedSymbol = "SEED-1",
            Max = 9,
            Limit = 1,
            Image = _image
        });
        var result = await InscriptionContractStub.IssueInscription.SendAsync(new IssueInscriptionInput
        {
            Tick = "ELFS"
        });
        {
            var limit = await InscriptionContractStub.GetInscribedLimit.CallAsync(new StringValue
            {
                Value = "ELFS"
            });
            limit.Value.ShouldBe(1);
        }
        {
            var log = InscriptionIssued.Parser.ParseFrom(result.TransactionResult.Logs
                .FirstOrDefault(l => l.Name == nameof(InscriptionIssued))?.NonIndexed);
            log.Tick.ShouldBe("ELFS");
            log.To.ShouldBe(InscriptionContractAddress);
            var info =
                @"{ ""p"": ""aelf"", ""op"": ""deploy"", ""tick"": ""ELFS"", ""max"": ""9"", ""lim"": ""1"" }";
            log.InscriptionInfo.ShouldBe(info);
            log.Amt.ShouldBe(9);
        }
        var list = await InscriptionContractStub.GetDistributorList.CallAsync(new StringValue
        {
            Value = "ELFS"
        });
        list.Values.Count.ShouldBe(1);
        foreach (var distributor in list.Values)
        {
            var balanceOutput = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = distributor,
                Symbol = "ELFS-1"
            });
            balanceOutput.Balance.ShouldBe(list.Values.IndexOf(distributor) == 0 ? 9 : 0);
        }
        {
            var balanceList = await InscriptionContractStub.GetDistributorBalance.CallAsync(new StringValue
            {
                Value = "ELFS"
            });
            balanceList.Values.Count.ShouldBe(1);
            balanceList.Values[0].Distributor.ShouldBe(list.Values[0]);
            balanceList.Values[0].Balance.ShouldBe(9);
        }
    }
    
    [Fact]
    public async Task IssueTest_Success_TotalSupplyLessThanCount_10()
    {
        await InitializeTest_Success();
        await BuySeed();
        await InscriptionContractStub.DeployInscription.SendAsync(new DeployInscriptionInput
        {
            Tick = _tick,
            SeedSymbol = "SEED-1",
            Max = 10,
            Limit = 2,
            Image = _image
        });
        var result = await InscriptionContractStub.IssueInscription.SendAsync(new IssueInscriptionInput
        {
            Tick = "ELFS"
        });
        {
            var limit = await InscriptionContractStub.GetInscribedLimit.CallAsync(new StringValue
            {
                Value = "ELFS"
            });
            limit.Value.ShouldBe(2);
        }
        {
            var log = InscriptionIssued.Parser.ParseFrom(result.TransactionResult.Logs
                .FirstOrDefault(l => l.Name == nameof(InscriptionIssued))?.NonIndexed);
            log.Tick.ShouldBe("ELFS");
            log.To.ShouldBe(InscriptionContractAddress);
            var info =
                @"{ ""p"": ""aelf"", ""op"": ""deploy"", ""tick"": ""ELFS"", ""max"": ""10"", ""lim"": ""2"" }";
            log.InscriptionInfo.ShouldBe(info);
            log.Amt.ShouldBe(10);
        }
        var list = await InscriptionContractStub.GetDistributorList.CallAsync(new StringValue
        {
            Value = "ELFS"
        });
        list.Values.Count.ShouldBe(10);
        foreach (var distributor in list.Values)
        {
            var balanceOutput = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = distributor,
                Symbol = "ELFS-1"
            });
            balanceOutput.Balance.ShouldBe(list.Values.IndexOf(distributor) == 0 ? 1 : 1);
        }
        {
            var balanceList = await InscriptionContractStub.GetDistributorBalance.CallAsync(new StringValue
            {
                Value = "ELFS"
            });
            balanceList.Values.Count.ShouldBe(10);
            balanceList.Values[0].Distributor.ShouldBe(list.Values[0]);
            balanceList.Values[0].Balance.ShouldBe(1);
            for (var i = 1; i < balanceList.Values.Count; i++)
            {
                balanceList.Values[i].Distributor.ShouldBe(list.Values[i]);
                balanceList.Values[i].Balance.ShouldBe(1);
            }
        }
    }
    [Fact]
    public async Task IssueTest_Success_TotalSupplyLessThanCount_5()
    {
        await InitializeTest_Success();
        await BuySeed();
        await InscriptionContractStub.SetDistributorCount.SendAsync(new Int32Value
        {
            Value = 3
        });
        await InscriptionContractStub.DeployInscription.SendAsync(new DeployInscriptionInput
        {
            Tick = _tick,
            SeedSymbol = "SEED-1",
            Max = 2,
            Limit = 1,
            Image = _image
        });
        var result = await InscriptionContractStub.IssueInscription.SendAsync(new IssueInscriptionInput
        {
            Tick = "ELFS"
        });
        {
            var limit = await InscriptionContractStub.GetInscribedLimit.CallAsync(new StringValue
            {
                Value = "ELFS"
            });
            limit.Value.ShouldBe(1);
        }
        {
            var log = InscriptionIssued.Parser.ParseFrom(result.TransactionResult.Logs
                .FirstOrDefault(l => l.Name == nameof(InscriptionIssued))?.NonIndexed);
            log.Tick.ShouldBe("ELFS");
            log.To.ShouldBe(InscriptionContractAddress);
            var info =
                @"{ ""p"": ""aelf"", ""op"": ""deploy"", ""tick"": ""ELFS"", ""max"": ""2"", ""lim"": ""1"" }";
            log.InscriptionInfo.ShouldBe(info);
            log.Amt.ShouldBe(2);
        }
        var list = await InscriptionContractStub.GetDistributorList.CallAsync(new StringValue
        {
            Value = "ELFS"
        });
        list.Values.Count.ShouldBe(1);
        foreach (var distributor in list.Values)
        {
            var balanceOutput = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = distributor,
                Symbol = "ELFS-1"
            });
            balanceOutput.Balance.ShouldBe(list.Values.IndexOf(distributor) == 0 ? 2 : 0);
        }
        {
            var balanceList = await InscriptionContractStub.GetDistributorBalance.CallAsync(new StringValue
            {
                Value = "ELFS"
            });
            balanceList.Values.Count.ShouldBe(1);
            balanceList.Values[0].Distributor.ShouldBe(list.Values[0]);
            balanceList.Values[0].Balance.ShouldBe(2);
        }
    }

    [Fact]
    public async Task IssueTest_Failed_NotInitialized()
    {
        var result = await InscriptionContractStub.IssueInscription.SendWithExceptionAsync(new IssueInscriptionInput
        {
            Tick = "ELFS"
        });
        result.TransactionResult.Error.ShouldContain("Not initialized yet.");
    }
    [Fact]
    public async Task IssueTest_Failed_InvalidInput()
    { 
        await DeployInscriptionTest_Success();
        {
            var result = await InscriptionContractStub.IssueInscription.SendWithExceptionAsync(new IssueInscriptionInput
            {
                Tick = ""
            });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var result = await InscriptionContractStub.IssueInscription.SendWithExceptionAsync(new IssueInscriptionInput
            {
                
            });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var result = await InscriptionContractStub.IssueInscription.SendWithExceptionAsync(new IssueInscriptionInput
            {
                Tick = "LLL"
            });
            result.TransactionResult.Error.ShouldContain("Token not exist.");
        }
    }
    
    [Fact]
    public async Task InscribeTest_Success()
    {
        await IssueTest_Success();
        var result = await InscriptionContractStub.CheckDistributorBalance.CallAsync(new CheckDistributorBalanceInput
        {
            Sender = DefaultAddress,
            Amt = 1000,
            Tick = "ELFS"
        });
        result.Value.ShouldBe(true);
        var executionResult = await InscriptionContractStub.Inscribe.SendAsync(new InscribedInput
        {
            Tick = "ELFS",
            Amt = 1000
        });
        var log = InscriptionTransferred.Parser.ParseFrom(executionResult.TransactionResult.Logs
            .FirstOrDefault(l => l.Name == nameof(InscriptionTransferred))?.NonIndexed);
        log.Amt.ShouldBe(1000);
        var info = @"{ ""p"": ""aelf"", ""op"": ""mint"", ""tick"": ""ELFS"", ""amt"": ""1"" }";
        log.InscriptionInfo.ShouldBe(info);
        var userBalance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultAddress,
            Symbol = "ELFS-1"
        });
        userBalance.Balance.ShouldBe(1000);
        var list = await InscriptionContractStub.GetDistributorList.CallAsync(new StringValue()
        {
            Value = "ELFS"
        });
        var index = (int)(Math.Abs(DefaultAddress.ToByteArray().ToInt64(true)) % list.Values.Count);
        foreach (var distributor in list.Values)
        {
            var balanceOutput = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = distributor,
                Symbol = "ELFS-1"
            });
            balanceOutput.Balance.ShouldBeLessThanOrEqualTo(list.Values.IndexOf(distributor) == index ? 2100000-1000 : 2100000);
        }
        
        var balanceList = await InscriptionContractStub.GetDistributorBalance.CallAsync(new StringValue
        {
            Value = "ELFS"
        });
        foreach (var balance in balanceList.Values)
        {
            balance.Distributor.ShouldBe(list.Values[balanceList.Values.IndexOf(balance)]);
            balance.Balance.ShouldBe(balanceList.Values.IndexOf(balance) == index  ? 2100000-1000 : 2100000);
        }
    }
    
    [Fact]
    public async Task InscribeTest_Success_LessThanLimit()
    {
        await IssueTest_Success();
        var result = await InscriptionContractStub.CheckDistributorBalance.CallAsync(new CheckDistributorBalanceInput
        {
            Sender = DefaultAddress,
            Amt = 50,
            Tick = "ELFS"
        });
        result.Value.ShouldBe(true);
        var executionResult = await InscriptionContractStub.Inscribe.SendAsync(new InscribedInput
        {
            Tick = "ELFS",
            Amt = 50
        });
        var log = InscriptionTransferred.Parser.ParseFrom(executionResult.TransactionResult.Logs
            .FirstOrDefault(l => l.Name == nameof(InscriptionTransferred))?.NonIndexed);
        log.Amt.ShouldBe(50);
        var info = @"{ ""p"": ""aelf"", ""op"": ""mint"", ""tick"": ""ELFS"", ""amt"": ""1"" }";
        log.InscriptionInfo.ShouldBe(info);
        var userBalance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultAddress,
            Symbol = "ELFS-1"
        });
        userBalance.Balance.ShouldBe(50);
        var list = await InscriptionContractStub.GetDistributorList.CallAsync(new StringValue()
        {
            Value = "ELFS"
        });
        var index = (int)(Math.Abs(DefaultAddress.ToByteArray().ToInt64(true)) % list.Values.Count);
        foreach (var distributor in list.Values)
        {
            var balanceOutput = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = distributor,
                Symbol = "ELFS-1"
            });
            balanceOutput.Balance.ShouldBeLessThanOrEqualTo(list.Values.IndexOf(distributor) == index ? 2100000-50 : 2100000);
        }
        
        var balanceList = await InscriptionContractStub.GetDistributorBalance.CallAsync(new StringValue
        {
            Value = "ELFS"
        });
        foreach (var balance in balanceList.Values)
        {
            balance.Distributor.ShouldBe(list.Values[balanceList.Values.IndexOf(balance)]);
            balance.Balance.ShouldBe(balanceList.Values.IndexOf(balance) == index  ? 2100000-50 : 2100000);
        }
    }
    
    [Fact]
    public async Task InscribeTest_Failed_NotInitialized()
    {
        var executionResult = await InscriptionContractStub.Inscribe.SendWithExceptionAsync(new InscribedInput
        {
            Tick = "ELFS",
            Amt = 1000
        });
        executionResult.TransactionResult.Error.ShouldContain("Not initialized yet.");
    }
    
    [Fact]
    public async Task InscribeTest_Failed_ExceedLimit()
    {
        await IssueTest_Success();
        var executionResult = await InscriptionContractStub.Inscribe.SendWithExceptionAsync(new InscribedInput
        {
            Tick = "ELFS",
            Amt = 2100001
        });
        executionResult.TransactionResult.Error.ShouldContain("Exceed limit.");
    }
    
    [Fact]
    public async Task InscribeTest_Failed_NotEnough()
    {
        await CreateInscriptionHelper();
        for (var i = 0; i < 4; i++)
        {
            await InscriptionContractStub.Inscribe.SendAsync(new InscribedInput
            {
                Tick = "ELFS",
                Amt = 100
            });
        }
        var userBalance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultAddress,
            Symbol = "ELFS-1"
        });
        userBalance.Balance.ShouldBe(400);
        var list = await InscriptionContractStub.GetDistributorList.CallAsync(new StringValue()
        {
            Value = "ELFS"
        });
        var index = (int)(Math.Abs(DefaultAddress.ToByteArray().ToInt64(true)) % list.Values.Count);
        foreach (var distributor in list.Values)
        {
            var balanceOutput = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = distributor,
                Symbol = "ELFS-1"
            });
            balanceOutput.Balance.ShouldBe(list.Values.IndexOf(distributor) == index ? 20 : 420);
        }
        
        var balanceList = await InscriptionContractStub.GetDistributorBalance.CallAsync(new StringValue
        {
            Value = "ELFS"
        });
        foreach (var balance in balanceList.Values)
        {
            balance.Distributor.ShouldBe(list.Values[balanceList.Values.IndexOf(balance)]);
            balance.Balance.ShouldBe(balanceList.Values.IndexOf(balance) == index  ? 20 : 420);
        }
        var executionResult = await InscriptionContractStub.Inscribe.SendWithExceptionAsync(new InscribedInput
        {
            Tick = "ELFS",
            Amt = 50
        });
        executionResult.TransactionResult.Error.ShouldContain("Distributor balance not enough.");
    }
    
    [Fact]
    public async Task InscribeTest_Failed_InvalidInput()
    {
        await IssueTest_Success();
        {
            var result = await InscriptionContractStub.Inscribe.SendWithExceptionAsync(new InscribedInput
            {
                Tick = "",
                Amt = 1000
            });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var result = await InscriptionContractStub.Inscribe.SendWithExceptionAsync(new InscribedInput
            {
                Amt = 1000
            });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var result = await InscriptionContractStub.Inscribe.SendWithExceptionAsync(new InscribedInput
            {
                Tick = "ELFS",
                Amt = 0
            });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var result = await InscriptionContractStub.Inscribe.SendWithExceptionAsync(new InscribedInput
            {
                Tick = "ELFS",
                Amt = -1
            });
            result.TransactionResult.Error.ShouldContain("Invalid input.");
        }
    }
    
       
    [Fact]
    public async Task MintInscriptionTest_Success()
    {
        await CreateInscriptionHelper();
        var list = await InscriptionContractStub.GetDistributorList.CallAsync(new StringValue()
        {
            Value = "ELFS"
        });
        var index = (int)(Math.Abs(DefaultAddress.ToByteArray().ToInt64(true)) % list.Values.Count);
        for (var i = 0; i < 4; i++)
        {
            await InscriptionContractStub.Inscribe.SendAsync(new InscribedInput
            {
                Tick = "ELFS",
                Amt = 100
            });
        }
        var result = await InscriptionContractStub.CheckDistributorBalance.CallAsync(new CheckDistributorBalanceInput
        {
            Sender = DefaultAddress,
            Amt = 100,
            Tick = "ELFS"
        });
        result.Value.ShouldBe(false);
        var executionResult = await InscriptionContractStub.MintInscription.SendAsync(new InscribedInput
        {
            Tick = "ELFS",
            Amt = 100
        });
        {
            var log = InscriptionTransferred.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .FirstOrDefault(l => l.Name == nameof(InscriptionTransferred))?.NonIndexed);
            log.Amt.ShouldBe(100);
            var info = @"{ ""p"": ""aelf"", ""op"": ""mint"", ""tick"": ""ELFS"", ""amt"": ""1"" }";
            log.InscriptionInfo.ShouldBe(info);
        }
        {
            var log = Transferred.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .FirstOrDefault(l => l.Name == nameof(Transferred))?.NonIndexed);
            var from = Transferred.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .FirstOrDefault(l => l.Name == nameof(Transferred))?.Indexed[0]).From;
            var to = Transferred.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .FirstOrDefault(l => l.Name == nameof(Transferred))?.Indexed[1]).To;
            var symbol = Transferred.Parser.ParseFrom(executionResult.TransactionResult.Logs
                .FirstOrDefault(l => l.Name == nameof(Transferred))?.Indexed[2]).Symbol;
            from.ShouldBe(list.Values[index]);
            to.ShouldBe(DefaultAddress);
            log.Amount.ShouldBe(20);
            symbol.ShouldBe("ELFS-1");
        }
        {
            var log = Transferred.Parser.ParseFrom(executionResult.TransactionResult.Logs[2].NonIndexed);
            var from = Transferred.Parser.ParseFrom(executionResult.TransactionResult.Logs[2]?.Indexed[0]).From;
            var to = Transferred.Parser.ParseFrom(executionResult.TransactionResult.Logs
                [2]?.Indexed[1]).To;
            var symbol = Transferred.Parser.ParseFrom(executionResult.TransactionResult.Logs
                [2]?.Indexed[2]).Symbol;
            from.ShouldBe(list.Values[index+1]);
            to.ShouldBe(DefaultAddress);
            log.Amount.ShouldBe(80);
            symbol.ShouldBe("ELFS-1");
        }
    
        var userBalance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultAddress,
            Symbol = "ELFS-1"
        });
        userBalance.Balance.ShouldBe(500);
        foreach (var distributor in list.Values)
        {
            var balanceOutput = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = distributor,
                Symbol = "ELFS-1"
            });
            if (list.Values.IndexOf(distributor) == index)
            {
                balanceOutput.Balance.ShouldBe(0);
            }
            else if (list.Values.IndexOf(distributor) == index + 1)
            {
                balanceOutput.Balance.ShouldBe(420 - 80);
            }
            else
            {
                balanceOutput.Balance.ShouldBe(420);
            }
        }
        
        var balanceList = await InscriptionContractStub.GetDistributorBalance.CallAsync(new StringValue
        {
            Value = "ELFS"
        });
        foreach (var balance in balanceList.Values)
        {
            balance.Distributor.ShouldBe(list.Values[balanceList.Values.IndexOf(balance)]);
            if (balanceList.Values.IndexOf(balance) == index)
            {
                balance.Balance.ShouldBe(0);
            }
            else if (balanceList.Values.IndexOf(balance) == index + 1)
            {
                balance.Balance.ShouldBe(420 - 80);
            }
            else
            {
                balance.Balance.ShouldBe(420);
            }
        }
        for (var i = 0; i < 3; i++)
        {
            await InscriptionContractStub.MintInscription.SendAsync(new InscribedInput
            {
                Tick = "ELFS",
                Amt = 100
            });
        }
        var balanceList2 = await InscriptionContractStub.GetDistributorBalance.CallAsync(new StringValue
        {
            Value = "ELFS"
        });
        foreach (var balance in balanceList2.Values)
        {
            balance.Distributor.ShouldBe(list.Values[balanceList2.Values.IndexOf(balance)]);
            if (balanceList2.Values.IndexOf(balance) == index)
            {
                balance.Balance.ShouldBe(0);
            }
            else if (balanceList2.Values.IndexOf(balance) == index + 1)
            {
                balance.Balance.ShouldBe(420 - 80 - 300);
            }
            else
            {
                balance.Balance.ShouldBe(420);
            }
        }
        var transactionResult = await InscriptionContractStub.MintInscription.SendAsync(new InscribedInput
        {
            Tick = "ELFS",
            Amt = 100
        });
        {
            var log = InscriptionTransferred.Parser.ParseFrom(transactionResult.TransactionResult.Logs
                .FirstOrDefault(l => l.Name == nameof(InscriptionTransferred))?.NonIndexed);
            var tick = InscriptionTransferred.Parser.ParseFrom(transactionResult.TransactionResult.Logs
                .FirstOrDefault(l => l.Name == nameof(InscriptionTransferred))?.Indexed[2]).Tick;
            tick.ShouldBe("ELFS");
            log.Amt.ShouldBe(100);
            var info = @"{ ""p"": ""aelf"", ""op"": ""mint"", ""tick"": ""ELFS"", ""amt"": ""1"" }";
            log.InscriptionInfo.ShouldBe(info);
        }
        {
            var log = Transferred.Parser.ParseFrom(transactionResult.TransactionResult.Logs
                .FirstOrDefault(l => l.Name == nameof(Transferred))?.NonIndexed);
            var from = Transferred.Parser.ParseFrom(transactionResult.TransactionResult.Logs
                .FirstOrDefault(l => l.Name == nameof(Transferred))?.Indexed[0]).From;
            var to = Transferred.Parser.ParseFrom(transactionResult.TransactionResult.Logs
                .FirstOrDefault(l => l.Name == nameof(Transferred))?.Indexed[1]).To;
            var symbol = Transferred.Parser.ParseFrom(transactionResult.TransactionResult.Logs
                .FirstOrDefault(l => l.Name == nameof(Transferred))?.Indexed[2]).Symbol;
            from.ShouldBe(list.Values[index+1]);
            to.ShouldBe(DefaultAddress);
            log.Amount.ShouldBe(40);
            symbol.ShouldBe("ELFS-1");
        }
        {
            var log = Transferred.Parser.ParseFrom(transactionResult.TransactionResult.Logs[2]?.NonIndexed);
            var from = Transferred.Parser.ParseFrom(transactionResult.TransactionResult.Logs[2]?.Indexed[0]).From;
            var to = Transferred.Parser.ParseFrom(transactionResult.TransactionResult.Logs[2]?.Indexed[1]).To;
            var symbol = Transferred.Parser.ParseFrom(transactionResult.TransactionResult.Logs[2]?.Indexed[2]).Symbol;
            from.ShouldBe(list.Values[index+2]);
            to.ShouldBe(DefaultAddress);
            log.Amount.ShouldBe(60);
            symbol.ShouldBe("ELFS-1");
        }
        var userBalance1 = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultAddress,
            Symbol = "ELFS-1"
        });
        userBalance1.Balance.ShouldBe(900);
        foreach (var distributor in list.Values)
        {
            var balanceOutput = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = distributor,
                Symbol = "ELFS-1"
            });
            if (list.Values.IndexOf(distributor) == index)
            {
                balanceOutput.Balance.ShouldBe(0);
            }
            else if (list.Values.IndexOf(distributor) == index + 1)
            {
                balanceOutput.Balance.ShouldBe(0);
            }
            else if (list.Values.IndexOf(distributor) == index + 2)
            {
                balanceOutput.Balance.ShouldBe(360);
            }
            else
            {
                balanceOutput.Balance.ShouldBe(420);
            }
        }
        
        var balanceList1 = await InscriptionContractStub.GetDistributorBalance.CallAsync(new StringValue
        {
            Value = "ELFS"
        });
        foreach (var balance in balanceList1.Values)
        {
            balance.Distributor.ShouldBe(list.Values[balanceList1.Values.IndexOf(balance)]);
            if (balanceList1.Values.IndexOf(balance) == index)
            {
                balance.Balance.ShouldBe(0);
            }
            else if (balanceList1.Values.IndexOf(balance) == index + 1)
            {
                balance.Balance.ShouldBe(0);
            }
            else if (balanceList1.Values.IndexOf(balance) == index + 2)
            {
                balance.Balance.ShouldBe(360);
            }
            else
            {
                balance.Balance.ShouldBe(420);
            }
        }
    }
    
    [Fact]
    public async Task MintInscriptionTest_Success_OtherAccount()
    {
        await CreateInscriptionHelper();
        var list = await InscriptionContractStub.GetDistributorList.CallAsync(new StringValue()
        {
            Value = "ELFS"
        });
        var list1 = await InscriptionContractStub.GetDistributorList.CallAsync(new StringValue()
        {
            Value = "TTT"
        });
        list1.Values.Count.ShouldBe(0);
        var index = (int)(Math.Abs(DefaultAddress.ToByteArray().ToInt64(true)) % list.Values.Count);
        var executionResult = await InscriptionContractStub.MintInscription.SendAsync(new InscribedInput
        {
            Tick = "ELFS",
            Amt = 100
        });
        var index1 = (int)(Math.Abs(User2Address.ToByteArray().ToInt64(true)) % list.Values.Count);
        await TokenContractStub.Transfer.SendAsync(new TransferInput
        {
            To = User2Address,
            Symbol = "ELF",
            Amount = 5,
        });        
        var executionResult1 = await InscriptionContractAccount1Stub.MintInscription.SendAsync(new InscribedInput
        {
            Tick = "ELFS",
            Amt = 80
        });

        var userBalance = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = DefaultAddress,
            Symbol = "ELFS-1"
        });
        userBalance.Balance.ShouldBe(100);
        var userBalance1 = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
        {
            Owner = User2Address,
            Symbol = "ELFS-1"
        });
        userBalance1.Balance.ShouldBe(80);
        foreach (var distributor in list.Values)
        {
            var balanceOutput = await TokenContractStub.GetBalance.CallAsync(new GetBalanceInput
            {
                Owner = distributor,
                Symbol = "ELFS-1"
            });
            if (list.Values.IndexOf(distributor) == index)
            {
                balanceOutput.Balance.ShouldBe(420 - 100);
            }
            else if (list.Values.IndexOf(distributor) == index1)
            {
                balanceOutput.Balance.ShouldBe(420 - 80);
            }
            else
            {
                balanceOutput.Balance.ShouldBe(420);
            }
        }
        
        var balanceList = await InscriptionContractStub.GetDistributorBalance.CallAsync(new StringValue
        {
            Value = "ELFS"
        });
        foreach (var balance in balanceList.Values)
        {
            balance.Distributor.ShouldBe(list.Values[balanceList.Values.IndexOf(balance)]);
            if (balanceList.Values.IndexOf(balance) == index)
            {
                balance.Balance.ShouldBe(420 - 100);
            }
            else if (balanceList.Values.IndexOf(balance) == index1)
            {
                balance.Balance.ShouldBe(420 - 80);
            }
            else
            {
                balance.Balance.ShouldBe(420);
            }
        }
    }

    [Fact]
    public async Task MintInscriptionTest_Failed_NotInitialized()
    {
        var executionResult = await InscriptionContractStub.MintInscription.SendWithExceptionAsync(new InscribedInput
        {
            Tick = "ELFS",
            Amt = 100
        });
        executionResult.TransactionResult.Error.ShouldContain("Not initialized yet.");
    }
    
    [Fact]
    public async Task MintInscriptionTest_Failed_InvalidInput()
    {
        await CreateInscriptionHelper();
        {
            var executionResult = await InscriptionContractStub.MintInscription.SendWithExceptionAsync(new InscribedInput
            {
                Tick = "",
                Amt = 100
            });
            executionResult.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var executionResult = await InscriptionContractStub.MintInscription.SendWithExceptionAsync(new InscribedInput
            {
                Amt = 100
            });
            executionResult.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var executionResult = await InscriptionContractStub.MintInscription.SendWithExceptionAsync(new InscribedInput
            {
                Tick = "ELFS",
                Amt = -1
            });
            executionResult.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var executionResult = await InscriptionContractStub.MintInscription.SendWithExceptionAsync(new InscribedInput
            {
                Tick = "ELFS",
                Amt = 0
            });
            executionResult.TransactionResult.Error.ShouldContain("Invalid input.");
        }
        {
            var executionResult = await InscriptionContractStub.MintInscription.SendWithExceptionAsync(new InscribedInput
            {
                Tick = "ELFS",
                Amt = 1000
            });
            executionResult.TransactionResult.Error.ShouldContain("Exceed limit.");
        }
    }

    [Fact]
    public async Task MintInscriptionTest_Failed_NotEnough()
    {
        await CreateInscriptionHelper();
        for (var i = 0; i < 20; i++)
        {
            await InscriptionContractStub.MintInscription.SendAsync(new InscribedInput
            {
                Tick = "ELFS",
                Amt = 100
            });
        }
        await InscriptionContractStub.MintInscription.SendAsync(new InscribedInput
        {
            Tick = "ELFS",
            Amt = 80
        });
        var result = await InscriptionContractAccount1Stub.MintInscription.SendWithExceptionAsync(new InscribedInput
        {
            Tick = "ELFS",
            Amt = 40
        });
        result.TransactionResult.Error.ShouldContain("Not enough ELF balance.");
        await TokenContractStub.Transfer.SendAsync(new TransferInput
        {
            To = User2Address,
            Symbol = "ELF",
            Amount = 5,
        });
        result = await InscriptionContractAccount1Stub.MintInscription.SendWithExceptionAsync(new InscribedInput
        {
            Tick = "ELFS",
            Amt = 40
        });
        result.TransactionResult.Error.ShouldContain(" Not enough inscription amount to mint.");
    }

    [Fact]
    public async Task SetDistributorCount_Failed_NoPermission()
    {
        var result = await InscriptionContractAccount1Stub.SetDistributorCount.SendWithExceptionAsync(new Int32Value
        {
           Value = 5
        });
        result.TransactionResult.Error.ShouldContain("No permission");
    }
    
    [Fact]
    public async Task SetDistributorCount_Failed_InvalidInput()
    {
        await InitializeTest_Success();
        var result = await InscriptionContractStub.SetDistributorCount.SendWithExceptionAsync(new Int32Value
        {
        });
        result.TransactionResult.Error.ShouldContain("Invalid input");
        
        var result1 = await InscriptionContractStub.SetDistributorCount.SendWithExceptionAsync(new Int32Value
        {
            Value = 0
        });
        result1.TransactionResult.Error.ShouldContain("Invalid input");
        
        var result2 = await InscriptionContractStub.SetDistributorCount.SendWithExceptionAsync(new Int32Value
        {
            Value = -1
        });
        result2.TransactionResult.Error.ShouldContain("Invalid input");
    }
    
    [Fact]
    public async Task SetIssueChainId_Success()
    {
        await InitializeTest_Success();
        await InscriptionContractStub.SetIssueChainId.SendAsync(new Int32Value
        {
            Value = _sideChainId
        });
        var chainId = await InscriptionContractStub.GetIssueChainId.CallAsync(new Empty());
        chainId.Value.ShouldBe(_sideChainId);
    }
    
        
    [Fact]
    public async Task SetIssueChainId_Failed_NoPermission()
    {
        await InitializeTest_Success();
        var result = await InscriptionContractAccount1Stub.SetIssueChainId.SendWithExceptionAsync(new Int32Value
        {
            Value = _sideChainId
        });
        result.TransactionResult.Error.ShouldContain("No permission.");
    }
    
    [Fact]
    public async Task SetIssueChainId_Failed_InvalidInput()
    {
        await InitializeTest_Success();
        var result = await InscriptionContractStub.SetIssueChainId.SendWithExceptionAsync(new Int32Value
        {
            
        });
        result.TransactionResult.Error.ShouldContain("Invalid input");
        
        var result1 = await InscriptionContractStub.SetIssueChainId.SendWithExceptionAsync(new Int32Value
        {
            Value = 0
        });
        result1.TransactionResult.Error.ShouldContain("Invalid input");
        
        var result2 = await InscriptionContractStub.SetIssueChainId.SendWithExceptionAsync(new Int32Value
        {
            Value = -1
        });
        result2.TransactionResult.Error.ShouldContain("Invalid input");
    }

    [Fact]
    public async Task SetMinimumELFBalance_Success()
    {
        await InitializeTest_Success();
        await InscriptionContractStub.SetMinimumELFBalance.SendAsync(new Int32Value
        {
            Value = 6
        });
        {
            var size = await InscriptionContractStub.GetMinimumELFBalance.CallAsync(new Empty());
            size.Value.ShouldBe(6);
        }
    }
    
    [Fact]
    public async Task SetMinimumELFBalance_Failed()
    {
        await InitializeTest_Success();
        var result = await InscriptionContractAccount1Stub.SetMinimumELFBalance.SendWithExceptionAsync(new Int32Value
        {
            Value = 6
        });
        result.TransactionResult.Error.ShouldContain("No permission");
        {
            var size = await InscriptionContractStub.GetMinimumELFBalance.CallAsync(new Empty());
            size.Value.ShouldBe(5);
        }
    }

    [Fact]
    public async Task SetMinimumELFBalance_InvalidInput()
    {
        await InitializeTest_Success();
        {
            var result = await InscriptionContractStub.SetMinimumELFBalance.SendWithExceptionAsync(new Int32Value
            {

            });
            result.TransactionResult.Error.ShouldContain("Invalid input");
        }
        {
            var result = await InscriptionContractStub.SetMinimumELFBalance.SendWithExceptionAsync(new Int32Value
            {
                Value = 0
            });
            result.TransactionResult.Error.ShouldContain("Invalid input");
        }
        {
            var result = await InscriptionContractStub.SetMinimumELFBalance.SendWithExceptionAsync(new Int32Value
            {
                Value = -1
            });
            result.TransactionResult.Error.ShouldContain("Invalid input");
        }
    }
    
    [Fact]
    public async Task SetImageSizeLimit_Success()
    {
        await InitializeTest_Success();
        var result = await InscriptionContractStub.SetImageSizeLimit.SendAsync(new Int32Value
        {
            Value = 5 * 1024
        });
        {
            var size = await InscriptionContractStub.GetImageSizeLimit.CallAsync(new Empty());
            size.Value.ShouldBe(5 * 1024);
        }
    }
    
    [Fact]
    public async Task SetImageSizeLimit_Failed()
    {
        await InitializeTest_Success();
        var result = await InscriptionContractAccount1Stub.SetImageSizeLimit.SendWithExceptionAsync(new Int32Value
        {
            Value = 5 * 1024
        });
        result.TransactionResult.Error.ShouldContain("No permission");
        {
            var size = await InscriptionContractStub.GetImageSizeLimit.CallAsync(new Empty());
            size.Value.ShouldBe(10 * 1024);
        }
    }

    [Fact]
    public async Task SetImageSizeLimit_Failed_InvalidInput()
    {
        await InitializeTest_Success();
        {
            var result = await InscriptionContractStub.SetImageSizeLimit.SendWithExceptionAsync(new Int32Value
            {

            });
            result.TransactionResult.Error.ShouldContain("Invalid input");
        }
        {
            var result = await InscriptionContractStub.SetImageSizeLimit.SendWithExceptionAsync(new Int32Value
            {
                Value = 0
            });
            result.TransactionResult.Error.ShouldContain("Invalid input");
        }
        {
            var result = await InscriptionContractStub.SetImageSizeLimit.SendWithExceptionAsync(new Int32Value
            {
                Value = -1
            });
            result.TransactionResult.Error.ShouldContain("Invalid input");
        }
    }



    private async Task CreateInscriptionHelper()
    {
        await InitializeTest_Success();
        await BuySeed();
        await InscriptionContractStub.SetDistributorCount.SendAsync(new Int32Value
        {
            Value = 5
        });
        await InscriptionContractStub.DeployInscription.SendAsync(new DeployInscriptionInput
        {
            Tick = _tick,
            SeedSymbol = "SEED-1",
            Max = 2100,
            Limit = 100,
            Image = _image
        });
        await InscriptionContractStub.IssueInscription.SendAsync(new IssueInscriptionInput
        {
            Tick = "ELFS"
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
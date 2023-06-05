using System.Collections.Generic;
using System.IO;
using AElf.ContractTestBase;
using AElf.ContractTestBase.ContractTestKit;
using AElf.Kernel.SmartContract.Application;
using AElf.Testing.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;
using ContractCodeProvider = AElf.ContractTestBase.ContractCodeProvider;

namespace Forest.Whitelist;

[DependsOn(typeof(MainChainDAppContractTestModule))]
public class WhitelistContractTestModule : MainChainDAppContractTestModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<IBlockTimeProvider, BlockTimeProvider>();
        context.Services
            .AddSingleton<IContractDeploymentListProvider, MainChainDAppContractTestDeploymentListProvider>();
    }

    public override void OnPreApplicationInitialization(ApplicationInitializationContext context)
    {
        var contractCodeProvider = context.ServiceProvider.GetService<IContractCodeProvider>() ??
                                   new ContractCodeProvider();
        var contractCodes = new Dictionary<string, byte[]>(contractCodeProvider.Codes)
        {
            {
                new WhitelistContractInitializationProvider().ContractCodeName,
                File.ReadAllBytes(typeof(WhitelistContract).Assembly.Location)
            }
        };
        contractCodeProvider.Codes = contractCodes;
    }
}
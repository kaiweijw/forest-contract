using System.Collections.Generic;
using System.IO;
using AElf.Boilerplate.TestBase;
using AElf.ContractTestBase;
using AElf.Kernel.SmartContract.Application;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;

namespace Forest.Contracts.Inscription;

[DependsOn(typeof(MainChainDAppContractTestModule))]
public class InscriptionContractTestModule : MainChainDAppContractTestModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
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
                new InscriptionContractInitializationProvider().ContractCodeName,
                File.ReadAllBytes(typeof(InscriptionContract).Assembly.Location)
            }
        };
        contractCodeProvider.Codes = contractCodes;
    }
}
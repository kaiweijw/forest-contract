using AElf.Boilerplate.TestBase;
using AElf.Kernel.SmartContract;
using AElf.Kernel.SmartContract.Application;
using Microsoft.Extensions.DependencyInjection;
using Forest.Contracts.Auction.ContractInitializationProvider;
using Volo.Abp.Modularity;

namespace Forest.Contracts.Auction
{
    [DependsOn(typeof(MainChainDAppContractTestModule))]
    public class AuctionContractTestModule : MainChainDAppContractTestModule
    {
        public override void ConfigureServices(ServiceConfigurationContext context)
        {
            context.Services.AddSingleton<IContractInitializationProvider, AuctionContractInitializationProvider>();
            Configure<ContractOptions>(o => o.ContractDeploymentAuthorityRequired = false);
        }
    }
}
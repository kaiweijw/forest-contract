using System.Collections.Generic;
using AElf.Boilerplate.TestBase.SmartContractNameProviders;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;
using Volo.Abp.DependencyInjection;

namespace Forest;

public class NFTContractInitializationProvider : IContractInitializationProvider, ISingletonDependency
{
    public List<ContractInitializationMethodCall> GetInitializeMethodList(byte[] contractCode)
    {
        return new List<ContractInitializationMethodCall>();
    }

    public Hash SystemSmartContractName => NFTSmartContractAddressNameProvider.Name;
    public string ContractCodeName => "AElf.Contracts.NFT";
}
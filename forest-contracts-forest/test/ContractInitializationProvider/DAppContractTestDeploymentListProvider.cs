using System.Collections.Generic;
using AElf.ContractTestBase;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;

namespace Forest;

public class MainChainDAppContractTestDeploymentListProvider : MainChainContractDeploymentListProvider,
    IContractDeploymentListProvider
{
    public new List<Hash> GetDeployContractNameList()
    {
        var list = base.GetDeployContractNameList();
        list.Add(ForestSmartContractAddressNameProvider.Name);
        list.Add(NFTSmartContractAddressNameProvider.Name);
        list.Add(WhitelistSmartContractAddressNameProvider.Name);
        return list;
    }
    
}

public class SideChainDAppContractTestDeploymentListProvider : SideChainContractDeploymentListProvider, IContractDeploymentListProvider
{
    public List<Hash> GetDeployContractNameList()
    {
        var list = base.GetDeployContractNameList();
        //list.Add(DAppSmartContractAddressNameProvider.Name);
        list.Add(ForestSmartContractAddressNameProvider.Name);
        list.Add(WhitelistSmartContractAddressNameProvider.Name);
        list.Add(NFTSmartContractAddressNameProvider.Name);
        return list;
    }
}
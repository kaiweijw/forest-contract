using System.Collections.Generic;
using AElf.Boilerplate.TestBase.SmartContractNameProviders;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;

namespace Forest.Contracts.Drop.ContractInitializationProvider
{
    public class DropContractInitializationProvider : IContractInitializationProvider
    {
        public List<ContractInitializationMethodCall> GetInitializeMethodList(byte[] contractCode)
        {
            return new List<ContractInitializationMethodCall>();
        }

        public Hash SystemSmartContractName { get; } = AuctionContractAddressNameProvider.Name;
        public string ContractCodeName { get; } = "AuctionContract";
    }
}
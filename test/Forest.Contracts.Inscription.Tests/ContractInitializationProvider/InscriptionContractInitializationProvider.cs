using System.Collections.Generic;
using AElf.Boilerplate.TestBase.SmartContractNameProviders;
using AElf.Kernel.SmartContract.Application;
using AElf.Types;

namespace Forest.Contracts.Inscription.ContractInitializationProvider
{
    public class InscriptionContractInitializationProvider : IContractInitializationProvider
    {
        public List<ContractInitializationMethodCall> GetInitializeMethodList(byte[] contractCode)
        {
            return new List<ContractInitializationMethodCall>();
        }

        public Hash SystemSmartContractName { get; } = InscriptionSmartContractAddressNameProvider.Name;
        public string ContractCodeName { get; } = "InscriptionContract";
    }
}
using AElf.Kernel.Infrastructure;
using AElf.Types;

namespace AElf.Boilerplate.TestBase.SmartContractNameProviders;

public class DropContractAddressNameProvider
{
    public static readonly Hash Name = HashHelper.ComputeFrom("Forest.Contracts.Drop");

    public static readonly string StringName = Name.ToStorageKey();
    public Hash ContractName => Name;
    public string ContractStringName => StringName;
}
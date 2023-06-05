using AElf;
using AElf.Kernel.Infrastructure;
using AElf.Types;

namespace Forest;

public class ForestSmartContractAddressNameProvider
{
    public static readonly Hash Name = HashHelper.ComputeFrom("AElf.ContractNames.Forest");

    public static readonly string StringName = Name.ToStorageKey();
    public Hash ContractName => Name;
    public string ContractStringName => StringName;
}
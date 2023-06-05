using AElf;
using AElf.Kernel.Infrastructure;
using AElf.Types;

namespace Forest;

public class NFTSmartContractAddressNameProvider
{
    public static readonly Hash Name = HashHelper.ComputeFrom("AElf.ContractNames.NFTContract");

    public static readonly string StringName = Name.ToStorageKey();
    public Hash ContractName => Name;
    public string ContractStringName => StringName;
}
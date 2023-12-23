using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace Forest.Inscription;

public partial class InscriptionContractState : ContractState
{
    public SingletonState<bool> Initialized { get; set; }

    /// <summary>
    /// Inscription tick -> per transfer limit
    /// </summary>
    public MappedState<string, long> InscribedLimit { get; set; }

    /// <summary>
    /// Inscription tick -> distributor hash list
    /// </summary>
    public MappedState<string, HashList> DistributorHashList { get; set; }

    /// <summary>
    /// Inscription tick -> distributor hash -> balance
    /// </summary>
    public MappedState<string, Hash, long> DistributorBalance { get; set; }
}
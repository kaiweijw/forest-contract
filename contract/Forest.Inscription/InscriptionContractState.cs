using AElf.Sdk.CSharp.State;

namespace Forest.Inscription;

public partial class InscriptionContractState : ContractState
{
    public SingletonState<bool> Initialized { get; set; }

    /// <summary>
    /// Inscription tick -> per transfer limit
    /// </summary>
    public MappedState<string, long> InscribedLimit { get; set; }

    /// <summary>
    /// Inscription tick -> virtual hash list
    /// </summary>
    public MappedState<string, HashList> DistributorHashList { get; set; }
}
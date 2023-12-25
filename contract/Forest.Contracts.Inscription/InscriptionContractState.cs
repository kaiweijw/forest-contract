using AElf.Sdk.CSharp.State;
using AElf.Types;

namespace Forest.Contracts.Inscription;

public partial class InscriptionContractState : ContractState
{
    public SingletonState<bool> Initialized { get; set; }

    public SingletonState<Address> Admin { get; set; }

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

    public SingletonState<int> DistributorCount { get; set; }

    public SingletonState<int> IssueChainId { get; set; }
}
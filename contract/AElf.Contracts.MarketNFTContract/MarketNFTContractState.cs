using AElf.Sdk.CSharp.State;
using AElf.Contracts.MultiToken;

namespace AElf.Contracts.MarketNFTContract;

/// <summary>
///     The state class of the contract, it inherits from the AElf.Sdk.CSharp.State.ContractState type.
/// </summary>
public partial class MarketNFTContractState : ContractState
{
    // state definitions go here.
    internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
}
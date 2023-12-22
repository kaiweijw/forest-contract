using AElf.Contracts.Configuration;
using AElf.Contracts.MultiToken;
using AElf.Standards.ACS0;

namespace Forest.Inscription;

public partial class InscriptionContractState
{
    internal TokenContractImplContainer.TokenContractImplReferenceState TokenContract { get; set; }
    internal ACS0Container.ACS0ReferenceState GenesisContract { get; set; }
    internal ConfigurationContainer.ConfigurationReferenceState ConfigurationContract { get; set; }

}
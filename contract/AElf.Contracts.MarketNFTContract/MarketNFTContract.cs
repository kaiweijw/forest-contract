using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
// using AElf.Standards.ACS3;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Contracts.MarketNFTContract;

/// <summary>
///     The C# implementation of the contract defined in marketNFT_contract.proto that is located in the "protobuf"
///     folder.
///     Notice that it inherits from the protobuf generated code.
/// </summary>
public partial class MarketNFTContract : MarketNFTContractContainer.MarketNFTContractBase
{
    /// <summary>
    ///     The implementation of the Create method. It takes no parameters and returns on of the custom data types
    ///     defined in the protobuf definition file.
    /// </summary>
    /// <param name="input">MultiToken.CreateInput message (from Protobuf)</param>
    /// <returns>a Empty</returns>
    public override Empty Create(CreateInput input)
    {
        State.TokenContract.Create.Send(new MultiToken.CreateInput()
        {
            Symbol = input.Symbol,
            TokenName = input.TokenName,
            TotalSupply = input.TotalSupply,
            Decimals = input.Decimals,
            Issuer = input.Issuer,
            IsBurnable = input.IsBurnable,
            // LockWhiteList = { input.LockWhiteList },
            IssueChainId = input.IssueChainId
        });
    
        State.TokenContract.Issue.Send(new MultiToken.IssueInput()
        {
            To = input.To,
            Symbol = input.Symbol,
            Amount = input.Amount,
            Memo = input.Memo,
        });
        return new Empty();
    }
}
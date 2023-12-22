using System.Collections.Generic;
using System.Linq;
using System.Text;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Forest.Inscription;

public partial class InscriptionContract : InscriptionContractContainer.InscriptionContractBase
{
    public override Empty Initialize(InitializeInput input)
    {
        Assert(!State.Initialized.Value, "Already initialized.");
        State.GenesisContract.Value = Context.GetZeroSmartContractAddress();
        // Assert(State.GenesisContract.GetContractInfo.Call(Context.Self).Deployer == Context.Sender, "No permission.");
        State.TokenContract.Value =
            Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
        State.ConfigurationContract.Value =
            Context.GetContractAddressByName(SmartContractConstants.ConfigurationContractSystemName);
        State.Initialized.Value = true;
        return new Empty();
    }

    public override Empty DeployInscription(DeployInscriptionInput input)
    {
        Assert(!string.IsNullOrWhiteSpace(input.SeedSymbol) && !string.IsNullOrWhiteSpace(input.Tick) &&
               input.Max > 0 && input.Limit > 0, "Invalid input.");
        Assert(!string.IsNullOrWhiteSpace(input.Image) && Encoding.UTF8.GetByteCount(input.Image) <=
            InscriptionContractConstants.ImageMaxLength, "Invalid image data.");
        // Approve Seed.
        State.TokenContract.TransferFrom.Send(new TransferFromInput
        {
            Symbol = input.SeedSymbol,
            From = Context.Sender,
            To = Context.Self,
            Amount = 1,
        });

        // Create collection
        var collectionExternalInfo =
            GenerateExternalInfo(input.Tick, input.Max, input.Limit, input.Image, SymbolType.NftCollection);
        var collectionSymbol = CreateInscription(input.Tick, input.Max, input.IssueChainId, collectionExternalInfo,
            SymbolType.NftCollection);

        // Create nft item
        var nftExternalInfo =
            GenerateExternalInfo(input.Tick, input.Max, input.Limit, input.Image, SymbolType.Nft);
        var nftSymbol = CreateInscription(input.Tick, input.Max, input.IssueChainId, nftExternalInfo, SymbolType.Nft);
        State.InscribedLimit[input.Tick?.ToUpper()] = input.Limit;

        Context.Fire(new InscriptionCreated
        {
            CollectionSymbol = collectionSymbol,
            ItemSymbol = nftSymbol,
            Tick = input.Tick,
            TotalSupply = input.Max,
            Decimals = InscriptionContractConstants.InscriptionDecimals,
            Issuer = Context.Self,
            IsBurnable = true,
            IssueChainId = input.IssueChainId,
            CollectionExternalInfo = new ExternalInfos
            {
                Value = { collectionExternalInfo.Value }
            },
            ItemExternalInfo = new ExternalInfos
            {
                Value = { nftExternalInfo.Value }
            },
            Owner = Context.Self,
            Deployer = Context.Sender,
            Limit = input.Limit
        });

        return new Empty();
    }

    public override Empty IssueInscription(IssueInscriptionInput input)
    {
        Assert(!string.IsNullOrWhiteSpace(input.Tick), "Invalid input.");
        var tick = input.Tick?.ToUpper();
        var symbol = $"{tick}-{InscriptionContractConstants.NftSymbolSuffix}";
        var tokenInfo = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
        {
            Symbol = symbol
        });
        Assert(tokenInfo != null, $"Token not exist.{tokenInfo?.Symbol}");
        var distributors = GenerateDistributors(tick);
        var limit = tokenInfo?.ExternalInfo.Value[InscriptionContractConstants.InscriptionLimitKey];
        Assert(long.TryParse(limit, out var lim), "Invalid inscription limit.");
        State.InscribedLimit[tick] = lim;
        var remain = tokenInfo.TotalSupply % distributors.Values.Count;
        var amount = tokenInfo.TotalSupply.Div(distributors.Values.Count);
        if (remain == 0)
        {
            Issue(symbol, amount, distributors.Values.ToList());
        }
        else
        {
            Issue(symbol, amount, distributors.Values.ToList().GetRange(0, distributors.Values.Count - 2));
            Issue(symbol, remain, new List<Hash> { distributors.Values.Last() });
        }

        var inscriptionInfo =
            $@"{{""p"":""{InscriptionContractConstants.InscriptionType}"",""op"":""deploy"",""tick"":""{tick}"",""max"":""{tokenInfo.TotalSupply}"",""lim"":""{lim}""}}";

        Context.Fire(new InscriptionIssued
        {
            Symbol = symbol,
            Tick = tick,
            Amt = tokenInfo.TotalSupply,
            To = Context.Self,
            InscriptionInfo = inscriptionInfo
        });

        return new Empty();
    }


    public override Empty Inscribe(InscribedInput input)
    {
        Assert(!string.IsNullOrWhiteSpace(input.Tick) && input.Amt > 0 && input.Amt <= State.InscribedLimit[input.Tick], "Invalid input.");
        var tick = input.Tick;
        var symbol = $"{tick?.ToUpper()}-{InscriptionContractConstants.NftSymbolSuffix}";
        var distributors = State.DistributorHashList[input.Tick];
        Assert(distributors != null, "Empty distributors.");
        var selectIndex = (int)(Context.OriginTransactionId.ToInt64() % distributors.Values.Count);
        Context.SendVirtualInline(distributors.Values[selectIndex], State.TokenContract.Value,
            nameof(State.TokenContract.Transfer), new TransferInput
            {
                Symbol = symbol,
                Amount = input.Amt,
                To = Context.Sender
            });
        var inscriptionInfo =
            $@"{{""p"":""{InscriptionContractConstants.InscriptionType}"",""op"":""mint"",""tick"":""{tick}"",""amt"":""{input.Amt}""}}";
        Context.Fire(new InscriptionTransferred
        {
            From = Context.Self,
            Symbol = symbol,
            Tick = tick?.ToUpper(),
            Amt = input.Amt,
            To = Context.Sender,
            InscriptionInfo = inscriptionInfo
        });
        return new Empty();
    }
}
using System.Linq;
using System.Text;
using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Forest.Contracts.Inscription;

public partial class InscriptionContract : InscriptionContractContainer.InscriptionContractBase
{
    public override Empty Initialize(InitializeInput input)
    {
        Assert(!State.Initialized.Value, "Already initialized.");
        State.GenesisContract.Value = Context.GetZeroSmartContractAddress();
        Assert(State.GenesisContract.GetContractInfo.Call(Context.Self).Deployer == Context.Sender, "No permission.");
        Assert(input.Admin != null && input.Admin.Value.Any() && input.IssueChainId > 0, "Invalid input.");
        State.TokenContract.Value =
            Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
        State.ConfigurationContract.Value =
            Context.GetContractAddressByName(SmartContractConstants.ConfigurationContractSystemName);
        State.Admin.Value = input.Admin;
        State.IssueChainId.Value = input.IssueChainId;
        State.Initialized.Value = true;
        return new Empty();
    }

    public override Empty ChangeAdmin(Address input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(input.Value.Any(), "Invalid input.");
        State.Admin.Value = input;
        return new Empty();
    }


    public override Empty DeployInscription(DeployInscriptionInput input)
    {
        Assert(State.Initialized.Value, "Not initialized yet.");
        Assert(!string.IsNullOrWhiteSpace(input.SeedSymbol) && !string.IsNullOrWhiteSpace(input.Tick) &&
               input.Max > 0 && input.Limit > 0 && input.Limit <= input.Max, "Invalid input.");
        var imageSize = GetImageSizeLimit();
        Assert(!string.IsNullOrWhiteSpace(input.Image) && Encoding.UTF8.GetByteCount(input.Image) <= imageSize,
            "Invalid image data.");
        var tick = input.Tick?.ToUpper();
        // Approve Seed.
        State.TokenContract.TransferFrom.Send(new TransferFromInput
        {
            Symbol = input.SeedSymbol,
            From = Context.Sender,
            To = Context.Self,
            Amount = 1,
        });

        var issueChainId = State.IssueChainId.Value;

        // Create collection
        var collectionExternalInfo =
            GenerateExternalInfo(tick, input.Max, input.Limit, input.Image, SymbolType.NftCollection);
        CreateInscription(tick, input.Max, issueChainId, collectionExternalInfo,
            SymbolType.NftCollection);

        // Create nft item
        var nftExternalInfo =
            GenerateExternalInfo(tick, input.Max, input.Limit, input.Image, SymbolType.Nft);
        CreateInscription(tick, input.Max, issueChainId, nftExternalInfo, SymbolType.Nft);
        State.InscribedLimit[tick] = input.Limit;

        Context.Fire(new InscriptionCreated
        {
            Tick = tick,
            TotalSupply = input.Max,
            IssueChainId = issueChainId,
            Issuer = Context.Self,
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
        Assert(State.Initialized.Value, "Not initialized yet.");
        Assert(!string.IsNullOrWhiteSpace(input.Tick), "Invalid input.");
        var tick = input.Tick?.ToUpper();
        var symbol = GetNftSymbol(tick);
        var collectionSymbol = GetCollectionSymbol(tick);
        var tokenInfo = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
        {
            Symbol = collectionSymbol
        });
        Assert(tokenInfo != null && !string.IsNullOrEmpty(tokenInfo.Symbol), $"Token not exist.{tokenInfo?.Symbol}");
        var distributors = GenerateDistributors(tick);
        var info = DeployInscriptionInfo.Parser.ParseJson(
            tokenInfo?.ExternalInfo.Value[InscriptionContractConstants.InscriptionDeployKey]);
        Assert(long.TryParse(info.Lim, out var lim), "Invalid inscription limit.");
        State.InscribedLimit[tick] = lim;
        IssueAndModifyBalance(tick, symbol, tokenInfo.TotalSupply, distributors.Values.ToList());

        Context.Fire(new InscriptionIssued
        {
            Tick = tick,
            Amt = tokenInfo.TotalSupply,
            To = Context.Self,
            InscriptionInfo = info.ToString()
        });

        return new Empty();
    }


    public override Empty Inscribe(InscribedInput input)
    {
        Assert(State.Initialized.Value, "Not initialized yet.");
        var tick = input.Tick?.ToUpper();
        var tokenInfo = CheckInputAndGetSymbol(tick, input.Amt);
        TransferWithDistributor(tick, tokenInfo.Symbol, input.Amt);
        Context.Fire(new InscriptionTransferred
        {
            From = Context.Self,
            Tick = tick,
            Amt = input.Amt,
            To = Context.Sender,
            InscriptionInfo = tokenInfo.ExternalInfo.Value[InscriptionContractConstants.InscriptionMintKey]
        });
        return new Empty();
    }

    public override Empty MintInscription(InscribedInput input)
    {
        Assert(State.Initialized.Value, "Not initialized yet.");
        var tick = input.Tick?.ToUpper();
        var tokenInfo = CheckInputAndGetSymbol(tick, input.Amt);
        TransferWithDistributors(tick, tokenInfo.Symbol, input.Amt);
        Context.Fire(new InscriptionTransferred
        {
            From = Context.Self,
            Tick = tick,
            Amt = input.Amt,
            To = Context.Sender,
            InscriptionInfo = tokenInfo.ExternalInfo.Value[InscriptionContractConstants.InscriptionMintKey]
        });
        return new Empty();
    }

    public override Empty SetIssueChainId(Int32Value input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(input != null && input.Value > 0, "Invalid input.");
        State.IssueChainId.Value = input.Value;
        return new Empty();
    }

    public override Empty SetDistributorCount(Int32Value input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(input != null && input.Value > 0, "Invalid input.");
        State.DistributorCount.Value = input.Value;
        return new Empty();
    }

    public override Empty SetImageSizeLimit(Int32Value input)
    {
        Assert(Context.Sender == State.Admin.Value, "No permission.");
        Assert(input != null && input.Value > 0, "Invalid input.");
        State.ImageSizeLimit.Value = input.Value;
        return new Empty();
    }
}
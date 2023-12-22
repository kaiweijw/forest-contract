using System.Collections.Generic;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Types;

namespace Forest.Inscription;

public partial class InscriptionContract
{
    private ExternalInfo GenerateExternalInfo(string tick, long max, long limit, string image, SymbolType symbolType)
    {
        var externalInfo = new ExternalInfo();
        if (symbolType == SymbolType.NftCollection)
        {
            var dic = new Dictionary<string, string>();
            var info =
                $@"{{""p"":""{InscriptionContractConstants.InscriptionType}"",""op"":""deploy"",""tick"":""{tick}"",""max"":""{max}"",""lim"":""{limit}""}}";
            dic[InscriptionContractConstants.InscriptionDeployKey] = info;
            dic[InscriptionContractConstants.InscriptionImageKey] = image;
            externalInfo.Value.Add(dic);
        }
        else
        {
            var dic = new Dictionary<string, string>();
            var info =
                $@"{{""p"":""{InscriptionContractConstants.InscriptionType}"",""op"":""mint"",""tick"":""{tick}"",""amt"":""{InscriptionContractConstants.InscriptionAmt}""}}";
            dic[InscriptionContractConstants.InscriptionMintKey] = info;
            dic[InscriptionContractConstants.InscriptionImageKey] = image;
            dic[InscriptionContractConstants.InscriptionLimitKey] = $"{limit}";
            externalInfo.Value.Add(dic);
        }

        return externalInfo;
    }

    private string CreateInscription(string tick, long max, int issueChainId, ExternalInfo externalInfo,
        SymbolType symbolType)
    {
        var symbol = symbolType == SymbolType.NftCollection
            ? $"{tick.ToUpper()}-{InscriptionContractConstants.CollectionSymbolSuffix}"
            : $"{tick.ToUpper()}-{InscriptionContractConstants.NftSymbolSuffix}";
        
        var creatTokenInput = new CreateInput
        {
            Symbol = symbol,
            TokenName = tick.ToUpper(),
            TotalSupply = max,
            Decimals = InscriptionContractConstants.InscriptionDecimals,
            Issuer = Context.Self,
            IsBurnable = true,
            IssueChainId = issueChainId,
            ExternalInfo = externalInfo,
            Owner = Context.Self
        };
        State.TokenContract.Create.Send(creatTokenInput);
        return symbol;
    }
    
    private void Issue(string symbol, long amount, List<Hash> distributors)
    {
        foreach (var distributor in distributors)
        {
            State.TokenContract.Issue.Send(new IssueInput
            {
                Symbol = symbol,
                Amount = amount,
                To = Context.ConvertVirtualAddressToContractAddress(distributor)
            });
        }
    }

    private HashList GenerateDistributors(string tick)
    {
        var distributors = new HashList();
        var salt = Context.TransactionId.ToInt64();
        for (var i = 0; i < InscriptionContractConstants.DistributorsCount; i++)
        {
            var distributor = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(salt.Add(i)), Context.TransactionId);
            distributors.Values.Add(distributor);
        }

        State.DistributorHashList[tick.ToUpper()] = distributors;
        return distributors;
    }
    
}
using System.Collections.Generic;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.CSharp.Core;
using AElf.Sdk.CSharp;
using AElf.Types;

namespace Forest.Inscription;

public partial class InscriptionContract
{
    private ExternalInfo GenerateExternalInfo(string tick, long max, long limit, string image, SymbolType symbolType)
    {
        var externalInfo = new ExternalInfo();
        var dic = new Dictionary<string, string>
        {
            [InscriptionContractConstants.InscriptionImageKey] = image
        };
        if (symbolType == SymbolType.NftCollection)
        {
            var info =
                $@"{{""p"":""{InscriptionContractConstants.InscriptionType}"",""op"":""deploy"",""tick"":""{tick}"",""max"":""{max}"",""lim"":""{limit}""}}";
            dic[InscriptionContractConstants.InscriptionDeployKey] = info;
        }
        else
        {
            var info =
                $@"{{""p"":""{InscriptionContractConstants.InscriptionType}"",""op"":""mint"",""tick"":""{tick}"",""amt"":""{InscriptionContractConstants.InscriptionAmt}""}}";
            dic[InscriptionContractConstants.InscriptionMintKey] = info;
            dic[InscriptionContractConstants.InscriptionLimitKey] = $"{limit}";
        }
        externalInfo.Value.Add(dic);

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

    private void IssueAndModifyBalance(string tick, string symbol, long amount, List<Hash> distributors)
    {
        foreach (var distributor in distributors)
        {
            State.TokenContract.Issue.Send(new IssueInput
            {
                Symbol = symbol,
                Amount = amount,
                To = Context.ConvertVirtualAddressToContractAddress(distributor)
            });
            ModifyDistributorBalance(tick,distributors,amount);
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
    
    private void SelectDistributorsAndTransfer(string tick, string symbol, HashList distributors, long amt)
    {
        var selectIndex = (int)(Context.OriginTransactionId.ToInt64() % distributors.Values.Count);
        var count = 0;
        do
        {
            var distributor = distributors.Values[selectIndex];
            var selectDistributorBalance = State.DistributorBalance[tick][distributor];
            if (selectDistributorBalance < amt)
            {
                DistributeInscription(tick, symbol, selectDistributorBalance, amt, distributor);
                State.DistributorHashList[tick].Values.Remove(distributor);
                amt = amt.Sub(selectDistributorBalance);
                count++;
                selectIndex = selectIndex == distributors.Values.Count.Sub(1) ? 0 : selectIndex.Add(1);
            }
            else
            {
                DistributeInscription(tick, symbol, selectDistributorBalance, amt, distributor);
                break;
            }
        } while (count < InscriptionContractConstants.RetrySelectDistributorCount);
    }

    private void DistributeInscription(string tick, string symbol, long balance, long amt, Hash distributor)
    {
        State.DistributorBalance[tick][distributor] = balance.Sub(amt);
        Context.SendVirtualInline(distributor, State.TokenContract.Value,
            nameof(State.TokenContract.Transfer), new TransferInput
            {
                Symbol = symbol,
                Amount = balance,
                To = Context.Sender
            });
    }

    private void ModifyDistributorBalance(string tick, List<Hash> distributors, long amount)
    {
        foreach (var distributor in distributors)
        {
            State.DistributorBalance[tick][distributor] =
                State.DistributorBalance[tick.ToUpper()][distributor].Add(amount);
        }
    }
}
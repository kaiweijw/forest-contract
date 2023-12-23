using System;
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
            var info = new DeployInscriptionOperation
            {
                P = InscriptionContractConstants.InscriptionType,
                Op = "deploy",
                Tick = tick,
                Max = max.ToString(),
                Lim = limit.ToString()
            };
            dic[InscriptionContractConstants.InscriptionDeployKey] = info.ToString();
        }
        else
        {
            var info = new MintInscriptionOperation
            {
                P = InscriptionContractConstants.InscriptionType,
                Op = "mint",
                Tick = tick,
                Amt = InscriptionContractConstants.InscriptionAmt.ToString()
            };
            dic[InscriptionContractConstants.InscriptionMintKey] = info.ToString();
        }

        externalInfo.Value.Add(dic);

        return externalInfo;
    }

    private string CreateInscription(string tick, long max, int issueChainId, ExternalInfo externalInfo,
        SymbolType symbolType)
    {
        var symbol = symbolType == SymbolType.NftCollection
            ? GetCollectionSymbol(tick)
            : GetNftSymbol(tick);

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
            ModifyDistributorBalance(tick, distributor, amount);
        }
    }

    private HashList GenerateDistributors(string tick)
    {
        var distributors = new HashList();
        var distributorCount = State.DistributorCount.Value == 0
            ? InscriptionContractConstants.DistributorsCount
            : State.DistributorCount.Value;
        for (var i = 0; i < distributorCount; i++)
        {
            var distributor = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(i), Context.OriginTransactionId);
            distributors.Values.Add(distributor);
        }

        State.DistributorHashList[tick.ToUpper()] = distributors;
        return distributors;
    }

    private void SelectDistributorAndTransfer(string tick, string symbol, long amt, InscribeType inscribeType)
    {
        var distributors = State.DistributorHashList[tick];
        Assert(distributors != null, "Empty distributors.");
        var selectIndex = (int)((Math.Abs(Context.Sender.ToByteArray().ToInt64(true)) % distributors.Values.Count));
        if (inscribeType == InscribeType.Parallel)
        {
            var distributor = distributors.Values[selectIndex];
            var selectDistributorBalance = State.DistributorBalance[tick][distributor];
            Assert(selectDistributorBalance >= amt,
                $"Distributor balance not enough.{Context.ConvertVirtualAddressToContractAddress(distributor)}");
            DistributeInscription(tick, symbol, selectDistributorBalance, amt, distributor);
        }
        else
        {
            var count = 0;
            do
            {
                var distributor = distributors.Values[selectIndex];
                var selectDistributorBalance = State.DistributorBalance[tick][distributor];
                if (selectDistributorBalance < amt)
                {
                    DistributeInscription(tick, symbol, selectDistributorBalance, amt, distributor);
                    amt = amt.Sub(selectDistributorBalance);
                    count++;
                    selectIndex = selectIndex == distributors.Values.Count.Sub(1) ? 0 : selectIndex.Add(1);
                }
                else
                {
                    DistributeInscription(tick, symbol, selectDistributorBalance, amt, distributor);
                    break;
                }
            } while (count < distributors.Values.Count);
        }
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

    private TokenInfo CheckInputAndGetSymbol(string tick, long amt)
    {
        Assert(!string.IsNullOrWhiteSpace(tick) && amt > 0 && amt <= State.InscribedLimit[tick],
            "Invalid input.");
        var symbol = GetNftSymbol(tick);
        var tokenInfo = State.TokenContract.GetTokenInfo.Call(new GetTokenInfoInput
        {
            Symbol = symbol
        });
        Assert(tokenInfo != null, "Token not exists.");
        return tokenInfo;
    }

    private void ModifyDistributorBalance(string tick, Hash distributor, long amount)
    {
        State.DistributorBalance[tick][distributor] =
            State.DistributorBalance[tick.ToUpper()][distributor].Add(amount);
    }

    private string GetNftSymbol(string tick)
    {
        return $"{tick.ToUpper()}-{InscriptionContractConstants.NftSymbolSuffix}";
    }

    private string GetCollectionSymbol(string tick)
    {
        return $"{tick.ToUpper()}-{InscriptionContractConstants.CollectionSymbolSuffix}";
    }
}
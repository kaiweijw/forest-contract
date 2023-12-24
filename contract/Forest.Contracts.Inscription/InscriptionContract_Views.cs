using System;
using System.Linq;
using AElf;
using AElf.Contracts.MultiToken;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Forest.Contracts.Inscription;

public partial class InscriptionContract
{
    public override Int64Value GetInscribedLimit(StringValue input)
    {
        return new Int64Value
        {
            Value = State.InscribedLimit[input.Value.ToUpper()]
        };
    }

    public override AddressList GetDistributorList(StringValue input)
    {
        var distributors = State.DistributorHashList[input.Value.ToUpper()]?.Values;
        return new AddressList
        {
            Values = { distributors?.Select(d => Context.ConvertVirtualAddressToContractAddress(d)) }
        };
    }

    public override DistributorsBalanceList GetDistributorBalance(StringValue input)
    {
        var result = new DistributorsBalanceList();
        var tick = input.Value.ToUpper();
        var distributors = State.DistributorHashList[tick]?.Values;
        if (distributors == null || distributors.Count <= 0) return result;
        foreach (var distributor in distributors)
        {
            var balance = State.DistributorBalance[tick][distributor];
            result.Values.Add(new DistributorsBalance
            {
                Distributor = Context.ConvertVirtualAddressToContractAddress(distributor),
                Balance = balance
            });
        }

        return result;
    }

    public override Address GetAdmin(Empty input)
    {
        return State.Admin.Value;
    }
    
    public override Int32Value GetIssueChainId(Empty input)
    {
        return new Int32Value
        {
            Value = State.IssueChainId.Value
        };
    }
    
    public override Int32Value GetDistributorCount(Empty input)
    {
        var result = State.DistributorCount.Value == 0
            ? InscriptionContractConstants.DistributorsCount
            : State.DistributorCount.Value;
        return new Int32Value
        {
            Value = result
        };
    }

    public override BoolValue CheckDistributorBalance(CheckDistributorBalanceInput input)
    {
        Assert(input.Sender != null && input.Amt > 0 && !string.IsNullOrWhiteSpace(input.Tick), "Invalid input.");
        var tick = input.Tick?.ToUpper();
        var result = new BoolValue
        {
            Value = false
        };
        var count = State.DistributorHashList[tick].Values.Count;
        var selectIndex = (int)((Math.Abs(input.Sender.ToByteArray().ToInt64(true)) % count));
        var distributor = State.DistributorHashList[tick];
        if (distributor == null || distributor.Values.Count <= 0) return result;
        var balance = State.TokenContract.GetBalance.Call(new GetBalanceInput
        {
            Symbol = GetNftSymbol(tick),
            Owner = Context.ConvertVirtualAddressToContractAddress(distributor.Values[selectIndex])
        });
        if (balance.Balance < input.Amt) return result;
        result.Value = true;
        return result;
    }
}
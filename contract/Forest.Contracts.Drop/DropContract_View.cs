using AElf;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Forest.Contracts.Drop;

public partial class DropContract
{
   
    public override Address GetAdmin(Empty input)
    {
        return State.Admin.Value;
    }
    
    public override Int32Value GetMaxDropDetailListCount(Empty input)
    {
        return new Int32Value { Value = State.MaxDropDetailListCount.Value };
    }
    
    public override Int32Value GetMaxDropDetailIndexCount(Empty input)
    {
        return new Int32Value { Value = State.MaxDropDetailIndexCount.Value };
    }
    
    public override DropInfo GetDropInfo(GetDropInfoInput input)
    {
        Assert(input != null && input.DropId != null && input.DropId != null, "Invalid input.");
        var dropInfo =  State.DropInfoMap[input.DropId];
        return dropInfo;
    }
    
    public override ClaimDropDetail GetClaimDropInfo(GetClaimDropInfoInput input)
    {
        Assert(input != null && input.DropId != null && input.Address != null, "Invalid input.");
        return State.ClaimDropMap[input.DropId][input.Address];
    }

    public override Int32Value GetDropSymbolExist(GetDropSymbolExistInput input)
    {
        return new Int32Value { Value = State.DropSymbolMap[input.DropId][input.Symbol]};
    }
    
    public override Hash GetDropId(GetDropIdInput input)
    {
        return  HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(input.TransactionId), HashHelper.ComputeFrom(input.Address));
    }

    public override DropDetailList GetDropDetailList(GetDropDetailListInput input)
    {
        return State.DropDetailListMap[input.DropId][input.Index];
    }
    public override Address GetProxyAccountContractAddress(Empty input)
    {
        return State.ProxyAccountContract.Value;
    }
}
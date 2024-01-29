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
    
    public override DropInfo GetDropInfo(GetDropInfoInput input)
    {
        Assert(input != null && input.DropId != null && input.DropId != null && input.Index > 0, "Invalid input.");
        var dropInfo =  State.DropInfoMap[input.DropId];
        Assert(dropInfo != null, "Drop info not found.");
        Assert(dropInfo.MaxIndex >= input.Index, "Invalid drop index.");

        if(input.Index == 1)
            return dropInfo;

        var dropDetailList = State.DropDetailListMap[input.DropId][input.Index];
        Assert(dropDetailList != null, "Drop detail list not found.");
        dropInfo.DetailList = dropDetailList;
        return dropInfo;
    }
    
    public override ClaimDropDetail GetClaimDropInfo(GetClaimDropInfoInput input)
    {
        Assert(input != null && input.DropId != null && input.Address != null, "Invalid input.");
        return State.ClaimDropMap[input.DropId][input.Address];
    }

}
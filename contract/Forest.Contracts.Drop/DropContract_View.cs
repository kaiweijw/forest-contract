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
    
}
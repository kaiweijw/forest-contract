using System.Linq;
using Google.Protobuf.WellKnownTypes;

namespace Forest.Inscription;

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
}
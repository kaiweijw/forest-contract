using AElf;
using AElf.Types;
using Forest.Whitelist;
using Google.Protobuf;

namespace Forest.Helpers;

public static class WhitelistHelper
{
    internal static Hash CalculateProjectId(string symbol, Address sender)
    {
        return HashHelper.ComputeFrom($"{symbol}{sender}");
    }

    internal static Price DeserializedInfo(TagInfo tagInfo)
    {
        var deserializedInfo = new PriceTag();
        deserializedInfo.MergeFrom(tagInfo.Info);
        return new Price
        {
            Symbol = deserializedInfo.Symbol,
            Amount = deserializedInfo.Amount
        };
    }
}
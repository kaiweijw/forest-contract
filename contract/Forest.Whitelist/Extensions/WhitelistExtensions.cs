using AElf;
using AElf.Types;

namespace Forest.Whitelist.Extensions;

public static class WhitelistExtensions
{
    public static Hash CalculateExtraInfoId(this Hash whitelistId, Hash projectId, string tagName)
    {
        return HashHelper.ComputeFrom($"{whitelistId}{projectId}{tagName}");
    }
}
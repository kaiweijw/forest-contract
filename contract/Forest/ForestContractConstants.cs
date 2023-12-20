namespace Forest;

public partial class ForestContract
{
    public const int FeeDenominator = 10000;
    public const int DefaultExpireDays = 100000;
    public const int DefaultServiceFeeRate = 10;
    public const int DefaultServiceFeeAmount = 1_00000000;
    public const int DefaultMaxListCount = 60;
    public const int DefaultMaxOfferCount = 60;
    public const int DefaultMaxTokenWhiteListCount = 20;
    public const int DefaultMaxOfferDealCount = 10;
    public const int BatchDeListTypeLessThan = 0;
    public const int BatchDeListTypeLessThanOrEquals = 1;
    public const int BatchDeListTypeGreaterThan = 2;
    public const int BatchDeListTypeGreaterThanOrEquals = 3;
}
namespace Forest.Contracts.SymbolRegistrar;

public class SymbolRegistrarContractConstants
{
    public const string ELFSymbol = "ELF";
    public const char NFTSymbolSeparator = '-';
        
    // Default auction configuration
    public const long DefaultDuration = 60 * 60 * 24 * 14; // 14 days
    public const long DefaultCountdownTime = 60 * 10; // 10 minutes
    public const long DefaultMaxExtensionTime = 60 * 60 * 24 * 7; // 7 days
    public const int DefaultMinMarkup = 1000; // 10%

    public const long DefaultSeedExpirationTime = 60 * 60 * 24 * 365; // 365 days

    public const int MaxAddSpecialSeedCount = 500;
    public const int MaxSymbolLength = 30;
    public const string SeedPrefix = "SEED-";
    public const int MaxCycleCount = 30;
    public const string CollectionSymbolSuffix = "0";
    public const string SeedOwnedSymbolExternalInfoKey = "__seed_owned_symbol";
    public const string SeedExpireTimeExternalInfoKey = "__seed_exp_time";
    public const string NftImageUrlExternalInfoKey = "__nft_image_url";
    public const string NftImageUrlSuffix = ".svg";
}
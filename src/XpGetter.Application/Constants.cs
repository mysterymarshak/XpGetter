namespace XpGetter.Application;

public static class Constants
{
    public const string ProgramName = "XpGetter";
    public const string Author = "mysterymarshak";
    public const string MasterBranch = "master";
    public const string Version = "0.3.0";
    public const string ConfigVersion = "1.3";
    public const int MaxInventoryHistoryPagesToLoad = 3;
    public const int MaxAccounts = 5;
    public const int Cs2AppId = 730;
    public const string GitHubPageUrl = $"https://github.com/{Author}/{ProgramName}";
    public const string GitHubReleasesPageUrl = $"{GitHubPageUrl}/releases";
}

// CONFIG VERSION CHANGES

// 1.0 -> 1.1:
// Added field "PersonalName" (personal_name) to Account

// 1.1 -> 1.2:
// Removed field "PersonalName" (personal_name) from Account
// Added object "CacheData" (cache_data) to Account
// Added field "IsCacheEnabled" (is_cache_enabled) to AppConfiguration
// AccountCacheData:
// LastUpdated (last_updated)
// PersonalName (personal_name)
// WalletCurrency (wallet_currency)

// 1.2 -> 1.3
// Added field "FamilyViewPin" (family_view_pin) to Account

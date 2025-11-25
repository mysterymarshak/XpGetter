using Newtonsoft.Json;

namespace XpGetter.Configuration.Entities;

public class AccountCacheData
{
    [JsonProperty("last_updated")]
    public DateTimeOffset? LastUpdated { get; set; }

    [JsonProperty("personal_name")]
    public string? PersonalName { get; set; }

    [JsonProperty("wallet_currency")]
    public string? WalletCurrency { get; set; }
}

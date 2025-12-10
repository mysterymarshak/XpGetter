using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;

namespace XpGetter.Application.Features.Configuration.Entities;

public class AppConfiguration
{
    [JsonProperty("version")]
    public required string Version { get; set; }

    [JsonProperty("is_cache_enabled")]
    public bool IsCacheEnabled { get; set; } = false;

    [JsonProperty("accounts")]
    public IEnumerable<Account> Accounts { get; set; } = [];

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Version))
        {
            throw new ValidationException();
        }

        foreach (var account in Accounts)
        {
            account.Validate();
        }

        // TODO: find better place
    }

    // public void EnableCache()
    // {
    //     IsCacheEnabled = true;
    //     ForceDisableCacheForOnce = true;
    //
    //     foreach (var account in Accounts)
    //     {
    //         account.CacheData = new AccountCacheData();
    //     }
    // }
}
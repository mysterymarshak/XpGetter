using Newtonsoft.Json;

namespace XpGetter.Settings.Entities;

public class SettingsRoot
{
    [JsonProperty("version")]
    public required string Version { get; set; }

    [JsonProperty("accounts")]
    public IEnumerable<Account> Accounts { get; set; } = [];
}
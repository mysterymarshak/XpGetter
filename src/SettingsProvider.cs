using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;
using Spectre.Console;

namespace XpGetter;

public class SettingsProvider
{
    private readonly ILogger<SettingsProvider> _logger;
    private string SettingsPath => Path.Combine(Directory.GetCurrentDirectory(), FileName);

    private const string FileName = "settings.json";

    public SettingsProvider(ILogger<SettingsProvider> logger)
    {
        _logger = logger;
    }

    public SettingsJson Import()
    {
        if (!File.Exists(SettingsPath))
        {
            return WriteDefaultConfig();
        }

        var fileContent = File.ReadAllText(SettingsPath);
        var deserialized = JsonConvert.DeserializeObject<SettingsJson>(fileContent);

        if (deserialized is null)
        {
            _logger.LogWarning("Settings file is invalid. Replacing it with the default one.");
            deserialized = WriteDefaultConfig();
        }

        return deserialized;
    }

    public void AddAccount(Account account, SettingsJson settings)
    {
        settings.Accounts = settings.Accounts.Append(account);
    }

    public void Sync(SettingsJson settings)
    {
        File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(settings));
    }

    private SettingsJson WriteDefaultConfig()
    {
        var settings = new SettingsJson { Version = Constants.ConfigVersion };
        Sync(settings);

        return settings;
    }
}

public class SettingsJson
{
    [JsonProperty("version")]
    public required string Version { get; set; }

    [JsonProperty("accounts")]
    public IEnumerable<Account> Accounts { get; set; } = [];
}

public class Account
{
    [JsonProperty("id")]
    public ulong Id { get; set; }

    [JsonProperty("access_token")]
    public string AccessToken { get; set; }

    [JsonProperty("refresh_token")]
    public string RefreshToken { get; set; }

    [JsonProperty("username")]
    public string Username { get; set; }
}

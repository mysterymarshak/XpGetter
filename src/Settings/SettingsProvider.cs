using System.Reflection;
using Newtonsoft.Json;
using Serilog;
using XpGetter.Settings.Entities;

namespace XpGetter.Settings;

public class SettingsProvider
{
    private string SettingsPath => Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, FileName);

    private readonly ILogger _logger;

    private const string FileName = "settings.json";

    public SettingsProvider(ILogger logger)
    {
        _logger = logger;
    }

    public SettingsRoot Import()
    {
        if (!File.Exists(SettingsPath))
        {
            return WriteDefaultConfig();
        }

        var fileContent = File.ReadAllText(SettingsPath);
        var deserialized = JsonConvert.DeserializeObject<SettingsRoot>(fileContent);

        if (deserialized is null)
        {
            _logger.Warning("Settings file is invalid. Replacing it with the default one.");
            deserialized = WriteDefaultConfig();
        }

        return deserialized;
    }

    public void AddAccount(Account account, SettingsRoot settings)
    {
        settings.Accounts = settings.Accounts
            .Append(account)
            .ToList();
    }

    public void RemoveAccount(Account account, SettingsRoot settings)
    {
        settings.Accounts = settings.Accounts
            .Where(x => x != account)
            .ToList();
    }

    public void Sync(SettingsRoot settings)
    {
        File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(settings));
    }

    private SettingsRoot WriteDefaultConfig()
    {
        var settings = new SettingsRoot { Version = Constants.ConfigVersion };
        Sync(settings);

        return settings;
    }
}

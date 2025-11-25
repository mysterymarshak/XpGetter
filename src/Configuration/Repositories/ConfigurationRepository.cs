using System.Reflection;
using Newtonsoft.Json;
using Serilog;
using XpGetter.Configuration.Entities;

namespace XpGetter.Configuration.Repositories;

public class ConfigurationRepository : IConfigurationRepository
{
    private string FilePath => Path.Combine(
        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, FileName);

    private const string FileName = "settings.json";

    private readonly ILogger _logger;

    private AppConfiguration? _configuration;

    public ConfigurationRepository(ILogger logger)
    {
        _logger = logger;
    }

    public AppConfiguration Get()
    {
        if (_configuration is not null)
        {
            return _configuration;
        }

        var configuration = LoadFromDiskOrWriteDefaults();
        StoreConfigurationInstance(configuration);

        return configuration;
    }

    private AppConfiguration LoadFromDiskOrWriteDefaults()
    {
        if (!File.Exists(FilePath))
        {
            var configuration = WriteDefaultToDisk();
            StoreConfigurationInstance(configuration);
            return _configuration!;
        }

        var fileContent = File.ReadAllText(FilePath);
        var deserialized = JsonConvert.DeserializeObject<AppConfiguration>(fileContent);

        if (deserialized is null)
        {
            _logger.Warning("Configuration file is invalid. Replacing it with the default one.");
            deserialized = WriteDefaultToDisk();
        }

        deserialized.Validate();
        return deserialized;
    }

    public void Export(AppConfiguration configuration)
    {
        StoreConfigurationInstance(configuration);
        File.WriteAllText(FilePath, JsonConvert.SerializeObject(configuration));
    }

    private AppConfiguration WriteDefaultToDisk()
    {
        var configuration = new AppConfiguration
        {
            Version = Constants.ConfigVersion
        };

        Export(configuration);
        return configuration;
    }

    private void StoreConfigurationInstance(AppConfiguration configuration)
    {
        _configuration = configuration;
    }
}

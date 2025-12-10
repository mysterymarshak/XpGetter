using System.Reflection;
using Newtonsoft.Json;
using Serilog;
using XpGetter.Configuration.Entities;
using XpGetter.Configuration.Repositories.FileOperationStrategies;
using XpGetter.Extensions;

namespace XpGetter.Configuration.Repositories;

public class ConfigurationRepository : IConfigurationRepository
{
    private string FilePath => Path.GetFilePathWithinExecutableDirectory(FileName);

    private const string FileName = "configuration";

    private readonly IFileOperationStrategy _fileOperationStrategy;
    private readonly ILogger _logger;

    private AppConfiguration? _configuration;

    public ConfigurationRepository(IFileOperationStrategy fileOperationStrategy, ILogger logger)
    {
        _fileOperationStrategy = fileOperationStrategy;
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

        var fileContent = _fileOperationStrategy.ReadFileContent(FilePath);
        var deserialized = JsonConvert.DeserializeObject<AppConfiguration>(fileContent);

        if (deserialized is null)
        {
            _logger.Warning(Messages.Configuration.IsInvalid);
            deserialized = WriteDefaultToDisk();
        }

        deserialized.Validate();
        return deserialized;
    }

    public void Export(AppConfiguration configuration)
    {
        StoreConfigurationInstance(configuration);
        _fileOperationStrategy.WriteFileContent(FilePath, JsonConvert.SerializeObject(configuration));
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

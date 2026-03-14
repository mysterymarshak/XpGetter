using Newtonsoft.Json;
using Serilog;
using XpGetter.Application.Features.Configuration.Entities;
using XpGetter.Application.Features.Configuration.Repositories.FileOperationStrategies;
using XpGetter.Application.Features.Io;

namespace XpGetter.Application.Features.Configuration.Repositories;

public class ConfigurationRepository : IConfigurationRepository
{
    private const string FileName = "configuration";

    private readonly IFileOperationStrategy _fileOperationStrategy;
    private readonly IFilesAccessor _filesAccessor;
    private readonly ILogger _logger;

    private AppConfiguration? _configuration;

    public ConfigurationRepository(IFileOperationStrategy fileOperationStrategy, IFilesAccessor filesAccessor, ILogger logger)
    {
        _fileOperationStrategy = fileOperationStrategy;
        _filesAccessor = filesAccessor;
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
        if (!_filesAccessor.Exists(FileName))
        {
            var configuration = WriteDefaultToDisk();
            StoreConfigurationInstance(configuration);
            return _configuration!;
        }

        var fileContent = _fileOperationStrategy.ReadFileContent(FileName);
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
        _fileOperationStrategy.WriteFileContent(FileName, JsonConvert.SerializeObject(configuration));
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

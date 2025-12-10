using OneOf;
using OneOf.Types;
using XpGetter.Configuration.Repositories;
using XpGetter.Dto;
using XpGetter.Mappers;
using XpGetter.Results;

namespace XpGetter.Configuration;

// TODO: make encrypted impl
public interface IConfigurationService
{
    AppConfigurationDto GetConfiguration();
    void WriteConfiguration(AppConfigurationDto configurationDto);
    OneOf<Success, AccountAlreadyExists> TryAddAccount(AppConfigurationDto configuration, AccountDto account);
}

public class ConfigurationService : IConfigurationService
{
    private readonly ConfigurationMapper _mapper;
    private readonly IConfigurationRepository _repository;

    public ConfigurationService(ConfigurationMapper mapper, IConfigurationRepository repository)
    {
        _mapper = mapper;
        _repository = repository;
    }

    public AppConfigurationDto GetConfiguration()
    {
        var configuration = _repository.Get();
        return _mapper.MapEntity(configuration);
    }

    public void WriteConfiguration(AppConfigurationDto configurationDto)
    {
        var configuration = _mapper.MapDto(configurationDto);
        _repository.Export(configuration);
    }

    public OneOf<Success, AccountAlreadyExists> TryAddAccount(AppConfigurationDto configuration, AccountDto account)
    {
        if (configuration.Accounts.Any(x => x.Id == account.Id))
        {
            return new AccountAlreadyExists();
        }

        configuration.AddAccount(account);
        return new Success();
    }
}

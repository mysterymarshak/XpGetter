using OneOf;
using OneOf.Types;
using XpGetter.Application.Dto;
using XpGetter.Application.Features.Configuration.Repositories;
using XpGetter.Application.Mappers;
using XpGetter.Application.Results;

namespace XpGetter.Application.Features.Configuration;

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
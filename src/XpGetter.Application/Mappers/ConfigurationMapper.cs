using XpGetter.Application.Dto;
using XpGetter.Application.Features.Configuration.Entities;

namespace XpGetter.Application.Mappers;

public class ConfigurationMapper
{
    private readonly AccountMapper _accountMapper;

    public ConfigurationMapper(AccountMapper accountMapper)
    {
        _accountMapper = accountMapper;
    }

    public AppConfigurationDto MapEntity(AppConfiguration entity)
    {
        var mappedAccounts = entity.Accounts
            .Select(_accountMapper.MapEntity)
            .ToList();

        return new AppConfigurationDto
        {
            Accounts = mappedAccounts,
            IsCacheEnabledField = entity.IsCacheEnabled,
            Version = entity.Version
        };
    }

    public AppConfiguration MapDto(AppConfigurationDto dto)
    {
        var mappedAccounts = dto.Accounts
            .Select(x => _accountMapper.MapDto(x, dto.IsCacheEnabled))
            .ToList();

        return new AppConfiguration
        {
            Accounts = mappedAccounts,
            IsCacheEnabled = dto.IsCacheEnabledField,
            Version = Constants.ConfigVersion
        };
    }
}
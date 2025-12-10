using XpGetter.Configuration.Entities;
using XpGetter.Dto;

namespace XpGetter.Mappers;

public class AccountMapper
{
    public AccountDto MapEntity(Account entity)
    {
        return new AccountDto
        {
            AccessToken = entity.AccessToken,
            Id = entity.Id,
            PersonalName = entity.CacheData?.PersonalName,
            WalletCurrency = entity.CacheData?.WalletCurrency,
            RefreshToken = entity.RefreshToken,
            Username = entity.Username,
            ActivityInfo = null // TODO: caching
        };
    }

    public Account MapDto(AccountDto dto, bool cacheDataIncluded)
    {
        var cacheData = cacheDataIncluded
            ? new AccountCacheData
            {
                LastUpdated = DateTimeOffset.UtcNow,
                PersonalName = dto.PersonalName,
                WalletCurrency = dto.WalletCurrency
            }
            : null;

        return new Account
        {
            AccessToken = dto.AccessToken,
            RefreshToken = dto.RefreshToken,
            Id = dto.Id,
            Username = dto.Username,
            CacheData = cacheData
        };
    }
}

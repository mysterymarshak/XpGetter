namespace XpGetter.Dto;

public class AppConfigurationDto
{
    public bool IsCacheEnabled => IsCacheEnabledField && !IsCacheDisabledForOnce;

    public required string Version { get; set; }
    public bool IsCacheEnabledField { get; set; } = false;
    public bool IsCacheDisabledForOnce { get; set; } = false;
    public IEnumerable<AccountDto> Accounts { get; set; } = [];

    public void AddAccount(AccountDto account)
    {
        Accounts = Accounts
            .Append(account)
            .ToList();
    }

    public void RemoveAccount(ulong id)
    {
        Accounts = Accounts
            .Where(x => x.Id != id)
            .ToList();
    }
}

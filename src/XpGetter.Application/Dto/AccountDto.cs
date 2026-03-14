using XpGetter.Application.Extensions;
using XpGetter.Application.Features.Configuration;

namespace XpGetter.Application.Dto;

public class AccountDto
{
    public ulong Id { get; set; }
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string? FamilyViewPin { get; set; }
    public string? PersonalName { get; set; }
    public string? WalletCurrency { get; set; }
    public ActivityInfo? ActivityInfo { get; set; }

    public string GetDisplayPersonalNameOrUsername()
    {
        if (RuntimeConfiguration.AnonymizePersonalNames)
        {
            return GetDisplayUsername();
        }

        var personalName = GetDisplayPersonalName(null);
        ThrowIfNull(personalName, Messages.Session.CannotGetPersonalName, GetDisplayUsername());

        return personalName;
    }

    public string GetDisplayUsername()
    {
        return Username.ToDisplayUsername(ignoreConfiguration: false);
    }

    public string? GetDisplayPersonalName(int? accountNumber)
    {
        return PersonalName?.ToDisplayPersonalName(accountNumber);
    }
}

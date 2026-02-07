using XpGetter.Application.Extensions;

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

    public string GetDisplayUsername()
    {
        return Username.ToDisplayString(false);
    }

    public string? GetDisplayPersonalName()
    {
        return PersonalName?.AnonymizeIfNeeded();
    }
}

namespace XpGetter.Dto;

public class AccountDto
{
    public ulong Id { get; set; }
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string? PersonalName { get; set; }
    public string? WalletCurrency { get; set; }
    public ActivityInfo? ActivityInfo { get; set; }
}
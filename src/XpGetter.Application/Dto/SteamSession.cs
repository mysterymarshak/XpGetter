using SteamKit2;
using SteamKit2.Internal;

namespace XpGetter.Application.Dto;

public class SteamSession
{
    public event Action<SteamSession>? AccountBind;

    public bool IsAuthenticated => Client is { SessionID: not null, SteamID: not null }
        && Client.SteamID.ConvertToUInt64() > 0;

    public AuthCookie AuthCookie => new(this, _parentalCookie);
    public string Name { get; private set; }
    public SteamClient Client { get; }
    public CallbackManager CallbackManager { get; }
    public SteamUser User { get; }
    public AccountDto? Account { get; private set; }
    public ParentalSettings? ParentalSettings { get; private set; }
    public WalletInfo? WalletInfo { get; private set; }

    private string? _parentalCookie;

    public SteamSession(string name, SteamClient client, CallbackManager callbackManager, SteamUser user)
    {
        Name = name;
        Client = client;
        CallbackManager = callbackManager;
        User = user;
    }

    public void BindAccount(AccountDto account)
    {
        Account = account;
        AccountBind?.Invoke(this);
    }

    public void BindName(string name)
    {
        Name = name;
    }

    public void BindParentalSettings(ParentalSettings? parentalSettings)
    {
        ParentalSettings = parentalSettings;
    }

    public void BindWalletInfo(WalletInfo walletInfo)
    {
        WalletInfo = walletInfo;
    }

    public void BindParentalCookie(string cookie)
    {
        _parentalCookie = cookie;
    }

    public void Dispose()
    {
        AccountBind = null;
        Client.Disconnect();
    }
}

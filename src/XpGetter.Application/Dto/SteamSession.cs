using SteamKit2;
using SteamKit2.Internal;

namespace XpGetter.Application.Dto;

public class SteamSession
{
    public event Action<SteamSession>? AccountBind;

    public bool IsAuthenticated => Client.SessionID is not null;

    public AuthCookie AuthCookie => new(this, _parentalCookie);
    public string Name { get; private set; }
    public SteamClient Client { get; }
    public CallbackManager CallbackManager { get; }
    public SteamUser User { get; }
    public AccountDto? Account { get; private set; }
    public ParentalSettings? ParentalSettings { get; private set; }

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

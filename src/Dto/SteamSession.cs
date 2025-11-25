using SteamKit2;

namespace XpGetter.Dto;

public class SteamSession
{
    public event Action<SteamSession>? AccountBind;

    public bool IsAuthenticated => Client.SessionID is not null;

    public string Name { get; private set; }
    public SteamClient Client { get; }
    public CallbackManager CallbackManager { get; }
    public SteamUser User { get; }
    public AccountDto? Account { get; private set; }

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

    public void Dispose()
    {
        AccountBind = null;
        Client.Disconnect();
    }
}

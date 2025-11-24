using SteamKit2;
using XpGetter.Settings.Entities;

namespace XpGetter.Dto;

public class SteamSession
{
    public bool IsAuthenticated => Client.SessionID is not null;

    public string Name { get; }
    public SteamClient Client { get; }
    public CallbackManager CallbackManager { get; }
    public SteamUser User { get; }
    public Account? Account { get; private set; }

    public SteamSession(string name, SteamClient client, CallbackManager callbackManager, SteamUser user, Account? account = null)
    {
        Name = name;
        Client = client;
        CallbackManager = callbackManager;
        User = user;
        Account = account;
    }

    public void BindAccount(Account account)
    {
        Account = account;
    }

    public void Dispose()
    {
        Client.Disconnect();
    }
}

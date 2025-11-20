using SteamKit2;

namespace XpGetter.Dto;

public record SteamSession(SteamClient Client, CallbackManager CallbackManager, SteamUser User)
{
    public void Dispose()
    {
        Client.Disconnect();
    }
}
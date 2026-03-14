using SteamKit2;
using SteamKit2.Internal;
using XpGetter.Application.Extensions;
using XpGetter.Application.Features.Steam;

namespace XpGetter.Application.Dto;

public class SteamSession
{
    public const string DefaultName = "<unnamed>";

    public event Action<SteamSession>? AccountBind;
    public event Action? PlayingStateChange;

    public bool IsAuthenticated => Client is { SessionID: not null, SteamID: var steamId } && steamId?.ConvertToUInt64() > 0;

    public AuthCookie AuthCookie => new(this, _parentalCookie);
    public SteamClient Client { get; }
    public MessagesFetcher MessagesFetcher { get; }
    public SteamUser User { get; }
    public AccountDto? Account { get; private set; }
    public ParentalSettings? ParentalSettings { get; private set; }
    public WalletInfo? WalletInfo { get; private set; }
    public bool VacBanned { get; private set; }
    public bool IsPlayingCs2
        => IsAuthenticated && (_playingAppId, _playingBlocked) is (Constants.Cs2AppId, false);
    public bool IsAnotherClientPlayingCs2
        => (_playingAppId, _playingBlocked) is (Constants.Cs2AppId, true);

    private uint _playingAppId;
    private bool _playingBlocked;

    private string _name;
    private string? _parentalCookie;

    public SteamSession(string name, SteamClient client, MessagesFetcher messagesFetcher, SteamUser user)
    {
        _name = name;
        Client = client;
        MessagesFetcher = messagesFetcher;
        User = user;
    }

    public string GetName(bool ignoreConfiguration = false)
    {
        if (_name == DefaultName)
        {
            return _name;
        }

        return _name.ToDisplayUsername(ignoreConfiguration);
    }

    public void BindAccount(AccountDto account)
    {
        Account = account;
        AccountBind?.Invoke(this);
    }

    public void BindName(string name)
    {
        _name = name;
    }

    public void BindParentalSettings(ParentalSettings? parentalSettings)
    {
        ParentalSettings = parentalSettings;
    }

    public void BindWalletInfo(WalletInfo walletInfo)
    {
        WalletInfo = walletInfo;
    }

    public void BindVacStatus(bool vacBanned)
    {
        VacBanned = vacBanned;
    }

    public void BindParentalCookie(string cookie)
    {
        _parentalCookie = cookie;
    }

    public void Dispose()
    {
        AccountBind = null;
        Client.Disconnect();
        MessagesFetcher.Dispose();
    }

    public void OnPlayingSessionState(SteamUser.PlayingSessionStateCallback callback)
    {
        if (callback.PlayingAppID is not (Constants.Cs2AppId or 0))
            return;

        _playingAppId = callback.PlayingAppID;
        _playingBlocked = callback.PlayingBlocked;

        PlayingStateChange?.Invoke();
    }
}

using System.Text;

namespace XpGetter.Application.Dto;

public class AuthCookie
{
    private AccountDto Account => _session.Account!;

    private readonly SteamSession _session;
    private readonly string? _parentalCookie;

    public AuthCookie(SteamSession session, string? parentalCookie)
    {
        _session = session;
        _parentalCookie = parentalCookie;
    }

    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.Append("steamLoginSecure=");
        sb.Append(Account.Id);
        sb.Append("||");
        sb.Append(Account.AccessToken);

        sb.Append(';');

        sb.Append("sessionid=");
        sb.Append(_session.Client.ID);

        if (_parentalCookie is not null)
        {
            sb.Append(';');

            sb.Append(_parentalCookie);
        }

        return sb.ToString();
    }
}

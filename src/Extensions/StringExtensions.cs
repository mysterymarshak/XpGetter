using XpGetter.Dto;

namespace XpGetter.Extensions;

public static class StringExtensions
{
    extension(string @string)
    {
        public string BindSession(SteamSession session, string? forceName = null)
        {
            return string.Format(Messages.Session.BoundedSessionLogFormat,
                session.Account?.Username ?? forceName ?? session.Name, @string);
        }
    }
}
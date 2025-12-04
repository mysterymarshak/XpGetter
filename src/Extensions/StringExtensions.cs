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

        // TODO: depends on configuration
        public string Censor()
        {
            if (@string.Length <= 4)
            {
                return "****";
            }

            return $"{@string.AsSpan()[..4]}{new string('*', @string.Length - 4)}";
        }
    }
}
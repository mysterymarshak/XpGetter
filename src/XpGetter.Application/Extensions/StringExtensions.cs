using XpGetter.Application.Dto;

namespace XpGetter.Application.Extensions;

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

            return string.Create(@string.Length, @string, (span, original) =>
            {
                original.AsSpan()[..4].CopyTo(span);
                span[4..].Fill('*');
            });
        }
    }
}
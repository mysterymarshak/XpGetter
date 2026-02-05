using XpGetter.Application.Dto;
using XpGetter.Application.Features.Configuration;

namespace XpGetter.Application.Extensions;

public static class StringExtensions
{
    extension(string @string)
    {
        public string BindSession(SteamSession session, string? forceName = null)
        {
            return string.Format(Messages.Session.BoundedSessionLogFormat,
                session.Account?.Username ?? forceName ?? session.GetName(ignoreConfiguration: true), @string);
        }

        public string ToDisplayString(bool ignoreConfiguration)
        {
            return (ignoreConfiguration, RuntimeConfiguration.AnonymizeUsernames, RuntimeConfiguration.CensorUsernames) switch
            {
                (true, _, _) => @string,
                (_, true, true) => "testswag".Censor(),
                (_, true, false) => "testswag",
                (_, false, true) => @string.Censor(),
                _ => @string
            };
        }

        public string AnonymizeIfNeeded()
        {
            if (RuntimeConfiguration.AnonymizeUsernames)
            {
                return Messages.Common.AnonymousName;
            }

            return @string;
        }

        public string Censor()
        {
            if (@string.Length <= 4)
            {
                return "****";
            }

            return string.Create(8, @string, (span, original) =>
            {
                original.AsSpan()[..4].CopyTo(span);
                span[4..].Fill('*');
            });
        }
    }
}

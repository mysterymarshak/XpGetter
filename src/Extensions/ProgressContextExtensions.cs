using Spectre.Console;
using XpGetter.Dto;

namespace XpGetter.Extensions;

public static class ProgressContextExtensions
{
    extension(ProgressContext ctx)
    {
        public ProgressTask AddTask(AccountDto account, string description)
        {
            return ctx.AddTask($"{account.Username}: {description}");
        }

        public ProgressTask AddTask(SteamSession session, string description)
        {
            return ctx.AddTask($"{session.Name}: {description}");
        }
    }
}

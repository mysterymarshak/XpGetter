using Spectre.Console;
using XpGetter.Application.Dto;

namespace XpGetter.Cli.Extensions;

public static class ProgressContextExtensions
{
    extension(ProgressContext ctx)
    {
        public ProgressTask AddTask(AccountDto account, string description)
        {
            return ctx.AddTask($"{account.GetDisplayUsername()}: {description}");
        }

        public ProgressTask AddTask(SteamSession session, string description)
        {
            return ctx.AddTask($"{session.GetName()}: {description}");
        }
    }
}

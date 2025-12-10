using Spectre.Console;
using XpGetter.Application.Dto;
using XpGetter.Application.Extensions;
using XpGetter.Application.Utils.Progress;

namespace XpGetter.Cli.Progress;

public class AnsiConsoleProgressContextWrapper : IProgressContext
{
    private readonly ProgressContext _ctx;

    public AnsiConsoleProgressContextWrapper(ProgressContext ctx)
    {
        _ctx = ctx;
    }

    public IProgressTask AddTask(SteamSession session, string description)
    {
        return new AnsiConsoleProgressTaskWrapper(_ctx.AddTask(session, description));
    }

    public IProgressTask AddTask(AccountDto account, string description)
    {
        return new AnsiConsoleProgressTaskWrapper(_ctx.AddTask(account, description));
    }
}
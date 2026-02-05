using Spectre.Console;
using XpGetter.Application.Dto;
using XpGetter.Application.Utils.Progress;
using XpGetter.Cli.Extensions;

namespace XpGetter.Cli.Progress;

public class AnsiConsoleProgressTaskWrapper : IProgressTask
{
    private readonly ProgressTask _task;

    public AnsiConsoleProgressTaskWrapper(ProgressTask task)
    {
        _task = task;
    }

    public void SetResult(string description)
    {
        _task.SetResult(description);
    }

    public void SetResult(SteamSession session, string description)
    {
        _task.SetResult(session, description);
    }

    public void SetResult(AccountDto account, string description)
    {
        _task.SetResult(account, description);
    }

    public void Description(SteamSession session, string description)
    {
        _task.Description(session, description);
    }

    public void Description(AccountDto account, string description)
    {
        _task.Description(account, description);
    }
}

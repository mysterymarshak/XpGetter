using Spectre.Console;
using XpGetter.Dto;
using XpGetter.Results.StateExecutionResults;

namespace XpGetter.Utils.Progress.AnsiConsole;

public class AnsiConsoleProgressTaskWrapper : IProgressTask
{
    private readonly ProgressTask _task;

    public AnsiConsoleProgressTaskWrapper(ProgressTask task)
    {
        _task = task;
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

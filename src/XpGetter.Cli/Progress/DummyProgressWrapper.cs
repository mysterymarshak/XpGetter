using XpGetter.Application.Dto;
using XpGetter.Application.Utils.Progress;

namespace XpGetter.Cli.Progress;

public class DummyProgressContextWrapper : IProgressContext
{
    public IProgressTask AddTask(string description)
    {
        return new DummyProgressTaskWrapper();
    }

    public IProgressTask AddTask(SteamSession session, string description)
    {
        return new DummyProgressTaskWrapper();
    }

    public IProgressTask AddTask(AccountDto account, string description)
    {
        return new DummyProgressTaskWrapper();
    }
}

public class DummyProgressTaskWrapper : IProgressTask
{
    public void SetResult(string description)
    {
    }

    public void SetResult(SteamSession session, string description)
    {
    }

    public void SetResult(AccountDto account, string description)
    {
    }

    public void Description(string description)
    {
    }

    public void Description(SteamSession session, string description)
    {
    }

    public void Description(AccountDto account, string description)
    {
    }
}

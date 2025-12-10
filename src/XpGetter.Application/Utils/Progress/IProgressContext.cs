using XpGetter.Application.Dto;

namespace XpGetter.Application.Utils.Progress;

public interface IProgressContext
{
    IProgressTask AddTask(SteamSession session, string description);
    IProgressTask AddTask(AccountDto account, string description);
}
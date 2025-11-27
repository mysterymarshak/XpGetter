using Spectre.Console;
using XpGetter.Dto;

namespace XpGetter.Results.StateExecutionResults;

public static class ProgressTaskExtensions
{
    extension(ProgressTask task)
    {
        public void Description(AccountDto account, string description)
        {
            task.Description($"{account.Username}: {description}");
        }
        
        public void Description(SteamSession session, string description)
        {
            task.Description($"{session.Name}: {description}");
        }
        
        public void SetResult(AccountDto account, string result)
        {
            task.Description(account, result);
            task.StopTask();
        }
        
        public void SetResult(SteamSession session, string result)
        {
            task.Description(session, result);
            task.StopTask();
        }
    }
}
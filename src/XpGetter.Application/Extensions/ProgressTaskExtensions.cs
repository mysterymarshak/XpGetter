using Spectre.Console;
using XpGetter.Application.Dto;

namespace XpGetter.Application.Extensions;

public static class ProgressTaskExtensions
{
    extension(ProgressTask task)
    {
        public void Description(AccountDto account, string description)
        {
            task.Description($"{account.Username.Censor()}: {description}");
        }

        public void Description(SteamSession session, string description)
        {
            task.Description($"{session.Name.Censor()}: {description}");
        }

        public void SetResult(string result)
        {
            task.Description(result);
            task.StopTask();
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

using XpGetter.Dto;

namespace XpGetter.Utils.Progress;

public interface IProgressTask
{
    void SetResult(SteamSession session, string description);
    void SetResult(AccountDto account, string description);
    void Description(SteamSession session, string description);
    void Description(AccountDto account, string description);
}
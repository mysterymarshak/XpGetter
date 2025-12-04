using XpGetter.Dto;
using XpGetter.Errors;
using XpGetter.Results;
using XpGetter.Utils.Progress;
using OneOf;

namespace XpGetter.Steam.Services.NewRankDropServices;

public interface INewRankDropService
{
    Task<OneOf<NewRankDrop, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>
        GetLastNewRankDropAsync(SteamSession session, IProgressContext ctx);
}
using OneOf;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;
using XpGetter.Application.Results;
using XpGetter.Application.Utils.Progress;

namespace XpGetter.Application.Features.Activity.NewRankDrops;

public interface INewRankDropsService
{
    Task<OneOf<NewRankDrop, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>
        GetLastNewRankDropAsync(SteamSession session, IProgressContext ctx);
    Task<OneOf<IReadOnlyList<NewRankDrop>, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>
        GetNewRankDropsAsync(SteamSession session, DateTimeOffset limit, IProgressContext ctx);
}

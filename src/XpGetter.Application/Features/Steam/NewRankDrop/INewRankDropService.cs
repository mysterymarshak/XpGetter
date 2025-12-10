using OneOf;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;
using XpGetter.Application.Results;
using XpGetter.Application.Utils.Progress;

namespace XpGetter.Application.Features.Steam.NewRankDrop;

public interface INewRankDropService
{
    Task<OneOf<Dto.NewRankDrop, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>
        GetLastNewRankDropAsync(SteamSession session, IProgressContext ctx);
}
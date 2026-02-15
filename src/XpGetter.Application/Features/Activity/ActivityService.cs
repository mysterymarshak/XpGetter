using OneOf;
using Serilog;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;
using XpGetter.Application.Features.Activity.NewRankDrops;
using XpGetter.Application.Features.Steam.Http.Parsers;
using XpGetter.Application.Results;
using XpGetter.Application.Utils.Progress;

namespace XpGetter.Application.Features.Activity;

public interface IActivityService
{
    Task<OneOf<ActivityInfo, ActivityServiceError>> GetActivityInfoAsync(SteamSession session, IProgressContext ctx);
}

public class ActivityService : IActivityService
{
    private readonly IMatchmakingService _matchmakingService;
    private readonly ILogger _logger;
    private readonly INewRankDropsService _newRankDropsService;
    private readonly ActivityInfoParser _activityInfoParser;

    public ActivityService(IMatchmakingService matchmakingService, INewRankDropsService newRankDropsService, ILogger logger)
    {
        _matchmakingService = matchmakingService;
        _newRankDropsService = newRankDropsService;
        _logger = logger;
        _activityInfoParser = new ActivityInfoParser();
    }

    public async Task<OneOf<ActivityInfo, ActivityServiceError>> GetActivityInfoAsync(
        SteamSession session, IProgressContext ctx)
    {
        var account = session.Account;
        if (account is null)
        {
            return new ActivityServiceError
            {
                Message = Messages.Activity.SessionWithNoAccount
            };
        }

        var getActivityInfoTask = ctx.AddTask(session, Messages.Statuses.RetrievingActivity);

        var tasks = new List<Task>
        {
            _matchmakingService.GetMatchmakingDataAsync(session, ctx),
            _newRankDropsService.GetLastNewRankDropAsync(session, ctx)
        };
        await Task.WhenAll(tasks);

        var getMatchmakingDataResult =
            ((Task<OneOf<MatchmakingData, MatchmakingServiceError>>)tasks[0]).Result;
        var getLastNewRankDropResult =
            ((Task<OneOf<NewRankDrop, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>)tasks[1]).Result;

        if (getMatchmakingDataResult.TryPickT1(out var matchmakingServiceError, out var document))
        {
            getActivityInfoTask.SetResult(session, Messages.Statuses.RetrievingActivityError);
            return new ActivityServiceError
            {
                Message = matchmakingServiceError.Message,
                Exception = matchmakingServiceError.Exception
            };
        }

        if (getLastNewRankDropResult.TryPickT3(out var newRankDropServiceError, out var remainder))
        {
            getActivityInfoTask.SetResult(session, Messages.Statuses.RetrievingActivityError);
            return new ActivityServiceError
            {
                Message = newRankDropServiceError.Message,
                Exception = newRankDropServiceError.Exception
            };
        }

        var lastNewRankDrop = remainder.IsT0 ? remainder.AsT0 : null;
        var additionalMessage = lastNewRankDrop is null ? (
            remainder.IsT1 ? remainder.AsT1.Message :
            remainder.IsT2 ? Messages.Activity.NoNewRankDropInfo : null) : null;

        lastNewRankDrop ??= new NewRankDrop(null, []);
        lastNewRankDrop.BindExternal(remainder.IsT2 ? true : null,
                                     remainder.IsT1 ? remainder.AsT1.LastEntryDateTime : null);

        getActivityInfoTask.SetResult(session, Messages.Statuses.RetrievingActivityOk);
        return new ActivityInfo
        {
            Account = account,
            AdditionalMessage = additionalMessage,
            MatchmakingData = getMatchmakingDataResult.AsT0,
            LastNewRankDrop = lastNewRankDrop
        };
    }
}

using HtmlAgilityPack;
using OneOf;
using Serilog;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;
using XpGetter.Application.Features.Steam.Http.Clients;
using XpGetter.Application.Features.Steam.Http.Responses.Parsers;
using XpGetter.Application.Features.Steam.NewRankDrop;
using XpGetter.Application.Results;
using XpGetter.Application.Utils.Progress;

namespace XpGetter.Application.Features.Steam;

public interface IActivityService
{
    Task<OneOf<ActivityInfo, ActivityServiceError>> GetActivityInfoAsync(SteamSession session, IProgressContext ctx);
}

public class ActivityService : IActivityService
{
    private readonly ISteamHttpClient _steamHttpClient;
    private readonly ILogger _logger;
    private readonly INewRankDropService _newRankDropService;
    private readonly ActivityInfoParser _activityInfoParser;

    public ActivityService(ISteamHttpClient steamHttpClient, INewRankDropService newRankDropService, ILogger logger)
    {
        _steamHttpClient = steamHttpClient;
        _newRankDropService = newRankDropService;
        _logger = logger;
        _activityInfoParser = new ActivityInfoParser();
    }

    public async Task<OneOf<ActivityInfo, ActivityServiceError>> GetActivityInfoAsync(
        SteamSession session, IProgressContext ctx)
    {
        var getActivityInfoTask = ctx.AddTask(session, Messages.Statuses.RetrievingActivity);
        var account = session.Account;

        if (account is null)
        {
            getActivityInfoTask.SetResult(session, Messages.Statuses.RetrievingActivityError);
            return new ActivityServiceError
            {
                Message = Messages.Activity.SessionWithNoAccount
            };
        }

        var getXpAndRankTask = ctx.AddTask(session, Messages.Statuses.RetrievingXpAndRank);
        var tasks = new List<Task>
        {
            // TODO: extract to external service (XpService or something)
            _steamHttpClient.GetHtmlAsync($"profiles/{account.Id}/gcpd/730?tab=accountmain", session.AuthCookie),
            _newRankDropService.GetLastNewRankDropAsync(session, ctx)
        };
        await Task.WhenAll(tasks);

        var getDocumentResult =
            ((Task<OneOf<HtmlDocument, SteamHttpClientError>>)tasks[0]).Result;
        var getLastNewRankDropResult =
            ((Task<OneOf<Dto.NewRankDrop, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>)tasks[1]).Result;

        if (getDocumentResult.TryPickT1(out var httpClientError, out var document))
        {
            getXpAndRankTask.SetResult(session, Messages.Statuses.RetrievingXpAndRankError);
            getActivityInfoTask.SetResult(session, Messages.Statuses.RetrievingActivityError);
            return new ActivityServiceError
            {
                Message = httpClientError.Message,
                Exception = httpClientError.Exception
            };
        }

        if (getLastNewRankDropResult.TryPickT3(out var error, out var remainder))
        {
            getXpAndRankTask.SetResult(session, Messages.Statuses.RetrievingXpAndRankError);
            getActivityInfoTask.SetResult(session, Messages.Statuses.RetrievingActivityError);
            return new ActivityServiceError { Message = error.Message, Exception = error.Exception };
        }

        // TODO: redundant if statement; remove
        if (remainder.TryPickT0(out var lastNewRankDrop, out _) || remainder.IsT1 || remainder.IsT2)
        {
            var additionalMessage = lastNewRankDrop is null ? (
                remainder.IsT1 ? remainder.AsT1.Message :
                remainder.IsT2 ? Messages.Activity.NoNewRankDropInfo : null) : null;

            lastNewRankDrop?.BindExternal(remainder.IsT2 ? true : null,
                remainder.IsT1 ? remainder.AsT1.LastEntryDateTime : null);

            var parseActivityResult = _activityInfoParser.ParseActivityInfoFromHtml(document);
            if (parseActivityResult.TryPickT1(out var parserError, out var xpData))
            {
                getXpAndRankTask.SetResult(session, Messages.Statuses.RetrievingXpAndRankError);
                getActivityInfoTask.SetResult(session, Messages.Statuses.RetrievingActivityError);
                return new ActivityServiceError
                {
                    Message = string.Format(Messages.Activity.ActivityParserError, parserError.Message),
                    Exception = parserError.Exception
                };
            }

            getXpAndRankTask.SetResult(session, Messages.Statuses.RetrievingXpAndRankOk);
            getActivityInfoTask.SetResult(session, Messages.Statuses.RetrievingActivityOk);
            return new ActivityInfo
            {
                Account = account,
                AdditionalMessage = additionalMessage,
                XpData = xpData,
                LastNewRankDrop = lastNewRankDrop ?? new Dto.NewRankDrop(null, [])
            };
        }

        getActivityInfoTask.SetResult(session, Messages.Statuses.RetrievingActivityError);
        return new ActivityServiceError { Message = string.Format(Messages.Common.ImpossibleMethodCase, nameof(GetActivityInfoAsync)) };
    }
}

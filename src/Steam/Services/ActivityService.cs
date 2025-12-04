using HtmlAgilityPack;
using OneOf;
using Serilog;
using XpGetter.Dto;
using XpGetter.Errors;
using XpGetter.Results;
using XpGetter.Steam.Http.Clients;
using XpGetter.Steam.Http.Responses.Parsers;
using XpGetter.Steam.Services.NewRankDropServices;
using XpGetter.Utils.Progress;

namespace XpGetter.Steam.Services;

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
            _steamHttpClient.GetHtmlAsync($"profiles/{account.Id}/gcpd/730?tab=accountmain", new AuthCookie(account)),
            _newRankDropService.GetLastNewRankDropAsync(session, ctx)
        };
        await Task.WhenAll(tasks);

        var getDocumentResult =
            ((Task<OneOf<HtmlDocument, SteamHttpClientError>>)tasks[0]).Result;
        var getLastNewRankDropResult =
            ((Task<OneOf<NewRankDrop, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>)tasks[1]).Result;

        if (getDocumentResult.TryPickT1(out var httpClientError, out var document))
        {
            getXpAndRankTask.SetResult(session, Messages.Statuses.RetrievingXpAndRankError);
            getActivityInfoTask.SetResult(session, Messages.Statuses.RetrievingActivityError);
            return new ActivityServiceError
            {
                Message = string.Format(Messages.Activity.HttpError, httpClientError.Message),
                Exception = httpClientError.Exception
            };
        }

        if (getLastNewRankDropResult.TryPickT3(out var error, out var remainder))
        {
            getXpAndRankTask.SetResult(session, Messages.Statuses.RetrievingXpAndRankError);
            getActivityInfoTask.SetResult(session, Messages.Statuses.RetrievingActivityError);
            return new ActivityServiceError { Message = error.Message, Exception = error.Exception };
        }

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
                LastNewRankDrop = lastNewRankDrop ?? new NewRankDrop()
            };
        }

        getActivityInfoTask.SetResult(session, Messages.Statuses.RetrievingActivityError);
        return new ActivityServiceError { Message = string.Format(Messages.Common.ImpossibleMethodCase, nameof(GetActivityInfoAsync)) };
    }
}

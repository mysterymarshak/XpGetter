using HtmlAgilityPack;
using OneOf;
using Serilog;
using XpGetter.Dto;
using XpGetter.Errors;
using XpGetter.Results;
using XpGetter.Steam.Http.Clients;
using XpGetter.Steam.Http.Responses;
using XpGetter.Steam.Http.Responses.Parsers;

namespace XpGetter.Steam.Services;

public interface IActivityService
{
    Task<OneOf<ActivityInfo, ActivityServiceError>> GetActivityInfoAsync(SteamSession session);
}

public class ActivityService : IActivityService
{
    private readonly ISteamHttpClient _steamHttpClient;
    private readonly ILogger _logger;
    private readonly NewRankDropParser _newRankDropParser;
    private readonly ActivityInfoParser _activityInfoParser;

    public ActivityService(ISteamHttpClient steamHttpClient, ILogger logger)
    {
        _steamHttpClient = steamHttpClient;
        _logger = logger;
        _newRankDropParser = new NewRankDropParser(logger);
        _activityInfoParser = new ActivityInfoParser();
    }

    public async Task<OneOf<ActivityInfo, ActivityServiceError>> GetActivityInfoAsync(SteamSession session)
    {
        var account = session.Account;
        var tasks = new List<Task>
        {
            _steamHttpClient.GetHtmlAsync($"/profiles/{account.Id}/gcpd/730?tab=accountmain", GetAuthCookie(account)),
            GetLastNewRankDropAsync(account)
        };
        await Task.WhenAll(tasks);

        var getDocumentResult =
            ((Task<OneOf<HtmlDocument, ActivityServiceError>>)tasks[0]).Result;
        var getLastNewRankDropResult =
            ((Task<OneOf<NewRankDrop, TooLongHistory, NoDropHistoryFound, ActivityServiceError>>)tasks[1]).Result;

        if (getDocumentResult.TryPickT1(out var error, out var document))
        {
            return error;
        }

        if (getLastNewRankDropResult.TryPickT3(out error, out var remainder))
        {
            return error;
        }

        if (remainder.TryPickT0(out var lastNewRankDrop, out _) || remainder.IsT1 || remainder.IsT2)
        {
            var additionalMessage = lastNewRankDrop is null ? (
                remainder.IsT1 ? remainder.AsT1.Message :
                remainder.IsT2 ? "No new rank drop info were found. Are you new in cs2?" : null) : null;

            lastNewRankDrop?.BindExternal(remainder.IsT2 ? true : null,
                remainder.IsT1 ? remainder.AsT1.LastEntryDateTime : null);

            var parseActivityResult = _activityInfoParser.ParseActivityInfoFromHtml(document);
            if (parseActivityResult.TryPickT1(out var parserError, out var xpData))
            {
                return new ActivityServiceError
                {
                    Message = $"Activity parser error: {parserError}",
                    Exception = parserError.Exception
                };
            }

            return new ActivityInfo
            {
                Account = account,
                AdditionalMessage = additionalMessage,
                XpData = xpData,
                LastNewRankDrop = lastNewRankDrop ?? new NewRankDrop()
            };
        }

        return new ActivityServiceError { Message = $"Impossible case. {nameof(GetActivityInfoAsync)}" };
    }

    private async Task<OneOf<NewRankDrop, TooLongHistory, NoDropHistoryFound, ActivityServiceError>>
        GetLastNewRankDropAsync(AccountDto account, NoResultsOnPage? previousPageResult = null)
    {
        if (previousPageResult is { Page: > Constants.MaxInventoryHistoryPagesToLoad })
        {
            return new TooLongHistory(previousPageResult.LastEntryDateTime, previousPageResult.TotalItemsParsed);
        }

        var loadInventoryHistoryResult = await LoadInventoryHistoryAsync(account);
        if (loadInventoryHistoryResult.TryPickT1(out var error, out var result))
        {
            return error;
        }

        var parseNewRankDropResult =
            _newRankDropParser.TryParseNext(result.Deserialized);

        if (parseNewRankDropResult.TryPickT0(out var newRankDrop, out _))
        {
            return newRankDrop;
        }

        if (parseNewRankDropResult.TryPickT1(out var noResultsOnPage, out _))
        {
            return await GetLastNewRankDropAsync(account, noResultsOnPage);
        }

        if (parseNewRankDropResult.TryPickT2(out var mispagedDrop, out _))
        {
            if (mispagedDrop.Cursor is null)
            {
                _logger.Warning("Cannot retrieve mispaged drop if cursor is null.");
                return new NewRankDrop(mispagedDrop.DateTime, [mispagedDrop.FirstItem]);
            }

            loadInventoryHistoryResult = await LoadInventoryHistoryAsync(account, mispagedDrop.Cursor);
            if (loadInventoryHistoryResult.TryPickT1(out error, out result))
            {
                return error;
            }

            var parseMispagedDropResult =
                _newRankDropParser.TryParseMispagedDrop(result.Deserialized);
            if (parseMispagedDropResult.TryPickT1(out error, out var secondItem))
            {
                return error;
            }

            if (secondItem is not null)
            {
                return new NewRankDrop(mispagedDrop.DateTime, [mispagedDrop.FirstItem, secondItem]);
            }

            return new NewRankDrop(mispagedDrop.DateTime, [mispagedDrop.FirstItem]);
        }

        if (parseNewRankDropResult.TryPickT3(out _, out _))
        {
            return new NoDropHistoryFound();
        }

        if (parseNewRankDropResult.TryPickT4(out var parserError, out _))
        {
            return new ActivityServiceError
            {
                Message = $"New rank drop parser error: {parserError.Message}",
                Exception = parserError.Exception
            };
        }

        return new ActivityServiceError { Message = $"Impossible case. {nameof(GetLastNewRankDropAsync)}()" };
    }

    private async Task<OneOf<(InventoryHistoryResponse Deserialized, string Raw), ActivityServiceError>>
        LoadInventoryHistoryAsync(AccountDto account, CursorInfo? cursor = null)
    {
        var queryString =
            $"l=english&ajax=1&cursor[time]={cursor?.Timestamp ?? 0}&cursor[time_frac]={cursor?.TimeFrac ?? 0}&cursor[s]={cursor?.CursorId ?? "0"}";
        var getJsonResult = await _steamHttpClient.GetJsonAsync<InventoryHistoryResponse>(
            $"/profiles/{account.Id}/inventoryhistory?{queryString}",
            GetAuthCookie(account));
        if (getJsonResult.TryPickT1(out var error, out var result))
        {
            return error;
        }

        if (!result.Deserialized.Success)
        {
            return new ActivityServiceError
            {
                Message = $"Success: false in {nameof(LoadInventoryHistoryAsync)}(). Raw response: {result.Raw}"
            };
        }

        return result;
    }

    private string GetAuthCookie(AccountDto account) => $"steamLoginSecure={account.Id}||{account.AccessToken}";
}

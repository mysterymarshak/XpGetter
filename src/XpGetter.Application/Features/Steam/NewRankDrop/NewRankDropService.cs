using OneOf;
using Serilog;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;
using XpGetter.Application.Features.Steam.Http.Clients;
using XpGetter.Application.Features.Steam.Http.Responses;
using XpGetter.Application.Features.Steam.Http.Responses.Parsers;
using XpGetter.Application.Results;
using XpGetter.Application.Utils.Progress;

namespace XpGetter.Application.Features.Steam.NewRankDrop;

public class NewRankDropService : INewRankDropService
{
    private readonly ISteamHttpClient _steamHttpClient;
    private readonly ILogger _logger;

    public NewRankDropService(ISteamHttpClient steamHttpClient, ILogger logger)
    {
        _steamHttpClient = steamHttpClient;
        _logger = logger;
    }

    public async Task<OneOf<Dto.NewRankDrop, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>
        GetLastNewRankDropAsync(SteamSession session, IProgressContext ctx)
    {
        var task = ctx.AddTask(session, Messages.Statuses.RetrievingLastNewRankDrop);
        var parser = new NewRankDropParser(_logger);
        var result = await GetNewRankDropsInternalAsync(session, null, task,
            () => task.Description(session, Messages.Statuses.RetrievingLastNewRankDropRateLimit), parser);

        result.Switch(
            newRankDrops =>
            {
                // 0 items (no new rank drop) is handled by other cases
                // so First should never fail as well as items is never null and count may be only 1 or 2
                var status = newRankDrops.First().Items.Count > 1
                    ? Messages.Statuses.NewRankDropOk
                    : Messages.Statuses.NewRankDropGotOnlyOne;
                task.SetResult(session, status);
            },
            tooLongHistory => task.SetResult(session, Messages.Statuses.TooLongHistory),
            noDropHistory => task.SetResult(session, Messages.Statuses.NewRankDropNotFound),
            error => task.SetResult(session, Messages.Statuses.NewRankDropError));

        return result.Match<OneOf<Dto.NewRankDrop, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>(
            newRankDrops => newRankDrops.First(),
            tooLongHistory => tooLongHistory,
            noDropHistory => noDropHistory,
            error => error);
    }

    public async Task<OneOf<IReadOnlyList<Dto.NewRankDrop>, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>
        GetNewRankDropsAsync(SteamSession session, DateTimeOffset limit, IProgressContext ctx)
    {
        var daysLimit = (DateTimeOffset.UtcNow - limit).Days;
        var task = ctx.AddTask(session,
            string.Format(Messages.Statuses.RetrievingNewRankDrops, daysLimit));
        var parser = new NewRankDropParser(_logger, parseOnlyLastDrop: false);
        var result = await GetNewRankDropsInternalAsync(session, limit, task,
            () => task.Description(session, Messages.Statuses.RetrievingNewRankDropsRateLimit), parser);

        result.Switch(
            newRankDrops => task.SetResult(session, Messages.Statuses.NewRankDropsOk),
            tooLongHistory => task.SetResult(session, Messages.Statuses.TooBigGapInHistory),
            noDropHistory => task.SetResult(session, Messages.Statuses.NewRankDropsNotFound),
            error => task.SetResult(session, Messages.Statuses.NewRankDropsError));

        return result;
    }

    private async Task<OneOf<IReadOnlyList<Dto.NewRankDrop>, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>
        GetNewRankDropsInternalAsync(SteamSession session, DateTimeOffset? limit, IProgressTask task,
            Action onRateLimit, NewRankDropParser parser)
    {
        var result = new List<Dto.NewRankDrop>();
        MispagedDrop? mispagedDrop = null;
        var loadedPagesWithNoResults = 0;

        while (true)
        {
            var loadInventoryHistoryResult = await LoadInventoryHistoryAsync(session, onRateLimit, parser.Cursor);
            if (loadInventoryHistoryResult.TryPickT1(out var error, out var inventoryHistory))
            {
                return error;
            }

            if (mispagedDrop is not null)
            {
                var parseMispagedDropResult = parser.TryParseMispagedDrop(inventoryHistory.Deserialized);
                if (parseMispagedDropResult.TryPickT0(out var secondItem, out var parserError))
                {
                    var items = new List<CsgoItem>(2) { mispagedDrop.FirstItem };
                    if (secondItem is not null)
                    {
                        items.Add(secondItem);
                    }

                    var mispagedNewRankDrop = new Dto.NewRankDrop(mispagedDrop.DateTime, items);
                    if (mispagedNewRankDrop.LastDateTime > limit)
                    {
                        result.Add(mispagedNewRankDrop);
                    }

                    mispagedDrop = null;
                }
                else
                {
                    return ConvertParserErrorToCurrentServiceError(parserError);
                }
            }

            var parseNewRankDropResult = parser.TryParseNext(inventoryHistory.Deserialized);

            if (parseNewRankDropResult.TryPickT0(out var newRankDrops, out var remainder)
                || parseNewRankDropResult.TryPickT2(out mispagedDrop, out _))
            {
                loadedPagesWithNoResults = 0;

                var newRankDropsConcated = (newRankDrops ?? []).Concat(mispagedDrop?.OthersOnThisPage ?? []);

                foreach (var newRankDrop in newRankDropsConcated)
                {
                    if (newRankDrop.LastDateTime < limit)
                    {
                        return result;
                    }

                    result.Add(newRankDrop);
                }

                if (parser.ParseOnlyLastDrop)
                {
                    return result;
                }

                continue;
            }

            if (remainder.TryPickT0(out _, out var remainder1))
            {
                loadedPagesWithNoResults++;

                if (loadedPagesWithNoResults > Constants.MaxInventoryHistoryPagesToLoad)
                {
                    return new TooLongHistory(parser.LastEntryDateTime!.Value, parser.ParsedItems);
                }

                task.Description(session, Messages.Statuses.NewRankDropNoResultsOnPage);
                continue;
            }

            if (remainder1.TryPickT0(out mispagedDrop, out var remainder2))
            {
                if (parser.Cursor is null)
                {
                    // probably impossible but who knows
                    _logger.Error(Messages.Activity.NoCursorLog);
                    return new NewRankDropServiceError { Message = Messages.Activity.NoCursor };
                }

                task.Description(session, Messages.Statuses.NewRankDropMispaged);
                continue;
            }

            if (remainder2.TryPickT0(out _, out var parserError1))
            {
                return new NoDropHistoryFound();
            }

            return ConvertParserErrorToCurrentServiceError(parserError1);
        }
    }

    private async Task<OneOf<(InventoryHistoryResponse Deserialized, string Raw), NewRankDropServiceError>>
        LoadInventoryHistoryAsync(SteamSession session, Action onRateLimit, CursorInfo? cursor = null)
    {
        var queryString =
            $"l=english&ajax=1&app[]=730&cursor[time]={cursor?.Timestamp ?? 0}&cursor[time_frac]={cursor?.TimeFrac ?? 0}&cursor[s]={cursor?.CursorId ?? "0"}";
        var getJsonResult = await _steamHttpClient.GetJsonAsync<InventoryHistoryResponse>(
            $"profiles/{session.Account!.Id}/inventoryhistory?{queryString}",
            session.AuthCookie,
            onRateLimit);
        if (getJsonResult.TryPickT1(out var error, out var result))
        {
            return new NewRankDropServiceError
            {
                Message = error.Message,
                Exception = error.Exception
            };
        }

        if (!result.Deserialized.Success)
        {
            _logger.Error(Messages.Activity.NotSuccessfulResultInLoadInventoryHistoryLogger, result.Raw);

            return new NewRankDropServiceError
            {
                Message = Messages.Activity.NotSuccessfulResultInLoadInventoryHistory
            };
        }

        return result;
    }

    private NewRankDropServiceError ConvertParserErrorToCurrentServiceError(NewRankDropParserError parserError) =>
        new NewRankDropServiceError
        {
            Message = string.Format(Messages.Activity.ActivityParserError, parserError.Message),
            Exception = parserError.Exception
        };
}

using XpGetter.Dto;
using XpGetter.Errors;
using XpGetter.Results;
using XpGetter.Steam.Http.Responses;
using XpGetter.Utils.Progress;
using OneOf;
using Serilog;
using XpGetter.Steam.Http.Clients;
using XpGetter.Steam.Http.Responses.Parsers;

namespace XpGetter.Steam.Services.NewRankDropServices;

public class NewRankDropService : INewRankDropService
{
    private readonly NewRankDropParser _parser;
    private readonly ISteamHttpClient _steamHttpClient;
    private readonly ILogger _logger;

    public NewRankDropService(ISteamHttpClient steamHttpClient, ILogger logger)
    {
        _parser = new NewRankDropParser(logger);
        _steamHttpClient = steamHttpClient;
        _logger = logger;
    }

    public async Task<OneOf<NewRankDrop, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>
        GetLastNewRankDropAsync(SteamSession session, IProgressContext ctx)
    {
        var account = session.Account!;
        var task = ctx.AddTask(account, Messages.Statuses.RetrievingLastNewRankDrop);
        return await GetLastNewRankDropInternalAsync(account, task, null);
    }

    private async Task<OneOf<NewRankDrop, TooLongHistory, NoDropHistoryFound, NewRankDropServiceError>>
        GetLastNewRankDropInternalAsync(AccountDto account, IProgressTask task, NoResultsOnPage? previousPageResult)
    {
        if (previousPageResult is { Page: > Constants.MaxInventoryHistoryPagesToLoad })
        {
            task.SetResult(account, Messages.Statuses.TooLongHistoryStatus);
            return new TooLongHistory(previousPageResult.LastEntryDateTime, previousPageResult.TotalItemsParsed);
        }

        var loadInventoryHistoryResult = await LoadInventoryHistoryAsync(account, previousPageResult?.Cursor);
        if (loadInventoryHistoryResult.TryPickT1(out var error, out var result))
        {
            task.SetResult(account, Messages.Statuses.NewRankDropError);
            return error;
        }

        var parseNewRankDropResult = _parser.TryParseNext(result.Deserialized);

        if (parseNewRankDropResult.TryPickT0(out var newRankDrop, out _))
        {
            task.SetResult(account, Messages.Statuses.NewRankDropOk);
            return newRankDrop;
        }

        if (parseNewRankDropResult.TryPickT1(out var noResultsOnPage, out _))
        {
            task.SetResult(account, Messages.Statuses.NewRankDropNoResultsOnPage);
            return await GetLastNewRankDropInternalAsync(account, task, noResultsOnPage);
        }

        if (parseNewRankDropResult.TryPickT2(out var mispagedDrop, out _))
        {
            if (mispagedDrop.Cursor is null)
            {
                task.SetResult(account, Messages.Statuses.NewRankDropGotOnlyOne);
                _logger.Warning(Messages.Activity.NullCursorForMispagedDrop);
                return new NewRankDrop(mispagedDrop.DateTime, [mispagedDrop.FirstItem]);
            }

            task.Description(account, Messages.Statuses.NewRankDropMispaged);
            loadInventoryHistoryResult = await LoadInventoryHistoryAsync(account, mispagedDrop.Cursor);
            if (loadInventoryHistoryResult.TryPickT1(out error, out result))
            {
                task.SetResult(account, Messages.Statuses.NewRankDropError);
                return error;
            }

            var parseMispagedDropResult =
                _parser.TryParseMispagedDrop(result.Deserialized);
            if (parseMispagedDropResult.TryPickT1(out var parserError, out var secondItem))
            {
                task.SetResult(account, Messages.Statuses.NewRankDropError);
                return new NewRankDropServiceError
                {
                    Message = string.Format(Messages.Activity.ActivityParserError, parserError.Message),
                    Exception = parserError.Exception
                };
            }

            if (secondItem is not null)
            {
                task.SetResult(account, Messages.Statuses.NewRankDropOk);
                return new NewRankDrop(mispagedDrop.DateTime, [mispagedDrop.FirstItem, secondItem]);
            }

            task.SetResult(account, Messages.Statuses.NewRankDropGotOnlyOne);
            return new NewRankDrop(mispagedDrop.DateTime, [mispagedDrop.FirstItem]);
        }

        if (parseNewRankDropResult.TryPickT3(out _, out _))
        {
            task.SetResult(account, Messages.Statuses.NewRankDropNotFound);
            return new NoDropHistoryFound();
        }

        // A local variable named 'parserError' cannot be declared in this scope because it would give a different meaning to 'parserError', which is already used in a parent or current scope to denote something else
        // wtf lol
        if (parseNewRankDropResult.TryPickT4(out var parserError1, out _))
        {
            task.SetResult(account, Messages.Statuses.NewRankDropError);
            return new NewRankDropServiceError
            {
                Message = string.Format(Messages.Activity.ActivityParserError, parserError1.Message),
                Exception = parserError1.Exception
            };
        }

        task.SetResult(account, Messages.Statuses.NewRankDropError);
        return new NewRankDropServiceError { Message = string.Format(Messages.Common.ImpossibleMethodCase, nameof(GetLastNewRankDropAsync)) };
    }

    private async Task<OneOf<(InventoryHistoryResponse Deserialized, string Raw), NewRankDropServiceError>>
        LoadInventoryHistoryAsync(AccountDto account, CursorInfo? cursor = null)
    {
        var queryString =
            $"l=english&ajax=1&app[]=730&cursor[time]={cursor?.Timestamp ?? 0}&cursor[time_frac]={cursor?.TimeFrac ?? 0}&cursor[s]={cursor?.CursorId ?? "0"}";
        var getJsonResult = await _steamHttpClient.GetJsonAsync<InventoryHistoryResponse>(
            $"profiles/{account.Id}/inventoryhistory?{queryString}",
            new AuthCookie(account));
        if (getJsonResult.TryPickT1(out var error, out var result))
        {
            return new NewRankDropServiceError
            {
                Message = string.Format(Messages.Activity.HttpError, error.Message),
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
}

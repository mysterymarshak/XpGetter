using OneOf;
using Serilog;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;
using XpGetter.Application.Extensions;
using XpGetter.Application.Features.Steam.Http;
using XpGetter.Application.Features.Steam.Http.Parsers;
using XpGetter.Application.Features.Steam.Http.Responses;
using XpGetter.Application.Utils.Progress;

namespace XpGetter.Application.Features.Activity;

public interface IMatchmakingService
{
    Task<OneOf<MatchmakingData, MatchmakingServiceError>> GetMatchmakingDataAsync(SteamSession session, IProgressContext ctx);
}

public class MatchmakingService : IMatchmakingService
{
    private readonly ISteamHttpClient _steamHttpClient;
    private readonly ILogger _logger;
    private readonly ActivityInfoParser _parser;

    public MatchmakingService(ISteamHttpClient steamHttpClient, ILogger logger)
    {
        _steamHttpClient = steamHttpClient;
        _logger = logger;
        _parser = new ActivityInfoParser();
    }

    public async Task<OneOf<MatchmakingData, MatchmakingServiceError>> GetMatchmakingDataAsync(
        SteamSession session, IProgressContext ctx)
    {
        var getXpDataTask = ctx.AddTask(session, Messages.Statuses.RetrievingMatchmakingData);
        var getCooldownDataTask = ctx.AddTask(session, Messages.Statuses.RetrievingCooldownData);

        var tasks = new List<Task>
        {
            GetXpDataAsync(session),
            GetCooldownDataAsync(session)
        };
        await Task.WhenAll(tasks);

        var getXpDataResult = ((Task<OneOf<XpData, MatchmakingServiceError>>)tasks[0]).Result;
        var cooldownData = ((Task<CooldownData?>)tasks[1]).Result;

        getCooldownDataTask.SetResult(session, cooldownData is null
                                      ? Messages.Statuses.RetrievingCooldownDataError
                                      : Messages.Statuses.RetrievingCooldownDataOk);

        getXpDataResult.Switch(
            _ => getXpDataTask.SetResult(session, cooldownData is null
                                         ? Messages.Statuses.RetrievingMatchmakingDataWarning
                                         : Messages.Statuses.RetrievingMatchmakingDataOk),
            _ => getXpDataTask.SetResult(session, Messages.Statuses.RetrievingMatchmakingDataError));

        if (getXpDataResult.TryPickT1(out var error, out var xpData))
        {
            return error;
        }

        return new MatchmakingData(xpData, cooldownData);
    }

    private async Task<OneOf<XpData, MatchmakingServiceError>> GetXpDataAsync(SteamSession session)
    {
        return await ParsePersonalDataTabAsync<XpData>(
            session,
            "accountmain",
            response => _parser.ParseActivityInfoFromResponse(response));
    }

    private async Task<CooldownData?> GetCooldownDataAsync(SteamSession session)
    {
        var result = await ParsePersonalDataTabAsync<CooldownData?>(
            session,
            "matchmaking",
            response => _parser.ParseCooldownInfoFromResponse(response));

        return result.Match<CooldownData?>(
            data => data,
            error =>
            {
                _logger.LogError(error);
                return null;
            });
    }

    private async Task<OneOf<T, MatchmakingServiceError>> ParsePersonalDataTabAsync<T>(
        SteamSession session, string tabName, Func<ActivityInfoResponse, OneOf<T, ActivityInfoParserError>> parserFunc)
    {
        var account = session.Account!;
        var result =
            await _steamHttpClient.GetJsonAsync<ActivityInfoResponse>(
                $"profiles/{account.Id}/gcpd/730?tab={tabName}&ajax=1&l=english",
                session.AuthCookie);

        if (result.TryPickT1(out var httpClientError, out var response))
        {
            return new MatchmakingServiceError
            {
                Message = httpClientError.Message,
                Exception = httpClientError.Exception
            };
        }

        var parseActivityResult = parserFunc.Invoke(response.Deserialized);
        if (parseActivityResult.TryPickT1(out var parserError, out var data))
        {
            return new MatchmakingServiceError
            {
                Message = string.Format(Messages.Activity.ActivityParserError, parserError.Message),
                Exception = parserError.Exception
            };
        }

        return data;
    }
}

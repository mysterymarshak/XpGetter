using OneOf;
using Serilog;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;
using XpGetter.Application.Features.Steam.NewRankDrop;
using XpGetter.Application.Utils.Progress;

namespace XpGetter.Application.Features.Statistics;

public interface IStatisticsService
{
    Task<OneOf<StatisticsDto, NewRankDropServiceError>> GetStatisticsAsync(SteamSession session,
        TimeSpan timeSpan, IProgressContext ctx);
}

public class StatisticsService : IStatisticsService
{
    private readonly INewRankDropService _newRankDropService;
    private readonly ILogger _logger;

    public StatisticsService(INewRankDropService newRankDropService, ILogger logger)
    {
        _newRankDropService = newRankDropService;
        _logger = logger;
    }

    public async Task<OneOf<StatisticsDto, NewRankDropServiceError>> GetStatisticsAsync(SteamSession session,
        TimeSpan timeSpan, IProgressContext ctx)
    {
        var result = await _newRankDropService.GetNewRankDropsAsync(session, DateTimeOffset.UtcNow - timeSpan, ctx);
        var loweredResult = result.Match(
            OneOf<IEnumerable<NewRankDrop>, NewRankDropServiceError>.FromT0,
            tooLongHistory => OneOf<IEnumerable<NewRankDrop>, NewRankDropServiceError>.FromT0(Enumerable.Empty<NewRankDrop>()),
            noDropHistoryFound => OneOf<IEnumerable<NewRankDrop>, NewRankDropServiceError>.FromT0(Enumerable.Empty<NewRankDrop>()),
            error => error);

        if (loweredResult.TryPickT0(out var newRankDrops, out var error))
        {
            return new StatisticsDto(newRankDrops) { Session = session, TimeSpan = timeSpan };
        }

        return error;
    }
}

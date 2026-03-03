using Autofac;
using Spectre.Console;
using XpGetter.Application;
using XpGetter.Application.Dto;
using XpGetter.Application.Features.Activity;
using XpGetter.Cli.Extensions;
using XpGetter.Cli.Progress;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

public class RetrieveStatisticsState : AuthenticatedState
{
    private readonly TimeSpan _timeSpan;
    private readonly IStatisticsService _statisticsService;

    public RetrieveStatisticsState(TimeSpan timeSpan, IStatisticsService statisticsService,
                                   StateContext context) : base(context)
    {
        _timeSpan = timeSpan;
        _statisticsService = statisticsService;
    }

    protected override async ValueTask<StateExecutionResult> OnAuthenticated(List<SteamSession> sessions)
    {
        var statisticsResult = await AnsiConsole
            .CreateProgressContext(async ansiConsoleCtx =>
            {
                var ctx = new AnsiConsoleProgressContextWrapper(ansiConsoleCtx);

                var tasks = sessions.Select(x => _statisticsService.GetStatisticsAsync(x, _timeSpan, ctx));
                var results = await Task.WhenAll(tasks);

                ErrorExecutionResult? errorResult = null;
                foreach (var result in results)
                {
                    if (result.TryPickT1(out var error, out _))
                    {
                        var errorDelegate = () => error.DumpToConsole(Messages.Statistics.GetStatisticsError);
                        errorResult = errorResult.CombineOrCreate(new ErrorExecutionResult(errorDelegate));
                    }
                }

                var statistics = results
                    .Where(x => x.IsT0)
                    .Select(x => x.AsT0)
                    .ToList();

                return new StatisticsExecutionResult
                {
                    Statistics = statistics,
                    Error = errorResult
                };
            });


        if (statisticsResult is { Statistics: var statistics } && statistics.Count > 0)
        {
            await GoTo<PrintStatisticsState>(
                new NamedParameter("statistics", statistics));
        }

        return statisticsResult;
    }
}

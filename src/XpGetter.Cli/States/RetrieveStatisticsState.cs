using Spectre.Console;
using XpGetter.Application;
using XpGetter.Application.Dto;
using XpGetter.Application.Features.Configuration;
using XpGetter.Application.Features.Steam;
using XpGetter.Application.Utils.Progress;
using XpGetter.Cli.States.Results;
using OneOf;
using XpGetter.Cli.Extensions;
using Serilog;
using XpGetter.Application.Features.Statistics;
using Autofac;
using XpGetter.Cli.Progress;
using XpGetter.Application.Extensions;
using SteamKit2;
using XpGetter.Application.Errors;

namespace XpGetter.Cli.States;

public class RetrieveStatisticsState : BaseState
{
    private readonly AppConfigurationDto _configuration;
    private readonly List<SteamSession> _sessions;
    private readonly TimeSpan _timeSpan;
    private readonly IStatisticsService _statisticsService;
    private readonly IProgressContext _ctx;
    private readonly ILogger _logger;

    public RetrieveStatisticsState(AppConfigurationDto configuration, List<SteamSession> sessions, TimeSpan timeSpan,
                           IStatisticsService statisticsService, StateContext context,
                           IProgressContext ctx, ILogger logger) : base(context)
    {
        _configuration = configuration;
        _sessions = sessions;
        _timeSpan = timeSpan;
        _statisticsService = statisticsService;
        _ctx = ctx;
        _logger = logger;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        var tasks = _sessions.Select(x => _statisticsService.GetStatisticsAsync(x, _timeSpan, _ctx));
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

        return new StatisticsExecutionResult { Statistics = statistics, Error = errorResult };
    }
}

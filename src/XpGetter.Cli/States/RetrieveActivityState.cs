using Autofac;
using Spectre.Console;
using XpGetter.Application;
using XpGetter.Application.Dto;
using XpGetter.Application.Features.Activity;
using XpGetter.Cli.Extensions;
using XpGetter.Cli.Progress;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

public class RetrieveActivityState : AuthenticatedState
{
    private readonly IActivityService _activityService;

    public RetrieveActivityState(IActivityService activityService,
                                 StateContext context) : base(context)
    {
        _activityService = activityService;
    }

    protected override async ValueTask<StateExecutionResult> OnAuthenticated(List<SteamSession> sessions)
    {
        var retrieveResult = await AnsiConsole
            .CreateProgressContext(async ansiConsoleCtx =>
            {
                var ctx = new AnsiConsoleProgressContextWrapper(ansiConsoleCtx);

                var tasks = sessions.Select(x => _activityService.GetActivityInfoAsync(x, ctx));
                var results = await Task.WhenAll(tasks);

                ErrorExecutionResult? errorResult = null;
                foreach (var result in results)
                {
                    if (result.TryPickT1(out var error, out _))
                    {
                        var errorDelegate = () => error.DumpToConsole(Messages.Activity.GetActivityError);
                        errorResult = errorResult.CombineOrCreate(new ErrorExecutionResult(errorDelegate));
                    }
                }

                var successResults = results
                    .Where(x => x.IsT0)
                    .Select(x => x.AsT0)
                    .ToList();

                return new RetrieveActivityExecutionResult
                {
                    ActivityInfos = successResults,
                    Error = errorResult
                };
            });

        if (retrieveResult is { ActivityInfos: var activityInfos } && activityInfos.Count > 0)
        {
            await GoTo<PrintActivityState>(
                new NamedParameter("activityInfos", activityInfos));
        }

        return retrieveResult;
    }
}

using XpGetter.Application;
using XpGetter.Application.Dto;
using XpGetter.Application.Features.Steam;
using XpGetter.Application.Utils.Progress;
using XpGetter.Cli.Extensions;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

public class RetrieveActivityState : BaseState
{
    private readonly IProgressContext _ctx;
    private readonly List<SteamSession> _sessions;
    private readonly IActivityService _activityService;

    public RetrieveActivityState(IProgressContext ctx, List<SteamSession> sessions,
        IActivityService activityService, StateContext context) : base(context)
    {
        _ctx = ctx;
        _sessions = sessions;
        _activityService = activityService;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        var tasks = _sessions.Select(x => _activityService.GetActivityInfoAsync(x, _ctx));
        var results = await Task.WhenAll(tasks);

        ErrorExecutionResult? errorResult = null;
        foreach (var result in results)
        {
            if (result.TryPickT1(out var error, out _))
            {
                var errorDelegate = () => error.DumpToConsole(Messages.Activity.GetActivityError);
                if (errorResult is null)
                {
                    errorResult = new ErrorExecutionResult(errorDelegate);
                }
                else
                {
                    errorResult.Combine(new ErrorExecutionResult(errorDelegate));
                }
            }
        }

        var successResults = results
            .Where(x => x.IsT0)
            .Select(x => x.AsT0)
            .ToList();

        return new RetrieveActivityStateResult { ActivityInfos = successResults, Error = errorResult };
    }
}

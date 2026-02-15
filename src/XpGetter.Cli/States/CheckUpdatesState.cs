using Spectre.Console;
using XpGetter.Application;
using XpGetter.Application.Features.Versioning;
using XpGetter.Cli.Extensions;
using XpGetter.Cli.Progress;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

public class CheckUpdatesState : BaseState
{
    private readonly IVersioningService _versioningService;

    public CheckUpdatesState(IVersioningService versioningService, StateContext context) : base(context)
    {
        _versioningService = versioningService;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        var version = await AnsiConsole.CreateProgressContext(async ansiConsoleCtx =>
        {
            var ctx = new AnsiConsoleProgressContextWrapper(ansiConsoleCtx);

            var task = ctx.AddTask(Messages.Statuses.RetrievingLatestVersion);
            var version = await _versioningService.GetLatestVersionAsync();

            task.SetResult(Messages.Common.Dummy);
            return version;
        }, autoClear: true);

        var currentVersion = new Version(Constants.Version);
        var message = (version, currentVersion) switch
        {
            (null, _) => Messages.Version.Error,
            (_, _) when version > currentVersion => string.Format(Messages.Version.Update, version),
            (_, _) when version < currentVersion => Messages.Version.Newer,
            _ => Messages.Version.NoUpdates
        };

        AnsiConsole.MarkupLine(message);

        return SuccessExecutionResult.WithoutAccountsPrint();
    }
}

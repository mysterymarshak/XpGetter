using Spectre.Console;
using XpGetter.Application;
using XpGetter.Application.Features.Versions;
using XpGetter.Cli.Extensions;
using XpGetter.Cli.Progress;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

public class CheckUpdatesState : BaseState
{
    private readonly IVersionService _versionService;

    public CheckUpdatesState(IVersionService versionService, StateContext context) : base(context)
    {
        _versionService = versionService;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        var version = await AnsiConsole.CreateProgressContext(async ansiConsoleCtx =>
        {
            var ctx = new AnsiConsoleProgressContextWrapper(ansiConsoleCtx);

            var task = ctx.AddTask(Messages.Statuses.RetrievingLatestVersion);
            var version = await _versionService.GetLatestVersionAsync();

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

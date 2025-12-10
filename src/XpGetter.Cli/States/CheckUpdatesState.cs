using Autofac;
using Spectre.Console;
using XpGetter.Application;
using XpGetter.Application.Dto;
using XpGetter.Application.Features.Versions;
using XpGetter.Cli.Progress;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

public class CheckUpdatesState : BaseState
{
    private readonly IVersionService _versionService;
    private readonly AppConfigurationDto _configuration;

    public CheckUpdatesState(IVersionService versionService, AppConfigurationDto configuration, StateContext context)
        : base(context)
    {
        _versionService = versionService;
        _configuration = configuration;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        var version = await AnsiConsole
            .Progress()
            .AutoRefresh(true)
            .AutoClear(true)
            .HideCompleted(false)
            .Columns(new TaskDescriptionColumn { Alignment = Justify.Left }, new SpinnerColumn(Spinner.Known.Flip),
                new ElapsedTimeColumn())
            .StartAsync(async ansiConsoleCtx =>
            {
                var ctx = new AnsiConsoleProgressContextWrapper(ansiConsoleCtx);

                var task = ctx.AddTask(Messages.Statuses.RetrievingLatestVersion);
                var version = await _versionService.GetLatestVersionAsync();

                task.SetResult(Messages.Common.Dummy);
                return version;
            });

        var currentVersion = new Version(Constants.Version);
        var message = (version, currentVersion) switch
        {
            (null, _) => Messages.Version.Error,
            (_, _) when version > currentVersion => string.Format(Messages.Version.Update, version),
            (_, _) when version < currentVersion => Messages.Version.Newer,
            _ => Messages.Version.NoUpdates
        };

        AnsiConsole.MarkupLine(message);

        return await GoTo<HelloState>(
            new NamedParameter("configuration", _configuration),
            new NamedParameter("skipHelloMessage", true));
    }
}

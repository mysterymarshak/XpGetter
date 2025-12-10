using Autofac;
using Spectre.Console;
using XpGetter.Application;
using XpGetter.Application.Dto;
using XpGetter.Cli.Progress;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

public class StartState : BaseState
{
    private readonly AppConfigurationDto _configuration;

    public StartState(AppConfigurationDto configuration, StateContext context) : base(context)
    {
        _configuration = configuration;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        if (!_configuration.Accounts.Any())
        {
            AnsiConsole.MarkupLine(Messages.Start.NoAccounts);
            return await GoTo<AddAccountState>();
        }

        var pipelineResult = await AnsiConsole
            .Progress()
            .AutoRefresh(true)
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(new TaskDescriptionColumn { Alignment = Justify.Left }, new SpinnerColumn(Spinner.Known.Flip), new ElapsedTimeColumn())
            .StartAsync(async ansiConsoleCtx =>
            {
                var ctx = new AnsiConsoleProgressContextWrapper(ansiConsoleCtx);
                var authenticationStateResult = (AuthenticationExecutionResult)await GoTo<AuthenticaionState>(
                    new NamedParameter("configuration", _configuration),
                    new NamedParameter("ctx", ctx));
                if (authenticationStateResult.Error is PanicExecutionResult)
                {
                    return authenticationStateResult.Error;
                }

                var authenticatedSessions = authenticationStateResult.AuthenticatedSessions;
                if (!authenticatedSessions.Any())
                {
                    if (!_configuration.Accounts.Any())
                    {
                        AnsiConsole.MarkupLine(Messages.Authentication.NoSavedAccounts);
                        return await GoTo<AddAccountState>();
                    }

                    authenticationStateResult.Error?.DumpError();
                    return new PanicExecutionResult();
                }

                var retrieveActivityStateResult = (RetrieveActivityStateResult)await GoTo<RetrieveActivityState>(
                    new NamedParameter("configuration", _configuration),
                    new NamedParameter("sessions", authenticatedSessions),
                    new NamedParameter("ctx", ctx));

                authenticationStateResult.Error?.DumpError();
                retrieveActivityStateResult.Error?.DumpError();

                if (retrieveActivityStateResult.ActivityInfos.Any())
                {
                    return new SuccessExecutionResult { ActivityInfos = retrieveActivityStateResult.ActivityInfos };
                }

                return new ExitExecutionResult();
            });

        if (pipelineResult is SuccessExecutionResult successExecutionResult)
        {
            return await GoTo<PrintActivityState>(
                new NamedParameter("configuration", _configuration),
                new NamedParameter("activityInfos", successExecutionResult.ActivityInfos));
        }

        return pipelineResult;
    }
}
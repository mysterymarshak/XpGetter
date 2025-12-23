using Autofac;
using Spectre.Console;
using XpGetter.Application;
using XpGetter.Application.Dto;
using XpGetter.Cli.Extensions;
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

        var authenticationResult = await AnsiConsole
            .CreateProgressContext(async ansiConsoleCtx =>
            {
                var ctx = new AnsiConsoleProgressContextWrapper(ansiConsoleCtx);
                var authenticationExecutionResult = (AuthenticationExecutionResult)await GoTo<AuthenticaionState>(
                    new NamedParameter("configuration", _configuration),
                    new NamedParameter("ctx", ctx));
                if (authenticationExecutionResult.Error is PanicExecutionResult)
                {
                    return authenticationExecutionResult.Error;
                }

                var authenticatedSessions = authenticationExecutionResult.AuthenticatedSessions;
                if (authenticatedSessions.Count == 0)
                {
                    if (!_configuration.Accounts.Any())
                    {
                        AnsiConsole.MarkupLine(Messages.Authentication.NoSavedAccounts);
                        return await GoTo<AddAccountState>();
                    }

                    authenticationExecutionResult.Error?.DumpError();
                    return new PanicExecutionResult();
                }

                return authenticationExecutionResult;
            });

        if (authenticationResult is not AuthenticationExecutionResult authenticationExecutionResult)
        {
            return authenticationResult;
        }

        var sessions = authenticationExecutionResult.AuthenticatedSessions;
        var familyViewProtected = sessions.Where(x => x.ParentalSettings?.is_enabled == true);
        var familyViewPassedSessions = sessions;
        PassFamilyViewExecutionResult? passFamilyViewResult = null;
        if (familyViewProtected.Any())
        {
            passFamilyViewResult = (PassFamilyViewExecutionResult)await GoTo<PassFamilyViewState>(
                new NamedParameter("sessions", sessions));

            familyViewPassedSessions = passFamilyViewResult.PassedSessions;
        }

        RetrieveActivityExecutionResult? retrieveActivityStateResult = null;
        if (familyViewPassedSessions.Count > 0)
        {
            retrieveActivityStateResult = await AnsiConsole
                .CreateProgressContext(async ansiConsoleCtx =>
                {
                    var ctx = new AnsiConsoleProgressContextWrapper(ansiConsoleCtx);
                    return (RetrieveActivityExecutionResult)await GoTo<RetrieveActivityState>(
                        new NamedParameter("configuration", _configuration),
                        new NamedParameter("sessions", familyViewPassedSessions),
                        new NamedParameter("ctx", ctx));
                });

            if (retrieveActivityStateResult.ActivityInfos.Any())
            {
                await GoTo<PrintActivityState>(
                    new NamedParameter("configuration", _configuration),
                    new NamedParameter("activityInfos", retrieveActivityStateResult.ActivityInfos));
            }
        }
        else
        {
            AnsiConsole.MarkupLine(Messages.Common.NothingToDo);
        }

        if (authenticationExecutionResult.Error is not null)
        {
            return authenticationExecutionResult.Error;
        }

        if (passFamilyViewResult?.Error is not null)
        {
            return passFamilyViewResult.Error;
        }

        if (retrieveActivityStateResult?.Error is not null)
        {
            return retrieveActivityStateResult.Error;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(Messages.Activity.AnyKeyToReturn);
        Console.ReadKey();

        AnsiConsole.MarkupLine(Messages.Common.Gap);
        return await GoTo<HelloState>(
            new NamedParameter("configuration", _configuration),
            new NamedParameter("skipHelloMessage", true));
    }
}

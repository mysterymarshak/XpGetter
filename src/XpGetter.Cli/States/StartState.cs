using Autofac;
using Spectre.Console;
using XpGetter.Application;
using XpGetter.Application.Dto;
using XpGetter.Application.Features.Configuration;
using XpGetter.Cli.Extensions;
using XpGetter.Cli.Progress;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

public class StartState : BaseState
{
    private readonly IConfigurationService _configurationService;
    private readonly Func<List<SteamSession>, Task<StateExecutionResult>> _postAuthenticationDelegate;

    public StartState(IConfigurationService configurationService,
                      Func<List<SteamSession>, Task<StateExecutionResult>> postAuthenticationDelegate,
                      StateContext context) : base(context)
    {
         _configurationService = configurationService;
        _postAuthenticationDelegate = postAuthenticationDelegate;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        if (!Configuration.Accounts.Any())
        {
            AnsiConsole.MarkupLine(Messages.Start.NoAccounts);
            return await GoTo<AddAccountState>();
        }

        var authenticationResult = await AnsiConsole
            .CreateProgressContext<StateExecutionResult>(async ansiConsoleCtx =>
            {
                var ctx = new AnsiConsoleProgressContextWrapper(ansiConsoleCtx);
                var authenticationExecutionResult = (AuthenticationExecutionResult)await GoTo<AuthenticaionState>(
                    new NamedParameter("ctx", ctx));

                if (authenticationExecutionResult.Error is PanicExecutionResult)
                {
                    return authenticationExecutionResult.Error;
                }

                var authenticatedSessions = authenticationExecutionResult.AuthenticatedSessions;
                return authenticationExecutionResult;
            });

        if (authenticationResult is not AuthenticationExecutionResult authenticationExecutionResult)
        {
            return authenticationResult;
        }

        if (!authenticationExecutionResult.AuthenticatedSessions.Any())
        {
            authenticationExecutionResult.Error?.DumpError();

            AnsiConsole.MarkupLine(Messages.Authentication.LogInAgain);
            return await GoTo<AddAccountState>();
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

        StateExecutionResult? stateExecutionResult = null;
        if (familyViewPassedSessions.Count > 0)
        {
            AnsiConsole.MarkupLine(Messages.Start.SuccessAuthorization);
            stateExecutionResult = await _postAuthenticationDelegate(familyViewPassedSessions);
        }
        else
        {
            AnsiConsole.MarkupLine(Messages.Common.NothingToDo);
        }

        var aggregatedError = authenticationExecutionResult.Error
            .CombineOrCreate(passFamilyViewResult?.Error)
            .CombineOrCreate(stateExecutionResult?.Error);

        if (aggregatedError is not null)
        {
            return aggregatedError;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(Messages.Activity.AnyKeyToReturn);
        Console.ReadKey();
        AnsiConsole.MarkupLine(Messages.Common.Gap);

        return new SuccessExecutionResult();
    }
}

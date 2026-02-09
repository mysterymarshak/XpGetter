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
    private readonly AppConfigurationDto _configuration;
    private readonly IConfigurationService _configurationService;
    private readonly Func<List<SteamSession>, Task<StateExecutionResult>> _postAuthenticationDelegate;

    public StartState(AppConfigurationDto configuration, IConfigurationService configurationService,
                      Func<List<SteamSession>, Task<StateExecutionResult>> postAuthenticationDelegate,
                      StateContext context) : base(context)
    {
        _configuration = configuration;
        _configurationService = configurationService;
        _postAuthenticationDelegate = postAuthenticationDelegate;
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
                    // all sessions were expired
                    if (!_configuration.Accounts.Any())
                    {
                        AnsiConsole.MarkupLine(Messages.Authentication.LogInAgain);
                        // TODO: must be outside of context
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
                new NamedParameter("configuration", _configuration),
                new NamedParameter("sessions", sessions));

            familyViewPassedSessions = passFamilyViewResult.PassedSessions;
        }

        // TODO: i should really rewrite this shit

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

        if (authenticationExecutionResult.Error is not null)
        {
            return authenticationExecutionResult.Error;
        }

        if (passFamilyViewResult?.Error is not null)
        {
            return passFamilyViewResult.Error;
        }

        if (stateExecutionResult?.Error is not null)
        {
            return stateExecutionResult.Error;
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

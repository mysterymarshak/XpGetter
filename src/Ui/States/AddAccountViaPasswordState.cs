using Autofac;
using Spectre.Console;
using XpGetter.Configuration;
using XpGetter.Dto;
using XpGetter.Extensions;
using XpGetter.Results.StateExecutionResults;
using XpGetter.Steam.Services;

namespace XpGetter.Ui.States;

public class AddAccountViaPasswordState : BaseState
{
    private readonly ISessionService _sessionService;
    private readonly IAuthenticationService _authenticationService;
    private readonly IConfigurationService _configurationService;

    public AddAccountViaPasswordState(ISessionService sessionService, IAuthenticationService authenticationService,
        IConfigurationService configurationService, StateContext context) : base(context)
    {
        _sessionService = sessionService;
        _authenticationService = authenticationService;
        _configurationService = configurationService;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        AnsiConsole.MarkupLine(Messages.AddAccount.AddingAccountViaPassword);

        var username = await AnsiConsole.PromptAsync(new TextPrompt<string>("Type the username:"));
        var password = await AnsiConsole.PromptAsync(new TextPrompt<string>("Type the password:").Secret(null));

        var createSessionResult = await _sessionService.GetOrCreateSessionAsync(username);
        if (createSessionResult.TryPickT1(out var sessionError, out var session))
        {
            sessionError.DumpToConsole(Messages.Session.FailedSessionCreation, sessionError.ClientName);
            return await GoTo<AddAccountState>();
        }

        var authenticationResult =
            await _authenticationService.AuthenticateByUsernameAndPasswordAsync(session, username, password);
        if (authenticationResult.TryPickT3(out var authError, out _))
        {
            authError.DumpToConsole(Messages.Authentication.AuthenticationError, session.Name);
            return await GoTo<AddAccountState>();
        }

        if (authenticationResult.IsT1)
        {
            AnsiConsole.MarkupLine(Messages.Authentication.InvalidPassword);
            return await GoTo<AddAccountState>();
        }

        if (authenticationResult.IsT2)
        {
            AnsiConsole.MarkupLine(Messages.Authentication.Cancelled);
            return await GoTo<AddAccountState>();
        }

        var account = session.Account!;
        var configuration = _configurationService.GetConfiguration();
        var addAccountResult = _configurationService.TryAddAccount(configuration, account);
        if (addAccountResult.IsT1)
        {
            AnsiConsole.MarkupLine(Messages.AddAccount.AccountAlreadyExists);
        }
        else
        {
            _configurationService.WriteConfiguration(configuration);
            AnsiConsole.MarkupLine(Messages.AddAccount.SuccessfullyAdded, account.Username);
        }

        return await GoTo<HelloState>(new NamedParameter("configuration", configuration));
    }
}

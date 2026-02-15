using Spectre.Console;
using XpGetter.Application;
using XpGetter.Application.Features.Configuration;
using XpGetter.Application.Features.Steam;
using XpGetter.Cli.Extensions;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

public class AddAccountViaPasswordState : BaseState
{
    private readonly ISessionService _sessionService;
    private readonly IAuthenticationService _authenticationService;
    private readonly IConfigurationService _configurationService;

    public AddAccountViaPasswordState(ISessionService sessionService,
                                      IAuthenticationService authenticationService,
                                      IConfigurationService configurationService,
                                      StateContext context) : base(context)
    {
        _sessionService = sessionService;
        _authenticationService = authenticationService;
        _configurationService = configurationService;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        AnsiConsole.MarkupLine(Messages.AddAccount.AddingAccountViaPassword);

        var username = await AnsiConsole.PromptAsync(new TextPrompt<string>(Messages.AddAccount.TypeUsername));
        var password = await AnsiConsole.PromptAsync(new TextPrompt<string>(Messages.AddAccount.TypePassword)
                                                     .Secret(null));

        var createSessionResult = await _sessionService.GetOrCreateSessionAsync(username);
        if (createSessionResult.TryPickT1(out var sessionError, out var session))
        {
            sessionError.DumpToConsole(Messages.Session.FailedSessionCreation, sessionError.ClientName);
            return new RetryExecutionResult();
        }

        var authenticationResult =
            await _authenticationService.AuthenticateByUsernameAndPasswordAsync(session, username, password);
        if (authenticationResult.TryPickT3(out var authError, out _))
        {
            authError.DumpToConsole(Messages.Authentication.AuthenticationError, session.GetName());
            return new RetryExecutionResult();
        }

        if (authenticationResult.IsT1)
        {
            AnsiConsole.MarkupLine(Messages.Authentication.InvalidPassword);
            return new RetryExecutionResult();
        }

        if (authenticationResult.IsT2)
        {
            AnsiConsole.MarkupLine(Messages.Authentication.Cancelled);
            return new RetryExecutionResult();
        }

        var account = session.Account!;
        var addAccountResult = _configurationService.TryAddAccount(Configuration, account);
        if (addAccountResult.IsT1)
        {
            AnsiConsole.MarkupLine(Messages.AddAccount.AccountAlreadyExists, account.GetDisplayUsername());
        }
        else
        {
            _configurationService.WriteConfiguration(Configuration);
            AnsiConsole.MarkupLine(Messages.AddAccount.SuccessfullyAdded, account.GetDisplayUsername());
        }

        return new SuccessExecutionResult();
    }
}

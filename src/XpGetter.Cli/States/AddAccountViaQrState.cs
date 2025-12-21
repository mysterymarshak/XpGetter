using Autofac;
using Spectre.Console;
using XpGetter.Application;
using XpGetter.Application.Features.Configuration;
using XpGetter.Application.Features.Steam;
using XpGetter.Cli.Extensions;
using XpGetter.Cli.States.Results;

namespace XpGetter.Cli.States;

public class AddAccountViaQrState : BaseState
{
    private readonly ISessionService _sessionService;
    private readonly IAuthenticationService _authenticationService;
    private readonly IConfigurationService _configurationService;

    public AddAccountViaQrState(ISessionService sessionService, IAuthenticationService authenticationService,
        IConfigurationService configurationService, StateContext context) : base(context)
    {
        _sessionService = sessionService;
        _authenticationService = authenticationService;
        _configurationService = configurationService;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        AnsiConsole.MarkupLine(Messages.AddAccount.AddingAccountViaQr);

        var createSessionResult = await _sessionService.GetOrCreateSessionAsync();
        if (createSessionResult.TryPickT1(out var sessionError, out var session))
        {
            sessionError.DumpToConsole(Messages.Session.FailedSessionCreation, sessionError.ClientName);
            return await GoTo<AddAccountState>();
        }

        var authenticationResult = await _authenticationService.AuthenticateByQrCodeAsync(session);
        if (authenticationResult.TryPickT2(out var authError, out _))
        {
            authError.DumpToConsole(Messages.Authentication.AuthenticationError, session.Name);
            return await GoTo<AddAccountState>();
        }

        if (authenticationResult.IsT1)
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

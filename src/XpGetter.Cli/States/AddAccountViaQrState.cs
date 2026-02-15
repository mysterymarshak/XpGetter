using Serilog;
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
    private readonly ILogger _logger;

    public AddAccountViaQrState(ISessionService sessionService,
                                IAuthenticationService authenticationService,
                                IConfigurationService configurationService,
                                ILogger logger,
                                StateContext context) : base(context)
    {
        _sessionService = sessionService;
        _authenticationService = authenticationService;
        _configurationService = configurationService;
        _logger = logger;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        AnsiConsole.MarkupLine(Messages.AddAccount.AddingAccountViaQr);

        var createSessionResult = await _sessionService.GetOrCreateSessionAsync();
        if (createSessionResult.TryPickT1(out var sessionError, out var session))
        {
            sessionError.DumpToConsole(Messages.Session.FailedSessionCreation, sessionError.ClientName);
            return new RetryExecutionResult();
        }

        var authenticationResult = await _authenticationService.AuthenticateByQrCodeAsync(session);

        if (authenticationResult.TryPickT2(out var steamKitFail, out _))
        {
            AnsiConsole.MarkupLine(Messages.Authentication.SteamKitJobFailed);
            _logger.Error(steamKitFail.Exception, string.Empty);
            return new RetryExecutionResult();
        }

        if (authenticationResult.TryPickT3(out var authError, out _))
        {
            authError.DumpToConsole(Messages.Authentication.AuthenticationError, session.GetName());
            return new RetryExecutionResult();
        }

        if (authenticationResult.IsT1)
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

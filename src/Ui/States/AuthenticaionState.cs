using Autofac;
using OneOf;
using Serilog;
using Spectre.Console;
using XpGetter.Configuration;
using XpGetter.Dto;
using XpGetter.Extensions;
using XpGetter.Results.StateExecutionResults;
using XpGetter.Steam.Services;

namespace XpGetter.Ui.States;

public class AuthenticaionState : BaseState
{
    private readonly AppConfigurationDto _configuration;
    private readonly IConfigurationService _configurationService;
    private readonly ISessionService _sessionService;
    private readonly IAuthenticationService _authenticationService;
    private readonly ILogger _logger;

    public AuthenticaionState(AppConfigurationDto configuration, IConfigurationService configurationService,
        ISessionService sessionService, IAuthenticationService authenticationService,
        StateContext context, ILogger logger) : base(context)
    {
        _configuration = configuration;
        _configurationService = configurationService;
        _sessionService = sessionService;
        _authenticationService = authenticationService;
        _logger = logger;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        var accounts = _configuration.Accounts.ToList();
        var createSessionsResult = await CreateSessions(accounts);
        if (createSessionsResult.TryPickT1(out var error, out var sessions))
        {
            return error;
        }

        var authenticatedSessions = await AuthenticateSessions(sessions, accounts);
        if (authenticatedSessions.Count == 0)
        {
            if (!_configuration.Accounts.Any())
            {
                AnsiConsole.MarkupLine(Messages.Authentication.NoSavedAccounts);
                return await GoTo<AddAccountState>();
            }

            AnsiConsole.MarkupLine(Messages.Common.FatalError);
            return new PanicExecutionResult(Messages.Authentication.UnauthenticatedSessions);
        }

        _logger.Debug(Messages.Authentication.SuccessfullyAuthenticated, authenticatedSessions.Select(x => x.Name));
        return await GoTo<RetrieveActivityState>(
            new NamedParameter("configuration", _configuration), new NamedParameter("sessions", authenticatedSessions));
    }

    private async Task<OneOf<List<SteamSession>, StateExecutionResult>> CreateSessions(List<AccountDto> accounts)
    {
        var createSessionTasks = accounts.Select(x => _sessionService.GetOrCreateSessionAsync(x.Username, x));
        var createSessionResults = await Task.WhenAll(createSessionTasks);
        var createSessionErrors = createSessionResults.Where(x => x.IsT1).ToList();

        foreach (var errorResult in createSessionErrors)
        {
            var error = errorResult.AsT1;
            error.DumpToConsole(Messages.Session.FailedSessionCreation, error.ClientName);
        }

        if (createSessionErrors.Count > 0)
        {
            return new PanicExecutionResult(Messages.Authentication.CannotCreateSteamSession);
        }

        return createSessionResults.Select(x => x.AsT0).ToList();
    }

    private async Task<List<SteamSession>> AuthenticateSessions(List<SteamSession> sessions, List<AccountDto> accounts)
    {
        var authenticateSessionTasks =
            sessions.Select((x, i) => _authenticationService.AuthenticateSessionAsync(x, accounts[i]));
        var authenticateSessionResults = await Task.WhenAll(authenticateSessionTasks);

        foreach (var (i, result) in authenticateSessionResults.Index())
        {
            var account = accounts[i];

            if (result.TryPickT1(out _, out _))
            {
                _configuration.RemoveAccount(account.Id);

                AnsiConsole.MarkupLine(Messages.Authentication.SessionExpired, account.Username);
                _logger.Debug(Messages.Authentication.AccountRemoved, account.Username);
            }
            else if (result.TryPickT2(out var authServiceError, out _))
            {
                authServiceError.DumpToConsole(Messages.Authentication.AuthenticationError, account.Username);
            }
        }

        _configurationService.WriteConfiguration(_configuration);

        return sessions.Where(x => x.IsAuthenticated).ToList();
    }
}

using OneOf;
using Serilog;
using Spectre.Console;
using XpGetter.Configuration;
using XpGetter.Dto;
using XpGetter.Extensions;
using XpGetter.Results.StateExecutionResults;
using XpGetter.Steam.Services;

namespace XpGetter.Cli.States;

public class AuthenticaionState : BaseState
{
    private readonly AppConfigurationDto _configuration;
    private readonly ProgressContext _ctx;
    private readonly IConfigurationService _configurationService;
    private readonly ISessionService _sessionService;
    private readonly IAuthenticationService _authenticationService;
    private readonly ILogger _logger;

    public AuthenticaionState(AppConfigurationDto configuration, ProgressContext ctx,
        IConfigurationService configurationService, ISessionService sessionService,
        IAuthenticationService authenticationService, StateContext context, ILogger logger) : base(context)
    {
        _configuration = configuration;
        _ctx = ctx;
        _configurationService = configurationService;
        _sessionService = sessionService;
        _authenticationService = authenticationService;
        _logger = logger;
    }

    public override async ValueTask<StateExecutionResult> OnExecuted()
    {
        var accounts = _configuration.Accounts.ToList();

        var createAndAuthenticateSessionTasks = accounts.Select(CreateAndAuthenticateSession);
        var createAndAuthenticateSessionResults = await Task.WhenAll(createAndAuthenticateSessionTasks);

        var panicExecutionResult = createAndAuthenticateSessionResults.FirstOrDefault(x => x.IsT2);
        if (panicExecutionResult.TryPickT2(out var panicResult, out _))
        {
            return new AuthenticationExecutionResult { Error = panicResult };
        }

        var combinedError = createAndAuthenticateSessionResults
            .Where(x => x.IsT1)
            .Select(x => x.AsT1)
            .DefaultIfEmpty(null)
            .Aggregate((x, y) => x!.Combine(y!));

        var authenticatedSessions = createAndAuthenticateSessionResults
            .Where(x => x.IsT0)
            .Select(x => x.AsT0)
            .ToList();

        if (authenticatedSessions.Any(x => !x.IsAuthenticated))
        {
            return new AuthenticationExecutionResult { Error = new PanicExecutionResult(Messages.Authentication.UnauthenticatedSessions) };
        }

        _logger.Debug(Messages.Authentication.SuccessfullyAuthenticated, authenticatedSessions.Select(x => x.Name));
        return new AuthenticationExecutionResult { AuthenticatedSessions = authenticatedSessions, Error = combinedError };
    }

    private async Task<OneOf<SteamSession, ErrorExecutionResult, PanicExecutionResult>> CreateAndAuthenticateSession(AccountDto account)
    {
        var authenticateTask = _ctx.AddTask(account, Messages.Statuses.CreatingSession);
        var createSessionResult = await _sessionService.GetOrCreateSessionAsync(account.Username, account);
        if (createSessionResult.TryPickT1(out var error, out var session))
        {
            authenticateTask.SetResult(account, Messages.Statuses.SessionCreationError);
            error.DumpToConsole(Messages.Authentication.CannotCreateSteamSession);
            return new PanicExecutionResult(string.Format(Messages.Session.FailedSessionCreation, error.ClientName));
        }

        ErrorExecutionResult? errorExecutionResult = null;
        authenticateTask.Description(session, Messages.Authentication.Authenticating);
        var authenticationResult = await _authenticationService.AuthenticateSessionAsync(session, account);
        if (authenticationResult.TryPickT1(out _, out _))
        {
            authenticateTask.SetResult(session, Messages.Authentication.SessionExpiredStatus);

            _configuration.RemoveAccount(account.Id);
            _logger.Debug(Messages.Authentication.AccountRemoved, account.Username);

            errorExecutionResult = new ErrorExecutionResult(() => AnsiConsole.MarkupLine(Messages.Authentication.SessionExpired, account.Username));
        }
        else if (authenticationResult.TryPickT2(out var authServiceError, out _))
        {
            authenticateTask.SetResult(session, Messages.Authentication.AuthenticationErrorStatus);
            errorExecutionResult =
                new ErrorExecutionResult(() => authServiceError.DumpToConsole(Messages.Authentication.AuthenticationError, account.Username));
        }

        _configurationService.WriteConfiguration(_configuration);

        if (errorExecutionResult is not null)
        {
            return errorExecutionResult;
        }

        authenticateTask.SetResult(session, Messages.Authentication.Authenticated);
        return session;
    }
}

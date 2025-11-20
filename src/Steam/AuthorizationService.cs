using OneOf;
using OneOf.Types;
using Serilog;
using SteamKit2;
using SteamKit2.Authentication;
using XpGetter.Dto;
using XpGetter.Results;
using XpGetter.Utils;

namespace XpGetter.Steam;

public interface IAuthorizationService
{
    Task<OneOf<Success, Updated, RefreshTokenExpired, SessionServiceError, AuthorizationServiceError>>
        AuthorizeAccountAsync(Account account);

    Task<OneOf<Account, SessionServiceError, AuthorizationServiceError>> AuthorizeByUsernameAndPasswordAsync(
        string username, string password);

    Task<OneOf<Account, SessionServiceError, AuthorizationServiceError>> AuthorizeByQrCodeAsync();
}

public class AuthorizationServiceError
{
    public required string Message { get; init; }
    public Exception? Exception { get; init; }
}

public class AuthorizationService : IAuthorizationService
{
    private readonly ISessionService _sessionService;
    private readonly ILogger _logger;

    public AuthorizationService(ISessionService sessionService, ILogger logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    public async Task<OneOf<Success, Updated, RefreshTokenExpired, SessionServiceError, AuthorizationServiceError>>
        AuthorizeAccountAsync(Account account)
    {
        var createSessionResult = await _sessionService.CreateSessionAsync(account.Username);
        if (createSessionResult.TryPickT1(out var sessionError, out var session))
        {
            return sessionError;
        }

        var tokenExpired = false;
        var updated = true;
        AuthorizationServiceError? authError = null;
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        using var logOnCallback = session.CallbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        using var accountInfoCallback = session.CallbackManager.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);

        try
        {
            _logger.Information("Client '{Username}': Logging in...", account.Username);

            var ensureTokensResult = await EnsureTokensAsync(account, session);
            if (!ensureTokensResult.TryPickT0(out _, out var remainder))
            {
                session.Dispose();
                return remainder.Match(
                    OneOf<Success, Updated, RefreshTokenExpired, SessionServiceError, AuthorizationServiceError>.FromT2,
                    OneOf<Success, Updated, RefreshTokenExpired, SessionServiceError, AuthorizationServiceError>
                        .FromT4);
            }

            session.User.LogOn(new SteamUser.LogOnDetails
            {
                AccessToken = account.RefreshToken,
                Username = account.Username
            });

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await session.CallbackManager.RunWaitCallbackAsync(ct);
                }
                catch (TaskCanceledException)
                {
                }
            }
        }
        catch (Exception exception)
        {
            return new AuthorizationServiceError
            {
                Message = "An error occured while trying to authorize into account via token.",
                Exception = exception
            };
        }
        finally
        {
            session.Dispose();
        }

        void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
                if (callback.Result == EResult.AccessDenied)
                {
                    tokenExpired = true;
                    cts.Cancel();
                    return;
                }

                authError = new AuthorizationServiceError
                {
                    Message = $"Client '{account.Username}': Unable to logon to Steam: {callback.Result} / {callback.ExtendedResult}"
                };

                cts.Cancel();
                return;
            }

            _logger.Information("Client '{Username}': Successfully logged on!", account.Username);
        }

        void OnAccountInfo(SteamUser.AccountInfoCallback callback)
        {
            if (account.PersonalName != callback.PersonaName)
            {
                account.PersonalName = callback.PersonaName;
                updated = true;
            }

            cts.Cancel();
        }

        if (authError is not null)
        {
            return authError;
        }

        if (tokenExpired)
        {
            return new RefreshTokenExpired();
        }

        if (updated)
        {
            return new Updated();
        }

        return new Success();
    }

    public async Task<OneOf<Account, SessionServiceError, AuthorizationServiceError>>
        AuthorizeByUsernameAndPasswordAsync(string username, string password)
    {
        var createSessionResult = await _sessionService.CreateSessionAsync(username);
        if (createSessionResult.TryPickT1(out var sessionError, out var session))
        {
            return sessionError;
        }

        Account? account = null;
        AuthorizationServiceError? authError = null;
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        using var logOnCallback = session.CallbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        using var accountInfoCallback = session.CallbackManager.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);

        try
        {
            _logger.Information("Client '{Username}': Logging in...", username);

            const bool shouldRememberPassword = true;
            var authSession = await session.Client.Authentication.BeginAuthSessionViaCredentialsAsync(
                new AuthSessionDetails
                {
                    Username = username,
                    Password = password,
                    IsPersistentSession = shouldRememberPassword,
                    Authenticator = new UserConsoleAuthenticator()
                });

            var pollResponse = await authSession.PollingWaitForResultAsync(ct);

            account = new Account
            {
                AccessToken = pollResponse.AccessToken,
                RefreshToken = pollResponse.RefreshToken,
                Username = pollResponse.AccountName
            };

            session.User.LogOn(new SteamUser.LogOnDetails
            {
                Username = pollResponse.AccountName,
                AccessToken = pollResponse.RefreshToken,
                ShouldRememberPassword = shouldRememberPassword
            });

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await session.CallbackManager.RunWaitCallbackAsync(ct);
                }
                catch (TaskCanceledException)
                {
                }
            }
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception exception)
        {
            return new AuthorizationServiceError
            {
                Message = "Unexpected error in auth via username/password code.",
                Exception = exception
            };
        }
        finally
        {
            session.Dispose();
        }

        void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
                authError = new AuthorizationServiceError
                {
                    Message = $"Client '{account!.Username}': Unable to logon to Steam: {callback.Result} / {callback.ExtendedResult}"
                };

                cts.Cancel();
                return;
            }

            account!.Id = callback.ClientSteamID!.ConvertToUInt64();

            _logger.Information("Client '{UserName}': Successfully logged on!", username);
        }

        void OnAccountInfo(SteamUser.AccountInfoCallback callback)
        {
            account!.PersonalName = callback.PersonaName;
            cts.Cancel();
        }

        if (authError is not null)
        {
            return authError;
        }

        if (account is null)
        {
            return new AuthorizationServiceError { Message = "For some reason no account is retrieved." };
        }

        return account;
    }

    public async Task<OneOf<Account, SessionServiceError, AuthorizationServiceError>> AuthorizeByQrCodeAsync()
    {
        var createSessionResult = await _sessionService.CreateSessionAsync("<unnamed>");
        if (createSessionResult.TryPickT1(out var sessionError, out var session))
        {
            return sessionError;
        }

        AuthorizationServiceError? authError = null;
        Account? account = null;
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        using var logOnCallback = session.CallbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        using var accountInfoCallback = session.CallbackManager.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);

        try
        {
            var authSession = await session.Client.Authentication.BeginAuthSessionViaQRAsync(new AuthSessionDetails());
            authSession.ChallengeURLChanged = () =>
            {
                // TODO: better refresh
                _logger.Information("Steam has refreshed the qr code");
                DrawChallengeUrl(authSession);
            };

            DrawChallengeUrl(authSession);

            var pollResponse = await authSession.PollingWaitForResultAsync(ct);

            account = new Account
            {
                AccessToken = pollResponse.AccessToken,
                RefreshToken = pollResponse.RefreshToken,
                Username = pollResponse.AccountName
            };

            _logger.Information("Logging in as '{Username}'...", pollResponse.AccountName);

            session.User.LogOn(new SteamUser.LogOnDetails
            {
                Username = pollResponse.AccountName,
                AccessToken = pollResponse.RefreshToken
            });

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await session.CallbackManager.RunWaitCallbackAsync(ct);
                }
                catch (TaskCanceledException)
                {
                }
            }
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception exception)
        {
            return new AuthorizationServiceError
            {
                Message = "Unexpected error in auth via qr code.",
                Exception = exception
            };
        }
        finally
        {
            session.Dispose();
        }

        void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
                authError = new AuthorizationServiceError
                {
                    Message =
                        $"Client '{account!.Username}': Unable to logon to Steam{(callback.Result == EResult.TryAnotherCM ? " (possible timeout)" : string.Empty)}: {callback.Result} / {callback.ExtendedResult}"
                };

                cts.Cancel();
                return;
            }

            account!.Id = callback.ClientSteamID!.ConvertToUInt64();

            _logger.Information("Client '{UserName}': Successfully logged on!", account.Username);
        }

        void OnAccountInfo(SteamUser.AccountInfoCallback callback)
        {
            account!.PersonalName = callback.PersonaName;
            cts.Cancel();
        }

        void DrawChallengeUrl(QrAuthSession authSession)
        {
            var url = authSession.ChallengeURL;
            Console.WriteLine($"URL: {url}");
            QrCode.DrawSmallestAscii(url);
        }

        if (authError is not null)
        {
            return authError;
        }

        if (account is null)
        {
            return new AuthorizationServiceError { Message = "For some reason no account is retrieved." };
        }

        return account;
    }

    private async Task<OneOf<Success, RefreshTokenExpired, AuthorizationServiceError>> EnsureTokensAsync(
        Account account,SteamSession session)
    {
        Error<string> error;

        var refreshToken = account.RefreshToken;
        var getRefreshTokenExpirationDateResult = JwtToken.GetExpirationDate(refreshToken);

        if (getRefreshTokenExpirationDateResult.TryPickT1(out error, out var refreshTokenExpirationDate))
        {
            return new AuthorizationServiceError { Message = error.Value };
        }

        _logger.Debug("Client '{UserName}': Refresh token expiration date: {ExpirationDate}",
            account.Username, refreshTokenExpirationDate.ToLocalTime());

        if (DateTimeOffset.Now > refreshTokenExpirationDate)
        {
            _logger.Debug("Client '{UserName}': Refresh token is expired.", account.Username);
            return new RefreshTokenExpired();
        }

        var accessToken = account.AccessToken;
        var getAccessTokenExpirationDateResult = JwtToken.GetExpirationDate(accessToken);

        if (getAccessTokenExpirationDateResult.TryPickT1(out error, out var accessTokenExpirationDate))
        {
            return new AuthorizationServiceError { Message = error.Value };
        }

        _logger.Debug("Client '{UserName}': Access token expiration date: {ExpirationDate}",
            account.Username, accessTokenExpirationDate.ToLocalTime());

        if (DateTimeOffset.Now > accessTokenExpirationDate)
        {
            _logger.Debug("Client '{UserName}': Access token is expired", account.Username);

            var renewAccessTokenResult = await RenewAccessTokenAsync(account, session);
            if (!renewAccessTokenResult.TryPickT0(out _, out var remainder))
            {
                return remainder.Match(
                    OneOf<Success, RefreshTokenExpired, AuthorizationServiceError>.FromT1,
                    OneOf<Success, RefreshTokenExpired, AuthorizationServiceError>.FromT2);
            }

            _logger.Debug("Client '{UserName}': Access token successfully renewed", account.Username);
        }

        return new Success();
    }

    private async Task<OneOf<Success, RefreshTokenExpired, AuthorizationServiceError>> RenewAccessTokenAsync(Account account, SteamSession session)
    {
        try
        {
            var steamClient = session.Client;
            var steamId = new SteamID(account.Id);
            var newTokens =
                await steamClient.Authentication.GenerateAccessTokenForAppAsync(steamId, account.RefreshToken,
                    allowRenewal: true);

            account.AccessToken = newTokens.AccessToken;

            if (!string.IsNullOrWhiteSpace(newTokens.RefreshToken))
            {
                account.RefreshToken = newTokens.RefreshToken;
            }
        }
        catch (AuthenticationException ex) when (ex.Result == EResult.AccessDenied)
        {
            return new RefreshTokenExpired();
        }
        catch (Exception exception)
        {
            return new AuthorizationServiceError { Message = "An error occured while renewing access token.", Exception = exception };
        }

        return new Success();
    }
}

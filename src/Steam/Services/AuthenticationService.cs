using OneOf;
using OneOf.Types;
using Serilog;
using SteamKit2;
using SteamKit2.Authentication;
using XpGetter.Dto;
using XpGetter.Results;
using XpGetter.Settings.Entities;
using XpGetter.Utils;

namespace XpGetter.Steam.Services;

public interface IAuthenticationService
{
    Task<OneOf<Success, RefreshTokenExpired, AuthenticationServiceError>> AuthenticateSessionAsync(SteamSession session);

    Task<OneOf<Success, AuthenticationServiceError>> AuthenticateByUsernameAndPasswordAsync(
        SteamSession session, string username, string password);

    Task<OneOf<Success, UserCancelledAuthByQr, AuthenticationServiceError>>
        AuthenticateByQrCodeAsync(SteamSession session);
}

public class AuthenticationServiceError
{
    public required string Message { get; init; }
    public Exception? Exception { get; init; }
}

public class AuthenticationService : IAuthenticationService
{
    private readonly ISessionService _sessionService;
    private readonly ILogger _logger;

    public AuthenticationService(ISessionService sessionService, ILogger logger)
    {
        _sessionService = sessionService;
        _logger = logger;
    }

    public async Task<OneOf<Success, RefreshTokenExpired, AuthenticationServiceError>> AuthenticateSessionAsync(SteamSession session)
    {
        var account = session.Account;
        if (account is null)
        {
            return new AuthenticationServiceError { Message = "No account is provided." };
        }

        if (session.IsAuthenticated)
        {
            return new Success();
        }

        var tokenExpired = false;
        AuthenticationServiceError? authError = null;

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        using var logOnCallback = session.CallbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        using var accountInfoCallback = session.CallbackManager.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);

        try
        {
            _logger.Information("Client '{Username}': Logging in...", account.Username);

            var ensureRefreshTokenResult = EnsureRefreshToken(account);
            if (!ensureRefreshTokenResult.TryPickT0(out _, out var remainder))
            {
                return remainder.Match(
                    OneOf<Success, RefreshTokenExpired, AuthenticationServiceError>.FromT1,
                    OneOf<Success, RefreshTokenExpired, AuthenticationServiceError>.FromT2);
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

            // var request = new CUserAccount_GetClientWalletDetails_Request();
            // var steamUnifiedMessages = session.Client.GetHandler<SteamUnifiedMessages>()!;
            // var steamGameCoordinator = session.Client.GetHandler<SteamGameCoordinator>()!;
            // var userAccountService = steamUnifiedMessages.CreateService<UserAccount>();
            // var response = await userAccountService.GetClientWalletDetails(request);

            var ensureAccessTokenResult = await EnsureAccessTokenAsync(account, session);
            if (!ensureAccessTokenResult.IsT0)
            {
                var impossible = OneOf<Success, RefreshTokenExpired, AuthenticationServiceError>.FromT2(
                    new AuthenticationServiceError { Message = "Impossible case" });

                return ensureAccessTokenResult.Match(
                    _ => impossible,
                    OneOf<Success, RefreshTokenExpired, AuthenticationServiceError>.FromT1,
                    OneOf<Success, RefreshTokenExpired, AuthenticationServiceError>.FromT2
                );
            }
        }
        catch (Exception exception)
        {
            return new AuthenticationServiceError
            {
                Message = "An error occured while trying to authenticate via token.",
                Exception = exception
            };
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

                authError = new AuthenticationServiceError
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

        return new Success();
    }

    public async Task<OneOf<Success, AuthenticationServiceError>> AuthenticateByUsernameAndPasswordAsync(
        SteamSession session, string username, string password)
    {
        Account? account = null;
        AuthenticationServiceError? authError = null;

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        using var logOnCallback = session.CallbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        using var accountInfoCallback = session.CallbackManager.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);

        try
        {
            _logger.Information("Client '{Username}': Logging in via password...", username);

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
            return new AuthenticationServiceError
            {
                Message = "Unexpected error in auth via username/password code.",
                Exception = exception
            };
        }

        void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
                authError = new AuthenticationServiceError
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
            return new AuthenticationServiceError { Message = $"Impossible case. {nameof(AuthenticateByUsernameAndPasswordAsync)}()" };
        }

        session.BindAccount(account);
        return new Success();
    }

    public async Task<OneOf<Success, UserCancelledAuthByQr, AuthenticationServiceError>> AuthenticateByQrCodeAsync(SteamSession session)
    {
        Account? account = null;
        AuthenticationServiceError? authError = null;

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        using var logOnCallback = session.CallbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        using var accountInfoCallback = session.CallbackManager.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);

        try
        {
            const bool shouldRememberPassword = true;
            var authSession = await session.Client.Authentication.BeginAuthSessionViaQRAsync(
                new AuthSessionDetails { IsPersistentSession = shouldRememberPassword });
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
        catch (AuthenticationException authenticationException) when (authenticationException.Result == EResult.FileNotFound)
        {
            return new UserCancelledAuthByQr();
        }
        catch (Exception exception)
        {
            return new AuthenticationServiceError
            {
                Message = "Unexpected error in auth via qr code.",
                Exception = exception
            };
        }

        void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
                authError = new AuthenticationServiceError
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
            return new AuthenticationServiceError { Message = $"Impossible case. nameof({AuthenticateByQrCodeAsync})()" };
        }

        session.BindAccount(account);
        return new Success();
    }

    private OneOf<Success, RefreshTokenExpired, AuthenticationServiceError> EnsureRefreshToken(Account account)
    {
        var refreshToken = account.RefreshToken;
        var getRefreshTokenExpirationDateResult = JwtToken.GetExpirationDate(refreshToken);

        if (getRefreshTokenExpirationDateResult.TryPickT1(out var error, out var refreshTokenExpirationDate))
        {
            return new AuthenticationServiceError { Message = error.Value };
        }

        _logger.Debug("Client '{UserName}': Refresh token expiration date: {ExpirationDate}",
            account.Username, refreshTokenExpirationDate.ToLocalTime());

        if (DateTimeOffset.Now > refreshTokenExpirationDate)
        {
            _logger.Debug("Client '{UserName}': Refresh token is expired.", account.Username);
            return new RefreshTokenExpired();
        }

        return new Success();
    }

    private async Task<OneOf<Success, RefreshTokenExpired, AuthenticationServiceError>> EnsureAccessTokenAsync(
        Account account, SteamSession session)
    {
        var accessToken = account.AccessToken;
        var getAccessTokenExpirationDateResult = JwtToken.GetExpirationDate(accessToken);

        if (getAccessTokenExpirationDateResult.TryPickT1(out var error, out var accessTokenExpirationDate))
        {
            return new AuthenticationServiceError { Message = error.Value };
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
                    OneOf<Success, RefreshTokenExpired, AuthenticationServiceError>.FromT1,
                    OneOf<Success, RefreshTokenExpired, AuthenticationServiceError>.FromT2);
            }

            _logger.Debug("Client '{UserName}': Access token has successfully renewed", account.Username);
        }

        return new Success();
    }

    private async Task<OneOf<Updated, RefreshTokenExpired, AuthenticationServiceError>> RenewAccessTokenAsync(Account account, SteamSession session)
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
            return new AuthenticationServiceError { Message = "An error occured while renewing access token.", Exception = exception };
        }

        return new Updated();
    }
}

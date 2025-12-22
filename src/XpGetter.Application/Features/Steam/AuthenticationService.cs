using OneOf;
using OneOf.Types;
using Serilog;
using SteamKit2;
using SteamKit2.Authentication;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;
using XpGetter.Application.Extensions;
using XpGetter.Application.Results;
using XpGetter.Application.Utils;

namespace XpGetter.Application.Features.Steam;

public interface IAuthenticationService
{
    Task<OneOf<Success, RefreshTokenExpired, AuthenticationServiceError>> AuthenticateSessionAsync(
        SteamSession session, AccountDto account);

    Task<OneOf<Success, InvalidPassword, UserCancelledAuth, AuthenticationServiceError>>
        AuthenticateByUsernameAndPasswordAsync(SteamSession session, string username, string password);

    Task<OneOf<Success, UserCancelledAuth, SteamKitJobFailed, AuthenticationServiceError>>
        AuthenticateByQrCodeAsync(SteamSession session);
}

public class AuthenticationService : IAuthenticationService
{
    private readonly IQrCode _qrCode;
    private readonly ILogger _logger;

    public AuthenticationService(IQrCode qrCode, ILogger logger)
    {
        _qrCode = qrCode;
        _logger = logger;
    }

    public async Task<OneOf<Success, RefreshTokenExpired, AuthenticationServiceError>> AuthenticateSessionAsync(
        SteamSession session, AccountDto account)
    {
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
            _logger.Information(Messages.Authentication.LoggingIn.BindSession(session));

            var ensureRefreshTokenResult = EnsureRefreshToken(session, account);
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

            var ensureAccessTokenResult = await EnsureAccessTokenAsync(account, session);
            if (!ensureAccessTokenResult.IsT0)
            {
                var impossible = OneOf<Success, RefreshTokenExpired, AuthenticationServiceError>.FromT2(
                    new AuthenticationServiceError { Message = string.Format(Messages.Common.ImpossibleMethodCase, nameof(AuthenticateSessionAsync)) });

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
                Message = Messages.Authentication.AuthenticateViaTokenException.BindSession(session),
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
                    Message = string
                        .Format(Messages.Authentication.LogOnNotOk, callback.Result, callback.ExtendedResult)
                        .BindSession(session)
                };

                cts.Cancel();
                return;
            }

            _logger.Information(Messages.Authentication.LogOnOk.BindSession(session));
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

        session.BindAccount(account);
        return new Success();
    }

    public async Task<OneOf<Success, InvalidPassword, UserCancelledAuth, AuthenticationServiceError>>
        AuthenticateByUsernameAndPasswordAsync(SteamSession session, string username, string password)
    {
        AccountDto? account = null;
        AuthenticationServiceError? authError = null;

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        using var logOnCallback = session.CallbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        using var accountInfoCallback = session.CallbackManager.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);

        try
        {
            _logger.Information(Messages.Authentication.LoggingInPassword.BindSession(session));

            const bool shouldRememberPassword = true;
            var authSession = await session.Client.Authentication.BeginAuthSessionViaCredentialsAsync(
                new AuthSessionDetails
                {
                    Username = username,
                    Password = password,
                    IsPersistentSession = shouldRememberPassword,
                    Authenticator = new UserConsoleAuthenticator(),
                    DeviceFriendlyName = Constants.ProgramName
                });

            var pollResponse = await authSession.PollingWaitForResultAsync(ct);

            account = new AccountDto
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
        catch (AuthenticationException authenticationException) when (authenticationException.Result == EResult.InvalidPassword)
        {
            return new InvalidPassword();
        }
        catch (AuthenticationException authenticationException) when (authenticationException.Result == EResult.FileNotFound)
        {
            return new UserCancelledAuth();
        }
        catch (Exception exception)
        {
            return new AuthenticationServiceError
            {
                Message = Messages.Authentication.AuthenticateViaPasswordException,
                Exception = exception
            };
        }

        void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
                authError = new AuthenticationServiceError
                {
                    Message = string
                        .Format(Messages.Authentication.LogOnNotOk, callback.Result, callback.ExtendedResult)
                        .BindSession(session)
                };

                cts.Cancel();
                return;
            }

            account!.Id = callback.ClientSteamID!.ConvertToUInt64();

            _logger.Information(Messages.Authentication.LogOnOk.BindSession(session));
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
            return new AuthenticationServiceError { Message = string.Format(Messages.Common.ImpossibleMethodCase, nameof(AuthenticateByUsernameAndPasswordAsync)) };
        }

        session.BindAccount(account);
        return new Success();
    }

    public async Task<OneOf<Success, UserCancelledAuth, SteamKitJobFailed, AuthenticationServiceError>> AuthenticateByQrCodeAsync(
        SteamSession session)
    {
        _qrCode.Reset();

        AccountDto? account = null;
        AuthenticationServiceError? authError = null;

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        using var logOnCallback = session.CallbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        using var accountInfoCallback = session.CallbackManager.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);

        try
        {
            const bool shouldRememberPassword = true;
            var authSession = await session.Client.Authentication.BeginAuthSessionViaQRAsync(
                new AuthSessionDetails
                {
                    IsPersistentSession = shouldRememberPassword,
                    DeviceFriendlyName = Constants.ProgramName
                });
            authSession.ChallengeURLChanged = () =>
            {
                _logger.Information(Messages.Authentication.QrCodeRefreshed);
                _qrCode.Clear();
                DrawChallengeUrl(authSession);
            };

            DrawChallengeUrl(authSession);

            var pollResponse = await authSession.PollingWaitForResultAsync(ct);

            account = new AccountDto
            {
                AccessToken = pollResponse.AccessToken,
                RefreshToken = pollResponse.RefreshToken,
                Username = pollResponse.AccountName
            };

            _logger.Information(Messages.Authentication.LoggingIn.BindSession(session, pollResponse.AccountName));

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
        catch (AuthenticationException authenticationException) when (authenticationException.Result ==
                                                                      EResult.FileNotFound)
        {
            return new UserCancelledAuth();
        }
        catch (AsyncJobFailedException jobFailedException)
        {
            return new SteamKitJobFailed(jobFailedException);
        }
        catch (Exception exception)
        {
            return new AuthenticationServiceError
            {
                Message = Messages.Authentication.AuthenticateViaQrException,
                Exception = exception
            };
        }
        finally
        {
            _qrCode.Clear();
        }

        void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
                if (callback.Result == EResult.TryAnotherCM)
                {
                    authError = new AuthenticationServiceError
                    {
                        Message = string
                            .Format(Messages.Authentication.LogOnNotOkTimeout, callback.Result, callback.ExtendedResult)
                            .BindSession(session, account!.Username)
                    };
                }

                authError = new AuthenticationServiceError
                {
                    Message = string
                        .Format(Messages.Authentication.LogOnNotOk, callback.Result, callback.ExtendedResult)
                        .BindSession(session, account!.Username)
                };

                cts.Cancel();
                return;
            }

            account!.Id = callback.ClientSteamID!.ConvertToUInt64();

            _logger.Information(Messages.Authentication.LogOnOk.BindSession(session, account.Username));
        }

        void OnAccountInfo(SteamUser.AccountInfoCallback callback)
        {
            account!.PersonalName = callback.PersonaName;
            cts.Cancel();
        }

        void DrawChallengeUrl(QrAuthSession authSession)
        {
            var url = authSession.ChallengeURL;
            _qrCode.Draw(Messages.AddAccount.ScanQrCode, url);
        }

        if (authError is not null)
        {
            return authError;
        }

        if (account is null)
        {
            return new AuthenticationServiceError { Message = string.Format(Messages.Common.ImpossibleMethodCase, nameof(AuthenticateByQrCodeAsync)) };
        }

        session.BindAccount(account);
        session.BindName(account.Username);
        return new Success();
    }

    private OneOf<Success, RefreshTokenExpired, AuthenticationServiceError> EnsureRefreshToken(
        SteamSession session, AccountDto account)
    {
        var refreshToken = account.RefreshToken;
        var getRefreshTokenExpirationDateResult = JwtToken.GetExpirationDate(refreshToken);

        if (getRefreshTokenExpirationDateResult.TryPickT1(out var error, out var refreshTokenExpirationDate))
        {
            return new AuthenticationServiceError { Message = error.Value };
        }

        _logger.Debug(string
            .Format(Messages.Authentication.RefreshTokenExpirationDate, refreshTokenExpirationDate.ToLocalTime())
            .BindSession(session, account.Username));

        if (DateTimeOffset.Now > refreshTokenExpirationDate)
        {
            _logger.Debug(Messages.Authentication.RefreshTokenExpired.BindSession(session, account.Username));
            return new RefreshTokenExpired();
        }

        return new Success();
    }

    private async Task<OneOf<Success, RefreshTokenExpired, AuthenticationServiceError>> EnsureAccessTokenAsync(
        AccountDto account, SteamSession session)
    {
        var accessToken = account.AccessToken;
        var getAccessTokenExpirationDateResult = JwtToken.GetExpirationDate(accessToken);

        if (getAccessTokenExpirationDateResult.TryPickT1(out var error, out var accessTokenExpirationDate))
        {
            return new AuthenticationServiceError { Message = error.Value };
        }

        _logger.Debug(string
            .Format(Messages.Authentication.AccessTokenExpirationDate, accessTokenExpirationDate.ToLocalTime())
            .BindSession(session));

        if (DateTimeOffset.Now > accessTokenExpirationDate)
        {
            _logger.Debug(Messages.Authentication.AccessTokenExpired.BindSession(session));

            var renewAccessTokenResult = await RenewAccessTokenAsync(account, session);
            if (!renewAccessTokenResult.TryPickT0(out _, out var remainder))
            {
                return remainder.Match(
                    OneOf<Success, RefreshTokenExpired, AuthenticationServiceError>.FromT1,
                    OneOf<Success, RefreshTokenExpired, AuthenticationServiceError>.FromT2);
            }

            _logger.Debug(Messages.Authentication.AccessTokenRenewed.BindSession(session));
        }

        return new Success();
    }

    private async Task<OneOf<Updated, RefreshTokenExpired, AuthenticationServiceError>> RenewAccessTokenAsync(
        AccountDto account, SteamSession session)
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
            return new AuthenticationServiceError
            {
                Message = Messages.Authentication.AccessRenewingException.BindSession(session),
                Exception = exception
            };
        }

        return new Updated();
    }
}

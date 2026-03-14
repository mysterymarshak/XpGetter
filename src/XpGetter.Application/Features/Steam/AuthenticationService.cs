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
        AuthenticateByQrCodeAsync(IQrCode qrCode, SteamSession session);
}

public class AuthenticationService : IAuthenticationService
{
    private readonly ILogger _logger;

    public AuthenticationService(ILogger logger)
    {
        _logger = logger;
    }

    private async Task<OneOf<SteamUser.LoggedOnCallback, RefreshTokenExpired, AuthenticationServiceError>>
        LogOnAsync(LogOnContext context)
    {
        var account = context.Account;
        var session = context.Session;
        var fetcher = session.MessagesFetcher;
        var loggedOnAwaiter = fetcher.CreateAndRegisterAwaiter<SteamUser.LoggedOnCallback>();
        var accountInfoAwaiter = fetcher.CreateAndRegisterAwaiter<SteamUser.AccountInfoCallback>();
        var walletInfoAwaiter = fetcher.CreateAndRegisterAwaiter<SteamUser.WalletInfoCallback>();
        var vacStatusAwaiter = fetcher.CreateAndRegisterAwaiter<SteamApps.VACStatusCallback>();
        var playingStateAwaiter = fetcher.CreateAndRegisterAwaiter<SteamUser.PlayingSessionStateCallback>();

        try
        {
            _logger.Information(Messages.Authentication.LoggingIn.BindSession(session));

            session.User.LogOn(new SteamUser.LogOnDetails
            {
                AccessToken = account.RefreshToken,
                Username = account.Username,
                ShouldRememberPassword = true
            });

            var logOnCallback = await fetcher.WaitForAsync(loggedOnAwaiter);
            if (logOnCallback.Result != EResult.OK)
            {
                if (logOnCallback.Result == EResult.AccessDenied)
                {
                    return new RefreshTokenExpired();
                }

                if (logOnCallback.Result == EResult.TryAnotherCM)
                {
                    return new AuthenticationServiceError
                    {
                        Message = string
                            .Format(
                                Messages.Authentication.LogOnNotOkProbablyTimeout,
                                logOnCallback.Result,
                                logOnCallback.ExtendedResult)
                            .BindSession(session, account.Username, logging: false)
                    };
                }

                return new AuthenticationServiceError
                {
                    Message = string
                        .Format(Messages.Authentication.LogOnNotOk, logOnCallback.Result, logOnCallback.ExtendedResult)
                        .BindSession(session, logging: false)
                };
            }

            if (context.ShouldBindAccountId)
            {
                account.Id = logOnCallback.ClientSteamID!.ConvertToUInt64();
            }

            _logger.Information(Messages.Authentication.LogOnOk.BindSession(session));

            if (context.RenewAccessToken)
            {
                var ensureAccessTokenResult = await EnsureAccessTokenAsync(account, session);
                if (!ensureAccessTokenResult.IsT0)
                {
                    var impossible =
                        OneOf<SteamUser.LoggedOnCallback, RefreshTokenExpired, AuthenticationServiceError>
                            .FromT2(new AuthenticationServiceError
                            {
                                Message = string.Format(Messages.Common.ImpossibleMethodCase, nameof(AuthenticateSessionAsync))
                            });

                    return ensureAccessTokenResult.Match(
                        _ => impossible,
                        OneOf<SteamUser.LoggedOnCallback, RefreshTokenExpired, AuthenticationServiceError>.FromT1,
                        OneOf<SteamUser.LoggedOnCallback, RefreshTokenExpired, AuthenticationServiceError>.FromT2
                    );
                }
            }

            var accountInfoResult = await fetcher.WaitForAsync(accountInfoAwaiter);
            account.PersonalName = accountInfoResult.PersonaName;

            var walletInfoResult = await fetcher.WaitForAsync(walletInfoAwaiter);
            var walletInfo = new WalletInfo(walletInfoResult.HasWallet, walletInfoResult.Currency);

            var vacStatusResult = await fetcher.WaitForAsync(vacStatusAwaiter);
            var vacBanned = vacStatusResult.BannedApps.Contains(Constants.Cs2AppId);

            var playingStateResult = await fetcher.WaitForAsync(playingStateAwaiter);

            session.BindAccount(account);
            session.BindParentalSettings(logOnCallback.ParentalSettings);
            session.BindWalletInfo(walletInfo);
            session.BindVacStatus(vacBanned);
            session.OnPlayingSessionState(playingStateResult);

            fetcher.HookCallback<SteamUser.PlayingSessionStateCallback>(session.OnPlayingSessionState);
            return logOnCallback;
        }
        catch (TaskCanceledException)
        {
            return new AuthenticationServiceError
            {
                Message = Messages.Common.TimeoutException
            };
        }
        catch (Exception exception)
        {
            return new AuthenticationServiceError
            {
                Message = Messages.Authentication.AuthenticateViaTokenException,
                Exception = exception
            };
        }
    }

    public async Task<OneOf<Success, RefreshTokenExpired, AuthenticationServiceError>>
        AuthenticateSessionAsync(SteamSession session, AccountDto account)
    {
        if (session.IsAuthenticated)
        {
            return new Success();
        }

        var ensureRefreshTokenResult = EnsureRefreshToken(session, account);
        if (!ensureRefreshTokenResult.TryPickT0(out _, out var remainder))
        {
            return remainder.Match(
                OneOf<Success, RefreshTokenExpired, AuthenticationServiceError>.FromT1,
                OneOf<Success, RefreshTokenExpired, AuthenticationServiceError>.FromT2);
        }

        var context = LogOnContext.ForAccessTokenAuthentication(session, account);
        var logOnResult = await LogOnAsync(context);
        return logOnResult.Match<OneOf<Success, RefreshTokenExpired, AuthenticationServiceError>>(
            _ => new Success(),
            expired => expired,
            error => error);
    }

    public async Task<OneOf<Success, InvalidPassword, UserCancelledAuth, AuthenticationServiceError>>
        AuthenticateByUsernameAndPasswordAsync(SteamSession session, string username, string password)
    {
        AccountDto? account;

        try
        {
            _logger.Information(Messages.Authentication.BeginAuthSessionPassword.BindSession(session));

            var authSession = await session.Client.Authentication.BeginAuthSessionViaCredentialsAsync(
                new AuthSessionDetails
                {
                    Username = username,
                    Password = password,
                    IsPersistentSession = true,
                    Authenticator = new UserConsoleAuthenticator(),
                    DeviceFriendlyName = Constants.ProgramName
                });

            var pollResponse = await authSession.PollingWaitForResultAsync();

            account = new AccountDto
            {
                AccessToken = pollResponse.AccessToken,
                RefreshToken = pollResponse.RefreshToken,
                Username = pollResponse.AccountName
            };
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

        var context = LogOnContext.ForPasswordAuthentication(session, account);
        var logOnResult = await LogOnAsync(context);
        return logOnResult.Match<OneOf<Success, InvalidPassword, UserCancelledAuth, AuthenticationServiceError>>(
            _ => new Success(),
            _ => new InvalidPassword(),
            error => error);
    }

    public async Task<OneOf<Success, UserCancelledAuth, SteamKitJobFailed, AuthenticationServiceError>>
        AuthenticateByQrCodeAsync(IQrCode qrCode, SteamSession session)
    {
        AccountDto? account;

        try
        {
            _logger.Information(Messages.Authentication.BeginAuthSessionQrCode.BindSession(session));

            var authSession = await session.Client.Authentication.BeginAuthSessionViaQRAsync(
                new AuthSessionDetails
                {
                    IsPersistentSession = true,
                    DeviceFriendlyName = Constants.ProgramName
                });
            authSession.ChallengeURLChanged = () =>
            {
                _logger.Information(Messages.Authentication.QrCodeRefreshed);
                qrCode.Clear();
                DrawChallengeUrl(qrCode, authSession);
            };

            DrawChallengeUrl(qrCode, authSession);

            var pollResponse = await authSession.PollingWaitForResultAsync();

            account = new AccountDto
            {
                AccessToken = pollResponse.AccessToken,
                RefreshToken = pollResponse.RefreshToken,
                Username = pollResponse.AccountName
            };

            session.BindName(account.Username);
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
            qrCode.Clear();
        }

        var context = LogOnContext.ForQrCodeAuthentication(session, account);
        var logOnResult = await LogOnAsync(context);
        return logOnResult.Match<OneOf<Success, UserCancelledAuth, SteamKitJobFailed, AuthenticationServiceError>>(
            _ => new Success(),
            _ => new UserCancelledAuth(),
            error => error);

        static void DrawChallengeUrl(IQrCode qrCode, QrAuthSession authSession)
        {
            var url = authSession.ChallengeURL;
            qrCode.Draw(Messages.AddAccount.ScanQrCode, url);
        }
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

        _logger.Debug(Messages.Authentication.RefreshTokenExpirationDate.BindSession(session, account.Username),
            refreshTokenExpirationDate.ToLocalTime());

        if (DateTimeOffset.Now > refreshTokenExpirationDate)
        {
            _logger.Debug(Messages.Authentication.RefreshTokenExpired.BindSession(session, account.Username));
            return new RefreshTokenExpired();
        }

        return new Success();
    }

    private async ValueTask<OneOf<Success, RefreshTokenExpired, AuthenticationServiceError>> EnsureAccessTokenAsync(
        AccountDto account, SteamSession session)
    {
        var accessToken = account.AccessToken;
        var getAccessTokenExpirationDateResult = JwtToken.GetExpirationDate(accessToken);

        if (getAccessTokenExpirationDateResult.TryPickT1(out var error, out var accessTokenExpirationDate))
        {
            return new AuthenticationServiceError { Message = error.Value };
        }

        _logger.Debug(Messages.Authentication.AccessTokenExpirationDate.BindSession(session),
            accessTokenExpirationDate.ToLocalTime());

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
                Message = Messages.Authentication.AccessRenewingException.BindSession(
                    session, logging: false),
                Exception = exception
            };
        }

        return new Updated();
    }

    private class LogOnContext
    {
        public SteamSession Session { get; }
        public bool RenewAccessToken { get; }
        public bool ShouldBindAccountId { get; }
        public AccountDto Account { get; }

        private LogOnContext(SteamSession session, bool renewAccessToken, bool shouldBindAccountId, AccountDto account)
        {
            Session = session;
            RenewAccessToken = renewAccessToken;
            ShouldBindAccountId = shouldBindAccountId;
            Account = account;
        }

        public static LogOnContext ForAccessTokenAuthentication(SteamSession session, AccountDto account)
        {
            return new LogOnContext(session, true, false, account);
        }

        public static LogOnContext ForPasswordAuthentication(SteamSession session, AccountDto account)
        {
            return new LogOnContext(session, false, true, account);
        }

        public static LogOnContext ForQrCodeAuthentication(SteamSession session, AccountDto account)
        {
            return new LogOnContext(session, false, true, account);
        }
    }
}

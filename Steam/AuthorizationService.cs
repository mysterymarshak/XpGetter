using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using QRCoder;
using Serilog;
using SteamKit2;
using SteamKit2.Authentication;
using XpGetter.Utils;

namespace XpGetter.Steam;

public interface IAuthorizationService
{
    Task<OneOf<Account, AuthorizationServiceError>> AuthorizeByUsernameAndPasswordAsync(string username,
        string password);

    Task<OneOf<Account, AuthorizationServiceError>> AuthorizeByQrCodeAsync();
}

public class AuthorizationServiceError
{
    public required string Message { get; init; }
    public Exception? Exception { get; init; }
}

public class AuthorizationService : IAuthorizationService
{
    private readonly ILogger<AuthorizationService> _logger;

    public AuthorizationService(ILogger<AuthorizationService> logger)
    {
        _logger = logger;
    }

    public async Task<OneOf<Account, AuthorizationServiceError>> AuthorizeByUsernameAndPasswordAsync(string username,
        string password)
    {
        AuthorizationServiceError? error = null;
        var account = new Account();
        
        var steamClient = new SteamClient();
        var manager = new CallbackManager(steamClient);
        var steamUser = steamClient.GetHandler<SteamUser>()!;

        var cts = new CancellationTokenSource();
        var ct = cts.Token;
        manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);

        try
        {
            _logger.LogInformation("Connecting to Steam...");
            steamClient.Connect();
        }
        catch (Exception exception)
        {
            return new AuthorizationServiceError { Message = "An error occured while connecting to steam.", Exception = exception };
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await manager.RunWaitCallbackAsync(ct);
            }
            catch (TaskCanceledException)
            {
            }
        }

        if (error is not null)
        {
            return error;
        }

        return account;

        async void OnConnected(SteamClient.ConnectedCallback callback)
        {
            try
            {
                _logger.LogInformation("Logging in as '{Username}'...", username);

                var shouldRememberPassword = true;
                var authSession = await steamClient.Authentication.BeginAuthSessionViaCredentialsAsync(
                    new AuthSessionDetails
                    {
                        Username = username,
                        Password = password,
                        IsPersistentSession = shouldRememberPassword,
                        Authenticator = new UserConsoleAuthenticator()
                    });

                var pollResponse = await authSession.PollingWaitForResultAsync(ct);

                account.AccessToken = pollResponse.AccessToken;
                account.RefreshToken = pollResponse.RefreshToken;
                account.Username = pollResponse.AccountName;

                steamUser.LogOn(new SteamUser.LogOnDetails
                {
                    Username = pollResponse.AccountName,
                    AccessToken = pollResponse.RefreshToken,
                    ShouldRememberPassword = shouldRememberPassword
                });
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception exception)
            {
                error = new AuthorizationServiceError
                {
                    Message = "Unexpected error in auth via username/password code.",
                    Exception = exception
                };

                cts.Cancel();
            }
        }

        void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            _logger.LogDebug("Disconnected from Steam");
            cts.Cancel();
        }

        void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
                error = new AuthorizationServiceError
                {
                    Message = $"Unable to logon to Steam: {callback.Result} / {callback.ExtendedResult}"
                };

                cts.Cancel();
                return;
            }
            
            account.Id = callback.ClientSteamID!.ConvertToUInt64();

            _logger.LogInformation("Successfully logged on!");
            cts.Cancel();
            
            // steamUser.LogOff();
        }
    }

    public async Task<OneOf<Account, AuthorizationServiceError>> AuthorizeByQrCodeAsync()
    {
        AuthorizationServiceError? error = null;
        var account = new Account();

        var steamClient = new SteamClient();
        var manager = new CallbackManager(steamClient);
        var steamUser = steamClient.GetHandler<SteamUser>()!;

        var cts = new CancellationTokenSource();
        var ct = cts.Token;
        manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);

        try
        {
            _logger.LogInformation("Connecting to Steam...");
            steamClient.Connect();
        }
        catch (Exception exception)
        {
            return new AuthorizationServiceError { Message = "An error occured while connecting to steam.", Exception = exception };
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await manager.RunWaitCallbackAsync(ct);
            }
            catch (TaskCanceledException)
            {
            }
        }

        cts.Dispose();

        if (error is not null)
        {
            return error;
        }

        return account;

        async void OnConnected(SteamClient.ConnectedCallback callback)
        {
            try
            {
                var authSession = await steamClient.Authentication.BeginAuthSessionViaQRAsync(new AuthSessionDetails());
                authSession.ChallengeURLChanged = () =>
                {
                    // TODO: better refresh
                    _logger.LogInformation("Steam has refreshed the qr code");
                    DrawChallengeUrl(authSession);
                };

                DrawChallengeUrl(authSession);

                var pollResponse = await authSession.PollingWaitForResultAsync(ct);

                account.AccessToken = pollResponse.AccessToken;
                account.RefreshToken = pollResponse.RefreshToken;
                account.Username = pollResponse.AccountName;

                _logger.LogInformation("Logging in as '{Username}'...", pollResponse.AccountName);
                steamUser.LogOn(new SteamUser.LogOnDetails
                {
                    Username = pollResponse.AccountName,
                    AccessToken = pollResponse.RefreshToken,
                });
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception exception)
            {
                error = new AuthorizationServiceError
                {
                    Message = "Unexpected error in auth via qr code.",
                    Exception = exception
                };

                cts.Cancel();
            }
        }

        void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            _logger.LogDebug("Disconnected from Steam");
            cts.Cancel();
        }

        void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            if (callback.Result != EResult.OK)
            {
                error = new AuthorizationServiceError
                {
                    Message =
                        $"Unable to logon to Steam{(callback.Result == EResult.TryAnotherCM ? " (possible timeout)" : string.Empty)}: {callback.Result} / {callback.ExtendedResult}"
                };

                cts.Cancel();
                return;
            }

            account.Id = callback.ClientSteamID!.ConvertToUInt64();

            _logger.LogInformation("Successfully logged on!");
            cts.Cancel();
            // steamUser.LogOff();

            // var steamLoginSecure = $"{callback.ClientSteamID}||{accessToken}";

            // The access token expires in 24 hours (at the time of writing) so you will have to renew it.
            // Parse this token with a JWT library to get the expiration date and set up a timer to renew it.
            // To renew you will have to call this:
            // When allowRenewal is set to true, Steam may return new RefreshToken
        }

        void DrawChallengeUrl(QrAuthSession authSession)
        {
            var url = authSession.ChallengeURL;
            Console.WriteLine($"URL: {url}");
            QrCode.DrawSmallestAscii(url);
        }
    }
}
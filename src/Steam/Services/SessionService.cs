using OneOf;
using Serilog;
using SteamKit2;
using XpGetter.Dto;
using XpGetter.Errors;

namespace XpGetter.Steam.Services;

public interface ISessionService
{
    Task<OneOf<SteamSession, SessionServiceError>> GetOrCreateSessionAsync(
        string clientName, AccountDto? account = null);
}

public class SessionService : ISessionService
{
    private readonly Dictionary<ulong, SteamSession> _sessions = new();
    private readonly ILogger _logger;

    public SessionService(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<OneOf<SteamSession, SessionServiceError>> GetOrCreateSessionAsync(
        string clientName, AccountDto? account = null)
    {
        if (account is not null && _sessions.TryGetValue(account.Id, out var session))
        {
            return session;
        }

        var steamClient = new SteamClient();
        var manager = new CallbackManager(steamClient);
        var steamUser = steamClient.GetHandler<SteamUser>()!;

        const int retriesCount = 3;
        var retryNumber = 1;
        var isDisconnected = false;
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        using var connectedSubscription = manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        using var disconnectedSubscription = manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);

        // "Connect" label for simplifying retry logic
        Connect:

        try
        {
            _logger.Debug("Client '{ClientName}': Connecting to Steam...", clientName);
            steamClient.Connect();
        }
        catch (Exception exception)
        {
            steamClient.Disconnect();
            return new SessionServiceError
            {
                ClientName = clientName,
                Message = "An error occured while connecting to steam.",
                Exception = exception
            };
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await manager.RunWaitCallbackAsync(ct);

                if (isDisconnected)
                {
                    if (retryNumber > retriesCount)
                    {
                        return new SessionServiceError
                        {
                            ClientName = clientName,
                            Message = "Cannot connect to steam."
                        };
                    }

                    isDisconnected = false;
                    _logger.Information("Trying to reconnect ({RetryNumber})", retryNumber++);
                    goto Connect;
                }
            }
            catch (TaskCanceledException)
            {
            }
        }

        // "End" of the label

        session = new SteamSession(clientName, steamClient, manager, steamUser);
        session.AccountBind += OnAccountBounded;
        return session;

        void OnConnected(SteamClient.ConnectedCallback callback)
        {
            _logger.Information("Client '{ClientName}': Connected to Steam.", clientName);
            cts.Cancel();
        }

        void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            isDisconnected = true;
            _logger.Debug("Client '{ClientName}': Disconnected from Steam.", clientName);
        }
    }

    private void OnAccountBounded(SteamSession session)
    {
        if (!session.IsAuthenticated || session is { Client.SteamID: null })
        {
            throw new InvalidOperationException(
                $"Attempt to bind unauthenticated account. Client '{session.Name}': {session.Account?.Id} | {session.Account?.Username}");
        }

        _sessions.Add(session.Client.SteamID, session);
    }
}

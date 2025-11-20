using SteamKit2;
using OneOf;
using Serilog;
using XpGetter.Dto;

namespace XpGetter.Steam;

public interface ISessionService
{
    Task<OneOf<SteamSession, SessionServiceError>> CreateSessionAsync(string clientName);
}

public class SessionServiceError
{
    public required string Message { get; init; }
    public Exception? Exception { get; init; }
}

public class SessionService : ISessionService
{
    private readonly ILogger _logger;

    public SessionService(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<OneOf<SteamSession, SessionServiceError>> CreateSessionAsync(string clientName)
    {
        var steamClient = new SteamClient();
        var manager = new CallbackManager(steamClient);
        var steamUser = steamClient.GetHandler<SteamUser>()!;

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;
        using var connectedSubscription = manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        using var disconnectedSubscription = manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);

        try
        {
            _logger.Debug("Client '{ClientName}': Connecting to Steam...", clientName);
            steamClient.Connect();
        }
        catch (Exception exception)
        {
            steamClient.Disconnect();
            return new SessionServiceError { Message = "An error occured while connecting to steam.", Exception = exception };
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

        return new SteamSession(steamClient, manager, steamUser);

        void OnConnected(SteamClient.ConnectedCallback callback)
        {
            _logger.Information("Client '{ClientName}': Connected to Steam.", clientName);
            cts.Cancel();
        }

        void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            _logger.Debug("Client '{ClientName}': Disconnected from Steam.", clientName);
        }
    }
}

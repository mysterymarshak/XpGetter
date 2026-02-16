using System.Collections.Concurrent;
using OneOf;
using Serilog;
using SteamKit2;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;
using XpGetter.Application.Extensions;

namespace XpGetter.Application.Features.Steam;

public interface ISessionService : IDisposable
{
    Task<OneOf<SteamSession, SessionServiceError>> GetOrCreateSessionAsync(
        string? clientName = null, AccountDto? account = null);
}

public class SessionService : ISessionService
{
    private readonly ConcurrentDictionary<ulong, SteamSession> _sessions = new();
    private readonly ILogger _logger;

    public SessionService(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<OneOf<SteamSession, SessionServiceError>> GetOrCreateSessionAsync(
        string? clientName = null, AccountDto? account = null)
    {
        var displayName = clientName?.ToDisplayUsername(ignoreConfiguration: false);
        clientName ??= SteamSession.DefaultName;
        displayName ??= clientName;

        if (account is not null && _sessions.TryGetValue(account.Id, out var session))
        {
            if (session is { Client.IsConnected: false })
            {
                _sessions.Remove(account.Id, out _);
            }
            else
            {
                return session;
            }
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
            _logger.Debug(Messages.Session.BoundedSessionLogFormat, clientName,
                Messages.Session.Connecting);
            steamClient.Connect();
        }
        catch (Exception exception)
        {
            steamClient.Disconnect();
            return new SessionServiceError
            {
                ClientName = displayName,
                Message = Messages.Session.ConnectingException,
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
                            ClientName = displayName,
                            Message = Messages.Session.TooManyRetryAttempts.BindSession(
                                forceName: displayName, logging: false)
                        };
                    }

                    isDisconnected = false;
                    _logger.Information(Messages.Session.BoundedSessionLogFormat, clientName,
                        string.Format(Messages.Session.Reconnect, retryNumber++));
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
            _logger.Information(Messages.Session.BoundedSessionLogFormat, clientName,
                Messages.Session.Connected);
            cts.Cancel();
        }

        void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            isDisconnected = true;
            _logger.Debug(Messages.Session.BoundedSessionLogFormat, clientName,
                Messages.Session.Disconnected);
        }
    }

    public void Dispose()
    {
        foreach (var kv in _sessions)
        {
            var session = kv.Value;
            session.Dispose();
        }
    }

    private void OnAccountBounded(SteamSession session)
    {
        session.AccountBind -= OnAccountBounded;

        if (!session.IsAuthenticated)
        {
            _logger.Error(Messages.Session.BoundedSessionLogFormat, session.GetName(ignoreConfiguration: true),
                Messages.Session.UnauthenticatedAccountBind);

            throw new InvalidOperationException(Messages.Session.UnauthenticatedAccountBind);
        }

        if (!_sessions.TryAdd(session.Client.SteamID!, session))
        {
            throw new InvalidOperationException(string.Format(Messages.Session.DuplicatedSession,
                                                              session.Client.SteamID));
        }
    }
}

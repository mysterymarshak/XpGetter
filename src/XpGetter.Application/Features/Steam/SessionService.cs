using System.Collections.Concurrent;
using OneOf;
using ProtoBuf;
using Serilog;
using SteamKit2;
using SteamKit2.GC;
using SteamKit2.GC.CSGO.Internal;
using XpGetter.Application.Dto;
using XpGetter.Application.Errors;
using XpGetter.Application.Extensions;
using XpGetter.Application.Utils;

namespace XpGetter.Application.Features.Steam;

public interface ISessionService : IDisposable
{
    ValueTask<OneOf<SteamSession, SessionServiceError>> GetOrCreateSessionAsync(
        string? clientName = null, AccountDto? account = null);
}

// TODO: base class for these two (?)

public class GcMessagesFetcher : IDisposable
{
    private TimeSpan GcMessageTimeout => TimeSpan.FromSeconds(10);

    private readonly ILogger _logger;
    private readonly Dictionary<uint, List<IGcMessageAwaiter>> _awaiters = new();

    public GcMessagesFetcher(ILogger logger)
    {
        _logger = logger;
    }

    public void Receive(SteamGameCoordinator.MessageCallback msg)
    {
        if (msg.AppID != Constants.Cs2AppId)
            return;

        MsgBase? deserialized = msg.EMsg switch
        {
            (uint)EGCBaseClientMsg.k_EMsgGCClientWelcome => new ClientGCMsgProtobuf<CMsgClientWelcome>(msg.Message),
            (uint)ECsgoGCMsg.k_EMsgGCCStrike15_v2_PlayersProfile => new ClientGCMsgProtobuf<CMsgGCCStrike15_v2_PlayersProfile>(msg.Message),
            (uint)ECsgoGCMsg.k_EMsgGCCStrike15_v2_Client2GCEconPreviewDataBlockResponse => new ClientGCMsgProtobuf<CMsgGCCStrike15_v2_Client2GCEconPreviewDataBlockResponse>(msg.Message),
            (uint)EGCItemMsg.k_EMsgGCItemCustomizationNotification => new ClientGCMsgProtobuf<CMsgGCItemCustomizationNotification>(msg.Message),
            _ => null
        };

        if (deserialized is null)
        {
            _logger.Debug(Messages.Session.UnknownGcMessageTypeLog, msg);
            return;
        }

        OnCallback(msg.EMsg, deserialized);
    }

    public async Task<ClientGCMsgProtobuf<T>> WaitForAsync<T>(
        GcMessageAwaiter<T> awaiter, TimeSpan? timeout = null) where T : IExtensible, new()
    {
        timeout ??= GcMessageTimeout;

        var timeoutTask = Task.Delay(timeout.Value);
        var completedTask = await Task.WhenAny(awaiter.Task, timeoutTask);

        if (completedTask == timeoutTask)
        {
            CancelAwaiting(awaiter);
        }

        return await awaiter.Task;
    }

    public GcMessageAwaiter<T> CreateAndRegisterAwaiter<T>(uint msgType) where T : IExtensible, new()
    {
        if (!_awaiters.TryGetValue(msgType, out var awaiters))
        {
            awaiters = [];
            _awaiters[msgType] = awaiters;
        }

        var awaiter = new GcMessageAwaiter<T>(msgType);
        awaiters.Add(awaiter);

        return awaiter;
    }

    public void Dispose()
    {
        foreach (var awaiters in _awaiters)
        {
            foreach (var awaiter in awaiters.Value)
            {
                awaiter.Cancel();
                awaiter.Dispose();
            }
        }

        _awaiters.Clear();
    }

    private void CancelAwaiting(IGcMessageAwaiter awaiter)
    {
        awaiter.Cancel();
        UnregisterAwaiter(awaiter);
        awaiter.Dispose();
    }

    private void UnregisterAwaiter(IGcMessageAwaiter awaiter)
    {
        if (_awaiters.TryGetValue(awaiter.MsgType, out var awaiters))
        {
            UnregisterAwaiter(awaiters, awaiter);
        }
    }

    private void UnregisterAwaiter(List<IGcMessageAwaiter> awaiters, IGcMessageAwaiter awaiter)
    {
        awaiters.Remove(awaiter);
        awaiter.Dispose();
    }

    private void OnCallback(uint eMsg, MsgBase msg)
    {
        if (_awaiters.TryGetValue(eMsg, out var awaiters))
        {
            for (var i = awaiters.Count - 1; i >= 0; i--)
            {
                var awaiter = awaiters[i];
                awaiter.Receive(msg);
                UnregisterAwaiter(awaiters, awaiter);
            }
        }
    }
}

public class MessagesFetcher : IDisposable
{
    public GcMessagesFetcher Gc => _gcMessagesFetcher;

    private TimeSpan MessageTimeout => TimeSpan.FromSeconds(10);

    private readonly SteamClient _client;
    private readonly GcMessagesFetcher _gcMessagesFetcher;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _cts;
    private readonly Dictionary<Type, List<ICallbackAwaiter>> _awaiters = new();
    private readonly Dictionary<Type, List<Action<CallbackMsg>>> _callbacks = new();

    public MessagesFetcher(SteamClient client, GcMessagesFetcher gcMessagesFetcher, ILogger logger)
    {
        _client = client;
        _gcMessagesFetcher = gcMessagesFetcher;
        _logger = logger;
        _cts = new CancellationTokenSource();
    }

    public void StartReceiving()
    {
        _ = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var callback = await _client.WaitForCallbackAsync(_cts.Token);
                    _logger.Verbose("Received callback: {Callback}", callback.GetType().FullName);
                    OnCallback(callback);
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception exception)
                {
                    _logger.Error(exception, Messages.Session.ExceptionInMessagesFetcherLog);
                }
            }
        }, _cts.Token);
    }

    public void HookCallback<T>(Action<T> callback) where T : CallbackMsg
    {
        var type = typeof(T);
        if (!_callbacks.TryGetValue(type, out var callbacks))
        {
            callbacks = [];
            _callbacks[type] = callbacks;
        }

        callbacks.Add(x => callback.Invoke((T)x));
    }

    public async Task<OneOf<T1, T2>> WaitForAnyAsync<T1, T2>(
        CallbackAwaiter<T1> awaiter1,
        CallbackAwaiter<T2> awaiter2,
        TimeSpan? timeout = null) where T1 : CallbackMsg where T2 : CallbackMsg
    {
        timeout ??= MessageTimeout;

        var timeoutTask = Task.Delay(timeout.Value);
        var completedTask = await Task.WhenAny(awaiter1.Task, awaiter2.Task, timeoutTask);

        if (completedTask == timeoutTask)
        {
            CancelAwaiting(awaiter1);
            CancelAwaiting(awaiter2);
            throw new TaskCanceledException();
        }

        if (completedTask == awaiter1.Task)
        {
            CancelAwaiting(awaiter2);
            return await awaiter1.Task;
        }

        CancelAwaiting(awaiter1);
        return await awaiter2.Task;
    }

    public async ValueTask<T> WaitForAsync<T>(
        CallbackAwaiter<T> awaiter, TimeSpan? timeout = null) where T : CallbackMsg
    {
        timeout ??= MessageTimeout;

        var timeoutTask = Task.Delay(timeout.Value);
        var completedTask = await Task.WhenAny(awaiter.Task, timeoutTask);

        if (completedTask == timeoutTask)
        {
            CancelAwaiting(awaiter);
        }

        return await awaiter.Task;
    }

    public async Task<T> WaitForAsync<T>(
        CallbackAwaiter<T> awaiter, CancellationToken ct) where T : CallbackMsg
    {
        var timeoutTask = Task.Delay(-1, ct);
        var completedTask = await Task.WhenAny(awaiter.Task, timeoutTask);

        if (completedTask == timeoutTask)
        {
            CancelAwaiting(awaiter);
        }

        return await awaiter.Task;
    }

    public CallbackAwaiter<T> CreateAndRegisterAwaiter<T>() where T : CallbackMsg
    {
        var type = typeof(T);
        if (!_awaiters.TryGetValue(type, out var awaiters))
        {
            awaiters = [];
            _awaiters[type] = awaiters;
        }

        var awaiter = new CallbackAwaiter<T>();
        awaiters.Add(awaiter);

        return awaiter;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _callbacks.Clear();

        foreach (var awaiters in _awaiters.Values)
        {
            foreach (var awaiter in awaiters)
            {
                awaiter.Cancel();
                awaiter.Dispose();
            }
        }
        _awaiters.Clear();

        _gcMessagesFetcher.Dispose();
    }

    private void CancelAwaiting<T>(CallbackAwaiter<T> awaiter) where T : CallbackMsg
    {
        awaiter.Cancel();
        UnregisterAwaiter(awaiter);
    }

    private void UnregisterAwaiter<T>(CallbackAwaiter<T> awaiter) where T : CallbackMsg
        => UnregisterAwaiter(typeof(T), awaiter);

    private void UnregisterAwaiter(Type type, ICallbackAwaiter awaiter)
    {
        if (_awaiters.TryGetValue(type, out var awaiters))
        {
            UnregisterAwaiter(awaiters, awaiter);
        }
    }

    private void UnregisterAwaiter(List<ICallbackAwaiter> awaiters, ICallbackAwaiter awaiter)
    {
        awaiters.Remove(awaiter);
        awaiter.Dispose();
    }

    private void OnCallback(CallbackMsg msg)
    {
        var type = msg.GetType();

        if (msg is SteamGameCoordinator.MessageCallback gcMessage)
        {
            _gcMessagesFetcher.Receive(gcMessage);
            return;
        }

        if (_callbacks.TryGetValue(type, out var callbacks))
        {
            foreach (var callback in callbacks)
            {
                callback.Invoke(msg);
            }
        }

        if (_awaiters.TryGetValue(type, out var awaiters))
        {
            for (var i = awaiters.Count - 1; i >= 0; i--)
            {
                var awaiter = awaiters[i];
                awaiter.Receive(msg);
                UnregisterAwaiter(type, awaiter);
            }
        }
    }
}

public class SessionService : ISessionService
{
    private readonly ConcurrentDictionary<ulong, SteamSession> _sessions = new();
    private readonly ILogger _logger;

    public SessionService(ILogger logger)
    {
        _logger = logger;
    }

    public async ValueTask<OneOf<SteamSession, SessionServiceError>> GetOrCreateSessionAsync(
        string? clientName = null, AccountDto? account = null)
    {
        var displayName = clientName?.ToDisplayUsername(ignoreConfiguration: false);
        clientName ??= SteamSession.DefaultName;
        displayName ??= clientName;

        if (account is not null && _sessions.TryGetValue(account.Id, out var session))
        {
            if (session is { Client.IsConnected: false })
            {
                _sessions.Remove(account.Id, out var removedSession);
                removedSession?.Dispose();
            }
            else
            {
                return session;
            }
        }

        return await CreateSessionAsync(clientName, displayName);
    }

    public void Dispose()
    {
        foreach (var kv in _sessions)
        {
            var session = kv.Value;
            session.Dispose();
        }
    }

    private async Task<OneOf<SteamSession, SessionServiceError>> CreateSessionAsync(string clientName, string displayName)
    {
        var steamClient = new SteamClient();
        var steamUser = steamClient.GetHandler<SteamUser>()!;
        var gcFetcher = new GcMessagesFetcher(_logger);
        var messagesFetcher = new MessagesFetcher(steamClient, gcFetcher, _logger);
        messagesFetcher.StartReceiving();

        var connected = await ConnectClientAsync(clientName, steamClient, messagesFetcher);
        if (!connected)
        {
            return new SessionServiceError
            {
                ClientName = displayName,
                Message = Messages.Session.TooManyRetryAttempts.BindSession(
                    forceName: displayName, logging: false)
            };
        }

        var session = new SteamSession(clientName, steamClient, messagesFetcher, steamUser);
        session.AccountBind += OnAccountBounded;

        return session;
    }

    private async Task<bool> ConnectClientAsync(string clientName, SteamClient client, MessagesFetcher fetcher)
    {
        var connected = false;
        const int retriesCount = 3;
        var retryNumber = 1;

        while (retryNumber <= retriesCount)
        {
            _logger.Debug(Messages.Session.BoundedSessionLogFormat, clientName,
                Messages.Session.Connecting);

            var connectAwaiter = fetcher.CreateAndRegisterAwaiter<SteamClient.ConnectedCallback>();
            var disconnectAwaiter = fetcher.CreateAndRegisterAwaiter<SteamClient.DisconnectedCallback>();

            try
            {
                client.Connect();

                var connectResult = await fetcher.WaitForAnyAsync(connectAwaiter, disconnectAwaiter);
                if (connectResult.IsT0)
                {
                    connected = true;
                    _logger.Information(Messages.Session.BoundedSessionLogFormat, clientName,
                        Messages.Session.Connected);
                    break;
                }
            }
            catch (TaskCanceledException)
            {
            }

            _logger.Debug(Messages.Session.DisconnectedLog.BindSession(forceName: clientName));
            _logger.Information(Messages.Session.Reconnect.BindSession(forceName: clientName), retryNumber++);
        }

        return connected;
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

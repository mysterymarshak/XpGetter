using ProtoBuf;
using SteamKit2;
using SteamKit2.GC;

namespace XpGetter.Application.Utils;

public class GcMessageAwaiter<T> : IGcMessageAwaiter where T : IExtensible, new()
{
    public uint MsgType { get; }
    public Task<ClientGCMsgProtobuf<T>> Task => _tcs.Task;

    private readonly TaskCompletionSource<ClientGCMsgProtobuf<T>> _tcs = new();

    public GcMessageAwaiter(uint msgType)
    {
        MsgType = msgType;
    }

    public void Receive(MsgBase msg)
    {
        _tcs.TrySetResult((ClientGCMsgProtobuf<T>)msg);
    }

    public void Cancel()
    {
        if (!_tcs.Task.IsCompleted)
        {
            _tcs.TrySetCanceled();
        }
    }

    public void Dispose()
    {
        _tcs.Task.Dispose();
    }
}

public interface IGcMessageAwaiter : IDisposable
{
    uint MsgType { get; }
    void Receive(MsgBase msg);
    void Cancel();
}

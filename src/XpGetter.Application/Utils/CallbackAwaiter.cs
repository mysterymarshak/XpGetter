using SteamKit2;

namespace XpGetter.Application.Utils;

public class CallbackAwaiter<T> : ICallbackAwaiter where T : CallbackMsg
{
    public Task<T> Task => _tcs.Task;

    private readonly TaskCompletionSource<T> _tcs = new();

    public void Receive(CallbackMsg msg)
    {
        _tcs.TrySetResult((T)msg);
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

public interface ICallbackAwaiter : IDisposable
{
    void Receive(CallbackMsg msg);
    void Cancel();
}
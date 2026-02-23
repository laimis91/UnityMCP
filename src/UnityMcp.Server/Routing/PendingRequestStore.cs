using System.Collections.Concurrent;

namespace UnityMcp.Server.Routing;

public sealed class PendingRequestStore
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pending = new();

    public bool TryRegister(string requestIdKey, out TaskCompletionSource<string> completionSource)
    {
        completionSource = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        return _pending.TryAdd(requestIdKey, completionSource);
    }

    public bool TryComplete(string requestIdKey, string payload)
    {
        if (_pending.TryRemove(requestIdKey, out var completionSource))
        {
            completionSource.TrySetResult(payload);
            return true;
        }

        return false;
    }

    public bool TryFail(string requestIdKey, Exception exception)
    {
        if (_pending.TryRemove(requestIdKey, out var completionSource))
        {
            completionSource.TrySetException(exception);
            return true;
        }

        return false;
    }

    public bool TryCancel(string requestIdKey)
    {
        if (_pending.TryRemove(requestIdKey, out var completionSource))
        {
            completionSource.TrySetCanceled();
            return true;
        }

        return false;
    }

    public void FailAll(Exception exception)
    {
        foreach (var pair in _pending)
        {
            if (_pending.TryRemove(pair.Key, out var completionSource))
            {
                completionSource.TrySetException(exception);
            }
        }
    }
}


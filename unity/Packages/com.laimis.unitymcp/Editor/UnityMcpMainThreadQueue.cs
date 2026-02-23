#nullable enable

using System;
using System.Collections.Concurrent;
using UnityEditor;

namespace UnityMcp.Editor
{

[InitializeOnLoad]
internal static class UnityMcpMainThreadQueue
{
    private static readonly ConcurrentQueue<Action> Queue = new();

    static UnityMcpMainThreadQueue()
    {
        EditorApplication.update += DrainQueue;
    }

    public static void Enqueue(Action action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        Queue.Enqueue(action);
    }

    private static void DrainQueue()
    {
        while (Queue.TryDequeue(out var action))
        {
            try
            {
                action.Invoke();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
            }
        }
    }
}
}

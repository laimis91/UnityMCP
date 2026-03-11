namespace UnityMcp.Server.Protocol;

public sealed class DuplicateRequestIdException : Exception
{
    public string RequestIdKey { get; }

    public DuplicateRequestIdException(string requestIdKey)
        : base($"Duplicate JSON-RPC request id '{requestIdKey}'.")
    {
        RequestIdKey = requestIdKey;
    }
}

namespace UnityMcp.Server.Protocol;

public static class JsonRpcErrorCodes
{
    public const int ParseError = -32700;
    public const int InvalidRequest = -32600;
    public const int MethodNotFound = -32601;
    public const int InvalidParams = -32602;
    public const int InternalError = -32603;

    public const int UnityNotConnected = -32001;
    public const int UnityTimeout = -32002;
    public const int DuplicateRequestId = -32003;
}


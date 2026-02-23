namespace UnityMcp.Server.Mcp;

public interface IUnityJsonRpcForwarder
{
    Task<string> ForwardAsync(string requestJson, string requestIdKey, CancellationToken cancellationToken);
}

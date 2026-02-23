using UnityMcp.Server.Routing;

namespace UnityMcp.Server.Mcp;

public sealed class UnityJsonRpcForwarder : IUnityJsonRpcForwarder
{
    private readonly UnityRelayService _relayService;

    public UnityJsonRpcForwarder(UnityRelayService relayService)
    {
        _relayService = relayService;
    }

    public Task<string> ForwardAsync(string requestJson, string requestIdKey, CancellationToken cancellationToken)
    {
        return _relayService.ForwardToUnityAsync(requestJson, requestIdKey, cancellationToken);
    }
}

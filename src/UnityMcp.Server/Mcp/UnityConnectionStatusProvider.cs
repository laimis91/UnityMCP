using UnityMcp.Server.Transport;

namespace UnityMcp.Server.Mcp;

public sealed class UnityConnectionStatusProvider : IUnityConnectionStatusProvider
{
    private readonly UnitySocketHub _unitySocketHub;

    public UnityConnectionStatusProvider(UnitySocketHub unitySocketHub)
    {
        _unitySocketHub = unitySocketHub;
    }

    public bool IsUnityConnected => _unitySocketHub.HasConnectedUnity;
}

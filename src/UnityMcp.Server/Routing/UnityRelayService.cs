using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UnityMcp.Server.Options;
using UnityMcp.Server.Protocol;
using UnityMcp.Server.Transport;

namespace UnityMcp.Server.Routing;

public sealed class UnityRelayService
{
    private readonly UnitySocketHub _unitySocketHub;
    private readonly PendingRequestStore _pendingRequestStore;
    private readonly ILogger<UnityRelayService> _logger;
    private readonly TimeSpan _timeout;

    public UnityRelayService(
        UnitySocketHub unitySocketHub,
        PendingRequestStore pendingRequestStore,
        IOptions<ServerOptions> options,
        ILogger<UnityRelayService> logger)
    {
        _unitySocketHub = unitySocketHub;
        _pendingRequestStore = pendingRequestStore;
        _logger = logger;
        _timeout = TimeSpan.FromSeconds(Math.Max(1, options.Value.UnityRequestTimeoutSeconds));
    }

    public async Task<string> ForwardToUnityAsync(string requestJson, string requestIdKey, CancellationToken cancellationToken)
    {
        if (!_pendingRequestStore.TryRegister(requestIdKey, out var pending))
        {
            throw new DuplicateRequestIdException(requestIdKey);
        }

        try
        {
            var sent = await _unitySocketHub.TrySendAsync(requestJson, cancellationToken);
            if (!sent)
            {
                _pendingRequestStore.TryFail(requestIdKey, new UnityNotConnectedException());
                throw new UnityNotConnectedException();
            }

            return await pending.Task.WaitAsync(_timeout, cancellationToken);
        }
        catch (TimeoutException)
        {
            _pendingRequestStore.TryCancel(requestIdKey);
            _logger.LogWarning("Timed out waiting for Unity response. RequestId={RequestId}", requestIdKey);
            throw;
        }
        catch (OperationCanceledException)
        {
            _pendingRequestStore.TryCancel(requestIdKey);
            throw;
        }
    }
}


using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using UnityMcp.Server.Protocol;
using UnityMcp.Server.Routing;

namespace UnityMcp.Server.Transport;

public sealed class UnitySocketHub
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly PendingRequestStore _pendingRequestStore;
    private readonly ILogger<UnitySocketHub> _logger;

    private WebSocket? _unitySocket;

    public UnitySocketHub(PendingRequestStore pendingRequestStore, ILogger<UnitySocketHub> logger)
    {
        _pendingRequestStore = pendingRequestStore;
        _logger = logger;
    }

    public bool HasConnectedUnity
    {
        get
        {
            lock (_sync)
            {
                return _unitySocket?.State == WebSocketState.Open;
            }
        }
    }

    public async Task AttachAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        WebSocket? previousSocket;

        lock (_sync)
        {
            previousSocket = _unitySocket;
            _unitySocket = socket;
        }

        if (previousSocket is not null && previousSocket.State == WebSocketState.Open)
        {
            try
            {
                await previousSocket.CloseAsync(
                    WebSocketCloseStatus.PolicyViolation,
                    "Replaced by a newer Unity connection.",
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to close previous Unity socket cleanly.");
            }
        }

        _logger.LogInformation("Unity connected.");

        try
        {
            await ReceiveLoopAsync(socket, cancellationToken);
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_unitySocket, socket))
                {
                    _unitySocket = null;
                }
            }

            _pendingRequestStore.FailAll(new InvalidOperationException("Unity connection closed."));
            _logger.LogWarning("Unity disconnected.");

            if (socket.State == WebSocketState.Open)
            {
                try
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing.", CancellationToken.None);
                }
                catch
                {
                    // Ignore cleanup failures.
                }
            }
        }
    }

    public async Task<bool> TrySendAsync(string payload, CancellationToken cancellationToken)
    {
        WebSocket? socket;

        lock (_sync)
        {
            socket = _unitySocket;
        }

        if (socket is null || socket.State != WebSocketState.Open)
        {
            return false;
        }

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            if (socket.State != WebSocketState.Open)
            {
                return false;
            }

            var bytes = Encoding.UTF8.GetBytes(payload);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            return true;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task ReceiveLoopAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            string? message;
            try
            {
                message = await WebSocketTextMessageReader.ReadAsync(socket, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (WebSocketException ex)
            {
                _logger.LogWarning(ex, "Unity receive loop terminated due to WebSocket error.");
                break;
            }

            if (message is null)
            {
                break;
            }

            if (!JsonRpcProtocol.TryParse(message, out var document, out var parseError))
            {
                _logger.LogWarning("Ignoring invalid JSON from Unity: {ParseError}", parseError);
                continue;
            }

            using var parsedDocument = document!;
            {
                var root = parsedDocument.RootElement;
                if (!JsonRpcProtocol.IsResponse(root))
                {
                    _logger.LogDebug("Ignoring non-response message from Unity.");
                    continue;
                }

                if (!JsonRpcProtocol.TryGetId(root, out _, out var idKey) || string.IsNullOrWhiteSpace(idKey))
                {
                    _logger.LogWarning("Unity response had invalid or missing id.");
                    continue;
                }

                if (!_pendingRequestStore.TryComplete(idKey!, message))
                {
                    _logger.LogDebug("No pending CLI request found for Unity response id {RequestId}.", idKey);
                }
            }
        }
    }
}

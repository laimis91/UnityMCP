using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using UnityMcp.Server.Protocol;
using UnityMcp.Server.Routing;

namespace UnityMcp.Server.Transport;

public sealed class CliSessionHandler
{
    private readonly UnityRelayService _relay;
    private readonly ILogger<CliSessionHandler> _logger;

    public CliSessionHandler(UnityRelayService relay, ILogger<CliSessionHandler> logger)
    {
        _relay = relay;
        _logger = logger;
    }

    public async Task HandleAsync(WebSocket socket, CancellationToken cancellationToken)
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
                _logger.LogWarning(ex, "CLI socket receive loop ended due to WebSocket error.");
                break;
            }

            if (message is null)
            {
                break;
            }

            if (!JsonRpcProtocol.TryParse(message, out var document, out var parseError))
            {
                await SendAsync(socket, JsonRpcProtocol.CreateError(
                    idNode: null,
                    code: JsonRpcErrorCodes.ParseError,
                    message: $"Invalid JSON: {parseError}"), cancellationToken);
                continue;
            }

            using var parsedDocument = document!;
            {
                var root = parsedDocument.RootElement;

                if (root.ValueKind != System.Text.Json.JsonValueKind.Object)
                {
                    await SendAsync(socket, JsonRpcProtocol.CreateError(
                        idNode: null,
                        code: JsonRpcErrorCodes.InvalidRequest,
                        message: "JSON-RPC payload must be an object."), cancellationToken);
                    continue;
                }

                if (!JsonRpcProtocol.TryGetId(root, out var idNode, out var idKey) || string.IsNullOrWhiteSpace(idKey))
                {
                    await SendAsync(socket, JsonRpcProtocol.CreateError(
                        idNode: null,
                        code: JsonRpcErrorCodes.InvalidRequest,
                        message: "JSON-RPC request must include a string or numeric id."), cancellationToken);
                    continue;
                }

                if (!JsonRpcProtocol.TryGetMethod(root, out var method) || string.IsNullOrWhiteSpace(method))
                {
                    await SendAsync(socket, JsonRpcProtocol.CreateError(
                        idNode,
                        JsonRpcErrorCodes.InvalidRequest,
                        "JSON-RPC request must include a method name."), cancellationToken);
                    continue;
                }

                if (string.Equals(method, "ping", StringComparison.Ordinal))
                {
                    var pingResult = new System.Text.Json.Nodes.JsonObject
                    {
                        ["ok"] = true,
                        ["serverTimeUtc"] = DateTimeOffset.UtcNow.ToString("O"),
                        ["source"] = "server"
                    };

                    await SendAsync(socket, JsonRpcProtocol.CreateResult(idNode, pingResult), cancellationToken);
                    continue;
                }

                try
                {
                    var responseJson = await _relay.ForwardToUnityAsync(message, idKey!, cancellationToken);
                    await SendAsync(socket, responseJson, cancellationToken);
                }
                catch (DuplicateRequestIdException ex)
                {
                    await SendAsync(socket, JsonRpcProtocol.CreateError(idNode, JsonRpcErrorCodes.DuplicateRequestId, ex.Message), cancellationToken);
                }
                catch (UnityNotConnectedException ex)
                {
                    await SendAsync(socket, JsonRpcProtocol.CreateError(idNode, JsonRpcErrorCodes.UnityNotConnected, ex.Message), cancellationToken);
                }
                catch (TimeoutException)
                {
                    await SendAsync(socket, JsonRpcProtocol.CreateError(idNode, JsonRpcErrorCodes.UnityTimeout, "Timed out waiting for Unity response."), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled CLI request processing error.");
                    await SendAsync(socket, JsonRpcProtocol.CreateError(idNode, JsonRpcErrorCodes.InternalError, "Internal server error."), cancellationToken);
                }
            }
        }

        if (socket.State == WebSocketState.Open)
        {
            try
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "CLI session closed.", CancellationToken.None);
            }
            catch
            {
                // Ignore cleanup failures.
            }
        }
    }

    private static async Task SendAsync(WebSocket socket, string payload, CancellationToken cancellationToken)
    {
        if (socket.State != WebSocketState.Open)
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(payload);
        await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
    }
}

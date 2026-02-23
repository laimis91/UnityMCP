using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using UnityMcp.Server.Mcp;
using UnityMcp.Server.Options;
using UnityMcp.Server.Protocol;
using UnityMcp.Server.Routing;
using UnityMcp.Server.Transport;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ServerOptions>(builder.Configuration.GetSection(ServerOptions.SectionName));
builder.Services.AddSingleton<PendingRequestStore>();
builder.Services.AddSingleton<UnitySocketHub>();
builder.Services.AddSingleton<UnityRelayService>();
builder.Services.AddSingleton<IUnityJsonRpcForwarder, UnityJsonRpcForwarder>();
builder.Services.AddSingleton<IUnityConnectionStatusProvider, UnityConnectionStatusProvider>();
builder.Services.AddSingleton<McpToolCatalog>();
builder.Services.AddSingleton<McpRequestHandler>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/mcp", StringComparison.Ordinal))
    {
        context.Response.OnStarting(() =>
        {
            if (string.IsNullOrWhiteSpace(context.Response.ContentType))
            {
                context.Response.ContentType = "application/json";
            }

            return Task.CompletedTask;
        });
    }

    await next();
});

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

app.MapGet("/", (IOptions<ServerOptions> options) =>
{
    return Results.Ok(new
    {
        service = "UnityMCP Server",
        transport = "WebSocket + MCP HTTP",
        endpoints = new[] { "/mcp", "/ws/cli", "/ws/unity" },
        port = options.Value.Port
    });
});

app.MapMethods("/mcp", new[] { "GET", "OPTIONS" }, (HttpContext context) =>
{
    context.Response.Headers.Allow = "POST, GET, OPTIONS";

    if (HttpMethods.IsOptions(context.Request.Method))
    {
        return Results.Json(new
        {
            ok = true,
            endpoint = "/mcp",
            methods = new[] { "POST" }
        });
    }

    return Results.Json(
        new
        {
            error = "Method Not Allowed",
            message = "Use POST /mcp for MCP JSON-RPC requests."
        },
        statusCode: StatusCodes.Status405MethodNotAllowed);
});

app.MapPost("/mcp", HandleMcpPostAsync);

app.MapMethods("/mcp/{**rest}", new[] { "GET", "OPTIONS" }, (HttpContext context) =>
{
    context.Response.Headers.Allow = "POST, GET, OPTIONS";
    return Results.Json(
        new
        {
            error = "Not Found",
            message = $"Unsupported MCP path '{context.Request.Path}'. Use /mcp."
        },
        statusCode: StatusCodes.Status404NotFound);
});

app.Map("/ws/unity", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("WebSocket request required.");
        return;
    }

    var hub = context.RequestServices.GetRequiredService<UnitySocketHub>();
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("UnitySocketEndpoint");

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    logger.LogInformation("Accepted Unity WebSocket connection from {RemoteIp}", context.Connection.RemoteIpAddress);
    await hub.AttachAsync(socket, context.RequestAborted);
});

app.Map("/ws/cli", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("WebSocket request required.");
        return;
    }

    var relay = context.RequestServices.GetRequiredService<UnityRelayService>();
    var loggerFactory = context.RequestServices.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("CliSocketEndpoint");

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    logger.LogInformation("Accepted CLI WebSocket connection from {RemoteIp}", context.Connection.RemoteIpAddress);

    await HandleCliSessionAsync(socket, relay, logger, context.RequestAborted);
});

var options = app.Services.GetRequiredService<IOptions<ServerOptions>>();
app.Urls.Clear();
app.Urls.Add($"http://127.0.0.1:{options.Value.Port}");

app.Run();

static async Task HandleMcpPostAsync(HttpContext context, McpRequestHandler mcpHandler, CancellationToken cancellationToken)
{
    try
    {
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8);
        var requestBody = await reader.ReadToEndAsync(cancellationToken);
        var response = await mcpHandler.HandlePostAsync(requestBody, cancellationToken);

        context.Response.ContentType = string.IsNullOrWhiteSpace(response.ContentType)
            ? "application/json"
            : response.ContentType;
        context.Response.StatusCode = response.StatusCode;
        if (!string.IsNullOrEmpty(response.Body))
        {
            await context.Response.WriteAsync(response.Body, cancellationToken);
        }
    }
    catch (OperationCanceledException)
    {
        throw;
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("McpEndpoint");
        logger.LogError(ex, "Unhandled /mcp request error.");

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            JsonRpcProtocol.CreateError(idNode: null, code: JsonRpcErrorCodes.InternalError, message: "Internal server error."),
            cancellationToken);
    }
}

static async Task HandleCliSessionAsync(
    WebSocket socket,
    UnityRelayService relay,
    ILogger logger,
    CancellationToken cancellationToken)
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
            logger.LogWarning(ex, "CLI socket receive loop ended due to WebSocket error.");
            break;
        }

        if (message is null)
        {
            break;
        }

        if (!JsonRpcProtocol.TryParse(message, out var document, out var parseError))
        {
            var parseResponse = JsonRpcProtocol.CreateError(
                idNode: null,
                code: JsonRpcErrorCodes.ParseError,
                message: $"Invalid JSON: {parseError}");

            await SendAsync(socket, parseResponse, cancellationToken);
            continue;
        }

        using var parsedDocument = document!;
        {
            var root = parsedDocument.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                var invalidRequest = JsonRpcProtocol.CreateError(
                    idNode: null,
                    code: JsonRpcErrorCodes.InvalidRequest,
                    message: "JSON-RPC payload must be an object.");

                await SendAsync(socket, invalidRequest, cancellationToken);
                continue;
            }

            if (!JsonRpcProtocol.TryGetId(root, out var idNode, out var idKey) || string.IsNullOrWhiteSpace(idKey))
            {
                var invalidRequest = JsonRpcProtocol.CreateError(
                    idNode: null,
                    code: JsonRpcErrorCodes.InvalidRequest,
                    message: "JSON-RPC request must include a string or numeric id.");

                await SendAsync(socket, invalidRequest, cancellationToken);
                continue;
            }

            if (!JsonRpcProtocol.TryGetMethod(root, out var method) || string.IsNullOrWhiteSpace(method))
            {
                var invalidRequest = JsonRpcProtocol.CreateError(
                    idNode,
                    JsonRpcErrorCodes.InvalidRequest,
                    "JSON-RPC request must include a method name.");

                await SendAsync(socket, invalidRequest, cancellationToken);
                continue;
            }

            if (string.Equals(method, "ping", StringComparison.Ordinal))
            {
                var pingResult = new JsonObject
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
                var responseJson = await relay.ForwardToUnityAsync(message, idKey!, cancellationToken);
                await SendAsync(socket, responseJson, cancellationToken);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Duplicate JSON-RPC request id", StringComparison.Ordinal))
            {
                var error = JsonRpcProtocol.CreateError(idNode, JsonRpcErrorCodes.DuplicateRequestId, ex.Message);
                await SendAsync(socket, error, cancellationToken);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Unity is not connected", StringComparison.Ordinal))
            {
                var error = JsonRpcProtocol.CreateError(idNode, JsonRpcErrorCodes.UnityNotConnected, ex.Message);
                await SendAsync(socket, error, cancellationToken);
            }
            catch (TimeoutException)
            {
                var error = JsonRpcProtocol.CreateError(idNode, JsonRpcErrorCodes.UnityTimeout, "Timed out waiting for Unity response.");
                await SendAsync(socket, error, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled CLI request processing error.");
                var error = JsonRpcProtocol.CreateError(idNode, JsonRpcErrorCodes.InternalError, "Internal server error.");
                await SendAsync(socket, error, cancellationToken);
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

static async Task SendAsync(WebSocket socket, string payload, CancellationToken cancellationToken)
{
    if (socket.State != WebSocketState.Open)
    {
        return;
    }

    var bytes = Encoding.UTF8.GetBytes(payload);
    await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
}

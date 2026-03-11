using System.Text;
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
builder.Services.AddTransient<CliSessionHandler>();

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

    var cliHandler = context.RequestServices.GetRequiredService<CliSessionHandler>();
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("CliSocketEndpoint");

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    logger.LogInformation("Accepted CLI WebSocket connection from {RemoteIp}", context.Connection.RemoteIpAddress);

    await cliHandler.HandleAsync(socket, context.RequestAborted);
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

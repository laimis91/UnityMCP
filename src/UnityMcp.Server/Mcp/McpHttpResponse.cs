namespace UnityMcp.Server.Mcp;

public sealed record McpHttpResponse(int StatusCode, string? Body, string ContentType = "application/json");

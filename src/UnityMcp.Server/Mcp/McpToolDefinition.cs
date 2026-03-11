using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp;

public sealed record McpToolDefinition(string Name, string Description, JsonObject InputSchema);

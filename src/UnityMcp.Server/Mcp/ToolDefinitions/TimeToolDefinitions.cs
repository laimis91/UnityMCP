using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp.ToolDefinitions;

internal static class TimeToolDefinitions
{
    internal static McpToolDefinition[] GetTools()
    {
        return new[]
        {
            new McpToolDefinition(
                "time.getSettings",
                "Returns Unity Time settings.",
                McpToolSchemaHelpers.EmptyObjectSchema()),
            new McpToolDefinition(
                "time.setSettings",
                "Mutates Unity Time settings.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new JsonObject
                    {
                        ["timeScale"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["fixedDeltaTime"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["maximumDeltaTime"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["captureDeltaTime"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 }
                    }
                })
        };
    }
}
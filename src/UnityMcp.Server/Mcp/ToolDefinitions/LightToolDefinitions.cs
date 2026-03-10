using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp.ToolDefinitions;

internal static class LightToolDefinitions
{
    internal static McpToolDefinition[] GetTools()
    {
        return new[]
        {
            new McpToolDefinition(
                "light.getSettings",
                "Returns common Light settings for a Light component target (or a GameObject with a single Light).",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" }
                    }
                }),
            new McpToolDefinition(
                "light.setSettings",
                "Mutates common Light settings using direct Unity Light APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["type"] = McpToolSchemaHelpers.EnumLikeSchema("Light type as enum name or integer value."),
                        ["color"] = McpToolSchemaHelpers.ColorSchema("RGBA color array [r,g,b,a]."),
                        ["intensity"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["range"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["spotAngle"] = new JsonObject { ["type"] = "number", ["description"] = "Spot angle in degrees (only valid for Spot lights)." },
                        ["shadows"] = McpToolSchemaHelpers.EnumLikeSchema("Light shadows mode as enum name or integer value.")
                    }
                })
        };
    }
}
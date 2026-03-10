using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp.ToolDefinitions;

internal static class TerrainToolDefinitions
{
    internal static McpToolDefinition[] GetTools()
    {
        return new[]
        {
            new McpToolDefinition(
                "terrain.getSettings",
                "Returns Terrain settings for a Terrain component target (or a GameObject with a single Terrain).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a Terrain component or a GameObject with a single Terrain.")),
            new McpToolDefinition(
                "terrain.setSettings",
                "Mutates Terrain settings using direct Unity Terrain APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["groupingID"] = new JsonObject { ["type"] = "integer" },
                        ["allowAutoConnect"] = new JsonObject { ["type"] = "boolean" },
                        ["drawHeightmap"] = new JsonObject { ["type"] = "boolean" },
                        ["drawInstanced"] = new JsonObject { ["type"] = "boolean" },
                        ["heightmapPixelError"] = new JsonObject { ["type"] = "number", ["minimum"] = 1, ["maximum"] = 200 },
                        ["basemapDistance"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["lightmapIndex"] = new JsonObject { ["type"] = "integer" },
                        ["realtimeLightmapIndex"] = new JsonObject { ["type"] = "integer" },
                        ["shadowCastingMode"] = McpToolSchemaHelpers.EnumLikeSchema("ShadowCastingMode enum name or integer value."),
                        ["reflectionProbeUsage"] = McpToolSchemaHelpers.EnumLikeSchema("ReflectionProbeUsage enum name or integer value.")
                    }
                })
        };
    }
}
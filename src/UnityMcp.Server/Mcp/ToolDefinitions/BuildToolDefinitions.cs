using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp.ToolDefinitions;

internal static class BuildToolDefinitions
{
    internal static McpToolDefinition[] GetTools()
    {
        return new[]
        {
            new McpToolDefinition(
                "build.getSettings",
                "Returns Unity build settings.",
                McpToolSchemaHelpers.EmptyObjectSchema()),
            new McpToolDefinition(
                "build.setSettings",
                "Mutates Unity build settings.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new JsonObject
                    {
                        ["target"] = McpToolSchemaHelpers.EnumLikeSchema("BuildTarget enum name or integer value."),
                        ["development"] = new JsonObject { ["type"] = "boolean" },
                        ["allowDebugging"] = new JsonObject { ["type"] = "boolean" },
                        ["connectProfiler"] = new JsonObject { ["type"] = "boolean" },
                        ["buildAppBundle"] = new JsonObject { ["type"] = "boolean" },
                        ["symlinkSources"] = new JsonObject { ["type"] = "boolean" },
                        ["uncompressedAssetBundle"] = new JsonObject { ["type"] = "boolean" },
                        ["detailedBuildReport"] = new JsonObject { ["type"] = "boolean" },
                        ["strictMode"] = new JsonObject { ["type"] = "boolean" }
                    }
                }),
            new McpToolDefinition(
                "build.build",
                "Starts a Unity build with the current settings.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("locationPath"),
                    ["properties"] = new JsonObject
                    {
                        ["locationPath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Path where the build output should be placed."
                        },
                        ["target"] = McpToolSchemaHelpers.EnumLikeSchema("BuildTarget enum name or integer value."),
                        ["options"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["description"] = "BuildOptions flags.",
                            ["items"] = new JsonObject { ["type"] = "string" }
                        },
                        ["scenes"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["description"] = "Scene paths to include in build (default: all enabled scenes).",
                            ["items"] = new JsonObject { ["type"] = "string" }
                        }
                    }
                })
        };
    }
}
using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp.ToolDefinitions;

internal static class AnimatorToolDefinitions
{
    internal static McpToolDefinition[] GetTools()
    {
        return new[]
        {
            new McpToolDefinition(
                "animator.getSettings",
                "Returns Animator settings for an Animator component target (or a GameObject with a single Animator).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of an Animator component or a GameObject with a single Animator.")),
            new McpToolDefinition(
                "animator.setSettings",
                "Mutates Animator settings using direct Unity Animator APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["runtimeAnimatorController"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity asset path to the RuntimeAnimatorController."
                        },
                        ["avatar"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity asset path to the Avatar."
                        },
                        ["applyRootMotion"] = new JsonObject { ["type"] = "boolean" },
                        ["updateMode"] = McpToolSchemaHelpers.EnumLikeSchema("AnimatorUpdateMode enum name or integer value."),
                        ["cullingMode"] = McpToolSchemaHelpers.EnumLikeSchema("AnimatorCullingMode enum name or integer value."),
                        ["speed"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "animator.getParameters",
                "Returns Animator controller parameters for an Animator component target.",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of an Animator component or a GameObject with a single Animator.")),
            new McpToolDefinition(
                "animator.setParameter",
                "Sets an Animator controller parameter value.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId", "parameterName", "value"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["parameterName"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Name of the parameter in the Animator controller."
                        },
                        ["value"] = new JsonObject
                        {
                            ["description"] = "Parameter value (type depends on parameter: bool, int, float, or trigger).",
                            ["oneOf"] = new JsonArray
                            {
                                new JsonObject { ["type"] = "boolean" },
                                new JsonObject { ["type"] = "integer" },
                                new JsonObject { ["type"] = "number" }
                            }
                        }
                    }
                })
        };
    }
}
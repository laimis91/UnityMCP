using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp;

public sealed partial class McpToolCatalog
{
    private static IEnumerable<McpToolDefinition> GetAnimatorTools()
    {
        yield return new McpToolDefinition(
            "animator.getSettings",
            "Returns Animator component settings for the target.",
            InstanceIdOnlySchema("Unity instance id of an Animator component or a GameObject with a single Animator."));

        yield return new McpToolDefinition(
            "animator.setSettings",
            "Mutates Animator component settings.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer" },
                    ["enabled"] = new JsonObject { ["type"] = "boolean" },
                    ["speed"] = new JsonObject { ["type"] = "number" },
                    ["applyRootMotion"] = new JsonObject { ["type"] = "boolean" },
                    ["updateMode"] = EnumLikeSchema("AnimatorUpdateMode enum name or integer value."),
                    ["cullingMode"] = EnumLikeSchema("AnimatorCullingMode enum name or integer value.")
                }
            });

        yield return new McpToolDefinition(
            "animator.getParameters",
            "Returns the list of Animator controller parameters.",
            InstanceIdOnlySchema("Unity instance id of an Animator component or a GameObject with a single Animator."));

        yield return new McpToolDefinition(
            "animator.setParameter",
            "Sets an Animator parameter value by name.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId", "parameterName", "value"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer" },
                    ["parameterName"] = new JsonObject { ["type"] = "string", ["description"] = "Animator parameter name." },
                    ["value"] = new JsonObject
                    {
                        ["description"] = "Parameter value. Use a boolean for Bool, integer for Int, number for Float. Trigger parameters ignore the value.",
                        ["oneOf"] = new JsonArray
                        {
                            new JsonObject { ["type"] = "boolean" },
                            new JsonObject { ["type"] = "integer" },
                            new JsonObject { ["type"] = "number" },
                            new JsonObject { ["type"] = "null" }
                        }
                    }
                }
            });
    }
}

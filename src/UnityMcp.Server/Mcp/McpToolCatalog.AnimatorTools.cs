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

        yield return new McpToolDefinition(
            "animationClip.getProperties",
            "Returns properties of an AnimationClip asset.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("assetPath"),
                ["properties"] = new JsonObject
                {
                    ["assetPath"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Project-relative asset path of the AnimationClip (e.g. Assets/Animations/Walk.anim)."
                    }
                }
            });

        yield return new McpToolDefinition(
            "animationClip.setProperties",
            "Modifies writable properties of an AnimationClip asset.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("assetPath"),
                ["properties"] = new JsonObject
                {
                    ["assetPath"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Project-relative asset path of the AnimationClip."
                    },
                    ["frameRate"] = new JsonObject
                    {
                        ["type"] = "number",
                        ["description"] = "Clip frame rate (frames per second).",
                        ["exclusiveMinimum"] = 0
                    },
                    ["wrapMode"] = EnumLikeSchema("WrapMode enum name or integer value. Valid names: Default, Once, Loop, PingPong, ClampForever."),
                    ["legacy"] = new JsonObject { ["type"] = "boolean", ["description"] = "Whether the clip uses the legacy animation system." }
                }
            });

        yield return new McpToolDefinition(
            "animationClip.getCurveBindings",
            "Lists all animated property curve bindings in an AnimationClip asset.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("assetPath"),
                ["properties"] = new JsonObject
                {
                    ["assetPath"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Project-relative asset path of the AnimationClip."
                    },
                    ["maxResults"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["description"] = "Optional cap for returned curve bindings (1-500).",
                        ["minimum"] = 1,
                        ["maximum"] = 500
                    }
                }
            });

        yield return new McpToolDefinition(
            "animationClip.getEvents",
            "Returns the list of animation events on an AnimationClip asset.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("assetPath"),
                ["properties"] = new JsonObject
                {
                    ["assetPath"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Project-relative asset path of the AnimationClip."
                    }
                }
            });

        yield return new McpToolDefinition(
            "animationClip.setEvents",
            "Replaces the animation events on an AnimationClip asset.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("assetPath", "events"),
                ["properties"] = new JsonObject
                {
                    ["assetPath"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Project-relative asset path of the AnimationClip."
                    },
                    ["events"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["description"] = "Array of animation events to set on the clip (replaces existing events).",
                        ["items"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject
                            {
                                ["time"] = new JsonObject { ["type"] = "number", ["description"] = "Time in seconds at which the event fires." },
                                ["functionName"] = new JsonObject { ["type"] = "string", ["description"] = "Name of the function to call." },
                                ["stringParameter"] = new JsonObject { ["type"] = "string", ["description"] = "Optional string parameter." },
                                ["floatParameter"] = new JsonObject { ["type"] = "number", ["description"] = "Optional float parameter." },
                                ["intParameter"] = new JsonObject { ["type"] = "integer", ["description"] = "Optional int parameter." }
                            }
                        }
                    }
                }
            });
    }
}

using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp;

public sealed partial class McpToolCatalog
{
    private static IEnumerable<McpToolDefinition> GetCameraLightTools()
    {
        yield return new McpToolDefinition(
            "camera.getSettings",
            "Returns common Camera settings for a Camera component target (or a GameObject with a single Camera).",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["description"] = "Unity instance id of a Camera component or a GameObject with a single Camera."
                    }
                }
            });

        yield return new McpToolDefinition(
            "camera.setSettings",
            "Mutates common Camera settings using direct Unity Camera APIs.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer" },
                    ["enabled"] = new JsonObject { ["type"] = "boolean" },
                    ["orthographic"] = new JsonObject { ["type"] = "boolean" },
                    ["fieldOfView"] = new JsonObject { ["type"] = "number", ["description"] = "Perspective FOV in degrees (0-179)." },
                    ["orthographicSize"] = new JsonObject { ["type"] = "number", ["description"] = "Orthographic half-size (>0)." },
                    ["nearClipPlane"] = new JsonObject { ["type"] = "number", ["description"] = "Near clip plane (>0)." },
                    ["farClipPlane"] = new JsonObject { ["type"] = "number", ["description"] = "Far clip plane (> nearClipPlane)." },
                    ["clearFlags"] = EnumLikeSchema("Camera clear flags as enum name or integer value."),
                    ["backgroundColor"] = ColorSchema("RGBA color array [r,g,b,a]."),
                    ["depth"] = new JsonObject { ["type"] = "number" }
                }
            });

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
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
                    ["type"] = EnumLikeSchema("Light type as enum name or integer value."),
                    ["color"] = ColorSchema("RGBA color array [r,g,b,a]."),
                    ["intensity"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                    ["range"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                    ["spotAngle"] = new JsonObject { ["type"] = "number", ["description"] = "Spot angle in degrees (only valid for Spot lights)." },
                    ["shadows"] = EnumLikeSchema("Light shadows mode as enum name or integer value.")
                }
            });

        yield return new McpToolDefinition(
            "camera.getProjection",
            "Returns camera projection settings: orthographic, orthographicSize, fieldOfView, nearClipPlane, farClipPlane, aspect.",
            InstanceIdOnlySchema("Instance ID of a Camera component or a GameObject with a Camera."));

        yield return new McpToolDefinition(
            "camera.setProjection",
            "Sets camera projection settings: orthographic, orthographicSize, fieldOfView, nearClipPlane, farClipPlane.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer", ["description"] = "Instance ID of a Camera component or a GameObject with a Camera." },
                    ["orthographic"] = new JsonObject { ["type"] = "boolean", ["description"] = "Enable orthographic projection." },
                    ["orthographicSize"] = new JsonObject { ["type"] = "number", ["description"] = "Orthographic camera half-size." },
                    ["fieldOfView"] = new JsonObject { ["type"] = "number", ["description"] = "Field of view in degrees (perspective mode)." },
                    ["nearClipPlane"] = new JsonObject { ["type"] = "number", ["description"] = "Near clipping plane distance." },
                    ["farClipPlane"] = new JsonObject { ["type"] = "number", ["description"] = "Far clipping plane distance." }
                }
            });
    }
}

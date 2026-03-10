using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp.ToolDefinitions;

internal static class CameraToolDefinitions
{
    internal static McpToolDefinition[] GetTools()
    {
        return new[]
        {
            new McpToolDefinition(
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
                }),
            new McpToolDefinition(
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
                        ["clearFlags"] = McpToolSchemaHelpers.EnumLikeSchema("Camera clear flags as enum name or integer value."),
                        ["backgroundColor"] = McpToolSchemaHelpers.ColorSchema("RGBA color array [r,g,b,a]."),
                        ["depth"] = new JsonObject { ["type"] = "number" }
                    }
                }),
            new McpToolDefinition(
                "camera.getProjection",
                "Returns the Camera's projection matrix.",
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
                }),
            new McpToolDefinition(
                "camera.setProjection",
                "Sets the Camera's projection matrix.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId", "projectionMatrix"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["projectionMatrix"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["description"] = "4x4 projection matrix as 16-element array [m00,m01,...,m33].",
                            ["minItems"] = 16,
                            ["maxItems"] = 16,
                            ["items"] = new JsonObject { ["type"] = "number" }
                        }
                    }
                })
        };
    }
}
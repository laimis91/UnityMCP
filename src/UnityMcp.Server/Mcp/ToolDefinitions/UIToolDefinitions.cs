using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp.ToolDefinitions;

internal static class UIToolDefinitions
{
    internal static McpToolDefinition[] GetTools()
    {
        return new[]
        {
            new McpToolDefinition(
                "rectTransform.getSettings",
                "Returns RectTransform settings for a RectTransform component target (or a GameObject with a single RectTransform).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a RectTransform component or a GameObject with a single RectTransform.")),
            new McpToolDefinition(
                "rectTransform.setSettings",
                "Mutates RectTransform settings using direct Unity RectTransform APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["anchoredPosition"] = McpToolSchemaHelpers.Vector2Schema("RectTransform anchored position [x,y]."),
                        ["sizeDelta"] = McpToolSchemaHelpers.Vector2Schema("RectTransform size delta [x,y]."),
                        ["anchorMin"] = McpToolSchemaHelpers.Vector2Schema("RectTransform anchor minimum [x,y]."),
                        ["anchorMax"] = McpToolSchemaHelpers.Vector2Schema("RectTransform anchor maximum [x,y]."),
                        ["pivot"] = McpToolSchemaHelpers.Vector2Schema("RectTransform pivot [x,y]."),
                        ["offsetMin"] = McpToolSchemaHelpers.Vector2Schema("RectTransform offset minimum [x,y]."),
                        ["offsetMax"] = McpToolSchemaHelpers.Vector2Schema("RectTransform offset maximum [x,y]."),
                        ["anchoredPosition3D"] = McpToolSchemaHelpers.Vector3Schema("RectTransform anchored position 3D [x,y,z]."),
                        ["rotation"] = McpToolSchemaHelpers.Vector3Schema("RectTransform rotation [x,y,z] as Euler angles in degrees."),
                        ["localScale"] = McpToolSchemaHelpers.Vector3Schema("RectTransform local scale [x,y,z].")
                    }
                }),
            new McpToolDefinition(
                "canvas.getSettings",
                "Returns Canvas settings for a Canvas component target (or a GameObject with a single Canvas).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a Canvas component or a GameObject with a single Canvas.")),
            new McpToolDefinition(
                "canvas.setSettings",
                "Mutates Canvas settings using direct Unity Canvas APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["renderMode"] = McpToolSchemaHelpers.EnumLikeSchema("RenderMode enum name or integer value."),
                        ["scaleFactor"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["referencePixelsPerUnit"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["overridePixelPerfect"] = new JsonObject { ["type"] = "boolean" },
                        ["pixelPerfect"] = new JsonObject { ["type"] = "boolean" },
                        ["planeDistance"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["overrideSorting"] = new JsonObject { ["type"] = "boolean" },
                        ["sortingLayerName"] = new JsonObject { ["type"] = "string" },
                        ["sortingOrder"] = new JsonObject { ["type"] = "integer" },
                        ["targetDisplay"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0 },
                        ["additionalShaderChannels"] = McpToolSchemaHelpers.EnumLikeSchema("AdditionalCanvasShaderChannels enum flags or integer value.")
                    }
                }),
            new McpToolDefinition(
                "canvasGroup.getSettings",
                "Returns CanvasGroup settings for a CanvasGroup component target (or a GameObject with a single CanvasGroup).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a CanvasGroup component or a GameObject with a single CanvasGroup.")),
            new McpToolDefinition(
                "canvasGroup.setSettings",
                "Mutates CanvasGroup settings using direct Unity CanvasGroup APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["alpha"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1 },
                        ["interactable"] = new JsonObject { ["type"] = "boolean" },
                        ["blocksRaycasts"] = new JsonObject { ["type"] = "boolean" },
                        ["ignoreParentGroups"] = new JsonObject { ["type"] = "boolean" }
                    }
                })
        };
    }
}
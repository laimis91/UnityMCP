using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp.ToolDefinitions;

internal static class RenderersToolDefinitions
{
    internal static McpToolDefinition[] GetTools()
    {
        return new[]
        {
            new McpToolDefinition(
                "meshRenderer.getSettings",
                "Returns MeshRenderer settings for a MeshRenderer component target (or a GameObject with a single MeshRenderer).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a MeshRenderer component or a GameObject with a single MeshRenderer.")),
            new McpToolDefinition(
                "meshRenderer.setSettings",
                "Mutates MeshRenderer settings using direct Unity MeshRenderer APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["shadowCastingMode"] = McpToolSchemaHelpers.EnumLikeSchema("ShadowCastingMode enum name or integer value."),
                        ["receiveShadows"] = new JsonObject { ["type"] = "boolean" },
                        ["lightProbeUsage"] = McpToolSchemaHelpers.EnumLikeSchema("LightProbeUsage enum name or integer value."),
                        ["reflectionProbeUsage"] = McpToolSchemaHelpers.EnumLikeSchema("ReflectionProbeUsage enum name or integer value."),
                        ["motionVectorGenerationMode"] = McpToolSchemaHelpers.EnumLikeSchema("MotionVectorGenerationMode enum name or integer value."),
                        ["allowOcclusionWhenDynamic"] = new JsonObject { ["type"] = "boolean" }
                    }
                }),
            new McpToolDefinition(
                "spriteRenderer.getSettings",
                "Returns SpriteRenderer settings for a SpriteRenderer component target (or a GameObject with a single SpriteRenderer).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a SpriteRenderer component or a GameObject with a single SpriteRenderer.")),
            new McpToolDefinition(
                "spriteRenderer.setSettings",
                "Mutates SpriteRenderer settings using direct Unity SpriteRenderer APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["sprite"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity asset path to the Sprite."
                        },
                        ["color"] = McpToolSchemaHelpers.ColorSchema("RGBA color array [r,g,b,a]."),
                        ["flipX"] = new JsonObject { ["type"] = "boolean" },
                        ["flipY"] = new JsonObject { ["type"] = "boolean" },
                        ["sortingLayerName"] = new JsonObject { ["type"] = "string" },
                        ["sortingOrder"] = new JsonObject { ["type"] = "integer" },
                        ["material"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity asset path to the Material."
                        }
                    }
                }),
            new McpToolDefinition(
                "skinnedMeshRenderer.getSettings",
                "Returns SkinnedMeshRenderer settings for a SkinnedMeshRenderer component target (or a GameObject with a single SkinnedMeshRenderer).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a SkinnedMeshRenderer component or a GameObject with a single SkinnedMeshRenderer.")),
            new McpToolDefinition(
                "skinnedMeshRenderer.setSettings",
                "Mutates SkinnedMeshRenderer settings using direct Unity SkinnedMeshRenderer APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["quality"] = McpToolSchemaHelpers.EnumLikeSchema("SkinQuality enum name or integer value."),
                        ["updateWhenOffscreen"] = new JsonObject { ["type"] = "boolean" },
                        ["rootBone"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Unity instance id of the root bone Transform."
                        },
                        ["shadowCastingMode"] = McpToolSchemaHelpers.EnumLikeSchema("ShadowCastingMode enum name or integer value."),
                        ["receiveShadows"] = new JsonObject { ["type"] = "boolean" }
                    }
                }),
            new McpToolDefinition(
                "lineRenderer.getSettings",
                "Returns LineRenderer settings for a LineRenderer component target (or a GameObject with a single LineRenderer).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a LineRenderer component or a GameObject with a single LineRenderer.")),
            new McpToolDefinition(
                "lineRenderer.setSettings",
                "Mutates LineRenderer settings using direct Unity LineRenderer APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["material"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity asset path to the Material."
                        },
                        ["color"] = McpToolSchemaHelpers.ColorSchema("RGBA color array [r,g,b,a]."),
                        ["startWidth"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["endWidth"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["startColor"] = McpToolSchemaHelpers.ColorSchema("Start color RGBA array [r,g,b,a]."),
                        ["endColor"] = McpToolSchemaHelpers.ColorSchema("End color RGBA array [r,g,b,a]."),
                        ["positionCount"] = new JsonObject { ["type"] = "integer", ["minimum"] = 2 },
                        ["useWorldSpace"] = new JsonObject { ["type"] = "boolean" },
                        ["loop"] = new JsonObject { ["type"] = "boolean" }
                    }
                }),
            new McpToolDefinition(
                "lodGroup.getSettings",
                "Returns LODGroup settings for a LODGroup component target (or a GameObject with a single LODGroup).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a LODGroup component or a GameObject with a single LODGroup.")),
            new McpToolDefinition(
                "lodGroup.setSettings",
                "Mutates LODGroup settings using direct Unity LODGroup APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["fadeMode"] = McpToolSchemaHelpers.EnumLikeSchema("LODFadeMode enum name or integer value."),
                        ["animateCrossFading"] = new JsonObject { ["type"] = "boolean" },
                        ["size"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "renderer.getMaterials",
                "Returns materials array for a Renderer component target (or a GameObject with a single Renderer).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a Renderer component or a GameObject with a single Renderer.")),
            new McpToolDefinition(
                "renderer.setMaterial",
                "Sets a material at a specific index on a Renderer component.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId", "materialIndex", "materialPath"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["materialIndex"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["minimum"] = 0,
                            ["description"] = "Index of the material slot to set."
                        },
                        ["materialPath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity asset path to the Material."
                        }
                    }
                })
        };
    }
}
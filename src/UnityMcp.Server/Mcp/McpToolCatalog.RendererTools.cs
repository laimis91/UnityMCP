using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp;

public sealed partial class McpToolCatalog
{
    private static IEnumerable<McpToolDefinition> GetRendererTools()
    {
        yield return new McpToolDefinition(
            "meshRenderer.getSettings",
            "Returns MeshRenderer settings for the target.",
            InstanceIdOnlySchema("Unity instance id of a MeshRenderer component or a GameObject with a single MeshRenderer."));

        yield return new McpToolDefinition(
            "meshRenderer.setSettings",
            "Mutates MeshRenderer settings.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer" },
                    ["enabled"] = new JsonObject { ["type"] = "boolean" },
                    ["shadowCastingMode"] = EnumLikeSchema("ShadowCastingMode enum name or integer value."),
                    ["receiveShadows"] = new JsonObject { ["type"] = "boolean" },
                    ["lightProbeUsage"] = EnumLikeSchema("LightProbeUsage enum name or integer value."),
                    ["reflectionProbeUsage"] = EnumLikeSchema("ReflectionProbeUsage enum name or integer value."),
                    ["motionVectorGenerationMode"] = EnumLikeSchema("MotionVectorGenerationMode enum name or integer value."),
                    ["staticShadowCaster"] = new JsonObject { ["type"] = "boolean" },
                    ["allowOcclusionWhenDynamic"] = new JsonObject { ["type"] = "boolean" }
                }
            });

        yield return new McpToolDefinition(
            "skinnedMeshRenderer.getSettings",
            "Returns SkinnedMeshRenderer component settings for the target (includes MeshRenderer fields plus rootBone, quality, updateWhenOffscreen).",
            InstanceIdOnlySchema("Unity instance id of a SkinnedMeshRenderer component or a GameObject with a single SkinnedMeshRenderer."));

        yield return new McpToolDefinition(
            "skinnedMeshRenderer.setSettings",
            "Mutates SkinnedMeshRenderer component settings.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer" },
                    ["enabled"] = new JsonObject { ["type"] = "boolean" },
                    ["shadowCastingMode"] = EnumLikeSchema("ShadowCastingMode enum name or integer value."),
                    ["receiveShadows"] = new JsonObject { ["type"] = "boolean" },
                    ["lightProbeUsage"] = EnumLikeSchema("LightProbeUsage enum name or integer value."),
                    ["reflectionProbeUsage"] = EnumLikeSchema("ReflectionProbeUsage enum name or integer value."),
                    ["motionVectorGenerationMode"] = EnumLikeSchema("MotionVectorGenerationMode enum name or integer value."),
                    ["staticShadowCaster"] = new JsonObject { ["type"] = "boolean" },
                    ["allowOcclusionWhenDynamic"] = new JsonObject { ["type"] = "boolean" },
                    ["quality"] = EnumLikeSchema("SkinQuality enum name or integer value."),
                    ["updateWhenOffscreen"] = new JsonObject { ["type"] = "boolean" }
                }
            });

        yield return new McpToolDefinition(
            "spriteRenderer.getSettings",
            "Returns SpriteRenderer settings: spriteName, color, flipX, flipY, sortingLayerName, sortingOrder, drawMode, maskInteraction.",
            InstanceIdOnlySchema("Instance ID of a SpriteRenderer component or a GameObject with a SpriteRenderer."));

        yield return new McpToolDefinition(
            "spriteRenderer.setSettings",
            "Sets SpriteRenderer settings: color, flipX, flipY, sortingLayerName, sortingOrder.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer", ["description"] = "Instance ID of a SpriteRenderer component or a GameObject with a SpriteRenderer." },
                    ["color"] = ColorSchema("Sprite color as [r, g, b, a]."),
                    ["flipX"] = new JsonObject { ["type"] = "boolean", ["description"] = "Flip the sprite horizontally." },
                    ["flipY"] = new JsonObject { ["type"] = "boolean", ["description"] = "Flip the sprite vertically." },
                    ["sortingLayerName"] = new JsonObject { ["type"] = "string", ["description"] = "Sorting layer name." },
                    ["sortingOrder"] = new JsonObject { ["type"] = "integer", ["description"] = "Order within sorting layer." }
                }
            });

        yield return new McpToolDefinition(
            "lineRenderer.getSettings",
            "Returns LineRenderer settings: positionCount, positions, loop, startWidth, endWidth, useWorldSpace, startColor, endColor.",
            InstanceIdOnlySchema("Instance ID of a LineRenderer component or a GameObject with a LineRenderer."));

        yield return new McpToolDefinition(
            "lineRenderer.setSettings",
            "Sets LineRenderer settings: loop, startWidth, endWidth, useWorldSpace, startColor, endColor.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer", ["description"] = "Instance ID of a LineRenderer component or a GameObject with a LineRenderer." },
                    ["loop"] = new JsonObject { ["type"] = "boolean", ["description"] = "Connect the first and last positions." },
                    ["startWidth"] = new JsonObject { ["type"] = "number", ["description"] = "Width at the start of the line." },
                    ["endWidth"] = new JsonObject { ["type"] = "number", ["description"] = "Width at the end of the line." },
                    ["useWorldSpace"] = new JsonObject { ["type"] = "boolean", ["description"] = "Use world space coordinates." },
                    ["startColor"] = ColorSchema("Line start color as [r, g, b, a]."),
                    ["endColor"] = ColorSchema("Line end color as [r, g, b, a].")
                }
            });

        yield return new McpToolDefinition(
            "lodGroup.getSettings",
            "Returns LODGroup settings: lodCount, fadeMode, animateCrossFading, size.",
            InstanceIdOnlySchema("Instance ID of a LODGroup component or a GameObject with a LODGroup."));

        yield return new McpToolDefinition(
            "lodGroup.setSettings",
            "Sets LODGroup settings: fadeMode, animateCrossFading.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer", ["description"] = "Instance ID of a LODGroup component or a GameObject with a LODGroup." },
                    ["fadeMode"] = EnumLikeSchema("LODFadeMode enum name or integer value."),
                    ["animateCrossFading"] = new JsonObject { ["type"] = "boolean", ["description"] = "Enable cross-fade animation." }
                }
            });

        yield return new McpToolDefinition(
            "renderer.getMaterials",
            "Returns array of material names and instance IDs for all materials on any Renderer component.",
            InstanceIdOnlySchema("Unity instance id of a Renderer component or a GameObject with a single Renderer."));

        yield return new McpToolDefinition(
            "renderer.setMaterial",
            "Assigns a material to a specific slot on a Renderer component by loading from asset path.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId", "materialIndex", "materialAssetPath"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer", ["description"] = "Unity instance id of a Renderer component or a GameObject with a single Renderer." },
                    ["materialIndex"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0, ["description"] = "Zero-based index of the material slot to assign." },
                    ["materialAssetPath"] = new JsonObject { ["type"] = "string", ["description"] = "Project-relative path to the material asset (e.g. Assets/Materials/MyMat.mat)." }
                }
            });
    }
}

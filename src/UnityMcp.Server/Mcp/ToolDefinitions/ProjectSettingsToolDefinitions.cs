using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp.ToolDefinitions;

internal static class ProjectSettingsToolDefinitions
{
    internal static McpToolDefinition[] GetTools()
    {
        return new[]
        {
            new McpToolDefinition(
                "projectSettings.getPlayerSettings",
                "Returns Unity Player Settings.",
                McpToolSchemaHelpers.EmptyObjectSchema()),
            new McpToolDefinition(
                "projectSettings.setPlayerSettings",
                "Mutates Unity Player Settings.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new JsonObject
                    {
                        ["companyName"] = new JsonObject { ["type"] = "string" },
                        ["productName"] = new JsonObject { ["type"] = "string" },
                        ["applicationVersion"] = new JsonObject { ["type"] = "string" },
                        ["bundleVersion"] = new JsonObject { ["type"] = "string" },
                        ["defaultIsNativeResolution"] = new JsonObject { ["type"] = "boolean" },
                        ["macRetinaSupport"] = new JsonObject { ["type"] = "boolean" },
                        ["runInBackground"] = new JsonObject { ["type"] = "boolean" },
                        ["captureSingleScreen"] = new JsonObject { ["type"] = "boolean" },
                        ["muteOtherAudioSources"] = new JsonObject { ["type"] = "boolean" },
                        ["prepareIOSForRecording"] = new JsonObject { ["type"] = "boolean" },
                        ["submitAnalytics"] = new JsonObject { ["type"] = "boolean" },
                        ["usePlayerLog"] = new JsonObject { ["type"] = "boolean" },
                        ["bakeCollisionMeshes"] = new JsonObject { ["type"] = "boolean" },
                        ["forceSingleInstance"] = new JsonObject { ["type"] = "boolean" },
                        ["useFlipModelSwapchain"] = new JsonObject { ["type"] = "boolean" },
                        ["resizableWindow"] = new JsonObject { ["type"] = "boolean" },
                        ["useMacAppStoreValidation"] = new JsonObject { ["type"] = "boolean" },
                        ["macAppStoreCategory"] = new JsonObject { ["type"] = "string" },
                        ["gpuSkinning"] = new JsonObject { ["type"] = "boolean" },
                        ["xboxPIXTextureCapture"] = new JsonObject { ["type"] = "boolean" },
                        ["xboxEnableAvatar"] = new JsonObject { ["type"] = "boolean" },
                        ["xboxEnableKinect"] = new JsonObject { ["type"] = "boolean" },
                        ["xboxEnableKinectAutoTracking"] = new JsonObject { ["type"] = "boolean" },
                        ["xboxEnableFitness"] = new JsonObject { ["type"] = "boolean" },
                        ["visibleInBackground"] = new JsonObject { ["type"] = "boolean" },
                        ["allowFullscreenSwitch"] = new JsonObject { ["type"] = "boolean" },
                        ["fullScreenMode"] = McpToolSchemaHelpers.EnumLikeSchema("FullScreenMode enum name or integer value."),
                        ["defaultScreenWidth"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 },
                        ["defaultScreenHeight"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 },
                        ["defaultWebScreenWidth"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 },
                        ["defaultWebScreenHeight"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 }
                    }
                }),
            new McpToolDefinition(
                "projectSettings.getQualitySettings",
                "Returns Unity Quality Settings.",
                McpToolSchemaHelpers.EmptyObjectSchema()),
            new McpToolDefinition(
                "projectSettings.setQualitySettings",
                "Mutates Unity Quality Settings.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new JsonObject
                    {
                        ["pixelLightCount"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0 },
                        ["shadows"] = McpToolSchemaHelpers.EnumLikeSchema("ShadowQuality enum name or integer value."),
                        ["shadowResolution"] = McpToolSchemaHelpers.EnumLikeSchema("ShadowResolution enum name or integer value."),
                        ["shadowProjection"] = McpToolSchemaHelpers.EnumLikeSchema("ShadowProjection enum name or integer value."),
                        ["shadowCascades"] = McpToolSchemaHelpers.EnumLikeSchema("ShadowCascades enum name or integer value."),
                        ["shadowDistance"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["shadowNearPlaneOffset"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["shadowCascade2Split"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1 },
                        ["shadowCascade4Split"] = McpToolSchemaHelpers.Vector3Schema("Shadow cascade 4 split [x,y,z]."),
                        ["shadowmaskMode"] = McpToolSchemaHelpers.EnumLikeSchema("ShadowmaskMode enum name or integer value."),
                        ["blendWeights"] = McpToolSchemaHelpers.EnumLikeSchema("BlendWeights enum name or integer value."),
                        ["textureQuality"] = McpToolSchemaHelpers.EnumLikeSchema("TextureQuality enum name or integer value."),
                        ["anisotropicTextures"] = McpToolSchemaHelpers.EnumLikeSchema("AnisotropicFiltering enum name or integer value."),
                        ["antiAliasing"] = new JsonObject { ["type"] = "integer", ["enum"] = new JsonArray(0, 2, 4, 8) },
                        ["softParticles"] = new JsonObject { ["type"] = "boolean" },
                        ["softVegetation"] = new JsonObject { ["type"] = "boolean" },
                        ["realtimeReflectionProbes"] = new JsonObject { ["type"] = "boolean" },
                        ["billboardsFaceCameraPosition"] = new JsonObject { ["type"] = "boolean" },
                        ["vSyncCount"] = new JsonObject { ["type"] = "integer", ["enum"] = new JsonArray(0, 1, 2) },
                        ["lodBias"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["maximumLODLevel"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0 },
                        ["streamingMipmapsActive"] = new JsonObject { ["type"] = "boolean" },
                        ["streamingMipmapsAddAllCameras"] = new JsonObject { ["type"] = "boolean" },
                        ["streamingMipmapsMemoryBudget"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["streamingMipmapsRenderersPerFrame"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0 },
                        ["streamingMipmapsMaxLevelReduction"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0 },
                        ["streamingMipmapsMaxFileIORequests"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0 },
                        ["particleRaycastBudget"] = new JsonObject { ["type"] = "integer", ["minimum"] = 4, ["maximum"] = 10000 },
                        ["asyncUploadTimeSlice"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 },
                        ["asyncUploadBufferSize"] = new JsonObject { ["type"] = "integer", ["minimum"] = 2 }
                    }
                }),
            new McpToolDefinition(
                "projectSettings.getPhysicsSettings",
                "Returns Unity Physics Settings.",
                McpToolSchemaHelpers.EmptyObjectSchema()),
            new McpToolDefinition(
                "projectSettings.setPhysicsSettings",
                "Mutates Unity Physics Settings.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new JsonObject
                    {
                        ["gravity"] = McpToolSchemaHelpers.Vector3Schema("Physics gravity [x,y,z]."),
                        ["defaultMaterial"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Asset path to default PhysicMaterial."
                        },
                        ["bounceThreshold"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["sleepThreshold"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["defaultContactOffset"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["defaultSolverIterations"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 },
                        ["defaultSolverVelocityIterations"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 },
                        ["queriesHitBackfaces"] = new JsonObject { ["type"] = "boolean" },
                        ["queriesHitTriggers"] = new JsonObject { ["type"] = "boolean" },
                        ["enableAdaptiveForce"] = new JsonObject { ["type"] = "boolean" },
                        ["clampedDragBehavior"] = new JsonObject { ["type"] = "boolean" },
                        ["autoSyncTransforms"] = new JsonObject { ["type"] = "boolean" },
                        ["reuseCollisionCallbacks"] = new JsonObject { ["type"] = "boolean" },
                        ["autoSimulation"] = new JsonObject { ["type"] = "boolean" },
                        ["clothInterCollisionDistance"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["clothInterCollisionStiffness"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["contactsGeneration"] = McpToolSchemaHelpers.EnumLikeSchema("ContactsGeneration enum name or integer value."),
                        ["layerCollisionMatrix"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["description"] = "32x32 collision matrix as 1024-element boolean array.",
                            ["minItems"] = 1024,
                            ["maxItems"] = 1024,
                            ["items"] = new JsonObject { ["type"] = "boolean" }
                        }
                    }
                })
        };
    }
}
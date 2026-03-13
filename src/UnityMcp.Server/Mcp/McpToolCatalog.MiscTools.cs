using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp;

public sealed partial class McpToolCatalog
{
    private static IEnumerable<McpToolDefinition> GetMiscTools()
    {
        yield return new McpToolDefinition(
            "characterController.getSettings",
            "Returns CharacterController component settings for the target.",
            InstanceIdOnlySchema("Unity instance id of a CharacterController component or a GameObject with a single CharacterController."));

        yield return new McpToolDefinition(
            "characterController.setSettings",
            "Mutates CharacterController component settings.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer" },
                    ["height"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                    ["radius"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                    ["center"] = Vector3Schema("CharacterController center [x,y,z]."),
                    ["slopeLimit"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 90 },
                    ["stepOffset"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                    ["skinWidth"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                    ["minMoveDistance"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                    ["enableOverlapRecovery"] = new JsonObject { ["type"] = "boolean" }
                }
            });

        yield return new McpToolDefinition(
            "particleSystem.getSettings",
            "Returns ParticleSystem component settings for the target.",
            InstanceIdOnlySchema("Unity instance id of a ParticleSystem component or a GameObject with a single ParticleSystem."));

        yield return new McpToolDefinition(
            "particleSystem.setSettings",
            "Mutates ParticleSystem component settings.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer" },
                    ["duration"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                    ["loop"] = new JsonObject { ["type"] = "boolean" },
                    ["prewarm"] = new JsonObject { ["type"] = "boolean" },
                    ["startDelay"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                    ["startLifetime"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                    ["startSpeed"] = new JsonObject { ["type"] = "number" },
                    ["startSize"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                    ["maxParticles"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0 },
                    ["playOnAwake"] = new JsonObject { ["type"] = "boolean" },
                    ["emissionRate"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 }
                }
            });

        yield return new McpToolDefinition(
            "particleSystem.play",
            "Starts playing a ParticleSystem.",
            InstanceIdOnlySchema("Unity instance id of a ParticleSystem component or a GameObject with a single ParticleSystem."));

        yield return new McpToolDefinition(
            "particleSystem.stop",
            "Stops a ParticleSystem (stop emitting and clear).",
            InstanceIdOnlySchema("Unity instance id of a ParticleSystem component or a GameObject with a single ParticleSystem."));

        yield return new McpToolDefinition(
            "navMeshAgent.getSettings",
            "Returns NavMeshAgent component settings for the target.",
            InstanceIdOnlySchema("Unity instance id of a NavMeshAgent component or a GameObject with a single NavMeshAgent."));

        yield return new McpToolDefinition(
            "navMeshAgent.setSettings",
            "Mutates NavMeshAgent component settings.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer" },
                    ["enabled"] = new JsonObject { ["type"] = "boolean" },
                    ["speed"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                    ["angularSpeed"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                    ["acceleration"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                    ["stoppingDistance"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                    ["radius"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                    ["height"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                    ["areaMask"] = new JsonObject { ["type"] = "integer" },
                    ["autoBraking"] = new JsonObject { ["type"] = "boolean" },
                    ["obstacleAvoidanceType"] = EnumLikeSchema("ObstacleAvoidanceType enum name or integer value."),
                    ["avoidancePriority"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 99 }
                }
            });

        yield return new McpToolDefinition(
            "navMeshObstacle.getSettings",
            "Returns NavMeshObstacle component settings for the target.",
            InstanceIdOnlySchema("Unity instance id of a NavMeshObstacle component or a GameObject with a single NavMeshObstacle."));

        yield return new McpToolDefinition(
            "navMeshObstacle.setSettings",
            "Mutates NavMeshObstacle component settings.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer" },
                    ["enabled"] = new JsonObject { ["type"] = "boolean" },
                    ["carving"] = new JsonObject { ["type"] = "boolean" },
                    ["carvingMoveThreshold"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                    ["carvingTimeToStationary"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                    ["shape"] = EnumLikeSchema("NavMeshObstacleShape enum name or integer value."),
                    ["center"] = Vector3Schema("NavMeshObstacle center [x,y,z]."),
                    ["size"] = Vector3Schema("NavMeshObstacle size [x,y,z]."),
                    ["radius"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                    ["height"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 }
                }
            });

        yield return new McpToolDefinition(
            "rectTransform.getSettings",
            "Returns RectTransform component settings for the target.",
            InstanceIdOnlySchema("Unity instance id of a RectTransform component or a GameObject with a single RectTransform."));

        yield return new McpToolDefinition(
            "rectTransform.setSettings",
            "Mutates RectTransform component settings.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer" },
                    ["anchorMin"] = Vector2Schema("RectTransform anchorMin [x,y]."),
                    ["anchorMax"] = Vector2Schema("RectTransform anchorMax [x,y]."),
                    ["anchoredPosition"] = Vector2Schema("RectTransform anchoredPosition [x,y]."),
                    ["sizeDelta"] = Vector2Schema("RectTransform sizeDelta [x,y]."),
                    ["pivot"] = Vector2Schema("RectTransform pivot [x,y]."),
                    ["offsetMin"] = Vector2Schema("RectTransform offsetMin [x,y]."),
                    ["offsetMax"] = Vector2Schema("RectTransform offsetMax [x,y].")
                }
            });

        yield return new McpToolDefinition(
            "canvas.getSettings",
            "Returns Canvas component settings for the target.",
            InstanceIdOnlySchema("Unity instance id of a Canvas component or a GameObject with a single Canvas."));

        yield return new McpToolDefinition(
            "canvas.setSettings",
            "Mutates Canvas component settings.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer" },
                    ["enabled"] = new JsonObject { ["type"] = "boolean" },
                    ["renderMode"] = EnumLikeSchema("RenderMode enum name or integer value."),
                    ["sortingOrder"] = new JsonObject { ["type"] = "integer" },
                    ["targetDisplay"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0 },
                    ["pixelPerfect"] = new JsonObject { ["type"] = "boolean" },
                    ["planeDistance"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                    ["overrideSorting"] = new JsonObject { ["type"] = "boolean" }
                }
            });

        yield return new McpToolDefinition(
            "navMesh.bake",
            "Bakes the NavMesh for the active scene using NavMeshBuilder.BuildNavMesh().",
            EmptyObjectSchema());

        yield return new McpToolDefinition(
            "terrain.getSettings",
            "Returns terrain settings for the Terrain component identified by instanceId.",
            InstanceIdOnlySchema("Instance ID of a GameObject with a Terrain component."));

        yield return new McpToolDefinition(
            "terrain.setSettings",
            "Sets terrain settings for the Terrain component identified by instanceId.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer", ["description"] = "Instance ID of a GameObject with a Terrain component." },
                    ["heightmapResolution"] = new JsonObject { ["type"] = "integer", ["description"] = "Heightmap resolution (power of two + 1)." },
                    ["size"] = Vector3Schema("Terrain size as [x, y, z]."),
                    ["basemapDistance"] = new JsonObject { ["type"] = "number", ["description"] = "Distance at which terrain textures switch to basemap." },
                    ["drawHeightmap"] = new JsonObject { ["type"] = "boolean" },
                    ["drawInstanced"] = new JsonObject { ["type"] = "boolean" },
                    ["detailObjectDistance"] = new JsonObject { ["type"] = "number" },
                    ["treeBillboardDistance"] = new JsonObject { ["type"] = "number" },
                    ["shadowCastingMode"] = EnumLikeSchema("ShadowCastingMode enum name or integer value.")
                }
            });

        yield return new McpToolDefinition(
            "build.getSettings",
            "Returns current build settings: scenes in build, active build target, development build flag.",
            EmptyObjectSchema());

        yield return new McpToolDefinition(
            "build.setSettings",
            "Sets build pipeline settings.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JsonObject
                {
                    ["developmentBuild"] = new JsonObject { ["type"] = "boolean", ["description"] = "Enable or disable development build." },
                    ["outputPath"] = new JsonObject { ["type"] = "string", ["description"] = "Build output path." }
                }
            });

        yield return new McpToolDefinition(
            "build.build",
            "Triggers BuildPipeline.BuildPlayer with current settings.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("outputPath"),
                ["properties"] = new JsonObject
                {
                    ["outputPath"] = new JsonObject { ["type"] = "string", ["description"] = "Output path for the build." }
                }
            });

        yield return new McpToolDefinition(
            "canvasGroup.getSettings",
            "Returns CanvasGroup settings: alpha, interactable, blocksRaycasts, ignoreParentGroups.",
            InstanceIdOnlySchema("Instance ID of a CanvasGroup component or a GameObject with a CanvasGroup."));

        yield return new McpToolDefinition(
            "canvasGroup.setSettings",
            "Sets CanvasGroup settings: alpha, interactable, blocksRaycasts, ignoreParentGroups.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer", ["description"] = "Instance ID of a CanvasGroup component or a GameObject with a CanvasGroup." },
                    ["alpha"] = new JsonObject { ["type"] = "number", ["description"] = "Group alpha (0-1).", ["minimum"] = 0, ["maximum"] = 1 },
                    ["interactable"] = new JsonObject { ["type"] = "boolean", ["description"] = "Is the group interactable." },
                    ["blocksRaycasts"] = new JsonObject { ["type"] = "boolean", ["description"] = "Does the group block raycasts." },
                    ["ignoreParentGroups"] = new JsonObject { ["type"] = "boolean", ["description"] = "Ignore parent CanvasGroup settings." }
                }
            });

        yield return new McpToolDefinition(
            "time.getSettings",
            "Returns Unity Time settings: timeScale, fixedDeltaTime, maximumDeltaTime, maximumParticleDeltaTime.",
            EmptyObjectSchema());

        yield return new McpToolDefinition(
            "time.setSettings",
            "Sets Unity Time settings: timeScale, fixedDeltaTime.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JsonObject
                {
                    ["timeScale"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["description"] = "Time scale (0 = paused, 1 = normal)." },
                    ["fixedDeltaTime"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0, ["description"] = "Fixed timestep in seconds." }
                }
            });

        yield return new McpToolDefinition(
            "testRunner.listTests",
            "List available Unity tests by mode. Returns test tree with full names, class names, method names, and assemblies.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("mode"),
                ["properties"] = new JsonObject
                {
                    ["mode"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Test mode: 'editMode' or 'playMode'.",
                        ["enum"] = new JsonArray("editMode", "playMode")
                    }
                }
            });

        yield return new McpToolDefinition(
            "testRunner.run",
            "Start a Unity test run. Returns immediately with started status. Use testRunner.getResults to poll for completion.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("mode"),
                ["properties"] = new JsonObject
                {
                    ["mode"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Test mode: 'editMode' or 'playMode'.",
                        ["enum"] = new JsonArray("editMode", "playMode")
                    },
                    ["testFilter"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Optional filter by test name pattern. Matches against full test names."
                    }
                }
            });

        yield return new McpToolDefinition(
            "testRunner.getResults",
            "Get results of the last completed test run. Returns pass/fail/skip counts and per-test details including duration and failure messages.",
            EmptyObjectSchema());

        yield return new McpToolDefinition(
            "testRunner.cancel",
            "Cancel an in-progress test run.",
            EmptyObjectSchema());

        yield return new McpToolDefinition(
            "projectSettings.getPlayerSettings",
            "Returns curated Unity Player Settings properties including build and platform settings.",
            EmptyObjectSchema());

        yield return new McpToolDefinition(
            "projectSettings.setPlayerSettings",
            "Updates Unity Player Settings properties. Supports any subset of available properties.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JsonObject
                {
                    ["companyName"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Company name shown in player."
                    },
                    ["productName"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Product name shown in player title bar."
                    },
                    ["applicationIdentifier"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Application identifier/bundle ID for the target platform."
                    },
                    ["bundleVersion"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Bundle version string."
                    },
                    ["defaultScreenWidth"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["minimum"] = 1,
                        ["description"] = "Default screen width in pixels."
                    },
                    ["defaultScreenHeight"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["minimum"] = 1,
                        ["description"] = "Default screen height in pixels."
                    },
                    ["fullscreenMode"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JsonArray("ExclusiveFullScreen", "FullScreenWindow", "MaximizedWindow", "Windowed"),
                        ["description"] = "Default fullscreen mode."
                    },
                    ["runInBackground"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Whether application continues running when it loses focus."
                    },
                    ["colorSpace"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JsonArray("Gamma", "Linear"),
                        ["description"] = "Color space for rendering."
                    },
                    ["allowUnsafeCode"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Allow unsafe code compilation."
                    },
                    ["stripEngineCode"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Strip engine code in builds."
                    }
                }
            });

        yield return new McpToolDefinition(
            "projectSettings.getQualitySettings",
            "Returns current quality level and settings including graphics and performance parameters.",
            EmptyObjectSchema());

        yield return new McpToolDefinition(
            "projectSettings.setQualitySettings",
            "Updates quality level settings. Optionally targets a specific quality level by index.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JsonObject
                {
                    ["levelIndex"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["minimum"] = 0,
                        ["description"] = "Quality level index to update (uses current level if not specified)."
                    },
                    ["pixelLightCount"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["minimum"] = 0,
                        ["description"] = "Maximum number of pixel lights."
                    },
                    ["shadows"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JsonArray("Disabled", "HardOnly", "All"),
                        ["description"] = "Shadow quality setting."
                    },
                    ["shadowResolution"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["enum"] = new JsonArray("Low", "Medium", "High", "VeryHigh"),
                        ["description"] = "Shadow resolution setting."
                    },
                    ["shadowDistance"] = new JsonObject
                    {
                        ["type"] = "number",
                        ["minimum"] = 0,
                        ["description"] = "Maximum shadow distance."
                    },
                    ["antiAliasing"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["enum"] = new JsonArray(0, 2, 4, 8),
                        ["description"] = "Anti-aliasing sample count."
                    },
                    ["vSyncCount"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["enum"] = new JsonArray(0, 1, 2),
                        ["description"] = "VSync count (0=disabled, 1=enabled, 2=every other frame)."
                    }
                }
            });

        yield return new McpToolDefinition(
            "projectSettings.getPhysicsSettings",
            "Returns Unity Physics settings including gravity, solver iterations, and simulation parameters.",
            EmptyObjectSchema());

        yield return new McpToolDefinition(
            "projectSettings.setPhysicsSettings",
            "Updates Unity Physics settings. Supports any subset of available parameters.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JsonObject
                {
                    ["gravity"] = Vector3Schema("Gravity vector applied to all rigidbodies."),
                    ["defaultSolverIterations"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["minimum"] = 1,
                        ["description"] = "Default number of solver iterations for rigidbodies."
                    },
                    ["defaultSolverVelocityIterations"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["minimum"] = 1,
                        ["description"] = "Default number of velocity solver iterations for rigidbodies."
                    },
                    ["sleepThreshold"] = new JsonObject
                    {
                        ["type"] = "number",
                        ["minimum"] = 0,
                        ["description"] = "Mass-normalized kinetic energy threshold below which objects sleep."
                    },
                    ["defaultContactOffset"] = new JsonObject
                    {
                        ["type"] = "number",
                        ["minimum"] = 0,
                        ["description"] = "Contact offset value for newly created colliders."
                    },
                    ["bounceThreshold"] = new JsonObject
                    {
                        ["type"] = "number",
                        ["minimum"] = 0,
                        ["description"] = "Relative velocity threshold for bounce."
                    },
                    ["autoSimulation"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Whether physics simulation runs automatically."
                    },
                    ["autoSyncTransforms"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Whether transform changes are automatically synced with physics."
                    }
                }
            });
    }
}

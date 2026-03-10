#nullable enable

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityMcp.Editor
{
    internal static class ProjectSettingsHandler
    {
        internal static string BuildGetPlayerSettingsResponse(JToken idToken)
        {
            return UnityMcpProtocol.CreateResult(idToken, new
            {
                companyName = PlayerSettings.companyName,
                productName = PlayerSettings.productName,
                applicationIdentifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup)),
                bundleVersion = PlayerSettings.bundleVersion,
                defaultScreenWidth = PlayerSettings.defaultScreenWidth,
                defaultScreenHeight = PlayerSettings.defaultScreenHeight,
                fullscreenMode = PlayerSettings.fullScreenMode.ToString(),
                runInBackground = PlayerSettings.runInBackground,
                scriptingBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup)).ToString(),
                apiCompatibilityLevel = PlayerSettings.GetApiCompatibilityLevel(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup)).ToString(),
                colorSpace = PlayerSettings.colorSpace.ToString(),
                allowUnsafeCode = PlayerSettings.allowUnsafeCode,
                stripEngineCode = PlayerSettings.stripEngineCode,
                defaultInterfaceOrientation = PlayerSettings.defaultInterfaceOrientation.ToString(),
                useAnimatedAutorotation = PlayerSettings.useAnimatedAutorotation,
                gpuSkinning = PlayerSettings.gpuSkinning,
                graphicsJobs = PlayerSettings.graphicsJobs
            });
        }

        internal static string BuildSetPlayerSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = UnityMcpParameterHelpers.RequireParamsObject(root, "projectSettings.setPlayerSettings");
            var updated = new List<string>();

            if (paramsObject.TryGetValue("companyName", out var companyNameToken) && companyNameToken.Type == JTokenType.String)
            {
                PlayerSettings.companyName = companyNameToken.Value<string>()!;
                updated.Add("companyName");
            }

            if (paramsObject.TryGetValue("productName", out var productNameToken) && productNameToken.Type == JTokenType.String)
            {
                PlayerSettings.productName = productNameToken.Value<string>()!;
                updated.Add("productName");
            }

            if (paramsObject.TryGetValue("applicationIdentifier", out var appIdToken) && appIdToken.Type == JTokenType.String)
            {
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup), appIdToken.Value<string>()!);
                updated.Add("applicationIdentifier");
            }

            if (paramsObject.TryGetValue("bundleVersion", out var bundleVersionToken) && bundleVersionToken.Type == JTokenType.String)
            {
                PlayerSettings.bundleVersion = bundleVersionToken.Value<string>()!;
                updated.Add("bundleVersion");
            }

            if (paramsObject.TryGetValue("defaultScreenWidth", out var widthToken) && widthToken.Type == JTokenType.Integer)
            {
                PlayerSettings.defaultScreenWidth = widthToken.Value<int>();
                updated.Add("defaultScreenWidth");
            }

            if (paramsObject.TryGetValue("defaultScreenHeight", out var heightToken) && heightToken.Type == JTokenType.Integer)
            {
                PlayerSettings.defaultScreenHeight = heightToken.Value<int>();
                updated.Add("defaultScreenHeight");
            }

            if (paramsObject.TryGetValue("fullscreenMode", out var fullscreenToken) && fullscreenToken.Type == JTokenType.String)
            {
                if (Enum.TryParse<FullScreenMode>(fullscreenToken.Value<string>(), out var fullscreenMode))
                {
                    PlayerSettings.fullScreenMode = fullscreenMode;
                    updated.Add("fullscreenMode");
                }
            }

            if (paramsObject.TryGetValue("runInBackground", out var runInBgToken) && runInBgToken.Type == JTokenType.Boolean)
            {
                PlayerSettings.runInBackground = runInBgToken.Value<bool>();
                updated.Add("runInBackground");
            }

            if (paramsObject.TryGetValue("colorSpace", out var colorSpaceToken) && colorSpaceToken.Type == JTokenType.String)
            {
                if (Enum.TryParse<ColorSpace>(colorSpaceToken.Value<string>(), out var colorSpace))
                {
                    PlayerSettings.colorSpace = colorSpace;
                    updated.Add("colorSpace");
                }
            }

            if (paramsObject.TryGetValue("allowUnsafeCode", out var allowUnsafeToken) && allowUnsafeToken.Type == JTokenType.Boolean)
            {
                PlayerSettings.allowUnsafeCode = allowUnsafeToken.Value<bool>();
                updated.Add("allowUnsafeCode");
            }

            if (paramsObject.TryGetValue("stripEngineCode", out var stripToken) && stripToken.Type == JTokenType.Boolean)
            {
                PlayerSettings.stripEngineCode = stripToken.Value<bool>();
                updated.Add("stripEngineCode");
            }

            if (updated.Count > 0)
            {
                AssetDatabase.SaveAssets();
            }

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                updated = updated.ToArray(),
                count = updated.Count
            });
        }

        internal static string BuildGetQualitySettingsResponse(JToken idToken)
        {
            var currentLevel = QualitySettings.GetQualityLevel();
            var levelNames = QualitySettings.names;

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                currentLevelIndex = currentLevel,
                currentLevelName = levelNames[currentLevel],
                allLevelNames = levelNames,
                activeSettings = new
                {
                    pixelLightCount = QualitySettings.pixelLightCount,
                    shadows = QualitySettings.shadows.ToString(),
                    shadowResolution = QualitySettings.shadowResolution.ToString(),
                    shadowDistance = QualitySettings.shadowDistance,
                    antiAliasing = QualitySettings.antiAliasing,
                    softParticles = QualitySettings.softParticles,
                    vSyncCount = QualitySettings.vSyncCount,
                    textureQuality = QualitySettings.globalTextureMipmapLimit,
                    anisotropicFiltering = QualitySettings.anisotropicFiltering.ToString(),
                    lodBias = QualitySettings.lodBias,
                    maximumLODLevel = QualitySettings.maximumLODLevel,
                    particleRaycastBudget = QualitySettings.particleRaycastBudget,
                    billboardsFaceCameraPosition = QualitySettings.billboardsFaceCameraPosition
                }
            });
        }

        internal static string BuildSetQualitySettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = UnityMcpParameterHelpers.RequireParamsObject(root, "projectSettings.setQualitySettings");
            var updated = new List<string>();

            var targetLevel = QualitySettings.GetQualityLevel();
            if (paramsObject.TryGetValue("levelIndex", out var levelIndexToken) && levelIndexToken.Type == JTokenType.Integer)
            {
                var newLevel = levelIndexToken.Value<int>();
                if (newLevel >= 0 && newLevel < QualitySettings.names.Length)
                {
                    targetLevel = newLevel;
                    QualitySettings.SetQualityLevel(targetLevel);
                    updated.Add("levelIndex");
                }
            }

            if (paramsObject.TryGetValue("pixelLightCount", out var pixelLightToken) && pixelLightToken.Type == JTokenType.Integer)
            {
                QualitySettings.pixelLightCount = pixelLightToken.Value<int>();
                updated.Add("pixelLightCount");
            }

            if (paramsObject.TryGetValue("shadows", out var shadowsToken) && shadowsToken.Type == JTokenType.String)
            {
                if (Enum.TryParse<ShadowQuality>(shadowsToken.Value<string>(), out var shadows))
                {
                    QualitySettings.shadows = shadows;
                    updated.Add("shadows");
                }
            }

            if (paramsObject.TryGetValue("shadowResolution", out var shadowResToken) && shadowResToken.Type == JTokenType.String)
            {
                if (Enum.TryParse<ShadowResolution>(shadowResToken.Value<string>(), out var shadowRes))
                {
                    QualitySettings.shadowResolution = shadowRes;
                    updated.Add("shadowResolution");
                }
            }

            if (paramsObject.TryGetValue("shadowDistance", out var shadowDistToken) && shadowDistToken.Type == JTokenType.Float)
            {
                QualitySettings.shadowDistance = shadowDistToken.Value<float>();
                updated.Add("shadowDistance");
            }

            if (paramsObject.TryGetValue("antiAliasing", out var aaToken) && aaToken.Type == JTokenType.Integer)
            {
                var aa = aaToken.Value<int>();
                if (aa == 0 || aa == 2 || aa == 4 || aa == 8)
                {
                    QualitySettings.antiAliasing = aa;
                    updated.Add("antiAliasing");
                }
            }

            if (paramsObject.TryGetValue("vSyncCount", out var vSyncToken) && vSyncToken.Type == JTokenType.Integer)
            {
                var vSync = vSyncToken.Value<int>();
                if (vSync >= 0 && vSync <= 2)
                {
                    QualitySettings.vSyncCount = vSync;
                    updated.Add("vSyncCount");
                }
            }

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                updated = updated.ToArray(),
                count = updated.Count,
                targetLevel = targetLevel,
                targetLevelName = QualitySettings.names[targetLevel]
            });
        }

        internal static string BuildGetPhysicsSettingsResponse(JToken idToken)
        {
            var gravity = Physics.gravity;

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                gravity = new[] { gravity.x, gravity.y, gravity.z },
                defaultSolverIterations = Physics.defaultSolverIterations,
                defaultSolverVelocityIterations = Physics.defaultSolverVelocityIterations,
                sleepThreshold = Physics.sleepThreshold,
                defaultContactOffset = Physics.defaultContactOffset,
                bounceThreshold = Physics.bounceThreshold,
                defaultMaxDepenetrationVelocity = Physics.defaultMaxDepenetrationVelocity,
                autoSimulation = Physics.simulationMode == SimulationMode.FixedUpdate,
                autoSyncTransforms = true, // Physics.SyncTransforms() is now a method call
                reuseCollisionCallbacks = Physics.reuseCollisionCallbacks
            });
        }

        internal static string BuildSetPhysicsSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = UnityMcpParameterHelpers.RequireParamsObject(root, "projectSettings.setPhysicsSettings");
            var updated = new List<string>();

            if (paramsObject.TryGetValue("gravity", out var gravityToken) && gravityToken.Type == JTokenType.Array)
            {
                var gravityArray = UnityMcpParameterHelpers.ParseFloatArrayToken(gravityToken, "gravity", 3);
                Physics.gravity = new Vector3(gravityArray[0], gravityArray[1], gravityArray[2]);
                updated.Add("gravity");
            }

            if (paramsObject.TryGetValue("defaultSolverIterations", out var solverIterToken) && solverIterToken.Type == JTokenType.Integer)
            {
                var value = solverIterToken.Value<int>();
                if (value >= 1)
                {
                    Physics.defaultSolverIterations = value;
                    updated.Add("defaultSolverIterations");
                }
            }

            if (paramsObject.TryGetValue("defaultSolverVelocityIterations", out var velIterToken) && velIterToken.Type == JTokenType.Integer)
            {
                var value = velIterToken.Value<int>();
                if (value >= 1)
                {
                    Physics.defaultSolverVelocityIterations = value;
                    updated.Add("defaultSolverVelocityIterations");
                }
            }

            if (paramsObject.TryGetValue("sleepThreshold", out var sleepToken) && sleepToken.Type == JTokenType.Float)
            {
                var value = sleepToken.Value<float>();
                if (value >= 0)
                {
                    Physics.sleepThreshold = value;
                    updated.Add("sleepThreshold");
                }
            }

            if (paramsObject.TryGetValue("defaultContactOffset", out var contactOffsetToken) && contactOffsetToken.Type == JTokenType.Float)
            {
                var value = contactOffsetToken.Value<float>();
                if (value >= 0)
                {
                    Physics.defaultContactOffset = value;
                    updated.Add("defaultContactOffset");
                }
            }

            if (paramsObject.TryGetValue("bounceThreshold", out var bounceToken) && bounceToken.Type == JTokenType.Float)
            {
                var value = bounceToken.Value<float>();
                if (value >= 0)
                {
                    Physics.bounceThreshold = value;
                    updated.Add("bounceThreshold");
                }
            }

            if (paramsObject.TryGetValue("autoSimulation", out var autoSimToken) && autoSimToken.Type == JTokenType.Boolean)
            {
                Physics.simulationMode = autoSimToken.Value<bool>() ? SimulationMode.FixedUpdate : SimulationMode.Script;
                updated.Add("autoSimulation");
            }

            if (paramsObject.TryGetValue("autoSyncTransforms", out var autoSyncToken) && autoSyncToken.Type == JTokenType.Boolean)
            {
                if (autoSyncToken.Value<bool>()) Physics.SyncTransforms(); // Call sync method if true
                updated.Add("autoSyncTransforms");
            }

            if (updated.Count > 0)
            {
                AssetDatabase.SaveAssets();
            }

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                updated = updated.ToArray(),
                count = updated.Count
            });
        }
    }
}
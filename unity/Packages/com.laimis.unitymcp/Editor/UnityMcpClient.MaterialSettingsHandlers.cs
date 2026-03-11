#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UnityMcp.Editor
{

internal sealed partial class UnityMcpClient
{
    private static string GetShaderPropertyTypeName(ShaderPropertyType type)
    {
        return type switch
        {
            ShaderPropertyType.Color => "Color",
            ShaderPropertyType.Vector => "Vector",
            ShaderPropertyType.Float => "Float",
            ShaderPropertyType.Range => "Range",
            ShaderPropertyType.Texture => "Texture",
            _ => type.ToString()
        };
    }

    private static object? GetShaderPropertyValue(Material material, string propName, ShaderPropertyType propType)
    {
        switch (propType)
        {
            case ShaderPropertyType.Color:
                var c = material.GetColor(propName);
                return new { r = c.r, g = c.g, b = c.b, a = c.a };
            case ShaderPropertyType.Vector:
                var v = material.GetVector(propName);
                return new { x = v.x, y = v.y, z = v.z, w = v.w };
            case ShaderPropertyType.Float:
            case ShaderPropertyType.Range:
                return material.GetFloat(propName);
            case ShaderPropertyType.Texture:
                var tex = material.GetTexture(propName);
                return tex != null ? AssetDatabase.GetAssetPath(tex) : null;
            default:
                // Try Int for newer Unity versions (ShaderPropertyType value 5)
                if ((int)propType == 5)
                    return material.GetInt(propName);
                return null;
        }
    }

    private static string BuildGetMaterialPropertiesResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "material.getProperties");
        var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
        var material = LoadMaterialFromAssetPath(assetPath);
        var shader = material.shader;
        var propertyCount = shader.GetPropertyCount();

        var properties = new object[propertyCount];
        for (var i = 0; i < propertyCount; i++)
        {
            var propName = shader.GetPropertyName(i);
            var propType = shader.GetPropertyType(i);
            properties[i] = new
            {
                name = propName,
                type = GetShaderPropertyTypeName(propType),
                value = GetShaderPropertyValue(material, propName, propType)
            };
        }

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            assetPath,
            shaderName = shader.name,
            propertyCount,
            properties
        });
    }

    private static string BuildGetMaterialPropertyResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "material.getProperty");
        var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
        var propertyName = ParseRequiredStringParameter(paramsObject, "propertyName");
        var material = LoadMaterialFromAssetPath(assetPath);
        var shader = material.shader;
        var propertyCount = shader.GetPropertyCount();

        for (var i = 0; i < propertyCount; i++)
        {
            var propName = shader.GetPropertyName(i);
            if (propName == propertyName)
            {
                var propType = shader.GetPropertyType(i);
                return UnityMcpProtocol.CreateResult(idToken, new
                {
                    assetPath,
                    name = propName,
                    type = GetShaderPropertyTypeName(propType),
                    value = GetShaderPropertyValue(material, propName, propType)
                });
            }
        }

        throw new ArgumentException($"Property '{propertyName}' not found on shader '{shader.name}'.");
    }

    private static string BuildSetMaterialPropertyResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "material.setProperty");
        var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
        var propertyName = ParseRequiredStringParameter(paramsObject, "propertyName");
        var propertyType = ParseRequiredStringParameter(paramsObject, "propertyType").ToLowerInvariant();
        var material = LoadMaterialFromAssetPath(assetPath);

        if (!material.HasProperty(propertyName))
            throw new ArgumentException($"Property '{propertyName}' not found on material at '{assetPath}'.");

        if (!paramsObject.TryGetValue("value", out var valueToken))
            throw new ArgumentException("Parameter 'value' is required.");

        switch (propertyType)
        {
            case "color":
            {
                var r = valueToken["r"]?.Value<float>() ?? 0f;
                var g = valueToken["g"]?.Value<float>() ?? 0f;
                var b = valueToken["b"]?.Value<float>() ?? 0f;
                var a = valueToken["a"]?.Value<float>() ?? 1f;
                material.SetColor(propertyName, new Color(r, g, b, a));
                break;
            }
            case "vector":
            {
                var x = valueToken["x"]?.Value<float>() ?? 0f;
                var y = valueToken["y"]?.Value<float>() ?? 0f;
                var z = valueToken["z"]?.Value<float>() ?? 0f;
                var w = valueToken["w"]?.Value<float>() ?? 0f;
                material.SetVector(propertyName, new Vector4(x, y, z, w));
                break;
            }
            case "float":
                material.SetFloat(propertyName, valueToken.Value<float>());
                break;
            case "int":
                material.SetInt(propertyName, valueToken.Value<int>());
                break;
            case "texture":
            {
                var texturePath = valueToken.Type == JTokenType.Null ? null : valueToken.Value<string>();
                if (string.IsNullOrEmpty(texturePath))
                {
                    material.SetTexture(propertyName, null);
                }
                else
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
                    if (texture == null)
                        throw new ArgumentException($"No Texture found at '{texturePath}'.");
                    material.SetTexture(propertyName, texture);
                }
                break;
            }
            default:
                throw new ArgumentException($"Invalid propertyType '{propertyType}'. Expected: color, float, int, vector, texture.");
        }

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            assetPath,
            propertyName,
            propertyType,
            updated = true
        });
    }

    private static string BuildGetMaterialKeywordsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "material.getKeywords");
        var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
        var material = LoadMaterialFromAssetPath(assetPath);

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            assetPath,
            keywords = material.shaderKeywords
        });
    }

    private static string BuildSetMaterialKeywordResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "material.setKeyword");
        var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
        var keyword = ParseRequiredStringParameter(paramsObject, "keyword");
        var enabled = ParseRequiredBooleanParameter(paramsObject, "enabled");
        var material = LoadMaterialFromAssetPath(assetPath);

        if (enabled)
            material.EnableKeyword(keyword);
        else
            material.DisableKeyword(keyword);

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            assetPath,
            keyword,
            enabled,
            keywords = material.shaderKeywords
        });
    }

    private static string BuildGetMaterialShaderResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "material.getShader");
        var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
        var material = LoadMaterialFromAssetPath(assetPath);

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            assetPath,
            shaderName = material.shader.name
        });
    }

    private static string BuildSetMaterialShaderResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "material.setShader");
        var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
        var shaderName = ParseRequiredStringParameter(paramsObject, "shaderName");
        var material = LoadMaterialFromAssetPath(assetPath);

        var shader = Shader.Find(shaderName);
        if (shader == null)
            throw new ArgumentException($"Shader '{shaderName}' not found.");

        material.shader = shader;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            assetPath,
            shaderName = material.shader.name,
            updated = true
        });
    }

    private static string BuildGetMaterialRenderQueueResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "material.getRenderQueue");
        var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
        var material = LoadMaterialFromAssetPath(assetPath);

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            assetPath,
            renderQueue = material.renderQueue
        });
    }

    private static string BuildSetMaterialRenderQueueResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "material.setRenderQueue");
        var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
        var renderQueue = ParseRequiredIntegerParameter(paramsObject, "renderQueue");
        var material = LoadMaterialFromAssetPath(assetPath);

        material.renderQueue = renderQueue;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            assetPath,
            renderQueue = material.renderQueue,
            updated = true
        });
    }

    private static string BuildGetSceneHierarchyResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "scene.getHierarchy");

        // Parse optional parameters
        var includeInactive = ParseOptionalBooleanParameter(paramsObject, "includeInactive", true);
        var maxDepth = ParseOptionalIntegerParameter(paramsObject, "maxDepth");
        var rootFilter = ParseOptionalStringParameter(paramsObject, "rootFilter");
        var scenePath = ParseOptionalStringParameter(paramsObject, "scenePath");
        var allScenes = ParseOptionalBooleanParameter(paramsObject, "allScenes");
        var maxNodes = ParseOptionalIntegerParameter(paramsObject, "maxNodes") ?? 2000;

        if (maxNodes < 1 || maxNodes > 10000)
        {
            throw new ArgumentException("Parameter 'maxNodes' must be between 1 and 10000.");
        }

        if (allScenes)
        {
            // Return hierarchy for all open scenes
            var allSceneResults = new List<object>();
            var sceneCount = SceneManager.sceneCount;

            for (var index = 0; index < sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (!scene.isLoaded) continue;

                var sceneResult = GetSceneHierarchy(scene, includeInactive, maxDepth, rootFilter, maxNodes);
                allSceneResults.Add(sceneResult);
            }

            return UnityMcpProtocol.CreateResult(idToken, allSceneResults);
        }
        else
        {
            // Return hierarchy for specific scene or active scene
            Scene targetScene;
            if (!string.IsNullOrWhiteSpace(scenePath))
            {
                targetScene = SceneManager.GetSceneByPath(scenePath);
                if (!targetScene.isLoaded)
                {
                    throw new ArgumentException($"Scene at path '{scenePath}' is not loaded or does not exist.");
                }
            }
            else
            {
                targetScene = SceneManager.GetActiveScene();
            }

            var result = GetSceneHierarchy(targetScene, includeInactive, maxDepth, rootFilter, maxNodes);
            return UnityMcpProtocol.CreateResult(idToken, result);
        }
    }

    private static object GetSceneHierarchy(Scene scene, bool includeInactive, int? maxDepth, string? rootFilter, int maxNodes)
    {
        var nodes = new List<object>();
        var nodeCount = 0;
        var truncated = false;

        // Get root GameObjects
        var rootGameObjects = scene.GetRootGameObjects();

        // Filter by root name if specified
        if (!string.IsNullOrWhiteSpace(rootFilter))
        {
            rootGameObjects = rootGameObjects.Where(go => go.name.Equals(rootFilter, StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        // Traverse hierarchy
        foreach (var rootGameObject in rootGameObjects)
        {
            if (!includeInactive && !rootGameObject.activeInHierarchy)
                continue;

            if (TraverseGameObject(rootGameObject, nodes, ref nodeCount, maxNodes, maxDepth, 0, includeInactive, null))
            {
                truncated = true;
                break;
            }
        }

        return new
        {
            sceneName = scene.name,
            scenePath = scene.path,
            nodeCount,
            truncated,
            nodes = nodes
        };
    }

    private static bool TraverseGameObject(GameObject gameObject, List<object> nodes, ref int nodeCount, int maxNodes, int? maxDepth, int currentDepth, bool includeInactive, int? parentInstanceId)
    {
        if (nodeCount >= maxNodes)
            return true; // Signal truncation

        if (maxDepth.HasValue && currentDepth > maxDepth.Value)
            return false;

        if (!includeInactive && !gameObject.activeInHierarchy)
            return false;

        // Get component type names
        var components = gameObject.GetComponents<Component>()
            .Where(c => c != null)
            .Select(c => c.GetType().FullName)
            .ToArray();

        // Add node to list
        nodes.Add(new
        {
            name = gameObject.name,
            instanceId = gameObject.GetInstanceID(),
            activeSelf = gameObject.activeSelf,
            activeInHierarchy = gameObject.activeInHierarchy,
            depth = currentDepth,
            parentInstanceId = parentInstanceId,
            components = components
        });

        nodeCount++;
        var currentInstanceId = gameObject.GetInstanceID();

        // Traverse children
        var transform = gameObject.transform;
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i).gameObject;
            if (TraverseGameObject(child, nodes, ref nodeCount, maxNodes, maxDepth, currentDepth + 1, includeInactive, currentInstanceId))
            {
                return true; // Propagate truncation signal
            }
        }

        return false;
    }

    private static string BuildGetPlayerSettingsResponse(JToken idToken)
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

    private static string BuildSetPlayerSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "projectSettings.setPlayerSettings");
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

    private static string BuildGetQualitySettingsResponse(JToken idToken)
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

    private static string BuildSetQualitySettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "projectSettings.setQualitySettings");
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

    private static string BuildGetPhysicsSettingsResponse(JToken idToken)
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

    private static string BuildSetPhysicsSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "projectSettings.setPhysicsSettings");
        var updated = new List<string>();

        if (paramsObject.TryGetValue("gravity", out var gravityToken) && gravityToken.Type == JTokenType.Array)
        {
            var gravityArray = ParseFloatArrayToken(gravityToken, "gravity", 3);
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

    private static string BuildGetPhysics2DSettingsResponse(JToken idToken)
    {
        var gravity = Physics2D.gravity;

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            gravity = new[] { gravity.x, gravity.y },
            defaultContactOffset = Physics2D.defaultContactOffset,
            velocityIterations = Physics2D.velocityIterations,
            positionIterations = Physics2D.positionIterations,
            bounceThreshold = Physics2D.bounceThreshold,
            maxLinearCorrection = Physics2D.maxLinearCorrection,
            maxAngularCorrection = Physics2D.maxAngularCorrection,
            maxTranslationSpeed = Physics2D.maxTranslationSpeed,
            maxRotationSpeed = Physics2D.maxRotationSpeed,
            baumgarteScale = Physics2D.baumgarteScale,
            baumgarteTOIScale = Physics2D.baumgarteTOIScale,
            timeToSleep = Physics2D.timeToSleep,
            linearSleepTolerance = Physics2D.linearSleepTolerance,
            angularSleepTolerance = Physics2D.angularSleepTolerance,
            autoSimulation = Physics2D.simulationMode == SimulationMode2D.FixedUpdate,
            autoSyncTransforms = true, // Physics2D.SyncTransforms() is now a method call
            callbacksOnDisable = Physics2D.callbacksOnDisable,
            reuseCollisionCallbacks = Physics2D.reuseCollisionCallbacks
        });
    }

    private static string BuildSetPhysics2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "physics2D.setSettings");
        var updated = new List<string>();

        if (paramsObject.TryGetValue("gravity", out var gravityToken) && gravityToken.Type == JTokenType.Array)
        {
            var gravityArray = ParseFloatArrayToken(gravityToken, "gravity", 2);
            Physics2D.gravity = new Vector2(gravityArray[0], gravityArray[1]);
            updated.Add("gravity");
        }

        if (paramsObject.TryGetValue("defaultContactOffset", out var contactOffsetToken) && contactOffsetToken.Type == JTokenType.Float)
        {
            var value = contactOffsetToken.Value<float>();
            if (value >= 0)
            {
                Physics2D.defaultContactOffset = value;
                updated.Add("defaultContactOffset");
            }
        }

        if (paramsObject.TryGetValue("velocityIterations", out var velIterToken) && velIterToken.Type == JTokenType.Integer)
        {
            var value = velIterToken.Value<int>();
            if (value >= 1)
            {
                Physics2D.velocityIterations = value;
                updated.Add("velocityIterations");
            }
        }

        if (paramsObject.TryGetValue("positionIterations", out var posIterToken) && posIterToken.Type == JTokenType.Integer)
        {
            var value = posIterToken.Value<int>();
            if (value >= 1)
            {
                Physics2D.positionIterations = value;
                updated.Add("positionIterations");
            }
        }

        if (paramsObject.TryGetValue("velocityThreshold", out var velThresholdToken) && velThresholdToken.Type == JTokenType.Float)
        {
            var value = velThresholdToken.Value<float>();
            if (value >= 0)
            {
                Physics2D.bounceThreshold = value;
                updated.Add("bounceThreshold");
            }
        }

        if (paramsObject.TryGetValue("maxLinearCorrection", out var maxLinearToken) && maxLinearToken.Type == JTokenType.Float)
        {
            var value = maxLinearToken.Value<float>();
            if (value >= 0)
            {
                Physics2D.maxLinearCorrection = value;
                updated.Add("maxLinearCorrection");
            }
        }

        if (paramsObject.TryGetValue("maxAngularCorrection", out var maxAngularToken) && maxAngularToken.Type == JTokenType.Float)
        {
            var value = maxAngularToken.Value<float>();
            if (value >= 0)
            {
                Physics2D.maxAngularCorrection = value;
                updated.Add("maxAngularCorrection");
            }
        }

        if (paramsObject.TryGetValue("maxTranslationSpeed", out var maxTransToken) && maxTransToken.Type == JTokenType.Float)
        {
            var value = maxTransToken.Value<float>();
            if (value >= 0)
            {
                Physics2D.maxTranslationSpeed = value;
                updated.Add("maxTranslationSpeed");
            }
        }

        if (paramsObject.TryGetValue("maxRotationSpeed", out var maxRotToken) && maxRotToken.Type == JTokenType.Float)
        {
            var value = maxRotToken.Value<float>();
            if (value >= 0)
            {
                Physics2D.maxRotationSpeed = value;
                updated.Add("maxRotationSpeed");
            }
        }

        if (paramsObject.TryGetValue("autoSimulation", out var autoSimToken) && autoSimToken.Type == JTokenType.Boolean)
        {
            Physics2D.simulationMode = autoSimToken.Value<bool>() ? SimulationMode2D.FixedUpdate : SimulationMode2D.Script;
            updated.Add("autoSimulation");
        }

        if (paramsObject.TryGetValue("autoSyncTransforms", out var autoSyncToken) && autoSyncToken.Type == JTokenType.Boolean)
        {
            if (autoSyncToken.Value<bool>()) Physics2D.SyncTransforms(); // Call sync method if true
            updated.Add("autoSyncTransforms");
        }

        if (paramsObject.TryGetValue("reuseCollisionCallbacks", out var reuseToken) && reuseToken.Type == JTokenType.Boolean)
        {
            Physics2D.reuseCollisionCallbacks = reuseToken.Value<bool>();
            updated.Add("reuseCollisionCallbacks");
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

    private static string BuildCaptureSceneViewResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "editor.captureSceneView");

        var width = ParseOptionalIntegerParameter(paramsObject, "width") ?? 1920;
        var height = ParseOptionalIntegerParameter(paramsObject, "height") ?? 1080;

        if (width <= 0 || height <= 0)
        {
            throw new ArgumentException("Width and height must be greater than 0.");
        }

        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            throw new ArgumentException("No Scene View is currently open. Open a Scene View window first.");
        }

        var camera = sceneView.camera;
        if (camera == null)
        {
            throw new ArgumentException("Scene View camera is not available.");
        }

        // Create render texture and capture
        var renderTexture = new RenderTexture(width, height, 24);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;

        try
        {
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture.active = renderTexture;
            var texture2D = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture2D.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture2D.Apply();

            var pngData = texture2D.EncodeToPNG();
            var base64 = System.Convert.ToBase64String(pngData);

            UnityEngine.Object.DestroyImmediate(texture2D);

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                base64 = base64,
                width = width,
                height = height,
                format = "png"
            });
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }
    }

    private static string BuildCaptureGameViewResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "editor.captureGameView");

        if (!Application.isPlaying)
        {
            throw new ArgumentException("Game view capture is not supported in Edit mode. Enter Play mode first.");
        }

        var width = ParseOptionalIntegerParameter(paramsObject, "width");
        var height = ParseOptionalIntegerParameter(paramsObject, "height");

        if (width.HasValue && width.Value <= 0)
        {
            throw new ArgumentException("Width must be greater than 0.");
        }

        if (height.HasValue && height.Value <= 0)
        {
            throw new ArgumentException("Height must be greater than 0.");
        }

        var camera = Camera.main ?? Camera.allCameras.FirstOrDefault(c => c.enabled && c.gameObject.activeInHierarchy);
        if (camera == null)
        {
            throw new ArgumentException("No active camera found in the scene.");
        }

        var targetWidth = width ?? Screen.width;
        var targetHeight = height ?? Screen.height;

        // Create render texture and capture
        var renderTexture = new RenderTexture(targetWidth, targetHeight, 24);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;

        try
        {
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture.active = renderTexture;
            var texture2D = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
            texture2D.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            texture2D.Apply();

            var pngData = texture2D.EncodeToPNG();
            var base64 = System.Convert.ToBase64String(pngData);

            UnityEngine.Object.DestroyImmediate(texture2D);

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                base64 = base64,
                width = targetWidth,
                height = targetHeight,
                format = "png"
            });
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }
    }

    private static Material LoadMaterialFromAssetPath(string assetPath)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (material == null)
            throw new ArgumentException($"No Material found at '{assetPath}'.");
        return material;
    }
}
}

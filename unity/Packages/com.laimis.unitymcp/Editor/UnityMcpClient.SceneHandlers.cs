#nullable enable

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMcp.Editor
{
    internal sealed partial class UnityMcpClient
    {
        private static string BuildGetActiveSceneResponse(JToken idToken)
        {
            var activeScene = SceneManager.GetActiveScene();
            var result = CreateSceneSummary(activeScene, isActive: true);
            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildListOpenScenesResponse(JToken idToken)
        {
            var activeScene = SceneManager.GetActiveScene();
            var activeHandle = activeScene.handle;
            var items = new List<object>();

            var sceneCount = SceneManager.sceneCount;
            for (var index = 0; index < sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                items.Add(CreateSceneSummary(scene, isActive: scene.handle == activeHandle));
            }

            var result = new
            {
                count = items.Count,
                activeSceneHandle = activeHandle,
                items
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        // ── Scene management ─────────────────────────────────────────────────

        private static string BuildSaveSceneResponse(JToken idToken, JObject root)
        {
            UnityEngine.SceneManagement.Scene targetScene;
            string? scenePath = null;

            if (root.TryGetValue("params", out var paramsToken) && paramsToken is JObject p &&
                p.TryGetValue("scenePath", out var spToken) && spToken.Type == JTokenType.String)
            {
                scenePath = spToken.Value<string>();
                targetScene = FindOpenSceneByPathOrName(scenePath!, "scene.save");
            }
            else
            {
                targetScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            }

            var saved = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(targetScene);
            return UnityMcpProtocol.CreateResult(idToken, new
            {
                saved,
                sceneName = targetScene.name,
                scenePath = targetScene.path
            });
        }

        private static string BuildOpenSceneResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.openScene");
            if (!paramsObject.TryGetValue("scenePath", out var spToken) || spToken.Type != JTokenType.String)
                throw new ArgumentException("Parameter 'scenePath' is required and must be a string.");
            var scenePath = spToken.Value<string>()!;

            var mode = UnityEditor.SceneManagement.OpenSceneMode.Single;
            if (paramsObject.TryGetValue("mode", out var modeToken) && modeToken.Type == JTokenType.String)
            {
                mode = modeToken.Value<string>() switch
                {
                    "Additive" => UnityEditor.SceneManagement.OpenSceneMode.Additive,
                    "Single" => UnityEditor.SceneManagement.OpenSceneMode.Single,
                    var m => throw new ArgumentException($"Invalid mode '{m}'. Use 'Single' or 'Additive'.")
                };
            }

            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, mode);
            if (!scene.IsValid())
                throw new ArgumentException($"Failed to open scene at '{scenePath}'.");

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                sceneName = scene.name,
                scenePath = scene.path,
                isActive = scene == UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(),
                mode = mode.ToString(),
                opened = true
            });
        }

        private static string BuildNewSceneResponse(JToken idToken, JObject root)
        {
            var setup = UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects;
            var mode = UnityEditor.SceneManagement.NewSceneMode.Single;

            if (root.TryGetValue("params", out var paramsToken) && paramsToken is JObject p)
            {
                if (p.TryGetValue("setup", out var setupToken) && setupToken.Type == JTokenType.String)
                {
                    setup = setupToken.Value<string>() switch
                    {
                        "EmptyScene" => UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                        "DefaultGameObjects" => UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
                        var s => throw new ArgumentException($"Invalid setup '{s}'. Use 'EmptyScene' or 'DefaultGameObjects'.")
                    };
                }

                if (p.TryGetValue("mode", out var modeToken) && modeToken.Type == JTokenType.String)
                {
                    mode = modeToken.Value<string>() switch
                    {
                        "Single" => UnityEditor.SceneManagement.NewSceneMode.Single,
                        "Additive" => UnityEditor.SceneManagement.NewSceneMode.Additive,
                        var m => throw new ArgumentException($"Invalid mode '{m}'. Use 'Single' or 'Additive'.")
                    };
                }
            }

            var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(setup, mode);
            return UnityMcpProtocol.CreateResult(idToken, new
            {
                sceneName = scene.name,
                scenePath = scene.path,
                setup = setup.ToString(),
                mode = mode.ToString(),
                created = true
            });
        }

        private static string BuildCloseSceneResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.closeScene");
            if (!paramsObject.TryGetValue("scenePath", out var spToken) || spToken.Type != JTokenType.String)
                throw new ArgumentException("Parameter 'scenePath' is required and must be a string.");

            var removeScene = true;
            if (paramsObject.TryGetValue("removeScene", out var rmToken) && rmToken.Type == JTokenType.Boolean)
                removeScene = rmToken.Value<bool>();

            var scene = FindOpenSceneByPathOrName(spToken.Value<string>()!, "scene.closeScene");
            var sceneName = scene.name;
            var scenePath = scene.path;

            var closed = UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, removeScene);
            return UnityMcpProtocol.CreateResult(idToken, new
            {
                closed,
                sceneName,
                scenePath,
                removeScene
            });
        }

        private static string BuildSetActiveSceneResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.setActiveScene");
            if (!paramsObject.TryGetValue("scenePath", out var spToken) || spToken.Type != JTokenType.String)
                throw new ArgumentException("Parameter 'scenePath' is required and must be a string.");

            var scene = FindOpenSceneByPathOrName(spToken.Value<string>()!, "scene.setActiveScene");
            var set = UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(scene);

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                set,
                sceneName = scene.name,
                scenePath = scene.path
            });
        }

        private static UnityEngine.SceneManagement.Scene FindOpenSceneByPathOrName(string pathOrName, string methodName)
        {
            var sceneCount = UnityEditor.SceneManagement.EditorSceneManager.sceneCount;
            for (var i = 0; i < sceneCount; i++)
            {
                var s = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i);
                if (s.path == pathOrName || s.name == pathOrName)
                    return s;
            }
            throw new ArgumentException($"[{methodName}] No open scene matches '{pathOrName}'.");
        }
    }
}

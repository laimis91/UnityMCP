#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using static UnityMcp.Editor.UnityMcpParameterHelpers;
using static UnityMcp.Editor.UnityMcpResolvers;

namespace UnityMcp.Editor
{
    internal static class AssetsHandler
    {
        internal static string BuildFindAssetsResponse(JToken idToken, JObject root)
        {
            if (!root.TryGetValue("params", out var paramsToken) || paramsToken is not JObject paramsObject)
            {
                throw new ArgumentException("Method 'assets.find' expects params to be an object.");
            }

            if (!paramsObject.TryGetValue("query", out var queryToken) || queryToken.Type != JTokenType.String)
            {
                throw new ArgumentException("Parameter 'query' is required and must be a string.");
            }

            var query = queryToken.Value<string>();
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("Parameter 'query' cannot be empty.");
            }

            var maxResults = 100;
            if (paramsObject.TryGetValue("maxResults", out var maxResultsToken))
            {
                if (maxResultsToken.Type != JTokenType.Integer)
                {
                    throw new ArgumentException("Parameter 'maxResults' must be an integer.");
                }

                var parsedMaxResults = maxResultsToken.Value<int?>();
                if (!parsedMaxResults.HasValue)
                {
                    throw new ArgumentException("Parameter 'maxResults' must be an integer.");
                }

                if (parsedMaxResults.Value < 1 || parsedMaxResults.Value > 500)
                {
                    throw new ArgumentException("Parameter 'maxResults' must be between 1 and 500.");
                }

                maxResults = parsedMaxResults.Value;
            }

            var searchInFolders = ParseOptionalStringArrayParameter(paramsObject, "searchInFolders");
            if (searchInFolders != null)
            {
                for (var index = 0; index < searchInFolders.Count; index++)
                {
                    var normalizedFolder = NormalizeAndValidateAssetPath(searchInFolders[index]);
                    if (!AssetDatabase.IsValidFolder(normalizedFolder))
                    {
                        throw new ArgumentException($"Search folder '{normalizedFolder}' does not exist or is not a valid Unity folder.");
                    }

                    searchInFolders[index] = normalizedFolder;
                }
            }

            var types = ParseOptionalStringArrayParameter(paramsObject, "types");
            var labels = ParseOptionalStringArrayParameter(paramsObject, "labels");
            var effectiveQuery = BuildEffectiveAssetsFindQuery(query!, types, labels);

            var guids = searchInFolders is { Count: > 0 }
                ? AssetDatabase.FindAssets(effectiveQuery, searchInFolders.ToArray())
                : AssetDatabase.FindAssets(effectiveQuery);
            var takeCount = Math.Min(maxResults, guids.Length);
            var items = new List<object>(takeCount);
            for (var index = 0; index < takeCount; index++)
            {
                var guid = guids[index];
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var mainAssetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);

                items.Add(new
                {
                    guid,
                    assetPath,
                    isFolder = AssetDatabase.IsValidFolder(assetPath),
                    mainAssetType = mainAssetType != null ? mainAssetType.FullName : null,
                    mainAssetName = mainAsset != null ? mainAsset.name : null
                });
            }

            var result = new
            {
                query,
                effectiveQuery,
                searchInFolders,
                types,
                labels,
                totalMatched = guids.Length,
                returnedCount = items.Count,
                maxResults,
                truncated = guids.Length > takeCount,
                items
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildImportAssetResponse(JToken idToken, JObject root)
        {
            if (!root.TryGetValue("params", out var paramsToken) || paramsToken is not JObject paramsObject)
            {
                throw new ArgumentException("Method 'assets.import' expects params to be an object.");
            }

            if (!paramsObject.TryGetValue("assetPath", out var assetPathToken) || assetPathToken.Type != JTokenType.String)
            {
                throw new ArgumentException("Parameter 'assetPath' is required and must be a string.");
            }

            var rawAssetPath = assetPathToken.Value<string>();
            var assetPath = NormalizeAndValidateAssetPath(rawAssetPath);

            var absoluteAssetPath = GetAbsoluteProjectPath(assetPath);
            var isFolder = Directory.Exists(absoluteAssetPath);
            var isFile = File.Exists(absoluteAssetPath);
            if (!isFolder && !isFile)
            {
                throw new ArgumentException($"Asset path '{assetPath}' does not exist in the Unity project.");
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh(ImportAssetOptions.Default);

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrWhiteSpace(guid))
            {
                throw new InvalidOperationException($"Unity did not return a GUID for imported asset '{assetPath}'.");
            }

            var mainAssetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);

            var result = new
            {
                assetPath,
                guid,
                isFolder,
                exists = true,
                mainAssetType = mainAssetType?.FullName,
                mainAssetName = mainAsset != null ? mainAsset.name : null,
                imported = true
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildPingAssetResponse(JToken idToken, JObject root)
        {
            var (assetPath, guid, targetObject, isFolder) = ResolveAssetNavigationTarget(root, "assets.ping");
            EditorGUIUtility.PingObject(targetObject);

            var result = new
            {
                pinged = true,
                assetPath,
                guid,
                isFolder,
                target = CreateObjectSummary(targetObject)
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildRevealAssetResponse(JToken idToken, JObject root)
        {
            var (assetPath, guid, targetObject, isFolder) = ResolveAssetNavigationTarget(root, "assets.reveal");

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = targetObject;
            EditorGUIUtility.PingObject(targetObject);

            var result = new
            {
                revealed = true,
                focusedProjectWindow = true,
                assetPath,
                guid,
                isFolder,
                target = CreateObjectSummary(targetObject)
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildCreateFolderResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "assets.createFolder");
            if (!paramsObject.TryGetValue("parentFolder", out var parentToken) || parentToken.Type != JTokenType.String)
                throw new ArgumentException("Parameter 'parentFolder' is required and must be a string.");
            if (!paramsObject.TryGetValue("folderName", out var nameToken) || nameToken.Type != JTokenType.String)
                throw new ArgumentException("Parameter 'folderName' is required and must be a string.");

            var parentFolder = parentToken.Value<string>()!;
            var folderName = nameToken.Value<string>()!;

            if (!AssetDatabase.IsValidFolder(parentFolder))
                throw new ArgumentException($"Parent folder '{parentFolder}' does not exist.");

            var guid = AssetDatabase.CreateFolder(parentFolder, folderName);
            var createdPath = AssetDatabase.GUIDToAssetPath(guid);

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                created = true,
                assetPath = createdPath,
                guid
            });
        }

        internal static string BuildCreateScriptResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "assets.createScript");
            if (!paramsObject.TryGetValue("assetPath", out var pathToken) || pathToken.Type != JTokenType.String)
                throw new ArgumentException("Parameter 'assetPath' is required and must be a string.");

            var assetPath = pathToken.Value<string>()!;
            if (!assetPath.EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Parameter 'assetPath' must end with '.cs'.");

            string content;
            if (paramsObject.TryGetValue("content", out var contentToken) && contentToken.Type == JTokenType.String)
            {
                content = contentToken.Value<string>()!;
            }
            else
            {
                var className = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                content = $"using UnityEngine;\n\npublic class {className} : MonoBehaviour\n{{\n    void Start()\n    {{\n    }}\n\n    void Update()\n    {{\n    }}\n}}\n";
            }

            var fullPath = System.IO.Path.Combine(Application.dataPath, "..", assetPath);
            fullPath = System.IO.Path.GetFullPath(fullPath);
            var dir = System.IO.Path.GetDirectoryName(fullPath)!;
            if (!System.IO.Directory.Exists(dir))
                System.IO.Directory.CreateDirectory(dir);

            System.IO.File.WriteAllText(fullPath, content);
            AssetDatabase.Refresh();

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            return UnityMcpProtocol.CreateResult(idToken, new
            {
                created = true,
                assetPath,
                guid
            });
        }

        internal static string BuildCreateMaterialResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "assets.createMaterial");
            if (!paramsObject.TryGetValue("assetPath", out var pathToken) || pathToken.Type != JTokenType.String)
                throw new ArgumentException("Parameter 'assetPath' is required and must be a string.");

            var assetPath = pathToken.Value<string>()!;
            var shaderName = "Standard";
            if (paramsObject.TryGetValue("shaderName", out var shaderToken) && shaderToken.Type == JTokenType.String)
                shaderName = shaderToken.Value<string>()!;

            var shader = Shader.Find(shaderName);
            if (shader == null)
                throw new ArgumentException($"Shader '{shaderName}' not found.");

            var material = new Material(shader);
            AssetDatabase.CreateAsset(material, assetPath);
            AssetDatabase.SaveAssets();

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            return UnityMcpProtocol.CreateResult(idToken, new
            {
                created = true,
                assetPath,
                guid,
                shaderName
            });
        }

        internal static string BuildCreatePrefabResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "assets.createPrefab");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            if (!paramsObject.TryGetValue("assetPath", out var pathToken) || pathToken.Type != JTokenType.String)
                throw new ArgumentException("Parameter 'assetPath' is required and must be a string.");

            var assetPath = pathToken.Value<string>()!;
            var gameObject = ResolveGameObjectFromInstanceId(instanceId, "assets.createPrefab");
            var prefab = PrefabUtility.SaveAsPrefabAsset(gameObject, assetPath);
            if (prefab == null)
                throw new System.Exception($"Failed to save prefab at '{assetPath}'.");

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            return UnityMcpProtocol.CreateResult(idToken, new
            {
                created = true,
                assetPath,
                guid,
                source = CreateObjectSummary(gameObject)
            });
        }

        internal static string BuildDeleteAssetResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "assets.delete");
            if (!paramsObject.TryGetValue("assetPath", out var pathToken) || pathToken.Type != JTokenType.String)
                throw new ArgumentException("Parameter 'assetPath' is required and must be a string.");

            var assetPath = pathToken.Value<string>()!;
            var deleted = AssetDatabase.DeleteAsset(assetPath);
            if (!deleted)
                throw new ArgumentException($"Failed to delete asset at '{assetPath}'. Check the path exists.");

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                deleted = true,
                assetPath
            });
        }

        internal static string BuildMoveAssetResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "assets.move");
            if (!paramsObject.TryGetValue("sourcePath", out var srcToken) || srcToken.Type != JTokenType.String)
                throw new ArgumentException("Parameter 'sourcePath' is required and must be a string.");
            if (!paramsObject.TryGetValue("destinationPath", out var dstToken) || dstToken.Type != JTokenType.String)
                throw new ArgumentException("Parameter 'destinationPath' is required and must be a string.");

            var sourcePath = srcToken.Value<string>()!;
            var destinationPath = dstToken.Value<string>()!;

            var error = AssetDatabase.MoveAsset(sourcePath, destinationPath);
            if (!string.IsNullOrEmpty(error))
                throw new ArgumentException($"Move failed: {error}");

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                moved = true,
                sourcePath,
                destinationPath,
                guid = AssetDatabase.AssetPathToGUID(destinationPath)
            });
        }

        internal static string BuildCreateScriptableObjectResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "assets.createScriptableObject");
            var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
            var typeName = ParseRequiredStringParameter(paramsObject, "typeName");

            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/"))
                throw new ArgumentException("Parameter 'assetPath' must be a project-relative path starting with 'Assets/'.");

            if (!assetPath.EndsWith(".asset"))
                assetPath += ".asset";

            // Ensure directory exists
            var dir = System.IO.Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                // Create directory hierarchy
                var parts = dir.Split('/');
                var current = parts[0];
                for (var i = 1; i < parts.Length; i++)
                {
                    var next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }

            // Find the type by name
            System.Type? soType = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                soType = asm.GetType(typeName);
                if (soType != null) break;
            }

            if (soType == null)
            {
                // Try short name search
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t.Name == typeName && t.IsSubclassOf(typeof(ScriptableObject)))
                        {
                            soType = t;
                            break;
                        }
                    }
                    if (soType != null) break;
                }
            }

            if (soType == null || !soType.IsSubclassOf(typeof(ScriptableObject)))
                throw new ArgumentException($"Type '{typeName}' was not found or is not a ScriptableObject subclass.");

            var instance = ScriptableObject.CreateInstance(soType);
            AssetDatabase.CreateAsset(instance, assetPath);
            AssetDatabase.SaveAssets();

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            return UnityMcpProtocol.CreateResult(idToken, new
            {
                assetPath,
                typeName = soType.FullName,
                guid,
                created = true
            });
        }

        // Helper methods used only by asset functions
        private static (string AssetPath, string Guid, UnityEngine.Object TargetObject, bool IsFolder)
            ResolveAssetNavigationTarget(JObject root, string methodName)
        {
            var paramsObject = RequireParamsObject(root, methodName);
            var rawAssetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
            var assetPath = NormalizeAndValidateAssetPath(rawAssetPath);
            var isFolder = AssetDatabase.IsValidFolder(assetPath);
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            var targetObject = AssetDatabase.LoadMainAssetAtPath(assetPath);

            if (targetObject == null && isFolder)
            {
                targetObject = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            }

            if (targetObject == null || string.IsNullOrWhiteSpace(guid))
            {
                throw new ArgumentException($"Asset path '{assetPath}' does not exist or is not available in the AssetDatabase.");
            }

            return (assetPath, guid, targetObject, isFolder);
        }

        private static string NormalizeAndValidateAssetPath(string? rawAssetPath)
        {
            if (string.IsNullOrWhiteSpace(rawAssetPath))
            {
                throw new ArgumentException("Parameter 'assetPath' cannot be empty.");
            }

            var normalized = rawAssetPath!.Trim().Replace('\\', '/');
            if (Path.IsPathRooted(normalized) || normalized.StartsWith("/", StringComparison.Ordinal))
            {
                throw new ArgumentException("Parameter 'assetPath' must be a Unity project-relative path under 'Assets/'.");
            }

            if (!string.Equals(normalized, "Assets", StringComparison.Ordinal) &&
                !normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException("Parameter 'assetPath' must start with 'Assets/'.");
            }

            var segments = normalized.Split('/');
            foreach (var segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment))
                {
                    throw new ArgumentException("Parameter 'assetPath' cannot contain empty path segments.");
                }

                if (string.Equals(segment, ".", StringComparison.Ordinal) ||
                    string.Equals(segment, "..", StringComparison.Ordinal))
                {
                    throw new ArgumentException("Parameter 'assetPath' cannot contain '.' or '..' path segments.");
                }
            }

            return normalized;
        }

        private static string GetAbsoluteProjectPath(string assetPath)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new InvalidOperationException("Unable to determine Unity project root path.");
            }

            var relativePath = assetPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(projectRoot, relativePath);
        }

        private static object CreateObjectSummary(UnityEngine.Object unityObject)
        {
            var unityType = unityObject.GetType();
            var assetPath = AssetDatabase.GetAssetPath(unityObject);
            var isPersistent = EditorUtility.IsPersistent(unityObject);

            string? sceneName = null;
            string? scenePath = null;
            string? hierarchyPath = null;
            bool? activeSelf = null;
            bool? activeInHierarchy = null;
            string? componentType = null;

            if (unityObject is GameObject gameObject)
            {
                var scene = gameObject.scene;
                sceneName = scene.name;
                scenePath = scene.path;
                hierarchyPath = GetHierarchyPath(gameObject.transform);
                activeSelf = gameObject.activeSelf;
                activeInHierarchy = gameObject.activeInHierarchy;
            }
            else if (unityObject is Component component)
            {
                var ownerGameObject = component.gameObject;
                var scene = ownerGameObject.scene;
                sceneName = scene.name;
                scenePath = scene.path;
                hierarchyPath = GetHierarchyPath(component.transform);
                activeSelf = ownerGameObject.activeSelf;
                activeInHierarchy = ownerGameObject.activeInHierarchy;
                componentType = unityType.FullName;
            }

            return new
            {
                instanceId = unityObject.GetInstanceID(),
                name = unityObject.name,
                unityType = unityType.FullName,
                isPersistent,
                assetPath = string.IsNullOrWhiteSpace(assetPath) ? null : assetPath,
                sceneName,
                scenePath,
                hierarchyPath,
                activeSelf,
                activeInHierarchy,
                componentType
            };
        }
    }
}
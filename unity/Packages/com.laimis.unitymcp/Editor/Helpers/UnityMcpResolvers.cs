#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMcp.Editor
{
    internal static class UnityMcpResolvers
    {
        internal static UnityEngine.Object ResolveObjectByInstanceId(int instanceId, string parameterName)
        {
            var resolved = TryResolveObjectByEntityId(instanceId) ?? ResolveObjectByLegacyInstanceId(instanceId);
            if (resolved == null)
            {
                throw new ArgumentException($"No Unity object found for instanceId {instanceId}.", parameterName);
            }

            return resolved;
        }

        internal static GameObject ResolveGameObjectTarget(UnityEngine.Object resolvedObject, string parameterName)
        {
            return resolvedObject switch
            {
                GameObject gameObject => gameObject,
                Component component => component.gameObject,
                _ => throw new ArgumentException(
                    $"Parameter '{parameterName}' must reference a GameObject or Component instance.")
            };
        }

        internal static Transform ResolveTransformTarget(UnityEngine.Object resolvedObject, string parameterName)
        {
            return resolvedObject switch
            {
                Transform transform => transform,
                GameObject gameObject => gameObject.transform,
                Component component => component.transform,
                _ => throw new ArgumentException(
                    $"Parameter '{parameterName}' must reference a GameObject or Component with a Transform.")
            };
        }

        internal static Component ResolveComponentTarget(UnityEngine.Object resolvedObject, string parameterName)
        {
            if (resolvedObject is not Component component)
            {
                throw new ArgumentException($"Parameter '{parameterName}' must reference a Component instance.");
            }

            return component;
        }

        internal static TComponent ResolveComponentOfTypeTarget<TComponent>(
            UnityEngine.Object resolvedObject,
            string parameterName,
            string componentTypeName)
            where TComponent : Component
        {
            if (resolvedObject is TComponent directComponent)
            {
                return directComponent;
            }

            GameObject gameObject = resolvedObject switch
            {
                GameObject go => go,
                Component component => component.gameObject,
                _ => throw new ArgumentException(
                    $"Parameter '{parameterName}' must reference a {componentTypeName} component or a GameObject containing one.")
            };

            var matches = gameObject.GetComponents<TComponent>();
            if (matches.Length == 1)
            {
                return matches[0];
            }

            if (matches.Length == 0)
            {
                throw new ArgumentException(
                    $"Parameter '{parameterName}' must reference a {componentTypeName} component or a GameObject containing one.");
            }

            throw new ArgumentException(
                $"Parameter '{parameterName}' resolves to GameObject '{gameObject.name}' with multiple {componentTypeName} components. Use the specific component instanceId.");
        }

        internal static GameObject ResolveSceneGameObjectTarget(UnityEngine.Object resolvedObject, string parameterName)
        {
            var gameObject = ResolveGameObjectTarget(resolvedObject, parameterName);
            ValidateDestroyableSceneObject(gameObject, parameterName);
            return gameObject;
        }

        internal static Component ResolveSceneComponentTarget(UnityEngine.Object resolvedObject, string parameterName)
        {
            var component = ResolveComponentTarget(resolvedObject, parameterName);
            ValidateDestroyableSceneObject(component, parameterName);
            return component;
        }

        internal static Component ResolveSceneComponentTargetAllowingTransform(UnityEngine.Object resolvedObject, string parameterName)
        {
            var component = ResolveComponentTarget(resolvedObject, parameterName);
            ValidateDestroyableSceneObject(component.gameObject, parameterName);
            return component;
        }

        internal static GameObject ResolveGameObjectFromInstanceId(int instanceId, string methodName)
        {
            var resolved = ResolveObjectByInstanceId(instanceId, "instanceId");
            return ResolveGameObjectTarget(resolved, "instanceId");
        }

        internal static (T component, GameObject ownerGo) ResolveComponentFromInstanceId<T>(int instanceId, string methodName)
            where T : Component
        {
            var resolved = ResolveObjectByInstanceId(instanceId, "instanceId");
            var component = ResolveComponentOfTypeTarget<T>(resolved, "instanceId", typeof(T).Name);
            return (component, component.gameObject);
        }

        internal static GameObject ResolveGameObjectByHierarchyPath(string rawPath, string? rawScenePath, string parameterName)
        {
            var (normalizedPath, normalizedScenePath, allMatches, activeMatches) = FindGameObjectsByHierarchyPath(rawPath, rawScenePath);
            var activeScene = SceneManager.GetActiveScene();

            if (!string.IsNullOrWhiteSpace(normalizedScenePath))
            {
                if (allMatches.Count == 1)
                {
                    return allMatches[0];
                }

                if (allMatches.Count == 0)
                {
                    throw new ArgumentException(
                        $"No scene object found for path '{normalizedPath}' in scene '{normalizedScenePath}'.",
                        parameterName);
                }

                throw new ArgumentException(
                    $"Multiple objects match path '{normalizedPath}' in scene '{normalizedScenePath}'. Use instanceId-based selection.",
                    parameterName);
            }

            if (activeMatches.Count == 1)
            {
                return activeMatches[0];
            }

            if (activeMatches.Count > 1)
            {
                throw new ArgumentException(
                    $"Multiple objects match path '{normalizedPath}' in active scene '{activeScene.name}'. Add disambiguation or use instanceId-based selection.",
                    parameterName);
            }

            if (allMatches.Count == 1)
            {
                return allMatches[0];
            }

            if (allMatches.Count == 0)
            {
                throw new ArgumentException($"No scene object found for path '{normalizedPath}'.", parameterName);
            }

            throw new ArgumentException(
                $"Multiple objects match path '{normalizedPath}' across open scenes. Use instanceId-based selection.",
                parameterName);
        }

        internal static string? NormalizeOptionalScenePath(string? scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return null;
            }

            var normalized = scenePath!.Trim().Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            return normalized;
        }

        internal static Type ResolveComponentType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                throw new ArgumentException("Parameter 'typeName' cannot be empty.");
            }

            var trimmedTypeName = typeName.Trim();

            var directType = Type.GetType(trimmedTypeName, throwOnError: false);
            if (directType != null)
            {
                ValidateResolvedComponentType(directType, trimmedTypeName);
                return directType;
            }

            var fullNameMatches = new List<Type>();
            var shortNameMatches = new List<Type>();
            foreach (var candidateType in TypeCache.GetTypesDerivedFrom<Component>())
            {
                if (candidateType == null || !IsSupportedAddComponentType(candidateType))
                {
                    continue;
                }

                if (string.Equals(candidateType.FullName, trimmedTypeName, StringComparison.Ordinal))
                {
                    fullNameMatches.Add(candidateType);
                }

                if (string.Equals(candidateType.Name, trimmedTypeName, StringComparison.Ordinal))
                {
                    shortNameMatches.Add(candidateType);
                }
            }

            if (fullNameMatches.Count == 1)
            {
                return fullNameMatches[0];
            }

            if (fullNameMatches.Count > 1)
            {
                throw new ArgumentException(
                    $"Component type '{trimmedTypeName}' is ambiguous. Use an assembly-qualified type name.");
            }

            if (shortNameMatches.Count == 1)
            {
                return shortNameMatches[0];
            }

            if (shortNameMatches.Count > 1)
            {
                var names = new List<string>(shortNameMatches.Count);
                foreach (var match in shortNameMatches)
                {
                    names.Add(match.FullName ?? match.Name);
                }

                throw new ArgumentException(
                    $"Component type '{trimmedTypeName}' is ambiguous. Matches: {string.Join(", ", names)}");
            }

            throw new ArgumentException($"Component type '{trimmedTypeName}' was not found.");
        }

        internal static void ValidateResolvedComponentType(Type componentType, string requestedTypeName)
        {
            if (!typeof(Component).IsAssignableFrom(componentType) || !IsSupportedAddComponentType(componentType))
            {
                throw new ArgumentException($"Component type '{requestedTypeName}' is not a supported Unity Component type.");
            }
        }

        internal static AudioListener ResolveAudioListener(JObject paramsObject, string commandName)
        {
            var instanceIdToken = paramsObject["instanceId"];
            if (instanceIdToken != null && instanceIdToken.Type != JTokenType.Null)
            {
                var instanceId = (int)instanceIdToken;
                var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
                return ResolveComponentOfTypeTarget<AudioListener>(resolvedObject, "instanceId", "AudioListener");
            }

            var listener = UnityEngine.Object.FindFirstObjectByType<AudioListener>();
            if (listener == null)
                throw new ArgumentException("No AudioListener found in the scene. Add an AudioListener component to a GameObject (typically the Main Camera).");
            return listener;
        }

        internal static UnityEngine.SceneManagement.Scene FindOpenSceneByPathOrName(string pathOrName, string methodName)
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

        internal static void CollectHierarchyPathMatches(
            Transform transform,
            string normalizedPath,
            List<GameObject> allMatches,
            List<GameObject> activeMatches,
            int activeSceneHandle)
        {
            if (string.Equals(GetHierarchyPath(transform), normalizedPath, StringComparison.Ordinal))
            {
                var gameObject = transform.gameObject;
                allMatches.Add(gameObject);

                if (gameObject.scene.handle == activeSceneHandle)
                {
                    activeMatches.Add(gameObject);
                }
            }

            var childCount = transform.childCount;
            for (var childIndex = 0; childIndex < childCount; childIndex++)
            {
                var child = transform.GetChild(childIndex);
                CollectHierarchyPathMatches(child, normalizedPath, allMatches, activeMatches, activeSceneHandle);
            }
        }

        internal static string GetHierarchyPath(Transform transform)
        {
            var names = new Stack<string>();
            var current = transform;

            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }

        internal static bool IsSupportedAddComponentType(Type componentType)
        {
            return componentType.IsClass &&
                   !componentType.IsAbstract &&
                   !componentType.IsGenericTypeDefinition &&
                   typeof(Component).IsAssignableFrom(componentType);
        }

        private static UnityEngine.Object? TryResolveObjectByEntityId(int instanceId)
        {
            try
            {
                var editorUtilityType = typeof(EditorUtility);
                var intMethod = editorUtilityType.GetMethod(
                    "EntityIdToObject",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(int) },
                    modifiers: null);

                if (intMethod != null)
                {
                    return intMethod.Invoke(null, new object[] { instanceId }) as UnityEngine.Object;
                }

                var longMethod = editorUtilityType.GetMethod(
                    "EntityIdToObject",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(long) },
                    modifiers: null);

                if (longMethod != null)
                {
                    return longMethod.Invoke(null, new object[] { (long)instanceId }) as UnityEngine.Object;
                }
            }
            catch
            {
                // Fall back to the legacy API if the newer API is unavailable or throws.
            }

            return null;
        }

        private static UnityEngine.Object? ResolveObjectByLegacyInstanceId(int instanceId)
        {
#pragma warning disable CS0618 // Unity 6 deprecates InstanceIDToObject in favor of EntityIdToObject.
            return EditorUtility.InstanceIDToObject(instanceId);
#pragma warning restore CS0618
        }

        private static void ValidateDestroyableSceneObject(GameObject gameObject, string parameterName)
        {
            if (EditorUtility.IsPersistent(gameObject))
            {
                throw new ArgumentException($"Parameter '{parameterName}' must reference a scene object, not an asset/prefab.");
            }

            if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
            {
                throw new ArgumentException($"Parameter '{parameterName}' must reference an object in a loaded scene.");
            }
        }

        private static void ValidateDestroyableSceneObject(Component component, string parameterName)
        {
            if (component is Transform)
            {
                throw new ArgumentException("Destroying a Transform component directly is not supported. Destroy the GameObject instead.");
            }

            ValidateDestroyableSceneObject(component.gameObject, parameterName);
        }

        private static string NormalizeHierarchyPath(string path)
        {
            var normalized = path.Trim().Replace('\\', '/');
            if (normalized.StartsWith("/", StringComparison.Ordinal) || normalized.EndsWith("/", StringComparison.Ordinal))
            {
                throw new ArgumentException("Parameter 'path' must not start or end with '/'.");
            }

            if (normalized.Contains("//", StringComparison.Ordinal))
            {
                throw new ArgumentException("Parameter 'path' must not contain empty path segments.");
            }

            return normalized;
        }

        private static (string NormalizedPath, string? NormalizedScenePath, List<GameObject> AllMatches, List<GameObject> ActiveMatches)
            FindGameObjectsByHierarchyPath(string rawPath, string? rawScenePath)
        {
            var normalizedPath = NormalizeHierarchyPath(rawPath);
            var normalizedScenePath = NormalizeOptionalScenePath(rawScenePath);
            var activeScene = SceneManager.GetActiveScene();
            var activeMatches = new List<GameObject>();
            var allMatches = new List<GameObject>();

            var sceneCount = SceneManager.sceneCount;
            for (var sceneIndex = 0; sceneIndex < sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(normalizedScenePath) &&
                    !string.Equals(scene.path, normalizedScenePath, StringComparison.Ordinal))
                {
                    continue;
                }

                var rootObjects = scene.GetRootGameObjects();
                foreach (var rootObject in rootObjects)
                {
                    CollectHierarchyPathMatches(rootObject.transform, normalizedPath, allMatches, activeMatches, activeScene.handle);
                }
            }

            return (normalizedPath, normalizedScenePath, allMatches, activeMatches);
        }
    }
}

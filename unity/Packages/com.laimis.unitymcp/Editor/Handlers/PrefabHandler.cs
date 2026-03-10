#nullable enable

using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static UnityMcp.Editor.UnityMcpParameterHelpers;
using static UnityMcp.Editor.UnityMcpResolvers;
using static UnityMcp.Editor.UnityMcpSnapshotHelpers;

namespace UnityMcp.Editor
{
    internal static class PrefabHandler
    {
        internal static string BuildInstantiatePrefabResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "prefab.instantiate");
            var assetPath = NormalizeAndValidateAssetPath(ParseRequiredStringParameter(paramsObject, "assetPath"));
            var parentInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "parentInstanceId");
            var position = ParseOptionalVector3Parameter(paramsObject, "position");
            var rotationEuler = ParseOptionalVector3Parameter(paramsObject, "rotationEuler");
            var select = ParseOptionalBooleanParameter(paramsObject, "select", true);
            var ping = ParseOptionalBooleanParameter(paramsObject, "ping");
            var focus = ParseOptionalBooleanParameter(paramsObject, "focus");

            var prefabAsset = LoadPrefabAsset(assetPath);
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                throw new InvalidOperationException("No active loaded scene is available for prefab instantiation.");
            }

            GameObject? parentGameObject = null;
            if (parentInstanceId.IsSpecified && parentInstanceId.HasValue)
            {
                var resolvedParentObject = ResolveObjectByInstanceId(parentInstanceId.Value!.Value, "parentInstanceId");
                parentGameObject = ResolveSceneGameObjectTarget(resolvedParentObject, "parentInstanceId");
                if (parentGameObject.scene != activeScene)
                {
                    throw new ArgumentException("Cross-scene parenting is not supported in the MVP. Parent must be in the active loaded scene.");
                }
            }

            var instanceObject = PrefabUtility.InstantiatePrefab(prefabAsset, activeScene);
            if (instanceObject is not GameObject instance)
            {
                throw new InvalidOperationException($"Unity did not return a GameObject when instantiating prefab '{assetPath}'.");
            }

            Undo.RegisterCreatedObjectUndo(instance, "UnityMCP Instantiate Prefab");

            if (parentGameObject != null)
            {
                Undo.SetTransformParent(instance.transform, parentGameObject.transform, "UnityMCP Instantiate Prefab");
            }

            if (position.HasValue || rotationEuler.HasValue)
            {
                Undo.RecordObject(instance.transform, "UnityMCP Instantiate Prefab");
                if (position.HasValue)
                {
                    instance.transform.position = position.Value;
                }

                if (rotationEuler.HasValue)
                {
                    instance.transform.rotation = Quaternion.Euler(rotationEuler.Value);
                }
            }

            if (select)
            {
                Selection.activeGameObject = instance;
                ApplySelectionEditorPresentation(instance, ping, focus);
            }
            else
            {
                ApplySceneObjectPresentationWithoutSelection(instance, ping, focus);
            }

            var result = new
            {
                instance = CreateObjectSummary(instance),
                prefabSource = CreatePrefabAssetSummary(prefabAsset, instance),
                selection = BuildSelectionSummaryResult(),
                applied = new
                {
                    parent = parentGameObject != null,
                    position = position.HasValue,
                    rotationEuler = rotationEuler.HasValue,
                    selected = select,
                    ping,
                    focus
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildGetPrefabSourceResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "prefab.getSource");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var targetGameObject = ResolveSceneGameObjectTarget(resolvedObject, "instanceId");

            var prefabDetails = InspectPrefabInstance(targetGameObject, "instanceId");

            var result = new
            {
                target = CreateObjectSummary(targetGameObject),
                prefabInstanceStatus = prefabDetails.PrefabInstanceStatus,
                prefabAssetType = prefabDetails.PrefabAssetType,
                instanceRoot = CreateObjectSummary(prefabDetails.OutermostPrefabInstanceRoot),
                sourceAsset = CreatePrefabAssetSummary(prefabDetails.SourceAsset, prefabDetails.OutermostPrefabInstanceRoot),
                nearestPrefabInstanceRoot = CreateObjectSummary(prefabDetails.NearestPrefabInstanceRoot),
                isOutermostPrefabInstanceRoot = prefabDetails.IsOutermostPrefabInstanceRoot
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildApplyPrefabOverridesResponse(JToken idToken, JObject root)
        {
            var result = ApplyPrefabOverrides(root, "prefab.applyOverrides", revert: false);
            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildRevertPrefabOverridesResponse(JToken idToken, JObject root)
        {
            var result = ApplyPrefabOverrides(root, "prefab.revertOverrides", revert: true);
            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static GameObject LoadPrefabAsset(string assetPath)
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefabAsset == null || !PrefabUtility.IsPartOfPrefabAsset(prefabAsset))
            {
                throw new ArgumentException($"Asset path '{assetPath}' does not point to a prefab asset.");
            }

            return prefabAsset;
        }

        private static PrefabInstanceDetails InspectPrefabInstance(GameObject targetGameObject, string parameterName)
        {
            var prefabInstanceStatus = PrefabUtility.GetPrefabInstanceStatus(targetGameObject);
            if (prefabInstanceStatus == PrefabInstanceStatus.NotAPrefab)
            {
                throw new ArgumentException($"Parameter '{parameterName}' must reference an object that is part of a prefab instance.");
            }

            var nearestPrefabInstanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(targetGameObject);
            var outermostPrefabInstanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(targetGameObject);
            if (nearestPrefabInstanceRoot == null || outermostPrefabInstanceRoot == null)
            {
                throw new ArgumentException($"Parameter '{parameterName}' must reference an object that is part of a prefab instance.");
            }

            var assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(targetGameObject);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new ArgumentException($"Parameter '{parameterName}' does not resolve to a prefab source asset.");
            }

            var sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (sourceAsset == null)
            {
                throw new ArgumentException($"Prefab source asset '{assetPath}' could not be loaded.");
            }

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrWhiteSpace(guid))
            {
                throw new ArgumentException($"Prefab source asset '{assetPath}' does not have a valid GUID.");
            }

            return new PrefabInstanceDetails(
                targetGameObject,
                nearestPrefabInstanceRoot,
                outermostPrefabInstanceRoot,
                sourceAsset,
                assetPath,
                guid,
                prefabInstanceStatus.ToString(),
                PrefabUtility.GetPrefabAssetType(targetGameObject).ToString());
        }

        private static object ApplyPrefabOverrides(JObject root, string methodName, bool revert)
        {
            var paramsObject = RequireParamsObject(root, methodName);
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var scope = ParseOptionalPrefabOverrideScopeParameter(paramsObject, "scope");
            var componentInstanceId = ParseOptionalIntegerParameter(paramsObject, "componentInstanceId");

            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var targetGameObject = ResolveSceneGameObjectTarget(resolvedObject, "instanceId");
            var prefabDetails = InspectPrefabInstance(targetGameObject, "instanceId");

            Component? componentTarget = null;
            switch (scope)
            {
                case PrefabOverrideScope.InstanceRoot:
                    if (revert)
                    {
                        PrefabUtility.RevertPrefabInstance(prefabDetails.OutermostPrefabInstanceRoot, InteractionMode.UserAction);
                    }
                    else
                    {
                        PrefabUtility.ApplyPrefabInstance(prefabDetails.OutermostPrefabInstanceRoot, InteractionMode.UserAction);
                    }

                    break;

                case PrefabOverrideScope.Object:
                    if (revert)
                    {
                        PrefabUtility.RevertObjectOverride(targetGameObject, InteractionMode.UserAction);
                    }
                    else
                    {
                        PrefabUtility.ApplyObjectOverride(targetGameObject, prefabDetails.AssetPath, InteractionMode.UserAction);
                    }

                    break;

                case PrefabOverrideScope.Component:
                    componentTarget = ResolvePrefabOverrideComponentTarget(resolvedObject, targetGameObject, componentInstanceId);
                    if (revert)
                    {
                        PrefabUtility.RevertObjectOverride(componentTarget, InteractionMode.UserAction);
                    }
                    else
                    {
                        PrefabUtility.ApplyObjectOverride(componentTarget, prefabDetails.AssetPath, InteractionMode.UserAction);
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            AssetDatabase.SaveAssets();

            return new
            {
                target = CreateObjectSummary(targetGameObject),
                scope = CreatePrefabOverrideScopeName(scope),
                prefabSource = CreatePrefabAssetSummary(prefabDetails.SourceAsset, prefabDetails.OutermostPrefabInstanceRoot),
                applied = new
                {
                    scope = CreatePrefabOverrideScopeName(scope),
                    componentInstanceId = componentTarget != null ? componentTarget.GetInstanceID() : (int?)null
                }
            };
        }

        private static Component ResolvePrefabOverrideComponentTarget(
            UnityEngine.Object resolvedObject,
            GameObject targetGameObject,
            int? componentInstanceId)
        {
            Component? componentTarget = null;

            if (componentInstanceId.HasValue)
            {
                var resolvedComponentObject = ResolveObjectByInstanceId(componentInstanceId.Value, "componentInstanceId");
                componentTarget = ResolveSceneComponentTargetAllowingTransform(resolvedComponentObject, "componentInstanceId");
            }
            else if (resolvedObject is Component resolvedComponent)
            {
                componentTarget = ResolveSceneComponentTargetAllowingTransform(resolvedComponent, "instanceId");
            }

            if (componentTarget == null)
            {
                throw new ArgumentException("Scope 'component' requires 'componentInstanceId' or an 'instanceId' that resolves to a Component.");
            }

            if (componentTarget.gameObject != targetGameObject)
            {
                throw new ArgumentException("Parameter 'componentInstanceId' must reference a component on the resolved target object.");
            }

            return componentTarget;
        }

        private static string CreatePrefabOverrideScopeName(PrefabOverrideScope scope)
        {
            return scope switch
            {
                PrefabOverrideScope.InstanceRoot => "instanceRoot",
                PrefabOverrideScope.Object => "object",
                PrefabOverrideScope.Component => "component",
                _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
            };
        }

        private static object CreatePrefabAssetSummary(GameObject prefabAsset, GameObject instanceContext)
        {
            var assetPath = AssetDatabase.GetAssetPath(prefabAsset);
            var guid = AssetDatabase.AssetPathToGUID(assetPath);

            return new
            {
                instanceId = prefabAsset.GetInstanceID(),
                name = prefabAsset.name,
                unityType = prefabAsset.GetType().FullName,
                assetPath = string.IsNullOrWhiteSpace(assetPath) ? null : assetPath,
                guid = string.IsNullOrWhiteSpace(guid) ? null : guid,
                prefabInstanceStatus = PrefabUtility.GetPrefabInstanceStatus(instanceContext).ToString(),
                prefabAssetType = PrefabUtility.GetPrefabAssetType(instanceContext).ToString()
            };
        }
    }
}
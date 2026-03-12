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
        private static string BuildInstantiatePrefabResponse(JToken idToken, JObject root)
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

        private static string BuildGetPrefabSourceResponse(JToken idToken, JObject root)
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

        private static string BuildApplyPrefabOverridesResponse(JToken idToken, JObject root)
        {
            var result = ApplyPrefabOverrides(root, "prefab.applyOverrides", revert: false);
            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildRevertPrefabOverridesResponse(JToken idToken, JObject root)
        {
            var result = ApplyPrefabOverrides(root, "prefab.revertOverrides", revert: true);
            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildSceneInstantiatePrefabResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.instantiatePrefab");
            var assetPath = NormalizeAndValidateAssetPath(ParseRequiredStringParameter(paramsObject, "assetPath"));
            var position = ParseOptionalVector3Parameter(paramsObject, "position");
            var parentInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "parentInstanceId");

            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefabAsset == null)
            {
                throw new ArgumentException($"No prefab found at asset path '{assetPath}'.");
            }

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
                    throw new ArgumentException("Cross-scene parenting is not supported. Parent must be in the active loaded scene.");
                }
            }

            var instanceObject = PrefabUtility.InstantiatePrefab(prefabAsset, activeScene);
            if (instanceObject is not GameObject instance)
            {
                throw new InvalidOperationException($"Unity did not return a GameObject when instantiating prefab '{assetPath}'.");
            }

            Undo.RegisterCreatedObjectUndo(instance, "UnityMCP Scene Instantiate Prefab");

            if (parentGameObject != null)
            {
                Undo.SetTransformParent(instance.transform, parentGameObject.transform, "UnityMCP Scene Instantiate Prefab");
            }

            if (position.HasValue)
            {
                Undo.RecordObject(instance.transform, "UnityMCP Scene Instantiate Prefab");
                instance.transform.position = position.Value;
            }

            Selection.activeGameObject = instance;

            var result = new
            {
                instance = CreateObjectSummary(instance),
                assetPath,
                parent = parentGameObject != null ? CreateObjectSummary(parentGameObject) : null,
                applied = new
                {
                    position = position.HasValue,
                    parent = parentGameObject != null
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }
    }
}

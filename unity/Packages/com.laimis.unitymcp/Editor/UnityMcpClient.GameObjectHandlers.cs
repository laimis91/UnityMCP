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
        private static string BuildGetSelectionResponse(JToken idToken)
        {
            return UnityMcpProtocol.CreateResult(idToken, BuildSelectionSummaryResult());
        }

        private static string BuildSelectObjectResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.selectObject");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var ping = ParseOptionalBooleanParameter(paramsObject, "ping");
            var focus = ParseOptionalBooleanParameter(paramsObject, "focus");
            var targetObject = ResolveObjectByInstanceId(instanceId, "instanceId");

            Selection.activeObject = targetObject;
            Selection.objects = new[] { targetObject };
            ApplySelectionEditorPresentation(targetObject, ping, focus);

            return UnityMcpProtocol.CreateResult(idToken, BuildSelectionSummaryResult());
        }

        private static string BuildSelectByPathResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.selectByPath");
            var path = ParseRequiredStringParameter(paramsObject, "path");
            var scenePath = ParseOptionalStringParameter(paramsObject, "scenePath");
            var ping = ParseOptionalBooleanParameter(paramsObject, "ping");
            var focus = ParseOptionalBooleanParameter(paramsObject, "focus");
            var targetObject = ResolveGameObjectByHierarchyPath(path, scenePath, "path");

            Selection.activeGameObject = targetObject;
            Selection.objects = new UnityEngine.Object[] { targetObject };
            ApplySelectionEditorPresentation(targetObject, ping, focus);

            return UnityMcpProtocol.CreateResult(idToken, BuildSelectionSummaryResult());
        }

        private static string BuildFindByPathResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.findByPath");
            var path = ParseRequiredStringParameter(paramsObject, "path");
            var scenePath = ParseOptionalStringParameter(paramsObject, "scenePath");
            var (normalizedPath, normalizedScenePath, allMatches, _) = FindGameObjectsByHierarchyPath(path, scenePath);

            var items = new List<object>(allMatches.Count);
            foreach (var match in allMatches)
            {
                items.Add(CreateObjectSummary(match));
            }

            var result = new
            {
                path = normalizedPath,
                scenePath = normalizedScenePath,
                count = items.Count,
                items
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildFindByTagResponse(JToken idToken, JObject root)
        {
            if (!root.TryGetValue("params", out var paramsToken) || paramsToken is not JObject paramsObject)
            {
                throw new ArgumentException("Method 'scene.findByTag' expects params to be an object.");
            }

            if (!paramsObject.TryGetValue("tag", out var tagToken) || tagToken.Type != JTokenType.String)
            {
                throw new ArgumentException("Parameter 'tag' is required and must be a string.");
            }

            var tag = tagToken.Value<string>();
            if (string.IsNullOrWhiteSpace(tag))
            {
                throw new ArgumentException("Parameter 'tag' cannot be empty.");
            }

            GameObject[] matches;
            try
            {
                matches = GameObject.FindGameObjectsWithTag(tag);
            }
            catch (UnityException ex)
            {
                throw new ArgumentException(ex.Message);
            }

            var items = new List<object>(matches.Length);
            foreach (var gameObject in matches)
            {
                var transform = gameObject.transform;
                var position = transform.position;
                var scene = gameObject.scene;

                items.Add(new
                {
                    instanceId = gameObject.GetInstanceID(),
                    name = gameObject.name,
                    tag = gameObject.tag,
                    activeSelf = gameObject.activeSelf,
                    activeInHierarchy = gameObject.activeInHierarchy,
                    sceneName = scene.name,
                    scenePath = scene.path,
                    hierarchyPath = GetHierarchyPath(transform),
                    position = new[] { position.x, position.y, position.z }
                });
            }

            var result = new
            {
                tag,
                count = matches.Length,
                items
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildCreateGameObjectResponse(JToken idToken, JObject root)
        {
            var name = "GameObject";
            Vector3? position = null;

            if (root.TryGetValue("params", out var paramsToken) && paramsToken.Type != JTokenType.Null)
            {
                if (paramsToken is not JObject paramsObject)
                {
                    throw new ArgumentException("Method 'scene.createGameObject' expects params to be an object.");
                }

                if (paramsObject.TryGetValue("name", out var nameToken))
                {
                    if (nameToken.Type != JTokenType.String)
                    {
                        throw new ArgumentException("Parameter 'name' must be a string.");
                    }

                    var parsedName = nameToken.Value<string>();
                    if (string.IsNullOrWhiteSpace(parsedName))
                    {
                        throw new ArgumentException("Parameter 'name' cannot be empty.");
                    }

                    name = parsedName;
                }

                if (paramsObject.TryGetValue("position", out var positionToken))
                {
                    position = ParsePosition(positionToken);
                }
            }

            var gameObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(gameObject, "UnityMCP Create GameObject");

            if (position.HasValue)
            {
                gameObject.transform.position = position.Value;
            }

            Selection.activeGameObject = gameObject;

            var activeScene = SceneManager.GetActiveScene();
            var currentPosition = gameObject.transform.position;
            var result = new
            {
                instanceId = gameObject.GetInstanceID(),
                name = gameObject.name,
                sceneName = activeScene.name,
                scenePath = activeScene.path,
                hierarchyPath = GetHierarchyPath(gameObject.transform),
                position = new[] { currentPosition.x, currentPosition.y, currentPosition.z }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildSetParentResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.setParent");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var parentInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "parentInstanceId");
            var keepWorldTransform = ParseOptionalBooleanParameter(paramsObject, "keepWorldTransform", true);
            var ping = ParseOptionalBooleanParameter(paramsObject, "ping");
            var focus = ParseOptionalBooleanParameter(paramsObject, "focus");

            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var targetGameObject = ResolveSceneGameObjectTarget(resolvedObject, "instanceId");
            var targetTransform = targetGameObject.transform;
            var originalLocalPosition = targetTransform.localPosition;
            var originalLocalRotation = targetTransform.localRotation;
            var originalLocalScale = targetTransform.localScale;

            GameObject? parentGameObject = null;
            if (parentInstanceId.IsSpecified && parentInstanceId.HasValue)
            {
                var resolvedParentObject = ResolveObjectByInstanceId(parentInstanceId.Value!.Value, "parentInstanceId");
                parentGameObject = ResolveSceneGameObjectTarget(resolvedParentObject, "parentInstanceId");

                if (parentGameObject == targetGameObject)
                {
                    throw new ArgumentException("Parameter 'parentInstanceId' cannot reference the same object as 'instanceId'.");
                }

                if (parentGameObject.transform.IsChildOf(targetTransform))
                {
                    throw new ArgumentException("Parameter 'parentInstanceId' cannot reference a descendant of the target object.");
                }

                if (parentGameObject.scene != targetGameObject.scene)
                {
                    throw new ArgumentException("Cross-scene parenting is not supported in the MVP.");
                }
            }

            Undo.IncrementCurrentGroup();
            Undo.SetTransformParent(targetTransform, parentGameObject != null ? parentGameObject.transform : null, "UnityMCP Set Parent");
            if (!keepWorldTransform)
            {
                Undo.RecordObject(targetTransform, "UnityMCP Set Parent");
                targetTransform.localPosition = originalLocalPosition;
                targetTransform.localRotation = originalLocalRotation;
                targetTransform.localScale = originalLocalScale;
            }

            EditorUtility.SetDirty(targetTransform);
            Selection.activeGameObject = targetGameObject;
            ApplySelectionEditorPresentation(targetGameObject, ping, focus);

            var result = new
            {
                target = CreateObjectSummary(targetGameObject),
                parent = parentGameObject != null ? CreateObjectSummary(parentGameObject) : null,
                keepWorldTransform,
                selection = BuildSelectionSummaryResult(),
                applied = new
                {
                    reparented = parentGameObject != null,
                    unparented = parentGameObject == null,
                    ping,
                    focus
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildDuplicateObjectResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.duplicateObject");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var select = ParseOptionalBooleanParameter(paramsObject, "select", true);
            var ping = ParseOptionalBooleanParameter(paramsObject, "ping");
            var focus = ParseOptionalBooleanParameter(paramsObject, "focus");

            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var sourceGameObject = ResolveSceneGameObjectTarget(resolvedObject, "instanceId");
            var sourceTransform = sourceGameObject.transform;
            var parentTransform = sourceTransform.parent;

            var duplicate = UnityEngine.Object.Instantiate(sourceGameObject, parentTransform);
            duplicate.name = sourceGameObject.name;
            if (duplicate.scene != sourceGameObject.scene && sourceGameObject.scene.IsValid() && sourceGameObject.scene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(duplicate, sourceGameObject.scene);
            }

            duplicate.transform.SetSiblingIndex(sourceTransform.GetSiblingIndex() + 1);
            Undo.RegisterCreatedObjectUndo(duplicate, "UnityMCP Duplicate Object");

            if (select)
            {
                Selection.activeGameObject = duplicate;
                ApplySelectionEditorPresentation(duplicate, ping, focus);
            }
            else
            {
                ApplySceneObjectPresentationWithoutSelection(duplicate, ping, focus);
            }

            var result = new
            {
                source = CreateObjectSummary(sourceGameObject),
                duplicate = CreateObjectSummary(duplicate),
                selection = BuildSelectionSummaryResult(),
                applied = new
                {
                    selected = select,
                    ping,
                    focus
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildRenameObjectResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.renameObject");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var name = ParseRequiredStringParameter(paramsObject, "name");

            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var targetGameObject = ResolveSceneGameObjectTarget(resolvedObject, "instanceId");
            var previousName = targetGameObject.name;

            Undo.RecordObject(targetGameObject, "UnityMCP Rename Object");
            targetGameObject.name = name;
            EditorUtility.SetDirty(targetGameObject);

            var result = new
            {
                target = CreateObjectSummary(targetGameObject),
                previousName,
                currentName = targetGameObject.name,
                applied = new
                {
                    name = targetGameObject.name
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildSetActiveResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.setActive");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var active = ParseRequiredBooleanParameter(paramsObject, "active");

            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var targetGameObject = ResolveSceneGameObjectTarget(resolvedObject, "instanceId");

            Undo.RecordObject(targetGameObject, "UnityMCP Set Active");
            targetGameObject.SetActive(active);
            EditorUtility.SetDirty(targetGameObject);

            var result = new
            {
                target = CreateObjectSummary(targetGameObject),
                activeSelf = targetGameObject.activeSelf,
                activeInHierarchy = targetGameObject.activeInHierarchy,
                applied = new
                {
                    active
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildDestroyObjectResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.destroyObject");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");

            if (resolvedObject is Transform)
            {
                throw new ArgumentException("Destroying a Transform component directly is not supported. Destroy the GameObject instead.");
            }

            string destroyedKind;
            if (resolvedObject is GameObject gameObject)
            {
                ValidateDestroyableSceneObject(gameObject, "instanceId");
                destroyedKind = "gameObject";
            }
            else if (resolvedObject is Component component)
            {
                ValidateDestroyableSceneObject(component, "instanceId");
                destroyedKind = "component";
            }
            else
            {
                throw new ArgumentException("Parameter 'instanceId' must reference a scene GameObject or Component.");
            }

            var targetSummary = CreateObjectSummary(resolvedObject);
            Undo.DestroyObjectImmediate(resolvedObject);

            var result = new
            {
                destroyed = true,
                destroyedKind,
                destroyedInstanceId = instanceId,
                target = targetSummary
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildSetTransformResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.setTransform");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var targetTransform = ResolveTransformTarget(resolvedObject, "instanceId");

            var position = ParseOptionalVector3Parameter(paramsObject, "position");
            var localPosition = ParseOptionalVector3Parameter(paramsObject, "localPosition");
            var rotationEuler = ParseOptionalVector3Parameter(paramsObject, "rotationEuler");
            var localRotationEuler = ParseOptionalVector3Parameter(paramsObject, "localRotationEuler");
            var localScale = ParseOptionalVector3Parameter(paramsObject, "localScale");

            if (!position.HasValue &&
                !localPosition.HasValue &&
                !rotationEuler.HasValue &&
                !localRotationEuler.HasValue &&
                !localScale.HasValue)
            {
                throw new ArgumentException(
                    "At least one transform property must be provided: position, localPosition, rotationEuler, localRotationEuler, or localScale.");
            }

            if (position.HasValue && localPosition.HasValue)
            {
                throw new ArgumentException("Parameters 'position' and 'localPosition' cannot both be set in the same request.");
            }

            if (rotationEuler.HasValue && localRotationEuler.HasValue)
            {
                throw new ArgumentException("Parameters 'rotationEuler' and 'localRotationEuler' cannot both be set in the same request.");
            }

            Undo.RecordObject(targetTransform, "UnityMCP Set Transform");

            if (position.HasValue)
            {
                targetTransform.position = position.Value;
            }

            if (localPosition.HasValue)
            {
                targetTransform.localPosition = localPosition.Value;
            }

            if (rotationEuler.HasValue)
            {
                targetTransform.rotation = Quaternion.Euler(rotationEuler.Value);
            }

            if (localRotationEuler.HasValue)
            {
                targetTransform.localRotation = Quaternion.Euler(localRotationEuler.Value);
            }

            if (localScale.HasValue)
            {
                targetTransform.localScale = localScale.Value;
            }

            EditorUtility.SetDirty(targetTransform);

            var result = new
            {
                instanceId,
                target = CreateObjectSummary(resolvedObject),
                transform = CreateTransformSnapshot(targetTransform),
                applied = new
                {
                    position = position.HasValue,
                    localPosition = localPosition.HasValue,
                    rotationEuler = rotationEuler.HasValue,
                    localRotationEuler = localRotationEuler.HasValue,
                    localScale = localScale.HasValue
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildSetSelectionResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.setSelection");
            var ping = ParseOptionalBooleanParameter(paramsObject, "ping");
            var focus = ParseOptionalBooleanParameter(paramsObject, "focus");
            if (!paramsObject.TryGetValue("instanceIds", out var instanceIdsToken) || instanceIdsToken is not JArray instanceIdsArray)
            {
                throw new ArgumentException("Parameter 'instanceIds' is required and must be an array of integers.");
            }

            var resolvedObjects = new List<UnityEngine.Object>(instanceIdsArray.Count);
            var seen = new HashSet<int>();

            foreach (var item in instanceIdsArray)
            {
                if (item.Type != JTokenType.Integer)
                {
                    throw new ArgumentException("Parameter 'instanceIds' must contain only integers.");
                }

                var instanceId = item.Value<int?>();
                if (!instanceId.HasValue)
                {
                    throw new ArgumentException("Parameter 'instanceIds' must contain only integers.");
                }

                if (!seen.Add(instanceId.Value))
                {
                    continue;
                }

                resolvedObjects.Add(ResolveObjectByInstanceId(instanceId.Value, "instanceIds"));
            }

            Selection.objects = resolvedObjects.ToArray();
            ApplySelectionEditorPresentation(Selection.activeObject, ping, focus);

            return UnityMcpProtocol.CreateResult(idToken, BuildSelectionSummaryResult());
        }

        private static string BuildPingObjectResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.pingObject");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var targetObject = ResolveObjectByInstanceId(instanceId, "instanceId");

            EditorGUIUtility.PingObject(targetObject);

            var result = new
            {
                pinged = true,
                instanceId,
                target = CreateObjectSummary(targetObject)
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildFrameSelectionResponse(JToken idToken)
        {
            var selectionCount = Selection.objects.Length;
            var activeObject = Selection.activeObject;
            var hasSceneSelection = Selection.activeTransform != null || Selection.activeGameObject != null;
            var sceneViewAvailable = SceneView.lastActiveSceneView != null;
            var framed = hasSceneSelection && sceneViewAvailable && TryFrameSelectionInSceneView();

            var result = new
            {
                framed,
                selectionCount,
                hasSceneSelection,
                sceneViewAvailable,
                activeObject = activeObject != null ? CreateObjectSummary(activeObject) : null
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildFrameObjectResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.frameObject");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var targetObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var sceneTarget = TryGetSceneFrameTarget(targetObject);
            var sceneViewAvailable = SceneView.lastActiveSceneView != null;
            var hasSceneTarget = sceneTarget != null;

            var framed = false;
            var selectionPreserved = true;

            if (hasSceneTarget && sceneViewAvailable && sceneTarget != null)
            {
                var previousSelection = Selection.objects;
                var previousActiveObject = Selection.activeObject;

                try
                {
                    Selection.activeObject = sceneTarget;
                    Selection.objects = new UnityEngine.Object[] { sceneTarget };
                    framed = TryFrameSelectionInSceneView();
                }
                finally
                {
                    selectionPreserved = TryRestoreSelection(previousSelection, previousActiveObject);
                }
            }

            var result = new
            {
                framed,
                hasSceneTarget,
                instanceId,
                target = CreateObjectSummary(targetObject)
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildGetSelectionDetailsResponse(JToken idToken)
        {
            var selectedObjects = Selection.gameObjects;
            var details = new List<object>(selectedObjects.Length);

            foreach (var go in selectedObjects)
            {
                if (go == null) continue;

                var components = go.GetComponents<Component>();
                var componentList = new List<object>(components.Length);
                foreach (var comp in components)
                {
                    if (comp == null) continue;
                    componentList.Add(new
                    {
                        type = comp.GetType().FullName,
                        instanceId = comp.GetInstanceID()
                    });
                }

                var t = go.transform;
                details.Add(new
                {
                    instanceId = go.GetInstanceID(),
                    name = go.name,
                    hierarchyPath = GetHierarchyPath(t),
                    activeSelf = go.activeSelf,
                    activeInHierarchy = go.activeInHierarchy,
                    tag = go.tag,
                    layer = go.layer,
                    layerName = LayerMask.LayerToName(go.layer),
                    transform = new
                    {
                        localPosition = CreateVector3Array(t.localPosition),
                        localRotation = CreateVector3Array(t.localEulerAngles),
                        localScale = CreateVector3Array(t.localScale),
                        worldPosition = CreateVector3Array(t.position),
                        worldRotation = CreateVector3Array(t.eulerAngles)
                    },
                    components = componentList
                });
            }

            return UnityMcpProtocol.CreateResult(idToken, new { count = details.Count, objects = details });
        }

        private static string BuildSelectByNameResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.selectByName");
            var name = ParseRequiredStringParameter(paramsObject, "name");
            var exactMatch = ParseOptionalBooleanParameter(paramsObject, "exactMatch", true);

            var matches = new List<GameObject>();

            if (exactMatch)
            {
                // Use FindObjectsByType for all matches
                var allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                foreach (var obj in allObjects)
                {
                    if (obj.name == name)
                        matches.Add(obj);
                }
            }
            else
            {
                var allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                foreach (var obj in allObjects)
                {
                    if (obj.name.Contains(name, System.StringComparison.OrdinalIgnoreCase))
                        matches.Add(obj);
                }
            }

            if (matches.Count == 0)
                throw new ArgumentException($"No GameObject found with name '{name}'.");

            Selection.objects = matches.ToArray();
            Selection.activeGameObject = matches[0];

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                count = matches.Count,
                selection = BuildSelectionSummaryResult()
            });
        }
    }
}

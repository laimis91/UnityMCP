#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static UnityMcp.Editor.UnityMcpParameterHelpers;
using static UnityMcp.Editor.UnityMcpResolvers;
using static UnityMcp.Editor.UnityMcpSnapshotHelpers;

namespace UnityMcp.Editor
{
    internal static class SceneHandler
    {
        #region Scene Info Methods

        internal static string BuildGetActiveSceneResponse(JToken idToken)
        {
            var activeScene = SceneManager.GetActiveScene();
            var result = CreateSceneSummary(activeScene, isActive: true);
            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildListOpenScenesResponse(JToken idToken)
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

        #endregion

        #region Selection Methods

        internal static string BuildGetSelectionResponse(JToken idToken)
        {
            return UnityMcpProtocol.CreateResult(idToken, BuildSelectionSummaryResult());
        }

        internal static string BuildSelectObjectResponse(JToken idToken, JObject root)
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

        internal static string BuildSelectByPathResponse(JToken idToken, JObject root)
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

        internal static string BuildSelectByNameResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.selectByName");
            var name = ParseRequiredStringParameter(paramsObject, "name");
            var scenePath = ParseOptionalStringParameter(paramsObject, "scenePath");
            var ping = ParseOptionalBooleanParameter(paramsObject, "ping");
            var focus = ParseOptionalBooleanParameter(paramsObject, "focus");
            var targetObject = ResolveGameObjectByName(name, scenePath, "name");

            Selection.activeGameObject = targetObject;
            Selection.objects = new UnityEngine.Object[] { targetObject };
            ApplySelectionEditorPresentation(targetObject, ping, focus);

            return UnityMcpProtocol.CreateResult(idToken, BuildSelectionSummaryResult());
        }

        internal static string BuildSetSelectionResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.setSelection");
            var instanceIds = ParseOptionalIntegerArrayParameter(paramsObject, "instanceIds");
            var ping = ParseOptionalBooleanParameter(paramsObject, "ping");
            var focus = ParseOptionalBooleanParameter(paramsObject, "focus");

            if (instanceIds == null || instanceIds.Count == 0)
            {
                Selection.activeObject = null;
                Selection.objects = new UnityEngine.Object[0];
                return UnityMcpProtocol.CreateResult(idToken, BuildSelectionSummaryResult());
            }

            var objects = new List<UnityEngine.Object>(instanceIds.Count);
            foreach (var instanceId in instanceIds)
            {
                var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceIds[]");
                objects.Add(resolvedObject);
            }

            Selection.objects = objects.ToArray();
            Selection.activeObject = objects.FirstOrDefault();

            if (objects.Count > 0)
            {
                ApplySelectionEditorPresentation(objects.First(), ping, focus);
            }

            return UnityMcpProtocol.CreateResult(idToken, BuildSelectionSummaryResult());
        }

        internal static string BuildGetSelectionDetailsResponse(JToken idToken)
        {
            var selectedObjects = Selection.objects;
            var items = new List<object>(selectedObjects.Length);

            foreach (var selectedObject in selectedObjects)
            {
                if (selectedObject == null) continue;

                var summary = CreateObjectSummary(selectedObject);

                if (selectedObject is GameObject gameObject)
                {
                    var components = gameObject.GetComponents<Component>();
                    var componentSummaries = new List<object>(components.Length);

                    foreach (var component in components)
                    {
                        if (component != null)
                        {
                            componentSummaries.Add(CreateComponentSummary(component));
                        }
                    }

                    items.Add(new
                    {
                        summary,
                        components = componentSummaries
                    });
                }
                else
                {
                    items.Add(new { summary });
                }
            }

            var result = new
            {
                count = items.Count,
                items
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        #endregion

        #region Find Methods

        internal static string BuildFindByPathResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.findByPath");
            var path = ParseRequiredStringParameter(paramsObject, "path");
            var scenePath = ParseOptionalStringParameter(paramsObject, "scenePath");
            var (normalizedPath, normalizedScenePath, allMatches, _) = FindGameObjectsByHierarchyPath(path, scenePath);

            var items = new List<object>(allMatches.Count);
            foreach (var match in allMatches)
            {
                items.Add(UnityMcpClient.CreateObjectSummary(match));
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

        internal static string BuildFindByTagResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.findByTag");
            var tag = ParseRequiredStringParameter(paramsObject, "tag");
            var scenePath = ParseOptionalStringParameter(paramsObject, "scenePath");

            var gameObjects = GameObject.FindGameObjectsWithTag(tag);
            var items = new List<object>();

            foreach (var gameObject in gameObjects)
            {
                if (scenePath != null)
                {
                    var scene = gameObject.scene;
                    if (!string.Equals(scene.path, scenePath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                items.Add(UnityMcpClient.CreateObjectSummary(gameObject));
            }

            var result = new
            {
                tag,
                scenePath,
                count = items.Count,
                items
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        #endregion

        #region Component Methods

        internal static string BuildGetComponentsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.getComponents");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var targetGameObject = ResolveGameObjectTarget(resolvedObject, "instanceId");

            var components = targetGameObject.GetComponents<Component>();
            var items = new List<object>(components.Length);
            var missingComponentCount = 0;

            foreach (var component in components)
            {
                if (component == null)
                {
                    missingComponentCount++;
                    continue;
                }

                items.Add(CreateComponentSummary(component));
            }

            var result = new
            {
                target = UnityMcpClient.CreateObjectSummary(targetGameObject),
                componentCount = items.Count,
                missingComponentCount,
                items
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildGetComponentPropertiesResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.getComponentProperties");
            var componentInstanceId = ParseRequiredIntegerParameter(paramsObject, "componentInstanceId");
            var resolvedObject = ResolveObjectByInstanceId(componentInstanceId, "componentInstanceId");
            var component = ResolveComponentTarget(resolvedObject, "componentInstanceId");

            using var serializedObject = new SerializedObject(component);
            serializedObject.UpdateIfRequiredOrScript();

            var properties = new JObject();
            var unsupported = new JArray();
            var iterator = serializedObject.GetIterator();
            var enterChildren = true;
            var visibleCount = 0;
            var supportedCount = 0;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                visibleCount++;

                if (TryReadSerializedPropertyValue(iterator, out var serializedValue, out var unsupportedReason))
                {
                    properties[iterator.propertyPath] = serializedValue;
                    supportedCount++;
                    continue;
                }

                unsupported.Add(new JObject
                {
                    ["path"] = iterator.propertyPath,
                    ["propertyType"] = iterator.propertyType.ToString(),
                    ["reason"] = unsupportedReason ?? "Unsupported property type."
                });
            }

            var result = new
            {
                component = CreateComponentSummary(component),
                target = UnityMcpClient.CreateObjectSummary(component.gameObject),
                visiblePropertyCount = visibleCount,
                propertyCount = supportedCount,
                unsupportedPropertyCount = unsupported.Count,
                properties,
                unsupportedProperties = unsupported
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildSetComponentPropertiesResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.setComponentProperties");
            var componentInstanceId = ParseRequiredIntegerParameter(paramsObject, "componentInstanceId");
            if (!paramsObject.TryGetValue("properties", out var propertiesToken) || propertiesToken is not JObject propertiesObject)
            {
                throw new ArgumentException("Parameter 'properties' is required and must be an object.");
            }

            if (!propertiesObject.HasValues)
            {
                throw new ArgumentException("Parameter 'properties' must contain at least one property assignment.");
            }

            var resolvedObject = ResolveObjectByInstanceId(componentInstanceId, "componentInstanceId");
            var component = ResolveComponentTarget(resolvedObject, "componentInstanceId");

            using var serializedObject = new SerializedObject(component);
            serializedObject.UpdateIfRequiredOrScript();

            Undo.RecordObject(component, "UnityMCP Set Component Properties");

            var updatedPaths = new List<string>();
            foreach (var propertyEntry in propertiesObject.Properties())
            {
                var propertyPath = propertyEntry.Name;
                var valueToken = propertyEntry.Value;

                var serializedProperty = serializedObject.FindProperty(propertyPath);
                if (serializedProperty == null)
                {
                    throw new ArgumentException($"Property path '{propertyPath}' was not found on the component.");
                }

                ValidateWritableSerializedProperty(serializedProperty);
                WriteSerializedPropertyValue(serializedProperty, valueToken);
                updatedPaths.Add(propertyPath);
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);

            var result = new
            {
                component = CreateComponentSummary(component),
                target = UnityMcpClient.CreateObjectSummary(component.gameObject),
                updatedPaths = updatedPaths.ToArray(),
                updatedPropertyCount = updatedPaths.Count
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildAddComponentResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.addComponent");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var typeName = ParseRequiredStringParameter(paramsObject, "typeName");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var targetGameObject = ResolveGameObjectTarget(resolvedObject, "instanceId");
            var componentType = ResolveComponentType(typeName);

            Component? addedComponent;
            try
            {
                addedComponent = Undo.AddComponent(targetGameObject, componentType);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(ex.Message);
            }

            if (addedComponent == null)
            {
                throw new InvalidOperationException("Failed to add component.");
            }

            var result = new
            {
                target = UnityMcpClient.CreateObjectSummary(targetGameObject),
                addedComponent = CreateComponentSummary(addedComponent),
                typeName = componentType.FullName
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        #endregion

        #region Object Manipulation Methods

        internal static string BuildDestroyObjectResponse(JToken idToken, JObject root)
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

            var targetSummary = UnityMcpClient.CreateObjectSummary(resolvedObject);
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

        internal static string BuildSetTransformResponse(JToken idToken, JObject root)
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
                target = UnityMcpClient.CreateObjectSummary(resolvedObject),
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

        internal static string BuildCreateGameObjectResponse(JToken idToken, JObject root)
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
                    position = ParseVector3Parameter(positionToken, "position");
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

        internal static string BuildSetParentResponse(JToken idToken, JObject root)
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
                target = UnityMcpClient.CreateObjectSummary(targetGameObject),
                parent = parentGameObject != null ? UnityMcpClient.CreateObjectSummary(parentGameObject) : null,
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

        internal static string BuildDuplicateObjectResponse(JToken idToken, JObject root)
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
                source = UnityMcpClient.CreateObjectSummary(sourceGameObject),
                duplicate = UnityMcpClient.CreateObjectSummary(duplicate),
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

        internal static string BuildRenameObjectResponse(JToken idToken, JObject root)
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
                target = UnityMcpClient.CreateObjectSummary(targetGameObject),
                previousName,
                currentName = targetGameObject.name,
                applied = new
                {
                    name = targetGameObject.name
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildSetActiveResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.setActive");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var active = ParseRequiredBooleanParameter(paramsObject, "active");

            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var targetGameObject = ResolveGameObjectTarget(resolvedObject, "instanceId");

            Undo.RecordObject(targetGameObject, "UnityMCP Set Active");
            targetGameObject.SetActive(active);
            EditorUtility.SetDirty(targetGameObject);

            var result = new
            {
                target = UnityMcpClient.CreateObjectSummary(targetGameObject),
                previousActive = !active,
                currentActive = targetGameObject.activeSelf,
                activeInHierarchy = targetGameObject.activeInHierarchy
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildSetTagResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.setTag");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var tag = ParseRequiredStringParameter(paramsObject, "tag");

            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var targetGameObject = ResolveGameObjectTarget(resolvedObject, "instanceId");
            var previousTag = targetGameObject.tag;

            Undo.RecordObject(targetGameObject, "UnityMCP Set Tag");
            targetGameObject.tag = tag;
            EditorUtility.SetDirty(targetGameObject);

            var result = new
            {
                target = UnityMcpClient.CreateObjectSummary(targetGameObject),
                previousTag,
                currentTag = targetGameObject.tag
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildSetLayerResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.setLayer");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var layer = ParseRequiredIntegerParameter(paramsObject, "layer");

            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var targetGameObject = ResolveGameObjectTarget(resolvedObject, "instanceId");
            var previousLayer = targetGameObject.layer;

            Undo.RecordObject(targetGameObject, "UnityMCP Set Layer");
            targetGameObject.layer = layer;
            EditorUtility.SetDirty(targetGameObject);

            var result = new
            {
                target = UnityMcpClient.CreateObjectSummary(targetGameObject),
                previousLayer,
                currentLayer = targetGameObject.layer
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        #endregion

        #region Presentation Methods

        internal static string BuildPingObjectResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.pingObject");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");

            EditorGUIUtility.PingObject(resolvedObject);

            var result = new
            {
                target = UnityMcpClient.CreateObjectSummary(resolvedObject),
                pinged = true
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildFrameSelectionResponse(JToken idToken)
        {
            var framed = TryFrameSelectionInSceneView();

            var result = new
            {
                framed,
                selection = BuildSelectionSummaryResult()
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildFrameObjectResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.frameObject");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");

            var previousSelection = Selection.activeObject;
            Selection.activeObject = resolvedObject;

            var framed = TryFrameSelectionInSceneView();

            if (previousSelection != null)
            {
                Selection.activeObject = previousSelection;
            }

            var result = new
            {
                target = UnityMcpClient.CreateObjectSummary(resolvedObject),
                framed
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        #endregion

        #region Scene Management Methods

        internal static string BuildSaveSceneResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.saveScene");
            var scenePath = ParseOptionalStringParameter(paramsObject, "scenePath");

            if (scenePath != null)
            {
                var targetScene = SceneManager.GetSceneByPath(scenePath);
                if (!targetScene.IsValid())
                {
                    throw new ArgumentException($"Scene path '{scenePath}' is not currently open.");
                }

                EditorSceneManager.SaveScene(targetScene);

                var result = new
                {
                    saved = true,
                    scene = CreateSceneSummary(targetScene, isActive: targetScene == SceneManager.GetActiveScene())
                };

                return UnityMcpProtocol.CreateResult(idToken, result);
            }
            else
            {
                EditorSceneManager.SaveOpenScenes();

                var result = new
                {
                    saved = true,
                    savedAllOpenScenes = true
                };

                return UnityMcpProtocol.CreateResult(idToken, result);
            }
        }

        internal static string BuildOpenSceneResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.openScene");
            var path = ParseRequiredStringParameter(paramsObject, "path");
            var additive = ParseOptionalBooleanParameter(paramsObject, "additive", false);

            var openSceneMode = additive ? OpenSceneMode.Additive : OpenSceneMode.Single;
            var openedScene = EditorSceneManager.OpenScene(path, openSceneMode);

            if (!openedScene.IsValid())
            {
                throw new InvalidOperationException($"Failed to open scene at path '{path}'.");
            }

            var result = new
            {
                opened = true,
                additive,
                scene = CreateSceneSummary(openedScene, isActive: openedScene == SceneManager.GetActiveScene())
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildNewSceneResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.newScene");
            var setup = ParseOptionalStringParameter(paramsObject, "setup") ?? "defaultGameObjects";
            var mode = ParseOptionalStringParameter(paramsObject, "mode") ?? "single";

            var newSceneSetup = setup.ToLowerInvariant() switch
            {
                "empty" => NewSceneSetup.EmptyScene,
                "defaultgameobjects" => NewSceneSetup.DefaultGameObjects,
                _ => throw new ArgumentException("Parameter 'setup' must be 'empty' or 'defaultGameObjects'.")
            };

            var newSceneMode = mode.ToLowerInvariant() switch
            {
                "single" => NewSceneMode.Single,
                "additive" => NewSceneMode.Additive,
                _ => throw new ArgumentException("Parameter 'mode' must be 'single' or 'additive'.")
            };

            var newScene = EditorSceneManager.NewScene(newSceneSetup, newSceneMode);

            var result = new
            {
                created = true,
                setup = newSceneSetup.ToString(),
                mode = newSceneMode.ToString(),
                scene = CreateSceneSummary(newScene, isActive: newScene == SceneManager.GetActiveScene())
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildCloseSceneResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.closeScene");
            var scenePath = ParseRequiredStringParameter(paramsObject, "scenePath");
            var removeScene = ParseOptionalBooleanParameter(paramsObject, "removeScene", true);

            var targetScene = SceneManager.GetSceneByPath(scenePath);
            if (!targetScene.IsValid())
            {
                throw new ArgumentException($"Scene path '{scenePath}' is not currently open.");
            }

            var closed = EditorSceneManager.CloseScene(targetScene, removeScene);

            var result = new
            {
                closed,
                scenePath,
                removeScene
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildSetActiveSceneResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.setActiveScene");
            var scenePath = ParseRequiredStringParameter(paramsObject, "scenePath");

            var targetScene = SceneManager.GetSceneByPath(scenePath);
            if (!targetScene.IsValid())
            {
                throw new ArgumentException($"Scene path '{scenePath}' is not currently open.");
            }

            var success = SceneManager.SetActiveScene(targetScene);
            if (!success)
            {
                throw new InvalidOperationException($"Failed to set scene '{scenePath}' as active.");
            }

            var result = new
            {
                setActive = true,
                scene = CreateSceneSummary(targetScene, isActive: true)
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildSceneInstantiatePrefabResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.instantiatePrefab");
            var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
            var position = ParseOptionalVector3Parameter(paramsObject, "position");
            var rotation = ParseOptionalVector3Parameter(paramsObject, "rotation");
            var scale = ParseOptionalVector3Parameter(paramsObject, "scale");
            var select = ParseOptionalBooleanParameter(paramsObject, "select", true);
            var ping = ParseOptionalBooleanParameter(paramsObject, "ping");
            var focus = ParseOptionalBooleanParameter(paramsObject, "focus");

            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefabAsset == null || !PrefabUtility.IsPartOfPrefabAsset(prefabAsset))
            {
                throw new ArgumentException($"Asset path '{assetPath}' does not point to a prefab asset.");
            }

            var instance = PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException("Failed to instantiate prefab.");
            }

            Undo.RegisterCreatedObjectUndo(instance, "UnityMCP Instantiate Prefab");

            if (position.HasValue)
            {
                instance.transform.position = position.Value;
            }

            if (rotation.HasValue)
            {
                instance.transform.rotation = Quaternion.Euler(rotation.Value);
            }

            if (scale.HasValue)
            {
                instance.transform.localScale = scale.Value;
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
                instance = UnityMcpClient.CreateObjectSummary(instance),
                prefabAssetPath = assetPath,
                applied = new
                {
                    position = position.HasValue,
                    rotation = rotation.HasValue,
                    scale = scale.HasValue,
                    selected = select,
                    ping,
                    focus
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildGetSceneHierarchyResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "scene.getSceneHierarchy");
            var scenePath = ParseOptionalStringParameter(paramsObject, "scenePath");
            var includeInactive = ParseOptionalBooleanParameter(paramsObject, "includeInactive", true);

            Scene targetScene;
            if (scenePath != null)
            {
                targetScene = SceneManager.GetSceneByPath(scenePath);
                if (!targetScene.IsValid())
                {
                    throw new ArgumentException($"Scene path '{scenePath}' is not currently open.");
                }
            }
            else
            {
                targetScene = SceneManager.GetActiveScene();
            }

            var rootGameObjects = targetScene.GetRootGameObjects();
            var items = new List<object>();

            foreach (var rootGameObject in rootGameObjects)
            {
                if (!includeInactive && !rootGameObject.activeInHierarchy)
                {
                    continue;
                }

                items.Add(CreateHierarchyNode(rootGameObject, includeInactive));
            }

            var result = new
            {
                scene = CreateSceneSummary(targetScene, isActive: targetScene == SceneManager.GetActiveScene()),
                includeInactive,
                rootCount = items.Count,
                items
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        #endregion

        #region Helper Methods

        private static object CreateSceneSummary(Scene scene, bool isActive)
        {
            if (!scene.IsValid())
            {
                return new
                {
                    isValid = false,
                    isLoaded = false,
                    isActive,
                    handle = scene.handle,
                    buildIndex = scene.buildIndex,
                    name = scene.name,
                    path = scene.path,
                    rootCount = 0
                };
            }

            return new
            {
                isValid = true,
                isLoaded = scene.isLoaded,
                isActive,
                handle = scene.handle,
                buildIndex = scene.buildIndex,
                name = scene.name,
                path = scene.path,
                rootCount = scene.rootCount
            };
        }

        private static object BuildSelectionSummaryResult()
        {
            var selectedObjects = Selection.objects;
            var items = new List<object>(selectedObjects.Length);
            foreach (var selectedObject in selectedObjects)
            {
                if (selectedObject == null)
                {
                    continue;
                }

                items.Add(UnityMcpClient.CreateObjectSummary(selectedObject));
            }

            var activeObject = Selection.activeObject;
            object? activeObjectSummary = null;
            if (activeObject != null)
            {
                activeObjectSummary = UnityMcpClient.CreateObjectSummary(activeObject);
            }

            var activeGameObject = Selection.activeGameObject;
            object? activeGameObjectSummary = null;
            if (activeGameObject != null)
            {
                activeGameObjectSummary = UnityMcpClient.CreateObjectSummary(activeGameObject);
            }

            return new
            {
                count = items.Count,
                activeObject = activeObjectSummary,
                activeGameObject = activeGameObjectSummary,
                items
            };
        }

        private static void ApplySelectionEditorPresentation(UnityEngine.Object? pingTarget, bool ping, bool focus)
        {
            if (ping && pingTarget != null)
            {
                EditorGUIUtility.PingObject(pingTarget);
            }

            if (!focus)
            {
                return;
            }

            // Scene framing only applies to scene-object selections; assets should no-op.
            if (Selection.activeTransform == null && Selection.activeGameObject == null)
            {
                return;
            }

            _ = TryFrameSelectionInSceneView();
        }

        private static void ApplySceneObjectPresentationWithoutSelection(GameObject targetGameObject, bool ping, bool focus)
        {
            if (ping)
            {
                EditorGUIUtility.PingObject(targetGameObject);
            }

            if (!focus)
            {
                return;
            }

            var previousSelection = Selection.activeObject;
            Selection.activeObject = targetGameObject;
            _ = TryFrameSelectionInSceneView();

            if (previousSelection != null)
            {
                Selection.activeObject = previousSelection;
            }
        }

        private static bool TryFrameSelectionInSceneView()
        {
            try
            {
                var sceneView = SceneView.lastActiveSceneView;
                if (sceneView == null)
                {
                    return false;
                }

                // Unity versions differ on the exact return type; handle bool/void via reflection.
                var method = typeof(SceneView).GetMethod("FrameSelected", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                if (method == null)
                {
                    return false;
                }

                var result = method.Invoke(sceneView, null);
                return result switch
                {
                    bool boolResult => boolResult,
                    _ => true
                };
            }
            catch
            {
                // Best-effort editor UX enhancement; selection change itself already succeeded.
                return false;
            }
        }

        private static object CreateTransformSnapshot(Transform transform)
        {
            return new
            {
                worldPosition = ToVectorArray(transform.position),
                localPosition = ToVectorArray(transform.localPosition),
                worldRotationEuler = ToVectorArray(transform.rotation.eulerAngles),
                localRotationEuler = ToVectorArray(transform.localRotation.eulerAngles),
                localScale = ToVectorArray(transform.localScale)
            };
        }

        private static float[] ToVectorArray(Vector3 value)
        {
            return new[] { value.x, value.y, value.z };
        }

        private static object CreateComponentSummary(Component component)
        {
            var componentType = component.GetType();
            var behaviour = component as Behaviour;
            var gameObject = component.gameObject;

            return new
            {
                instanceId = component.GetInstanceID(),
                name = component.name,
                typeName = componentType.Name,
                fullTypeName = componentType.FullName,
                isBehaviour = behaviour != null,
                enabled = behaviour != null ? behaviour.enabled : (bool?)null,
                gameObjectInstanceId = gameObject.GetInstanceID(),
                gameObjectName = gameObject.name
            };
        }

        private static object CreateHierarchyNode(GameObject gameObject, bool includeInactive)
        {
            var children = new List<object>();
            var transform = gameObject.transform;

            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i).gameObject;
                if (!includeInactive && !child.activeInHierarchy)
                {
                    continue;
                }

                children.Add(CreateHierarchyNode(child, includeInactive));
            }

            return new
            {
                instanceId = gameObject.GetInstanceID(),
                name = gameObject.name,
                activeSelf = gameObject.activeSelf,
                activeInHierarchy = gameObject.activeInHierarchy,
                tag = gameObject.tag,
                layer = gameObject.layer,
                hierarchyPath = GetHierarchyPath(transform),
                childCount = children.Count,
                children
            };
        }

        // Helper method to parse optional integer array parameter
        private static List<int>? ParseOptionalIntegerArrayParameter(JObject paramsObject, string parameterName)
        {
            if (!paramsObject.TryGetValue(parameterName, out var token))
            {
                return null;
            }

            if (token.Type != JTokenType.Array || token is not JArray array)
            {
                throw new ArgumentException($"Parameter '{parameterName}' must be an array of integers.");
            }

            var values = new List<int>(array.Count);
            foreach (var item in array)
            {
                if (item.Type != JTokenType.Integer)
                {
                    throw new ArgumentException($"Parameter '{parameterName}' must contain only integers.");
                }

                var value = item.Value<int?>();
                if (!value.HasValue)
                {
                    throw new ArgumentException($"Parameter '{parameterName}' must contain valid integers.");
                }

                values.Add(value.Value);
            }

            return values;
        }

        #endregion
    }
}
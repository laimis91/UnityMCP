#nullable enable

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMcp.Editor
{
    internal sealed partial class UnityMcpClient
    {
        private static string BuildGetComponentsResponse(JToken idToken, JObject root)
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
                target = CreateObjectSummary(targetGameObject),
                componentCount = items.Count,
                missingComponentCount,
                items
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildGetComponentPropertiesResponse(JToken idToken, JObject root)
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
                target = CreateObjectSummary(component.gameObject),
                visiblePropertyCount = visibleCount,
                propertyCount = supportedCount,
                unsupportedPropertyCount = unsupported.Count,
                properties,
                unsupportedProperties = unsupported
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildSetComponentPropertiesResponse(JToken idToken, JObject root)
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
                if (string.IsNullOrWhiteSpace(propertyPath))
                {
                    throw new ArgumentException("Property paths in 'properties' must not be empty.");
                }

                var property = serializedObject.FindProperty(propertyPath);
                if (property == null)
                {
                    throw new ArgumentException($"Serialized property '{propertyPath}' was not found on component '{component.GetType().Name}'.");
                }

                ValidateWritableSerializedProperty(property);
                WriteSerializedPropertyValue(property, propertyEntry.Value);
                updatedPaths.Add(property.propertyPath);
            }

            var appliedModifiedProperties = serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);

            var result = new
            {
                component = CreateComponentSummary(component),
                target = CreateObjectSummary(component.gameObject),
                appliedModifiedProperties,
                appliedCount = updatedPaths.Count,
                updated = updatedPaths
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildAddComponentResponse(JToken idToken, JObject root)
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
                throw new InvalidOperationException(
                    $"Unity did not return a component instance after adding '{componentType.FullName}' to '{targetGameObject.name}'.");
            }

            Selection.activeGameObject = targetGameObject;

            var result = new
            {
                target = CreateObjectSummary(targetGameObject),
                addedComponent = CreateComponentSummary(addedComponent),
                componentCount = targetGameObject.GetComponents<Component>().Length
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }
    }
}

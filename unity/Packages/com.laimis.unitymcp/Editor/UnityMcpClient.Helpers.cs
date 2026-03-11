#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace UnityMcp.Editor
{

internal sealed partial class UnityMcpClient
{
    private static JObject RequireParamsObject(JObject root, string methodName)
    {
        if (!root.TryGetValue("params", out var paramsToken) || paramsToken is not JObject paramsObject)
        {
            throw new ArgumentException($"Method '{methodName}' expects params to be an object.");
        }

        return paramsObject;
    }

    private static int ParseRequiredIntegerParameter(JObject paramsObject, string parameterName)
    {
        if (!paramsObject.TryGetValue(parameterName, out var token) || token.Type != JTokenType.Integer)
        {
            throw new ArgumentException($"Parameter '{parameterName}' is required and must be an integer.");
        }

        var value = token.Value<int?>();
        if (!value.HasValue)
        {
            throw new ArgumentException($"Parameter '{parameterName}' is required and must be an integer.");
        }

        return value.Value;
    }

    private static string ParseRequiredStringParameter(JObject paramsObject, string parameterName)
    {
        if (!paramsObject.TryGetValue(parameterName, out var token) || token.Type != JTokenType.String)
        {
            throw new ArgumentException($"Parameter '{parameterName}' is required and must be a string.");
        }

        var value = token.Value<string>();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Parameter '{parameterName}' cannot be empty.");
        }

        return value!.Trim();
    }

    private static string? ParseOptionalStringParameter(JObject paramsObject, string parameterName)
    {
        if (!paramsObject.TryGetValue(parameterName, out var token))
        {
            return null;
        }

        if (token.Type != JTokenType.String)
        {
            throw new ArgumentException($"Parameter '{parameterName}' must be a string.");
        }

        var value = token.Value<string>();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Parameter '{parameterName}' cannot be empty.");
        }

        return value!.Trim();
    }

    private static Vector3? ParseOptionalVector3Parameter(JObject paramsObject, string parameterName)
    {
        if (!paramsObject.TryGetValue(parameterName, out var token))
        {
            return null;
        }

        return ParseVector3Parameter(token, parameterName);
    }

    private static Vector2? ParseOptionalVector2Parameter(JObject paramsObject, string parameterName)
    {
        if (!paramsObject.TryGetValue(parameterName, out var token))
        {
            return null;
        }

        return ParseVector2Token(token, parameterName);
    }

    private static Color? ParseOptionalColorParameter(JObject paramsObject, string parameterName)
    {
        if (!paramsObject.TryGetValue(parameterName, out var token))
        {
            return null;
        }

        return ParseColorToken(token, parameterName);
    }

    private static float? ParseOptionalFloatParameter(JObject paramsObject, string parameterName)
    {
        if (!paramsObject.TryGetValue(parameterName, out var token))
        {
            return null;
        }

        if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
        {
            throw new ArgumentException($"Parameter '{parameterName}' must be numeric.");
        }

        var value = token.Value<float?>();
        if (!value.HasValue)
        {
            throw new ArgumentException($"Parameter '{parameterName}' must be numeric.");
        }

        return value.Value;
    }

    private static float ParseRequiredFloatParameter(JObject paramsObject, string parameterName)
    {
        if (!paramsObject.TryGetValue(parameterName, out var token))
        {
            throw new ArgumentException($"Parameter '{parameterName}' is required.");
        }

        if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
        {
            throw new ArgumentException($"Parameter '{parameterName}' must be numeric.");
        }

        var value = token.Value<float?>();
        if (!value.HasValue)
        {
            throw new ArgumentException($"Parameter '{parameterName}' must be numeric.");
        }

        return value.Value;
    }

    private static int? ParseOptionalIntegerParameter(JObject paramsObject, string parameterName)
    {
        if (!paramsObject.TryGetValue(parameterName, out var token))
        {
            return null;
        }

        if (token.Type != JTokenType.Integer)
        {
            throw new ArgumentException($"Parameter '{parameterName}' must be an integer.");
        }

        var value = token.Value<int?>();
        if (!value.HasValue)
        {
            throw new ArgumentException($"Parameter '{parameterName}' must be an integer.");
        }

        return value.Value;
    }

    private static OptionalInstanceIdParameter ParseOptionalNullableIntegerParameter(JObject paramsObject, string parameterName)
    {
        if (!paramsObject.TryGetValue(parameterName, out var token))
        {
            return default;
        }

        if (token.Type == JTokenType.Null)
        {
            return new OptionalInstanceIdParameter(true, null);
        }

        if (token.Type != JTokenType.Integer)
        {
            throw new ArgumentException($"Parameter '{parameterName}' must be an integer or null.");
        }

        var value = token.Value<int?>();
        if (!value.HasValue)
        {
            throw new ArgumentException($"Parameter '{parameterName}' must be an integer or null.");
        }

        return new OptionalInstanceIdParameter(true, value.Value);
    }

    private static bool? ParseOptionalBooleanValueParameter(JObject paramsObject, string parameterName)
    {
        if (!paramsObject.TryGetValue(parameterName, out var token))
        {
            return null;
        }

        if (token.Type != JTokenType.Boolean)
        {
            throw new ArgumentException($"Parameter '{parameterName}' must be a boolean.");
        }

        var value = token.Value<bool?>();
        if (!value.HasValue)
        {
            throw new ArgumentException($"Parameter '{parameterName}' must be a boolean.");
        }

        return value.Value;
    }

    private static TEnum? ParseOptionalEnumParameter<TEnum>(JObject paramsObject, string parameterName)
        where TEnum : struct, Enum
    {
        if (!paramsObject.TryGetValue(parameterName, out var token))
        {
            return null;
        }

        return ParseEnumToken<TEnum>(token, parameterName);
    }

    private static JObject? ParseOptionalObjectParameter(JObject paramsObject, string parameterName)
    {
        if (!paramsObject.TryGetValue(parameterName, out var token))
        {
            return null;
        }

        if (token is not JObject objectToken)
        {
            throw new ArgumentException($"Parameter '{parameterName}' must be an object.");
        }

        return objectToken;
    }

    private static ConnectedAnchorMode? ParseOptionalConnectedAnchorModeParameter(JObject paramsObject, string parameterName)
    {
        var value = ParseOptionalStringParameter(paramsObject, parameterName);
        if (value == null)
        {
            return null;
        }

        return value.ToLowerInvariant() switch
        {
            "preserve" => ConnectedAnchorMode.Preserve,
            "auto" => ConnectedAnchorMode.Auto,
            "zero" => ConnectedAnchorMode.Zero,
            "matchanchor" => ConnectedAnchorMode.MatchAnchor,
            _ => throw new ArgumentException($"Parameter '{parameterName}' has invalid value '{value}'. Expected preserve, auto, zero, or matchAnchor.")
        };
    }

    private static PrefabOverrideScope ParseOptionalPrefabOverrideScopeParameter(
        JObject paramsObject,
        string parameterName,
        PrefabOverrideScope defaultValue = PrefabOverrideScope.InstanceRoot)
    {
        var value = ParseOptionalStringParameter(paramsObject, parameterName);
        if (value == null)
        {
            return defaultValue;
        }

        return value switch
        {
            "instanceRoot" => PrefabOverrideScope.InstanceRoot,
            "object" => PrefabOverrideScope.Object,
            "component" => PrefabOverrideScope.Component,
            _ => throw new ArgumentException($"Parameter '{parameterName}' has invalid value '{value}'. Expected instanceRoot, object, or component.")
        };
    }

    private static SoftJointLimitUpdate ParseOptionalSoftJointLimitParameter(JObject paramsObject, string parameterName)
    {
        var objectToken = ParseOptionalObjectParameter(paramsObject, parameterName);
        if (objectToken == null)
        {
            return default;
        }

        var limit = ParseOptionalFloatParameter(objectToken, "limit");
        var bounciness = ParseOptionalFloatParameter(objectToken, "bounciness");
        var contactDistance = ParseOptionalFloatParameter(objectToken, "contactDistance");
        if (!limit.HasValue && !bounciness.HasValue && !contactDistance.HasValue)
        {
            throw new ArgumentException($"Parameter '{parameterName}' must include at least one supported property.");
        }

        return new SoftJointLimitUpdate(true, limit, bounciness, contactDistance);
    }

    private static SoftJointLimitSpringUpdate ParseOptionalSoftJointLimitSpringParameter(JObject paramsObject, string parameterName)
    {
        var objectToken = ParseOptionalObjectParameter(paramsObject, parameterName);
        if (objectToken == null)
        {
            return default;
        }

        var spring = ParseOptionalFloatParameter(objectToken, "spring");
        var damper = ParseOptionalFloatParameter(objectToken, "damper");
        if (!spring.HasValue && !damper.HasValue)
        {
            throw new ArgumentException($"Parameter '{parameterName}' must include at least one supported property.");
        }

        return new SoftJointLimitSpringUpdate(true, spring, damper);
    }

    private static JointDriveUpdate ParseOptionalJointDriveParameter(JObject paramsObject, string parameterName)
    {
        var objectToken = ParseOptionalObjectParameter(paramsObject, parameterName);
        if (objectToken == null)
        {
            return default;
        }

        var positionSpring = ParseOptionalFloatParameter(objectToken, "positionSpring");
        var positionDamper = ParseOptionalFloatParameter(objectToken, "positionDamper");
        var maximumForce = ParseOptionalFloatParameter(objectToken, "maximumForce");
        if (!positionSpring.HasValue && !positionDamper.HasValue && !maximumForce.HasValue)
        {
            throw new ArgumentException($"Parameter '{parameterName}' must include at least one supported property.");
        }

        return new JointDriveUpdate(true, positionSpring, positionDamper, maximumForce);
    }

    private static TEnum ParseEnumToken<TEnum>(JToken token, string parameterName)
        where TEnum : struct, Enum
    {
        if (token.Type == JTokenType.String)
        {
            var rawValue = token.Value<string>();
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                throw new ArgumentException($"Parameter '{parameterName}' cannot be empty.");
            }

            if (Enum.TryParse<TEnum>(rawValue!.Trim(), ignoreCase: true, out var parsedFromName))
            {
                return parsedFromName;
            }

            throw new ArgumentException(
                $"Parameter '{parameterName}' has invalid value '{rawValue}'. Expected a valid {typeof(TEnum).Name} enum name.");
        }

        if (token.Type == JTokenType.Integer)
        {
            var rawValue = token.Value<long?>();
            if (!rawValue.HasValue)
            {
                throw new ArgumentException($"Parameter '{parameterName}' must be a string or integer enum value.");
            }

            return (TEnum)Enum.ToObject(typeof(TEnum), rawValue.Value);
        }

        throw new ArgumentException($"Parameter '{parameterName}' must be a string or integer enum value.");
    }

    private static bool ParseOptionalBooleanParameter(JObject paramsObject, string parameterName, bool defaultValue = false)
    {
        if (!paramsObject.TryGetValue(parameterName, out var token))
        {
            return defaultValue;
        }

        if (token.Type != JTokenType.Boolean)
        {
            throw new ArgumentException($"Parameter '{parameterName}' must be a boolean.");
        }

        var value = token.Value<bool?>();
        if (!value.HasValue)
        {
            throw new ArgumentException($"Parameter '{parameterName}' must be a boolean.");
        }

        return value.Value;
    }

    private static bool ParseRequiredBooleanParameter(JObject paramsObject, string parameterName)
    {
        if (!paramsObject.TryGetValue(parameterName, out var token) || token.Type != JTokenType.Boolean)
        {
            throw new ArgumentException($"Parameter '{parameterName}' is required and must be a boolean.");
        }

        var value = token.Value<bool?>();
        if (!value.HasValue)
        {
            throw new ArgumentException($"Parameter '{parameterName}' is required and must be a boolean.");
        }

        return value.Value;
    }

    private static UnityEngine.Object ResolveObjectByInstanceId(int instanceId, string parameterName)
    {
        var resolved = TryResolveObjectByEntityId(instanceId) ?? ResolveObjectByLegacyInstanceId(instanceId);
        if (resolved == null)
        {
            throw new ArgumentException($"No Unity object found for instanceId {instanceId}.", parameterName);
        }

        return resolved;
    }

    private static GameObject ResolveGameObjectTarget(UnityEngine.Object resolvedObject, string parameterName)
    {
        return resolvedObject switch
        {
            GameObject gameObject => gameObject,
            Component component => component.gameObject,
            _ => throw new ArgumentException(
                $"Parameter '{parameterName}' must reference a GameObject or Component instance.")
        };
    }

    private static Transform ResolveTransformTarget(UnityEngine.Object resolvedObject, string parameterName)
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

    private static Component ResolveComponentTarget(UnityEngine.Object resolvedObject, string parameterName)
    {
        if (resolvedObject is not Component component)
        {
            throw new ArgumentException($"Parameter '{parameterName}' must reference a Component instance.");
        }

        return component;
    }

    private static TComponent ResolveComponentOfTypeTarget<TComponent>(
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

    private static GameObject ResolveSceneGameObjectTarget(UnityEngine.Object resolvedObject, string parameterName)
    {
        var gameObject = ResolveGameObjectTarget(resolvedObject, parameterName);
        ValidateDestroyableSceneObject(gameObject, parameterName);
        return gameObject;
    }

    private static Component ResolveSceneComponentTarget(UnityEngine.Object resolvedObject, string parameterName)
    {
        var component = ResolveComponentTarget(resolvedObject, parameterName);
        ValidateDestroyableSceneObject(component, parameterName);
        return component;
    }

    private static Component ResolveSceneComponentTargetAllowingTransform(UnityEngine.Object resolvedObject, string parameterName)
    {
        var component = ResolveComponentTarget(resolvedObject, parameterName);
        ValidateDestroyableSceneObject(component.gameObject, parameterName);
        return component;
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

    private static void ValidateWritableSerializedProperty(SerializedProperty property)
    {
        if (string.Equals(property.propertyPath, "m_Script", StringComparison.Ordinal))
        {
            throw new ArgumentException("Serialized property 'm_Script' is read-only and cannot be modified.");
        }

        if (!property.editable)
        {
            throw new ArgumentException($"Serialized property '{property.propertyPath}' is not editable.");
        }

        if (property.propertyType == SerializedPropertyType.Generic)
        {
            throw new ArgumentException(
                $"Serialized property '{property.propertyPath}' is a generic/nested property container. Set a concrete child property path instead.");
        }
    }

    private static bool TryReadSerializedPropertyValue(SerializedProperty property, out JToken serializedValue, out string? unsupportedReason)
    {
        unsupportedReason = null;

        switch (property.propertyType)
        {
            case SerializedPropertyType.Boolean:
                serializedValue = new JValue(property.boolValue);
                return true;

            case SerializedPropertyType.Integer:
                serializedValue = new JValue(property.intValue);
                return true;

            case SerializedPropertyType.Float:
                serializedValue = new JValue(property.floatValue);
                return true;

            case SerializedPropertyType.String:
                serializedValue = new JValue(property.stringValue);
                return true;

            case SerializedPropertyType.Enum:
                serializedValue = CreateEnumSerializedValue(property);
                return true;

            case SerializedPropertyType.Color:
                serializedValue = CreateColorArray(property.colorValue);
                return true;

            case SerializedPropertyType.ObjectReference:
                serializedValue = property.objectReferenceValue != null
                    ? JToken.FromObject(CreateObjectSummary(property.objectReferenceValue))
                    : JValue.CreateNull();
                return true;

            case SerializedPropertyType.LayerMask:
                serializedValue = new JValue(property.intValue);
                return true;

            case SerializedPropertyType.Vector2:
                serializedValue = CreateVector2Array(property.vector2Value);
                return true;

            case SerializedPropertyType.Vector3:
                serializedValue = CreateVector3Array(property.vector3Value);
                return true;

            case SerializedPropertyType.Vector4:
                serializedValue = CreateVector4Array(property.vector4Value);
                return true;

            case SerializedPropertyType.Rect:
                serializedValue = CreateRectArray(property.rectValue);
                return true;

            case SerializedPropertyType.Bounds:
                serializedValue = CreateBoundsObject(property.boundsValue);
                return true;

            case SerializedPropertyType.Quaternion:
                serializedValue = CreateQuaternionArray(property.quaternionValue);
                return true;

            default:
                serializedValue = JValue.CreateNull();
                unsupportedReason = $"SerializedPropertyType '{property.propertyType}' is not supported in the MVP.";
                return false;
        }
    }

    private static JToken CreateEnumSerializedValue(SerializedProperty property)
    {
        var index = property.enumValueIndex;
        var enumNames = property.enumNames;
        var enumDisplayNames = property.enumDisplayNames;

        string? enumName = index >= 0 && index < enumNames.Length ? enumNames[index] : null;
        string? enumDisplayName = index >= 0 && index < enumDisplayNames.Length ? enumDisplayNames[index] : null;

        return new JObject
        {
            ["index"] = index,
            ["name"] = enumName,
            ["displayName"] = enumDisplayName
        };
    }

    private static void WriteSerializedPropertyValue(SerializedProperty property, JToken valueToken)
    {
        var propertyPath = property.propertyPath;

        switch (property.propertyType)
        {
            case SerializedPropertyType.Boolean:
                property.boolValue = ParseBooleanToken(valueToken, propertyPath);
                return;

            case SerializedPropertyType.Integer:
            case SerializedPropertyType.LayerMask:
                property.intValue = ParseIntegerToken(valueToken, propertyPath);
                return;

            case SerializedPropertyType.Float:
                property.floatValue = ParseFloatToken(valueToken, propertyPath);
                return;

            case SerializedPropertyType.String:
                property.stringValue = ParseStringToken(valueToken, propertyPath);
                return;

            case SerializedPropertyType.Enum:
                property.enumValueIndex = ParseEnumToken(valueToken, property, propertyPath);
                return;

            case SerializedPropertyType.Color:
                property.colorValue = ParseColorToken(valueToken, propertyPath);
                return;

            case SerializedPropertyType.Vector2:
                property.vector2Value = ParseVector2Token(valueToken, propertyPath);
                return;

            case SerializedPropertyType.Vector3:
                property.vector3Value = ParseVector3Parameter(valueToken, propertyPath);
                return;

            case SerializedPropertyType.Vector4:
                property.vector4Value = ParseVector4Token(valueToken, propertyPath);
                return;

            case SerializedPropertyType.Rect:
                property.rectValue = ParseRectToken(valueToken, propertyPath);
                return;

            case SerializedPropertyType.Bounds:
                property.boundsValue = ParseBoundsToken(valueToken, propertyPath);
                return;

            case SerializedPropertyType.Quaternion:
                property.quaternionValue = ParseQuaternionToken(valueToken, propertyPath);
                return;

            default:
                throw new ArgumentException(
                    $"Serialized property '{propertyPath}' has unsupported type '{property.propertyType}' for writes in the MVP.");
        }
    }

    private static bool ParseBooleanToken(JToken token, string propertyPath)
    {
        if (token.Type != JTokenType.Boolean)
        {
            throw new ArgumentException($"Property '{propertyPath}' must be set to a boolean value.");
        }

        var value = token.Value<bool?>();
        if (!value.HasValue)
        {
            throw new ArgumentException($"Property '{propertyPath}' must be set to a boolean value.");
        }

        return value.Value;
    }

    private static int ParseIntegerToken(JToken token, string propertyPath)
    {
        if (token.Type != JTokenType.Integer)
        {
            throw new ArgumentException($"Property '{propertyPath}' must be set to an integer value.");
        }

        var value = token.Value<int?>();
        if (!value.HasValue)
        {
            throw new ArgumentException($"Property '{propertyPath}' must be set to an integer value.");
        }

        return value.Value;
    }

    private static float ParseFloatToken(JToken token, string propertyPath)
    {
        if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
        {
            throw new ArgumentException($"Property '{propertyPath}' must be set to a numeric value.");
        }

        var value = token.Value<float?>();
        if (!value.HasValue)
        {
            throw new ArgumentException($"Property '{propertyPath}' must be set to a numeric value.");
        }

        return value.Value;
    }

    private static string ParseStringToken(JToken token, string propertyPath)
    {
        if (token.Type != JTokenType.String)
        {
            throw new ArgumentException($"Property '{propertyPath}' must be set to a string value.");
        }

        var value = token.Value<string>();
        if (value == null)
        {
            throw new ArgumentException($"Property '{propertyPath}' must be set to a string value.");
        }

        return value;
    }

    private static int ParseEnumToken(JToken token, SerializedProperty property, string propertyPath)
    {
        if (token.Type == JTokenType.Integer)
        {
            var index = token.Value<int?>();
            if (!index.HasValue)
            {
                throw new ArgumentException($"Property '{propertyPath}' enum value must be an integer index or string name.");
            }

            return ValidateEnumIndex(property, propertyPath, index.Value);
        }

        if (token.Type == JTokenType.String)
        {
            var name = token.Value<string>();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException($"Property '{propertyPath}' enum name cannot be empty.");
            }

            var trimmedName = name!.Trim();
            var enumNames = property.enumNames;
            for (var index = 0; index < enumNames.Length; index++)
            {
                if (string.Equals(enumNames[index], trimmedName, StringComparison.Ordinal) ||
                    string.Equals(property.enumDisplayNames[index], trimmedName, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            throw new ArgumentException(
                $"Property '{propertyPath}' enum name '{trimmedName}' was not found. Use a valid enum name/display name or index.");
        }

        throw new ArgumentException($"Property '{propertyPath}' enum value must be an integer index or string name.");
    }

    private static int ValidateEnumIndex(SerializedProperty property, string propertyPath, int index)
    {
        if (index < 0 || index >= property.enumNames.Length)
        {
            throw new ArgumentException(
                $"Property '{propertyPath}' enum index {index} is out of range (0-{Math.Max(0, property.enumNames.Length - 1)}).");
        }

        return index;
    }

    private static GameObject ResolveGameObjectByHierarchyPath(string rawPath, string? rawScenePath, string parameterName)
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

    private static string? NormalizeOptionalScenePath(string? scenePath)
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

    private static void CollectHierarchyPathMatches(
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

    private static Type ResolveComponentType(string typeName)
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

    private static void ValidateResolvedComponentType(Type componentType, string requestedTypeName)
    {
        if (!typeof(Component).IsAssignableFrom(componentType) || !IsSupportedAddComponentType(componentType))
        {
            throw new ArgumentException($"Component type '{requestedTypeName}' is not a supported Unity Component type.");
        }
    }

    private static bool IsSupportedAddComponentType(Type componentType)
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

    private static GameObject? TryGetSceneFrameTarget(UnityEngine.Object targetObject)
    {
        GameObject? gameObject = targetObject switch
        {
            GameObject go => go,
            Component component => component.gameObject,
            _ => null
        };

        if (gameObject == null)
        {
            return null;
        }

        if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
        {
            return null;
        }

        return gameObject;
    }

    private static bool TryRestoreSelection(UnityEngine.Object[] previousSelection, UnityEngine.Object? previousActiveObject)
    {
        try
        {
            Selection.objects = previousSelection ?? Array.Empty<UnityEngine.Object>();

            if (previousActiveObject != null)
            {
                Selection.activeObject = previousActiveObject;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

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

        _ = TryFrameGameObjectWithoutChangingSelection(targetGameObject);
    }

    private static bool TryFrameGameObjectWithoutChangingSelection(GameObject targetGameObject)
    {
        if (!targetGameObject.scene.IsValid() || !targetGameObject.scene.isLoaded || SceneView.lastActiveSceneView == null)
        {
            return false;
        }

        var previousSelection = Selection.objects;
        var previousActiveObject = Selection.activeObject;

        try
        {
            Selection.activeObject = targetGameObject;
            Selection.objects = new UnityEngine.Object[] { targetGameObject };
            return TryFrameSelectionInSceneView();
        }
        finally
        {
            _ = TryRestoreSelection(previousSelection, previousActiveObject);
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

    private static Vector3 ParsePosition(JToken positionToken)
    {
        return ParseVector3Parameter(positionToken, "position");
    }

    private static Vector3 ParseVector3Parameter(JToken token, string parameterName)
    {
        if (token is not JArray array)
        {
            throw new ArgumentException($"Parameter '{parameterName}' must be an array [x, y, z].");
        }

        var values = new float[3];
        var index = 0;
        foreach (var item in array)
        {
            if (index >= values.Length)
            {
                throw new ArgumentException($"Parameter '{parameterName}' must contain exactly 3 numeric values.");
            }

            if (item.Type != JTokenType.Integer && item.Type != JTokenType.Float)
            {
                throw new ArgumentException($"Parameter '{parameterName}' must contain numeric values.");
            }

            var itemValue = item.Value<float?>();
            if (!itemValue.HasValue)
            {
                throw new ArgumentException($"Parameter '{parameterName}' must contain numeric values.");
            }

            values[index] = itemValue.Value;
            index++;
        }

        if (index != 3)
        {
            throw new ArgumentException($"Parameter '{parameterName}' must contain exactly 3 numeric values.");
        }

        return new Vector3(values[0], values[1], values[2]);
    }

    private static float[] ParseFloatArrayToken(JToken token, string parameterName, int expectedCount)
    {
        if (token is not JArray array)
        {
            throw new ArgumentException($"Property '{parameterName}' must be an array with {expectedCount} numeric values.");
        }

        var values = new float[expectedCount];
        var index = 0;
        foreach (var item in array)
        {
            if (index >= expectedCount)
            {
                throw new ArgumentException($"Property '{parameterName}' must contain exactly {expectedCount} numeric values.");
            }

            if (item.Type != JTokenType.Integer && item.Type != JTokenType.Float)
            {
                throw new ArgumentException($"Property '{parameterName}' must contain numeric values.");
            }

            var itemValue = item.Value<float?>();
            if (!itemValue.HasValue)
            {
                throw new ArgumentException($"Property '{parameterName}' must contain numeric values.");
            }

            values[index] = itemValue.Value;
            index++;
        }

        if (index != expectedCount)
        {
            throw new ArgumentException($"Property '{parameterName}' must contain exactly {expectedCount} numeric values.");
        }

        return values;
    }

    private static Vector2 ParseVector2Token(JToken token, string parameterName)
    {
        var values = ParseFloatArrayToken(token, parameterName, 2);
        return new Vector2(values[0], values[1]);
    }

    private static Vector4 ParseVector4Token(JToken token, string parameterName)
    {
        var values = ParseFloatArrayToken(token, parameterName, 4);
        return new Vector4(values[0], values[1], values[2], values[3]);
    }

    private static Color ParseColorToken(JToken token, string parameterName)
    {
        var values = ParseFloatArrayToken(token, parameterName, 4);
        return new Color(values[0], values[1], values[2], values[3]);
    }

    private static Rect ParseRectToken(JToken token, string parameterName)
    {
        var values = ParseFloatArrayToken(token, parameterName, 4);
        return new Rect(values[0], values[1], values[2], values[3]);
    }

    private static Bounds ParseBoundsToken(JToken token, string parameterName)
    {
        if (token is not JObject objectToken)
        {
            throw new ArgumentException($"Property '{parameterName}' must be an object with 'center' and 'size' vector values.");
        }

        if (!objectToken.TryGetValue("center", out var centerToken))
        {
            throw new ArgumentException($"Property '{parameterName}' must include 'center'.");
        }

        if (!objectToken.TryGetValue("size", out var sizeToken))
        {
            throw new ArgumentException($"Property '{parameterName}' must include 'size'.");
        }

        return new Bounds(ParseVector3Parameter(centerToken, $"{parameterName}.center"), ParseVector3Parameter(sizeToken, $"{parameterName}.size"));
    }

    private static Quaternion ParseQuaternionToken(JToken token, string parameterName)
    {
        var values = ParseFloatArrayToken(token, parameterName, 4);
        return new Quaternion(values[0], values[1], values[2], values[3]);
    }

    private static int? ParseOptionalCapsuleDirectionParameter(JObject paramsObject, string parameterName)
    {
        if (!paramsObject.TryGetValue(parameterName, out var token))
        {
            return null;
        }

        if (token.Type == JTokenType.Integer)
        {
            var value = token.Value<int?>();
            if (!value.HasValue)
            {
                throw new ArgumentException($"Parameter '{parameterName}' must be X, Y, Z, or integer 0/1/2.");
            }

            return value.Value;
        }

        if (token.Type == JTokenType.String)
        {
            var rawValue = token.Value<string>();
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                throw new ArgumentException($"Parameter '{parameterName}' cannot be empty.");
            }

            var normalized = rawValue!.Trim();
            if (string.Equals(normalized, "X", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (string.Equals(normalized, "Y", StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (string.Equals(normalized, "Z", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            throw new ArgumentException($"Parameter '{parameterName}' must be X, Y, Z, or integer 0/1/2.");
        }

        throw new ArgumentException($"Parameter '{parameterName}' must be X, Y, Z, or integer 0/1/2.");
    }

    private static bool IsValidCapsuleDirection(int value)
    {
        return value >= 0 && value <= 2;
    }

    private static void ValidateCommonColliderSettingValues(float? contactOffset)
    {
        if (contactOffset.HasValue && contactOffset.Value < 0f)
        {
            throw new ArgumentException("Parameter 'contactOffset' must be greater than or equal to 0.");
        }
    }

    private static void ValidateCommonCollider2DSettingValues(float? density)
    {
        if (density.HasValue && density.Value < 0f)
        {
            throw new ArgumentException("Parameter 'density' must be greater than or equal to 0.");
        }
    }

    private static void ValidatePositiveVector3(Vector3? value, string parameterName, string errorMessage)
    {
        if (!value.HasValue)
        {
            return;
        }

        var vector = value.Value;
        if (vector.x <= 0f || vector.y <= 0f || vector.z <= 0f)
        {
            throw new ArgumentException(errorMessage);
        }
    }

    private static void ValidatePositiveVector2(Vector2? value, string parameterName, string errorMessage)
    {
        if (!value.HasValue)
        {
            return;
        }

        var vector = value.Value;
        if (vector.x <= 0f || vector.y <= 0f)
        {
            throw new ArgumentException(errorMessage);
        }
    }

    private static void ApplyCommonColliderSettings(Collider collider, bool? enabled, bool? isTrigger, float? contactOffset)
    {
        if (enabled.HasValue)
        {
            collider.enabled = enabled.Value;
        }

        if (isTrigger.HasValue)
        {
            collider.isTrigger = isTrigger.Value;
        }

        if (contactOffset.HasValue)
        {
            collider.contactOffset = contactOffset.Value;
        }
    }

    private static void ApplyCommonCollider2DSettings(
        Collider2D collider,
        bool? enabled,
        bool? isTrigger,
        bool? usedByEffector,
        Vector2? offset,
        float? density)
    {
        if (enabled.HasValue)
        {
            collider.enabled = enabled.Value;
        }

        if (isTrigger.HasValue)
        {
            collider.isTrigger = isTrigger.Value;
        }

        if (usedByEffector.HasValue)
        {
            collider.usedByEffector = usedByEffector.Value;
        }

        if (offset.HasValue)
        {
            collider.offset = offset.Value;
        }

        if (density.HasValue)
        {
            collider.density = density.Value;
        }
    }

    private static void ValidateCommonJoint2DSettingValues(float? breakForce, float? breakTorque)
    {
        if (breakForce.HasValue && breakForce.Value < 0f)
        {
            throw new ArgumentException("Parameter 'breakForce' must be greater than or equal to 0.");
        }

        if (breakTorque.HasValue && breakTorque.Value < 0f)
        {
            throw new ArgumentException("Parameter 'breakTorque' must be greater than or equal to 0.");
        }
    }

    private static Rigidbody2D ResolveConnectedRigidbody2D(int connectedBodyInstanceId)
    {
        var resolvedObject = ResolveObjectByInstanceId(connectedBodyInstanceId, "connectedBodyInstanceId");
        return ResolveComponentOfTypeTarget<Rigidbody2D>(resolvedObject, "connectedBodyInstanceId", "Rigidbody2D");
    }

    private static Rigidbody ResolveConnectedRigidbody(int connectedBodyInstanceId)
    {
        var resolvedObject = ResolveObjectByInstanceId(connectedBodyInstanceId, "connectedBodyInstanceId");
        return ResolveComponentOfTypeTarget<Rigidbody>(resolvedObject, "connectedBodyInstanceId", "Rigidbody");
    }

    private static void ApplyCommonJoint2DSettings(
        AnchoredJoint2D joint,
        bool? enabled,
        bool? autoConfigureConnectedAnchor,
        Vector2? anchor,
        Vector2? connectedAnchor,
        bool? enableCollision,
        float? breakForce,
        float? breakTorque,
        OptionalInstanceIdParameter connectedBodyInstanceId,
        ConnectedAnchorMode? connectedAnchorMode)
    {
        ValidateConnectedAnchorHelperArguments(connectedAnchor, connectedAnchorMode);

        if (enabled.HasValue)
        {
            joint.enabled = enabled.Value;
        }

        if (autoConfigureConnectedAnchor.HasValue)
        {
            joint.autoConfigureConnectedAnchor = autoConfigureConnectedAnchor.Value;
        }

        if (anchor.HasValue)
        {
            joint.anchor = anchor.Value;
        }

        if (connectedBodyInstanceId.IsSpecified)
        {
            if (connectedBodyInstanceId.HasValue)
            {
                var resolvedInstanceId = connectedBodyInstanceId.Value.GetValueOrDefault();
                joint.connectedBody = ResolveConnectedRigidbody2D(resolvedInstanceId);
            }
            else
            {
                joint.connectedBody = null;
            }
        }

        if (connectedAnchor.HasValue)
        {
            joint.connectedAnchor = connectedAnchor.Value;
        }

        ApplyConnectedAnchorMode(joint, anchor, connectedAnchorMode);

        if (enableCollision.HasValue)
        {
            joint.enableCollision = enableCollision.Value;
        }

        if (breakForce.HasValue)
        {
            joint.breakForce = breakForce.Value;
        }

        if (breakTorque.HasValue)
        {
            joint.breakTorque = breakTorque.Value;
        }
    }

    private static void ValidateCommonJointSettingValues(float? breakForce, float? breakTorque)
    {
        if (breakForce.HasValue && breakForce.Value < 0f)
        {
            throw new ArgumentException("Parameter 'breakForce' must be greater than or equal to 0.");
        }

        if (breakTorque.HasValue && breakTorque.Value < 0f)
        {
            throw new ArgumentException("Parameter 'breakTorque' must be greater than or equal to 0.");
        }
    }

    private static void ApplyCommonJointSettings(
        Joint joint,
        bool? autoConfigureConnectedAnchor,
        Vector3? anchor,
        Vector3? connectedAnchor,
        Vector3? axis,
        bool? enableCollision,
        float? breakForce,
        float? breakTorque,
        OptionalInstanceIdParameter connectedBodyInstanceId,
        ConnectedAnchorMode? connectedAnchorMode)
    {
        ValidateConnectedAnchorHelperArguments(connectedAnchor, connectedAnchorMode);

        if (autoConfigureConnectedAnchor.HasValue)
        {
            joint.autoConfigureConnectedAnchor = autoConfigureConnectedAnchor.Value;
        }

        if (anchor.HasValue)
        {
            joint.anchor = anchor.Value;
        }

        if (connectedBodyInstanceId.IsSpecified)
        {
            if (connectedBodyInstanceId.HasValue)
            {
                var resolvedInstanceId = connectedBodyInstanceId.Value.GetValueOrDefault();
                joint.connectedBody = ResolveConnectedRigidbody(resolvedInstanceId);
            }
            else
            {
                joint.connectedBody = null;
            }
        }

        if (connectedAnchor.HasValue)
        {
            joint.connectedAnchor = connectedAnchor.Value;
        }

        ApplyConnectedAnchorMode(joint, anchor, connectedAnchorMode);

        if (axis.HasValue)
        {
            joint.axis = axis.Value;
        }

        if (enableCollision.HasValue)
        {
            joint.enableCollision = enableCollision.Value;
        }

        if (breakForce.HasValue)
        {
            joint.breakForce = breakForce.Value;
        }

        if (breakTorque.HasValue)
        {
            joint.breakTorque = breakTorque.Value;
        }
    }

    private static void ValidateConnectedAnchorHelperArguments(Vector2? connectedAnchor, ConnectedAnchorMode? connectedAnchorMode)
    {
        if (connectedAnchorMode.HasValue && connectedAnchor.HasValue)
        {
            throw new ArgumentException("Parameter 'connectedAnchorMode' cannot be combined with 'connectedAnchor'.");
        }
    }

    private static void ValidateConnectedAnchorHelperArguments(Vector3? connectedAnchor, ConnectedAnchorMode? connectedAnchorMode)
    {
        if (connectedAnchorMode.HasValue && connectedAnchor.HasValue)
        {
            throw new ArgumentException("Parameter 'connectedAnchorMode' cannot be combined with 'connectedAnchor'.");
        }
    }

    private static void ApplyConnectedAnchorMode(AnchoredJoint2D joint, Vector2? anchor, ConnectedAnchorMode? connectedAnchorMode)
    {
        if (!connectedAnchorMode.HasValue || connectedAnchorMode.Value == ConnectedAnchorMode.Preserve)
        {
            return;
        }

        switch (connectedAnchorMode.Value)
        {
            case ConnectedAnchorMode.Auto:
                joint.autoConfigureConnectedAnchor = true;
                break;
            case ConnectedAnchorMode.Zero:
                joint.autoConfigureConnectedAnchor = false;
                joint.connectedAnchor = Vector2.zero;
                break;
            case ConnectedAnchorMode.MatchAnchor:
                joint.autoConfigureConnectedAnchor = false;
                joint.connectedAnchor = anchor ?? joint.anchor;
                break;
        }
    }

    private static void ApplyConnectedAnchorMode(Joint joint, Vector3? anchor, ConnectedAnchorMode? connectedAnchorMode)
    {
        if (!connectedAnchorMode.HasValue || connectedAnchorMode.Value == ConnectedAnchorMode.Preserve)
        {
            return;
        }

        switch (connectedAnchorMode.Value)
        {
            case ConnectedAnchorMode.Auto:
                joint.autoConfigureConnectedAnchor = true;
                break;
            case ConnectedAnchorMode.Zero:
                joint.autoConfigureConnectedAnchor = false;
                joint.connectedAnchor = Vector3.zero;
                break;
            case ConnectedAnchorMode.MatchAnchor:
                joint.autoConfigureConnectedAnchor = false;
                joint.connectedAnchor = anchor ?? joint.anchor;
                break;
        }
    }

    private static string GetConnectedAnchorModeName(ConnectedAnchorMode? connectedAnchorMode)
    {
        return connectedAnchorMode switch
        {
            ConnectedAnchorMode.Auto => "auto",
            ConnectedAnchorMode.Zero => "zero",
            ConnectedAnchorMode.MatchAnchor => "matchAnchor",
            _ => "preserve"
        };
    }

    private static (int? ConnectedBodyInstanceId, JArray ConnectedAnchor, string ConnectedAnchorMode, bool AutoConfigureConnectedAnchor) CreateJoint2DAppliedConnectionState(
        AnchoredJoint2D joint,
        ConnectedAnchorMode? connectedAnchorMode)
    {
        return (
            joint.connectedBody != null ? joint.connectedBody.GetInstanceID() : (int?)null,
            CreateVector2Array(joint.connectedAnchor),
            GetConnectedAnchorModeName(connectedAnchorMode),
            joint.autoConfigureConnectedAnchor);
    }

    private static (int? ConnectedBodyInstanceId, JArray ConnectedAnchor, string ConnectedAnchorMode, bool AutoConfigureConnectedAnchor) CreateJointAppliedConnectionState(
        Joint joint,
        ConnectedAnchorMode? connectedAnchorMode)
    {
        return (
            joint.connectedBody != null ? joint.connectedBody.GetInstanceID() : (int?)null,
            CreateVector3Array(joint.connectedAnchor),
            GetConnectedAnchorModeName(connectedAnchorMode),
            joint.autoConfigureConnectedAnchor);
    }

    private static void ValidateSoftJointLimitUpdate(SoftJointLimitUpdate update, string parameterName)
    {
        if (update.ContactDistance.HasValue && update.ContactDistance.Value < 0f)
        {
            throw new ArgumentException($"Parameter '{parameterName}.contactDistance' must be greater than or equal to 0.");
        }
    }

    private static void ValidateSoftJointLimitSpringUpdate(SoftJointLimitSpringUpdate update, string parameterName)
    {
        if (update.Spring.HasValue && update.Spring.Value < 0f)
        {
            throw new ArgumentException($"Parameter '{parameterName}.spring' must be greater than or equal to 0.");
        }

        if (update.Damper.HasValue && update.Damper.Value < 0f)
        {
            throw new ArgumentException($"Parameter '{parameterName}.damper' must be greater than or equal to 0.");
        }
    }

    private static void ValidateJointDriveUpdate(JointDriveUpdate update, string parameterName)
    {
        if (update.PositionSpring.HasValue && update.PositionSpring.Value < 0f)
        {
            throw new ArgumentException($"Parameter '{parameterName}.positionSpring' must be greater than or equal to 0.");
        }

        if (update.PositionDamper.HasValue && update.PositionDamper.Value < 0f)
        {
            throw new ArgumentException($"Parameter '{parameterName}.positionDamper' must be greater than or equal to 0.");
        }

        if (update.MaximumForce.HasValue && update.MaximumForce.Value < 0f)
        {
            throw new ArgumentException($"Parameter '{parameterName}.maximumForce' must be greater than or equal to 0.");
        }
    }

    private static SoftJointLimit ApplySoftJointLimitUpdate(SoftJointLimit limit, SoftJointLimitUpdate update)
    {
        if (update.Limit.HasValue)
        {
            limit.limit = update.Limit.Value;
        }

        if (update.Bounciness.HasValue)
        {
            limit.bounciness = update.Bounciness.Value;
        }

        if (update.ContactDistance.HasValue)
        {
            limit.contactDistance = update.ContactDistance.Value;
        }

        return limit;
    }

    private static SoftJointLimitSpring ApplySoftJointLimitSpringUpdate(SoftJointLimitSpring spring, SoftJointLimitSpringUpdate update)
    {
        if (update.Spring.HasValue)
        {
            spring.spring = update.Spring.Value;
        }

        if (update.Damper.HasValue)
        {
            spring.damper = update.Damper.Value;
        }

        return spring;
    }

    private static JointDrive ApplyJointDriveUpdate(JointDrive drive, JointDriveUpdate update)
    {
        if (update.PositionSpring.HasValue)
        {
            drive.positionSpring = update.PositionSpring.Value;
        }

        if (update.PositionDamper.HasValue)
        {
            drive.positionDamper = update.PositionDamper.Value;
        }

        if (update.MaximumForce.HasValue)
        {
            drive.maximumForce = update.MaximumForce.Value;
        }

        return drive;
    }

    private static string GetHierarchyPath(Transform transform)
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

    private static object CreateCameraSettingsSnapshot(Camera camera)
    {
        return new
        {
            enabled = camera.enabled,
            orthographic = camera.orthographic,
            fieldOfView = camera.fieldOfView,
            orthographicSize = camera.orthographicSize,
            nearClipPlane = camera.nearClipPlane,
            farClipPlane = camera.farClipPlane,
            depth = camera.depth,
            clearFlags = CreateEnumSummary(camera.clearFlags),
            backgroundColor = CreateColorArray(camera.backgroundColor),
            cullingMask = camera.cullingMask,
            allowHDR = camera.allowHDR,
            allowMSAA = camera.allowMSAA,
            allowDynamicResolution = camera.allowDynamicResolution
        };
    }

    private static object CreateLightSettingsSnapshot(Light light)
    {
        return new
        {
            enabled = light.enabled,
            type = CreateEnumSummary(light.type),
            color = CreateColorArray(light.color),
            intensity = light.intensity,
            range = light.range,
            spotAngle = light.spotAngle,
            shadows = CreateEnumSummary(light.shadows)
        };
    }

    #pragma warning disable CS0618
    private static object CreateRigidbodySettingsSnapshot(Rigidbody rigidbody)
    {
        return new
        {
            mass = rigidbody.mass,
            drag = rigidbody.drag,
            angularDrag = rigidbody.angularDrag,
            useGravity = rigidbody.useGravity,
            isKinematic = rigidbody.isKinematic,
            detectCollisions = rigidbody.detectCollisions,
            constraints = CreateEnumSummary(rigidbody.constraints),
            interpolation = CreateEnumSummary(rigidbody.interpolation),
            collisionDetectionMode = CreateEnumSummary(rigidbody.collisionDetectionMode)
        };
    }
    #pragma warning restore CS0618

    private static object CreateRigidbody2DSettingsSnapshot(Rigidbody2D rigidbody)
    {
        return new
        {
            bodyType = CreateEnumSummary(rigidbody.bodyType),
            simulated = rigidbody.simulated,
            useAutoMass = rigidbody.useAutoMass,
            mass = rigidbody.mass,
            gravityScale = rigidbody.gravityScale,
            constraints = CreateEnumSummary(rigidbody.constraints),
            interpolation = CreateEnumSummary(rigidbody.interpolation),
            collisionDetectionMode = CreateEnumSummary(rigidbody.collisionDetectionMode),
            sleepMode = CreateEnumSummary(rigidbody.sleepMode)
        };
    }

    private static object CreateColliderSettingsSnapshot(Collider collider)
    {
        var boxCollider = collider as BoxCollider;
        var sharedMaterial = collider.sharedMaterial;
        var attachedRigidbody = collider.attachedRigidbody;

        object? subtype = null;
        if (boxCollider != null)
        {
            subtype = new
            {
                kind = "BoxCollider",
                center = CreateVector3Array(boxCollider.center),
                size = CreateVector3Array(boxCollider.size)
            };
        }

        return new
        {
            colliderType = collider.GetType().FullName,
            enabled = collider.enabled,
            isTrigger = collider.isTrigger,
            contactOffset = collider.contactOffset,
            boundsCenter = CreateVector3Array(collider.bounds.center),
            boundsSize = CreateVector3Array(collider.bounds.size),
            sharedMaterial = sharedMaterial != null ? CreateObjectSummary(sharedMaterial) : null,
            attachedRigidbody = attachedRigidbody != null ? CreateObjectSummary(attachedRigidbody) : null,
            subtype
        };
    }

    private static object CreateCollider2DSettingsSnapshot(Collider2D collider)
    {
        var sharedMaterial = collider.sharedMaterial;
        var attachedRigidbody = collider.attachedRigidbody;

        object? subtype = null;
        if (collider is BoxCollider2D boxCollider)
        {
            subtype = new
            {
                kind = "BoxCollider2D",
                size = CreateVector2Array(boxCollider.size),
                edgeRadius = boxCollider.edgeRadius
            };
        }
        else if (collider is CircleCollider2D circleCollider)
        {
            subtype = new
            {
                kind = "CircleCollider2D",
                radius = circleCollider.radius
            };
        }
        else if (collider is CapsuleCollider2D capsuleCollider)
        {
            subtype = new
            {
                kind = "CapsuleCollider2D",
                size = CreateVector2Array(capsuleCollider.size),
                direction = CreateEnumSummary(capsuleCollider.direction)
            };
        }

        return new
        {
            colliderType = collider.GetType().FullName,
            enabled = collider.enabled,
            isTrigger = collider.isTrigger,
            usedByEffector = collider.usedByEffector,
            usedByComposite = IsCollider2DUsedByComposite(collider),
            compositeOperation = CreateEnumSummary(collider.compositeOperation),
            offset = CreateVector2Array(collider.offset),
            density = collider.density,
            shapeCount = collider.shapeCount,
            boundsCenter = CreateVector3Array(collider.bounds.center),
            boundsSize = CreateVector3Array(collider.bounds.size),
            sharedMaterial = sharedMaterial != null ? CreateObjectSummary(sharedMaterial) : null,
            attachedRigidbody = attachedRigidbody != null ? CreateObjectSummary(attachedRigidbody) : null,
            subtype
        };
    }

    private static object CreateBoxColliderSettingsSnapshot(BoxCollider collider)
    {
        return new
        {
            colliderType = collider.GetType().FullName,
            enabled = collider.enabled,
            isTrigger = collider.isTrigger,
            contactOffset = collider.contactOffset,
            boundsCenter = CreateVector3Array(collider.bounds.center),
            boundsSize = CreateVector3Array(collider.bounds.size),
            sharedMaterial = collider.sharedMaterial != null ? CreateObjectSummary(collider.sharedMaterial) : null,
            attachedRigidbody = collider.attachedRigidbody != null ? CreateObjectSummary(collider.attachedRigidbody) : null,
            center = CreateVector3Array(collider.center),
            size = CreateVector3Array(collider.size)
        };
    }

    private static object CreateSphereColliderSettingsSnapshot(SphereCollider collider)
    {
        return new
        {
            colliderType = collider.GetType().FullName,
            enabled = collider.enabled,
            isTrigger = collider.isTrigger,
            contactOffset = collider.contactOffset,
            boundsCenter = CreateVector3Array(collider.bounds.center),
            boundsSize = CreateVector3Array(collider.bounds.size),
            sharedMaterial = collider.sharedMaterial != null ? CreateObjectSummary(collider.sharedMaterial) : null,
            attachedRigidbody = collider.attachedRigidbody != null ? CreateObjectSummary(collider.attachedRigidbody) : null,
            center = CreateVector3Array(collider.center),
            radius = collider.radius
        };
    }

    private static object CreateCapsuleColliderSettingsSnapshot(CapsuleCollider collider)
    {
        return new
        {
            colliderType = collider.GetType().FullName,
            enabled = collider.enabled,
            isTrigger = collider.isTrigger,
            contactOffset = collider.contactOffset,
            boundsCenter = CreateVector3Array(collider.bounds.center),
            boundsSize = CreateVector3Array(collider.bounds.size),
            sharedMaterial = collider.sharedMaterial != null ? CreateObjectSummary(collider.sharedMaterial) : null,
            attachedRigidbody = collider.attachedRigidbody != null ? CreateObjectSummary(collider.attachedRigidbody) : null,
            center = CreateVector3Array(collider.center),
            radius = collider.radius,
            height = collider.height,
            direction = CreateCapsuleDirectionSummary(collider.direction)
        };
    }

    private static object CreateMeshColliderSettingsSnapshot(MeshCollider collider)
    {
        return new
        {
            colliderType = collider.GetType().FullName,
            enabled = collider.enabled,
            isTrigger = collider.isTrigger,
            contactOffset = collider.contactOffset,
            boundsCenter = CreateVector3Array(collider.bounds.center),
            boundsSize = CreateVector3Array(collider.bounds.size),
            sharedMaterial = collider.sharedMaterial != null ? CreateObjectSummary(collider.sharedMaterial) : null,
            attachedRigidbody = collider.attachedRigidbody != null ? CreateObjectSummary(collider.attachedRigidbody) : null,
            convex = collider.convex,
            cookingOptions = CreateEnumSummary(collider.cookingOptions),
            sharedMesh = collider.sharedMesh != null ? CreateObjectSummary(collider.sharedMesh) : null
        };
    }

    private static object CreateBoxCollider2DSettingsSnapshot(BoxCollider2D collider)
    {
        return new
        {
            colliderType = collider.GetType().FullName,
            enabled = collider.enabled,
            isTrigger = collider.isTrigger,
            usedByEffector = collider.usedByEffector,
            usedByComposite = IsCollider2DUsedByComposite(collider),
            compositeOperation = CreateEnumSummary(collider.compositeOperation),
            offset = CreateVector2Array(collider.offset),
            density = collider.density,
            shapeCount = collider.shapeCount,
            boundsCenter = CreateVector3Array(collider.bounds.center),
            boundsSize = CreateVector3Array(collider.bounds.size),
            sharedMaterial = collider.sharedMaterial != null ? CreateObjectSummary(collider.sharedMaterial) : null,
            attachedRigidbody = collider.attachedRigidbody != null ? CreateObjectSummary(collider.attachedRigidbody) : null,
            size = CreateVector2Array(collider.size),
            edgeRadius = collider.edgeRadius
        };
    }

    private static object CreateCircleCollider2DSettingsSnapshot(CircleCollider2D collider)
    {
        return new
        {
            colliderType = collider.GetType().FullName,
            enabled = collider.enabled,
            isTrigger = collider.isTrigger,
            usedByEffector = collider.usedByEffector,
            usedByComposite = IsCollider2DUsedByComposite(collider),
            compositeOperation = CreateEnumSummary(collider.compositeOperation),
            offset = CreateVector2Array(collider.offset),
            density = collider.density,
            shapeCount = collider.shapeCount,
            boundsCenter = CreateVector3Array(collider.bounds.center),
            boundsSize = CreateVector3Array(collider.bounds.size),
            sharedMaterial = collider.sharedMaterial != null ? CreateObjectSummary(collider.sharedMaterial) : null,
            attachedRigidbody = collider.attachedRigidbody != null ? CreateObjectSummary(collider.attachedRigidbody) : null,
            radius = collider.radius
        };
    }

    private static object CreateCapsuleCollider2DSettingsSnapshot(CapsuleCollider2D collider)
    {
        return new
        {
            colliderType = collider.GetType().FullName,
            enabled = collider.enabled,
            isTrigger = collider.isTrigger,
            usedByEffector = collider.usedByEffector,
            usedByComposite = IsCollider2DUsedByComposite(collider),
            compositeOperation = CreateEnumSummary(collider.compositeOperation),
            offset = CreateVector2Array(collider.offset),
            density = collider.density,
            shapeCount = collider.shapeCount,
            boundsCenter = CreateVector3Array(collider.bounds.center),
            boundsSize = CreateVector3Array(collider.bounds.size),
            sharedMaterial = collider.sharedMaterial != null ? CreateObjectSummary(collider.sharedMaterial) : null,
            attachedRigidbody = collider.attachedRigidbody != null ? CreateObjectSummary(collider.attachedRigidbody) : null,
            size = CreateVector2Array(collider.size),
            direction = CreateEnumSummary(collider.direction)
        };
    }

    private static object CreatePolygonCollider2DSettingsSnapshot(PolygonCollider2D collider)
    {
        return new
        {
            colliderType = collider.GetType().FullName,
            enabled = collider.enabled,
            isTrigger = collider.isTrigger,
            usedByEffector = collider.usedByEffector,
            usedByComposite = IsCollider2DUsedByComposite(collider),
            compositeOperation = CreateEnumSummary(collider.compositeOperation),
            offset = CreateVector2Array(collider.offset),
            density = collider.density,
            shapeCount = collider.shapeCount,
            boundsCenter = CreateVector3Array(collider.bounds.center),
            boundsSize = CreateVector3Array(collider.bounds.size),
            sharedMaterial = collider.sharedMaterial != null ? CreateObjectSummary(collider.sharedMaterial) : null,
            attachedRigidbody = collider.attachedRigidbody != null ? CreateObjectSummary(collider.attachedRigidbody) : null,
            pathCount = collider.pathCount,
            pointCount = collider.points?.Length ?? 0
        };
    }

    private static object CreateEdgeCollider2DSettingsSnapshot(EdgeCollider2D collider)
    {
        return new
        {
            colliderType = collider.GetType().FullName,
            enabled = collider.enabled,
            isTrigger = collider.isTrigger,
            usedByEffector = collider.usedByEffector,
            usedByComposite = IsCollider2DUsedByComposite(collider),
            compositeOperation = CreateEnumSummary(collider.compositeOperation),
            offset = CreateVector2Array(collider.offset),
            density = collider.density,
            shapeCount = collider.shapeCount,
            boundsCenter = CreateVector3Array(collider.bounds.center),
            boundsSize = CreateVector3Array(collider.bounds.size),
            sharedMaterial = collider.sharedMaterial != null ? CreateObjectSummary(collider.sharedMaterial) : null,
            attachedRigidbody = collider.attachedRigidbody != null ? CreateObjectSummary(collider.attachedRigidbody) : null,
            edgeRadius = collider.edgeRadius,
            pointCount = collider.points?.Length ?? 0
        };
    }

    private static object CreateCompositeCollider2DSettingsSnapshot(CompositeCollider2D collider)
    {
        return new
        {
            colliderType = collider.GetType().FullName,
            enabled = collider.enabled,
            isTrigger = collider.isTrigger,
            usedByEffector = collider.usedByEffector,
            usedByComposite = IsCollider2DUsedByComposite(collider),
            compositeOperation = CreateEnumSummary(collider.compositeOperation),
            offset = CreateVector2Array(collider.offset),
            density = collider.density,
            shapeCount = collider.shapeCount,
            boundsCenter = CreateVector3Array(collider.bounds.center),
            boundsSize = CreateVector3Array(collider.bounds.size),
            sharedMaterial = collider.sharedMaterial != null ? CreateObjectSummary(collider.sharedMaterial) : null,
            attachedRigidbody = collider.attachedRigidbody != null ? CreateObjectSummary(collider.attachedRigidbody) : null,
            geometryType = CreateEnumSummary(collider.geometryType),
            generationType = CreateEnumSummary(collider.generationType),
            pathCount = collider.pathCount,
            pointCount = collider.pointCount
        };
    }

    private static object CreateJoint2DSettingsSnapshot(AnchoredJoint2D joint)
    {
        return new
        {
            jointType = joint.GetType().FullName,
            enabled = joint.enabled,
            autoConfigureConnectedAnchor = joint.autoConfigureConnectedAnchor,
            anchor = CreateVector2Array(joint.anchor),
            connectedAnchor = CreateVector2Array(joint.connectedAnchor),
            enableCollision = joint.enableCollision,
            breakForce = joint.breakForce,
            breakTorque = joint.breakTorque,
            reactionForce = CreateVector2Array(joint.reactionForce),
            reactionTorque = joint.reactionTorque,
            connectedBody = joint.connectedBody != null ? CreateObjectSummary(joint.connectedBody) : null
        };
    }

    private static object CreateHingeJoint2DSettingsSnapshot(HingeJoint2D joint)
    {
        return new
        {
            jointType = joint.GetType().FullName,
            enabled = joint.enabled,
            autoConfigureConnectedAnchor = joint.autoConfigureConnectedAnchor,
            anchor = CreateVector2Array(joint.anchor),
            connectedAnchor = CreateVector2Array(joint.connectedAnchor),
            enableCollision = joint.enableCollision,
            breakForce = joint.breakForce,
            breakTorque = joint.breakTorque,
            reactionForce = CreateVector2Array(joint.reactionForce),
            reactionTorque = joint.reactionTorque,
            connectedBody = joint.connectedBody != null ? CreateObjectSummary(joint.connectedBody) : null,
            useConnectedAnchor = joint.useConnectedAnchor,
            useMotor = joint.useMotor,
            motor = new
            {
                motorSpeed = joint.motor.motorSpeed,
                maxMotorTorque = joint.motor.maxMotorTorque
            },
            useLimits = joint.useLimits,
            limits = new
            {
                lowerAngle = joint.limits.min,
                upperAngle = joint.limits.max
            },
            referenceAngle = joint.referenceAngle,
            jointAngle = joint.jointAngle,
            jointSpeed = joint.jointSpeed
        };
    }

    private static object CreateSpringJoint2DSettingsSnapshot(SpringJoint2D joint)
    {
        return new
        {
            jointType = joint.GetType().FullName,
            enabled = joint.enabled,
            autoConfigureConnectedAnchor = joint.autoConfigureConnectedAnchor,
            anchor = CreateVector2Array(joint.anchor),
            connectedAnchor = CreateVector2Array(joint.connectedAnchor),
            enableCollision = joint.enableCollision,
            breakForce = joint.breakForce,
            breakTorque = joint.breakTorque,
            reactionForce = CreateVector2Array(joint.reactionForce),
            reactionTorque = joint.reactionTorque,
            connectedBody = joint.connectedBody != null ? CreateObjectSummary(joint.connectedBody) : null,
            autoConfigureDistance = joint.autoConfigureDistance,
            distance = joint.distance,
            dampingRatio = joint.dampingRatio,
            frequency = joint.frequency
        };
    }

    private static object CreateDistanceJoint2DSettingsSnapshot(DistanceJoint2D joint)
    {
        return new
        {
            jointType = joint.GetType().FullName,
            enabled = joint.enabled,
            autoConfigureConnectedAnchor = joint.autoConfigureConnectedAnchor,
            anchor = CreateVector2Array(joint.anchor),
            connectedAnchor = CreateVector2Array(joint.connectedAnchor),
            enableCollision = joint.enableCollision,
            breakForce = joint.breakForce,
            breakTorque = joint.breakTorque,
            reactionForce = CreateVector2Array(joint.reactionForce),
            reactionTorque = joint.reactionTorque,
            connectedBody = joint.connectedBody != null ? CreateObjectSummary(joint.connectedBody) : null,
            autoConfigureDistance = joint.autoConfigureDistance,
            distance = joint.distance,
            maxDistanceOnly = joint.maxDistanceOnly
        };
    }

    private static object CreateFixedJoint2DSettingsSnapshot(FixedJoint2D joint)
    {
        return new
        {
            jointType = joint.GetType().FullName,
            enabled = joint.enabled,
            autoConfigureConnectedAnchor = joint.autoConfigureConnectedAnchor,
            anchor = CreateVector2Array(joint.anchor),
            connectedAnchor = CreateVector2Array(joint.connectedAnchor),
            enableCollision = joint.enableCollision,
            breakForce = joint.breakForce,
            breakTorque = joint.breakTorque,
            reactionForce = CreateVector2Array(joint.reactionForce),
            reactionTorque = joint.reactionTorque,
            connectedBody = joint.connectedBody != null ? CreateObjectSummary(joint.connectedBody) : null,
            dampingRatio = joint.dampingRatio,
            frequency = joint.frequency,
            referenceAngle = joint.referenceAngle
        };
    }

    private static object CreateSliderJoint2DSettingsSnapshot(SliderJoint2D joint)
    {
        return new
        {
            jointType = joint.GetType().FullName,
            enabled = joint.enabled,
            autoConfigureConnectedAnchor = joint.autoConfigureConnectedAnchor,
            anchor = CreateVector2Array(joint.anchor),
            connectedAnchor = CreateVector2Array(joint.connectedAnchor),
            enableCollision = joint.enableCollision,
            breakForce = joint.breakForce,
            breakTorque = joint.breakTorque,
            reactionForce = CreateVector2Array(joint.reactionForce),
            reactionTorque = joint.reactionTorque,
            connectedBody = joint.connectedBody != null ? CreateObjectSummary(joint.connectedBody) : null,
            autoConfigureAngle = joint.autoConfigureAngle,
            angle = joint.angle,
            useMotor = joint.useMotor,
            motor = new
            {
                motorSpeed = joint.motor.motorSpeed,
                maxMotorTorque = joint.motor.maxMotorTorque
            },
            useLimits = joint.useLimits,
            limits = new
            {
                lowerTranslation = joint.limits.min,
                upperTranslation = joint.limits.max
            },
            limitState = CreateEnumSummary(joint.limitState),
            referenceAngle = joint.referenceAngle,
            jointTranslation = joint.jointTranslation,
            jointSpeed = joint.jointSpeed
        };
    }

    private static object CreateWheelJoint2DSettingsSnapshot(WheelJoint2D joint)
    {
        return new
        {
            jointType = joint.GetType().FullName,
            enabled = joint.enabled,
            autoConfigureConnectedAnchor = joint.autoConfigureConnectedAnchor,
            anchor = CreateVector2Array(joint.anchor),
            connectedAnchor = CreateVector2Array(joint.connectedAnchor),
            enableCollision = joint.enableCollision,
            breakForce = joint.breakForce,
            breakTorque = joint.breakTorque,
            reactionForce = CreateVector2Array(joint.reactionForce),
            reactionTorque = joint.reactionTorque,
            connectedBody = joint.connectedBody != null ? CreateObjectSummary(joint.connectedBody) : null,
            useMotor = joint.useMotor,
            motor = new
            {
                motorSpeed = joint.motor.motorSpeed,
                maxMotorTorque = joint.motor.maxMotorTorque
            },
            suspension = new
            {
                dampingRatio = joint.suspension.dampingRatio,
                frequency = joint.suspension.frequency,
                angle = joint.suspension.angle
            },
            jointTranslation = joint.jointTranslation,
            jointLinearSpeed = joint.jointLinearSpeed
        };
    }

    private static object CreateTargetJoint2DSettingsSnapshot(TargetJoint2D joint)
    {
        return new
        {
            jointType = joint.GetType().FullName,
            enabled = joint.enabled,
            anchor = CreateVector2Array(joint.anchor),
            breakForce = joint.breakForce,
            breakTorque = joint.breakTorque,
            reactionForce = CreateVector2Array(joint.reactionForce),
            reactionTorque = joint.reactionTorque,
            autoConfigureTarget = joint.autoConfigureTarget,
            target = CreateVector2Array(joint.target),
            maxForce = joint.maxForce,
            dampingRatio = joint.dampingRatio,
            frequency = joint.frequency
        };
    }

    private static object CreateJointSettingsSnapshot(Joint joint)
    {
        return new
        {
            jointType = joint.GetType().FullName,
            autoConfigureConnectedAnchor = joint.autoConfigureConnectedAnchor,
            anchor = CreateVector3Array(joint.anchor),
            connectedAnchor = CreateVector3Array(joint.connectedAnchor),
            axis = CreateVector3Array(joint.axis),
            enableCollision = joint.enableCollision,
            breakForce = joint.breakForce,
            breakTorque = joint.breakTorque,
            currentForce = CreateVector3Array(joint.currentForce),
            currentTorque = CreateVector3Array(joint.currentTorque),
            connectedBody = joint.connectedBody != null ? CreateObjectSummary(joint.connectedBody) : null
        };
    }

    private static object CreateHingeJointSettingsSnapshot(HingeJoint joint)
    {
        return new
        {
            jointType = joint.GetType().FullName,
            autoConfigureConnectedAnchor = joint.autoConfigureConnectedAnchor,
            anchor = CreateVector3Array(joint.anchor),
            connectedAnchor = CreateVector3Array(joint.connectedAnchor),
            axis = CreateVector3Array(joint.axis),
            enableCollision = joint.enableCollision,
            breakForce = joint.breakForce,
            breakTorque = joint.breakTorque,
            currentForce = CreateVector3Array(joint.currentForce),
            currentTorque = CreateVector3Array(joint.currentTorque),
            connectedBody = joint.connectedBody != null ? CreateObjectSummary(joint.connectedBody) : null,
            useSpring = joint.useSpring,
            spring = new
            {
                spring = joint.spring.spring,
                damper = joint.spring.damper,
                targetPosition = joint.spring.targetPosition
            },
            useMotor = joint.useMotor,
            motor = new
            {
                targetVelocity = joint.motor.targetVelocity,
                force = joint.motor.force,
                freeSpin = joint.motor.freeSpin
            },
            useLimits = joint.useLimits,
            limits = new
            {
                minLimit = joint.limits.min,
                maxLimit = joint.limits.max,
                bounciness = joint.limits.bounciness,
                bounceMinVelocity = joint.limits.bounceMinVelocity,
                contactDistance = joint.limits.contactDistance
            },
            angle = joint.angle,
            velocity = joint.velocity
        };
    }

    private static object CreateSpringJointSettingsSnapshot(SpringJoint joint)
    {
        return new
        {
            jointType = joint.GetType().FullName,
            autoConfigureConnectedAnchor = joint.autoConfigureConnectedAnchor,
            anchor = CreateVector3Array(joint.anchor),
            connectedAnchor = CreateVector3Array(joint.connectedAnchor),
            axis = CreateVector3Array(joint.axis),
            enableCollision = joint.enableCollision,
            breakForce = joint.breakForce,
            breakTorque = joint.breakTorque,
            currentForce = CreateVector3Array(joint.currentForce),
            currentTorque = CreateVector3Array(joint.currentTorque),
            connectedBody = joint.connectedBody != null ? CreateObjectSummary(joint.connectedBody) : null,
            spring = joint.spring,
            damper = joint.damper,
            minDistance = joint.minDistance,
            maxDistance = joint.maxDistance,
            tolerance = joint.tolerance
        };
    }

    private static object CreateFixedJointSettingsSnapshot(FixedJoint joint)
    {
        return new
        {
            jointType = joint.GetType().FullName,
            autoConfigureConnectedAnchor = joint.autoConfigureConnectedAnchor,
            anchor = CreateVector3Array(joint.anchor),
            connectedAnchor = CreateVector3Array(joint.connectedAnchor),
            axis = CreateVector3Array(joint.axis),
            enableCollision = joint.enableCollision,
            breakForce = joint.breakForce,
            breakTorque = joint.breakTorque,
            currentForce = CreateVector3Array(joint.currentForce),
            currentTorque = CreateVector3Array(joint.currentTorque),
            connectedBody = joint.connectedBody != null ? CreateObjectSummary(joint.connectedBody) : null
        };
    }

    private static object CreateCharacterJointSettingsSnapshot(CharacterJoint joint)
    {
        return new
        {
            jointType = joint.GetType().FullName,
            autoConfigureConnectedAnchor = joint.autoConfigureConnectedAnchor,
            anchor = CreateVector3Array(joint.anchor),
            connectedAnchor = CreateVector3Array(joint.connectedAnchor),
            axis = CreateVector3Array(joint.axis),
            enableCollision = joint.enableCollision,
            breakForce = joint.breakForce,
            breakTorque = joint.breakTorque,
            currentForce = CreateVector3Array(joint.currentForce),
            currentTorque = CreateVector3Array(joint.currentTorque),
            connectedBody = joint.connectedBody != null ? CreateObjectSummary(joint.connectedBody) : null,
            swingAxis = CreateVector3Array(joint.swingAxis),
            enableProjection = joint.enableProjection,
            enablePreprocessing = joint.enablePreprocessing,
            twistLimitSpring = CreateSoftJointLimitSpringSnapshot(joint.twistLimitSpring),
            swingLimitSpring = CreateSoftJointLimitSpringSnapshot(joint.swingLimitSpring),
            lowTwistLimit = CreateSoftJointLimitSnapshot(joint.lowTwistLimit),
            highTwistLimit = CreateSoftJointLimitSnapshot(joint.highTwistLimit),
            swing1Limit = CreateSoftJointLimitSnapshot(joint.swing1Limit),
            swing2Limit = CreateSoftJointLimitSnapshot(joint.swing2Limit)
        };
    }

    private static object CreateConfigurableJointSettingsSnapshot(ConfigurableJoint joint)
    {
        return new
        {
            jointType = joint.GetType().FullName,
            autoConfigureConnectedAnchor = joint.autoConfigureConnectedAnchor,
            anchor = CreateVector3Array(joint.anchor),
            connectedAnchor = CreateVector3Array(joint.connectedAnchor),
            axis = CreateVector3Array(joint.axis),
            secondaryAxis = CreateVector3Array(joint.secondaryAxis),
            enableCollision = joint.enableCollision,
            breakForce = joint.breakForce,
            breakTorque = joint.breakTorque,
            currentForce = CreateVector3Array(joint.currentForce),
            currentTorque = CreateVector3Array(joint.currentTorque),
            connectedBody = joint.connectedBody != null ? CreateObjectSummary(joint.connectedBody) : null,
            configuredInWorldSpace = joint.configuredInWorldSpace,
            swapBodies = joint.swapBodies,
            xMotion = CreateEnumSummary(joint.xMotion),
            yMotion = CreateEnumSummary(joint.yMotion),
            zMotion = CreateEnumSummary(joint.zMotion),
            angularXMotion = CreateEnumSummary(joint.angularXMotion),
            angularYMotion = CreateEnumSummary(joint.angularYMotion),
            angularZMotion = CreateEnumSummary(joint.angularZMotion),
            linearLimit = CreateSoftJointLimitSnapshot(joint.linearLimit),
            lowAngularXLimit = CreateSoftJointLimitSnapshot(joint.lowAngularXLimit),
            highAngularXLimit = CreateSoftJointLimitSnapshot(joint.highAngularXLimit),
            angularYLimit = CreateSoftJointLimitSnapshot(joint.angularYLimit),
            angularZLimit = CreateSoftJointLimitSnapshot(joint.angularZLimit),
            targetPosition = CreateVector3Array(joint.targetPosition),
            targetVelocity = CreateVector3Array(joint.targetVelocity),
            targetAngularVelocity = CreateVector3Array(joint.targetAngularVelocity),
            rotationDriveMode = CreateEnumSummary(joint.rotationDriveMode),
            xDrive = CreateJointDriveSnapshot(joint.xDrive),
            yDrive = CreateJointDriveSnapshot(joint.yDrive),
            zDrive = CreateJointDriveSnapshot(joint.zDrive),
            angularXDrive = CreateJointDriveSnapshot(joint.angularXDrive),
            angularYZDrive = CreateJointDriveSnapshot(joint.angularYZDrive),
            slerpDrive = CreateJointDriveSnapshot(joint.slerpDrive),
            projectionMode = CreateEnumSummary(joint.projectionMode),
            projectionDistance = joint.projectionDistance,
            projectionAngle = joint.projectionAngle
        };
    }

    private static object CreateSoftJointLimitSnapshot(SoftJointLimit limit)
    {
        return new
        {
            limit = limit.limit,
            bounciness = limit.bounciness,
            contactDistance = limit.contactDistance
        };
    }

    private static object CreateSoftJointLimitSpringSnapshot(SoftJointLimitSpring spring)
    {
        return new
        {
            spring = spring.spring,
            damper = spring.damper
        };
    }

    private static object CreateJointDriveSnapshot(JointDrive drive)
    {
        return new
        {
            positionSpring = drive.positionSpring,
            positionDamper = drive.positionDamper,
            maximumForce = drive.maximumForce
        };
    }

    private static object CreateCapsuleDirectionSummary(int direction)
    {
        var name = direction switch
        {
            0 => "X",
            1 => "Y",
            2 => "Z",
            _ => "Unknown"
        };

        return new
        {
            name,
            value = direction
        };
    }

    private static bool IsCollider2DUsedByComposite(Collider2D collider)
    {
        return collider.compositeOperation != Collider2D.CompositeOperation.None;
    }

    private static object CreateEnumSummary<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        return new
        {
            name = value.ToString(),
            value = Convert.ToInt64(value)
        };
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

    private static JArray CreateVector2Array(Vector2 value)
    {
        return new JArray(value.x, value.y);
    }

    private static JArray CreateVector3Array(Vector3 value)
    {
        return new JArray(value.x, value.y, value.z);
    }

    private static JArray CreateVector4Array(Vector4 value)
    {
        return new JArray(value.x, value.y, value.z, value.w);
    }

    private static JArray CreateColorArray(Color value)
    {
        return new JArray(value.r, value.g, value.b, value.a);
    }

    private static JArray CreateRectArray(Rect value)
    {
        return new JArray(value.x, value.y, value.width, value.height);
    }

    private static JObject CreateBoundsObject(Bounds value)
    {
        return new JObject
        {
            ["center"] = CreateVector3Array(value.center),
            ["size"] = CreateVector3Array(value.size),
            ["extents"] = CreateVector3Array(value.extents)
        };
    }

    private static JArray CreateQuaternionArray(Quaternion value)
    {
        return new JArray(value.x, value.y, value.z, value.w);
    }

    private static float[] ToVectorArray(Vector3 value)
    {
        return new[] { value.x, value.y, value.z };
    }

}
}

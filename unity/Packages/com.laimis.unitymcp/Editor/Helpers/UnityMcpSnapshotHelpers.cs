#nullable enable

using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMcp.Editor
{
    internal static class UnityMcpSnapshotHelpers
    {
        internal static JArray CreateVector2Array(Vector2 value)
        {
            return new JArray(value.x, value.y);
        }

        internal static JArray CreateVector3Array(Vector3 value)
        {
            return new JArray(value.x, value.y, value.z);
        }

        internal static JArray CreateVector4Array(Vector4 value)
        {
            return new JArray(value.x, value.y, value.z, value.w);
        }

        internal static JArray CreateColorArray(Color value)
        {
            return new JArray(value.r, value.g, value.b, value.a);
        }

        internal static JArray CreateRectArray(Rect value)
        {
            return new JArray(value.x, value.y, value.width, value.height);
        }

        internal static JArray CreateQuaternionArray(Quaternion value)
        {
            return new JArray(value.x, value.y, value.z, value.w);
        }

        internal static JObject CreateBoundsObject(Bounds value)
        {
            return new JObject
            {
                ["center"] = CreateVector3Array(value.center),
                ["size"] = CreateVector3Array(value.size),
                ["extents"] = CreateVector3Array(value.extents)
            };
        }

        internal static void ValidateWritableSerializedProperty(SerializedProperty property)
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

        internal static bool TryReadSerializedPropertyValue(SerializedProperty property, out JToken serializedValue, out string? unsupportedReason)
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
                        ? JToken.FromObject(UnityMcpClient.CreateObjectSummary(property.objectReferenceValue))
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

        internal static JToken CreateEnumSerializedValue(SerializedProperty property)
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

        internal static void WriteSerializedPropertyValue(SerializedProperty property, JToken valueToken)
        {
            var propertyPath = property.propertyPath;

            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    property.boolValue = UnityMcpParameterHelpers.ParseBooleanToken(valueToken, propertyPath);
                    return;

                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                    property.intValue = UnityMcpParameterHelpers.ParseIntegerToken(valueToken, propertyPath);
                    return;

                case SerializedPropertyType.Float:
                    property.floatValue = UnityMcpParameterHelpers.ParseFloatToken(valueToken, propertyPath);
                    return;

                case SerializedPropertyType.String:
                    property.stringValue = UnityMcpParameterHelpers.ParseStringToken(valueToken, propertyPath);
                    return;

                case SerializedPropertyType.Enum:
                    property.enumValueIndex = UnityMcpParameterHelpers.ParseEnumToken(valueToken, property, propertyPath);
                    return;

                case SerializedPropertyType.Color:
                    property.colorValue = UnityMcpParameterHelpers.ParseColorToken(valueToken, propertyPath);
                    return;

                case SerializedPropertyType.Vector2:
                    property.vector2Value = UnityMcpParameterHelpers.ParseVector2Token(valueToken, propertyPath);
                    return;

                case SerializedPropertyType.Vector3:
                    property.vector3Value = UnityMcpParameterHelpers.ParseVector3Parameter(valueToken, propertyPath);
                    return;

                case SerializedPropertyType.Vector4:
                    property.vector4Value = UnityMcpParameterHelpers.ParseVector4Token(valueToken, propertyPath);
                    return;

                case SerializedPropertyType.Rect:
                    property.rectValue = UnityMcpParameterHelpers.ParseRectToken(valueToken, propertyPath);
                    return;

                case SerializedPropertyType.Bounds:
                    property.boundsValue = UnityMcpParameterHelpers.ParseBoundsToken(valueToken, propertyPath);
                    return;

                case SerializedPropertyType.Quaternion:
                    property.quaternionValue = UnityMcpParameterHelpers.ParseQuaternionToken(valueToken, propertyPath);
                    return;

                default:
                    throw new ArgumentException(
                        $"Serialized property '{propertyPath}' has unsupported type '{property.propertyType}' for writes in the MVP.");
            }
        }

        internal static int ValidateEnumIndex(SerializedProperty property, string propertyPath, int index)
        {
            if (index < 0 || index >= property.enumNames.Length)
            {
                throw new ArgumentException(
                    $"Property '{propertyPath}' enum index {index} is out of range (0-{Math.Max(0, property.enumNames.Length - 1)}).");
            }

            return index;
        }
    }
}

#nullable enable

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace UnityMcp.Editor
{
    internal static class UnityMcpParameterHelpers
    {
        internal static JObject RequireParamsObject(JObject root, string methodName)
        {
            if (!root.TryGetValue("params", out var paramsToken) || paramsToken is not JObject paramsObject)
            {
                throw new ArgumentException($"Method '{methodName}' expects params to be an object.");
            }

            return paramsObject;
        }

        internal static int ParseRequiredIntegerParameter(JObject paramsObject, string parameterName)
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

        internal static string ParseRequiredStringParameter(JObject paramsObject, string parameterName)
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

        internal static float ParseRequiredFloatParameter(JObject paramsObject, string parameterName)
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

        internal static bool ParseRequiredBooleanParameter(JObject paramsObject, string parameterName)
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

        internal static string? ParseOptionalStringParameter(JObject paramsObject, string parameterName)
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

        internal static Vector3? ParseOptionalVector3Parameter(JObject paramsObject, string parameterName)
        {
            if (!paramsObject.TryGetValue(parameterName, out var token))
            {
                return null;
            }

            return ParseVector3Parameter(token, parameterName);
        }

        internal static Vector2? ParseOptionalVector2Parameter(JObject paramsObject, string parameterName)
        {
            if (!paramsObject.TryGetValue(parameterName, out var token))
            {
                return null;
            }

            return ParseVector2Token(token, parameterName);
        }

        internal static Color? ParseOptionalColorParameter(JObject paramsObject, string parameterName)
        {
            if (!paramsObject.TryGetValue(parameterName, out var token))
            {
                return null;
            }

            return ParseColorToken(token, parameterName);
        }

        internal static float? ParseOptionalFloatParameter(JObject paramsObject, string parameterName)
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

        internal static int? ParseOptionalIntegerParameter(JObject paramsObject, string parameterName)
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

        internal static OptionalInstanceIdParameter ParseOptionalNullableIntegerParameter(JObject paramsObject, string parameterName)
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

        internal static bool? ParseOptionalBooleanValueParameter(JObject paramsObject, string parameterName)
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

        internal static bool ParseOptionalBooleanParameter(JObject paramsObject, string parameterName, bool defaultValue = false)
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

        internal static TEnum? ParseOptionalEnumParameter<TEnum>(JObject paramsObject, string parameterName)
            where TEnum : struct, Enum
        {
            if (!paramsObject.TryGetValue(parameterName, out var token))
            {
                return null;
            }

            return ParseEnumToken<TEnum>(token, parameterName);
        }

        internal static JObject? ParseOptionalObjectParameter(JObject paramsObject, string parameterName)
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

        internal static ConnectedAnchorMode? ParseOptionalConnectedAnchorModeParameter(JObject paramsObject, string parameterName)
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

        internal static PrefabOverrideScope ParseOptionalPrefabOverrideScopeParameter(
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

        internal static SoftJointLimitUpdate ParseOptionalSoftJointLimitParameter(JObject paramsObject, string parameterName)
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

        internal static SoftJointLimitSpringUpdate ParseOptionalSoftJointLimitSpringParameter(JObject paramsObject, string parameterName)
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

        internal static JointDriveUpdate ParseOptionalJointDriveParameter(JObject paramsObject, string parameterName)
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

        internal static TEnum ParseEnumToken<TEnum>(JToken token, string parameterName)
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

        internal static bool ParseBooleanToken(JToken token, string propertyPath)
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

        internal static int ParseIntegerToken(JToken token, string propertyPath)
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

        internal static float ParseFloatToken(JToken token, string propertyPath)
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

        internal static string ParseStringToken(JToken token, string propertyPath)
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

        internal static int ParseEnumToken(JToken token, SerializedProperty property, string propertyPath)
        {
            if (token.Type == JTokenType.Integer)
            {
                var index = token.Value<int?>();
                if (!index.HasValue)
                {
                    throw new ArgumentException($"Property '{propertyPath}' enum value must be an integer index or string name.");
                }

                return UnityMcpSnapshotHelpers.ValidateEnumIndex(property, propertyPath, index.Value);
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

        internal static List<string>? ParseOptionalStringArrayParameter(JObject paramsObject, string parameterName)
        {
            if (!paramsObject.TryGetValue(parameterName, out var token))
            {
                return null;
            }

            if (token.Type != JTokenType.Array || token is not JArray array)
            {
                throw new ArgumentException($"Parameter '{parameterName}' must be an array of strings.");
            }

            var values = new List<string>(array.Count);
            foreach (var item in array)
            {
                if (item.Type != JTokenType.String)
                {
                    throw new ArgumentException($"Parameter '{parameterName}' must contain only strings.");
                }

                var value = item.Value<string>();
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException($"Parameter '{parameterName}' cannot contain empty values.");
                }

                values.Add(value!.Trim());
            }

            return values;
        }

        internal static string BuildEffectiveAssetsFindQuery(string query, List<string>? types, List<string>? labels)
        {
            var parts = new List<string> { query };

            if (types != null)
            {
                foreach (var type in types)
                {
                    parts.Add($"t:{type}");
                }
            }

            if (labels != null)
            {
                foreach (var label in labels)
                {
                    parts.Add($"l:{label}");
                }
            }

            return string.Join(" ", parts);
        }

        internal static void ParseConsoleQueryOptions(
            JObject root,
            string methodName,
            int defaultMaxResults,
            bool defaultIncludeStackTrace,
            bool requireAfterSequence,
            out int maxResults,
            out bool includeStackTrace,
            out long afterSequence,
            out List<string>? levels,
            out string? contains)
        {
            maxResults = defaultMaxResults;
            includeStackTrace = defaultIncludeStackTrace;
            afterSequence = 0;
            levels = null;
            contains = null;

            if (!root.TryGetValue("params", out var paramsToken) || paramsToken.Type == JTokenType.Null)
            {
                if (requireAfterSequence)
                {
                    throw new ArgumentException($"Method '{methodName}' expects params to be an object.");
                }

                return;
            }

            if (paramsToken is not JObject paramsObject)
            {
                throw new ArgumentException($"Method '{methodName}' expects params to be an object.");
            }

            if (paramsObject.TryGetValue("maxResults", out var maxResultsToken))
            {
                if (maxResultsToken.Type != JTokenType.Integer)
                {
                    throw new ArgumentException("Parameter 'maxResults' must be an integer.");
                }

                var parsedMaxResults = maxResultsToken.Value<int?>();
                if (!parsedMaxResults.HasValue || parsedMaxResults.Value < 1 || parsedMaxResults.Value > 500)
                {
                    throw new ArgumentException("Parameter 'maxResults' must be between 1 and 500.");
                }

                maxResults = parsedMaxResults.Value;
            }

            if (paramsObject.TryGetValue("includeStackTrace", out var includeStackTraceToken))
            {
                if (includeStackTraceToken.Type != JTokenType.Boolean)
                {
                    throw new ArgumentException("Parameter 'includeStackTrace' must be a boolean.");
                }

                var parsedIncludeStackTrace = includeStackTraceToken.Value<bool?>();
                if (!parsedIncludeStackTrace.HasValue)
                {
                    throw new ArgumentException("Parameter 'includeStackTrace' must be a boolean.");
                }

                includeStackTrace = parsedIncludeStackTrace.Value;
            }

            var parsedLevels = ParseOptionalStringArrayParameter(paramsObject, "levels");
            if (parsedLevels != null)
            {
                levels = NormalizeConsoleLevels(parsedLevels);
            }

            if (paramsObject.TryGetValue("contains", out var containsToken))
            {
                if (containsToken.Type != JTokenType.String)
                {
                    throw new ArgumentException("Parameter 'contains' must be a string.");
                }

                var parsedContains = containsToken.Value<string>();
                if (string.IsNullOrWhiteSpace(parsedContains))
                {
                    throw new ArgumentException("Parameter 'contains' cannot be empty.");
                }

                contains = parsedContains!.Trim();
            }

            if (requireAfterSequence)
            {
                if (!paramsObject.TryGetValue("afterSequence", out var afterSequenceToken) || afterSequenceToken.Type != JTokenType.Integer)
                {
                    throw new ArgumentException("Parameter 'afterSequence' is required and must be an integer.");
                }

                var parsedAfterSequence = afterSequenceToken.Value<long?>();
                if (!parsedAfterSequence.HasValue || parsedAfterSequence.Value < 0)
                {
                    throw new ArgumentException("Parameter 'afterSequence' must be a non-negative integer.");
                }

                afterSequence = parsedAfterSequence.Value;
            }
        }

        internal static bool TryGetFloat(JObject obj, string key, out float value)
        {
            value = 0f;
            if (!obj.TryGetValue(key, out var token)) return false;
            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
            {
                value = token.Value<float>();
                return true;
            }
            return false;
        }

        internal static TestMode ParseTestMode(string mode)
        {
            return mode.ToLowerInvariant() switch
            {
                "editmode" => TestMode.EditMode,
                "playmode" => TestMode.PlayMode,
                _ => throw new ArgumentException("Parameter 'mode' must be 'editMode' or 'playMode'.")
            };
        }

        internal static Vector3 ParseVector3Parameter(JToken token, string parameterName)
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

        internal static float[] ParseFloatArrayToken(JToken token, string parameterName, int expectedCount)
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

        internal static Vector2 ParseVector2Token(JToken token, string parameterName)
        {
            var values = ParseFloatArrayToken(token, parameterName, 2);
            return new Vector2(values[0], values[1]);
        }

        internal static Vector4 ParseVector4Token(JToken token, string parameterName)
        {
            var values = ParseFloatArrayToken(token, parameterName, 4);
            return new Vector4(values[0], values[1], values[2], values[3]);
        }

        internal static Color ParseColorToken(JToken token, string parameterName)
        {
            var values = ParseFloatArrayToken(token, parameterName, 4);
            return new Color(values[0], values[1], values[2], values[3]);
        }

        internal static Rect ParseRectToken(JToken token, string parameterName)
        {
            var values = ParseFloatArrayToken(token, parameterName, 4);
            return new Rect(values[0], values[1], values[2], values[3]);
        }

        internal static Bounds ParseBoundsToken(JToken token, string parameterName)
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

        internal static Quaternion ParseQuaternionToken(JToken token, string parameterName)
        {
            var values = ParseFloatArrayToken(token, parameterName, 4);
            return new Quaternion(values[0], values[1], values[2], values[3]);
        }

        private static List<string> NormalizeConsoleLevels(List<string> levels)
        {
            var normalized = new List<string>(levels.Count);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var level in levels)
            {
                string canonical = level.ToLowerInvariant() switch
                {
                    "info" => "info",
                    "log" => "info",
                    "warning" => "warning",
                    "warn" => "warning",
                    "error" => "error",
                    "assert" => "assert",
                    "exception" => "exception",
                    _ => throw new ArgumentException(
                        "Parameter 'levels' contains an unsupported value. Allowed values: info, warning, error, assert, exception.")
                };

                if (seen.Add(canonical))
                {
                    normalized.Add(canonical);
                }
            }

            return normalized;
        }
    }
}

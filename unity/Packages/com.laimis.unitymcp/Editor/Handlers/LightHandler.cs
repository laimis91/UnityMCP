#nullable enable

using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static UnityMcp.Editor.UnityMcpParameterHelpers;
using static UnityMcp.Editor.UnityMcpResolvers;
using static UnityMcp.Editor.UnityMcpSnapshotHelpers;

namespace UnityMcp.Editor
{
    internal static class LightHandler
    {
        // ── Light Get Settings ──────────────────────────────────────────────

        internal static string BuildGetLightSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "light.getSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var light = ResolveComponentOfTypeTarget<Light>(resolvedObject, "instanceId", "Light");

            var result = new
            {
                target = CreateObjectSummary(light.gameObject),
                component = CreateComponentSummary(light),
                settings = CreateLightSettingsSnapshot(light)
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        // ── Light Set Settings ──────────────────────────────────────────────

        internal static string BuildSetLightSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "light.setSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var light = ResolveComponentOfTypeTarget<Light>(resolvedObject, "instanceId", "Light");

            var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
            var type = ParseOptionalEnumParameter<LightType>(paramsObject, "type");
            var color = ParseOptionalColorParameter(paramsObject, "color");
            var intensity = ParseOptionalFloatParameter(paramsObject, "intensity");
            var range = ParseOptionalFloatParameter(paramsObject, "range");
            var spotAngle = ParseOptionalFloatParameter(paramsObject, "spotAngle");
            var shadows = ParseOptionalEnumParameter<LightShadows>(paramsObject, "shadows");

            if (!enabled.HasValue &&
                !type.HasValue &&
                !color.HasValue &&
                !intensity.HasValue &&
                !range.HasValue &&
                !spotAngle.HasValue &&
                !shadows.HasValue)
            {
                throw new ArgumentException(
                    "At least one light setting must be provided: enabled, type, color, intensity, range, spotAngle, or shadows.");
            }

            if (intensity.HasValue && intensity.Value < 0f)
            {
                throw new ArgumentException("Parameter 'intensity' must be greater than or equal to 0.");
            }

            if (range.HasValue && range.Value <= 0f)
            {
                throw new ArgumentException("Parameter 'range' must be greater than 0.");
            }

            if (spotAngle.HasValue)
            {
                if (spotAngle.Value <= 0f || spotAngle.Value >= 180f)
                {
                    throw new ArgumentException("Parameter 'spotAngle' must be greater than 0 and less than 180 degrees.");
                }

                var effectiveType = type ?? light.type;
                if (effectiveType != LightType.Spot)
                {
                    throw new ArgumentException("Parameter 'spotAngle' is only valid for Spot lights.");
                }
            }

            Undo.RecordObject(light, "UnityMCP Set Light Settings");

            if (enabled.HasValue)
            {
                light.enabled = enabled.Value;
            }

            if (type.HasValue)
            {
                light.type = type.Value;
            }

            if (color.HasValue)
            {
                light.color = color.Value;
            }

            if (intensity.HasValue)
            {
                light.intensity = intensity.Value;
            }

            if (range.HasValue)
            {
                light.range = range.Value;
            }

            if (spotAngle.HasValue)
            {
                light.spotAngle = spotAngle.Value;
            }

            if (shadows.HasValue)
            {
                light.shadows = shadows.Value;
            }

            EditorUtility.SetDirty(light);

            var result = new
            {
                target = CreateObjectSummary(light.gameObject),
                component = CreateComponentSummary(light),
                settings = CreateLightSettingsSnapshot(light),
                applied = new
                {
                    enabled = enabled.HasValue,
                    type = type.HasValue,
                    color = color.HasValue,
                    intensity = intensity.HasValue,
                    range = range.HasValue,
                    spotAngle = spotAngle.HasValue,
                    shadows = shadows.HasValue
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }
    }
}
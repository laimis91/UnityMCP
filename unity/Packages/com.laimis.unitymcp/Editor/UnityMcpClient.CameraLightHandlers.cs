#nullable enable

using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMcp.Editor
{
    internal sealed partial class UnityMcpClient
    {
        private static string BuildGetCameraSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "camera.getSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var camera = ResolveComponentOfTypeTarget<Camera>(resolvedObject, "instanceId", "Camera");

            var result = new
            {
                target = CreateObjectSummary(camera.gameObject),
                component = CreateComponentSummary(camera),
                settings = CreateCameraSettingsSnapshot(camera)
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildSetCameraSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "camera.setSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var camera = ResolveComponentOfTypeTarget<Camera>(resolvedObject, "instanceId", "Camera");

            var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
            var orthographic = ParseOptionalBooleanValueParameter(paramsObject, "orthographic");
            var fieldOfView = ParseOptionalFloatParameter(paramsObject, "fieldOfView");
            var orthographicSize = ParseOptionalFloatParameter(paramsObject, "orthographicSize");
            var nearClipPlane = ParseOptionalFloatParameter(paramsObject, "nearClipPlane");
            var farClipPlane = ParseOptionalFloatParameter(paramsObject, "farClipPlane");
            var depth = ParseOptionalFloatParameter(paramsObject, "depth");
            var clearFlags = ParseOptionalEnumParameter<CameraClearFlags>(paramsObject, "clearFlags");
            var backgroundColor = ParseOptionalColorParameter(paramsObject, "backgroundColor");

            if (!enabled.HasValue &&
                !orthographic.HasValue &&
                !fieldOfView.HasValue &&
                !orthographicSize.HasValue &&
                !nearClipPlane.HasValue &&
                !farClipPlane.HasValue &&
                !depth.HasValue &&
                !clearFlags.HasValue &&
                !backgroundColor.HasValue)
            {
                throw new ArgumentException(
                    "At least one camera setting must be provided: enabled, orthographic, fieldOfView, orthographicSize, nearClipPlane, farClipPlane, depth, clearFlags, or backgroundColor.");
            }

            if (fieldOfView.HasValue && (fieldOfView.Value <= 0f || fieldOfView.Value >= 180f))
            {
                throw new ArgumentException("Parameter 'fieldOfView' must be greater than 0 and less than 180 degrees.");
            }

            if (orthographicSize.HasValue && orthographicSize.Value <= 0f)
            {
                throw new ArgumentException("Parameter 'orthographicSize' must be greater than 0.");
            }

            var effectiveNear = nearClipPlane ?? camera.nearClipPlane;
            var effectiveFar = farClipPlane ?? camera.farClipPlane;
            if (effectiveNear <= 0f)
            {
                throw new ArgumentException("Parameter 'nearClipPlane' must be greater than 0.");
            }

            if (effectiveFar <= effectiveNear)
            {
                throw new ArgumentException("Parameter 'farClipPlane' must be greater than 'nearClipPlane'.");
            }

            Undo.RecordObject(camera, "UnityMCP Set Camera Settings");

            if (enabled.HasValue)
            {
                camera.enabled = enabled.Value;
            }

            if (orthographic.HasValue)
            {
                camera.orthographic = orthographic.Value;
            }

            if (fieldOfView.HasValue)
            {
                camera.fieldOfView = fieldOfView.Value;
            }

            if (orthographicSize.HasValue)
            {
                camera.orthographicSize = orthographicSize.Value;
            }

            if (nearClipPlane.HasValue)
            {
                camera.nearClipPlane = nearClipPlane.Value;
            }

            if (farClipPlane.HasValue)
            {
                camera.farClipPlane = farClipPlane.Value;
            }

            if (depth.HasValue)
            {
                camera.depth = depth.Value;
            }

            if (clearFlags.HasValue)
            {
                camera.clearFlags = clearFlags.Value;
            }

            if (backgroundColor.HasValue)
            {
                camera.backgroundColor = backgroundColor.Value;
            }

            EditorUtility.SetDirty(camera);

            var result = new
            {
                target = CreateObjectSummary(camera.gameObject),
                component = CreateComponentSummary(camera),
                settings = CreateCameraSettingsSnapshot(camera),
                applied = new
                {
                    enabled = enabled.HasValue,
                    orthographic = orthographic.HasValue,
                    fieldOfView = fieldOfView.HasValue,
                    orthographicSize = orthographicSize.HasValue,
                    nearClipPlane = nearClipPlane.HasValue,
                    farClipPlane = farClipPlane.HasValue,
                    depth = depth.HasValue,
                    clearFlags = clearFlags.HasValue,
                    backgroundColor = backgroundColor.HasValue
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildGetCameraProjectionResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "camera.getProjection");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var camera = ResolveComponentOfTypeTarget<Camera>(resolvedObject, "instanceId", "Camera");

            var result = new
            {
                target = CreateObjectSummary(camera.gameObject),
                component = CreateComponentSummary(camera),
                projection = new
                {
                    orthographic = camera.orthographic,
                    orthographicSize = camera.orthographicSize,
                    fieldOfView = camera.fieldOfView,
                    nearClipPlane = camera.nearClipPlane,
                    farClipPlane = camera.farClipPlane,
                    aspect = camera.aspect
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildSetCameraProjectionResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "camera.setProjection");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var camera = ResolveComponentOfTypeTarget<Camera>(resolvedObject, "instanceId", "Camera");

            var orthographic = ParseOptionalBooleanValueParameter(paramsObject, "orthographic");
            var orthographicSize = ParseOptionalFloatParameter(paramsObject, "orthographicSize");
            var fieldOfView = ParseOptionalFloatParameter(paramsObject, "fieldOfView");
            var nearClipPlane = ParseOptionalFloatParameter(paramsObject, "nearClipPlane");
            var farClipPlane = ParseOptionalFloatParameter(paramsObject, "farClipPlane");

            if (!orthographic.HasValue &&
                !orthographicSize.HasValue &&
                !fieldOfView.HasValue &&
                !nearClipPlane.HasValue &&
                !farClipPlane.HasValue)
            {
                throw new ArgumentException(
                    "At least one projection setting must be provided: orthographic, orthographicSize, fieldOfView, nearClipPlane, or farClipPlane.");
            }

            if (fieldOfView.HasValue && (fieldOfView.Value <= 0f || fieldOfView.Value >= 180f))
            {
                throw new ArgumentException("Parameter 'fieldOfView' must be greater than 0 and less than 180 degrees.");
            }

            if (orthographicSize.HasValue && orthographicSize.Value <= 0f)
            {
                throw new ArgumentException("Parameter 'orthographicSize' must be greater than 0.");
            }

            var effectiveNear = nearClipPlane ?? camera.nearClipPlane;
            var effectiveFar = farClipPlane ?? camera.farClipPlane;
            if (effectiveNear <= 0f)
            {
                throw new ArgumentException("Parameter 'nearClipPlane' must be greater than 0.");
            }

            if (effectiveFar <= effectiveNear)
            {
                throw new ArgumentException("Parameter 'farClipPlane' must be greater than 'nearClipPlane'.");
            }

            Undo.RecordObject(camera, "UnityMCP Set Camera Projection");

            if (orthographic.HasValue)
            {
                camera.orthographic = orthographic.Value;
            }

            if (orthographicSize.HasValue)
            {
                camera.orthographicSize = orthographicSize.Value;
            }

            if (fieldOfView.HasValue)
            {
                camera.fieldOfView = fieldOfView.Value;
            }

            if (nearClipPlane.HasValue)
            {
                camera.nearClipPlane = nearClipPlane.Value;
            }

            if (farClipPlane.HasValue)
            {
                camera.farClipPlane = farClipPlane.Value;
            }

            EditorUtility.SetDirty(camera);

            var result = new
            {
                target = CreateObjectSummary(camera.gameObject),
                component = CreateComponentSummary(camera),
                projection = new
                {
                    orthographic = camera.orthographic,
                    orthographicSize = camera.orthographicSize,
                    fieldOfView = camera.fieldOfView,
                    nearClipPlane = camera.nearClipPlane,
                    farClipPlane = camera.farClipPlane,
                    aspect = camera.aspect
                },
                applied = new
                {
                    orthographic = orthographic.HasValue,
                    orthographicSize = orthographicSize.HasValue,
                    fieldOfView = fieldOfView.HasValue,
                    nearClipPlane = nearClipPlane.HasValue,
                    farClipPlane = farClipPlane.HasValue
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        private static string BuildGetLightSettingsResponse(JToken idToken, JObject root)
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

        private static string BuildSetLightSettingsResponse(JToken idToken, JObject root)
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

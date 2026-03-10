#nullable enable

using System;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using static UnityMcp.Editor.UnityMcpParameterHelpers;
using static UnityMcp.Editor.UnityMcpResolvers;
using static UnityMcp.Editor.UnityMcpSnapshotHelpers;

namespace UnityMcp.Editor
{

internal static class CameraHandler
{
    internal static string BuildGetCameraSettingsResponse(JToken idToken, JObject root)
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

    internal static string BuildSetCameraSettingsResponse(JToken idToken, JObject root)
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

    internal static string BuildGetCameraProjectionResponse(JToken idToken, JObject root)
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

    internal static string BuildSetCameraProjectionResponse(JToken idToken, JObject root)
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
}

}
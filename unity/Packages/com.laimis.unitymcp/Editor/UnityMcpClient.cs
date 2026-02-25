#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMcp.Editor
{

internal sealed class UnityMcpClient : IDisposable
{
    private static readonly object Sync = new();

    private static UnityMcpClient? _instance;

    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private CancellationTokenSource? _lifetimeCts;
    private Task? _connectionLoopTask;
    private ClientWebSocket? _socket;
    private Uri? _configuredServerUri;

    public static UnityMcpClient Instance
    {
        get
        {
            lock (Sync)
            {
                return _instance ??= new UnityMcpClient();
            }
        }
    }

    public bool IsRunning => _connectionLoopTask is { IsCompleted: false };

    public void Start()
    {
        if (IsRunning)
        {
            return;
        }

        UnityMcpConsoleLogBuffer.EnsureInitialized();

        if (!UnityMcpSettings.TryGetServerUri(out var serverUri, out var serverUriError))
        {
            Debug.LogWarning($"[UnityMCP] Invalid server URI configuration: {serverUriError}");
            return;
        }

        _configuredServerUri = serverUri;
        _lifetimeCts = new CancellationTokenSource();
        _connectionLoopTask = Task.Run(() => ConnectionLoopAsync(_lifetimeCts.Token));
    }

    public void Stop()
    {
        try
        {
            _lifetimeCts?.Cancel();
        }
        catch
        {
            // Ignore cancellation races during domain reload/editor shutdown.
        }
    }

    public void Dispose()
    {
        Stop();
        _socket?.Dispose();
        _sendLock.Dispose();
        _lifetimeCts?.Dispose();
    }

    private async Task ConnectionLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            ClientWebSocket? socket = null;
            try
            {
                var serverUri = _configuredServerUri;
                if (serverUri == null)
                {
                    Debug.LogWarning("[UnityMCP] Server URI is not configured. Use Tools/Unity MCP/Settings.");
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                    continue;
                }

                socket = new ClientWebSocket();
                _socket = socket;

                await socket.ConnectAsync(serverUri, cancellationToken);
                Debug.Log($"[UnityMCP] Connected to {serverUri}.");

                await ReceiveLoopAsync(socket, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[UnityMCP] Connection loop error: {ex.Message}");
            }
            finally
            {
                try
                {
                    socket?.Dispose();
                }
                catch
                {
                    // Ignore cleanup failures.
                }

                if (ReferenceEquals(_socket, socket))
                {
                    _socket = null;
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var message = await ReceiveTextAsync(socket, cancellationToken);
            if (message == null)
            {
                break;
            }

            UnityMcpMainThreadQueue.Enqueue(() => HandleMessageOnMainThread(message));
        }
    }

    private void HandleMessageOnMainThread(string message)
    {
        if (!UnityMcpProtocol.TryParse(message, out var document, out var parseError))
        {
            Debug.LogWarning($"[UnityMCP] Ignoring invalid JSON-RPC payload from server: {parseError}");
            return;
        }

        var root = document;

        if (!UnityMcpProtocol.TryGetId(root, out var idToken))
        {
            Debug.LogWarning("[UnityMCP] Ignoring JSON-RPC request without a valid id.");
            return;
        }

        if (!UnityMcpProtocol.TryGetMethod(root, out var method))
        {
            _ = SendAsync(UnityMcpProtocol.CreateError(idToken, -32600, "Missing JSON-RPC method."));
            return;
        }

        try
        {
            string response = method switch
            {
                "ping" => BuildPingResponse(idToken),
                "editor.getPlayModeState" => BuildPlayModeStateResponse(idToken),
                "editor.getConsoleLogs" => BuildGetConsoleLogsResponse(idToken, root),
                "editor.consoleTail" => BuildConsoleTailResponse(idToken, root),
                "editor.enterPlayMode" => BuildSetPlayModeResponse(idToken, shouldPlay: true),
                "editor.exitPlayMode" => BuildSetPlayModeResponse(idToken, shouldPlay: false),
                "scene.getActiveScene" => BuildGetActiveSceneResponse(idToken),
                "scene.listOpenScenes" => BuildListOpenScenesResponse(idToken),
                "scene.getSelection" => BuildGetSelectionResponse(idToken),
                "scene.selectObject" => BuildSelectObjectResponse(idToken, root),
                "scene.selectByPath" => BuildSelectByPathResponse(idToken, root),
                "scene.findByPath" => BuildFindByPathResponse(idToken, root),
                "camera.getSettings" => BuildGetCameraSettingsResponse(idToken, root),
                "camera.setSettings" => BuildSetCameraSettingsResponse(idToken, root),
                "light.getSettings" => BuildGetLightSettingsResponse(idToken, root),
                "light.setSettings" => BuildSetLightSettingsResponse(idToken, root),
                "rigidbody.getSettings" => BuildGetRigidbodySettingsResponse(idToken, root),
                "rigidbody.setSettings" => BuildSetRigidbodySettingsResponse(idToken, root),
                "collider.getSettings" => BuildGetColliderSettingsResponse(idToken, root),
                "collider.setSettings" => BuildSetColliderSettingsResponse(idToken, root),
                "scene.getComponents" => BuildGetComponentsResponse(idToken, root),
                "scene.destroyObject" => BuildDestroyObjectResponse(idToken, root),
                "scene.getComponentProperties" => BuildGetComponentPropertiesResponse(idToken, root),
                "scene.setComponentProperties" => BuildSetComponentPropertiesResponse(idToken, root),
                "scene.setTransform" => BuildSetTransformResponse(idToken, root),
                "scene.addComponent" => BuildAddComponentResponse(idToken, root),
                "scene.setSelection" => BuildSetSelectionResponse(idToken, root),
                "scene.pingObject" => BuildPingObjectResponse(idToken, root),
                "scene.frameSelection" => BuildFrameSelectionResponse(idToken),
                "scene.frameObject" => BuildFrameObjectResponse(idToken, root),
                "scene.createGameObject" => BuildCreateGameObjectResponse(idToken, root),
                "scene.findByTag" => BuildFindByTagResponse(idToken, root),
                "assets.find" => BuildFindAssetsResponse(idToken, root),
                "assets.import" => BuildImportAssetResponse(idToken, root),
                "assets.ping" => BuildPingAssetResponse(idToken, root),
                "assets.reveal" => BuildRevealAssetResponse(idToken, root),
                _ => UnityMcpProtocol.CreateError(idToken, -32601, $"Method '{method}' is not supported by UnityMCP MVP.")
            };

            _ = SendAsync(response);
        }
        catch (ArgumentException ex)
        {
            _ = SendAsync(UnityMcpProtocol.CreateError(idToken, -32602, ex.Message));
        }
        catch (Exception ex)
        {
            _ = SendAsync(UnityMcpProtocol.CreateError(idToken, -32603, ex.Message));
        }
    }

    private static string BuildPingResponse(JToken idToken)
    {
        var result = new
        {
            ok = true,
            source = "unity",
            unityVersion = Application.unityVersion
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildPlayModeStateResponse(JToken idToken)
    {
        return UnityMcpProtocol.CreateResult(idToken, BuildEditorStateResult());
    }

    private static string BuildGetConsoleLogsResponse(JToken idToken, JObject root)
    {
        ParseConsoleQueryOptions(
            root,
            methodName: "editor.getConsoleLogs",
            defaultMaxResults: 100,
            defaultIncludeStackTrace: false,
            requireAfterSequence: false,
            out var maxResults,
            out var includeStackTrace,
            out _,
            out var levels,
            out var contains);

        var queryResult = UnityMcpConsoleLogBuffer.GetSnapshot(maxResults, includeStackTrace, levels, contains);
        return UnityMcpProtocol.CreateResult(idToken, CreateConsoleQueryResultPayload(queryResult, levels, contains));
    }

    private static string BuildConsoleTailResponse(JToken idToken, JObject root)
    {
        ParseConsoleQueryOptions(
            root,
            methodName: "editor.consoleTail",
            defaultMaxResults: 100,
            defaultIncludeStackTrace: false,
            requireAfterSequence: true,
            out var maxResults,
            out var includeStackTrace,
            out var afterSequence,
            out var levels,
            out var contains);

        var queryResult = UnityMcpConsoleLogBuffer.GetTail(afterSequence, maxResults, includeStackTrace, levels, contains);
        return UnityMcpProtocol.CreateResult(idToken, CreateConsoleQueryResultPayload(queryResult, levels, contains));
    }

    private static string BuildSetPlayModeResponse(JToken idToken, bool shouldPlay)
    {
        var changed = EditorApplication.isPlaying != shouldPlay;
        if (changed)
        {
            EditorApplication.isPlaying = shouldPlay;
        }

        var state = BuildEditorStateResult();
        var result = new
        {
            isPlaying = state.isPlaying,
            isPaused = state.isPaused,
            isCompiling = state.isCompiling,
            isPlayingOrWillChangePlaymode = state.isPlayingOrWillChangePlaymode,
            requestedState = shouldPlay ? "playing" : "editing",
            changed
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetActiveSceneResponse(JToken idToken)
    {
        var activeScene = SceneManager.GetActiveScene();
        var result = CreateSceneSummary(activeScene, isActive: true);
        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildListOpenScenesResponse(JToken idToken)
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

    private static string BuildGetRigidbodySettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "rigidbody.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var rigidbody = ResolveComponentOfTypeTarget<Rigidbody>(resolvedObject, "instanceId", "Rigidbody");

        var result = new
        {
            target = CreateObjectSummary(rigidbody.gameObject),
            component = CreateComponentSummary(rigidbody),
            settings = CreateRigidbodySettingsSnapshot(rigidbody)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetRigidbodySettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "rigidbody.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var rigidbody = ResolveComponentOfTypeTarget<Rigidbody>(resolvedObject, "instanceId", "Rigidbody");

        var mass = ParseOptionalFloatParameter(paramsObject, "mass");
        var useGravity = ParseOptionalBooleanValueParameter(paramsObject, "useGravity");
        var isKinematic = ParseOptionalBooleanValueParameter(paramsObject, "isKinematic");
        var detectCollisions = ParseOptionalBooleanValueParameter(paramsObject, "detectCollisions");
        var constraints = ParseOptionalEnumParameter<RigidbodyConstraints>(paramsObject, "constraints");
        var interpolation = ParseOptionalEnumParameter<RigidbodyInterpolation>(paramsObject, "interpolation");
        var collisionDetectionMode = ParseOptionalEnumParameter<CollisionDetectionMode>(paramsObject, "collisionDetectionMode");

        if (!mass.HasValue &&
            !useGravity.HasValue &&
            !isKinematic.HasValue &&
            !detectCollisions.HasValue &&
            !constraints.HasValue &&
            !interpolation.HasValue &&
            !collisionDetectionMode.HasValue)
        {
            throw new ArgumentException(
                "At least one rigidbody setting must be provided: mass, useGravity, isKinematic, detectCollisions, constraints, interpolation, or collisionDetectionMode.");
        }

        if (mass.HasValue && mass.Value <= 0f)
        {
            throw new ArgumentException("Parameter 'mass' must be greater than 0.");
        }

        Undo.RecordObject(rigidbody, "UnityMCP Set Rigidbody Settings");

        if (mass.HasValue)
        {
            rigidbody.mass = mass.Value;
        }

        if (useGravity.HasValue)
        {
            rigidbody.useGravity = useGravity.Value;
        }

        if (isKinematic.HasValue)
        {
            rigidbody.isKinematic = isKinematic.Value;
        }

        if (detectCollisions.HasValue)
        {
            rigidbody.detectCollisions = detectCollisions.Value;
        }

        if (constraints.HasValue)
        {
            rigidbody.constraints = constraints.Value;
        }

        if (interpolation.HasValue)
        {
            rigidbody.interpolation = interpolation.Value;
        }

        if (collisionDetectionMode.HasValue)
        {
            rigidbody.collisionDetectionMode = collisionDetectionMode.Value;
        }

        EditorUtility.SetDirty(rigidbody);

        var result = new
        {
            target = CreateObjectSummary(rigidbody.gameObject),
            component = CreateComponentSummary(rigidbody),
            settings = CreateRigidbodySettingsSnapshot(rigidbody),
            applied = new
            {
                mass = mass.HasValue,
                useGravity = useGravity.HasValue,
                isKinematic = isKinematic.HasValue,
                detectCollisions = detectCollisions.HasValue,
                constraints = constraints.HasValue,
                interpolation = interpolation.HasValue,
                collisionDetectionMode = collisionDetectionMode.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetColliderSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "collider.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<Collider>(resolvedObject, "instanceId", "Collider");

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateColliderSettingsSnapshot(collider)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetColliderSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "collider.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<Collider>(resolvedObject, "instanceId", "Collider");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var isTrigger = ParseOptionalBooleanValueParameter(paramsObject, "isTrigger");
        var contactOffset = ParseOptionalFloatParameter(paramsObject, "contactOffset");
        var center = ParseOptionalVector3Parameter(paramsObject, "center");
        var size = ParseOptionalVector3Parameter(paramsObject, "size");

        if (!enabled.HasValue &&
            !isTrigger.HasValue &&
            !contactOffset.HasValue &&
            !center.HasValue &&
            !size.HasValue)
        {
            throw new ArgumentException(
                "At least one collider setting must be provided: enabled, isTrigger, contactOffset, center, or size.");
        }

        if (contactOffset.HasValue && contactOffset.Value < 0f)
        {
            throw new ArgumentException("Parameter 'contactOffset' must be greater than or equal to 0.");
        }

        if (size.HasValue &&
            (size.Value.x <= 0f || size.Value.y <= 0f || size.Value.z <= 0f))
        {
            throw new ArgumentException("Parameter 'size' must contain positive values for all BoxCollider axes.");
        }

        var boxCollider = collider as BoxCollider;
        if ((center.HasValue || size.HasValue) && boxCollider == null)
        {
            throw new ArgumentException("Parameters 'center' and 'size' are only supported for BoxCollider in the MVP.");
        }

        Undo.RecordObject(collider, "UnityMCP Set Collider Settings");

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

        if (boxCollider != null)
        {
            if (center.HasValue)
            {
                boxCollider.center = center.Value;
            }

            if (size.HasValue)
            {
                boxCollider.size = size.Value;
            }
        }

        EditorUtility.SetDirty(collider);

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateColliderSettingsSnapshot(collider),
            applied = new
            {
                enabled = enabled.HasValue,
                isTrigger = isTrigger.HasValue,
                contactOffset = contactOffset.HasValue,
                center = center.HasValue,
                size = size.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

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
            selectionPreserved,
            sceneViewAvailable,
            hasSceneTarget,
            instanceId,
            target = CreateObjectSummary(targetObject)
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

    private static string BuildImportAssetResponse(JToken idToken, JObject root)
    {
        if (!root.TryGetValue("params", out var paramsToken) || paramsToken is not JObject paramsObject)
        {
            throw new ArgumentException("Method 'assets.import' expects params to be an object.");
        }

        if (!paramsObject.TryGetValue("assetPath", out var assetPathToken) || assetPathToken.Type != JTokenType.String)
        {
            throw new ArgumentException("Parameter 'assetPath' is required and must be a string.");
        }

        var rawAssetPath = assetPathToken.Value<string>();
        var assetPath = NormalizeAndValidateAssetPath(rawAssetPath);

        var absoluteAssetPath = GetAbsoluteProjectPath(assetPath);
        var isFolder = Directory.Exists(absoluteAssetPath);
        var isFile = File.Exists(absoluteAssetPath);
        if (!isFolder && !isFile)
        {
            throw new ArgumentException($"Asset path '{assetPath}' does not exist in the Unity project.");
        }

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh(ImportAssetOptions.Default);

        var guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrWhiteSpace(guid))
        {
            throw new InvalidOperationException($"Unity did not return a GUID for imported asset '{assetPath}'.");
        }

        var mainAssetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
        var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);

        var result = new
        {
            assetPath,
            guid,
            isFolder,
            exists = true,
            mainAssetType = mainAssetType?.FullName,
            mainAssetName = mainAsset != null ? mainAsset.name : null,
            imported = true
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildPingAssetResponse(JToken idToken, JObject root)
    {
        var (assetPath, guid, targetObject, isFolder) = ResolveAssetNavigationTarget(root, "assets.ping");
        EditorGUIUtility.PingObject(targetObject);

        var result = new
        {
            pinged = true,
            assetPath,
            guid,
            isFolder,
            target = CreateObjectSummary(targetObject)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildRevealAssetResponse(JToken idToken, JObject root)
    {
        var (assetPath, guid, targetObject, isFolder) = ResolveAssetNavigationTarget(root, "assets.reveal");

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = targetObject;
        EditorGUIUtility.PingObject(targetObject);

        var result = new
        {
            revealed = true,
            focusedProjectWindow = true,
            assetPath,
            guid,
            isFolder,
            target = CreateObjectSummary(targetObject)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildFindAssetsResponse(JToken idToken, JObject root)
    {
        if (!root.TryGetValue("params", out var paramsToken) || paramsToken is not JObject paramsObject)
        {
            throw new ArgumentException("Method 'assets.find' expects params to be an object.");
        }

        if (!paramsObject.TryGetValue("query", out var queryToken) || queryToken.Type != JTokenType.String)
        {
            throw new ArgumentException("Parameter 'query' is required and must be a string.");
        }

        var query = queryToken.Value<string>();
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Parameter 'query' cannot be empty.");
        }

        var maxResults = 100;
        if (paramsObject.TryGetValue("maxResults", out var maxResultsToken))
        {
            if (maxResultsToken.Type != JTokenType.Integer)
            {
                throw new ArgumentException("Parameter 'maxResults' must be an integer.");
            }

            var parsedMaxResults = maxResultsToken.Value<int?>();
            if (!parsedMaxResults.HasValue)
            {
                throw new ArgumentException("Parameter 'maxResults' must be an integer.");
            }

            if (parsedMaxResults.Value < 1 || parsedMaxResults.Value > 500)
            {
                throw new ArgumentException("Parameter 'maxResults' must be between 1 and 500.");
            }

            maxResults = parsedMaxResults.Value;
        }

        var searchInFolders = ParseOptionalStringArrayParameter(paramsObject, "searchInFolders");
        if (searchInFolders != null)
        {
            for (var index = 0; index < searchInFolders.Count; index++)
            {
                var normalizedFolder = NormalizeAndValidateAssetPath(searchInFolders[index]);
                if (!AssetDatabase.IsValidFolder(normalizedFolder))
                {
                    throw new ArgumentException($"Search folder '{normalizedFolder}' does not exist or is not a valid Unity folder.");
                }

                searchInFolders[index] = normalizedFolder;
            }
        }

        var types = ParseOptionalStringArrayParameter(paramsObject, "types");
        var labels = ParseOptionalStringArrayParameter(paramsObject, "labels");
        var effectiveQuery = BuildEffectiveAssetsFindQuery(query!, types, labels);

        var guids = searchInFolders is { Count: > 0 }
            ? AssetDatabase.FindAssets(effectiveQuery, searchInFolders.ToArray())
            : AssetDatabase.FindAssets(effectiveQuery);
        var takeCount = Math.Min(maxResults, guids.Length);
        var items = new List<object>(takeCount);
        for (var index = 0; index < takeCount; index++)
        {
            var guid = guids[index];
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var mainAssetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);

            items.Add(new
            {
                guid,
                assetPath,
                isFolder = AssetDatabase.IsValidFolder(assetPath),
                mainAssetType = mainAssetType != null ? mainAssetType.FullName : null,
                mainAssetName = mainAsset != null ? mainAsset.name : null
            });
        }

        var result = new
        {
            query,
            effectiveQuery,
            searchInFolders,
            types,
            labels,
            totalMatched = guids.Length,
            returnedCount = items.Count,
            maxResults,
            truncated = guids.Length > takeCount,
            items
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static List<string>? ParseOptionalStringArrayParameter(JObject paramsObject, string parameterName)
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

    private static string BuildEffectiveAssetsFindQuery(string query, List<string>? types, List<string>? labels)
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

    private static object CreateConsoleQueryResultPayload(
        UnityMcpConsoleLogBuffer.ConsoleLogQueryResult queryResult,
        IReadOnlyList<string>? levels,
        string? contains)
    {
        return new
        {
            bufferCapacity = queryResult.BufferCapacity,
            totalBuffered = queryResult.TotalBuffered,
            bufferStartSequence = queryResult.BufferStartSequence,
            latestSequence = queryResult.LatestSequence,
            afterSequence = queryResult.AfterSequence,
            nextAfterSequence = queryResult.NextAfterSequence,
            cursorBehindBuffer = queryResult.CursorBehindBuffer,
            returnedCount = queryResult.Items.Count,
            truncated = queryResult.Truncated,
            includeStackTrace = queryResult.IncludeStackTrace,
            levels = levels,
            contains,
            items = queryResult.Items
        };
    }

    private static void ParseConsoleQueryOptions(
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

            items.Add(CreateObjectSummary(selectedObject));
        }

        var activeObject = Selection.activeObject;
        object? activeObjectSummary = null;
        if (activeObject != null)
        {
            activeObjectSummary = CreateObjectSummary(activeObject);
        }

        var activeGameObject = Selection.activeGameObject;
        object? activeGameObjectSummary = null;
        if (activeGameObject != null)
        {
            activeGameObjectSummary = CreateObjectSummary(activeGameObject);
        }

        return new
        {
            count = items.Count,
            activeObject = activeObjectSummary,
            activeGameObject = activeGameObjectSummary,
            items
        };
    }

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

    private static object CreateRigidbodySettingsSnapshot(Rigidbody rigidbody)
    {
        return new
        {
            mass = rigidbody.mass,
            useGravity = rigidbody.useGravity,
            isKinematic = rigidbody.isKinematic,
            detectCollisions = rigidbody.detectCollisions,
            constraints = CreateEnumSummary(rigidbody.constraints),
            interpolation = CreateEnumSummary(rigidbody.interpolation),
            collisionDetectionMode = CreateEnumSummary(rigidbody.collisionDetectionMode)
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

    private static EditorStateSnapshot BuildEditorStateResult()
    {
        return new EditorStateSnapshot
        {
            isPlaying = EditorApplication.isPlaying,
            isPaused = EditorApplication.isPaused,
            isCompiling = EditorApplication.isCompiling,
            isPlayingOrWillChangePlaymode = EditorApplication.isPlayingOrWillChangePlaymode
        };
    }

    private sealed class EditorStateSnapshot
    {
        public bool isPlaying { get; set; }
        public bool isPaused { get; set; }
        public bool isCompiling { get; set; }
        public bool isPlayingOrWillChangePlaymode { get; set; }
    }

    private async Task SendAsync(string payload)
    {
        var socket = _socket;
        if (socket == null || socket.State != WebSocketState.Open)
        {
            return;
        }

        await _sendLock.WaitAsync();
        try
        {
            if (socket.State != WebSocketState.Open)
            {
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(payload);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UnityMCP] Send failed: {ex.Message}");
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private static async Task<string?> ReceiveTextAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var builder = new StringBuilder();

        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                continue;
            }

            builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            if (result.EndOfMessage)
            {
                return builder.ToString();
            }
        }

        return null;
    }
}
}

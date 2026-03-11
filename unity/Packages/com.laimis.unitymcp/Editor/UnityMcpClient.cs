#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Audio;
using System.IO;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UnityMcp.Editor
{

internal sealed partial class UnityMcpClient : IDisposable
{
    private static readonly object Sync = new();

    private static UnityMcpClient? _instance;

    private readonly object _lifecycleSync = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private CancellationTokenSource? _lifetimeCts;
    private Task? _connectionLoopTask;
    private ClientWebSocket? _socket;
    private Uri? _configuredServerUri;
    private int _runVersion;

    private enum ConnectedAnchorMode
    {
        Preserve,
        Auto,
        Zero,
        MatchAnchor
    }

    private enum PrefabOverrideScope
    {
        InstanceRoot,
        Object,
        Component
    }

    private readonly struct OptionalInstanceIdParameter
    {
        public OptionalInstanceIdParameter(bool isSpecified, int? value)
        {
            IsSpecified = isSpecified;
            Value = value;
        }

        public bool IsSpecified { get; }

        public int? Value { get; }

        public bool HasValue => Value.HasValue;
    }

    private readonly struct PrefabInstanceDetails
    {
        public PrefabInstanceDetails(
            GameObject targetGameObject,
            GameObject nearestPrefabInstanceRoot,
            GameObject outermostPrefabInstanceRoot,
            GameObject sourceAsset,
            string assetPath,
            string guid,
            string prefabInstanceStatus,
            string prefabAssetType)
        {
            TargetGameObject = targetGameObject;
            NearestPrefabInstanceRoot = nearestPrefabInstanceRoot;
            OutermostPrefabInstanceRoot = outermostPrefabInstanceRoot;
            SourceAsset = sourceAsset;
            AssetPath = assetPath;
            Guid = guid;
            PrefabInstanceStatus = prefabInstanceStatus;
            PrefabAssetType = prefabAssetType;
        }

        public GameObject TargetGameObject { get; }

        public GameObject NearestPrefabInstanceRoot { get; }

        public GameObject OutermostPrefabInstanceRoot { get; }

        public GameObject SourceAsset { get; }

        public string AssetPath { get; }

        public string Guid { get; }

        public string PrefabInstanceStatus { get; }

        public string PrefabAssetType { get; }

        public bool IsOutermostPrefabInstanceRoot => TargetGameObject == OutermostPrefabInstanceRoot;
    }

    private readonly struct SoftJointLimitUpdate
    {
        public SoftJointLimitUpdate(bool isSpecified, float? limit, float? bounciness, float? contactDistance)
        {
            IsSpecified = isSpecified;
            Limit = limit;
            Bounciness = bounciness;
            ContactDistance = contactDistance;
        }

        public bool IsSpecified { get; }

        public float? Limit { get; }

        public float? Bounciness { get; }

        public float? ContactDistance { get; }
    }

    private readonly struct SoftJointLimitSpringUpdate
    {
        public SoftJointLimitSpringUpdate(bool isSpecified, float? spring, float? damper)
        {
            IsSpecified = isSpecified;
            Spring = spring;
            Damper = damper;
        }

        public bool IsSpecified { get; }

        public float? Spring { get; }

        public float? Damper { get; }
    }

    private readonly struct JointDriveUpdate
    {
        public JointDriveUpdate(bool isSpecified, float? positionSpring, float? positionDamper, float? maximumForce)
        {
            IsSpecified = isSpecified;
            PositionSpring = positionSpring;
            PositionDamper = positionDamper;
            MaximumForce = maximumForce;
        }

        public bool IsSpecified { get; }

        public float? PositionSpring { get; }

        public float? PositionDamper { get; }

        public float? MaximumForce { get; }
    }

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
        UnityMcpConsoleLogBuffer.EnsureInitialized();

        if (!UnityMcpSettings.TryGetServerUri(out var serverUri, out var serverUriError))
        {
            Debug.LogWarning($"[UnityMCP] Invalid server URI configuration: {serverUriError}");
            return;
        }

        CancellationTokenSource? previousLifetime = null;
        ClientWebSocket? previousSocket = null;
        var shouldStart = false;
        var runVersion = 0;
        CancellationTokenSource? newLifetime = null;

        lock (_lifecycleSync)
        {
            if (_connectionLoopTask is { IsCompleted: false } &&
                _socket?.State == WebSocketState.Open &&
                Equals(_configuredServerUri, serverUri))
            {
                return;
            }

            previousLifetime = _lifetimeCts;
            previousSocket = _socket;

            _runVersion++;
            runVersion = _runVersion;
            _configuredServerUri = serverUri;
            _socket = null;

            newLifetime = new CancellationTokenSource();
            _lifetimeCts = newLifetime;
            _connectionLoopTask = Task.Run(() => ConnectionLoopAsync(runVersion, newLifetime.Token));
            shouldStart = true;
        }

        CancelLifetime(previousLifetime);
        DisposeSocket(previousSocket);

        if (!shouldStart)
        {
            newLifetime?.Dispose();
        }
    }

    public void Stop()
    {
        CancellationTokenSource? lifetimeToCancel;
        ClientWebSocket? socketToDispose;

        lock (_lifecycleSync)
        {
            _runVersion++;
            lifetimeToCancel = _lifetimeCts;
            socketToDispose = _socket;
            _lifetimeCts = null;
            _connectionLoopTask = null;
            _socket = null;
        }

        DisposeSocket(socketToDispose);

        try
        {
            lifetimeToCancel?.Cancel();
        }
        catch
        {
            // Ignore cancellation races during domain reload/editor shutdown.
        }
        finally
        {
            lifetimeToCancel?.Dispose();
        }
    }

    public void Dispose()
    {
        Stop();
        _socket?.Dispose();
        _sendLock.Dispose();
        _lifetimeCts?.Dispose();
    }

    private async Task ConnectionLoopAsync(int runVersion, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && IsCurrentRun(runVersion))
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
                if (!TryRegisterSocket(runVersion, socket))
                {
                    socket.Dispose();
                    break;
                }

                await socket.ConnectAsync(serverUri, cancellationToken);

                if (!IsCurrentRun(runVersion))
                {
                    break;
                }

                Debug.Log($"[UnityMCP] Connected to {serverUri}.");

                await ReceiveLoopAsync(socket, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (IsCurrentRun(runVersion) && !cancellationToken.IsCancellationRequested)
                {
                    Debug.LogWarning($"[UnityMCP] Connection loop error: {ex.Message}");
                }
            }
            finally
            {
                DisposeSocket(socket);
                ClearSocket(runVersion, socket);
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

    private bool IsCurrentRun(int runVersion)
    {
        lock (_lifecycleSync)
        {
            return _runVersion == runVersion;
        }
    }

    private bool TryRegisterSocket(int runVersion, ClientWebSocket socket)
    {
        lock (_lifecycleSync)
        {
            if (_runVersion != runVersion)
            {
                return false;
            }

            _socket = socket;
            return true;
        }
    }

    private void ClearSocket(int runVersion, ClientWebSocket? socket)
    {
        lock (_lifecycleSync)
        {
            if (_runVersion == runVersion && ReferenceEquals(_socket, socket))
            {
                _socket = null;
            }
        }
    }

    private static void CancelLifetime(CancellationTokenSource? lifetime)
    {
        if (lifetime == null)
        {
            return;
        }

        try
        {
            lifetime.Cancel();
        }
        catch
        {
            // Ignore cancellation races during restart.
        }
        finally
        {
            lifetime.Dispose();
        }
    }

    private static void DisposeSocket(ClientWebSocket? socket)
    {
        if (socket == null)
        {
            return;
        }

        try
        {
            socket.Abort();
        }
        catch
        {
            // Ignore socket abort failures during shutdown/restart.
        }

        try
        {
            socket.Dispose();
        }
        catch
        {
            // Ignore cleanup failures.
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
                "rigidbody2D.getSettings" => BuildGetRigidbody2DSettingsResponse(idToken, root),
                "rigidbody2D.setSettings" => BuildSetRigidbody2DSettingsResponse(idToken, root),
                "collider.getSettings" => BuildGetColliderSettingsResponse(idToken, root),
                "collider.setSettings" => BuildSetColliderSettingsResponse(idToken, root),
                "collider2D.getSettings" => BuildGetCollider2DSettingsResponse(idToken, root),
                "collider2D.setSettings" => BuildSetCollider2DSettingsResponse(idToken, root),
                "boxCollider.getSettings" => BuildGetBoxColliderSettingsResponse(idToken, root),
                "boxCollider.setSettings" => BuildSetBoxColliderSettingsResponse(idToken, root),
                "boxCollider2D.getSettings" => BuildGetBoxCollider2DSettingsResponse(idToken, root),
                "boxCollider2D.setSettings" => BuildSetBoxCollider2DSettingsResponse(idToken, root),
                "sphereCollider.getSettings" => BuildGetSphereColliderSettingsResponse(idToken, root),
                "sphereCollider.setSettings" => BuildSetSphereColliderSettingsResponse(idToken, root),
                "sphereCollider2D.getSettings" => BuildGetCircleCollider2DSettingsResponse(idToken, root),
                "sphereCollider2D.setSettings" => BuildSetCircleCollider2DSettingsResponse(idToken, root),
                "circleCollider2D.getSettings" => BuildGetCircleCollider2DSettingsResponse(idToken, root),
                "circleCollider2D.setSettings" => BuildSetCircleCollider2DSettingsResponse(idToken, root),
                "capsuleCollider.getSettings" => BuildGetCapsuleColliderSettingsResponse(idToken, root),
                "capsuleCollider.setSettings" => BuildSetCapsuleColliderSettingsResponse(idToken, root),
                "capsuleCollider2D.getSettings" => BuildGetCapsuleCollider2DSettingsResponse(idToken, root),
                "capsuleCollider2D.setSettings" => BuildSetCapsuleCollider2DSettingsResponse(idToken, root),
                "meshCollider.getSettings" => BuildGetMeshColliderSettingsResponse(idToken, root),
                "meshCollider.setSettings" => BuildSetMeshColliderSettingsResponse(idToken, root),
                "polygonCollider2D.getSettings" => BuildGetPolygonCollider2DSettingsResponse(idToken, root),
                "polygonCollider2D.setSettings" => BuildSetPolygonCollider2DSettingsResponse(idToken, root),
                "edgeCollider2D.getSettings" => BuildGetEdgeCollider2DSettingsResponse(idToken, root),
                "edgeCollider2D.setSettings" => BuildSetEdgeCollider2DSettingsResponse(idToken, root),
                "compositeCollider2D.getSettings" => BuildGetCompositeCollider2DSettingsResponse(idToken, root),
                "compositeCollider2D.setSettings" => BuildSetCompositeCollider2DSettingsResponse(idToken, root),
                "hingeJoint2D.getSettings" => BuildGetHingeJoint2DSettingsResponse(idToken, root),
                "hingeJoint2D.setSettings" => BuildSetHingeJoint2DSettingsResponse(idToken, root),
                "springJoint2D.getSettings" => BuildGetSpringJoint2DSettingsResponse(idToken, root),
                "springJoint2D.setSettings" => BuildSetSpringJoint2DSettingsResponse(idToken, root),
                "distanceJoint2D.getSettings" => BuildGetDistanceJoint2DSettingsResponse(idToken, root),
                "distanceJoint2D.setSettings" => BuildSetDistanceJoint2DSettingsResponse(idToken, root),
                "fixedJoint2D.getSettings" => BuildGetFixedJoint2DSettingsResponse(idToken, root),
                "fixedJoint2D.setSettings" => BuildSetFixedJoint2DSettingsResponse(idToken, root),
                "sliderJoint2D.getSettings" => BuildGetSliderJoint2DSettingsResponse(idToken, root),
                "sliderJoint2D.setSettings" => BuildSetSliderJoint2DSettingsResponse(idToken, root),
                "wheelJoint2D.getSettings" => BuildGetWheelJoint2DSettingsResponse(idToken, root),
                "wheelJoint2D.setSettings" => BuildSetWheelJoint2DSettingsResponse(idToken, root),
                "targetJoint2D.getSettings" => BuildGetTargetJoint2DSettingsResponse(idToken, root),
                "targetJoint2D.setSettings" => BuildSetTargetJoint2DSettingsResponse(idToken, root),
                "hingeJoint.getSettings" => BuildGetHingeJointSettingsResponse(idToken, root),
                "hingeJoint.setSettings" => BuildSetHingeJointSettingsResponse(idToken, root),
                "springJoint.getSettings" => BuildGetSpringJointSettingsResponse(idToken, root),
                "springJoint.setSettings" => BuildSetSpringJointSettingsResponse(idToken, root),
                "fixedJoint.getSettings" => BuildGetFixedJointSettingsResponse(idToken, root),
                "fixedJoint.setSettings" => BuildSetFixedJointSettingsResponse(idToken, root),
                "characterJoint.getSettings" => BuildGetCharacterJointSettingsResponse(idToken, root),
                "characterJoint.setSettings" => BuildSetCharacterJointSettingsResponse(idToken, root),
                "configurableJoint.getSettings" => BuildGetConfigurableJointSettingsResponse(idToken, root),
                "configurableJoint.setSettings" => BuildSetConfigurableJointSettingsResponse(idToken, root),
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
                "scene.setParent" => BuildSetParentResponse(idToken, root),
                "scene.duplicateObject" => BuildDuplicateObjectResponse(idToken, root),
                "scene.renameObject" => BuildRenameObjectResponse(idToken, root),
                "scene.setActive" => BuildSetActiveResponse(idToken, root),
                "prefab.instantiate" => BuildInstantiatePrefabResponse(idToken, root),
                "prefab.getSource" => BuildGetPrefabSourceResponse(idToken, root),
                "prefab.applyOverrides" => BuildApplyPrefabOverridesResponse(idToken, root),
                "prefab.revertOverrides" => BuildRevertPrefabOverridesResponse(idToken, root),
                "scene.findByTag" => BuildFindByTagResponse(idToken, root),
                "scene.getHierarchy" => BuildGetSceneHierarchyResponse(idToken, root),
                "assets.find" => BuildFindAssetsResponse(idToken, root),
                "assets.import" => BuildImportAssetResponse(idToken, root),
                "assets.ping" => BuildPingAssetResponse(idToken, root),
                "assets.reveal" => BuildRevealAssetResponse(idToken, root),
                // Project Settings
                "projectSettings.getPlayerSettings" => BuildGetPlayerSettingsResponse(idToken),
                "projectSettings.setPlayerSettings" => BuildSetPlayerSettingsResponse(idToken, root),
                "projectSettings.getQualitySettings" => BuildGetQualitySettingsResponse(idToken),
                "projectSettings.setQualitySettings" => BuildSetQualitySettingsResponse(idToken, root),
                "projectSettings.getPhysicsSettings" => BuildGetPhysicsSettingsResponse(idToken),
                "projectSettings.setPhysicsSettings" => BuildSetPhysicsSettingsResponse(idToken, root),
                // Physics2D Settings
                "physics2D.getSettings" => BuildGetPhysics2DSettingsResponse(idToken),
                "physics2D.setSettings" => BuildSetPhysics2DSettingsResponse(idToken, root),
                // Screenshot Capture
                "editor.captureSceneView" => BuildCaptureSceneViewResponse(idToken, root),
                "editor.captureGameView" => BuildCaptureGameViewResponse(idToken, root),
                // Editor utility
                "editor.clearConsole" => BuildClearConsoleResponse(idToken),
                "editor.pausePlayMode" => BuildPausePlayModeResponse(idToken, root),
                "editor.undo" => BuildUndoResponse(idToken),
                "editor.redo" => BuildRedoResponse(idToken),
                "editor.getTags" => BuildGetTagsResponse(idToken),
                "editor.getLayers" => BuildGetLayersResponse(idToken),
                // Scene tag / layer
                "scene.setTag" => BuildSetTagResponse(idToken, root),
                "scene.setLayer" => BuildSetLayerResponse(idToken, root),
                // Scene management
                "scene.save" => BuildSaveSceneResponse(idToken, root),
                "scene.openScene" => BuildOpenSceneResponse(idToken, root),
                "scene.newScene" => BuildNewSceneResponse(idToken, root),
                "scene.closeScene" => BuildCloseSceneResponse(idToken, root),
                "scene.setActiveScene" => BuildSetActiveSceneResponse(idToken, root),
                // Asset creation / management
                "assets.createFolder" => BuildCreateFolderResponse(idToken, root),
                "assets.createScript" => BuildCreateScriptResponse(idToken, root),
                "assets.createMaterial" => BuildCreateMaterialResponse(idToken, root),
                "assets.createPrefab" => BuildCreatePrefabResponse(idToken, root),
                "assets.delete" => BuildDeleteAssetResponse(idToken, root),
                "assets.move" => BuildMoveAssetResponse(idToken, root),
                // Animator
                "animator.getSettings" => BuildGetAnimatorSettingsResponse(idToken, root),
                "animator.setSettings" => BuildSetAnimatorSettingsResponse(idToken, root),
                "animator.getParameters" => BuildGetAnimatorParametersResponse(idToken, root),
                "animator.setParameter" => BuildSetAnimatorParameterResponse(idToken, root),
                // MeshRenderer
                "meshRenderer.getSettings" => BuildGetMeshRendererSettingsResponse(idToken, root),
                "meshRenderer.setSettings" => BuildSetMeshRendererSettingsResponse(idToken, root),
                // AudioSource
                "audioSource.getSettings" => BuildGetAudioSourceSettingsResponse(idToken, root),
                "audioSource.setSettings" => BuildSetAudioSourceSettingsResponse(idToken, root),
                // CharacterController
                "characterController.getSettings" => BuildGetCharacterControllerSettingsResponse(idToken, root),
                "characterController.setSettings" => BuildSetCharacterControllerSettingsResponse(idToken, root),
                // ParticleSystem
                "particleSystem.getSettings" => BuildGetParticleSystemSettingsResponse(idToken, root),
                "particleSystem.setSettings" => BuildSetParticleSystemSettingsResponse(idToken, root),
                "particleSystem.play" => BuildParticleSystemPlayResponse(idToken, root),
                "particleSystem.stop" => BuildParticleSystemStopResponse(idToken, root),
                // NavMeshAgent
                "navMeshAgent.getSettings" => BuildGetNavMeshAgentSettingsResponse(idToken, root),
                "navMeshAgent.setSettings" => BuildSetNavMeshAgentSettingsResponse(idToken, root),
                // NavMeshObstacle
                "navMeshObstacle.getSettings" => BuildGetNavMeshObstacleSettingsResponse(idToken, root),
                "navMeshObstacle.setSettings" => BuildSetNavMeshObstacleSettingsResponse(idToken, root),
                // RectTransform
                "rectTransform.getSettings" => BuildGetRectTransformSettingsResponse(idToken, root),
                "rectTransform.setSettings" => BuildSetRectTransformSettingsResponse(idToken, root),
                // Canvas
                "canvas.getSettings" => BuildGetCanvasSettingsResponse(idToken, root),
                "canvas.setSettings" => BuildSetCanvasSettingsResponse(idToken, root),
                // SkinnedMeshRenderer
                "skinnedMeshRenderer.getSettings" => BuildGetSkinnedMeshRendererSettingsResponse(idToken, root),
                "skinnedMeshRenderer.setSettings" => BuildSetSkinnedMeshRendererSettingsResponse(idToken, root),
                // ScriptableObject
                "assets.createScriptableObject" => BuildCreateScriptableObjectResponse(idToken, root),
                // Batch 3: NavMesh
                "navMesh.bake" => BuildNavMeshBakeResponse(idToken),
                // Batch 3: Terrain
                "terrain.getSettings" => BuildGetTerrainSettingsResponse(idToken, root),
                "terrain.setSettings" => BuildSetTerrainSettingsResponse(idToken, root),
                // Batch 3: Build Pipeline
                "build.getSettings" => BuildGetBuildSettingsResponse(idToken),
                "build.setSettings" => BuildSetBuildSettingsResponse(idToken, root),
                "build.build" => BuildBuildResponse(idToken, root),
                // Batch 3: Tags & Layers Management
                "editor.addTag" => BuildAddTagResponse(idToken, root),
                "editor.removeTag" => BuildRemoveTagResponse(idToken, root),
                "editor.addLayer" => BuildAddLayerResponse(idToken, root),
                "editor.removeLayer" => BuildRemoveLayerResponse(idToken, root),
                // Batch 3: Selection Utilities
                "scene.getSelectionDetails" => BuildGetSelectionDetailsResponse(idToken),
                "scene.selectByName" => BuildSelectByNameResponse(idToken, root),
                // Batch 3: Undo History
                "editor.getUndoHistory" => BuildGetUndoHistoryResponse(idToken),
                // Batch 4: Camera Projection
                "camera.getProjection" => BuildGetCameraProjectionResponse(idToken, root),
                "camera.setProjection" => BuildSetCameraProjectionResponse(idToken, root),
                // Batch 4: SpriteRenderer
                "spriteRenderer.getSettings" => BuildGetSpriteRendererSettingsResponse(idToken, root),
                "spriteRenderer.setSettings" => BuildSetSpriteRendererSettingsResponse(idToken, root),
                // Batch 4: LineRenderer
                "lineRenderer.getSettings" => BuildGetLineRendererSettingsResponse(idToken, root),
                "lineRenderer.setSettings" => BuildSetLineRendererSettingsResponse(idToken, root),
                // Batch 4: LODGroup
                "lodGroup.getSettings" => BuildGetLODGroupSettingsResponse(idToken, root),
                "lodGroup.setSettings" => BuildSetLODGroupSettingsResponse(idToken, root),
                // Batch 4: CanvasGroup
                "canvasGroup.getSettings" => BuildGetCanvasGroupSettingsResponse(idToken, root),
                "canvasGroup.setSettings" => BuildSetCanvasGroupSettingsResponse(idToken, root),
                // Batch 4: Editor Recompile
                "editor.recompileScripts" => BuildRecompileScriptsResponse(idToken),
                // Batch 4: Scene Instantiate Prefab
                "scene.instantiatePrefab" => BuildSceneInstantiatePrefabResponse(idToken, root),
                // Batch 5: Physics Queries
                "physics.raycast" => BuildPhysicsRaycastResponse(idToken, root),
                "physics.overlapSphere" => BuildPhysicsOverlapSphereResponse(idToken, root),
                // Batch 5: Time
                "time.getSettings" => BuildGetTimeSettingsResponse(idToken),
                "time.setSettings" => BuildSetTimeSettingsResponse(idToken, root),
                // Batch 5: Joint (base 3D)
                "joint.getSettings" => BuildGetJointSettingsResponse(idToken, root),
                "joint.setSettings" => BuildSetJointSettingsResponse(idToken, root),
                // Batch 5: Renderer
                "renderer.getMaterials" => BuildGetRendererMaterialsResponse(idToken, root),
                "renderer.setMaterial" => BuildSetRendererMaterialResponse(idToken, root),

                "audio.getSourceSettings" => BuildAudioSourceGetSettingsResponse(idToken, root),
                "audio.setSourceSettings" => BuildAudioSourceSetSettingsResponse(idToken, root),
                "audio.play"             => BuildAudioPlayResponse(idToken, root),
                "audio.stop"             => BuildAudioStopResponse(idToken, root),
                "audio.pause"            => BuildAudioPauseResponse(idToken, root),
                "audio.unpause"          => BuildAudioUnpauseResponse(idToken, root),
                "audio.getIsPlaying"     => BuildGetAudioIsPlayingResponse(idToken, root),
                "audio.getMixerSettings" => BuildGetAudioMixerSettingsResponse(idToken, root),
                "audio.setMixerParameter" => BuildSetAudioMixerParameterResponse(idToken, root),
                "audio.getListenerSettings" => BuildGetAudioListenerSettingsResponse(idToken, root),
                "audio.setListenerSettings" => BuildSetAudioListenerSettingsResponse(idToken, root),
                // Batch 7: Test Runner
                "testRunner.listTests" => BuildListTestsResponse(idToken, root),
                "testRunner.run" => BuildRunTestsResponse(idToken, root),
                "testRunner.getResults" => BuildGetTestResultsResponse(idToken),
                "testRunner.cancel" => BuildCancelTestRunResponse(idToken),
                // Batch 8: Material/Shader Properties
                "material.getProperties" => BuildGetMaterialPropertiesResponse(idToken, root),
                "material.getProperty" => BuildGetMaterialPropertyResponse(idToken, root),
                "material.setProperty" => BuildSetMaterialPropertyResponse(idToken, root),
                "material.getKeywords" => BuildGetMaterialKeywordsResponse(idToken, root),
                "material.setKeyword" => BuildSetMaterialKeywordResponse(idToken, root),
                "material.getShader" => BuildGetMaterialShaderResponse(idToken, root),
                "material.setShader" => BuildSetMaterialShaderResponse(idToken, root),
                "material.getRenderQueue" => BuildGetMaterialRenderQueueResponse(idToken, root),
                "material.setRenderQueue" => BuildSetMaterialRenderQueueResponse(idToken, root),
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

    private static string BuildInstantiatePrefabResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "prefab.instantiate");
        var assetPath = NormalizeAndValidateAssetPath(ParseRequiredStringParameter(paramsObject, "assetPath"));
        var parentInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "parentInstanceId");
        var position = ParseOptionalVector3Parameter(paramsObject, "position");
        var rotationEuler = ParseOptionalVector3Parameter(paramsObject, "rotationEuler");
        var select = ParseOptionalBooleanParameter(paramsObject, "select", true);
        var ping = ParseOptionalBooleanParameter(paramsObject, "ping");
        var focus = ParseOptionalBooleanParameter(paramsObject, "focus");

        var prefabAsset = LoadPrefabAsset(assetPath);
        var activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            throw new InvalidOperationException("No active loaded scene is available for prefab instantiation.");
        }

        GameObject? parentGameObject = null;
        if (parentInstanceId.IsSpecified && parentInstanceId.HasValue)
        {
            var resolvedParentObject = ResolveObjectByInstanceId(parentInstanceId.Value!.Value, "parentInstanceId");
            parentGameObject = ResolveSceneGameObjectTarget(resolvedParentObject, "parentInstanceId");
            if (parentGameObject.scene != activeScene)
            {
                throw new ArgumentException("Cross-scene parenting is not supported in the MVP. Parent must be in the active loaded scene.");
            }
        }

        var instanceObject = PrefabUtility.InstantiatePrefab(prefabAsset, activeScene);
        if (instanceObject is not GameObject instance)
        {
            throw new InvalidOperationException($"Unity did not return a GameObject when instantiating prefab '{assetPath}'.");
        }

        Undo.RegisterCreatedObjectUndo(instance, "UnityMCP Instantiate Prefab");

        if (parentGameObject != null)
        {
            Undo.SetTransformParent(instance.transform, parentGameObject.transform, "UnityMCP Instantiate Prefab");
        }

        if (position.HasValue || rotationEuler.HasValue)
        {
            Undo.RecordObject(instance.transform, "UnityMCP Instantiate Prefab");
            if (position.HasValue)
            {
                instance.transform.position = position.Value;
            }

            if (rotationEuler.HasValue)
            {
                instance.transform.rotation = Quaternion.Euler(rotationEuler.Value);
            }
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
            instance = CreateObjectSummary(instance),
            prefabSource = CreatePrefabAssetSummary(prefabAsset, instance),
            selection = BuildSelectionSummaryResult(),
            applied = new
            {
                parent = parentGameObject != null,
                position = position.HasValue,
                rotationEuler = rotationEuler.HasValue,
                selected = select,
                ping,
                focus
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetPrefabSourceResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "prefab.getSource");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var targetGameObject = ResolveSceneGameObjectTarget(resolvedObject, "instanceId");

        var prefabDetails = InspectPrefabInstance(targetGameObject, "instanceId");

        var result = new
        {
            target = CreateObjectSummary(targetGameObject),
            prefabInstanceStatus = prefabDetails.PrefabInstanceStatus,
            prefabAssetType = prefabDetails.PrefabAssetType,
            instanceRoot = CreateObjectSummary(prefabDetails.OutermostPrefabInstanceRoot),
            sourceAsset = CreatePrefabAssetSummary(prefabDetails.SourceAsset, prefabDetails.OutermostPrefabInstanceRoot),
            nearestPrefabInstanceRoot = CreateObjectSummary(prefabDetails.NearestPrefabInstanceRoot),
            isOutermostPrefabInstanceRoot = prefabDetails.IsOutermostPrefabInstanceRoot
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildApplyPrefabOverridesResponse(JToken idToken, JObject root)
    {
        var result = ApplyPrefabOverrides(root, "prefab.applyOverrides", revert: false);
        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildRevertPrefabOverridesResponse(JToken idToken, JObject root)
    {
        var result = ApplyPrefabOverrides(root, "prefab.revertOverrides", revert: true);
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

    // ── Editor utility ────────────────────────────────────────────────────

    private static string BuildClearConsoleResponse(JToken idToken)
    {
        var logEntries = System.Type.GetType("UnityEditor.LogEntries, UnityEditor");
        logEntries?.GetMethod("Clear")?.Invoke(null, null);
        return UnityMcpProtocol.CreateResult(idToken, new { cleared = true });
    }

    private static string BuildPausePlayModeResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "editor.pausePlayMode");
        if (!paramsObject.TryGetValue("paused", out var pausedToken) || pausedToken.Type != JTokenType.Boolean)
            throw new ArgumentException("Parameter 'paused' is required and must be a boolean.");
        EditorApplication.isPaused = pausedToken.Value<bool>();
        return UnityMcpProtocol.CreateResult(idToken, new
        {
            paused = EditorApplication.isPaused,
            editorState = BuildEditorStateResult()
        });
    }

    private static string BuildUndoResponse(JToken idToken)
    {
        Undo.PerformUndo();
        return UnityMcpProtocol.CreateResult(idToken, new { applied = true });
    }

    private static string BuildRedoResponse(JToken idToken)
    {
        Undo.PerformRedo();
        return UnityMcpProtocol.CreateResult(idToken, new { applied = true });
    }

    private static string BuildGetTagsResponse(JToken idToken)
    {
        var tags = UnityEditorInternal.InternalEditorUtility.tags;
        return UnityMcpProtocol.CreateResult(idToken, new { count = tags.Length, tags });
    }

    private static string BuildGetLayersResponse(JToken idToken)
    {
        var layers = UnityEditorInternal.InternalEditorUtility.layers;
        return UnityMcpProtocol.CreateResult(idToken, new { count = layers.Length, layers });
    }

    // ── Scene tag / layer ────────────────────────────────────────────────

    private static string BuildSetTagResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "scene.setTag");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        if (!paramsObject.TryGetValue("tag", out var tagToken) || tagToken.Type != JTokenType.String)
            throw new ArgumentException("Parameter 'tag' is required and must be a string.");
        var tag = tagToken.Value<string>()!;

        var gameObject = ResolveGameObjectFromInstanceId(instanceId, "scene.setTag");
        Undo.RecordObject(gameObject, "Set Tag");
        var previousTag = gameObject.tag;
        gameObject.tag = tag;
        EditorUtility.SetDirty(gameObject);

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(gameObject),
            previousTag,
            currentTag = gameObject.tag,
            applied = true
        });
    }

    private static string BuildSetLayerResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "scene.setLayer");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");

        if (!paramsObject.TryGetValue("layer", out var layerToken))
            throw new ArgumentException("Parameter 'layer' is required.");

        int layerIndex;
        if (layerToken.Type == JTokenType.Integer)
        {
            layerIndex = layerToken.Value<int>();
            if (layerIndex < 0 || layerIndex > 31)
                throw new ArgumentException("Parameter 'layer' integer must be between 0 and 31.");
        }
        else if (layerToken.Type == JTokenType.String)
        {
            layerIndex = LayerMask.NameToLayer(layerToken.Value<string>()!);
            if (layerIndex < 0)
                throw new ArgumentException($"Layer name '{layerToken.Value<string>()}' not found.");
        }
        else
        {
            throw new ArgumentException("Parameter 'layer' must be an integer index or layer name string.");
        }

        var includeChildren = false;
        if (paramsObject.TryGetValue("includeChildren", out var inclToken) && inclToken.Type == JTokenType.Boolean)
            includeChildren = inclToken.Value<bool>();

        var gameObject = ResolveGameObjectFromInstanceId(instanceId, "scene.setLayer");
        var previousLayer = gameObject.layer;
        SetLayerRecursive(gameObject, layerIndex, includeChildren);

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(gameObject),
            previousLayer,
            currentLayer = gameObject.layer,
            layerName = LayerMask.LayerToName(layerIndex),
            includeChildren,
            applied = true
        });
    }

    private static void SetLayerRecursive(GameObject go, int layer, bool includeChildren)
    {
        Undo.RecordObject(go, "Set Layer");
        go.layer = layer;
        EditorUtility.SetDirty(go);
        if (!includeChildren) return;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer, true);
    }

    // ── Scene management ─────────────────────────────────────────────────

    private static string BuildSaveSceneResponse(JToken idToken, JObject root)
    {
        UnityEngine.SceneManagement.Scene targetScene;
        string? scenePath = null;

        if (root.TryGetValue("params", out var paramsToken) && paramsToken is JObject p &&
            p.TryGetValue("scenePath", out var spToken) && spToken.Type == JTokenType.String)
        {
            scenePath = spToken.Value<string>();
            targetScene = FindOpenSceneByPathOrName(scenePath!, "scene.save");
        }
        else
        {
            targetScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        }

        var saved = UnityEditor.SceneManagement.EditorSceneManager.SaveScene(targetScene);
        return UnityMcpProtocol.CreateResult(idToken, new
        {
            saved,
            sceneName = targetScene.name,
            scenePath = targetScene.path
        });
    }

    private static string BuildOpenSceneResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "scene.openScene");
        if (!paramsObject.TryGetValue("scenePath", out var spToken) || spToken.Type != JTokenType.String)
            throw new ArgumentException("Parameter 'scenePath' is required and must be a string.");
        var scenePath = spToken.Value<string>()!;

        var mode = UnityEditor.SceneManagement.OpenSceneMode.Single;
        if (paramsObject.TryGetValue("mode", out var modeToken) && modeToken.Type == JTokenType.String)
        {
            mode = modeToken.Value<string>() switch
            {
                "Additive" => UnityEditor.SceneManagement.OpenSceneMode.Additive,
                "Single" => UnityEditor.SceneManagement.OpenSceneMode.Single,
                var m => throw new ArgumentException($"Invalid mode '{m}'. Use 'Single' or 'Additive'.")
            };
        }

        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, mode);
        if (!scene.IsValid())
            throw new ArgumentException($"Failed to open scene at '{scenePath}'.");

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            sceneName = scene.name,
            scenePath = scene.path,
            isActive = scene == UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene(),
            mode = mode.ToString(),
            opened = true
        });
    }

    private static string BuildNewSceneResponse(JToken idToken, JObject root)
    {
        var setup = UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects;
        var mode = UnityEditor.SceneManagement.NewSceneMode.Single;

        if (root.TryGetValue("params", out var paramsToken) && paramsToken is JObject p)
        {
            if (p.TryGetValue("setup", out var setupToken) && setupToken.Type == JTokenType.String)
            {
                setup = setupToken.Value<string>() switch
                {
                    "EmptyScene" => UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                    "DefaultGameObjects" => UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
                    var s => throw new ArgumentException($"Invalid setup '{s}'. Use 'EmptyScene' or 'DefaultGameObjects'.")
                };
            }

            if (p.TryGetValue("mode", out var modeToken) && modeToken.Type == JTokenType.String)
            {
                mode = modeToken.Value<string>() switch
                {
                    "Single" => UnityEditor.SceneManagement.NewSceneMode.Single,
                    "Additive" => UnityEditor.SceneManagement.NewSceneMode.Additive,
                    var m => throw new ArgumentException($"Invalid mode '{m}'. Use 'Single' or 'Additive'.")
                };
            }
        }

        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(setup, mode);
        return UnityMcpProtocol.CreateResult(idToken, new
        {
            sceneName = scene.name,
            scenePath = scene.path,
            setup = setup.ToString(),
            mode = mode.ToString(),
            created = true
        });
    }

    private static string BuildCloseSceneResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "scene.closeScene");
        if (!paramsObject.TryGetValue("scenePath", out var spToken) || spToken.Type != JTokenType.String)
            throw new ArgumentException("Parameter 'scenePath' is required and must be a string.");

        var removeScene = true;
        if (paramsObject.TryGetValue("removeScene", out var rmToken) && rmToken.Type == JTokenType.Boolean)
            removeScene = rmToken.Value<bool>();

        var scene = FindOpenSceneByPathOrName(spToken.Value<string>()!, "scene.closeScene");
        var sceneName = scene.name;
        var scenePath = scene.path;

        var closed = UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, removeScene);
        return UnityMcpProtocol.CreateResult(idToken, new
        {
            closed,
            sceneName,
            scenePath,
            removeScene
        });
    }

    private static string BuildSetActiveSceneResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "scene.setActiveScene");
        if (!paramsObject.TryGetValue("scenePath", out var spToken) || spToken.Type != JTokenType.String)
            throw new ArgumentException("Parameter 'scenePath' is required and must be a string.");

        var scene = FindOpenSceneByPathOrName(spToken.Value<string>()!, "scene.setActiveScene");
        var set = UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(scene);

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            set,
            sceneName = scene.name,
            scenePath = scene.path
        });
    }

    private static UnityEngine.SceneManagement.Scene FindOpenSceneByPathOrName(string pathOrName, string methodName)
    {
        var sceneCount = UnityEditor.SceneManagement.EditorSceneManager.sceneCount;
        for (var i = 0; i < sceneCount; i++)
        {
            var s = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i);
            if (s.path == pathOrName || s.name == pathOrName)
                return s;
        }
        throw new ArgumentException($"[{methodName}] No open scene matches '{pathOrName}'.");
    }

    // ── Asset creation / management ──────────────────────────────────────

    private static string BuildCreateFolderResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "assets.createFolder");
        if (!paramsObject.TryGetValue("parentFolder", out var parentToken) || parentToken.Type != JTokenType.String)
            throw new ArgumentException("Parameter 'parentFolder' is required and must be a string.");
        if (!paramsObject.TryGetValue("folderName", out var nameToken) || nameToken.Type != JTokenType.String)
            throw new ArgumentException("Parameter 'folderName' is required and must be a string.");

        var parentFolder = parentToken.Value<string>()!;
        var folderName = nameToken.Value<string>()!;

        if (!AssetDatabase.IsValidFolder(parentFolder))
            throw new ArgumentException($"Parent folder '{parentFolder}' does not exist.");

        var guid = AssetDatabase.CreateFolder(parentFolder, folderName);
        var createdPath = AssetDatabase.GUIDToAssetPath(guid);

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            created = true,
            assetPath = createdPath,
            guid
        });
    }

    private static string BuildCreateScriptResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "assets.createScript");
        if (!paramsObject.TryGetValue("assetPath", out var pathToken) || pathToken.Type != JTokenType.String)
            throw new ArgumentException("Parameter 'assetPath' is required and must be a string.");

        var assetPath = pathToken.Value<string>()!;
        if (!assetPath.EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Parameter 'assetPath' must end with '.cs'.");

        string content;
        if (paramsObject.TryGetValue("content", out var contentToken) && contentToken.Type == JTokenType.String)
        {
            content = contentToken.Value<string>()!;
        }
        else
        {
            var className = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            content = $"using UnityEngine;\n\npublic class {className} : MonoBehaviour\n{{\n    void Start()\n    {{\n    }}\n\n    void Update()\n    {{\n    }}\n}}\n";
        }

        var fullPath = System.IO.Path.Combine(Application.dataPath, "..", assetPath);
        fullPath = System.IO.Path.GetFullPath(fullPath);
        var dir = System.IO.Path.GetDirectoryName(fullPath)!;
        if (!System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        System.IO.File.WriteAllText(fullPath, content);
        AssetDatabase.Refresh();

        var guid = AssetDatabase.AssetPathToGUID(assetPath);
        return UnityMcpProtocol.CreateResult(idToken, new
        {
            created = true,
            assetPath,
            guid
        });
    }

    private static string BuildCreateMaterialResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "assets.createMaterial");
        if (!paramsObject.TryGetValue("assetPath", out var pathToken) || pathToken.Type != JTokenType.String)
            throw new ArgumentException("Parameter 'assetPath' is required and must be a string.");

        var assetPath = pathToken.Value<string>()!;
        var shaderName = "Standard";
        if (paramsObject.TryGetValue("shaderName", out var shaderToken) && shaderToken.Type == JTokenType.String)
            shaderName = shaderToken.Value<string>()!;

        var shader = Shader.Find(shaderName);
        if (shader == null)
            throw new ArgumentException($"Shader '{shaderName}' not found.");

        var material = new Material(shader);
        AssetDatabase.CreateAsset(material, assetPath);
        AssetDatabase.SaveAssets();

        var guid = AssetDatabase.AssetPathToGUID(assetPath);
        return UnityMcpProtocol.CreateResult(idToken, new
        {
            created = true,
            assetPath,
            guid,
            shaderName
        });
    }

    private static string BuildCreatePrefabResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "assets.createPrefab");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        if (!paramsObject.TryGetValue("assetPath", out var pathToken) || pathToken.Type != JTokenType.String)
            throw new ArgumentException("Parameter 'assetPath' is required and must be a string.");

        var assetPath = pathToken.Value<string>()!;
        var gameObject = ResolveGameObjectFromInstanceId(instanceId, "assets.createPrefab");
        var prefab = PrefabUtility.SaveAsPrefabAsset(gameObject, assetPath);
        if (prefab == null)
            throw new System.Exception($"Failed to save prefab at '{assetPath}'.");

        var guid = AssetDatabase.AssetPathToGUID(assetPath);
        return UnityMcpProtocol.CreateResult(idToken, new
        {
            created = true,
            assetPath,
            guid,
            source = CreateObjectSummary(gameObject)
        });
    }

    private static string BuildDeleteAssetResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "assets.delete");
        if (!paramsObject.TryGetValue("assetPath", out var pathToken) || pathToken.Type != JTokenType.String)
            throw new ArgumentException("Parameter 'assetPath' is required and must be a string.");

        var assetPath = pathToken.Value<string>()!;
        var deleted = AssetDatabase.DeleteAsset(assetPath);
        if (!deleted)
            throw new ArgumentException($"Failed to delete asset at '{assetPath}'. Check the path exists.");

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            deleted = true,
            assetPath
        });
    }

    private static string BuildMoveAssetResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "assets.move");
        if (!paramsObject.TryGetValue("sourcePath", out var srcToken) || srcToken.Type != JTokenType.String)
            throw new ArgumentException("Parameter 'sourcePath' is required and must be a string.");
        if (!paramsObject.TryGetValue("destinationPath", out var dstToken) || dstToken.Type != JTokenType.String)
            throw new ArgumentException("Parameter 'destinationPath' is required and must be a string.");

        var sourcePath = srcToken.Value<string>()!;
        var destinationPath = dstToken.Value<string>()!;

        var error = AssetDatabase.MoveAsset(sourcePath, destinationPath);
        if (!string.IsNullOrEmpty(error))
            throw new ArgumentException($"Move failed: {error}");

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            moved = true,
            sourcePath,
            destinationPath,
            guid = AssetDatabase.AssetPathToGUID(destinationPath)
        });
    }

    // ── Animator ─────────────────────────────────────────────────────────

    private static string BuildGetAnimatorSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "animator.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (animator, ownerGo) = ResolveComponentFromInstanceId<Animator>(instanceId, "animator.getSettings");

        var settings = new
        {
            enabled = animator.enabled,
            speed = animator.speed,
            applyRootMotion = animator.applyRootMotion,
            updateMode = animator.updateMode.ToString(),
            cullingMode = animator.cullingMode.ToString(),
            hasController = animator.runtimeAnimatorController != null,
            controllerName = animator.runtimeAnimatorController?.name,
            hasAvatar = animator.avatar != null,
            avatarName = animator.avatar?.name,
            layerCount = animator.layerCount,
            parameterCount = animator.parameterCount,
            isHuman = animator.isHuman,
            isInitialized = animator.isInitialized
        };

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(ownerGo),
            component = CreateObjectSummary(animator),
            settings
        });
    }

    private static string BuildSetAnimatorSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "animator.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (animator, ownerGo) = ResolveComponentFromInstanceId<Animator>(instanceId, "animator.setSettings");

        Undo.RecordObject(animator, "Set Animator Settings");
        var applied = new System.Collections.Generic.List<string>();

        if (paramsObject.TryGetValue("enabled", out var en) && en.Type == JTokenType.Boolean)
        { animator.enabled = en.Value<bool>(); applied.Add("enabled"); }
        if (TryGetFloat(paramsObject, "speed", out var animSpeed))
        { animator.speed = animSpeed; applied.Add("speed"); }
        if (paramsObject.TryGetValue("applyRootMotion", out var rm) && rm.Type == JTokenType.Boolean)
        { animator.applyRootMotion = rm.Value<bool>(); applied.Add("applyRootMotion"); }
        if (paramsObject.TryGetValue("updateMode", out var um))
        { animator.updateMode = ParseEnumToken<AnimatorUpdateMode>(um, "updateMode"); applied.Add("updateMode"); }
        if (paramsObject.TryGetValue("cullingMode", out var cm))
        { animator.cullingMode = ParseEnumToken<AnimatorCullingMode>(cm, "cullingMode"); applied.Add("cullingMode"); }

        EditorUtility.SetDirty(animator);
        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(ownerGo),
            component = CreateObjectSummary(animator),
            applied
        });
    }

    private static string BuildGetAnimatorParametersResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "animator.getParameters");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (animator, ownerGo) = ResolveComponentFromInstanceId<Animator>(instanceId, "animator.getParameters");

        if (animator.runtimeAnimatorController == null)
            throw new ArgumentException("Animator has no controller assigned.");

        var parameters = new System.Collections.Generic.List<object>();
        for (var i = 0; i < animator.parameterCount; i++)
        {
            var p = animator.GetParameter(i);
            object defaultValue = p.type switch
            {
                AnimatorControllerParameterType.Bool => p.defaultBool,
                AnimatorControllerParameterType.Int => p.defaultInt,
                AnimatorControllerParameterType.Float => p.defaultFloat,
                _ => (object)"trigger"
            };
            parameters.Add(new { name = p.name, type = p.type.ToString(), defaultValue });
        }

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(ownerGo),
            parameterCount = parameters.Count,
            parameters
        });
    }

    private static string BuildSetAnimatorParameterResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "animator.setParameter");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        if (!paramsObject.TryGetValue("parameterName", out var nameToken) || nameToken.Type != JTokenType.String)
            throw new ArgumentException("Parameter 'parameterName' is required and must be a string.");

        var (animator, ownerGo) = ResolveComponentFromInstanceId<Animator>(instanceId, "animator.setParameter");
        var paramName = nameToken.Value<string>()!;

        // Find parameter type
        AnimatorControllerParameterType? paramType = null;
        for (var i = 0; i < animator.parameterCount; i++)
        {
            var p = animator.GetParameter(i);
            if (p.name == paramName) { paramType = p.type; break; }
        }
        if (paramType == null)
            throw new ArgumentException($"Parameter '{paramName}' not found in Animator.");

        paramsObject.TryGetValue("value", out var valueToken);

        switch (paramType)
        {
            case AnimatorControllerParameterType.Bool:
                if (valueToken == null || valueToken.Type != JTokenType.Boolean)
                    throw new ArgumentException($"Parameter '{paramName}' is Bool; 'value' must be boolean.");
                animator.SetBool(paramName, valueToken.Value<bool>());
                break;
            case AnimatorControllerParameterType.Int:
                if (valueToken == null || valueToken.Type != JTokenType.Integer)
                    throw new ArgumentException($"Parameter '{paramName}' is Int; 'value' must be integer.");
                animator.SetInteger(paramName, valueToken.Value<int>());
                break;
            case AnimatorControllerParameterType.Float:
                if (valueToken == null || (valueToken.Type != JTokenType.Float && valueToken.Type != JTokenType.Integer))
                    throw new ArgumentException($"Parameter '{paramName}' is Float; 'value' must be a number.");
                animator.SetFloat(paramName, valueToken.Value<float>());
                break;
            case AnimatorControllerParameterType.Trigger:
                animator.SetTrigger(paramName);
                break;
        }

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(ownerGo),
            parameterName = paramName,
            parameterType = paramType.ToString(),
            applied = true
        });
    }

    // ── MeshRenderer ─────────────────────────────────────────────────────

    private static string BuildGetMeshRendererSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "meshRenderer.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (mr, ownerGo) = ResolveComponentFromInstanceId<MeshRenderer>(instanceId, "meshRenderer.getSettings");

        var materials = new System.Collections.Generic.List<object>();
        foreach (var mat in mr.sharedMaterials)
            materials.Add(new { name = mat != null ? mat.name : null, shader = mat != null ? mat.shader?.name : null });

        var settings = new
        {
            enabled = mr.enabled,
            shadowCastingMode = mr.shadowCastingMode.ToString(),
            receiveShadows = mr.receiveShadows,
            lightProbeUsage = mr.lightProbeUsage.ToString(),
            reflectionProbeUsage = mr.reflectionProbeUsage.ToString(),
            motionVectorGenerationMode = mr.motionVectorGenerationMode.ToString(),
            staticShadowCaster = mr.staticShadowCaster,
            allowOcclusionWhenDynamic = mr.allowOcclusionWhenDynamic,
            materialCount = mr.sharedMaterials.Length,
            materials
        };

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(ownerGo),
            component = CreateObjectSummary(mr),
            settings
        });
    }

    private static string BuildSetMeshRendererSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "meshRenderer.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (mr, ownerGo) = ResolveComponentFromInstanceId<MeshRenderer>(instanceId, "meshRenderer.setSettings");

        Undo.RecordObject(mr, "Set MeshRenderer Settings");
        var applied = new System.Collections.Generic.List<string>();

        if (paramsObject.TryGetValue("enabled", out var en) && en.Type == JTokenType.Boolean)
        { mr.enabled = en.Value<bool>(); applied.Add("enabled"); }
        if (paramsObject.TryGetValue("shadowCastingMode", out var scm))
        { mr.shadowCastingMode = ParseEnumToken<UnityEngine.Rendering.ShadowCastingMode>(scm, "shadowCastingMode"); applied.Add("shadowCastingMode"); }
        if (paramsObject.TryGetValue("receiveShadows", out var rs) && rs.Type == JTokenType.Boolean)
        { mr.receiveShadows = rs.Value<bool>(); applied.Add("receiveShadows"); }
        if (paramsObject.TryGetValue("lightProbeUsage", out var lpu))
        { mr.lightProbeUsage = ParseEnumToken<UnityEngine.Rendering.LightProbeUsage>(lpu, "lightProbeUsage"); applied.Add("lightProbeUsage"); }
        if (paramsObject.TryGetValue("reflectionProbeUsage", out var rpu))
        { mr.reflectionProbeUsage = ParseEnumToken<UnityEngine.Rendering.ReflectionProbeUsage>(rpu, "reflectionProbeUsage"); applied.Add("reflectionProbeUsage"); }
        if (paramsObject.TryGetValue("motionVectorGenerationMode", out var mvm))
        { mr.motionVectorGenerationMode = ParseEnumToken<MotionVectorGenerationMode>(mvm, "motionVectorGenerationMode"); applied.Add("motionVectorGenerationMode"); }
        if (paramsObject.TryGetValue("staticShadowCaster", out var ssc) && ssc.Type == JTokenType.Boolean)
        { mr.staticShadowCaster = ssc.Value<bool>(); applied.Add("staticShadowCaster"); }
        if (paramsObject.TryGetValue("allowOcclusionWhenDynamic", out var aod) && aod.Type == JTokenType.Boolean)
        { mr.allowOcclusionWhenDynamic = aod.Value<bool>(); applied.Add("allowOcclusionWhenDynamic"); }

        EditorUtility.SetDirty(mr);
        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(ownerGo),
            component = CreateObjectSummary(mr),
            applied
        });
    }

    // ── AudioSource ───────────────────────────────────────────────────────

    private static string BuildGetAudioSourceSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "audioSource.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (audio, ownerGo) = ResolveComponentFromInstanceId<AudioSource>(instanceId, "audioSource.getSettings");

        var settings = new
        {
            enabled = audio.enabled,
            clipName = audio.clip != null ? audio.clip.name : null,
            volume = audio.volume,
            pitch = audio.pitch,
            loop = audio.loop,
            playOnAwake = audio.playOnAwake,
            mute = audio.mute,
            spatialBlend = audio.spatialBlend,
            spatialize = audio.spatialize,
            priority = audio.priority,
            dopplerLevel = audio.dopplerLevel,
            minDistance = audio.minDistance,
            maxDistance = audio.maxDistance,
            rolloffMode = audio.rolloffMode.ToString(),
            isPlaying = audio.isPlaying
        };

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(ownerGo),
            component = CreateObjectSummary(audio),
            settings
        });
    }

    private static string BuildSetAudioSourceSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "audioSource.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (audio, ownerGo) = ResolveComponentFromInstanceId<AudioSource>(instanceId, "audioSource.setSettings");

        Undo.RecordObject(audio, "Set AudioSource Settings");
        var applied = new System.Collections.Generic.List<string>();

        if (paramsObject.TryGetValue("enabled", out var en) && en.Type == JTokenType.Boolean)
        { audio.enabled = en.Value<bool>(); applied.Add("enabled"); }
        if (TryGetFloat(paramsObject, "volume", out var vol))
        { audio.volume = vol; applied.Add("volume"); }
        if (TryGetFloat(paramsObject, "pitch", out var pitch))
        { audio.pitch = pitch; applied.Add("pitch"); }
        if (paramsObject.TryGetValue("loop", out var loop) && loop.Type == JTokenType.Boolean)
        { audio.loop = loop.Value<bool>(); applied.Add("loop"); }
        if (paramsObject.TryGetValue("playOnAwake", out var poa) && poa.Type == JTokenType.Boolean)
        { audio.playOnAwake = poa.Value<bool>(); applied.Add("playOnAwake"); }
        if (paramsObject.TryGetValue("mute", out var mute) && mute.Type == JTokenType.Boolean)
        { audio.mute = mute.Value<bool>(); applied.Add("mute"); }
        if (TryGetFloat(paramsObject, "spatialBlend", out var sb))
        { audio.spatialBlend = sb; applied.Add("spatialBlend"); }
        if (paramsObject.TryGetValue("spatialize", out var spat) && spat.Type == JTokenType.Boolean)
        { audio.spatialize = spat.Value<bool>(); applied.Add("spatialize"); }
        if (paramsObject.TryGetValue("priority", out var pri) && pri.Type == JTokenType.Integer)
        { audio.priority = pri.Value<int>(); applied.Add("priority"); }
        if (TryGetFloat(paramsObject, "dopplerLevel", out var dl))
        { audio.dopplerLevel = dl; applied.Add("dopplerLevel"); }
        if (TryGetFloat(paramsObject, "minDistance", out var minD))
        { audio.minDistance = minD; applied.Add("minDistance"); }
        if (TryGetFloat(paramsObject, "maxDistance", out var maxD))
        { audio.maxDistance = maxD; applied.Add("maxDistance"); }
        if (paramsObject.TryGetValue("rolloffMode", out var rom))
        { audio.rolloffMode = ParseEnumToken<AudioRolloffMode>(rom, "rolloffMode"); applied.Add("rolloffMode"); }

        EditorUtility.SetDirty(audio);
        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(ownerGo),
            component = CreateObjectSummary(audio),
            applied
        });
    }

    // ── CharacterController ───────────────────────────────────────────────

    private static string BuildGetCharacterControllerSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "characterController.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (cc, ownerGo) = ResolveComponentFromInstanceId<CharacterController>(instanceId, "characterController.getSettings");

        var settings = new
        {
            height = cc.height,
            radius = cc.radius,
            center = CreateVector3Array(cc.center),
            slopeLimit = cc.slopeLimit,
            stepOffset = cc.stepOffset,
            skinWidth = cc.skinWidth,
            minMoveDistance = cc.minMoveDistance,
            enableOverlapRecovery = cc.enableOverlapRecovery,
            isGrounded = cc.isGrounded
        };

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(ownerGo),
            component = CreateObjectSummary(cc),
            settings
        });
    }

    private static string BuildSetCharacterControllerSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "characterController.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (cc, ownerGo) = ResolveComponentFromInstanceId<CharacterController>(instanceId, "characterController.setSettings");

        Undo.RecordObject(cc, "Set CharacterController Settings");
        var applied = new System.Collections.Generic.List<string>();

        if (TryGetFloat(paramsObject, "height", out var h))
        { cc.height = h; applied.Add("height"); }
        if (TryGetFloat(paramsObject, "radius", out var r))
        { cc.radius = r; applied.Add("radius"); }
        if (paramsObject.TryGetValue("center", out var ctr) && ctr is JArray ctrArr && ctrArr.Count == 3)
        { cc.center = new Vector3(ctrArr[0].Value<float>(), ctrArr[1].Value<float>(), ctrArr[2].Value<float>()); applied.Add("center"); }
        if (TryGetFloat(paramsObject, "slopeLimit", out var sl))
        { cc.slopeLimit = sl; applied.Add("slopeLimit"); }
        if (TryGetFloat(paramsObject, "stepOffset", out var so))
        { cc.stepOffset = so; applied.Add("stepOffset"); }
        if (TryGetFloat(paramsObject, "skinWidth", out var sw))
        { cc.skinWidth = sw; applied.Add("skinWidth"); }
        if (TryGetFloat(paramsObject, "minMoveDistance", out var mmd))
        { cc.minMoveDistance = mmd; applied.Add("minMoveDistance"); }
        if (paramsObject.TryGetValue("enableOverlapRecovery", out var eor) && eor.Type == JTokenType.Boolean)
        { cc.enableOverlapRecovery = eor.Value<bool>(); applied.Add("enableOverlapRecovery"); }

        EditorUtility.SetDirty(cc);
        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(ownerGo),
            component = CreateObjectSummary(cc),
            applied
        });
    }

    // ── ParticleSystem ──────────────────────────────────────────────────

    private static string BuildGetParticleSystemSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "particleSystem.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (ps, ownerGo) = ResolveComponentFromInstanceId<ParticleSystem>(instanceId, "particleSystem.getSettings");

        var main = ps.main;
        var emission = ps.emission;
        var settings = new
        {
            duration = main.duration,
            loop = main.loop,
            prewarm = main.prewarm,
            startDelay = main.startDelay.constant,
            startLifetime = main.startLifetime.constant,
            startSpeed = main.startSpeed.constant,
            startSize = main.startSize.constant,
            maxParticles = main.maxParticles,
            playOnAwake = main.playOnAwake,
            emissionRate = emission.rateOverTime.constant,
            isPlaying = ps.isPlaying,
            isPaused = ps.isPaused,
            isStopped = ps.isStopped,
            particleCount = ps.particleCount
        };

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(ownerGo),
            component = CreateObjectSummary(ps),
            settings
        });
    }

    private static string BuildSetParticleSystemSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "particleSystem.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (ps, ownerGo) = ResolveComponentFromInstanceId<ParticleSystem>(instanceId, "particleSystem.setSettings");

        Undo.RecordObject(ps, "Set ParticleSystem Settings");
        var applied = new System.Collections.Generic.List<string>();

        var main = ps.main;
        var emission = ps.emission;

        if (TryGetFloat(paramsObject, "duration", out var duration))
        { main.duration = duration; applied.Add("duration"); }
        if (paramsObject.TryGetValue("loop", out var loopToken) && loopToken.Type == JTokenType.Boolean)
        { main.loop = loopToken.Value<bool>(); applied.Add("loop"); }
        if (paramsObject.TryGetValue("prewarm", out var prewarmToken) && prewarmToken.Type == JTokenType.Boolean)
        { main.prewarm = prewarmToken.Value<bool>(); applied.Add("prewarm"); }
        if (TryGetFloat(paramsObject, "startDelay", out var startDelay))
        { main.startDelay = startDelay; applied.Add("startDelay"); }
        if (TryGetFloat(paramsObject, "startLifetime", out var startLifetime))
        { main.startLifetime = startLifetime; applied.Add("startLifetime"); }
        if (TryGetFloat(paramsObject, "startSpeed", out var startSpeed))
        { main.startSpeed = startSpeed; applied.Add("startSpeed"); }
        if (TryGetFloat(paramsObject, "startSize", out var startSize))
        { main.startSize = startSize; applied.Add("startSize"); }
        if (paramsObject.TryGetValue("maxParticles", out var maxP) && maxP.Type == JTokenType.Integer)
        { main.maxParticles = maxP.Value<int>(); applied.Add("maxParticles"); }
        if (paramsObject.TryGetValue("playOnAwake", out var poa) && poa.Type == JTokenType.Boolean)
        { main.playOnAwake = poa.Value<bool>(); applied.Add("playOnAwake"); }
        if (TryGetFloat(paramsObject, "emissionRate", out var emRate))
        { emission.rateOverTime = emRate; applied.Add("emissionRate"); }

        EditorUtility.SetDirty(ps);
        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(ownerGo),
            component = CreateObjectSummary(ps),
            applied
        });
    }

    private static string BuildParticleSystemPlayResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "particleSystem.play");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (ps, ownerGo) = ResolveComponentFromInstanceId<ParticleSystem>(instanceId, "particleSystem.play");

        ps.Play();
        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(ownerGo),
            component = CreateObjectSummary(ps),
            isPlaying = ps.isPlaying,
            particleCount = ps.particleCount
        });
    }

    private static string BuildParticleSystemStopResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "particleSystem.stop");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (ps, ownerGo) = ResolveComponentFromInstanceId<ParticleSystem>(instanceId, "particleSystem.stop");

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(ownerGo),
            component = CreateObjectSummary(ps),
            isPlaying = ps.isPlaying,
            isStopped = ps.isStopped,
            particleCount = ps.particleCount
        });
    }

    // ── NavMeshAgent ────────────────────────────────────────────────────

    private static string BuildGetNavMeshAgentSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "navMeshAgent.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (agent, ownerGo) = ResolveComponentFromInstanceId<UnityEngine.AI.NavMeshAgent>(instanceId, "navMeshAgent.getSettings");

        var settings = new
        {
            speed = agent.speed,
            angularSpeed = agent.angularSpeed,
            acceleration = agent.acceleration,
            stoppingDistance = agent.stoppingDistance,
            radius = agent.radius,
            height = agent.height,
            areaMask = agent.areaMask,
            autoBraking = agent.autoBraking,
            obstacleAvoidanceType = agent.obstacleAvoidanceType.ToString(),
            avoidancePriority = agent.avoidancePriority,
            enabled = agent.enabled
        };

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(ownerGo),
            component = CreateObjectSummary(agent),
            settings
        });
    }

    private static string BuildSetNavMeshAgentSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "navMeshAgent.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (agent, ownerGo) = ResolveComponentFromInstanceId<UnityEngine.AI.NavMeshAgent>(instanceId, "navMeshAgent.setSettings");

        Undo.RecordObject(agent, "Set NavMeshAgent Settings");
        var applied = new System.Collections.Generic.List<string>();

        if (TryGetFloat(paramsObject, "speed", out var speed))
        { agent.speed = speed; applied.Add("speed"); }
        if (TryGetFloat(paramsObject, "angularSpeed", out var angSpeed))
        { agent.angularSpeed = angSpeed; applied.Add("angularSpeed"); }
        if (TryGetFloat(paramsObject, "acceleration", out var accel))
        { agent.acceleration = accel; applied.Add("acceleration"); }
        if (TryGetFloat(paramsObject, "stoppingDistance", out var stopDist))
        { agent.stoppingDistance = stopDist; applied.Add("stoppingDistance"); }
        if (TryGetFloat(paramsObject, "radius", out var radius))
        { agent.radius = radius; applied.Add("radius"); }
        if (TryGetFloat(paramsObject, "height", out var height))
        { agent.height = height; applied.Add("height"); }
        if (paramsObject.TryGetValue("areaMask", out var am) && am.Type == JTokenType.Integer)
        { agent.areaMask = am.Value<int>(); applied.Add("areaMask"); }
        if (paramsObject.TryGetValue("autoBraking", out var ab) && ab.Type == JTokenType.Boolean)
        { agent.autoBraking = ab.Value<bool>(); applied.Add("autoBraking"); }
        if (paramsObject.TryGetValue("obstacleAvoidanceType", out var oat))
        { agent.obstacleAvoidanceType = ParseEnumToken<UnityEngine.AI.ObstacleAvoidanceType>(oat, "obstacleAvoidanceType"); applied.Add("obstacleAvoidanceType"); }
        if (paramsObject.TryGetValue("avoidancePriority", out var ap) && ap.Type == JTokenType.Integer)
        { agent.avoidancePriority = ap.Value<int>(); applied.Add("avoidancePriority"); }
        if (paramsObject.TryGetValue("enabled", out var en) && en.Type == JTokenType.Boolean)
        { agent.enabled = en.Value<bool>(); applied.Add("enabled"); }

        EditorUtility.SetDirty(agent);
        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(ownerGo),
            component = CreateObjectSummary(agent),
            applied
        });
    }

    // ── NavMeshObstacle ─────────────────────────────────────────────────

    private static string BuildGetNavMeshObstacleSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "navMeshObstacle.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (obstacle, ownerGo) = ResolveComponentFromInstanceId<UnityEngine.AI.NavMeshObstacle>(instanceId, "navMeshObstacle.getSettings");

        var settings = new
        {
            carving = obstacle.carving,
            carvingMoveThreshold = obstacle.carvingMoveThreshold,
            carvingTimeToStationary = obstacle.carvingTimeToStationary,
            shape = obstacle.shape.ToString(),
            center = CreateVector3Array(obstacle.center),
            size = CreateVector3Array(obstacle.size),
            radius = obstacle.radius,
            height = obstacle.height,
            enabled = obstacle.enabled
        };

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(ownerGo),
            component = CreateObjectSummary(obstacle),
            settings
        });
    }

    private static string BuildSetNavMeshObstacleSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "navMeshObstacle.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (obstacle, ownerGo) = ResolveComponentFromInstanceId<UnityEngine.AI.NavMeshObstacle>(instanceId, "navMeshObstacle.setSettings");

        Undo.RecordObject(obstacle, "Set NavMeshObstacle Settings");
        var applied = new System.Collections.Generic.List<string>();

        if (paramsObject.TryGetValue("carving", out var carv) && carv.Type == JTokenType.Boolean)
        { obstacle.carving = carv.Value<bool>(); applied.Add("carving"); }
        if (TryGetFloat(paramsObject, "carvingMoveThreshold", out var cmt))
        { obstacle.carvingMoveThreshold = cmt; applied.Add("carvingMoveThreshold"); }
        if (TryGetFloat(paramsObject, "carvingTimeToStationary", out var tts))
        { obstacle.carvingTimeToStationary = tts; applied.Add("carvingTimeToStationary"); }
        if (paramsObject.TryGetValue("shape", out var shapeToken))
        { obstacle.shape = ParseEnumToken<UnityEngine.AI.NavMeshObstacleShape>(shapeToken, "shape"); applied.Add("shape"); }
        if (paramsObject.TryGetValue("center", out var ctr) && ctr is JArray ctrArr && ctrArr.Count == 3)
        { obstacle.center = new Vector3(ctrArr[0].Value<float>(), ctrArr[1].Value<float>(), ctrArr[2].Value<float>()); applied.Add("center"); }
        if (paramsObject.TryGetValue("size", out var sz) && sz is JArray szArr && szArr.Count == 3)
        { obstacle.size = new Vector3(szArr[0].Value<float>(), szArr[1].Value<float>(), szArr[2].Value<float>()); applied.Add("size"); }
        if (TryGetFloat(paramsObject, "radius", out var rad))
        { obstacle.radius = rad; applied.Add("radius"); }
        if (TryGetFloat(paramsObject, "height", out var h))
        { obstacle.height = h; applied.Add("height"); }
        if (paramsObject.TryGetValue("enabled", out var en) && en.Type == JTokenType.Boolean)
        { obstacle.enabled = en.Value<bool>(); applied.Add("enabled"); }

        EditorUtility.SetDirty(obstacle);
        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(ownerGo),
            component = CreateObjectSummary(obstacle),
            applied
        });
    }

    // ── RectTransform ───────────────────────────────────────────────────

    private static string BuildGetRectTransformSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "rectTransform.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (rt, ownerGo) = ResolveComponentFromInstanceId<RectTransform>(instanceId, "rectTransform.getSettings");

        var settings = new
        {
            anchorMin = CreateVector2Array(rt.anchorMin),
            anchorMax = CreateVector2Array(rt.anchorMax),
            anchoredPosition = CreateVector2Array(rt.anchoredPosition),
            sizeDelta = CreateVector2Array(rt.sizeDelta),
            pivot = CreateVector2Array(rt.pivot),
            offsetMin = CreateVector2Array(rt.offsetMin),
            offsetMax = CreateVector2Array(rt.offsetMax),
            rect = new { x = rt.rect.x, y = rt.rect.y, width = rt.rect.width, height = rt.rect.height }
        };

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(ownerGo),
            component = CreateObjectSummary(rt),
            settings
        });
    }

    private static string BuildSetRectTransformSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "rectTransform.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (rt, ownerGo) = ResolveComponentFromInstanceId<RectTransform>(instanceId, "rectTransform.setSettings");

        Undo.RecordObject(rt, "Set RectTransform Settings");
        var applied = new System.Collections.Generic.List<string>();

        if (paramsObject.TryGetValue("anchorMin", out var amin) && amin is JArray aminArr && aminArr.Count == 2)
        { rt.anchorMin = new Vector2(aminArr[0].Value<float>(), aminArr[1].Value<float>()); applied.Add("anchorMin"); }
        if (paramsObject.TryGetValue("anchorMax", out var amax) && amax is JArray amaxArr && amaxArr.Count == 2)
        { rt.anchorMax = new Vector2(amaxArr[0].Value<float>(), amaxArr[1].Value<float>()); applied.Add("anchorMax"); }
        if (paramsObject.TryGetValue("anchoredPosition", out var ap) && ap is JArray apArr && apArr.Count == 2)
        { rt.anchoredPosition = new Vector2(apArr[0].Value<float>(), apArr[1].Value<float>()); applied.Add("anchoredPosition"); }
        if (paramsObject.TryGetValue("sizeDelta", out var sd) && sd is JArray sdArr && sdArr.Count == 2)
        { rt.sizeDelta = new Vector2(sdArr[0].Value<float>(), sdArr[1].Value<float>()); applied.Add("sizeDelta"); }
        if (paramsObject.TryGetValue("pivot", out var pv) && pv is JArray pvArr && pvArr.Count == 2)
        { rt.pivot = new Vector2(pvArr[0].Value<float>(), pvArr[1].Value<float>()); applied.Add("pivot"); }
        if (paramsObject.TryGetValue("offsetMin", out var omin) && omin is JArray ominArr && ominArr.Count == 2)
        { rt.offsetMin = new Vector2(ominArr[0].Value<float>(), ominArr[1].Value<float>()); applied.Add("offsetMin"); }
        if (paramsObject.TryGetValue("offsetMax", out var omax) && omax is JArray omaxArr && omaxArr.Count == 2)
        { rt.offsetMax = new Vector2(omaxArr[0].Value<float>(), omaxArr[1].Value<float>()); applied.Add("offsetMax"); }

        EditorUtility.SetDirty(rt);
        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(ownerGo),
            component = CreateObjectSummary(rt),
            applied
        });
    }

    // ── Canvas ──────────────────────────────────────────────────────────

    private static string BuildGetCanvasSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "canvas.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (canvas, ownerGo) = ResolveComponentFromInstanceId<Canvas>(instanceId, "canvas.getSettings");

        var settings = new
        {
            renderMode = canvas.renderMode.ToString(),
            sortingOrder = canvas.sortingOrder,
            targetDisplay = canvas.targetDisplay,
            pixelPerfect = canvas.pixelPerfect,
            planeDistance = canvas.planeDistance,
            overrideSorting = canvas.overrideSorting,
            enabled = canvas.enabled
        };

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(ownerGo),
            component = CreateObjectSummary(canvas),
            settings
        });
    }

    private static string BuildSetCanvasSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "canvas.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (canvas, ownerGo) = ResolveComponentFromInstanceId<Canvas>(instanceId, "canvas.setSettings");

        Undo.RecordObject(canvas, "Set Canvas Settings");
        var applied = new System.Collections.Generic.List<string>();

        if (paramsObject.TryGetValue("renderMode", out var rm))
        { canvas.renderMode = ParseEnumToken<RenderMode>(rm, "renderMode"); applied.Add("renderMode"); }
        if (paramsObject.TryGetValue("sortingOrder", out var so) && so.Type == JTokenType.Integer)
        { canvas.sortingOrder = so.Value<int>(); applied.Add("sortingOrder"); }
        if (paramsObject.TryGetValue("targetDisplay", out var td) && td.Type == JTokenType.Integer)
        { canvas.targetDisplay = td.Value<int>(); applied.Add("targetDisplay"); }
        if (paramsObject.TryGetValue("pixelPerfect", out var pp) && pp.Type == JTokenType.Boolean)
        { canvas.pixelPerfect = pp.Value<bool>(); applied.Add("pixelPerfect"); }
        if (TryGetFloat(paramsObject, "planeDistance", out var pd))
        { canvas.planeDistance = pd; applied.Add("planeDistance"); }
        if (paramsObject.TryGetValue("overrideSorting", out var os) && os.Type == JTokenType.Boolean)
        { canvas.overrideSorting = os.Value<bool>(); applied.Add("overrideSorting"); }
        if (paramsObject.TryGetValue("enabled", out var en) && en.Type == JTokenType.Boolean)
        { canvas.enabled = en.Value<bool>(); applied.Add("enabled"); }

        EditorUtility.SetDirty(canvas);
        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(ownerGo),
            component = CreateObjectSummary(canvas),
            applied
        });
    }

    // ── SkinnedMeshRenderer ─────────────────────────────────────────────

    private static string BuildGetSkinnedMeshRendererSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "skinnedMeshRenderer.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (smr, ownerGo) = ResolveComponentFromInstanceId<SkinnedMeshRenderer>(instanceId, "skinnedMeshRenderer.getSettings");

        var materials = new System.Collections.Generic.List<object>();
        foreach (var mat in smr.sharedMaterials)
            materials.Add(new { name = mat != null ? mat.name : null, shader = mat != null ? mat.shader?.name : null });

        var settings = new
        {
            enabled = smr.enabled,
            shadowCastingMode = smr.shadowCastingMode.ToString(),
            receiveShadows = smr.receiveShadows,
            lightProbeUsage = smr.lightProbeUsage.ToString(),
            reflectionProbeUsage = smr.reflectionProbeUsage.ToString(),
            motionVectorGenerationMode = smr.motionVectorGenerationMode.ToString(),
            staticShadowCaster = smr.staticShadowCaster,
            allowOcclusionWhenDynamic = smr.allowOcclusionWhenDynamic,
            materialCount = smr.sharedMaterials.Length,
            materials,
            rootBone = smr.rootBone != null ? smr.rootBone.name : null,
            quality = smr.quality.ToString(),
            updateWhenOffscreen = smr.updateWhenOffscreen,
            sharedMeshName = smr.sharedMesh != null ? smr.sharedMesh.name : null,
            blendShapeCount = smr.sharedMesh != null ? smr.sharedMesh.blendShapeCount : 0
        };

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(ownerGo),
            component = CreateObjectSummary(smr),
            settings
        });
    }

    private static string BuildSetSkinnedMeshRendererSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "skinnedMeshRenderer.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (smr, ownerGo) = ResolveComponentFromInstanceId<SkinnedMeshRenderer>(instanceId, "skinnedMeshRenderer.setSettings");

        Undo.RecordObject(smr, "Set SkinnedMeshRenderer Settings");
        var applied = new System.Collections.Generic.List<string>();

        if (paramsObject.TryGetValue("enabled", out var en) && en.Type == JTokenType.Boolean)
        { smr.enabled = en.Value<bool>(); applied.Add("enabled"); }
        if (paramsObject.TryGetValue("shadowCastingMode", out var scm))
        { smr.shadowCastingMode = ParseEnumToken<UnityEngine.Rendering.ShadowCastingMode>(scm, "shadowCastingMode"); applied.Add("shadowCastingMode"); }
        if (paramsObject.TryGetValue("receiveShadows", out var rs) && rs.Type == JTokenType.Boolean)
        { smr.receiveShadows = rs.Value<bool>(); applied.Add("receiveShadows"); }
        if (paramsObject.TryGetValue("lightProbeUsage", out var lpu))
        { smr.lightProbeUsage = ParseEnumToken<UnityEngine.Rendering.LightProbeUsage>(lpu, "lightProbeUsage"); applied.Add("lightProbeUsage"); }
        if (paramsObject.TryGetValue("reflectionProbeUsage", out var rpu))
        { smr.reflectionProbeUsage = ParseEnumToken<UnityEngine.Rendering.ReflectionProbeUsage>(rpu, "reflectionProbeUsage"); applied.Add("reflectionProbeUsage"); }
        if (paramsObject.TryGetValue("motionVectorGenerationMode", out var mvm))
        { smr.motionVectorGenerationMode = ParseEnumToken<MotionVectorGenerationMode>(mvm, "motionVectorGenerationMode"); applied.Add("motionVectorGenerationMode"); }
        if (paramsObject.TryGetValue("staticShadowCaster", out var ssc) && ssc.Type == JTokenType.Boolean)
        { smr.staticShadowCaster = ssc.Value<bool>(); applied.Add("staticShadowCaster"); }
        if (paramsObject.TryGetValue("allowOcclusionWhenDynamic", out var aod) && aod.Type == JTokenType.Boolean)
        { smr.allowOcclusionWhenDynamic = aod.Value<bool>(); applied.Add("allowOcclusionWhenDynamic"); }
        if (paramsObject.TryGetValue("quality", out var q))
        { smr.quality = ParseEnumToken<SkinQuality>(q, "quality"); applied.Add("quality"); }
        if (paramsObject.TryGetValue("updateWhenOffscreen", out var uwo) && uwo.Type == JTokenType.Boolean)
        { smr.updateWhenOffscreen = uwo.Value<bool>(); applied.Add("updateWhenOffscreen"); }

        EditorUtility.SetDirty(smr);
        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(ownerGo),
            component = CreateObjectSummary(smr),
            applied
        });
    }

    // ── ScriptableObject Creation ───────────────────────────────────────

    private static string BuildCreateScriptableObjectResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "assets.createScriptableObject");
        var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
        var typeName = ParseRequiredStringParameter(paramsObject, "typeName");

        if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/"))
            throw new ArgumentException("Parameter 'assetPath' must be a project-relative path starting with 'Assets/'.");

        if (!assetPath.EndsWith(".asset"))
            assetPath += ".asset";

        // Ensure directory exists
        var dir = System.IO.Path.GetDirectoryName(assetPath);
        if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
        {
            // Create directory hierarchy
            var parts = dir.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        // Find the type by name
        System.Type? soType = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            soType = asm.GetType(typeName);
            if (soType != null) break;
        }

        if (soType == null)
        {
            // Try short name search
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var t in asm.GetTypes())
                {
                    if (t.Name == typeName && t.IsSubclassOf(typeof(ScriptableObject)))
                    {
                        soType = t;
                        break;
                    }
                }
                if (soType != null) break;
            }
        }

        if (soType == null || !soType.IsSubclassOf(typeof(ScriptableObject)))
            throw new ArgumentException($"Type '{typeName}' was not found or is not a ScriptableObject subclass.");

        var instance = ScriptableObject.CreateInstance(soType);
        AssetDatabase.CreateAsset(instance, assetPath);
        AssetDatabase.SaveAssets();

        var guid = AssetDatabase.AssetPathToGUID(assetPath);
        return UnityMcpProtocol.CreateResult(idToken, new
        {
            assetPath,
            typeName = soType.FullName,
            guid,
            created = true
        });
    }

    // ── Batch 3: NavMesh ────────────────────────────────────────────────

    private static string BuildNavMeshBakeResponse(JToken idToken)
    {
#pragma warning disable CS0618 // UnityEditor.AI.NavMeshBuilder deprecated; replacement not available in Editor context
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
#pragma warning restore CS0618
        return UnityMcpProtocol.CreateResult(idToken, new { baked = true });
    }

    // ── Batch 3: Terrain ────────────────────────────────────────────────

    private static string BuildGetTerrainSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "terrain.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (terrain, _) = ResolveComponentFromInstanceId<Terrain>(instanceId, "terrain.getSettings");
        var td = terrain.terrainData;

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(terrain),
            heightmapResolution = td != null ? td.heightmapResolution : 0,
            size = td != null ? CreateVector3Array(td.size) : null,
            basemapDistance = terrain.basemapDistance,
            drawHeightmap = terrain.drawHeightmap,
            drawInstanced = terrain.drawInstanced,
            detailObjectDistance = terrain.detailObjectDistance,
            treeBillboardDistance = terrain.treeBillboardDistance,
            shadowCastingMode = terrain.shadowCastingMode.ToString()
        });
    }

    private static string BuildSetTerrainSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "terrain.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var (terrain, ownerGo) = ResolveComponentFromInstanceId<Terrain>(instanceId, "terrain.setSettings");
        var td = terrain.terrainData;

        Undo.RecordObject(terrain, "Set Terrain Settings");
        if (td != null)
            Undo.RecordObject(td, "Set Terrain Settings");

        if (td != null && paramsObject.TryGetValue("heightmapResolution", out var hmrToken) && hmrToken.Type == JTokenType.Integer)
            td.heightmapResolution = hmrToken.Value<int>();

        if (td != null && paramsObject.TryGetValue("size", out var sizeToken) && sizeToken is JArray sizeArr && sizeArr.Count == 3)
            td.size = new Vector3(sizeArr[0].Value<float>(), sizeArr[1].Value<float>(), sizeArr[2].Value<float>());

        if (TryGetFloat(paramsObject, "basemapDistance", out var basemap))
            terrain.basemapDistance = basemap;

        if (paramsObject.TryGetValue("drawHeightmap", out var dhToken) && dhToken.Type == JTokenType.Boolean)
            terrain.drawHeightmap = dhToken.Value<bool>();

        if (paramsObject.TryGetValue("drawInstanced", out var diToken) && diToken.Type == JTokenType.Boolean)
            terrain.drawInstanced = diToken.Value<bool>();

        if (TryGetFloat(paramsObject, "detailObjectDistance", out var detailDist))
            terrain.detailObjectDistance = detailDist;

        if (TryGetFloat(paramsObject, "treeBillboardDistance", out var treeDist))
            terrain.treeBillboardDistance = treeDist;

        if (paramsObject.TryGetValue("shadowCastingMode", out var scmToken))
        {
            terrain.shadowCastingMode = ParseEnumToken<UnityEngine.Rendering.ShadowCastingMode>(scmToken, "shadowCastingMode");
        }

        EditorUtility.SetDirty(terrain);
        if (td != null)
            EditorUtility.SetDirty(td);

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            target = CreateObjectSummary(terrain),
            applied = true
        });
    }

    // ── Batch 3: Build Pipeline ─────────────────────────────────────────

    private static string BuildGetBuildSettingsResponse(JToken idToken)
    {
        var scenes = EditorBuildSettings.scenes;
        var sceneList = new List<object>(scenes.Length);
        foreach (var scene in scenes)
        {
            sceneList.Add(new
            {
                path = scene.path,
                enabled = scene.enabled,
                guid = scene.guid.ToString()
            });
        }

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            scenes = sceneList,
            activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
            developmentBuild = EditorUserBuildSettings.development,
            buildOutputPath = EditorUserBuildSettings.GetBuildLocation(EditorUserBuildSettings.activeBuildTarget)
        });
    }

    private static string BuildSetBuildSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "build.setSettings");

        if (paramsObject.TryGetValue("developmentBuild", out var devToken) && devToken.Type == JTokenType.Boolean)
            EditorUserBuildSettings.development = devToken.Value<bool>();

        if (paramsObject.TryGetValue("outputPath", out var opToken) && opToken.Type == JTokenType.String)
            EditorUserBuildSettings.SetBuildLocation(EditorUserBuildSettings.activeBuildTarget, opToken.Value<string>()!);

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            developmentBuild = EditorUserBuildSettings.development,
            buildOutputPath = EditorUserBuildSettings.GetBuildLocation(EditorUserBuildSettings.activeBuildTarget),
            applied = true
        });
    }

    private static string BuildBuildResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "build.build");
        var outputPath = ParseRequiredStringParameter(paramsObject, "outputPath");

        var scenes = EditorBuildSettings.scenes;
        var enabledScenes = new List<string>();
        foreach (var scene in scenes)
        {
            if (scene.enabled)
                enabledScenes.Add(scene.path);
        }

        if (enabledScenes.Count == 0)
            throw new ArgumentException("No enabled scenes in build settings.");

        var options = EditorUserBuildSettings.development
            ? BuildOptions.Development
            : BuildOptions.None;

        var report = BuildPipeline.BuildPlayer(
            enabledScenes.ToArray(),
            outputPath,
            EditorUserBuildSettings.activeBuildTarget,
            options);

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            summary = report.summary.result.ToString(),
            totalErrors = report.summary.totalErrors,
            totalWarnings = report.summary.totalWarnings,
            totalTime = report.summary.totalTime.TotalSeconds,
            outputPath = report.summary.outputPath
        });
    }

    // ── Batch 3: Tags & Layers Management ───────────────────────────────

    private static string BuildAddTagResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "editor.addTag");
        var tag = ParseRequiredStringParameter(paramsObject, "tag");

        // Check if tag already exists
        foreach (var existing in UnityEditorInternal.InternalEditorUtility.tags)
        {
            if (existing == tag)
                return UnityMcpProtocol.CreateResult(idToken, new { added = false, reason = "Tag already exists.", tag });
        }

        var tagManager = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
        var tagsProp = tagManager.FindProperty("tags");

        tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
        var newTag = tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1);
        newTag.stringValue = tag;
        tagManager.ApplyModifiedProperties();

        return UnityMcpProtocol.CreateResult(idToken, new { added = true, tag });
    }

    private static string BuildRemoveTagResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "editor.removeTag");
        var tag = ParseRequiredStringParameter(paramsObject, "tag");

        var tagManager = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
        var tagsProp = tagManager.FindProperty("tags");

        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag)
            {
                tagsProp.DeleteArrayElementAtIndex(i);
                tagManager.ApplyModifiedProperties();
                return UnityMcpProtocol.CreateResult(idToken, new { removed = true, tag });
            }
        }

        return UnityMcpProtocol.CreateResult(idToken, new { removed = false, reason = "Tag not found.", tag });
    }

    private static string BuildAddLayerResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "editor.addLayer");
        var layer = ParseRequiredStringParameter(paramsObject, "layer");

        // Check if layer already exists
        for (int i = 0; i < 32; i++)
        {
            if (LayerMask.LayerToName(i) == layer)
                return UnityMcpProtocol.CreateResult(idToken, new { added = false, reason = "Layer already exists.", layer, slot = i });
        }

        var tagManager = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
        var layersProp = tagManager.FindProperty("layers");

        // Find first empty slot in user layers (8-31)
        for (int i = 8; i < 32; i++)
        {
            var element = layersProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(element.stringValue))
            {
                element.stringValue = layer;
                tagManager.ApplyModifiedProperties();
                return UnityMcpProtocol.CreateResult(idToken, new { added = true, layer, slot = i });
            }
        }

        throw new ArgumentException("No empty layer slots available (layers 8-31 are all used).");
    }

    private static string BuildRemoveLayerResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "editor.removeLayer");
        var layer = ParseRequiredStringParameter(paramsObject, "layer");

        var tagManager = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
        var layersProp = tagManager.FindProperty("layers");

        for (int i = 8; i < 32; i++)
        {
            var element = layersProp.GetArrayElementAtIndex(i);
            if (element.stringValue == layer)
            {
                element.stringValue = "";
                tagManager.ApplyModifiedProperties();
                return UnityMcpProtocol.CreateResult(idToken, new { removed = true, layer, slot = i });
            }
        }

        return UnityMcpProtocol.CreateResult(idToken, new { removed = false, reason = "Layer not found.", layer });
    }

    // ── Batch 3: Selection Utilities ────────────────────────────────────

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

    // ── Batch 3: Undo History ───────────────────────────────────────────

    private static string BuildGetUndoHistoryResponse(JToken idToken)
    {
        return UnityMcpProtocol.CreateResult(idToken, new
        {
            currentGroupName = Undo.GetCurrentGroupName(),
            currentGroup = Undo.GetCurrentGroup()
        });
    }

    // ── Batch 4: Camera Projection ──────────────────────────────────────

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

    // ── Batch 4: SpriteRenderer ─────────────────────────────────────────

    private static string BuildGetSpriteRendererSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "spriteRenderer.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var sr = ResolveComponentOfTypeTarget<SpriteRenderer>(resolvedObject, "instanceId", "SpriteRenderer");

        var result = new
        {
            target = CreateObjectSummary(sr.gameObject),
            component = CreateComponentSummary(sr),
            settings = new
            {
                spriteName = sr.sprite != null ? sr.sprite.name : (string?)null,
                color = CreateColorArray(sr.color),
                flipX = sr.flipX,
                flipY = sr.flipY,
                sortingLayerName = sr.sortingLayerName,
                sortingOrder = sr.sortingOrder,
                drawMode = CreateEnumSummary(sr.drawMode),
                maskInteraction = CreateEnumSummary(sr.maskInteraction)
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetSpriteRendererSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "spriteRenderer.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var sr = ResolveComponentOfTypeTarget<SpriteRenderer>(resolvedObject, "instanceId", "SpriteRenderer");

        var color = ParseOptionalColorParameter(paramsObject, "color");
        var flipX = ParseOptionalBooleanValueParameter(paramsObject, "flipX");
        var flipY = ParseOptionalBooleanValueParameter(paramsObject, "flipY");
        var sortingLayerName = ParseOptionalStringParameter(paramsObject, "sortingLayerName");
        var sortingOrder = ParseOptionalIntegerParameter(paramsObject, "sortingOrder");

        if (!color.HasValue &&
            !flipX.HasValue &&
            !flipY.HasValue &&
            sortingLayerName == null &&
            !sortingOrder.HasValue)
        {
            throw new ArgumentException(
                "At least one SpriteRenderer setting must be provided: color, flipX, flipY, sortingLayerName, or sortingOrder.");
        }

        Undo.RecordObject(sr, "UnityMCP Set SpriteRenderer Settings");

        if (color.HasValue)
        {
            sr.color = color.Value;
        }

        if (flipX.HasValue)
        {
            sr.flipX = flipX.Value;
        }

        if (flipY.HasValue)
        {
            sr.flipY = flipY.Value;
        }

        if (sortingLayerName != null)
        {
            sr.sortingLayerName = sortingLayerName;
        }

        if (sortingOrder.HasValue)
        {
            sr.sortingOrder = sortingOrder.Value;
        }

        EditorUtility.SetDirty(sr);

        var result = new
        {
            target = CreateObjectSummary(sr.gameObject),
            component = CreateComponentSummary(sr),
            settings = new
            {
                spriteName = sr.sprite != null ? sr.sprite.name : (string?)null,
                color = CreateColorArray(sr.color),
                flipX = sr.flipX,
                flipY = sr.flipY,
                sortingLayerName = sr.sortingLayerName,
                sortingOrder = sr.sortingOrder,
                drawMode = CreateEnumSummary(sr.drawMode),
                maskInteraction = CreateEnumSummary(sr.maskInteraction)
            },
            applied = new
            {
                color = color.HasValue,
                flipX = flipX.HasValue,
                flipY = flipY.HasValue,
                sortingLayerName = sortingLayerName != null,
                sortingOrder = sortingOrder.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    // ── Batch 4: LineRenderer ───────────────────────────────────────────

    private static string BuildGetLineRendererSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "lineRenderer.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var lr = ResolveComponentOfTypeTarget<LineRenderer>(resolvedObject, "instanceId", "LineRenderer");

        var positions = new List<object>(lr.positionCount);
        for (var i = 0; i < lr.positionCount; i++)
        {
            var pos = lr.GetPosition(i);
            positions.Add(CreateVector3Array(pos));
        }

        var result = new
        {
            target = CreateObjectSummary(lr.gameObject),
            component = CreateComponentSummary(lr),
            settings = new
            {
                positionCount = lr.positionCount,
                positions,
                loop = lr.loop,
                startWidth = lr.startWidth,
                endWidth = lr.endWidth,
                useWorldSpace = lr.useWorldSpace,
                startColor = CreateColorArray(lr.startColor),
                endColor = CreateColorArray(lr.endColor)
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetLineRendererSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "lineRenderer.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var lr = ResolveComponentOfTypeTarget<LineRenderer>(resolvedObject, "instanceId", "LineRenderer");

        var loop = ParseOptionalBooleanValueParameter(paramsObject, "loop");
        var startWidth = ParseOptionalFloatParameter(paramsObject, "startWidth");
        var endWidth = ParseOptionalFloatParameter(paramsObject, "endWidth");
        var useWorldSpace = ParseOptionalBooleanValueParameter(paramsObject, "useWorldSpace");
        var startColor = ParseOptionalColorParameter(paramsObject, "startColor");
        var endColor = ParseOptionalColorParameter(paramsObject, "endColor");

        if (!loop.HasValue &&
            !startWidth.HasValue &&
            !endWidth.HasValue &&
            !useWorldSpace.HasValue &&
            !startColor.HasValue &&
            !endColor.HasValue)
        {
            throw new ArgumentException(
                "At least one LineRenderer setting must be provided: loop, startWidth, endWidth, useWorldSpace, startColor, or endColor.");
        }

        if (startWidth.HasValue && startWidth.Value < 0f)
        {
            throw new ArgumentException("Parameter 'startWidth' must be greater than or equal to 0.");
        }

        if (endWidth.HasValue && endWidth.Value < 0f)
        {
            throw new ArgumentException("Parameter 'endWidth' must be greater than or equal to 0.");
        }

        Undo.RecordObject(lr, "UnityMCP Set LineRenderer Settings");

        if (loop.HasValue)
        {
            lr.loop = loop.Value;
        }

        if (startWidth.HasValue)
        {
            lr.startWidth = startWidth.Value;
        }

        if (endWidth.HasValue)
        {
            lr.endWidth = endWidth.Value;
        }

        if (useWorldSpace.HasValue)
        {
            lr.useWorldSpace = useWorldSpace.Value;
        }

        if (startColor.HasValue)
        {
            lr.startColor = startColor.Value;
        }

        if (endColor.HasValue)
        {
            lr.endColor = endColor.Value;
        }

        EditorUtility.SetDirty(lr);

        var positionsAfter = new List<object>(lr.positionCount);
        for (var i = 0; i < lr.positionCount; i++)
        {
            positionsAfter.Add(CreateVector3Array(lr.GetPosition(i)));
        }

        var result = new
        {
            target = CreateObjectSummary(lr.gameObject),
            component = CreateComponentSummary(lr),
            settings = new
            {
                positionCount = lr.positionCount,
                positions = positionsAfter,
                loop = lr.loop,
                startWidth = lr.startWidth,
                endWidth = lr.endWidth,
                useWorldSpace = lr.useWorldSpace,
                startColor = CreateColorArray(lr.startColor),
                endColor = CreateColorArray(lr.endColor)
            },
            applied = new
            {
                loop = loop.HasValue,
                startWidth = startWidth.HasValue,
                endWidth = endWidth.HasValue,
                useWorldSpace = useWorldSpace.HasValue,
                startColor = startColor.HasValue,
                endColor = endColor.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    // ── Batch 4: LODGroup ───────────────────────────────────────────────

    private static string BuildGetLODGroupSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "lodGroup.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var lodGroup = ResolveComponentOfTypeTarget<LODGroup>(resolvedObject, "instanceId", "LODGroup");

        var result = new
        {
            target = CreateObjectSummary(lodGroup.gameObject),
            component = CreateComponentSummary(lodGroup),
            settings = new
            {
                lodCount = lodGroup.lodCount,
                fadeMode = CreateEnumSummary(lodGroup.fadeMode),
                animateCrossFading = lodGroup.animateCrossFading,
                size = lodGroup.size
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetLODGroupSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "lodGroup.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var lodGroup = ResolveComponentOfTypeTarget<LODGroup>(resolvedObject, "instanceId", "LODGroup");

        var fadeMode = ParseOptionalEnumParameter<LODFadeMode>(paramsObject, "fadeMode");
        var animateCrossFading = ParseOptionalBooleanValueParameter(paramsObject, "animateCrossFading");

        if (!fadeMode.HasValue && !animateCrossFading.HasValue)
        {
            throw new ArgumentException(
                "At least one LODGroup setting must be provided: fadeMode or animateCrossFading.");
        }

        Undo.RecordObject(lodGroup, "UnityMCP Set LODGroup Settings");

        if (fadeMode.HasValue)
        {
            lodGroup.fadeMode = fadeMode.Value;
        }

        if (animateCrossFading.HasValue)
        {
            lodGroup.animateCrossFading = animateCrossFading.Value;
        }

        EditorUtility.SetDirty(lodGroup);

        var result = new
        {
            target = CreateObjectSummary(lodGroup.gameObject),
            component = CreateComponentSummary(lodGroup),
            settings = new
            {
                lodCount = lodGroup.lodCount,
                fadeMode = CreateEnumSummary(lodGroup.fadeMode),
                animateCrossFading = lodGroup.animateCrossFading,
                size = lodGroup.size
            },
            applied = new
            {
                fadeMode = fadeMode.HasValue,
                animateCrossFading = animateCrossFading.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    // ── Batch 4: CanvasGroup ────────────────────────────────────────────

    private static string BuildGetCanvasGroupSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "canvasGroup.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var canvasGroup = ResolveComponentOfTypeTarget<CanvasGroup>(resolvedObject, "instanceId", "CanvasGroup");

        var result = new
        {
            target = CreateObjectSummary(canvasGroup.gameObject),
            component = CreateComponentSummary(canvasGroup),
            settings = new
            {
                alpha = canvasGroup.alpha,
                interactable = canvasGroup.interactable,
                blocksRaycasts = canvasGroup.blocksRaycasts,
                ignoreParentGroups = canvasGroup.ignoreParentGroups
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetCanvasGroupSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "canvasGroup.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var canvasGroup = ResolveComponentOfTypeTarget<CanvasGroup>(resolvedObject, "instanceId", "CanvasGroup");

        var alpha = ParseOptionalFloatParameter(paramsObject, "alpha");
        var interactable = ParseOptionalBooleanValueParameter(paramsObject, "interactable");
        var blocksRaycasts = ParseOptionalBooleanValueParameter(paramsObject, "blocksRaycasts");
        var ignoreParentGroups = ParseOptionalBooleanValueParameter(paramsObject, "ignoreParentGroups");

        if (!alpha.HasValue &&
            !interactable.HasValue &&
            !blocksRaycasts.HasValue &&
            !ignoreParentGroups.HasValue)
        {
            throw new ArgumentException(
                "At least one CanvasGroup setting must be provided: alpha, interactable, blocksRaycasts, or ignoreParentGroups.");
        }

        if (alpha.HasValue && (alpha.Value < 0f || alpha.Value > 1f))
        {
            throw new ArgumentException("Parameter 'alpha' must be between 0 and 1.");
        }

        Undo.RecordObject(canvasGroup, "UnityMCP Set CanvasGroup Settings");

        if (alpha.HasValue)
        {
            canvasGroup.alpha = alpha.Value;
        }

        if (interactable.HasValue)
        {
            canvasGroup.interactable = interactable.Value;
        }

        if (blocksRaycasts.HasValue)
        {
            canvasGroup.blocksRaycasts = blocksRaycasts.Value;
        }

        if (ignoreParentGroups.HasValue)
        {
            canvasGroup.ignoreParentGroups = ignoreParentGroups.Value;
        }

        EditorUtility.SetDirty(canvasGroup);

        var result = new
        {
            target = CreateObjectSummary(canvasGroup.gameObject),
            component = CreateComponentSummary(canvasGroup),
            settings = new
            {
                alpha = canvasGroup.alpha,
                interactable = canvasGroup.interactable,
                blocksRaycasts = canvasGroup.blocksRaycasts,
                ignoreParentGroups = canvasGroup.ignoreParentGroups
            },
            applied = new
            {
                alpha = alpha.HasValue,
                interactable = interactable.HasValue,
                blocksRaycasts = blocksRaycasts.HasValue,
                ignoreParentGroups = ignoreParentGroups.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    // ── Batch 4: Editor Recompile Scripts ────────────────────────────────

    private static string BuildRecompileScriptsResponse(JToken idToken)
    {
        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
        return UnityMcpProtocol.CreateResult(idToken, new { requested = true });
    }

    // ── Batch 4: Scene Instantiate Prefab ───────────────────────────────

    private static string BuildSceneInstantiatePrefabResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "scene.instantiatePrefab");
        var assetPath = NormalizeAndValidateAssetPath(ParseRequiredStringParameter(paramsObject, "assetPath"));
        var position = ParseOptionalVector3Parameter(paramsObject, "position");
        var parentInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "parentInstanceId");

        var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefabAsset == null)
        {
            throw new ArgumentException($"No prefab found at asset path '{assetPath}'.");
        }

        var activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            throw new InvalidOperationException("No active loaded scene is available for prefab instantiation.");
        }

        GameObject? parentGameObject = null;
        if (parentInstanceId.IsSpecified && parentInstanceId.HasValue)
        {
            var resolvedParentObject = ResolveObjectByInstanceId(parentInstanceId.Value!.Value, "parentInstanceId");
            parentGameObject = ResolveSceneGameObjectTarget(resolvedParentObject, "parentInstanceId");
            if (parentGameObject.scene != activeScene)
            {
                throw new ArgumentException("Cross-scene parenting is not supported. Parent must be in the active loaded scene.");
            }
        }

        var instanceObject = PrefabUtility.InstantiatePrefab(prefabAsset, activeScene);
        if (instanceObject is not GameObject instance)
        {
            throw new InvalidOperationException($"Unity did not return a GameObject when instantiating prefab '{assetPath}'.");
        }

        Undo.RegisterCreatedObjectUndo(instance, "UnityMCP Scene Instantiate Prefab");

        if (parentGameObject != null)
        {
            Undo.SetTransformParent(instance.transform, parentGameObject.transform, "UnityMCP Scene Instantiate Prefab");
        }

        if (position.HasValue)
        {
            Undo.RecordObject(instance.transform, "UnityMCP Scene Instantiate Prefab");
            instance.transform.position = position.Value;
        }

        Selection.activeGameObject = instance;

        var result = new
        {
            instance = CreateObjectSummary(instance),
            assetPath,
            parent = parentGameObject != null ? CreateObjectSummary(parentGameObject) : null,
            applied = new
            {
                position = position.HasValue,
                parent = parentGameObject != null
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    // ── Batch 5: Physics Queries ──────────────────────────────────────

    private static string BuildPhysicsRaycastResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "physics.raycast");

        if (!paramsObject.TryGetValue("origin", out var originToken))
            throw new ArgumentException("Parameter 'origin' is required.");
        var origin = ParseVector3Parameter(originToken, "origin");

        if (!paramsObject.TryGetValue("direction", out var directionToken))
            throw new ArgumentException("Parameter 'direction' is required.");
        var direction = ParseVector3Parameter(directionToken, "direction");

        var maxDistance = Mathf.Infinity;
        if (paramsObject.TryGetValue("maxDistance", out var maxDistToken) &&
            (maxDistToken.Type == JTokenType.Float || maxDistToken.Type == JTokenType.Integer))
        {
            maxDistance = maxDistToken.Value<float>();
        }

        var layerMask = Physics.DefaultRaycastLayers;
        if (paramsObject.TryGetValue("layerMask", out var layerMaskToken) &&
            layerMaskToken.Type == JTokenType.Integer)
        {
            layerMask = layerMaskToken.Value<int>();
        }

        object result;
        if (Physics.Raycast(origin, direction, out var hit, maxDistance, layerMask))
        {
            result = new
            {
                hit = true,
                point = CreateVector3Array(hit.point),
                normal = CreateVector3Array(hit.normal),
                distance = hit.distance,
                gameObjectName = hit.collider.gameObject.name,
                instanceId = hit.collider.gameObject.GetInstanceID()
            };
        }
        else
        {
            result = new
            {
                hit = false,
                point = (object?)null,
                normal = (object?)null,
                distance = (object?)null,
                gameObjectName = (object?)null,
                instanceId = (object?)null
            };
        }

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildPhysicsOverlapSphereResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "physics.overlapSphere");

        if (!paramsObject.TryGetValue("center", out var centerToken))
            throw new ArgumentException("Parameter 'center' is required.");
        var center = ParseVector3Parameter(centerToken, "center");

        if (!paramsObject.TryGetValue("radius", out var radiusToken) ||
            (radiusToken.Type != JTokenType.Float && radiusToken.Type != JTokenType.Integer))
            throw new ArgumentException("Parameter 'radius' is required and must be a number.");
        var radius = radiusToken.Value<float>();
        if (radius <= 0f)
            throw new ArgumentException("Parameter 'radius' must be greater than 0.");

        var layerMask = Physics.AllLayers;
        if (paramsObject.TryGetValue("layerMask", out var layerMaskToken) &&
            layerMaskToken.Type == JTokenType.Integer)
        {
            layerMask = layerMaskToken.Value<int>();
        }

        var colliders = Physics.OverlapSphere(center, radius, layerMask);
        var items = new object[colliders.Length];
        for (var i = 0; i < colliders.Length; i++)
        {
            items[i] = new
            {
                gameObjectName = colliders[i].gameObject.name,
                instanceId = colliders[i].gameObject.GetInstanceID()
            };
        }

        var result = new
        {
            count = colliders.Length,
            colliders = items
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    // ── Batch 5: Time ─────────────────────────────────────────────────

    private static string BuildGetTimeSettingsResponse(JToken idToken)
    {
        var result = new
        {
            timeScale = Time.timeScale,
            fixedDeltaTime = Time.fixedDeltaTime,
            maximumDeltaTime = Time.maximumDeltaTime,
            maximumParticleDeltaTime = Time.maximumParticleDeltaTime
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetTimeSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "time.setSettings");

        var timeScale = ParseOptionalFloatParameter(paramsObject, "timeScale");
        var fixedDeltaTime = ParseOptionalFloatParameter(paramsObject, "fixedDeltaTime");

        if (!timeScale.HasValue && !fixedDeltaTime.HasValue)
        {
            throw new ArgumentException("At least one time setting must be provided: timeScale or fixedDeltaTime.");
        }

        if (timeScale.HasValue && timeScale.Value < 0f)
        {
            throw new ArgumentException("Parameter 'timeScale' must be greater than or equal to 0.");
        }

        if (fixedDeltaTime.HasValue && fixedDeltaTime.Value <= 0f)
        {
            throw new ArgumentException("Parameter 'fixedDeltaTime' must be greater than 0.");
        }

        if (timeScale.HasValue)
        {
            Time.timeScale = timeScale.Value;
        }

        if (fixedDeltaTime.HasValue)
        {
            Time.fixedDeltaTime = fixedDeltaTime.Value;
        }

        var result = new
        {
            timeScale = Time.timeScale,
            fixedDeltaTime = Time.fixedDeltaTime,
            maximumDeltaTime = Time.maximumDeltaTime,
            maximumParticleDeltaTime = Time.maximumParticleDeltaTime,
            applied = new
            {
                timeScale = timeScale.HasValue,
                fixedDeltaTime = fixedDeltaTime.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    // ── Batch 5: Joint (base 3D) ──────────────────────────────────────

    private static string BuildGetJointSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "joint.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<Joint>(resolvedObject, "instanceId", "Joint");

        var connectedBody = joint.connectedBody;
        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = new
            {
                connectedBodyInstanceId = connectedBody != null ? (int?)connectedBody.gameObject.GetInstanceID() : null,
                breakForce = joint.breakForce,
                breakTorque = joint.breakTorque,
                enableCollision = joint.enableCollision,
                enablePreprocessing = joint.enablePreprocessing
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetJointSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "joint.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<Joint>(resolvedObject, "instanceId", "Joint");

        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var enableCollision = ParseOptionalBooleanValueParameter(paramsObject, "enableCollision");

        if (!breakForce.HasValue && !breakTorque.HasValue && !enableCollision.HasValue)
        {
            throw new ArgumentException("At least one joint setting must be provided: breakForce, breakTorque, or enableCollision.");
        }

        Undo.RecordObject(joint, "UnityMCP Set Joint Settings");

        if (breakForce.HasValue)
        {
            joint.breakForce = breakForce.Value;
        }

        if (breakTorque.HasValue)
        {
            joint.breakTorque = breakTorque.Value;
        }

        if (enableCollision.HasValue)
        {
            joint.enableCollision = enableCollision.Value;
        }

        EditorUtility.SetDirty(joint);

        var connectedBody = joint.connectedBody;
        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = new
            {
                connectedBodyInstanceId = connectedBody != null ? (int?)connectedBody.gameObject.GetInstanceID() : null,
                breakForce = joint.breakForce,
                breakTorque = joint.breakTorque,
                enableCollision = joint.enableCollision,
                enablePreprocessing = joint.enablePreprocessing
            },
            applied = new
            {
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                enableCollision = enableCollision.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    // ── Batch 5: Renderer ─────────────────────────────────────────────

    private static string BuildGetRendererMaterialsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "renderer.getMaterials");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var renderer = ResolveComponentOfTypeTarget<Renderer>(resolvedObject, "instanceId", "Renderer");

        var sharedMaterials = renderer.sharedMaterials;
        var materials = new object[sharedMaterials.Length];
        for (var i = 0; i < sharedMaterials.Length; i++)
        {
            var mat = sharedMaterials[i];
            materials[i] = mat != null
                ? new { name = (string?)mat.name, instanceId = (int?)mat.GetInstanceID() }
                : new { name = (string?)null, instanceId = (int?)null };
        }

        var result = new
        {
            target = CreateObjectSummary(renderer.gameObject),
            component = CreateComponentSummary(renderer),
            materialCount = sharedMaterials.Length,
            materials
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetRendererMaterialResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "renderer.setMaterial");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var renderer = ResolveComponentOfTypeTarget<Renderer>(resolvedObject, "instanceId", "Renderer");

        var materialIndex = ParseRequiredIntegerParameter(paramsObject, "materialIndex");
        var materialAssetPath = ParseRequiredStringParameter(paramsObject, "materialAssetPath");

        var sharedMaterials = renderer.sharedMaterials;
        if (materialIndex < 0 || materialIndex >= sharedMaterials.Length)
        {
            throw new ArgumentException($"Parameter 'materialIndex' ({materialIndex}) is out of range. Renderer has {sharedMaterials.Length} material slot(s).");
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
        if (material == null)
        {
            throw new ArgumentException($"No Material found at asset path '{materialAssetPath}'.");
        }

        Undo.RecordObject(renderer, "UnityMCP Set Renderer Material");

        sharedMaterials[materialIndex] = material;
        renderer.sharedMaterials = sharedMaterials;

        EditorUtility.SetDirty(renderer);

        // Return updated materials list
        var updatedMaterials = renderer.sharedMaterials;
        var materialsResult = new object[updatedMaterials.Length];
        for (var i = 0; i < updatedMaterials.Length; i++)
        {
            var mat = updatedMaterials[i];
            materialsResult[i] = mat != null
                ? new { name = (string?)mat.name, instanceId = (int?)mat.GetInstanceID() }
                : new { name = (string?)null, instanceId = (int?)null };
        }

        var result = new
        {
            target = CreateObjectSummary(renderer.gameObject),
            component = CreateComponentSummary(renderer),
            materialIndex,
            materialAssetPath,
            materialCount = updatedMaterials.Length,
            materials = materialsResult
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    // ── Shared helpers for new methods ───────────────────────────────────

    private static GameObject ResolveGameObjectFromInstanceId(int instanceId, string methodName)
    {
        var resolved = ResolveObjectByInstanceId(instanceId, "instanceId");
        return ResolveGameObjectTarget(resolved, "instanceId");
    }

    private static (T component, GameObject ownerGo) ResolveComponentFromInstanceId<T>(int instanceId, string methodName)
        where T : Component
    {
        var resolved = ResolveObjectByInstanceId(instanceId, "instanceId");
        var component = ResolveComponentOfTypeTarget<T>(resolved, "instanceId", typeof(T).Name);
        return (component, component.gameObject);
    }

    private static bool TryGetFloat(JObject obj, string key, out float value)
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

    // ── Batch 6: Audio ──────────────────────────────────────────────────────

    private static string BuildAudioSourceGetSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "audio.getSourceSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var source = ResolveComponentOfTypeTarget<AudioSource>(resolvedObject, "instanceId", "AudioSource");

        var clipPath = source.clip != null ? AssetDatabase.GetAssetPath(source.clip) : "";
        var clipName = source.clip != null ? source.clip.name : "";
        string? mixerGroupPath = null;
        if (source.outputAudioMixerGroup != null)
        {
            var mixer = source.outputAudioMixerGroup.audioMixer;
            var groupName = source.outputAudioMixerGroup.name;
            mixerGroupPath = mixer != null ? $"{mixer.name}/{groupName}" : groupName;
        }

        var result = new
        {
            target = CreateObjectSummary(source.gameObject),
            component = CreateComponentSummary(source),
            settings = new
            {
                clipAssetPath = clipPath,
                clipName,
                volume = source.volume,
                pitch = source.pitch,
                loop = source.loop,
                mute = source.mute,
                playOnAwake = source.playOnAwake,
                spatialBlend = source.spatialBlend,
                minDistance = source.minDistance,
                maxDistance = source.maxDistance,
                rolloffMode = source.rolloffMode.ToString(),
                mixerGroupPath,
                isPlaying = source.isPlaying
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildAudioSourceSetSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "audio.setSourceSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var source = ResolveComponentOfTypeTarget<AudioSource>(resolvedObject, "instanceId", "AudioSource");

        var volume       = ParseOptionalFloatParameter(paramsObject, "volume");
        var pitch        = ParseOptionalFloatParameter(paramsObject, "pitch");
        var loop         = ParseOptionalBooleanValueParameter(paramsObject, "loop");
        var mute         = ParseOptionalBooleanValueParameter(paramsObject, "mute");
        var playOnAwake  = ParseOptionalBooleanValueParameter(paramsObject, "playOnAwake");
        var spatialBlend = ParseOptionalFloatParameter(paramsObject, "spatialBlend");
        var minDistance  = ParseOptionalFloatParameter(paramsObject, "minDistance");
        var maxDistance  = ParseOptionalFloatParameter(paramsObject, "maxDistance");
        var rolloffMode  = ParseOptionalStringParameter(paramsObject, "rolloffMode");
        var clipAssetPath   = ParseOptionalStringParameter(paramsObject, "clipAssetPath");
        var mixerGroupPath  = ParseOptionalStringParameter(paramsObject, "mixerGroupPath");

        if (!volume.HasValue && !pitch.HasValue && !loop.HasValue && !mute.HasValue &&
            !playOnAwake.HasValue && !spatialBlend.HasValue && !minDistance.HasValue &&
            !maxDistance.HasValue && rolloffMode == null && clipAssetPath == null && mixerGroupPath == null)
        {
            throw new ArgumentException(
                "At least one AudioSource property must be specified: volume, pitch, loop, mute, playOnAwake, spatialBlend, minDistance, maxDistance, rolloffMode, clipAssetPath, or mixerGroupPath.");
        }

        if (minDistance.HasValue && minDistance.Value <= 0f)
            throw new ArgumentException("Parameter 'minDistance' must be greater than 0.");

        var effectiveMin = minDistance ?? source.minDistance;
        var effectiveMax = maxDistance ?? source.maxDistance;
        if (maxDistance.HasValue && effectiveMax <= effectiveMin)
            throw new ArgumentException("Parameter 'maxDistance' must be greater than 'minDistance'.");

        Undo.RecordObject(source, "UnityMCP Set AudioSource Settings");

        if (volume.HasValue)      source.volume      = Mathf.Clamp01(volume.Value);
        if (pitch.HasValue)       source.pitch       = Mathf.Clamp(pitch.Value, -3f, 3f);
        if (loop.HasValue)        source.loop        = loop.Value;
        if (mute.HasValue)        source.mute        = mute.Value;
        if (playOnAwake.HasValue) source.playOnAwake = playOnAwake.Value;
        if (spatialBlend.HasValue) source.spatialBlend = Mathf.Clamp01(spatialBlend.Value);
        if (minDistance.HasValue) source.minDistance = minDistance.Value;
        if (maxDistance.HasValue) source.maxDistance = maxDistance.Value;

        if (rolloffMode != null)
        {
            if (Enum.TryParse<AudioRolloffMode>(rolloffMode, ignoreCase: true, out var rm))
                source.rolloffMode = rm;
            else if (int.TryParse(rolloffMode, out var rmi))
                source.rolloffMode = (AudioRolloffMode)rmi;
            else
                throw new ArgumentException($"Invalid rolloffMode '{rolloffMode}'. Expected Logarithmic, Linear, or Custom (or 0/1/2).");
        }

        bool clipApplied = false;
        if (clipAssetPath != null)
        {
            if (string.IsNullOrEmpty(clipAssetPath))
            {
                source.clip = null;
                clipApplied = true;
            }
            else
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipAssetPath);
                if (clip == null)
                    throw new ArgumentException($"No AudioClip found at path '{clipAssetPath}'. Check the asset path and ensure it is imported.");
                source.clip = clip;
                clipApplied = true;
            }
        }

        bool mixerApplied = false;
        if (mixerGroupPath != null)
        {
            if (string.IsNullOrEmpty(mixerGroupPath))
            {
                source.outputAudioMixerGroup = null;
                mixerApplied = true;
            }
            else
            {
                var parts = mixerGroupPath.Split('/', 2);
                if (parts.Length != 2)
                    throw new ArgumentException($"No AudioMixerGroup found at path '{mixerGroupPath}'. Format must be 'MixerName/GroupName'.");
                var mixerGuids = AssetDatabase.FindAssets($"t:AudioMixer {parts[0]}");
                AudioMixerGroup? foundGroup = null;
                foreach (var guid in mixerGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
                    if (mixer != null)
                    {
                        var groups = mixer.FindMatchingGroups(parts[1]);
                        if (groups != null && groups.Length > 0)
                        {
                            foundGroup = groups[0];
                            break;
                        }
                    }
                }
                if (foundGroup == null)
                    throw new ArgumentException($"No AudioMixerGroup found at path '{mixerGroupPath}'. Format must be 'MixerName/GroupName'.");
                source.outputAudioMixerGroup = foundGroup;
                mixerApplied = true;
            }
        }

        EditorUtility.SetDirty(source);

        string resultClipPath = source.clip != null ? AssetDatabase.GetAssetPath(source.clip) : "";
        string? resultMixerGroupPath = null;
        if (source.outputAudioMixerGroup != null)
        {
            var mixer = source.outputAudioMixerGroup.audioMixer;
            var groupName = source.outputAudioMixerGroup.name;
            resultMixerGroupPath = mixer != null ? $"{mixer.name}/{groupName}" : groupName;
        }

        var result = new
        {
            target = CreateObjectSummary(source.gameObject),
            component = CreateComponentSummary(source),
            settings = new
            {
                clipAssetPath = resultClipPath,
                clipName = source.clip != null ? source.clip.name : "",
                volume = source.volume,
                pitch = source.pitch,
                loop = source.loop,
                mute = source.mute,
                playOnAwake = source.playOnAwake,
                spatialBlend = source.spatialBlend,
                minDistance = source.minDistance,
                maxDistance = source.maxDistance,
                rolloffMode = source.rolloffMode.ToString(),
                mixerGroupPath = resultMixerGroupPath,
                isPlaying = source.isPlaying
            },
            applied = new
            {
                volume = volume.HasValue,
                pitch = pitch.HasValue,
                loop = loop.HasValue,
                mute = mute.HasValue,
                playOnAwake = playOnAwake.HasValue,
                spatialBlend = spatialBlend.HasValue,
                minDistance = minDistance.HasValue,
                maxDistance = maxDistance.HasValue,
                rolloffMode = rolloffMode != null,
                clipAssetPath = clipApplied,
                mixerGroupPath = mixerApplied
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildAudioPlayResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "audio.play");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var delay = ParseOptionalFloatParameter(paramsObject, "delay");

        if (!Application.isPlaying)
            throw new ArgumentException("audio.play requires the Editor to be in Play mode. AudioSource.Play() is a no-op in Edit mode.");

        if (delay.HasValue && delay.Value < 0f)
            throw new ArgumentException("Parameter 'delay' must be >= 0.");

        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var source = ResolveComponentOfTypeTarget<AudioSource>(resolvedObject, "instanceId", "AudioSource");

        if (source.clip == null)
            throw new ArgumentException("AudioSource has no clip assigned. Assign a clip via audio.setSourceSettings before calling audio.play.");

        if (delay.HasValue && delay.Value > 0f)
            source.PlayDelayed(delay.Value);
        else
            source.Play();

        var result = new
        {
            target = CreateObjectSummary(source.gameObject),
            component = CreateComponentSummary(source),
            isPlaying = source.isPlaying,
            delay = delay ?? 0f
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildAudioStopResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "audio.stop");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");

        if (!Application.isPlaying)
            throw new ArgumentException("audio.stop requires the Editor to be in Play mode. AudioSource.Stop() is a no-op in Edit mode.");

        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var source = ResolveComponentOfTypeTarget<AudioSource>(resolvedObject, "instanceId", "AudioSource");

        source.Stop();

        var result = new
        {
            target = CreateObjectSummary(source.gameObject),
            component = CreateComponentSummary(source),
            isPlaying = source.isPlaying
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildAudioPauseResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "audio.pause");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");

        if (!Application.isPlaying)
            throw new ArgumentException("audio.pause requires the Editor to be in Play mode. AudioSource.Pause() is a no-op in Edit mode.");

        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var source = ResolveComponentOfTypeTarget<AudioSource>(resolvedObject, "instanceId", "AudioSource");

        source.Pause();

        var result = new
        {
            target = CreateObjectSummary(source.gameObject),
            component = CreateComponentSummary(source),
            isPlaying = source.isPlaying
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildAudioUnpauseResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "audio.unpause");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");

        if (!Application.isPlaying)
            throw new ArgumentException("audio.unpause requires the Editor to be in Play mode. AudioSource.UnPause() is a no-op in Edit mode.");

        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var source = ResolveComponentOfTypeTarget<AudioSource>(resolvedObject, "instanceId", "AudioSource");

        source.UnPause();

        var result = new
        {
            target = CreateObjectSummary(source.gameObject),
            component = CreateComponentSummary(source),
            isPlaying = source.isPlaying
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetAudioIsPlayingResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "audio.getIsPlaying");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var source = ResolveComponentOfTypeTarget<AudioSource>(resolvedObject, "instanceId", "AudioSource");

        var result = new
        {
            target = CreateObjectSummary(source.gameObject),
            component = CreateComponentSummary(source),
            isPlaying = source.isPlaying,
            isPlayMode = Application.isPlaying
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetAudioMixerSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "audio.getMixerSettings");
        var mixerAssetPath = ParseRequiredStringParameter(paramsObject, "mixerAssetPath");

        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(mixerAssetPath);
        if (mixer == null)
            throw new ArgumentException($"No AudioMixer found at path '{mixerAssetPath}'. Verify the asset path ends with '.mixer'.");

        var snapshots = AssetDatabase.LoadAllAssetsAtPath(mixerAssetPath)
            .OfType<AudioMixerSnapshot>()
            .Select(s => new { name = s.name })
            .ToArray();

        var exposedParams = new List<object>();
        var so = new SerializedObject(mixer);
        var exposedParamsProp = so.FindProperty("m_ExposedParameters");
        if (exposedParamsProp != null)
        {
            for (int i = 0; i < exposedParamsProp.arraySize; i++)
            {
                var elem = exposedParamsProp.GetArrayElementAtIndex(i);
                var nameProp = elem.FindPropertyRelative("name");
                if (nameProp == null) continue;
                var paramName = nameProp.stringValue;
                float paramValue = 0f;
                mixer.GetFloat(paramName, out paramValue);
                exposedParams.Add(new { name = paramName, value = paramValue });
            }
        }

        var result = new
        {
            mixerAssetPath,
            name = mixer.name,
            snapshots,
            exposedParameters = exposedParams.ToArray()
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetAudioMixerParameterResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "audio.setMixerParameter");
        var mixerAssetPath = ParseRequiredStringParameter(paramsObject, "mixerAssetPath");
        var parameterName  = ParseRequiredStringParameter(paramsObject, "parameterName");
        var value          = ParseRequiredFloatParameter(paramsObject, "value");

        if (string.IsNullOrEmpty(parameterName))
            throw new ArgumentException("Parameter 'parameterName' is required and must be non-empty.");

        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(mixerAssetPath);
        if (mixer == null)
            throw new ArgumentException($"No AudioMixer found at path '{mixerAssetPath}'.");

        // Step 1: Find the GUID for this parameter from m_ExposedParameters.
        // m_ExposedParameters only stores { m_GUID, name } — no value field.
        // The actual float values live in each snapshot's m_FloatValues, keyed by GUID.
        var so = new SerializedObject(mixer);
        var exposedParams = so.FindProperty("m_ExposedParameters");

        string? targetGuid = null;
        for (int i = 0; i < exposedParams.arraySize; i++)
        {
            var param = exposedParams.GetArrayElementAtIndex(i);
            var nameProperty = param.FindPropertyRelative("name");
            if (nameProperty != null && nameProperty.stringValue == parameterName)
            {
                var guidProperty = param.FindPropertyRelative("m_GUID");
                targetGuid = guidProperty?.stringValue;
                break;
            }
        }

        if (targetGuid == null)
            throw new ArgumentException($"Parameter '{parameterName}' not found in mixer exposed parameters on '{mixer.name}'. Use audio.getMixerSettings to list exposed parameters.");

        // Step 2: Read the current value and write the new value into all snapshots.
        // Values are stored in AudioMixerSnapshotController.m_FloatValues as a
        // serialized dictionary with keys "first" (GUID) and "second" (float value).
        float previousValue = 0f;
        bool valueWritten = false;
        var mixerPath = AssetDatabase.GetAssetPath(mixer);
        var snapshots = AssetDatabase.LoadAllAssetsAtPath(mixerPath)
            .OfType<AudioMixerSnapshot>()
            .ToArray();

        foreach (var snapshot in snapshots)
        {
            var snapshotSo = new SerializedObject(snapshot);
            var floatValues = snapshotSo.FindProperty("m_FloatValues");
            if (floatValues == null) continue;

            for (int i = 0; i < floatValues.arraySize; i++)
            {
                var entry = floatValues.GetArrayElementAtIndex(i);
                var key = entry.FindPropertyRelative("first");
                if (key != null && key.stringValue == targetGuid)
                {
                    var valueProperty = entry.FindPropertyRelative("second");
                    if (valueProperty != null)
                    {
                        snapshotSo.Update();
                        if (!valueWritten) previousValue = valueProperty.floatValue;
                        valueProperty.floatValue = value;
                        snapshotSo.ApplyModifiedProperties();
                        EditorUtility.SetDirty(snapshot);
                        valueWritten = true;
                    }
                    break;
                }
            }
        }

        EditorUtility.SetDirty(mixer);
        AssetDatabase.SaveAssets();

        if (!valueWritten)
            throw new ArgumentException($"Parameter '{parameterName}' (GUID: {targetGuid}) found in exposed parameters but no matching entry in snapshot m_FloatValues. The parameter may not have a value set in any snapshot.");

        var result = new
        {
            mixerAssetPath,
            parameterName,
            value,
            previousValue
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static AudioListener ResolveAudioListener(JObject paramsObject, string commandName)
    {
        var instanceIdToken = paramsObject["instanceId"];
        if (instanceIdToken != null && instanceIdToken.Type != JTokenType.Null)
        {
            var instanceId = (int)instanceIdToken;
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            return ResolveComponentOfTypeTarget<AudioListener>(resolvedObject, "instanceId", "AudioListener");
        }

        var listener = UnityEngine.Object.FindFirstObjectByType<AudioListener>();
        if (listener == null)
            throw new ArgumentException("No AudioListener found in the scene. Add an AudioListener component to a GameObject (typically the Main Camera).");
        return listener;
    }

    private static string BuildGetAudioListenerSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = root["params"] as JObject ?? new JObject();
        var listener = ResolveAudioListener(paramsObject, "audio.getListenerSettings");

        var result = new
        {
            target = CreateObjectSummary(listener.gameObject),
            component = CreateComponentSummary(listener),
            settings = new
            {
                volume = AudioListener.volume,
                pause = AudioListener.pause,
                velocityUpdateMode = listener.velocityUpdateMode.ToString()
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetAudioListenerSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = root["params"] as JObject ?? new JObject();

        var volume             = ParseOptionalFloatParameter(paramsObject, "volume");
        var pause              = ParseOptionalBooleanValueParameter(paramsObject, "pause");
        var velocityUpdateMode = ParseOptionalStringParameter(paramsObject, "velocityUpdateMode");

        if (!volume.HasValue && !pause.HasValue && velocityUpdateMode == null)
            throw new ArgumentException("At least one AudioListener property must be specified: volume, pause, or velocityUpdateMode.");

        var listener = ResolveAudioListener(paramsObject, "audio.setListenerSettings");

        if (volume.HasValue)
            AudioListener.volume = Mathf.Clamp01(volume.Value);

        if (pause.HasValue)
            AudioListener.pause = pause.Value;

        if (velocityUpdateMode != null)
        {
            if (Enum.TryParse<AudioVelocityUpdateMode>(velocityUpdateMode, ignoreCase: true, out var vm))
            {
                Undo.RecordObject(listener, "UnityMCP Set AudioListener Settings");
                listener.velocityUpdateMode = vm;
                EditorUtility.SetDirty(listener);
            }
            else if (int.TryParse(velocityUpdateMode, out var vmi))
            {
                Undo.RecordObject(listener, "UnityMCP Set AudioListener Settings");
                listener.velocityUpdateMode = (AudioVelocityUpdateMode)vmi;
                EditorUtility.SetDirty(listener);
            }
            else
            {
                throw new ArgumentException($"Parameter 'velocityUpdateMode' must be a valid AudioVelocityUpdateMode: Auto, Fixed, Dynamic (or integer 0, 1, 2).");
            }
        }

        var result = new
        {
            target = CreateObjectSummary(listener.gameObject),
            component = CreateComponentSummary(listener),
            settings = new
            {
                volume = AudioListener.volume,
                pause = AudioListener.pause,
                velocityUpdateMode = listener.velocityUpdateMode.ToString()
            },
            applied = new
            {
                volume = volume.HasValue,
                pause = pause.HasValue,
                velocityUpdateMode = velocityUpdateMode != null
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    // ── Batch 7: Test Runner ──────────────────────────────────────────────

    private static bool _testRunInProgress;
    private static string? _lastTestMode;
    private static object? _lastTestResults;
    private static TestRunnerApi? _activeTestRunnerApi;

    private sealed class TestRunCallbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun)
        {
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            _lastTestResults = BuildTestResultData(result);
            _testRunInProgress = false;

            if (_activeTestRunnerApi != null)
            {
                _activeTestRunnerApi.UnregisterCallbacks(this);
            }
        }

        public void TestStarted(ITestAdaptor test)
        {
        }

        public void TestFinished(ITestResultAdaptor result)
        {
        }
    }

    private static string BuildListTestsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "testRunner.listTests");
        var mode = ParseRequiredStringParameter(paramsObject, "mode");
        var testMode = ParseTestMode(mode);

        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        ITestAdaptor? testTree = null;

        api.RetrieveTestList(testMode, adaptor => { testTree = adaptor; });

        if (testTree == null)
        {
            var result = new
            {
                mode,
                count = 0,
                tests = new List<object>()
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        var tests = new List<object>();
        CollectTestCases(testTree, tests);

        var response = new
        {
            mode,
            count = tests.Count,
            tests
        };

        return UnityMcpProtocol.CreateResult(idToken, response);
    }

    private static string BuildRunTestsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "testRunner.run");
        var mode = ParseRequiredStringParameter(paramsObject, "mode");
        var testFilter = ParseOptionalStringParameter(paramsObject, "testFilter");
        var testMode = ParseTestMode(mode);

        if (_testRunInProgress)
        {
            throw new ArgumentException("A test run is already in progress. Use testRunner.cancel to stop it, or testRunner.getResults to check status.");
        }

        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        _activeTestRunnerApi = api;
        _testRunInProgress = true;
        _lastTestMode = mode;
        _lastTestResults = null;

        var callbacks = new TestRunCallbacks();
        api.RegisterCallbacks(callbacks);

        var filter = new Filter
        {
            testMode = testMode
        };

        if (!string.IsNullOrEmpty(testFilter))
        {
            filter.testNames = new[] { testFilter };
        }

        api.Execute(new ExecutionSettings(filter));

        var result = new
        {
            started = true,
            mode,
            testFilter = testFilter ?? (object?)null,
            message = $"Test run started in {mode} mode. Use testRunner.getResults to poll for results."
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetTestResultsResponse(JToken idToken)
    {
        if (_testRunInProgress)
        {
            var result = new
            {
                status = "running",
                mode = _lastTestMode,
                message = "Test run is still in progress."
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        if (_lastTestResults == null)
        {
            var result = new
            {
                status = "none",
                message = "No test results available. Use testRunner.run to start a test run."
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        return UnityMcpProtocol.CreateResult(idToken, _lastTestResults);
    }

    private static string BuildCancelTestRunResponse(JToken idToken)
    {
        if (!_testRunInProgress)
        {
            var result = new
            {
                cancelled = false,
                message = "No test run is currently in progress."
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        // Unity Test Runner API does not expose a direct cancel method.
        // Reset state so a new run can be started.
        _testRunInProgress = false;

        var response = new
        {
            cancelled = true,
            message = "Test run state has been reset. Note: the underlying Unity test execution may continue to completion."
        };

        return UnityMcpProtocol.CreateResult(idToken, response);
    }

    private static TestMode ParseTestMode(string mode)
    {
        return mode.ToLowerInvariant() switch
        {
            "editmode" => TestMode.EditMode,
            "playmode" => TestMode.PlayMode,
            _ => throw new ArgumentException("Parameter 'mode' must be 'editMode' or 'playMode'.")
        };
    }

    private static void CollectTestCases(ITestAdaptor adaptor, List<object> tests)
    {
        if (!adaptor.HasChildren)
        {
            tests.Add(new
            {
                fullName = adaptor.FullName,
                name = adaptor.Name,
                typeName = adaptor.TypeInfo?.FullName,
                methodName = adaptor.Name,
                testCaseCount = adaptor.TestCaseCount,
                runState = adaptor.RunState.ToString()
            });

            return;
        }

        foreach (var child in adaptor.Children)
        {
            CollectTestCases(child, tests);
        }
    }

    private static object BuildTestResultData(ITestResultAdaptor result)
    {
        var testResults = new List<object>();
        var counts = new TestResultCounts();
        CollectTestResults(result, testResults, counts);

        return new
        {
            status = "completed",
            mode = _lastTestMode,
            summary = new
            {
                total = testResults.Count,
                passed = counts.Passed,
                failed = counts.Failed,
                skipped = counts.Skipped,
                inconclusive = counts.Inconclusive
            },
            tests = testResults
        };
    }

    private sealed class TestResultCounts
    {
        public int Passed;
        public int Failed;
        public int Skipped;
        public int Inconclusive;
    }

    private static void CollectTestResults(ITestResultAdaptor result, List<object> results, TestResultCounts counts)
    {
        if (!result.HasChildren)
        {
            var state = result.ResultState.ToString();

            if (state.Contains("Pass", StringComparison.OrdinalIgnoreCase))
            {
                counts.Passed++;
            }
            else if (state.Contains("Fail", StringComparison.OrdinalIgnoreCase) ||
                     state.Contains("Error", StringComparison.OrdinalIgnoreCase))
            {
                counts.Failed++;
            }
            else if (state.Contains("Skip", StringComparison.OrdinalIgnoreCase) ||
                     state.Contains("Ignore", StringComparison.OrdinalIgnoreCase))
            {
                counts.Skipped++;
            }
            else if (state.Contains("Inconclusive", StringComparison.OrdinalIgnoreCase))
            {
                counts.Inconclusive++;
            }

            results.Add(new
            {
                fullName = result.FullName,
                name = result.Name,
                resultState = state,
                duration = result.Duration,
                message = result.Message,
                stackTrace = result.StackTrace
            });

            return;
        }

        foreach (var child in result.Children)
        {
            CollectTestResults(child, results, counts);
        }
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

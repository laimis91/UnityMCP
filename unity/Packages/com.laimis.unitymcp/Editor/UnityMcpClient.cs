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
using UnityEngine.SceneManagement;

namespace UnityMcp.Editor
{

internal sealed class UnityMcpClient : IDisposable
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
        var drag = ParseOptionalFloatParameter(paramsObject, "drag");
        var angularDrag = ParseOptionalFloatParameter(paramsObject, "angularDrag");
        var useGravity = ParseOptionalBooleanValueParameter(paramsObject, "useGravity");
        var isKinematic = ParseOptionalBooleanValueParameter(paramsObject, "isKinematic");
        var detectCollisions = ParseOptionalBooleanValueParameter(paramsObject, "detectCollisions");
        var constraints = ParseOptionalEnumParameter<RigidbodyConstraints>(paramsObject, "constraints");
        var interpolation = ParseOptionalEnumParameter<RigidbodyInterpolation>(paramsObject, "interpolation");
        var collisionDetectionMode = ParseOptionalEnumParameter<CollisionDetectionMode>(paramsObject, "collisionDetectionMode");

        if (!mass.HasValue &&
            !drag.HasValue &&
            !angularDrag.HasValue &&
            !useGravity.HasValue &&
            !isKinematic.HasValue &&
            !detectCollisions.HasValue &&
            !constraints.HasValue &&
            !interpolation.HasValue &&
            !collisionDetectionMode.HasValue)
        {
            throw new ArgumentException(
                "At least one rigidbody setting must be provided: mass, drag, angularDrag, useGravity, isKinematic, detectCollisions, constraints, interpolation, or collisionDetectionMode.");
        }

        if (mass.HasValue && mass.Value <= 0f)
        {
            throw new ArgumentException("Parameter 'mass' must be greater than 0.");
        }

        if (drag.HasValue && drag.Value < 0f)
        {
            throw new ArgumentException("Parameter 'drag' must be greater than or equal to 0.");
        }

        if (angularDrag.HasValue && angularDrag.Value < 0f)
        {
            throw new ArgumentException("Parameter 'angularDrag' must be greater than or equal to 0.");
        }

        Undo.RecordObject(rigidbody, "UnityMCP Set Rigidbody Settings");

        if (mass.HasValue)
        {
            rigidbody.mass = mass.Value;
        }

        #pragma warning disable CS0618
        if (drag.HasValue)
        {
            rigidbody.drag = drag.Value;
        }

        if (angularDrag.HasValue)
        {
            rigidbody.angularDrag = angularDrag.Value;
        }
        #pragma warning restore CS0618

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
                drag = drag.HasValue,
                angularDrag = angularDrag.HasValue,
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

    private static string BuildGetRigidbody2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "rigidbody2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var rigidbody = ResolveComponentOfTypeTarget<Rigidbody2D>(resolvedObject, "instanceId", "Rigidbody2D");

        var result = new
        {
            target = CreateObjectSummary(rigidbody.gameObject),
            component = CreateComponentSummary(rigidbody),
            settings = CreateRigidbody2DSettingsSnapshot(rigidbody)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetRigidbody2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "rigidbody2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var rigidbody = ResolveComponentOfTypeTarget<Rigidbody2D>(resolvedObject, "instanceId", "Rigidbody2D");

        var bodyType = ParseOptionalEnumParameter<RigidbodyType2D>(paramsObject, "bodyType");
        var simulated = ParseOptionalBooleanValueParameter(paramsObject, "simulated");
        var useAutoMass = ParseOptionalBooleanValueParameter(paramsObject, "useAutoMass");
        var mass = ParseOptionalFloatParameter(paramsObject, "mass");
        var gravityScale = ParseOptionalFloatParameter(paramsObject, "gravityScale");
        var constraints = ParseOptionalEnumParameter<RigidbodyConstraints2D>(paramsObject, "constraints");
        var interpolation = ParseOptionalEnumParameter<RigidbodyInterpolation2D>(paramsObject, "interpolation");
        var collisionDetectionMode = ParseOptionalEnumParameter<CollisionDetectionMode2D>(paramsObject, "collisionDetectionMode");
        var sleepMode = ParseOptionalEnumParameter<RigidbodySleepMode2D>(paramsObject, "sleepMode");

        if (!bodyType.HasValue &&
            !simulated.HasValue &&
            !useAutoMass.HasValue &&
            !mass.HasValue &&
            !gravityScale.HasValue &&
            !constraints.HasValue &&
            !interpolation.HasValue &&
            !collisionDetectionMode.HasValue &&
            !sleepMode.HasValue)
        {
            throw new ArgumentException("At least one Rigidbody2D setting must be provided: bodyType, simulated, useAutoMass, mass, gravityScale, constraints, interpolation, collisionDetectionMode, or sleepMode.");
        }

        if (mass.HasValue && mass.Value <= 0f)
        {
            throw new ArgumentException("Parameter 'mass' must be greater than 0.");
        }

        Undo.RecordObject(rigidbody, "UnityMCP Set Rigidbody2D Settings");

        if (bodyType.HasValue)
        {
            rigidbody.bodyType = bodyType.Value;
        }

        if (simulated.HasValue)
        {
            rigidbody.simulated = simulated.Value;
        }

        if (useAutoMass.HasValue)
        {
            rigidbody.useAutoMass = useAutoMass.Value;
        }

        if (mass.HasValue)
        {
            rigidbody.mass = mass.Value;
        }

        if (gravityScale.HasValue)
        {
            rigidbody.gravityScale = gravityScale.Value;
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

        if (sleepMode.HasValue)
        {
            rigidbody.sleepMode = sleepMode.Value;
        }

        EditorUtility.SetDirty(rigidbody);

        var result = new
        {
            target = CreateObjectSummary(rigidbody.gameObject),
            component = CreateComponentSummary(rigidbody),
            settings = CreateRigidbody2DSettingsSnapshot(rigidbody),
            applied = new
            {
                bodyType = bodyType.HasValue,
                simulated = simulated.HasValue,
                useAutoMass = useAutoMass.HasValue,
                mass = mass.HasValue,
                gravityScale = gravityScale.HasValue,
                constraints = constraints.HasValue,
                interpolation = interpolation.HasValue,
                collisionDetectionMode = collisionDetectionMode.HasValue,
                sleepMode = sleepMode.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "collider2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<Collider2D>(resolvedObject, "instanceId", "Collider2D");

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateCollider2DSettingsSnapshot(collider)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "collider2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<Collider2D>(resolvedObject, "instanceId", "Collider2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var isTrigger = ParseOptionalBooleanValueParameter(paramsObject, "isTrigger");
        var usedByEffector = ParseOptionalBooleanValueParameter(paramsObject, "usedByEffector");
        var offset = ParseOptionalVector2Parameter(paramsObject, "offset");
        var density = ParseOptionalFloatParameter(paramsObject, "density");

        if (!enabled.HasValue &&
            !isTrigger.HasValue &&
            !usedByEffector.HasValue &&
            !offset.HasValue &&
            !density.HasValue)
        {
            throw new ArgumentException("At least one Collider2D setting must be provided: enabled, isTrigger, usedByEffector, offset, or density.");
        }

        ValidateCommonCollider2DSettingValues(density);

        Undo.RecordObject(collider, "UnityMCP Set Collider2D Settings");
        ApplyCommonCollider2DSettings(collider, enabled, isTrigger, usedByEffector, offset, density);
        EditorUtility.SetDirty(collider);

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateCollider2DSettingsSnapshot(collider),
            applied = new
            {
                enabled = enabled.HasValue,
                isTrigger = isTrigger.HasValue,
                usedByEffector = usedByEffector.HasValue,
                offset = offset.HasValue,
                density = density.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetBoxCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "boxCollider2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<BoxCollider2D>(resolvedObject, "instanceId", "BoxCollider2D");

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateBoxCollider2DSettingsSnapshot(collider)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetBoxCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "boxCollider2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<BoxCollider2D>(resolvedObject, "instanceId", "BoxCollider2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var isTrigger = ParseOptionalBooleanValueParameter(paramsObject, "isTrigger");
        var usedByEffector = ParseOptionalBooleanValueParameter(paramsObject, "usedByEffector");
        var offset = ParseOptionalVector2Parameter(paramsObject, "offset");
        var density = ParseOptionalFloatParameter(paramsObject, "density");
        var size = ParseOptionalVector2Parameter(paramsObject, "size");
        var edgeRadius = ParseOptionalFloatParameter(paramsObject, "edgeRadius");

        if (!enabled.HasValue &&
            !isTrigger.HasValue &&
            !usedByEffector.HasValue &&
            !offset.HasValue &&
            !density.HasValue &&
            !size.HasValue &&
            !edgeRadius.HasValue)
        {
            throw new ArgumentException("At least one BoxCollider2D setting must be provided: enabled, isTrigger, usedByEffector, offset, density, size, or edgeRadius.");
        }

        ValidateCommonCollider2DSettingValues(density);
        ValidatePositiveVector2(size, "size", "Parameter 'size' must contain positive values for BoxCollider2D width and height.");
        if (edgeRadius.HasValue && edgeRadius.Value < 0f)
        {
            throw new ArgumentException("Parameter 'edgeRadius' must be greater than or equal to 0.");
        }

        Undo.RecordObject(collider, "UnityMCP Set BoxCollider2D Settings");
        ApplyCommonCollider2DSettings(collider, enabled, isTrigger, usedByEffector, offset, density);

        if (size.HasValue)
        {
            collider.size = size.Value;
        }

        if (edgeRadius.HasValue)
        {
            collider.edgeRadius = edgeRadius.Value;
        }

        EditorUtility.SetDirty(collider);

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateBoxCollider2DSettingsSnapshot(collider),
            applied = new
            {
                enabled = enabled.HasValue,
                isTrigger = isTrigger.HasValue,
                usedByEffector = usedByEffector.HasValue,
                offset = offset.HasValue,
                density = density.HasValue,
                size = size.HasValue,
                edgeRadius = edgeRadius.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetCircleCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "circleCollider2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<CircleCollider2D>(resolvedObject, "instanceId", "CircleCollider2D");

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateCircleCollider2DSettingsSnapshot(collider)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetCircleCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "circleCollider2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<CircleCollider2D>(resolvedObject, "instanceId", "CircleCollider2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var isTrigger = ParseOptionalBooleanValueParameter(paramsObject, "isTrigger");
        var usedByEffector = ParseOptionalBooleanValueParameter(paramsObject, "usedByEffector");
        var offset = ParseOptionalVector2Parameter(paramsObject, "offset");
        var density = ParseOptionalFloatParameter(paramsObject, "density");
        var radius = ParseOptionalFloatParameter(paramsObject, "radius");

        if (!enabled.HasValue &&
            !isTrigger.HasValue &&
            !usedByEffector.HasValue &&
            !offset.HasValue &&
            !density.HasValue &&
            !radius.HasValue)
        {
            throw new ArgumentException("At least one CircleCollider2D setting must be provided: enabled, isTrigger, usedByEffector, offset, density, or radius.");
        }

        ValidateCommonCollider2DSettingValues(density);
        if (radius.HasValue && radius.Value <= 0f)
        {
            throw new ArgumentException("Parameter 'radius' must be greater than 0.");
        }

        Undo.RecordObject(collider, "UnityMCP Set CircleCollider2D Settings");
        ApplyCommonCollider2DSettings(collider, enabled, isTrigger, usedByEffector, offset, density);

        if (radius.HasValue)
        {
            collider.radius = radius.Value;
        }

        EditorUtility.SetDirty(collider);

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateCircleCollider2DSettingsSnapshot(collider),
            applied = new
            {
                enabled = enabled.HasValue,
                isTrigger = isTrigger.HasValue,
                usedByEffector = usedByEffector.HasValue,
                offset = offset.HasValue,
                density = density.HasValue,
                radius = radius.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetCapsuleCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "capsuleCollider2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<CapsuleCollider2D>(resolvedObject, "instanceId", "CapsuleCollider2D");

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateCapsuleCollider2DSettingsSnapshot(collider)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetCapsuleCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "capsuleCollider2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<CapsuleCollider2D>(resolvedObject, "instanceId", "CapsuleCollider2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var isTrigger = ParseOptionalBooleanValueParameter(paramsObject, "isTrigger");
        var usedByEffector = ParseOptionalBooleanValueParameter(paramsObject, "usedByEffector");
        var offset = ParseOptionalVector2Parameter(paramsObject, "offset");
        var density = ParseOptionalFloatParameter(paramsObject, "density");
        var size = ParseOptionalVector2Parameter(paramsObject, "size");
        var direction = ParseOptionalEnumParameter<CapsuleDirection2D>(paramsObject, "direction");

        if (!enabled.HasValue &&
            !isTrigger.HasValue &&
            !usedByEffector.HasValue &&
            !offset.HasValue &&
            !density.HasValue &&
            !size.HasValue &&
            !direction.HasValue)
        {
            throw new ArgumentException("At least one CapsuleCollider2D setting must be provided: enabled, isTrigger, usedByEffector, offset, density, size, or direction.");
        }

        ValidateCommonCollider2DSettingValues(density);
        ValidatePositiveVector2(size, "size", "Parameter 'size' must contain positive values for CapsuleCollider2D width and height.");

        Undo.RecordObject(collider, "UnityMCP Set CapsuleCollider2D Settings");
        ApplyCommonCollider2DSettings(collider, enabled, isTrigger, usedByEffector, offset, density);

        if (size.HasValue)
        {
            collider.size = size.Value;
        }

        if (direction.HasValue)
        {
            collider.direction = direction.Value;
        }

        EditorUtility.SetDirty(collider);

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateCapsuleCollider2DSettingsSnapshot(collider),
            applied = new
            {
                enabled = enabled.HasValue,
                isTrigger = isTrigger.HasValue,
                usedByEffector = usedByEffector.HasValue,
                offset = offset.HasValue,
                density = density.HasValue,
                size = size.HasValue,
                direction = direction.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetPolygonCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "polygonCollider2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<PolygonCollider2D>(resolvedObject, "instanceId", "PolygonCollider2D");

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreatePolygonCollider2DSettingsSnapshot(collider)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetPolygonCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "polygonCollider2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<PolygonCollider2D>(resolvedObject, "instanceId", "PolygonCollider2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var isTrigger = ParseOptionalBooleanValueParameter(paramsObject, "isTrigger");
        var usedByEffector = ParseOptionalBooleanValueParameter(paramsObject, "usedByEffector");
        var offset = ParseOptionalVector2Parameter(paramsObject, "offset");
        var density = ParseOptionalFloatParameter(paramsObject, "density");

        if (!enabled.HasValue &&
            !isTrigger.HasValue &&
            !usedByEffector.HasValue &&
            !offset.HasValue &&
            !density.HasValue)
        {
            throw new ArgumentException("At least one PolygonCollider2D setting must be provided: enabled, isTrigger, usedByEffector, offset, or density.");
        }

        ValidateCommonCollider2DSettingValues(density);

        Undo.RecordObject(collider, "UnityMCP Set PolygonCollider2D Settings");
        ApplyCommonCollider2DSettings(collider, enabled, isTrigger, usedByEffector, offset, density);
        EditorUtility.SetDirty(collider);

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreatePolygonCollider2DSettingsSnapshot(collider),
            applied = new
            {
                enabled = enabled.HasValue,
                isTrigger = isTrigger.HasValue,
                usedByEffector = usedByEffector.HasValue,
                offset = offset.HasValue,
                density = density.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetEdgeCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "edgeCollider2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<EdgeCollider2D>(resolvedObject, "instanceId", "EdgeCollider2D");

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateEdgeCollider2DSettingsSnapshot(collider)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetEdgeCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "edgeCollider2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<EdgeCollider2D>(resolvedObject, "instanceId", "EdgeCollider2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var isTrigger = ParseOptionalBooleanValueParameter(paramsObject, "isTrigger");
        var usedByEffector = ParseOptionalBooleanValueParameter(paramsObject, "usedByEffector");
        var offset = ParseOptionalVector2Parameter(paramsObject, "offset");
        var density = ParseOptionalFloatParameter(paramsObject, "density");
        var edgeRadius = ParseOptionalFloatParameter(paramsObject, "edgeRadius");

        if (!enabled.HasValue &&
            !isTrigger.HasValue &&
            !usedByEffector.HasValue &&
            !offset.HasValue &&
            !density.HasValue &&
            !edgeRadius.HasValue)
        {
            throw new ArgumentException("At least one EdgeCollider2D setting must be provided: enabled, isTrigger, usedByEffector, offset, density, or edgeRadius.");
        }

        ValidateCommonCollider2DSettingValues(density);
        if (edgeRadius.HasValue && edgeRadius.Value < 0f)
        {
            throw new ArgumentException("Parameter 'edgeRadius' must be greater than or equal to 0.");
        }

        Undo.RecordObject(collider, "UnityMCP Set EdgeCollider2D Settings");
        ApplyCommonCollider2DSettings(collider, enabled, isTrigger, usedByEffector, offset, density);

        if (edgeRadius.HasValue)
        {
            collider.edgeRadius = edgeRadius.Value;
        }

        EditorUtility.SetDirty(collider);

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateEdgeCollider2DSettingsSnapshot(collider),
            applied = new
            {
                enabled = enabled.HasValue,
                isTrigger = isTrigger.HasValue,
                usedByEffector = usedByEffector.HasValue,
                offset = offset.HasValue,
                density = density.HasValue,
                edgeRadius = edgeRadius.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetCompositeCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "compositeCollider2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<CompositeCollider2D>(resolvedObject, "instanceId", "CompositeCollider2D");

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateCompositeCollider2DSettingsSnapshot(collider)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetCompositeCollider2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "compositeCollider2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<CompositeCollider2D>(resolvedObject, "instanceId", "CompositeCollider2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var isTrigger = ParseOptionalBooleanValueParameter(paramsObject, "isTrigger");
        var usedByEffector = ParseOptionalBooleanValueParameter(paramsObject, "usedByEffector");
        var offset = ParseOptionalVector2Parameter(paramsObject, "offset");
        var density = ParseOptionalFloatParameter(paramsObject, "density");
        var geometryType = ParseOptionalEnumParameter<CompositeCollider2D.GeometryType>(paramsObject, "geometryType");
        var generationType = ParseOptionalEnumParameter<CompositeCollider2D.GenerationType>(paramsObject, "generationType");

        if (!enabled.HasValue &&
            !isTrigger.HasValue &&
            !usedByEffector.HasValue &&
            !offset.HasValue &&
            !density.HasValue &&
            !geometryType.HasValue &&
            !generationType.HasValue)
        {
            throw new ArgumentException("At least one CompositeCollider2D setting must be provided: enabled, isTrigger, usedByEffector, offset, density, geometryType, or generationType.");
        }

        ValidateCommonCollider2DSettingValues(density);

        Undo.RecordObject(collider, "UnityMCP Set CompositeCollider2D Settings");
        ApplyCommonCollider2DSettings(collider, enabled, isTrigger, usedByEffector, offset, density);

        if (geometryType.HasValue)
        {
            collider.geometryType = geometryType.Value;
        }

        if (generationType.HasValue)
        {
            collider.generationType = generationType.Value;
        }

        EditorUtility.SetDirty(collider);

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateCompositeCollider2DSettingsSnapshot(collider),
            applied = new
            {
                enabled = enabled.HasValue,
                isTrigger = isTrigger.HasValue,
                usedByEffector = usedByEffector.HasValue,
                offset = offset.HasValue,
                density = density.HasValue,
                geometryType = geometryType.HasValue,
                generationType = generationType.HasValue
            }
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

    private static string BuildGetBoxColliderSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "boxCollider.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<BoxCollider>(resolvedObject, "instanceId", "BoxCollider");

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateBoxColliderSettingsSnapshot(collider)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetBoxColliderSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "boxCollider.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<BoxCollider>(resolvedObject, "instanceId", "BoxCollider");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var isTrigger = ParseOptionalBooleanValueParameter(paramsObject, "isTrigger");
        var contactOffset = ParseOptionalFloatParameter(paramsObject, "contactOffset");
        var center = ParseOptionalVector3Parameter(paramsObject, "center");
        var size = ParseOptionalVector3Parameter(paramsObject, "size");

        if (!enabled.HasValue && !isTrigger.HasValue && !contactOffset.HasValue && !center.HasValue && !size.HasValue)
        {
            throw new ArgumentException("At least one BoxCollider setting must be provided: enabled, isTrigger, contactOffset, center, or size.");
        }

        ValidateCommonColliderSettingValues(contactOffset);
        ValidatePositiveVector3(size, "size", "Parameter 'size' must contain positive values for all BoxCollider axes.");

        Undo.RecordObject(collider, "UnityMCP Set BoxCollider Settings");
        ApplyCommonColliderSettings(collider, enabled, isTrigger, contactOffset);

        if (center.HasValue)
        {
            collider.center = center.Value;
        }

        if (size.HasValue)
        {
            collider.size = size.Value;
        }

        EditorUtility.SetDirty(collider);

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateBoxColliderSettingsSnapshot(collider),
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

    private static string BuildGetSphereColliderSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "sphereCollider.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<SphereCollider>(resolvedObject, "instanceId", "SphereCollider");

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateSphereColliderSettingsSnapshot(collider)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetSphereColliderSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "sphereCollider.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<SphereCollider>(resolvedObject, "instanceId", "SphereCollider");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var isTrigger = ParseOptionalBooleanValueParameter(paramsObject, "isTrigger");
        var contactOffset = ParseOptionalFloatParameter(paramsObject, "contactOffset");
        var center = ParseOptionalVector3Parameter(paramsObject, "center");
        var radius = ParseOptionalFloatParameter(paramsObject, "radius");

        if (!enabled.HasValue && !isTrigger.HasValue && !contactOffset.HasValue && !center.HasValue && !radius.HasValue)
        {
            throw new ArgumentException("At least one SphereCollider setting must be provided: enabled, isTrigger, contactOffset, center, or radius.");
        }

        ValidateCommonColliderSettingValues(contactOffset);
        if (radius.HasValue && radius.Value <= 0f)
        {
            throw new ArgumentException("Parameter 'radius' must be greater than 0.");
        }

        Undo.RecordObject(collider, "UnityMCP Set SphereCollider Settings");
        ApplyCommonColliderSettings(collider, enabled, isTrigger, contactOffset);

        if (center.HasValue)
        {
            collider.center = center.Value;
        }

        if (radius.HasValue)
        {
            collider.radius = radius.Value;
        }

        EditorUtility.SetDirty(collider);

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateSphereColliderSettingsSnapshot(collider),
            applied = new
            {
                enabled = enabled.HasValue,
                isTrigger = isTrigger.HasValue,
                contactOffset = contactOffset.HasValue,
                center = center.HasValue,
                radius = radius.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetCapsuleColliderSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "capsuleCollider.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<CapsuleCollider>(resolvedObject, "instanceId", "CapsuleCollider");

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateCapsuleColliderSettingsSnapshot(collider)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetCapsuleColliderSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "capsuleCollider.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<CapsuleCollider>(resolvedObject, "instanceId", "CapsuleCollider");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var isTrigger = ParseOptionalBooleanValueParameter(paramsObject, "isTrigger");
        var contactOffset = ParseOptionalFloatParameter(paramsObject, "contactOffset");
        var center = ParseOptionalVector3Parameter(paramsObject, "center");
        var radius = ParseOptionalFloatParameter(paramsObject, "radius");
        var height = ParseOptionalFloatParameter(paramsObject, "height");
        var direction = ParseOptionalCapsuleDirectionParameter(paramsObject, "direction");

        if (!enabled.HasValue &&
            !isTrigger.HasValue &&
            !contactOffset.HasValue &&
            !center.HasValue &&
            !radius.HasValue &&
            !height.HasValue &&
            !direction.HasValue)
        {
            throw new ArgumentException("At least one CapsuleCollider setting must be provided: enabled, isTrigger, contactOffset, center, radius, height, or direction.");
        }

        ValidateCommonColliderSettingValues(contactOffset);
        if (radius.HasValue && radius.Value <= 0f)
        {
            throw new ArgumentException("Parameter 'radius' must be greater than 0.");
        }

        if (height.HasValue && height.Value <= 0f)
        {
            throw new ArgumentException("Parameter 'height' must be greater than 0.");
        }

        if (direction.HasValue && !IsValidCapsuleDirection(direction.Value))
        {
            throw new ArgumentException("Parameter 'direction' must be a valid CapsuleDirection value (X, Y, Z or 0, 1, 2).");
        }

        Undo.RecordObject(collider, "UnityMCP Set CapsuleCollider Settings");
        ApplyCommonColliderSettings(collider, enabled, isTrigger, contactOffset);

        if (center.HasValue)
        {
            collider.center = center.Value;
        }

        if (radius.HasValue)
        {
            collider.radius = radius.Value;
        }

        if (height.HasValue)
        {
            collider.height = height.Value;
        }

        if (direction.HasValue)
        {
            collider.direction = direction.Value;
        }

        EditorUtility.SetDirty(collider);

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateCapsuleColliderSettingsSnapshot(collider),
            applied = new
            {
                enabled = enabled.HasValue,
                isTrigger = isTrigger.HasValue,
                contactOffset = contactOffset.HasValue,
                center = center.HasValue,
                radius = radius.HasValue,
                height = height.HasValue,
                direction = direction.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetMeshColliderSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "meshCollider.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<MeshCollider>(resolvedObject, "instanceId", "MeshCollider");

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateMeshColliderSettingsSnapshot(collider)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetMeshColliderSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "meshCollider.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var collider = ResolveComponentOfTypeTarget<MeshCollider>(resolvedObject, "instanceId", "MeshCollider");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var isTrigger = ParseOptionalBooleanValueParameter(paramsObject, "isTrigger");
        var contactOffset = ParseOptionalFloatParameter(paramsObject, "contactOffset");
        var convex = ParseOptionalBooleanValueParameter(paramsObject, "convex");
        var cookingOptions = ParseOptionalEnumParameter<MeshColliderCookingOptions>(paramsObject, "cookingOptions");

        if (!enabled.HasValue &&
            !isTrigger.HasValue &&
            !contactOffset.HasValue &&
            !convex.HasValue &&
            !cookingOptions.HasValue)
        {
            throw new ArgumentException("At least one MeshCollider setting must be provided: enabled, isTrigger, contactOffset, convex, or cookingOptions.");
        }

        ValidateCommonColliderSettingValues(contactOffset);

        var effectiveConvex = convex ?? collider.convex;
        var effectiveIsTrigger = isTrigger ?? collider.isTrigger;
        if (effectiveIsTrigger && !effectiveConvex)
        {
            throw new ArgumentException("MeshCollider triggers must be convex. Set 'convex' to true when enabling 'isTrigger'.");
        }

        Undo.RecordObject(collider, "UnityMCP Set MeshCollider Settings");
        ApplyCommonColliderSettings(collider, enabled, isTrigger, contactOffset);

        if (convex.HasValue)
        {
            collider.convex = convex.Value;
        }

        if (cookingOptions.HasValue)
        {
            collider.cookingOptions = cookingOptions.Value;
        }

        EditorUtility.SetDirty(collider);

        var result = new
        {
            target = CreateObjectSummary(collider.gameObject),
            component = CreateComponentSummary(collider),
            settings = CreateMeshColliderSettingsSnapshot(collider),
            applied = new
            {
                enabled = enabled.HasValue,
                isTrigger = isTrigger.HasValue,
                contactOffset = contactOffset.HasValue,
                convex = convex.HasValue,
                cookingOptions = cookingOptions.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetHingeJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "hingeJoint2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<HingeJoint2D>(resolvedObject, "instanceId", "HingeJoint2D");

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateHingeJoint2DSettingsSnapshot(joint)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetHingeJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "hingeJoint2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<HingeJoint2D>(resolvedObject, "instanceId", "HingeJoint2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var autoConfigureConnectedAnchor = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureConnectedAnchor");
        var anchor = ParseOptionalVector2Parameter(paramsObject, "anchor");
        var connectedAnchor = ParseOptionalVector2Parameter(paramsObject, "connectedAnchor");
        var enableCollision = ParseOptionalBooleanValueParameter(paramsObject, "enableCollision");
        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var connectedBodyInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "connectedBodyInstanceId");
        var connectedAnchorMode = ParseOptionalConnectedAnchorModeParameter(paramsObject, "connectedAnchorMode");
        var useMotor = ParseOptionalBooleanValueParameter(paramsObject, "useMotor");
        var motorSpeed = ParseOptionalFloatParameter(paramsObject, "motorSpeed");
        var maxMotorTorque = ParseOptionalFloatParameter(paramsObject, "maxMotorTorque");
        var useLimits = ParseOptionalBooleanValueParameter(paramsObject, "useLimits");
        var lowerAngle = ParseOptionalFloatParameter(paramsObject, "lowerAngle");
        var upperAngle = ParseOptionalFloatParameter(paramsObject, "upperAngle");
        var useConnectedAnchor = ParseOptionalBooleanValueParameter(paramsObject, "useConnectedAnchor");

        if (!enabled.HasValue &&
            !autoConfigureConnectedAnchor.HasValue &&
            !anchor.HasValue &&
            !connectedAnchor.HasValue &&
            !enableCollision.HasValue &&
            !breakForce.HasValue &&
            !breakTorque.HasValue &&
            !connectedBodyInstanceId.IsSpecified &&
            !connectedAnchorMode.HasValue &&
            !useMotor.HasValue &&
            !motorSpeed.HasValue &&
            !maxMotorTorque.HasValue &&
            !useLimits.HasValue &&
            !lowerAngle.HasValue &&
            !upperAngle.HasValue &&
            !useConnectedAnchor.HasValue)
        {
            throw new ArgumentException("At least one HingeJoint2D setting must be provided.");
        }

        ValidateCommonJoint2DSettingValues(breakForce, breakTorque);
        if (maxMotorTorque.HasValue && maxMotorTorque.Value < 0f)
        {
            throw new ArgumentException("Parameter 'maxMotorTorque' must be greater than or equal to 0.");
        }

        var helperRequiresConnectedAnchor = connectedAnchor.HasValue || connectedAnchorMode.HasValue;
        if (helperRequiresConnectedAnchor && useConnectedAnchor.HasValue && !useConnectedAnchor.Value)
        {
            throw new ArgumentException("Parameter 'useConnectedAnchor' cannot be false when 'connectedAnchor' or 'connectedAnchorMode' is provided.");
        }

        Undo.RecordObject(joint, "UnityMCP Set HingeJoint2D Settings");
        ApplyCommonJoint2DSettings(joint, enabled, autoConfigureConnectedAnchor, anchor, connectedAnchor, enableCollision, breakForce, breakTorque, connectedBodyInstanceId, connectedAnchorMode);

        if (helperRequiresConnectedAnchor)
        {
            joint.useConnectedAnchor = true;
        }
        else if (useConnectedAnchor.HasValue)
        {
            joint.useConnectedAnchor = useConnectedAnchor.Value;
        }

        if (useMotor.HasValue)
        {
            joint.useMotor = useMotor.Value;
        }

        if (motorSpeed.HasValue || maxMotorTorque.HasValue)
        {
            var motor = joint.motor;
            if (motorSpeed.HasValue)
            {
                motor.motorSpeed = motorSpeed.Value;
            }

            if (maxMotorTorque.HasValue)
            {
                motor.maxMotorTorque = maxMotorTorque.Value;
            }

            joint.motor = motor;
        }

        if (useLimits.HasValue)
        {
            joint.useLimits = useLimits.Value;
        }

        if (lowerAngle.HasValue || upperAngle.HasValue)
        {
            var limits = joint.limits;
            if (lowerAngle.HasValue)
            {
                limits.min = lowerAngle.Value;
            }

            if (upperAngle.HasValue)
            {
                limits.max = upperAngle.Value;
            }

            joint.limits = limits;
        }

        EditorUtility.SetDirty(joint);
        var connectionState = CreateJoint2DAppliedConnectionState(joint, connectedAnchorMode);

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateHingeJoint2DSettingsSnapshot(joint),
            applied = new
            {
                enabled = enabled.HasValue,
                autoConfigureConnectedAnchor = connectionState.AutoConfigureConnectedAnchor,
                anchor = anchor.HasValue,
                connectedAnchor = connectionState.ConnectedAnchor,
                connectedAnchorMode = connectionState.ConnectedAnchorMode,
                enableCollision = enableCollision.HasValue,
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                connectedBodyInstanceId = connectionState.ConnectedBodyInstanceId,
                useMotor = useMotor.HasValue,
                motorSpeed = motorSpeed.HasValue,
                maxMotorTorque = maxMotorTorque.HasValue,
                useLimits = useLimits.HasValue,
                lowerAngle = lowerAngle.HasValue,
                upperAngle = upperAngle.HasValue,
                useConnectedAnchor = joint.useConnectedAnchor
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetSpringJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "springJoint2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<SpringJoint2D>(resolvedObject, "instanceId", "SpringJoint2D");

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateSpringJoint2DSettingsSnapshot(joint)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetSpringJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "springJoint2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<SpringJoint2D>(resolvedObject, "instanceId", "SpringJoint2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var autoConfigureConnectedAnchor = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureConnectedAnchor");
        var anchor = ParseOptionalVector2Parameter(paramsObject, "anchor");
        var connectedAnchor = ParseOptionalVector2Parameter(paramsObject, "connectedAnchor");
        var enableCollision = ParseOptionalBooleanValueParameter(paramsObject, "enableCollision");
        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var connectedBodyInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "connectedBodyInstanceId");
        var connectedAnchorMode = ParseOptionalConnectedAnchorModeParameter(paramsObject, "connectedAnchorMode");
        var autoConfigureDistance = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureDistance");
        var distance = ParseOptionalFloatParameter(paramsObject, "distance");
        var dampingRatio = ParseOptionalFloatParameter(paramsObject, "dampingRatio");
        var frequency = ParseOptionalFloatParameter(paramsObject, "frequency");

        if (!enabled.HasValue &&
            !autoConfigureConnectedAnchor.HasValue &&
            !anchor.HasValue &&
            !connectedAnchor.HasValue &&
            !enableCollision.HasValue &&
            !breakForce.HasValue &&
            !breakTorque.HasValue &&
            !connectedBodyInstanceId.IsSpecified &&
            !connectedAnchorMode.HasValue &&
            !autoConfigureDistance.HasValue &&
            !distance.HasValue &&
            !dampingRatio.HasValue &&
            !frequency.HasValue)
        {
            throw new ArgumentException("At least one SpringJoint2D setting must be provided.");
        }

        ValidateCommonJoint2DSettingValues(breakForce, breakTorque);
        if (distance.HasValue && distance.Value < 0f)
        {
            throw new ArgumentException("Parameter 'distance' must be greater than or equal to 0.");
        }

        if (dampingRatio.HasValue && (dampingRatio.Value < 0f || dampingRatio.Value > 1f))
        {
            throw new ArgumentException("Parameter 'dampingRatio' must be between 0 and 1.");
        }

        if (frequency.HasValue && frequency.Value < 0f)
        {
            throw new ArgumentException("Parameter 'frequency' must be greater than or equal to 0.");
        }

        Undo.RecordObject(joint, "UnityMCP Set SpringJoint2D Settings");
        ApplyCommonJoint2DSettings(joint, enabled, autoConfigureConnectedAnchor, anchor, connectedAnchor, enableCollision, breakForce, breakTorque, connectedBodyInstanceId, connectedAnchorMode);

        if (autoConfigureDistance.HasValue)
        {
            joint.autoConfigureDistance = autoConfigureDistance.Value;
        }

        if (distance.HasValue)
        {
            joint.distance = distance.Value;
        }

        if (dampingRatio.HasValue)
        {
            joint.dampingRatio = dampingRatio.Value;
        }

        if (frequency.HasValue)
        {
            joint.frequency = frequency.Value;
        }

        EditorUtility.SetDirty(joint);
        var connectionState = CreateJoint2DAppliedConnectionState(joint, connectedAnchorMode);

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateSpringJoint2DSettingsSnapshot(joint),
            applied = new
            {
                enabled = enabled.HasValue,
                autoConfigureConnectedAnchor = connectionState.AutoConfigureConnectedAnchor,
                anchor = anchor.HasValue,
                connectedAnchor = connectionState.ConnectedAnchor,
                connectedAnchorMode = connectionState.ConnectedAnchorMode,
                enableCollision = enableCollision.HasValue,
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                connectedBodyInstanceId = connectionState.ConnectedBodyInstanceId,
                autoConfigureDistance = autoConfigureDistance.HasValue,
                distance = distance.HasValue,
                dampingRatio = dampingRatio.HasValue,
                frequency = frequency.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetDistanceJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "distanceJoint2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<DistanceJoint2D>(resolvedObject, "instanceId", "DistanceJoint2D");

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateDistanceJoint2DSettingsSnapshot(joint)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetDistanceJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "distanceJoint2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<DistanceJoint2D>(resolvedObject, "instanceId", "DistanceJoint2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var autoConfigureConnectedAnchor = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureConnectedAnchor");
        var anchor = ParseOptionalVector2Parameter(paramsObject, "anchor");
        var connectedAnchor = ParseOptionalVector2Parameter(paramsObject, "connectedAnchor");
        var enableCollision = ParseOptionalBooleanValueParameter(paramsObject, "enableCollision");
        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var connectedBodyInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "connectedBodyInstanceId");
        var connectedAnchorMode = ParseOptionalConnectedAnchorModeParameter(paramsObject, "connectedAnchorMode");
        var autoConfigureDistance = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureDistance");
        var distance = ParseOptionalFloatParameter(paramsObject, "distance");
        var maxDistanceOnly = ParseOptionalBooleanValueParameter(paramsObject, "maxDistanceOnly");

        if (!enabled.HasValue &&
            !autoConfigureConnectedAnchor.HasValue &&
            !anchor.HasValue &&
            !connectedAnchor.HasValue &&
            !enableCollision.HasValue &&
            !breakForce.HasValue &&
            !breakTorque.HasValue &&
            !connectedBodyInstanceId.IsSpecified &&
            !connectedAnchorMode.HasValue &&
            !autoConfigureDistance.HasValue &&
            !distance.HasValue &&
            !maxDistanceOnly.HasValue)
        {
            throw new ArgumentException("At least one DistanceJoint2D setting must be provided.");
        }

        ValidateCommonJoint2DSettingValues(breakForce, breakTorque);
        if (distance.HasValue && distance.Value < 0f)
        {
            throw new ArgumentException("Parameter 'distance' must be greater than or equal to 0.");
        }

        Undo.RecordObject(joint, "UnityMCP Set DistanceJoint2D Settings");
        ApplyCommonJoint2DSettings(joint, enabled, autoConfigureConnectedAnchor, anchor, connectedAnchor, enableCollision, breakForce, breakTorque, connectedBodyInstanceId, connectedAnchorMode);

        if (autoConfigureDistance.HasValue)
        {
            joint.autoConfigureDistance = autoConfigureDistance.Value;
        }

        if (distance.HasValue)
        {
            joint.distance = distance.Value;
        }

        if (maxDistanceOnly.HasValue)
        {
            joint.maxDistanceOnly = maxDistanceOnly.Value;
        }

        EditorUtility.SetDirty(joint);
        var connectionState = CreateJoint2DAppliedConnectionState(joint, connectedAnchorMode);

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateDistanceJoint2DSettingsSnapshot(joint),
            applied = new
            {
                enabled = enabled.HasValue,
                autoConfigureConnectedAnchor = connectionState.AutoConfigureConnectedAnchor,
                anchor = anchor.HasValue,
                connectedAnchor = connectionState.ConnectedAnchor,
                connectedAnchorMode = connectionState.ConnectedAnchorMode,
                enableCollision = enableCollision.HasValue,
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                connectedBodyInstanceId = connectionState.ConnectedBodyInstanceId,
                autoConfigureDistance = autoConfigureDistance.HasValue,
                distance = distance.HasValue,
                maxDistanceOnly = maxDistanceOnly.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetFixedJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "fixedJoint2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<FixedJoint2D>(resolvedObject, "instanceId", "FixedJoint2D");

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateFixedJoint2DSettingsSnapshot(joint)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetFixedJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "fixedJoint2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<FixedJoint2D>(resolvedObject, "instanceId", "FixedJoint2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var autoConfigureConnectedAnchor = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureConnectedAnchor");
        var anchor = ParseOptionalVector2Parameter(paramsObject, "anchor");
        var connectedAnchor = ParseOptionalVector2Parameter(paramsObject, "connectedAnchor");
        var enableCollision = ParseOptionalBooleanValueParameter(paramsObject, "enableCollision");
        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var connectedBodyInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "connectedBodyInstanceId");
        var connectedAnchorMode = ParseOptionalConnectedAnchorModeParameter(paramsObject, "connectedAnchorMode");
        var dampingRatio = ParseOptionalFloatParameter(paramsObject, "dampingRatio");
        var frequency = ParseOptionalFloatParameter(paramsObject, "frequency");

        if (!enabled.HasValue &&
            !autoConfigureConnectedAnchor.HasValue &&
            !anchor.HasValue &&
            !connectedAnchor.HasValue &&
            !enableCollision.HasValue &&
            !breakForce.HasValue &&
            !breakTorque.HasValue &&
            !connectedBodyInstanceId.IsSpecified &&
            !connectedAnchorMode.HasValue &&
            !dampingRatio.HasValue &&
            !frequency.HasValue)
        {
            throw new ArgumentException("At least one FixedJoint2D setting must be provided.");
        }

        ValidateCommonJoint2DSettingValues(breakForce, breakTorque);
        if (dampingRatio.HasValue && (dampingRatio.Value < 0f || dampingRatio.Value > 1f))
        {
            throw new ArgumentException("Parameter 'dampingRatio' must be between 0 and 1.");
        }

        if (frequency.HasValue && frequency.Value < 0f)
        {
            throw new ArgumentException("Parameter 'frequency' must be greater than or equal to 0.");
        }

        Undo.RecordObject(joint, "UnityMCP Set FixedJoint2D Settings");
        ApplyCommonJoint2DSettings(joint, enabled, autoConfigureConnectedAnchor, anchor, connectedAnchor, enableCollision, breakForce, breakTorque, connectedBodyInstanceId, connectedAnchorMode);

        if (dampingRatio.HasValue)
        {
            joint.dampingRatio = dampingRatio.Value;
        }

        if (frequency.HasValue)
        {
            joint.frequency = frequency.Value;
        }

        EditorUtility.SetDirty(joint);
        var connectionState = CreateJoint2DAppliedConnectionState(joint, connectedAnchorMode);

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateFixedJoint2DSettingsSnapshot(joint),
            applied = new
            {
                enabled = enabled.HasValue,
                autoConfigureConnectedAnchor = connectionState.AutoConfigureConnectedAnchor,
                anchor = anchor.HasValue,
                connectedAnchor = connectionState.ConnectedAnchor,
                connectedAnchorMode = connectionState.ConnectedAnchorMode,
                enableCollision = enableCollision.HasValue,
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                connectedBodyInstanceId = connectionState.ConnectedBodyInstanceId,
                dampingRatio = dampingRatio.HasValue,
                frequency = frequency.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetSliderJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "sliderJoint2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<SliderJoint2D>(resolvedObject, "instanceId", "SliderJoint2D");

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateSliderJoint2DSettingsSnapshot(joint)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetSliderJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "sliderJoint2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<SliderJoint2D>(resolvedObject, "instanceId", "SliderJoint2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var autoConfigureConnectedAnchor = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureConnectedAnchor");
        var anchor = ParseOptionalVector2Parameter(paramsObject, "anchor");
        var connectedAnchor = ParseOptionalVector2Parameter(paramsObject, "connectedAnchor");
        var enableCollision = ParseOptionalBooleanValueParameter(paramsObject, "enableCollision");
        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var connectedBodyInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "connectedBodyInstanceId");
        var connectedAnchorMode = ParseOptionalConnectedAnchorModeParameter(paramsObject, "connectedAnchorMode");
        var autoConfigureAngle = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureAngle");
        var angle = ParseOptionalFloatParameter(paramsObject, "angle");
        var useMotor = ParseOptionalBooleanValueParameter(paramsObject, "useMotor");
        var motorSpeed = ParseOptionalFloatParameter(paramsObject, "motorSpeed");
        var maxMotorTorque = ParseOptionalFloatParameter(paramsObject, "maxMotorTorque");
        var useLimits = ParseOptionalBooleanValueParameter(paramsObject, "useLimits");
        var lowerTranslation = ParseOptionalFloatParameter(paramsObject, "lowerTranslation");
        var upperTranslation = ParseOptionalFloatParameter(paramsObject, "upperTranslation");

        if (!enabled.HasValue &&
            !autoConfigureConnectedAnchor.HasValue &&
            !anchor.HasValue &&
            !connectedAnchor.HasValue &&
            !enableCollision.HasValue &&
            !breakForce.HasValue &&
            !breakTorque.HasValue &&
            !connectedBodyInstanceId.IsSpecified &&
            !connectedAnchorMode.HasValue &&
            !autoConfigureAngle.HasValue &&
            !angle.HasValue &&
            !useMotor.HasValue &&
            !motorSpeed.HasValue &&
            !maxMotorTorque.HasValue &&
            !useLimits.HasValue &&
            !lowerTranslation.HasValue &&
            !upperTranslation.HasValue)
        {
            throw new ArgumentException("At least one SliderJoint2D setting must be provided.");
        }

        ValidateCommonJoint2DSettingValues(breakForce, breakTorque);
        if (maxMotorTorque.HasValue && maxMotorTorque.Value < 0f)
        {
            throw new ArgumentException("Parameter 'maxMotorTorque' must be greater than or equal to 0.");
        }

        Undo.RecordObject(joint, "UnityMCP Set SliderJoint2D Settings");
        ApplyCommonJoint2DSettings(joint, enabled, autoConfigureConnectedAnchor, anchor, connectedAnchor, enableCollision, breakForce, breakTorque, connectedBodyInstanceId, connectedAnchorMode);

        if (autoConfigureAngle.HasValue)
        {
            joint.autoConfigureAngle = autoConfigureAngle.Value;
        }

        if (angle.HasValue)
        {
            joint.angle = angle.Value;
        }

        if (useMotor.HasValue)
        {
            joint.useMotor = useMotor.Value;
        }

        if (motorSpeed.HasValue || maxMotorTorque.HasValue)
        {
            var motor = joint.motor;
            if (motorSpeed.HasValue)
            {
                motor.motorSpeed = motorSpeed.Value;
            }

            if (maxMotorTorque.HasValue)
            {
                motor.maxMotorTorque = maxMotorTorque.Value;
            }

            joint.motor = motor;
        }

        if (useLimits.HasValue)
        {
            joint.useLimits = useLimits.Value;
        }

        if (lowerTranslation.HasValue || upperTranslation.HasValue)
        {
            var limits = joint.limits;
            if (lowerTranslation.HasValue)
            {
                limits.min = lowerTranslation.Value;
            }

            if (upperTranslation.HasValue)
            {
                limits.max = upperTranslation.Value;
            }

            joint.limits = limits;
        }

        EditorUtility.SetDirty(joint);
        var connectionState = CreateJoint2DAppliedConnectionState(joint, connectedAnchorMode);

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateSliderJoint2DSettingsSnapshot(joint),
            applied = new
            {
                enabled = enabled.HasValue,
                autoConfigureConnectedAnchor = connectionState.AutoConfigureConnectedAnchor,
                anchor = anchor.HasValue,
                connectedAnchor = connectionState.ConnectedAnchor,
                connectedAnchorMode = connectionState.ConnectedAnchorMode,
                enableCollision = enableCollision.HasValue,
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                connectedBodyInstanceId = connectionState.ConnectedBodyInstanceId,
                autoConfigureAngle = autoConfigureAngle.HasValue,
                angle = angle.HasValue,
                useMotor = useMotor.HasValue,
                motorSpeed = motorSpeed.HasValue,
                maxMotorTorque = maxMotorTorque.HasValue,
                useLimits = useLimits.HasValue,
                lowerTranslation = lowerTranslation.HasValue,
                upperTranslation = upperTranslation.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetWheelJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "wheelJoint2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<WheelJoint2D>(resolvedObject, "instanceId", "WheelJoint2D");

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateWheelJoint2DSettingsSnapshot(joint)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetWheelJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "wheelJoint2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<WheelJoint2D>(resolvedObject, "instanceId", "WheelJoint2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var autoConfigureConnectedAnchor = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureConnectedAnchor");
        var anchor = ParseOptionalVector2Parameter(paramsObject, "anchor");
        var connectedAnchor = ParseOptionalVector2Parameter(paramsObject, "connectedAnchor");
        var enableCollision = ParseOptionalBooleanValueParameter(paramsObject, "enableCollision");
        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var connectedBodyInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "connectedBodyInstanceId");
        var connectedAnchorMode = ParseOptionalConnectedAnchorModeParameter(paramsObject, "connectedAnchorMode");
        var useMotor = ParseOptionalBooleanValueParameter(paramsObject, "useMotor");
        var motorSpeed = ParseOptionalFloatParameter(paramsObject, "motorSpeed");
        var maxMotorTorque = ParseOptionalFloatParameter(paramsObject, "maxMotorTorque");
        var suspensionDampingRatio = ParseOptionalFloatParameter(paramsObject, "suspensionDampingRatio");
        var suspensionFrequency = ParseOptionalFloatParameter(paramsObject, "suspensionFrequency");
        var suspensionAngle = ParseOptionalFloatParameter(paramsObject, "suspensionAngle");

        if (!enabled.HasValue &&
            !autoConfigureConnectedAnchor.HasValue &&
            !anchor.HasValue &&
            !connectedAnchor.HasValue &&
            !enableCollision.HasValue &&
            !breakForce.HasValue &&
            !breakTorque.HasValue &&
            !connectedBodyInstanceId.IsSpecified &&
            !connectedAnchorMode.HasValue &&
            !useMotor.HasValue &&
            !motorSpeed.HasValue &&
            !maxMotorTorque.HasValue &&
            !suspensionDampingRatio.HasValue &&
            !suspensionFrequency.HasValue &&
            !suspensionAngle.HasValue)
        {
            throw new ArgumentException("At least one WheelJoint2D setting must be provided.");
        }

        ValidateCommonJoint2DSettingValues(breakForce, breakTorque);
        if (maxMotorTorque.HasValue && maxMotorTorque.Value < 0f)
        {
            throw new ArgumentException("Parameter 'maxMotorTorque' must be greater than or equal to 0.");
        }

        if (suspensionDampingRatio.HasValue && (suspensionDampingRatio.Value < 0f || suspensionDampingRatio.Value > 1f))
        {
            throw new ArgumentException("Parameter 'suspensionDampingRatio' must be between 0 and 1.");
        }

        if (suspensionFrequency.HasValue && suspensionFrequency.Value < 0f)
        {
            throw new ArgumentException("Parameter 'suspensionFrequency' must be greater than or equal to 0.");
        }

        Undo.RecordObject(joint, "UnityMCP Set WheelJoint2D Settings");
        ApplyCommonJoint2DSettings(joint, enabled, autoConfigureConnectedAnchor, anchor, connectedAnchor, enableCollision, breakForce, breakTorque, connectedBodyInstanceId, connectedAnchorMode);

        if (useMotor.HasValue)
        {
            joint.useMotor = useMotor.Value;
        }

        if (motorSpeed.HasValue || maxMotorTorque.HasValue)
        {
            var motor = joint.motor;
            if (motorSpeed.HasValue)
            {
                motor.motorSpeed = motorSpeed.Value;
            }

            if (maxMotorTorque.HasValue)
            {
                motor.maxMotorTorque = maxMotorTorque.Value;
            }

            joint.motor = motor;
        }

        if (suspensionDampingRatio.HasValue || suspensionFrequency.HasValue || suspensionAngle.HasValue)
        {
            var suspension = joint.suspension;
            if (suspensionDampingRatio.HasValue)
            {
                suspension.dampingRatio = suspensionDampingRatio.Value;
            }

            if (suspensionFrequency.HasValue)
            {
                suspension.frequency = suspensionFrequency.Value;
            }

            if (suspensionAngle.HasValue)
            {
                suspension.angle = suspensionAngle.Value;
            }

            joint.suspension = suspension;
        }

        EditorUtility.SetDirty(joint);
        var connectionState = CreateJoint2DAppliedConnectionState(joint, connectedAnchorMode);

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateWheelJoint2DSettingsSnapshot(joint),
            applied = new
            {
                enabled = enabled.HasValue,
                autoConfigureConnectedAnchor = connectionState.AutoConfigureConnectedAnchor,
                anchor = anchor.HasValue,
                connectedAnchor = connectionState.ConnectedAnchor,
                connectedAnchorMode = connectionState.ConnectedAnchorMode,
                enableCollision = enableCollision.HasValue,
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                connectedBodyInstanceId = connectionState.ConnectedBodyInstanceId,
                useMotor = useMotor.HasValue,
                motorSpeed = motorSpeed.HasValue,
                maxMotorTorque = maxMotorTorque.HasValue,
                suspensionDampingRatio = suspensionDampingRatio.HasValue,
                suspensionFrequency = suspensionFrequency.HasValue,
                suspensionAngle = suspensionAngle.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetTargetJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "targetJoint2D.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<TargetJoint2D>(resolvedObject, "instanceId", "TargetJoint2D");

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateTargetJoint2DSettingsSnapshot(joint)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetTargetJoint2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "targetJoint2D.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<TargetJoint2D>(resolvedObject, "instanceId", "TargetJoint2D");

        var enabled = ParseOptionalBooleanValueParameter(paramsObject, "enabled");
        var anchor = ParseOptionalVector2Parameter(paramsObject, "anchor");
        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var autoConfigureTarget = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureTarget");
        var target = ParseOptionalVector2Parameter(paramsObject, "target");
        var maxForce = ParseOptionalFloatParameter(paramsObject, "maxForce");
        var dampingRatio = ParseOptionalFloatParameter(paramsObject, "dampingRatio");
        var frequency = ParseOptionalFloatParameter(paramsObject, "frequency");

        if (!enabled.HasValue &&
            !anchor.HasValue &&
            !breakForce.HasValue &&
            !breakTorque.HasValue &&
            !autoConfigureTarget.HasValue &&
            !target.HasValue &&
            !maxForce.HasValue &&
            !dampingRatio.HasValue &&
            !frequency.HasValue)
        {
            throw new ArgumentException("At least one TargetJoint2D setting must be provided.");
        }

        ValidateCommonJoint2DSettingValues(breakForce, breakTorque);
        if (maxForce.HasValue && maxForce.Value < 0f)
        {
            throw new ArgumentException("Parameter 'maxForce' must be greater than or equal to 0.");
        }

        if (dampingRatio.HasValue && (dampingRatio.Value < 0f || dampingRatio.Value > 1f))
        {
            throw new ArgumentException("Parameter 'dampingRatio' must be between 0 and 1.");
        }

        if (frequency.HasValue && frequency.Value < 0f)
        {
            throw new ArgumentException("Parameter 'frequency' must be greater than or equal to 0.");
        }

        Undo.RecordObject(joint, "UnityMCP Set TargetJoint2D Settings");

        if (enabled.HasValue)
        {
            joint.enabled = enabled.Value;
        }

        if (anchor.HasValue)
        {
            joint.anchor = anchor.Value;
        }

        if (breakForce.HasValue)
        {
            joint.breakForce = breakForce.Value;
        }

        if (breakTorque.HasValue)
        {
            joint.breakTorque = breakTorque.Value;
        }

        if (autoConfigureTarget.HasValue)
        {
            joint.autoConfigureTarget = autoConfigureTarget.Value;
        }

        if (target.HasValue)
        {
            joint.target = target.Value;
        }

        if (maxForce.HasValue)
        {
            joint.maxForce = maxForce.Value;
        }

        if (dampingRatio.HasValue)
        {
            joint.dampingRatio = dampingRatio.Value;
        }

        if (frequency.HasValue)
        {
            joint.frequency = frequency.Value;
        }

        EditorUtility.SetDirty(joint);

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateTargetJoint2DSettingsSnapshot(joint),
            applied = new
            {
                enabled = enabled.HasValue,
                anchor = anchor.HasValue,
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                autoConfigureTarget = autoConfigureTarget.HasValue,
                target = target.HasValue,
                maxForce = maxForce.HasValue,
                dampingRatio = dampingRatio.HasValue,
                frequency = frequency.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetHingeJointSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "hingeJoint.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<HingeJoint>(resolvedObject, "instanceId", "HingeJoint");

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateHingeJointSettingsSnapshot(joint)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetHingeJointSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "hingeJoint.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<HingeJoint>(resolvedObject, "instanceId", "HingeJoint");

        var autoConfigureConnectedAnchor = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureConnectedAnchor");
        var anchor = ParseOptionalVector3Parameter(paramsObject, "anchor");
        var connectedAnchor = ParseOptionalVector3Parameter(paramsObject, "connectedAnchor");
        var axis = ParseOptionalVector3Parameter(paramsObject, "axis");
        var enableCollision = ParseOptionalBooleanValueParameter(paramsObject, "enableCollision");
        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var connectedBodyInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "connectedBodyInstanceId");
        var connectedAnchorMode = ParseOptionalConnectedAnchorModeParameter(paramsObject, "connectedAnchorMode");
        var useSpring = ParseOptionalBooleanValueParameter(paramsObject, "useSpring");
        var spring = ParseOptionalFloatParameter(paramsObject, "spring");
        var damper = ParseOptionalFloatParameter(paramsObject, "damper");
        var targetPosition = ParseOptionalFloatParameter(paramsObject, "targetPosition");
        var useMotor = ParseOptionalBooleanValueParameter(paramsObject, "useMotor");
        var motorTargetVelocity = ParseOptionalFloatParameter(paramsObject, "motorTargetVelocity");
        var motorForce = ParseOptionalFloatParameter(paramsObject, "motorForce");
        var motorFreeSpin = ParseOptionalBooleanValueParameter(paramsObject, "motorFreeSpin");
        var useLimits = ParseOptionalBooleanValueParameter(paramsObject, "useLimits");
        var minLimit = ParseOptionalFloatParameter(paramsObject, "minLimit");
        var maxLimit = ParseOptionalFloatParameter(paramsObject, "maxLimit");

        if (!autoConfigureConnectedAnchor.HasValue &&
            !anchor.HasValue &&
            !connectedAnchor.HasValue &&
            !axis.HasValue &&
            !enableCollision.HasValue &&
            !breakForce.HasValue &&
            !breakTorque.HasValue &&
            !connectedBodyInstanceId.IsSpecified &&
            !connectedAnchorMode.HasValue &&
            !useSpring.HasValue &&
            !spring.HasValue &&
            !damper.HasValue &&
            !targetPosition.HasValue &&
            !useMotor.HasValue &&
            !motorTargetVelocity.HasValue &&
            !motorForce.HasValue &&
            !motorFreeSpin.HasValue &&
            !useLimits.HasValue &&
            !minLimit.HasValue &&
            !maxLimit.HasValue)
        {
            throw new ArgumentException("At least one HingeJoint setting must be provided.");
        }

        ValidateCommonJointSettingValues(breakForce, breakTorque);
        if (spring.HasValue && spring.Value < 0f)
        {
            throw new ArgumentException("Parameter 'spring' must be greater than or equal to 0.");
        }

        if (damper.HasValue && damper.Value < 0f)
        {
            throw new ArgumentException("Parameter 'damper' must be greater than or equal to 0.");
        }

        if (motorForce.HasValue && motorForce.Value < 0f)
        {
            throw new ArgumentException("Parameter 'motorForce' must be greater than or equal to 0.");
        }

        Undo.RecordObject(joint, "UnityMCP Set HingeJoint Settings");
        ApplyCommonJointSettings(joint, autoConfigureConnectedAnchor, anchor, connectedAnchor, axis, enableCollision, breakForce, breakTorque, connectedBodyInstanceId, connectedAnchorMode);

        if (useSpring.HasValue)
        {
            joint.useSpring = useSpring.Value;
        }

        if (spring.HasValue || damper.HasValue || targetPosition.HasValue)
        {
            var springSettings = joint.spring;
            if (spring.HasValue)
            {
                springSettings.spring = spring.Value;
            }

            if (damper.HasValue)
            {
                springSettings.damper = damper.Value;
            }

            if (targetPosition.HasValue)
            {
                springSettings.targetPosition = targetPosition.Value;
            }

            joint.spring = springSettings;
        }

        if (useMotor.HasValue)
        {
            joint.useMotor = useMotor.Value;
        }

        if (motorTargetVelocity.HasValue || motorForce.HasValue || motorFreeSpin.HasValue)
        {
            var motor = joint.motor;
            if (motorTargetVelocity.HasValue)
            {
                motor.targetVelocity = motorTargetVelocity.Value;
            }

            if (motorForce.HasValue)
            {
                motor.force = motorForce.Value;
            }

            if (motorFreeSpin.HasValue)
            {
                motor.freeSpin = motorFreeSpin.Value;
            }

            joint.motor = motor;
        }

        if (useLimits.HasValue)
        {
            joint.useLimits = useLimits.Value;
        }

        if (minLimit.HasValue || maxLimit.HasValue)
        {
            var limits = joint.limits;
            if (minLimit.HasValue)
            {
                limits.min = minLimit.Value;
            }

            if (maxLimit.HasValue)
            {
                limits.max = maxLimit.Value;
            }

            joint.limits = limits;
        }

        EditorUtility.SetDirty(joint);
        var connectionState = CreateJointAppliedConnectionState(joint, connectedAnchorMode);

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateHingeJointSettingsSnapshot(joint),
            applied = new
            {
                autoConfigureConnectedAnchor = connectionState.AutoConfigureConnectedAnchor,
                anchor = anchor.HasValue,
                connectedAnchor = connectionState.ConnectedAnchor,
                connectedAnchorMode = connectionState.ConnectedAnchorMode,
                axis = axis.HasValue,
                enableCollision = enableCollision.HasValue,
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                connectedBodyInstanceId = connectionState.ConnectedBodyInstanceId,
                useSpring = useSpring.HasValue,
                spring = spring.HasValue,
                damper = damper.HasValue,
                targetPosition = targetPosition.HasValue,
                useMotor = useMotor.HasValue,
                motorTargetVelocity = motorTargetVelocity.HasValue,
                motorForce = motorForce.HasValue,
                motorFreeSpin = motorFreeSpin.HasValue,
                useLimits = useLimits.HasValue,
                minLimit = minLimit.HasValue,
                maxLimit = maxLimit.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetSpringJointSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "springJoint.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<SpringJoint>(resolvedObject, "instanceId", "SpringJoint");

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateSpringJointSettingsSnapshot(joint)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetSpringJointSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "springJoint.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<SpringJoint>(resolvedObject, "instanceId", "SpringJoint");

        var autoConfigureConnectedAnchor = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureConnectedAnchor");
        var anchor = ParseOptionalVector3Parameter(paramsObject, "anchor");
        var connectedAnchor = ParseOptionalVector3Parameter(paramsObject, "connectedAnchor");
        var axis = ParseOptionalVector3Parameter(paramsObject, "axis");
        var enableCollision = ParseOptionalBooleanValueParameter(paramsObject, "enableCollision");
        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var connectedBodyInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "connectedBodyInstanceId");
        var connectedAnchorMode = ParseOptionalConnectedAnchorModeParameter(paramsObject, "connectedAnchorMode");
        var spring = ParseOptionalFloatParameter(paramsObject, "spring");
        var damper = ParseOptionalFloatParameter(paramsObject, "damper");
        var minDistance = ParseOptionalFloatParameter(paramsObject, "minDistance");
        var maxDistance = ParseOptionalFloatParameter(paramsObject, "maxDistance");
        var tolerance = ParseOptionalFloatParameter(paramsObject, "tolerance");

        if (!autoConfigureConnectedAnchor.HasValue &&
            !anchor.HasValue &&
            !connectedAnchor.HasValue &&
            !axis.HasValue &&
            !enableCollision.HasValue &&
            !breakForce.HasValue &&
            !breakTorque.HasValue &&
            !connectedBodyInstanceId.IsSpecified &&
            !connectedAnchorMode.HasValue &&
            !spring.HasValue &&
            !damper.HasValue &&
            !minDistance.HasValue &&
            !maxDistance.HasValue &&
            !tolerance.HasValue)
        {
            throw new ArgumentException("At least one SpringJoint setting must be provided.");
        }

        ValidateCommonJointSettingValues(breakForce, breakTorque);
        if (spring.HasValue && spring.Value < 0f)
        {
            throw new ArgumentException("Parameter 'spring' must be greater than or equal to 0.");
        }

        if (damper.HasValue && damper.Value < 0f)
        {
            throw new ArgumentException("Parameter 'damper' must be greater than or equal to 0.");
        }

        if (minDistance.HasValue && minDistance.Value < 0f)
        {
            throw new ArgumentException("Parameter 'minDistance' must be greater than or equal to 0.");
        }

        if (maxDistance.HasValue && maxDistance.Value < 0f)
        {
            throw new ArgumentException("Parameter 'maxDistance' must be greater than or equal to 0.");
        }

        if (tolerance.HasValue && tolerance.Value < 0f)
        {
            throw new ArgumentException("Parameter 'tolerance' must be greater than or equal to 0.");
        }

        Undo.RecordObject(joint, "UnityMCP Set SpringJoint Settings");
        ApplyCommonJointSettings(joint, autoConfigureConnectedAnchor, anchor, connectedAnchor, axis, enableCollision, breakForce, breakTorque, connectedBodyInstanceId, connectedAnchorMode);

        if (spring.HasValue)
        {
            joint.spring = spring.Value;
        }

        if (damper.HasValue)
        {
            joint.damper = damper.Value;
        }

        if (minDistance.HasValue)
        {
            joint.minDistance = minDistance.Value;
        }

        if (maxDistance.HasValue)
        {
            joint.maxDistance = maxDistance.Value;
        }

        if (tolerance.HasValue)
        {
            joint.tolerance = tolerance.Value;
        }

        EditorUtility.SetDirty(joint);
        var connectionState = CreateJointAppliedConnectionState(joint, connectedAnchorMode);

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateSpringJointSettingsSnapshot(joint),
            applied = new
            {
                autoConfigureConnectedAnchor = connectionState.AutoConfigureConnectedAnchor,
                anchor = anchor.HasValue,
                connectedAnchor = connectionState.ConnectedAnchor,
                connectedAnchorMode = connectionState.ConnectedAnchorMode,
                axis = axis.HasValue,
                enableCollision = enableCollision.HasValue,
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                connectedBodyInstanceId = connectionState.ConnectedBodyInstanceId,
                spring = spring.HasValue,
                damper = damper.HasValue,
                minDistance = minDistance.HasValue,
                maxDistance = maxDistance.HasValue,
                tolerance = tolerance.HasValue
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetFixedJointSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "fixedJoint.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<FixedJoint>(resolvedObject, "instanceId", "FixedJoint");

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateFixedJointSettingsSnapshot(joint)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetFixedJointSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "fixedJoint.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<FixedJoint>(resolvedObject, "instanceId", "FixedJoint");

        var autoConfigureConnectedAnchor = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureConnectedAnchor");
        var anchor = ParseOptionalVector3Parameter(paramsObject, "anchor");
        var connectedAnchor = ParseOptionalVector3Parameter(paramsObject, "connectedAnchor");
        var axis = ParseOptionalVector3Parameter(paramsObject, "axis");
        var enableCollision = ParseOptionalBooleanValueParameter(paramsObject, "enableCollision");
        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var connectedBodyInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "connectedBodyInstanceId");
        var connectedAnchorMode = ParseOptionalConnectedAnchorModeParameter(paramsObject, "connectedAnchorMode");

        if (!autoConfigureConnectedAnchor.HasValue &&
            !anchor.HasValue &&
            !connectedAnchor.HasValue &&
            !axis.HasValue &&
            !enableCollision.HasValue &&
            !breakForce.HasValue &&
            !breakTorque.HasValue &&
            !connectedBodyInstanceId.IsSpecified &&
            !connectedAnchorMode.HasValue)
        {
            throw new ArgumentException("At least one FixedJoint setting must be provided.");
        }

        ValidateCommonJointSettingValues(breakForce, breakTorque);

        Undo.RecordObject(joint, "UnityMCP Set FixedJoint Settings");
        ApplyCommonJointSettings(joint, autoConfigureConnectedAnchor, anchor, connectedAnchor, axis, enableCollision, breakForce, breakTorque, connectedBodyInstanceId, connectedAnchorMode);
        EditorUtility.SetDirty(joint);
        var connectionState = CreateJointAppliedConnectionState(joint, connectedAnchorMode);

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateFixedJointSettingsSnapshot(joint),
            applied = new
            {
                autoConfigureConnectedAnchor = connectionState.AutoConfigureConnectedAnchor,
                anchor = anchor.HasValue,
                connectedAnchor = connectionState.ConnectedAnchor,
                connectedAnchorMode = connectionState.ConnectedAnchorMode,
                axis = axis.HasValue,
                enableCollision = enableCollision.HasValue,
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                connectedBodyInstanceId = connectionState.ConnectedBodyInstanceId
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetCharacterJointSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "characterJoint.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<CharacterJoint>(resolvedObject, "instanceId", "CharacterJoint");

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateCharacterJointSettingsSnapshot(joint)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetCharacterJointSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "characterJoint.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<CharacterJoint>(resolvedObject, "instanceId", "CharacterJoint");

        var autoConfigureConnectedAnchor = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureConnectedAnchor");
        var anchor = ParseOptionalVector3Parameter(paramsObject, "anchor");
        var connectedAnchor = ParseOptionalVector3Parameter(paramsObject, "connectedAnchor");
        var axis = ParseOptionalVector3Parameter(paramsObject, "axis");
        var enableCollision = ParseOptionalBooleanValueParameter(paramsObject, "enableCollision");
        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var connectedBodyInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "connectedBodyInstanceId");
        var connectedAnchorMode = ParseOptionalConnectedAnchorModeParameter(paramsObject, "connectedAnchorMode");
        var swingAxis = ParseOptionalVector3Parameter(paramsObject, "swingAxis");
        var enableProjection = ParseOptionalBooleanValueParameter(paramsObject, "enableProjection");
        var enablePreprocessing = ParseOptionalBooleanValueParameter(paramsObject, "enablePreprocessing");
        var twistLimitSpring = ParseOptionalSoftJointLimitSpringParameter(paramsObject, "twistLimitSpring");
        var swingLimitSpring = ParseOptionalSoftJointLimitSpringParameter(paramsObject, "swingLimitSpring");
        var lowTwistLimit = ParseOptionalSoftJointLimitParameter(paramsObject, "lowTwistLimit");
        var highTwistLimit = ParseOptionalSoftJointLimitParameter(paramsObject, "highTwistLimit");
        var swing1Limit = ParseOptionalSoftJointLimitParameter(paramsObject, "swing1Limit");
        var swing2Limit = ParseOptionalSoftJointLimitParameter(paramsObject, "swing2Limit");

        if (!autoConfigureConnectedAnchor.HasValue &&
            !anchor.HasValue &&
            !connectedAnchor.HasValue &&
            !axis.HasValue &&
            !enableCollision.HasValue &&
            !breakForce.HasValue &&
            !breakTorque.HasValue &&
            !connectedBodyInstanceId.IsSpecified &&
            !connectedAnchorMode.HasValue &&
            !swingAxis.HasValue &&
            !enableProjection.HasValue &&
            !enablePreprocessing.HasValue &&
            !twistLimitSpring.IsSpecified &&
            !swingLimitSpring.IsSpecified &&
            !lowTwistLimit.IsSpecified &&
            !highTwistLimit.IsSpecified &&
            !swing1Limit.IsSpecified &&
            !swing2Limit.IsSpecified)
        {
            throw new ArgumentException("At least one CharacterJoint setting must be provided.");
        }

        ValidateCommonJointSettingValues(breakForce, breakTorque);
        ValidateSoftJointLimitSpringUpdate(twistLimitSpring, "twistLimitSpring");
        ValidateSoftJointLimitSpringUpdate(swingLimitSpring, "swingLimitSpring");
        ValidateSoftJointLimitUpdate(lowTwistLimit, "lowTwistLimit");
        ValidateSoftJointLimitUpdate(highTwistLimit, "highTwistLimit");
        ValidateSoftJointLimitUpdate(swing1Limit, "swing1Limit");
        ValidateSoftJointLimitUpdate(swing2Limit, "swing2Limit");

        Undo.RecordObject(joint, "UnityMCP Set CharacterJoint Settings");
        ApplyCommonJointSettings(joint, autoConfigureConnectedAnchor, anchor, connectedAnchor, axis, enableCollision, breakForce, breakTorque, connectedBodyInstanceId, connectedAnchorMode);

        if (swingAxis.HasValue)
        {
            joint.swingAxis = swingAxis.Value;
        }

        if (enableProjection.HasValue)
        {
            joint.enableProjection = enableProjection.Value;
        }

        if (enablePreprocessing.HasValue)
        {
            joint.enablePreprocessing = enablePreprocessing.Value;
        }

        if (twistLimitSpring.IsSpecified)
        {
            joint.twistLimitSpring = ApplySoftJointLimitSpringUpdate(joint.twistLimitSpring, twistLimitSpring);
        }

        if (swingLimitSpring.IsSpecified)
        {
            joint.swingLimitSpring = ApplySoftJointLimitSpringUpdate(joint.swingLimitSpring, swingLimitSpring);
        }

        if (lowTwistLimit.IsSpecified)
        {
            joint.lowTwistLimit = ApplySoftJointLimitUpdate(joint.lowTwistLimit, lowTwistLimit);
        }

        if (highTwistLimit.IsSpecified)
        {
            joint.highTwistLimit = ApplySoftJointLimitUpdate(joint.highTwistLimit, highTwistLimit);
        }

        if (swing1Limit.IsSpecified)
        {
            joint.swing1Limit = ApplySoftJointLimitUpdate(joint.swing1Limit, swing1Limit);
        }

        if (swing2Limit.IsSpecified)
        {
            joint.swing2Limit = ApplySoftJointLimitUpdate(joint.swing2Limit, swing2Limit);
        }

        EditorUtility.SetDirty(joint);
        var connectionState = CreateJointAppliedConnectionState(joint, connectedAnchorMode);

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateCharacterJointSettingsSnapshot(joint),
            applied = new
            {
                autoConfigureConnectedAnchor = connectionState.AutoConfigureConnectedAnchor,
                anchor = anchor.HasValue,
                connectedAnchor = connectionState.ConnectedAnchor,
                connectedAnchorMode = connectionState.ConnectedAnchorMode,
                axis = axis.HasValue,
                enableCollision = enableCollision.HasValue,
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                connectedBodyInstanceId = connectionState.ConnectedBodyInstanceId,
                swingAxis = swingAxis.HasValue,
                enableProjection = enableProjection.HasValue,
                enablePreprocessing = enablePreprocessing.HasValue,
                twistLimitSpring = twistLimitSpring.IsSpecified,
                swingLimitSpring = swingLimitSpring.IsSpecified,
                lowTwistLimit = lowTwistLimit.IsSpecified,
                highTwistLimit = highTwistLimit.IsSpecified,
                swing1Limit = swing1Limit.IsSpecified,
                swing2Limit = swing2Limit.IsSpecified
            }
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildGetConfigurableJointSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "configurableJoint.getSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<ConfigurableJoint>(resolvedObject, "instanceId", "ConfigurableJoint");

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateConfigurableJointSettingsSnapshot(joint)
        };

        return UnityMcpProtocol.CreateResult(idToken, result);
    }

    private static string BuildSetConfigurableJointSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "configurableJoint.setSettings");
        var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
        var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
        var joint = ResolveComponentOfTypeTarget<ConfigurableJoint>(resolvedObject, "instanceId", "ConfigurableJoint");

        var autoConfigureConnectedAnchor = ParseOptionalBooleanValueParameter(paramsObject, "autoConfigureConnectedAnchor");
        var anchor = ParseOptionalVector3Parameter(paramsObject, "anchor");
        var connectedAnchor = ParseOptionalVector3Parameter(paramsObject, "connectedAnchor");
        var axis = ParseOptionalVector3Parameter(paramsObject, "axis");
        var secondaryAxis = ParseOptionalVector3Parameter(paramsObject, "secondaryAxis");
        var enableCollision = ParseOptionalBooleanValueParameter(paramsObject, "enableCollision");
        var breakForce = ParseOptionalFloatParameter(paramsObject, "breakForce");
        var breakTorque = ParseOptionalFloatParameter(paramsObject, "breakTorque");
        var connectedBodyInstanceId = ParseOptionalNullableIntegerParameter(paramsObject, "connectedBodyInstanceId");
        var connectedAnchorMode = ParseOptionalConnectedAnchorModeParameter(paramsObject, "connectedAnchorMode");
        var configuredInWorldSpace = ParseOptionalBooleanValueParameter(paramsObject, "configuredInWorldSpace");
        var swapBodies = ParseOptionalBooleanValueParameter(paramsObject, "swapBodies");
        var xMotion = ParseOptionalEnumParameter<ConfigurableJointMotion>(paramsObject, "xMotion");
        var yMotion = ParseOptionalEnumParameter<ConfigurableJointMotion>(paramsObject, "yMotion");
        var zMotion = ParseOptionalEnumParameter<ConfigurableJointMotion>(paramsObject, "zMotion");
        var angularXMotion = ParseOptionalEnumParameter<ConfigurableJointMotion>(paramsObject, "angularXMotion");
        var angularYMotion = ParseOptionalEnumParameter<ConfigurableJointMotion>(paramsObject, "angularYMotion");
        var angularZMotion = ParseOptionalEnumParameter<ConfigurableJointMotion>(paramsObject, "angularZMotion");
        var linearLimit = ParseOptionalSoftJointLimitParameter(paramsObject, "linearLimit");
        var lowAngularXLimit = ParseOptionalSoftJointLimitParameter(paramsObject, "lowAngularXLimit");
        var highAngularXLimit = ParseOptionalSoftJointLimitParameter(paramsObject, "highAngularXLimit");
        var angularYLimit = ParseOptionalSoftJointLimitParameter(paramsObject, "angularYLimit");
        var angularZLimit = ParseOptionalSoftJointLimitParameter(paramsObject, "angularZLimit");
        var targetPosition = ParseOptionalVector3Parameter(paramsObject, "targetPosition");
        var targetVelocity = ParseOptionalVector3Parameter(paramsObject, "targetVelocity");
        var targetAngularVelocity = ParseOptionalVector3Parameter(paramsObject, "targetAngularVelocity");
        var rotationDriveMode = ParseOptionalEnumParameter<RotationDriveMode>(paramsObject, "rotationDriveMode");
        var xDrive = ParseOptionalJointDriveParameter(paramsObject, "xDrive");
        var yDrive = ParseOptionalJointDriveParameter(paramsObject, "yDrive");
        var zDrive = ParseOptionalJointDriveParameter(paramsObject, "zDrive");
        var angularXDrive = ParseOptionalJointDriveParameter(paramsObject, "angularXDrive");
        var angularYZDrive = ParseOptionalJointDriveParameter(paramsObject, "angularYZDrive");
        var slerpDrive = ParseOptionalJointDriveParameter(paramsObject, "slerpDrive");
        var projectionMode = ParseOptionalEnumParameter<JointProjectionMode>(paramsObject, "projectionMode");
        var projectionDistance = ParseOptionalFloatParameter(paramsObject, "projectionDistance");
        var projectionAngle = ParseOptionalFloatParameter(paramsObject, "projectionAngle");

        if (!autoConfigureConnectedAnchor.HasValue &&
            !anchor.HasValue &&
            !connectedAnchor.HasValue &&
            !axis.HasValue &&
            !secondaryAxis.HasValue &&
            !enableCollision.HasValue &&
            !breakForce.HasValue &&
            !breakTorque.HasValue &&
            !connectedBodyInstanceId.IsSpecified &&
            !connectedAnchorMode.HasValue &&
            !configuredInWorldSpace.HasValue &&
            !swapBodies.HasValue &&
            !xMotion.HasValue &&
            !yMotion.HasValue &&
            !zMotion.HasValue &&
            !angularXMotion.HasValue &&
            !angularYMotion.HasValue &&
            !angularZMotion.HasValue &&
            !linearLimit.IsSpecified &&
            !lowAngularXLimit.IsSpecified &&
            !highAngularXLimit.IsSpecified &&
            !angularYLimit.IsSpecified &&
            !angularZLimit.IsSpecified &&
            !targetPosition.HasValue &&
            !targetVelocity.HasValue &&
            !targetAngularVelocity.HasValue &&
            !rotationDriveMode.HasValue &&
            !xDrive.IsSpecified &&
            !yDrive.IsSpecified &&
            !zDrive.IsSpecified &&
            !angularXDrive.IsSpecified &&
            !angularYZDrive.IsSpecified &&
            !slerpDrive.IsSpecified &&
            !projectionMode.HasValue &&
            !projectionDistance.HasValue &&
            !projectionAngle.HasValue)
        {
            throw new ArgumentException("At least one ConfigurableJoint setting must be provided.");
        }

        ValidateCommonJointSettingValues(breakForce, breakTorque);
        ValidateSoftJointLimitUpdate(linearLimit, "linearLimit");
        ValidateSoftJointLimitUpdate(lowAngularXLimit, "lowAngularXLimit");
        ValidateSoftJointLimitUpdate(highAngularXLimit, "highAngularXLimit");
        ValidateSoftJointLimitUpdate(angularYLimit, "angularYLimit");
        ValidateSoftJointLimitUpdate(angularZLimit, "angularZLimit");
        ValidateJointDriveUpdate(xDrive, "xDrive");
        ValidateJointDriveUpdate(yDrive, "yDrive");
        ValidateJointDriveUpdate(zDrive, "zDrive");
        ValidateJointDriveUpdate(angularXDrive, "angularXDrive");
        ValidateJointDriveUpdate(angularYZDrive, "angularYZDrive");
        ValidateJointDriveUpdate(slerpDrive, "slerpDrive");
        if (projectionDistance.HasValue && projectionDistance.Value < 0f)
        {
            throw new ArgumentException("Parameter 'projectionDistance' must be greater than or equal to 0.");
        }

        if (projectionAngle.HasValue && projectionAngle.Value < 0f)
        {
            throw new ArgumentException("Parameter 'projectionAngle' must be greater than or equal to 0.");
        }

        Undo.RecordObject(joint, "UnityMCP Set ConfigurableJoint Settings");
        ApplyCommonJointSettings(joint, autoConfigureConnectedAnchor, anchor, connectedAnchor, axis, enableCollision, breakForce, breakTorque, connectedBodyInstanceId, connectedAnchorMode);

        if (secondaryAxis.HasValue)
        {
            joint.secondaryAxis = secondaryAxis.Value;
        }

        if (configuredInWorldSpace.HasValue)
        {
            joint.configuredInWorldSpace = configuredInWorldSpace.Value;
        }

        if (swapBodies.HasValue)
        {
            joint.swapBodies = swapBodies.Value;
        }

        if (xMotion.HasValue)
        {
            joint.xMotion = xMotion.Value;
        }

        if (yMotion.HasValue)
        {
            joint.yMotion = yMotion.Value;
        }

        if (zMotion.HasValue)
        {
            joint.zMotion = zMotion.Value;
        }

        if (angularXMotion.HasValue)
        {
            joint.angularXMotion = angularXMotion.Value;
        }

        if (angularYMotion.HasValue)
        {
            joint.angularYMotion = angularYMotion.Value;
        }

        if (angularZMotion.HasValue)
        {
            joint.angularZMotion = angularZMotion.Value;
        }

        if (linearLimit.IsSpecified)
        {
            joint.linearLimit = ApplySoftJointLimitUpdate(joint.linearLimit, linearLimit);
        }

        if (lowAngularXLimit.IsSpecified)
        {
            joint.lowAngularXLimit = ApplySoftJointLimitUpdate(joint.lowAngularXLimit, lowAngularXLimit);
        }

        if (highAngularXLimit.IsSpecified)
        {
            joint.highAngularXLimit = ApplySoftJointLimitUpdate(joint.highAngularXLimit, highAngularXLimit);
        }

        if (angularYLimit.IsSpecified)
        {
            joint.angularYLimit = ApplySoftJointLimitUpdate(joint.angularYLimit, angularYLimit);
        }

        if (angularZLimit.IsSpecified)
        {
            joint.angularZLimit = ApplySoftJointLimitUpdate(joint.angularZLimit, angularZLimit);
        }

        if (targetPosition.HasValue)
        {
            joint.targetPosition = targetPosition.Value;
        }

        if (targetVelocity.HasValue)
        {
            joint.targetVelocity = targetVelocity.Value;
        }

        if (targetAngularVelocity.HasValue)
        {
            joint.targetAngularVelocity = targetAngularVelocity.Value;
        }

        if (rotationDriveMode.HasValue)
        {
            joint.rotationDriveMode = rotationDriveMode.Value;
        }

        if (xDrive.IsSpecified)
        {
            joint.xDrive = ApplyJointDriveUpdate(joint.xDrive, xDrive);
        }

        if (yDrive.IsSpecified)
        {
            joint.yDrive = ApplyJointDriveUpdate(joint.yDrive, yDrive);
        }

        if (zDrive.IsSpecified)
        {
            joint.zDrive = ApplyJointDriveUpdate(joint.zDrive, zDrive);
        }

        if (angularXDrive.IsSpecified)
        {
            joint.angularXDrive = ApplyJointDriveUpdate(joint.angularXDrive, angularXDrive);
        }

        if (angularYZDrive.IsSpecified)
        {
            joint.angularYZDrive = ApplyJointDriveUpdate(joint.angularYZDrive, angularYZDrive);
        }

        if (slerpDrive.IsSpecified)
        {
            joint.slerpDrive = ApplyJointDriveUpdate(joint.slerpDrive, slerpDrive);
        }

        if (projectionMode.HasValue)
        {
            joint.projectionMode = projectionMode.Value;
        }

        if (projectionDistance.HasValue)
        {
            joint.projectionDistance = projectionDistance.Value;
        }

        if (projectionAngle.HasValue)
        {
            joint.projectionAngle = projectionAngle.Value;
        }

        EditorUtility.SetDirty(joint);
        var connectionState = CreateJointAppliedConnectionState(joint, connectedAnchorMode);

        var result = new
        {
            target = CreateObjectSummary(joint.gameObject),
            component = CreateComponentSummary(joint),
            settings = CreateConfigurableJointSettingsSnapshot(joint),
            applied = new
            {
                autoConfigureConnectedAnchor = connectionState.AutoConfigureConnectedAnchor,
                anchor = anchor.HasValue,
                connectedAnchor = connectionState.ConnectedAnchor,
                connectedAnchorMode = connectionState.ConnectedAnchorMode,
                axis = axis.HasValue,
                secondaryAxis = secondaryAxis.HasValue,
                enableCollision = enableCollision.HasValue,
                breakForce = breakForce.HasValue,
                breakTorque = breakTorque.HasValue,
                connectedBodyInstanceId = connectionState.ConnectedBodyInstanceId,
                configuredInWorldSpace = configuredInWorldSpace.HasValue,
                swapBodies = swapBodies.HasValue,
                xMotion = xMotion.HasValue,
                yMotion = yMotion.HasValue,
                zMotion = zMotion.HasValue,
                angularXMotion = angularXMotion.HasValue,
                angularYMotion = angularYMotion.HasValue,
                angularZMotion = angularZMotion.HasValue,
                linearLimit = linearLimit.IsSpecified,
                lowAngularXLimit = lowAngularXLimit.IsSpecified,
                highAngularXLimit = highAngularXLimit.IsSpecified,
                angularYLimit = angularYLimit.IsSpecified,
                angularZLimit = angularZLimit.IsSpecified,
                targetPosition = targetPosition.HasValue,
                targetVelocity = targetVelocity.HasValue,
                targetAngularVelocity = targetAngularVelocity.HasValue,
                rotationDriveMode = rotationDriveMode.HasValue,
                xDrive = xDrive.IsSpecified,
                yDrive = yDrive.IsSpecified,
                zDrive = zDrive.IsSpecified,
                angularXDrive = angularXDrive.IsSpecified,
                angularYZDrive = angularYZDrive.IsSpecified,
                slerpDrive = slerpDrive.IsSpecified,
                projectionMode = projectionMode.HasValue,
                projectionDistance = projectionDistance.HasValue,
                projectionAngle = projectionAngle.HasValue
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

    // ── Batch 8: Material/Shader Properties ────────────────────────────

    private static Material LoadMaterialFromAssetPath(string assetPath)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (material == null)
            throw new ArgumentException($"No Material found at '{assetPath}'.");
        return material;
    }

    private static string GetShaderPropertyTypeName(ShaderUtil.ShaderPropertyType type)
    {
        return type switch
        {
            ShaderUtil.ShaderPropertyType.Color => "Color",
            ShaderUtil.ShaderPropertyType.Vector => "Vector",
            ShaderUtil.ShaderPropertyType.Float => "Float",
            ShaderUtil.ShaderPropertyType.Range => "Range",
            ShaderUtil.ShaderPropertyType.TexEnv => "Texture",
            _ => type.ToString()
        };
    }

    private static object GetShaderPropertyValue(Material material, string propName, ShaderUtil.ShaderPropertyType propType)
    {
        switch (propType)
        {
            case ShaderUtil.ShaderPropertyType.Color:
                var c = material.GetColor(propName);
                return new { r = c.r, g = c.g, b = c.b, a = c.a };
            case ShaderUtil.ShaderPropertyType.Vector:
                var v = material.GetVector(propName);
                return new { x = v.x, y = v.y, z = v.z, w = v.w };
            case ShaderUtil.ShaderPropertyType.Float:
            case ShaderUtil.ShaderPropertyType.Range:
                return material.GetFloat(propName);
            case ShaderUtil.ShaderPropertyType.TexEnv:
                var tex = material.GetTexture(propName);
                return tex != null ? AssetDatabase.GetAssetPath(tex) : null;
            default:
                // Try Int for newer Unity versions (ShaderPropertyType value 5)
                if ((int)propType == 5)
                    return material.GetInt(propName);
                return null;
        }
    }

    private static string BuildGetMaterialPropertiesResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "material.getProperties");
        var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
        var material = LoadMaterialFromAssetPath(assetPath);
        var shader = material.shader;
        var propertyCount = ShaderUtil.GetPropertyCount(shader);

        var properties = new object[propertyCount];
        for (var i = 0; i < propertyCount; i++)
        {
            var propName = ShaderUtil.GetPropertyName(shader, i);
            var propType = ShaderUtil.GetPropertyType(shader, i);
            properties[i] = new
            {
                name = propName,
                type = GetShaderPropertyTypeName(propType),
                value = GetShaderPropertyValue(material, propName, propType)
            };
        }

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            assetPath,
            shaderName = shader.name,
            propertyCount,
            properties
        });
    }

    private static string BuildGetMaterialPropertyResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "material.getProperty");
        var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
        var propertyName = ParseRequiredStringParameter(paramsObject, "propertyName");
        var material = LoadMaterialFromAssetPath(assetPath);
        var shader = material.shader;
        var propertyCount = ShaderUtil.GetPropertyCount(shader);

        for (var i = 0; i < propertyCount; i++)
        {
            var propName = ShaderUtil.GetPropertyName(shader, i);
            if (propName == propertyName)
            {
                var propType = ShaderUtil.GetPropertyType(shader, i);
                return UnityMcpProtocol.CreateResult(idToken, new
                {
                    assetPath,
                    name = propName,
                    type = GetShaderPropertyTypeName(propType),
                    value = GetShaderPropertyValue(material, propName, propType)
                });
            }
        }

        throw new ArgumentException($"Property '{propertyName}' not found on shader '{shader.name}'.");
    }

    private static string BuildSetMaterialPropertyResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "material.setProperty");
        var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
        var propertyName = ParseRequiredStringParameter(paramsObject, "propertyName");
        var propertyType = ParseRequiredStringParameter(paramsObject, "propertyType").ToLowerInvariant();
        var material = LoadMaterialFromAssetPath(assetPath);

        if (!material.HasProperty(propertyName))
            throw new ArgumentException($"Property '{propertyName}' not found on material at '{assetPath}'.");

        if (!paramsObject.TryGetValue("value", out var valueToken))
            throw new ArgumentException("Parameter 'value' is required.");

        switch (propertyType)
        {
            case "color":
            {
                var r = valueToken["r"]?.Value<float>() ?? 0f;
                var g = valueToken["g"]?.Value<float>() ?? 0f;
                var b = valueToken["b"]?.Value<float>() ?? 0f;
                var a = valueToken["a"]?.Value<float>() ?? 1f;
                material.SetColor(propertyName, new Color(r, g, b, a));
                break;
            }
            case "vector":
            {
                var x = valueToken["x"]?.Value<float>() ?? 0f;
                var y = valueToken["y"]?.Value<float>() ?? 0f;
                var z = valueToken["z"]?.Value<float>() ?? 0f;
                var w = valueToken["w"]?.Value<float>() ?? 0f;
                material.SetVector(propertyName, new Vector4(x, y, z, w));
                break;
            }
            case "float":
                material.SetFloat(propertyName, valueToken.Value<float>());
                break;
            case "int":
                material.SetInt(propertyName, valueToken.Value<int>());
                break;
            case "texture":
            {
                var texturePath = valueToken.Type == JTokenType.Null ? null : valueToken.Value<string>();
                if (string.IsNullOrEmpty(texturePath))
                {
                    material.SetTexture(propertyName, null);
                }
                else
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
                    if (texture == null)
                        throw new ArgumentException($"No Texture found at '{texturePath}'.");
                    material.SetTexture(propertyName, texture);
                }
                break;
            }
            default:
                throw new ArgumentException($"Invalid propertyType '{propertyType}'. Expected: color, float, int, vector, texture.");
        }

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            assetPath,
            propertyName,
            propertyType,
            updated = true
        });
    }

    private static string BuildGetMaterialKeywordsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "material.getKeywords");
        var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
        var material = LoadMaterialFromAssetPath(assetPath);

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            assetPath,
            keywords = material.shaderKeywords
        });
    }

    private static string BuildSetMaterialKeywordResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "material.setKeyword");
        var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
        var keyword = ParseRequiredStringParameter(paramsObject, "keyword");
        var enabled = ParseRequiredBooleanParameter(paramsObject, "enabled");
        var material = LoadMaterialFromAssetPath(assetPath);

        if (enabled)
            material.EnableKeyword(keyword);
        else
            material.DisableKeyword(keyword);

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            assetPath,
            keyword,
            enabled,
            keywords = material.shaderKeywords
        });
    }

    private static string BuildGetMaterialShaderResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "material.getShader");
        var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
        var material = LoadMaterialFromAssetPath(assetPath);

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            assetPath,
            shaderName = material.shader.name
        });
    }

    private static string BuildSetMaterialShaderResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "material.setShader");
        var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
        var shaderName = ParseRequiredStringParameter(paramsObject, "shaderName");
        var material = LoadMaterialFromAssetPath(assetPath);

        var shader = Shader.Find(shaderName);
        if (shader == null)
            throw new ArgumentException($"Shader '{shaderName}' not found.");

        material.shader = shader;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            assetPath,
            shaderName = material.shader.name,
            updated = true
        });
    }

    private static string BuildGetMaterialRenderQueueResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "material.getRenderQueue");
        var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
        var material = LoadMaterialFromAssetPath(assetPath);

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            assetPath,
            renderQueue = material.renderQueue
        });
    }

    private static string BuildSetMaterialRenderQueueResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "material.setRenderQueue");
        var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
        var renderQueue = ParseRequiredIntegerParameter(paramsObject, "renderQueue");
        var material = LoadMaterialFromAssetPath(assetPath);

        material.renderQueue = renderQueue;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            assetPath,
            renderQueue = material.renderQueue,
            updated = true
        });
    }

    private static string BuildGetSceneHierarchyResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "scene.getHierarchy");

        // Parse optional parameters
        var includeInactive = ParseOptionalBooleanParameter(paramsObject, "includeInactive", true);
        var maxDepth = ParseOptionalIntegerParameter(paramsObject, "maxDepth");
        var rootFilter = ParseOptionalStringParameter(paramsObject, "rootFilter");
        var scenePath = ParseOptionalStringParameter(paramsObject, "scenePath");
        var allScenes = ParseOptionalBooleanParameter(paramsObject, "allScenes");
        var maxNodes = ParseOptionalIntegerParameter(paramsObject, "maxNodes") ?? 2000;

        if (maxNodes < 1 || maxNodes > 10000)
        {
            throw new ArgumentException("Parameter 'maxNodes' must be between 1 and 10000.");
        }

        if (allScenes)
        {
            // Return hierarchy for all open scenes
            var allSceneResults = new List<object>();
            var sceneCount = SceneManager.sceneCount;

            for (var index = 0; index < sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (!scene.isLoaded) continue;

                var sceneResult = GetSceneHierarchy(scene, includeInactive, maxDepth, rootFilter, maxNodes);
                allSceneResults.Add(sceneResult);
            }

            return UnityMcpProtocol.CreateResult(idToken, allSceneResults);
        }
        else
        {
            // Return hierarchy for specific scene or active scene
            Scene targetScene;
            if (!string.IsNullOrWhiteSpace(scenePath))
            {
                targetScene = SceneManager.GetSceneByPath(scenePath);
                if (!targetScene.isLoaded)
                {
                    throw new ArgumentException($"Scene at path '{scenePath}' is not loaded or does not exist.");
                }
            }
            else
            {
                targetScene = SceneManager.GetActiveScene();
            }

            var result = GetSceneHierarchy(targetScene, includeInactive, maxDepth, rootFilter, maxNodes);
            return UnityMcpProtocol.CreateResult(idToken, result);
        }
    }

    private static object GetSceneHierarchy(Scene scene, bool includeInactive, int? maxDepth, string? rootFilter, int maxNodes)
    {
        var nodes = new List<object>();
        var nodeCount = 0;
        var truncated = false;

        // Get root GameObjects
        var rootGameObjects = scene.GetRootGameObjects();

        // Filter by root name if specified
        if (!string.IsNullOrWhiteSpace(rootFilter))
        {
            rootGameObjects = rootGameObjects.Where(go => go.name.Equals(rootFilter, StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        // Traverse hierarchy
        foreach (var rootGameObject in rootGameObjects)
        {
            if (!includeInactive && !rootGameObject.activeInHierarchy)
                continue;

            if (TraverseGameObject(rootGameObject, nodes, ref nodeCount, maxNodes, maxDepth, 0, includeInactive, null))
            {
                truncated = true;
                break;
            }
        }

        return new
        {
            sceneName = scene.name,
            scenePath = scene.path,
            nodeCount,
            truncated,
            nodes = nodes
        };
    }

    private static bool TraverseGameObject(GameObject gameObject, List<object> nodes, ref int nodeCount, int maxNodes, int? maxDepth, int currentDepth, bool includeInactive, int? parentInstanceId)
    {
        if (nodeCount >= maxNodes)
            return true; // Signal truncation

        if (maxDepth.HasValue && currentDepth > maxDepth.Value)
            return false;

        if (!includeInactive && !gameObject.activeInHierarchy)
            return false;

        // Get component type names
        var components = gameObject.GetComponents<Component>()
            .Where(c => c != null)
            .Select(c => c.GetType().FullName)
            .ToArray();

        // Add node to list
        nodes.Add(new
        {
            name = gameObject.name,
            instanceId = gameObject.GetInstanceID(),
            activeSelf = gameObject.activeSelf,
            activeInHierarchy = gameObject.activeInHierarchy,
            depth = currentDepth,
            parentInstanceId = parentInstanceId,
            components = components
        });

        nodeCount++;
        var currentInstanceId = gameObject.GetInstanceID();

        // Traverse children
        var transform = gameObject.transform;
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i).gameObject;
            if (TraverseGameObject(child, nodes, ref nodeCount, maxNodes, maxDepth, currentDepth + 1, includeInactive, currentInstanceId))
            {
                return true; // Propagate truncation signal
            }
        }

        return false;
    }

    private static string BuildGetPlayerSettingsResponse(JToken idToken)
    {
        return UnityMcpProtocol.CreateResult(idToken, new
        {
            companyName = PlayerSettings.companyName,
            productName = PlayerSettings.productName,
            applicationIdentifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup)),
            bundleVersion = PlayerSettings.bundleVersion,
            defaultScreenWidth = PlayerSettings.defaultScreenWidth,
            defaultScreenHeight = PlayerSettings.defaultScreenHeight,
            fullscreenMode = PlayerSettings.fullScreenMode.ToString(),
            runInBackground = PlayerSettings.runInBackground,
            scriptingBackend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup)).ToString(),
            apiCompatibilityLevel = PlayerSettings.GetApiCompatibilityLevel(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup)).ToString(),
            colorSpace = PlayerSettings.colorSpace.ToString(),
            allowUnsafeCode = PlayerSettings.allowUnsafeCode,
            stripEngineCode = PlayerSettings.stripEngineCode,
            defaultInterfaceOrientation = PlayerSettings.defaultInterfaceOrientation.ToString(),
            useAnimatedAutorotation = PlayerSettings.useAnimatedAutorotation,
            gpuSkinning = PlayerSettings.gpuSkinning,
            graphicsJobs = PlayerSettings.graphicsJobs
        });
    }

    private static string BuildSetPlayerSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "projectSettings.setPlayerSettings");
        var updated = new List<string>();

        if (paramsObject.TryGetValue("companyName", out var companyNameToken) && companyNameToken.Type == JTokenType.String)
        {
            PlayerSettings.companyName = companyNameToken.Value<string>()!;
            updated.Add("companyName");
        }

        if (paramsObject.TryGetValue("productName", out var productNameToken) && productNameToken.Type == JTokenType.String)
        {
            PlayerSettings.productName = productNameToken.Value<string>()!;
            updated.Add("productName");
        }

        if (paramsObject.TryGetValue("applicationIdentifier", out var appIdToken) && appIdToken.Type == JTokenType.String)
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup), appIdToken.Value<string>()!);
            updated.Add("applicationIdentifier");
        }

        if (paramsObject.TryGetValue("bundleVersion", out var bundleVersionToken) && bundleVersionToken.Type == JTokenType.String)
        {
            PlayerSettings.bundleVersion = bundleVersionToken.Value<string>()!;
            updated.Add("bundleVersion");
        }

        if (paramsObject.TryGetValue("defaultScreenWidth", out var widthToken) && widthToken.Type == JTokenType.Integer)
        {
            PlayerSettings.defaultScreenWidth = widthToken.Value<int>();
            updated.Add("defaultScreenWidth");
        }

        if (paramsObject.TryGetValue("defaultScreenHeight", out var heightToken) && heightToken.Type == JTokenType.Integer)
        {
            PlayerSettings.defaultScreenHeight = heightToken.Value<int>();
            updated.Add("defaultScreenHeight");
        }

        if (paramsObject.TryGetValue("fullscreenMode", out var fullscreenToken) && fullscreenToken.Type == JTokenType.String)
        {
            if (Enum.TryParse<FullScreenMode>(fullscreenToken.Value<string>(), out var fullscreenMode))
            {
                PlayerSettings.fullScreenMode = fullscreenMode;
                updated.Add("fullscreenMode");
            }
        }

        if (paramsObject.TryGetValue("runInBackground", out var runInBgToken) && runInBgToken.Type == JTokenType.Boolean)
        {
            PlayerSettings.runInBackground = runInBgToken.Value<bool>();
            updated.Add("runInBackground");
        }

        if (paramsObject.TryGetValue("colorSpace", out var colorSpaceToken) && colorSpaceToken.Type == JTokenType.String)
        {
            if (Enum.TryParse<ColorSpace>(colorSpaceToken.Value<string>(), out var colorSpace))
            {
                PlayerSettings.colorSpace = colorSpace;
                updated.Add("colorSpace");
            }
        }

        if (paramsObject.TryGetValue("allowUnsafeCode", out var allowUnsafeToken) && allowUnsafeToken.Type == JTokenType.Boolean)
        {
            PlayerSettings.allowUnsafeCode = allowUnsafeToken.Value<bool>();
            updated.Add("allowUnsafeCode");
        }

        if (paramsObject.TryGetValue("stripEngineCode", out var stripToken) && stripToken.Type == JTokenType.Boolean)
        {
            PlayerSettings.stripEngineCode = stripToken.Value<bool>();
            updated.Add("stripEngineCode");
        }

        if (updated.Count > 0)
        {
            AssetDatabase.SaveAssets();
        }

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            updated = updated.ToArray(),
            count = updated.Count
        });
    }

    private static string BuildGetQualitySettingsResponse(JToken idToken)
    {
        var currentLevel = QualitySettings.GetQualityLevel();
        var levelNames = QualitySettings.names;

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            currentLevelIndex = currentLevel,
            currentLevelName = levelNames[currentLevel],
            allLevelNames = levelNames,
            activeSettings = new
            {
                pixelLightCount = QualitySettings.pixelLightCount,
                shadows = QualitySettings.shadows.ToString(),
                shadowResolution = QualitySettings.shadowResolution.ToString(),
                shadowDistance = QualitySettings.shadowDistance,
                antiAliasing = QualitySettings.antiAliasing,
                softParticles = QualitySettings.softParticles,
                vSyncCount = QualitySettings.vSyncCount,
                textureQuality = QualitySettings.globalTextureMipmapLimit,
                anisotropicFiltering = QualitySettings.anisotropicFiltering.ToString(),
                lodBias = QualitySettings.lodBias,
                maximumLODLevel = QualitySettings.maximumLODLevel,
                particleRaycastBudget = QualitySettings.particleRaycastBudget,
                billboardsFaceCameraPosition = QualitySettings.billboardsFaceCameraPosition
            }
        });
    }

    private static string BuildSetQualitySettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "projectSettings.setQualitySettings");
        var updated = new List<string>();

        var targetLevel = QualitySettings.GetQualityLevel();
        if (paramsObject.TryGetValue("levelIndex", out var levelIndexToken) && levelIndexToken.Type == JTokenType.Integer)
        {
            var newLevel = levelIndexToken.Value<int>();
            if (newLevel >= 0 && newLevel < QualitySettings.names.Length)
            {
                targetLevel = newLevel;
                QualitySettings.SetQualityLevel(targetLevel);
                updated.Add("levelIndex");
            }
        }

        if (paramsObject.TryGetValue("pixelLightCount", out var pixelLightToken) && pixelLightToken.Type == JTokenType.Integer)
        {
            QualitySettings.pixelLightCount = pixelLightToken.Value<int>();
            updated.Add("pixelLightCount");
        }

        if (paramsObject.TryGetValue("shadows", out var shadowsToken) && shadowsToken.Type == JTokenType.String)
        {
            if (Enum.TryParse<ShadowQuality>(shadowsToken.Value<string>(), out var shadows))
            {
                QualitySettings.shadows = shadows;
                updated.Add("shadows");
            }
        }

        if (paramsObject.TryGetValue("shadowResolution", out var shadowResToken) && shadowResToken.Type == JTokenType.String)
        {
            if (Enum.TryParse<ShadowResolution>(shadowResToken.Value<string>(), out var shadowRes))
            {
                QualitySettings.shadowResolution = shadowRes;
                updated.Add("shadowResolution");
            }
        }

        if (paramsObject.TryGetValue("shadowDistance", out var shadowDistToken) && shadowDistToken.Type == JTokenType.Float)
        {
            QualitySettings.shadowDistance = shadowDistToken.Value<float>();
            updated.Add("shadowDistance");
        }

        if (paramsObject.TryGetValue("antiAliasing", out var aaToken) && aaToken.Type == JTokenType.Integer)
        {
            var aa = aaToken.Value<int>();
            if (aa == 0 || aa == 2 || aa == 4 || aa == 8)
            {
                QualitySettings.antiAliasing = aa;
                updated.Add("antiAliasing");
            }
        }

        if (paramsObject.TryGetValue("vSyncCount", out var vSyncToken) && vSyncToken.Type == JTokenType.Integer)
        {
            var vSync = vSyncToken.Value<int>();
            if (vSync >= 0 && vSync <= 2)
            {
                QualitySettings.vSyncCount = vSync;
                updated.Add("vSyncCount");
            }
        }

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            updated = updated.ToArray(),
            count = updated.Count,
            targetLevel = targetLevel,
            targetLevelName = QualitySettings.names[targetLevel]
        });
    }

    private static string BuildGetPhysicsSettingsResponse(JToken idToken)
    {
        var gravity = Physics.gravity;

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            gravity = new[] { gravity.x, gravity.y, gravity.z },
            defaultSolverIterations = Physics.defaultSolverIterations,
            defaultSolverVelocityIterations = Physics.defaultSolverVelocityIterations,
            sleepThreshold = Physics.sleepThreshold,
            defaultContactOffset = Physics.defaultContactOffset,
            bounceThreshold = Physics.bounceThreshold,
            defaultMaxDepenetrationVelocity = Physics.defaultMaxDepenetrationVelocity,
            autoSimulation = Physics.simulationMode == SimulationMode.AutoSimulation,
            autoSyncTransforms = true, // Physics.SyncTransforms() is now a method call
            reuseCollisionCallbacks = Physics.reuseCollisionCallbacks
        });
    }

    private static string BuildSetPhysicsSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "projectSettings.setPhysicsSettings");
        var updated = new List<string>();

        if (paramsObject.TryGetValue("gravity", out var gravityToken) && gravityToken.Type == JTokenType.Array)
        {
            var gravityArray = ParseFloatArrayToken(gravityToken, "gravity", 3);
            Physics.gravity = new Vector3(gravityArray[0], gravityArray[1], gravityArray[2]);
            updated.Add("gravity");
        }

        if (paramsObject.TryGetValue("defaultSolverIterations", out var solverIterToken) && solverIterToken.Type == JTokenType.Integer)
        {
            var value = solverIterToken.Value<int>();
            if (value >= 1)
            {
                Physics.defaultSolverIterations = value;
                updated.Add("defaultSolverIterations");
            }
        }

        if (paramsObject.TryGetValue("defaultSolverVelocityIterations", out var velIterToken) && velIterToken.Type == JTokenType.Integer)
        {
            var value = velIterToken.Value<int>();
            if (value >= 1)
            {
                Physics.defaultSolverVelocityIterations = value;
                updated.Add("defaultSolverVelocityIterations");
            }
        }

        if (paramsObject.TryGetValue("sleepThreshold", out var sleepToken) && sleepToken.Type == JTokenType.Float)
        {
            var value = sleepToken.Value<float>();
            if (value >= 0)
            {
                Physics.sleepThreshold = value;
                updated.Add("sleepThreshold");
            }
        }

        if (paramsObject.TryGetValue("defaultContactOffset", out var contactOffsetToken) && contactOffsetToken.Type == JTokenType.Float)
        {
            var value = contactOffsetToken.Value<float>();
            if (value >= 0)
            {
                Physics.defaultContactOffset = value;
                updated.Add("defaultContactOffset");
            }
        }

        if (paramsObject.TryGetValue("bounceThreshold", out var bounceToken) && bounceToken.Type == JTokenType.Float)
        {
            var value = bounceToken.Value<float>();
            if (value >= 0)
            {
                Physics.bounceThreshold = value;
                updated.Add("bounceThreshold");
            }
        }

        if (paramsObject.TryGetValue("autoSimulation", out var autoSimToken) && autoSimToken.Type == JTokenType.Boolean)
        {
            Physics.simulationMode = autoSimToken.Value<bool>() ? SimulationMode.AutoSimulation : SimulationMode.Script;
            updated.Add("autoSimulation");
        }

        if (paramsObject.TryGetValue("autoSyncTransforms", out var autoSyncToken) && autoSyncToken.Type == JTokenType.Boolean)
        {
            if (autoSyncToken.Value<bool>()) Physics.SyncTransforms(); // Call sync method if true
            updated.Add("autoSyncTransforms");
        }

        if (updated.Count > 0)
        {
            AssetDatabase.SaveAssets();
        }

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            updated = updated.ToArray(),
            count = updated.Count
        });
    }

    private static string BuildGetPhysics2DSettingsResponse(JToken idToken)
    {
        var gravity = Physics2D.gravity;

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            gravity = new[] { gravity.x, gravity.y },
            defaultContactOffset = Physics2D.defaultContactOffset,
            velocityIterations = Physics2D.velocityIterations,
            positionIterations = Physics2D.positionIterations,
            bounceThreshold = Physics2D.bounceThreshold,
            maxLinearCorrection = Physics2D.maxLinearCorrection,
            maxAngularCorrection = Physics2D.maxAngularCorrection,
            maxTranslationSpeed = Physics2D.maxTranslationSpeed,
            maxRotationSpeed = Physics2D.maxRotationSpeed,
            baumgarteScale = Physics2D.baumgarteScale,
            baumgarteTOIScale = Physics2D.baumgarteTOIScale,
            timeToSleep = Physics2D.timeToSleep,
            linearSleepTolerance = Physics2D.linearSleepTolerance,
            angularSleepTolerance = Physics2D.angularSleepTolerance,
            autoSimulation = Physics2D.simulationMode == SimulationMode2D.FixedUpdate,
            autoSyncTransforms = true, // Physics2D.SyncTransforms() is now a method call
            callbacksOnDisable = Physics2D.callbacksOnDisable,
            reuseCollisionCallbacks = Physics2D.reuseCollisionCallbacks
        });
    }

    private static string BuildSetPhysics2DSettingsResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "physics2D.setSettings");
        var updated = new List<string>();

        if (paramsObject.TryGetValue("gravity", out var gravityToken) && gravityToken.Type == JTokenType.Array)
        {
            var gravityArray = ParseFloatArrayToken(gravityToken, "gravity", 2);
            Physics2D.gravity = new Vector2(gravityArray[0], gravityArray[1]);
            updated.Add("gravity");
        }

        if (paramsObject.TryGetValue("defaultContactOffset", out var contactOffsetToken) && contactOffsetToken.Type == JTokenType.Float)
        {
            var value = contactOffsetToken.Value<float>();
            if (value >= 0)
            {
                Physics2D.defaultContactOffset = value;
                updated.Add("defaultContactOffset");
            }
        }

        if (paramsObject.TryGetValue("velocityIterations", out var velIterToken) && velIterToken.Type == JTokenType.Integer)
        {
            var value = velIterToken.Value<int>();
            if (value >= 1)
            {
                Physics2D.velocityIterations = value;
                updated.Add("velocityIterations");
            }
        }

        if (paramsObject.TryGetValue("positionIterations", out var posIterToken) && posIterToken.Type == JTokenType.Integer)
        {
            var value = posIterToken.Value<int>();
            if (value >= 1)
            {
                Physics2D.positionIterations = value;
                updated.Add("positionIterations");
            }
        }

        if (paramsObject.TryGetValue("velocityThreshold", out var velThresholdToken) && velThresholdToken.Type == JTokenType.Float)
        {
            var value = velThresholdToken.Value<float>();
            if (value >= 0)
            {
                Physics2D.bounceThreshold = value;
                updated.Add("bounceThreshold");
            }
        }

        if (paramsObject.TryGetValue("maxLinearCorrection", out var maxLinearToken) && maxLinearToken.Type == JTokenType.Float)
        {
            var value = maxLinearToken.Value<float>();
            if (value >= 0)
            {
                Physics2D.maxLinearCorrection = value;
                updated.Add("maxLinearCorrection");
            }
        }

        if (paramsObject.TryGetValue("maxAngularCorrection", out var maxAngularToken) && maxAngularToken.Type == JTokenType.Float)
        {
            var value = maxAngularToken.Value<float>();
            if (value >= 0)
            {
                Physics2D.maxAngularCorrection = value;
                updated.Add("maxAngularCorrection");
            }
        }

        if (paramsObject.TryGetValue("maxTranslationSpeed", out var maxTransToken) && maxTransToken.Type == JTokenType.Float)
        {
            var value = maxTransToken.Value<float>();
            if (value >= 0)
            {
                Physics2D.maxTranslationSpeed = value;
                updated.Add("maxTranslationSpeed");
            }
        }

        if (paramsObject.TryGetValue("maxRotationSpeed", out var maxRotToken) && maxRotToken.Type == JTokenType.Float)
        {
            var value = maxRotToken.Value<float>();
            if (value >= 0)
            {
                Physics2D.maxRotationSpeed = value;
                updated.Add("maxRotationSpeed");
            }
        }

        if (paramsObject.TryGetValue("autoSimulation", out var autoSimToken) && autoSimToken.Type == JTokenType.Boolean)
        {
            Physics2D.simulationMode = autoSimToken.Value<bool>() ? SimulationMode2D.FixedUpdate : SimulationMode2D.Script;
            updated.Add("autoSimulation");
        }

        if (paramsObject.TryGetValue("autoSyncTransforms", out var autoSyncToken) && autoSyncToken.Type == JTokenType.Boolean)
        {
            if (autoSyncToken.Value<bool>()) Physics2D.SyncTransforms(); // Call sync method if true
            updated.Add("autoSyncTransforms");
        }

        if (paramsObject.TryGetValue("reuseCollisionCallbacks", out var reuseToken) && reuseToken.Type == JTokenType.Boolean)
        {
            Physics2D.reuseCollisionCallbacks = reuseToken.Value<bool>();
            updated.Add("reuseCollisionCallbacks");
        }

        if (updated.Count > 0)
        {
            AssetDatabase.SaveAssets();
        }

        return UnityMcpProtocol.CreateResult(idToken, new
        {
            updated = updated.ToArray(),
            count = updated.Count
        });
    }

    private static string BuildCaptureSceneViewResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "editor.captureSceneView");

        var width = ParseOptionalIntegerParameter(paramsObject, "width") ?? 1920;
        var height = ParseOptionalIntegerParameter(paramsObject, "height") ?? 1080;

        if (width <= 0 || height <= 0)
        {
            throw new ArgumentException("Width and height must be greater than 0.");
        }

        var sceneView = SceneView.lastActiveSceneView;
        if (sceneView == null)
        {
            throw new ArgumentException("No Scene View is currently open. Open a Scene View window first.");
        }

        var camera = sceneView.camera;
        if (camera == null)
        {
            throw new ArgumentException("Scene View camera is not available.");
        }

        // Create render texture and capture
        var renderTexture = new RenderTexture(width, height, 24);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;

        try
        {
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture.active = renderTexture;
            var texture2D = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture2D.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture2D.Apply();

            var pngData = texture2D.EncodeToPNG();
            var base64 = System.Convert.ToBase64String(pngData);

            UnityEngine.Object.DestroyImmediate(texture2D);

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                base64 = base64,
                width = width,
                height = height,
                format = "png"
            });
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }
    }

    private static string BuildCaptureGameViewResponse(JToken idToken, JObject root)
    {
        var paramsObject = RequireParamsObject(root, "editor.captureGameView");

        if (!Application.isPlaying)
        {
            throw new ArgumentException("Game view capture is not supported in Edit mode. Enter Play mode first.");
        }

        var width = ParseOptionalIntegerParameter(paramsObject, "width");
        var height = ParseOptionalIntegerParameter(paramsObject, "height");

        if (width.HasValue && width.Value <= 0)
        {
            throw new ArgumentException("Width must be greater than 0.");
        }

        if (height.HasValue && height.Value <= 0)
        {
            throw new ArgumentException("Height must be greater than 0.");
        }

        var camera = Camera.main ?? Camera.allCameras.FirstOrDefault(c => c.enabled && c.gameObject.activeInHierarchy);
        if (camera == null)
        {
            throw new ArgumentException("No active camera found in the scene.");
        }

        var targetWidth = width ?? Screen.width;
        var targetHeight = height ?? Screen.height;

        // Create render texture and capture
        var renderTexture = new RenderTexture(targetWidth, targetHeight, 24);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;

        try
        {
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture.active = renderTexture;
            var texture2D = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
            texture2D.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            texture2D.Apply();

            var pngData = texture2D.EncodeToPNG();
            var base64 = System.Convert.ToBase64String(pngData);

            UnityEngine.Object.DestroyImmediate(texture2D);

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                base64 = base64,
                width = targetWidth,
                height = targetHeight,
                format = "png"
            });
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }
    }
}
}

#nullable enable

using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

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
                "textureImporter.getSettings" => BuildGetTextureImporterSettingsResponse(idToken, root),
                "textureImporter.setSettings" => BuildSetTextureImporterSettingsResponse(idToken, root),
                "animationClip.getProperties" => BuildGetAnimationClipPropertiesResponse(idToken, root),
                "animationClip.setProperties" => BuildSetAnimationClipPropertiesResponse(idToken, root),
                "animationClip.getCurveBindings" => BuildGetAnimationClipCurveBindingsResponse(idToken, root),
                "animationClip.getEvents" => BuildGetAnimationClipEventsResponse(idToken, root),
                "animationClip.setEvents" => BuildSetAnimationClipEventsResponse(idToken, root),
                "sceneView.getCamera" => BuildGetSceneViewCameraResponse(idToken),
                "sceneView.setCamera" => BuildSetSceneViewCameraResponse(idToken, root),
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

    // ── Shared helpers used by multiple handler partials ─────────────────

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

    // ── Network I/O ─────────────────────────────────────────────────────

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

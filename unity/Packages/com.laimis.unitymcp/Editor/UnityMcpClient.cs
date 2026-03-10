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
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static UnityMcp.Editor.UnityMcpParameterHelpers;
using static UnityMcp.Editor.UnityMcpResolvers;
using static UnityMcp.Editor.UnityMcpSnapshotHelpers;

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
                "editor.getPlayModeState" => EditorHandler.BuildPlayModeStateResponse(idToken),
                "editor.getConsoleLogs" => EditorHandler.BuildGetConsoleLogsResponse(idToken, root),
                "editor.consoleTail" => EditorHandler.BuildConsoleTailResponse(idToken, root),
                "editor.enterPlayMode" => EditorHandler.BuildSetPlayModeResponse(idToken, shouldPlay: true),
                "editor.exitPlayMode" => EditorHandler.BuildSetPlayModeResponse(idToken, shouldPlay: false),
                "scene.getActiveScene" => SceneHandler.BuildGetActiveSceneResponse(idToken),
                "scene.listOpenScenes" => SceneHandler.BuildListOpenScenesResponse(idToken),
                "scene.getSelection" => SceneHandler.BuildGetSelectionResponse(idToken),
                "scene.selectObject" => SceneHandler.BuildSelectObjectResponse(idToken, root),
                "scene.selectByPath" => SceneHandler.BuildSelectByPathResponse(idToken, root),
                "scene.findByPath" => SceneHandler.BuildFindByPathResponse(idToken, root),
                "camera.getSettings" => CameraHandler.BuildGetCameraSettingsResponse(idToken, root),
                "camera.setSettings" => CameraHandler.BuildSetCameraSettingsResponse(idToken, root),
                "light.getSettings" => LightHandler.BuildGetLightSettingsResponse(idToken, root),
                "light.setSettings" => LightHandler.BuildSetLightSettingsResponse(idToken, root),
                "rigidbody.getSettings" => PhysicsHandler.BuildGetRigidbodySettingsResponse(idToken, root),
                "rigidbody.setSettings" => PhysicsHandler.BuildSetRigidbodySettingsResponse(idToken, root),
                "rigidbody2D.getSettings" => Physics2DHandler.BuildGetRigidbody2DSettingsResponse(idToken, root),
                "rigidbody2D.setSettings" => Physics2DHandler.BuildSetRigidbody2DSettingsResponse(idToken, root),
                "collider.getSettings" => PhysicsHandler.BuildGetColliderSettingsResponse(idToken, root),
                "collider.setSettings" => PhysicsHandler.BuildSetColliderSettingsResponse(idToken, root),
                "collider2D.getSettings" => Physics2DHandler.BuildGetCollider2DSettingsResponse(idToken, root),
                "collider2D.setSettings" => Physics2DHandler.BuildSetCollider2DSettingsResponse(idToken, root),
                "boxCollider.getSettings" => PhysicsHandler.BuildGetBoxColliderSettingsResponse(idToken, root),
                "boxCollider.setSettings" => PhysicsHandler.BuildSetBoxColliderSettingsResponse(idToken, root),
                "boxCollider2D.getSettings" => Physics2DHandler.BuildGetBoxCollider2DSettingsResponse(idToken, root),
                "boxCollider2D.setSettings" => Physics2DHandler.BuildSetBoxCollider2DSettingsResponse(idToken, root),
                "sphereCollider.getSettings" => PhysicsHandler.BuildGetSphereColliderSettingsResponse(idToken, root),
                "sphereCollider.setSettings" => PhysicsHandler.BuildSetSphereColliderSettingsResponse(idToken, root),
                "sphereCollider2D.getSettings" => Physics2DHandler.BuildGetCircleCollider2DSettingsResponse(idToken, root),
                "sphereCollider2D.setSettings" => Physics2DHandler.BuildSetCircleCollider2DSettingsResponse(idToken, root),
                "circleCollider2D.getSettings" => Physics2DHandler.BuildGetCircleCollider2DSettingsResponse(idToken, root),
                "circleCollider2D.setSettings" => Physics2DHandler.BuildSetCircleCollider2DSettingsResponse(idToken, root),
                "capsuleCollider.getSettings" => PhysicsHandler.BuildGetCapsuleColliderSettingsResponse(idToken, root),
                "capsuleCollider.setSettings" => PhysicsHandler.BuildSetCapsuleColliderSettingsResponse(idToken, root),
                "capsuleCollider2D.getSettings" => Physics2DHandler.BuildGetCapsuleCollider2DSettingsResponse(idToken, root),
                "capsuleCollider2D.setSettings" => Physics2DHandler.BuildSetCapsuleCollider2DSettingsResponse(idToken, root),
                "meshCollider.getSettings" => PhysicsHandler.BuildGetMeshColliderSettingsResponse(idToken, root),
                "meshCollider.setSettings" => PhysicsHandler.BuildSetMeshColliderSettingsResponse(idToken, root),
                "polygonCollider2D.getSettings" => Physics2DHandler.BuildGetPolygonCollider2DSettingsResponse(idToken, root),
                "polygonCollider2D.setSettings" => Physics2DHandler.BuildSetPolygonCollider2DSettingsResponse(idToken, root),
                "edgeCollider2D.getSettings" => Physics2DHandler.BuildGetEdgeCollider2DSettingsResponse(idToken, root),
                "edgeCollider2D.setSettings" => Physics2DHandler.BuildSetEdgeCollider2DSettingsResponse(idToken, root),
                "compositeCollider2D.getSettings" => Physics2DHandler.BuildGetCompositeCollider2DSettingsResponse(idToken, root),
                "compositeCollider2D.setSettings" => Physics2DHandler.BuildSetCompositeCollider2DSettingsResponse(idToken, root),
                "hingeJoint2D.getSettings" => Joints2DHandler.BuildGetHingeJoint2DSettingsResponse(idToken, root),
                "hingeJoint2D.setSettings" => Joints2DHandler.BuildSetHingeJoint2DSettingsResponse(idToken, root),
                "springJoint2D.getSettings" => Joints2DHandler.BuildGetSpringJoint2DSettingsResponse(idToken, root),
                "springJoint2D.setSettings" => Joints2DHandler.BuildSetSpringJoint2DSettingsResponse(idToken, root),
                "distanceJoint2D.getSettings" => Joints2DHandler.BuildGetDistanceJoint2DSettingsResponse(idToken, root),
                "distanceJoint2D.setSettings" => Joints2DHandler.BuildSetDistanceJoint2DSettingsResponse(idToken, root),
                "fixedJoint2D.getSettings" => Joints2DHandler.BuildGetFixedJoint2DSettingsResponse(idToken, root),
                "fixedJoint2D.setSettings" => Joints2DHandler.BuildSetFixedJoint2DSettingsResponse(idToken, root),
                "sliderJoint2D.getSettings" => Joints2DHandler.BuildGetSliderJoint2DSettingsResponse(idToken, root),
                "sliderJoint2D.setSettings" => Joints2DHandler.BuildSetSliderJoint2DSettingsResponse(idToken, root),
                "wheelJoint2D.getSettings" => Joints2DHandler.BuildGetWheelJoint2DSettingsResponse(idToken, root),
                "wheelJoint2D.setSettings" => Joints2DHandler.BuildSetWheelJoint2DSettingsResponse(idToken, root),
                "targetJoint2D.getSettings" => Joints2DHandler.BuildGetTargetJoint2DSettingsResponse(idToken, root),
                "targetJoint2D.setSettings" => Joints2DHandler.BuildSetTargetJoint2DSettingsResponse(idToken, root),
                "hingeJoint.getSettings" => JointsHandler.BuildGetHingeJointSettingsResponse(idToken, root),
                "hingeJoint.setSettings" => JointsHandler.BuildSetHingeJointSettingsResponse(idToken, root),
                "springJoint.getSettings" => JointsHandler.BuildGetSpringJointSettingsResponse(idToken, root),
                "springJoint.setSettings" => JointsHandler.BuildSetSpringJointSettingsResponse(idToken, root),
                "fixedJoint.getSettings" => JointsHandler.BuildGetFixedJointSettingsResponse(idToken, root),
                "fixedJoint.setSettings" => JointsHandler.BuildSetFixedJointSettingsResponse(idToken, root),
                "characterJoint.getSettings" => JointsHandler.BuildGetCharacterJointSettingsResponse(idToken, root),
                "characterJoint.setSettings" => JointsHandler.BuildSetCharacterJointSettingsResponse(idToken, root),
                "configurableJoint.getSettings" => JointsHandler.BuildGetConfigurableJointSettingsResponse(idToken, root),
                "configurableJoint.setSettings" => JointsHandler.BuildSetConfigurableJointSettingsResponse(idToken, root),
                "scene.getComponents" => SceneHandler.BuildGetComponentsResponse(idToken, root),
                "scene.destroyObject" => SceneHandler.BuildDestroyObjectResponse(idToken, root),
                "scene.getComponentProperties" => SceneHandler.BuildGetComponentPropertiesResponse(idToken, root),
                "scene.setComponentProperties" => SceneHandler.BuildSetComponentPropertiesResponse(idToken, root),
                "scene.setTransform" => SceneHandler.BuildSetTransformResponse(idToken, root),
                "scene.addComponent" => SceneHandler.BuildAddComponentResponse(idToken, root),
                "scene.setSelection" => SceneHandler.BuildSetSelectionResponse(idToken, root),
                "scene.pingObject" => SceneHandler.BuildPingObjectResponse(idToken, root),
                "scene.frameSelection" => SceneHandler.BuildFrameSelectionResponse(idToken),
                "scene.frameObject" => SceneHandler.BuildFrameObjectResponse(idToken, root),
                "scene.createGameObject" => SceneHandler.BuildCreateGameObjectResponse(idToken, root),
                "scene.setParent" => SceneHandler.BuildSetParentResponse(idToken, root),
                "scene.duplicateObject" => SceneHandler.BuildDuplicateObjectResponse(idToken, root),
                "scene.renameObject" => SceneHandler.BuildRenameObjectResponse(idToken, root),
                "scene.setActive" => SceneHandler.BuildSetActiveResponse(idToken, root),
                "prefab.instantiate" => PrefabHandler.BuildInstantiatePrefabResponse(idToken, root),
                "prefab.getSource" => PrefabHandler.BuildGetPrefabSourceResponse(idToken, root),
                "prefab.applyOverrides" => PrefabHandler.BuildApplyPrefabOverridesResponse(idToken, root),
                "prefab.revertOverrides" => PrefabHandler.BuildRevertPrefabOverridesResponse(idToken, root),
                "scene.findByTag" => SceneHandler.BuildFindByTagResponse(idToken, root),
                "scene.getHierarchy" => SceneHandler.BuildGetSceneHierarchyResponse(idToken, root),
                "assets.find" => AssetsHandler.BuildFindAssetsResponse(idToken, root),
                "assets.import" => AssetsHandler.BuildImportAssetResponse(idToken, root),
                "assets.ping" => AssetsHandler.BuildPingAssetResponse(idToken, root),
                "assets.reveal" => AssetsHandler.BuildRevealAssetResponse(idToken, root),
                // Project Settings
                "projectSettings.getPlayerSettings" => ProjectSettingsHandler.BuildGetPlayerSettingsResponse(idToken),
                "projectSettings.setPlayerSettings" => ProjectSettingsHandler.BuildSetPlayerSettingsResponse(idToken, root),
                "projectSettings.getQualitySettings" => ProjectSettingsHandler.BuildGetQualitySettingsResponse(idToken),
                "projectSettings.setQualitySettings" => ProjectSettingsHandler.BuildSetQualitySettingsResponse(idToken, root),
                "projectSettings.getPhysicsSettings" => ProjectSettingsHandler.BuildGetPhysicsSettingsResponse(idToken),
                "projectSettings.setPhysicsSettings" => ProjectSettingsHandler.BuildSetPhysicsSettingsResponse(idToken, root),
                // Physics2D Settings
                "physics2D.getSettings" => Physics2DHandler.BuildGetPhysics2DSettingsResponse(idToken),
                "physics2D.setSettings" => Physics2DHandler.BuildSetPhysics2DSettingsResponse(idToken, root),
                // Screenshot Capture
                "editor.captureSceneView" => EditorHandler.BuildCaptureSceneViewResponse(idToken, root),
                "editor.captureGameView" => EditorHandler.BuildCaptureGameViewResponse(idToken, root),
                // Editor utility
                "editor.clearConsole" => EditorHandler.BuildClearConsoleResponse(idToken),
                "editor.pausePlayMode" => EditorHandler.BuildPausePlayModeResponse(idToken, root),
                "editor.undo" => EditorHandler.BuildUndoResponse(idToken),
                "editor.redo" => EditorHandler.BuildRedoResponse(idToken),
                "editor.getTags" => EditorHandler.BuildGetTagsResponse(idToken),
                "editor.getLayers" => EditorHandler.BuildGetLayersResponse(idToken),
                // Scene tag / layer
                "scene.setTag" => SceneHandler.BuildSetTagResponse(idToken, root),
                "scene.setLayer" => SceneHandler.BuildSetLayerResponse(idToken, root),
                // Scene management
                "scene.save" => SceneHandler.BuildSaveSceneResponse(idToken, root),
                "scene.openScene" => SceneHandler.BuildOpenSceneResponse(idToken, root),
                "scene.newScene" => SceneHandler.BuildNewSceneResponse(idToken, root),
                "scene.closeScene" => SceneHandler.BuildCloseSceneResponse(idToken, root),
                "scene.setActiveScene" => SceneHandler.BuildSetActiveSceneResponse(idToken, root),
                // Asset creation / management
                "assets.createFolder" => AssetsHandler.BuildCreateFolderResponse(idToken, root),
                "assets.createScript" => AssetsHandler.BuildCreateScriptResponse(idToken, root),
                "assets.createMaterial" => AssetsHandler.BuildCreateMaterialResponse(idToken, root),
                "assets.createPrefab" => AssetsHandler.BuildCreatePrefabResponse(idToken, root),
                "assets.delete" => AssetsHandler.BuildDeleteAssetResponse(idToken, root),
                "assets.move" => AssetsHandler.BuildMoveAssetResponse(idToken, root),
                // Animator
                "animator.getSettings" => AnimatorHandler.BuildGetAnimatorSettingsResponse(idToken, root),
                "animator.setSettings" => AnimatorHandler.BuildSetAnimatorSettingsResponse(idToken, root),
                "animator.getParameters" => AnimatorHandler.BuildGetAnimatorParametersResponse(idToken, root),
                "animator.setParameter" => AnimatorHandler.BuildSetAnimatorParameterResponse(idToken, root),
                // MeshRenderer
                "meshRenderer.getSettings" => RenderersHandler.BuildGetMeshRendererSettingsResponse(idToken, root),
                "meshRenderer.setSettings" => RenderersHandler.BuildSetMeshRendererSettingsResponse(idToken, root),
                // AudioSource
                "audioSource.getSettings" => AudioHandler.BuildGetAudioSourceSettingsResponse(idToken, root),
                "audioSource.setSettings" => AudioHandler.BuildSetAudioSourceSettingsResponse(idToken, root),
                // CharacterController
                "characterController.getSettings" => PhysicsHandler.BuildGetCharacterControllerSettingsResponse(idToken, root),
                "characterController.setSettings" => PhysicsHandler.BuildSetCharacterControllerSettingsResponse(idToken, root),
                // ParticleSystem
                "particleSystem.getSettings" => ParticleSystemHandler.BuildGetParticleSystemSettingsResponse(idToken, root),
                "particleSystem.setSettings" => ParticleSystemHandler.BuildSetParticleSystemSettingsResponse(idToken, root),
                "particleSystem.play" => ParticleSystemHandler.BuildParticleSystemPlayResponse(idToken, root),
                "particleSystem.stop" => ParticleSystemHandler.BuildParticleSystemStopResponse(idToken, root),
                // NavMeshAgent
                "navMeshAgent.getSettings" => NavMeshHandler.BuildGetNavMeshAgentSettingsResponse(idToken, root),
                "navMeshAgent.setSettings" => NavMeshHandler.BuildSetNavMeshAgentSettingsResponse(idToken, root),
                // NavMeshObstacle
                "navMeshObstacle.getSettings" => NavMeshHandler.BuildGetNavMeshObstacleSettingsResponse(idToken, root),
                "navMeshObstacle.setSettings" => NavMeshHandler.BuildSetNavMeshObstacleSettingsResponse(idToken, root),
                // RectTransform
                "rectTransform.getSettings" => UIHandler.BuildGetRectTransformSettingsResponse(idToken, root),
                "rectTransform.setSettings" => UIHandler.BuildSetRectTransformSettingsResponse(idToken, root),
                // Canvas
                "canvas.getSettings" => UIHandler.BuildGetCanvasSettingsResponse(idToken, root),
                "canvas.setSettings" => UIHandler.BuildSetCanvasSettingsResponse(idToken, root),
                // SkinnedMeshRenderer
                "skinnedMeshRenderer.getSettings" => RenderersHandler.BuildGetSkinnedMeshRendererSettingsResponse(idToken, root),
                "skinnedMeshRenderer.setSettings" => RenderersHandler.BuildSetSkinnedMeshRendererSettingsResponse(idToken, root),
                // ScriptableObject
                "assets.createScriptableObject" => AssetsHandler.BuildCreateScriptableObjectResponse(idToken, root),
                // Batch 3: NavMesh
                "navMesh.bake" => NavMeshHandler.BuildNavMeshBakeResponse(idToken),
                // Batch 3: Terrain
                "terrain.getSettings" => TerrainHandler.BuildGetTerrainSettingsResponse(idToken, root),
                "terrain.setSettings" => TerrainHandler.BuildSetTerrainSettingsResponse(idToken, root),
                // Batch 3: Build Pipeline
                "build.getSettings" => BuildHandler.BuildGetBuildSettingsResponse(idToken),
                "build.setSettings" => BuildHandler.BuildSetBuildSettingsResponse(idToken, root),
                "build.build" => BuildHandler.BuildBuildResponse(idToken, root),
                // Batch 3: Tags & Layers Management
                "editor.addTag" => EditorHandler.BuildAddTagResponse(idToken, root),
                "editor.removeTag" => EditorHandler.BuildRemoveTagResponse(idToken, root),
                "editor.addLayer" => EditorHandler.BuildAddLayerResponse(idToken, root),
                "editor.removeLayer" => EditorHandler.BuildRemoveLayerResponse(idToken, root),
                // Batch 3: Selection Utilities
                "scene.getSelectionDetails" => SceneHandler.BuildGetSelectionDetailsResponse(idToken),
                "scene.selectByName" => SceneHandler.BuildSelectByNameResponse(idToken, root),
                // Batch 3: Undo History
                "editor.getUndoHistory" => EditorHandler.BuildGetUndoHistoryResponse(idToken),
                // Batch 4: Camera Projection
                "camera.getProjection" => CameraHandler.BuildGetCameraProjectionResponse(idToken, root),
                "camera.setProjection" => CameraHandler.BuildSetCameraProjectionResponse(idToken, root),
                // Batch 4: SpriteRenderer
                "spriteRenderer.getSettings" => RenderersHandler.BuildGetSpriteRendererSettingsResponse(idToken, root),
                "spriteRenderer.setSettings" => RenderersHandler.BuildSetSpriteRendererSettingsResponse(idToken, root),
                // Batch 4: LineRenderer
                "lineRenderer.getSettings" => RenderersHandler.BuildGetLineRendererSettingsResponse(idToken, root),
                "lineRenderer.setSettings" => RenderersHandler.BuildSetLineRendererSettingsResponse(idToken, root),
                // Batch 4: LODGroup
                "lodGroup.getSettings" => RenderersHandler.BuildGetLODGroupSettingsResponse(idToken, root),
                "lodGroup.setSettings" => RenderersHandler.BuildSetLODGroupSettingsResponse(idToken, root),
                // Batch 4: CanvasGroup
                "canvasGroup.getSettings" => UIHandler.BuildGetCanvasGroupSettingsResponse(idToken, root),
                "canvasGroup.setSettings" => UIHandler.BuildSetCanvasGroupSettingsResponse(idToken, root),
                // Batch 4: Editor Recompile
                "editor.recompileScripts" => EditorHandler.BuildRecompileScriptsResponse(idToken),
                // Batch 4: Scene Instantiate Prefab
                "scene.instantiatePrefab" => SceneHandler.BuildSceneInstantiatePrefabResponse(idToken, root),
                // Batch 5: Physics Queries
                "physics.raycast" => PhysicsHandler.BuildPhysicsRaycastResponse(idToken, root),
                "physics.overlapSphere" => PhysicsHandler.BuildPhysicsOverlapSphereResponse(idToken, root),
                // Batch 5: Time
                "time.getSettings" => TimeHandler.BuildGetTimeSettingsResponse(idToken),
                "time.setSettings" => TimeHandler.BuildSetTimeSettingsResponse(idToken, root),
                // Batch 5: Joint (base 3D)
                "joint.getSettings" => JointsHandler.BuildGetJointSettingsResponse(idToken, root),
                "joint.setSettings" => JointsHandler.BuildSetJointSettingsResponse(idToken, root),
                // Batch 5: Renderer
                "renderer.getMaterials" => RenderersHandler.BuildGetRendererMaterialsResponse(idToken, root),
                "renderer.setMaterial" => RenderersHandler.BuildSetRendererMaterialResponse(idToken, root),

                "audio.getSourceSettings" => AudioHandler.BuildAudioSourceGetSettingsResponse(idToken, root),
                "audio.setSourceSettings" => AudioHandler.BuildAudioSourceSetSettingsResponse(idToken, root),
                "audio.play"             => AudioHandler.BuildAudioPlayResponse(idToken, root),
                "audio.stop"             => AudioHandler.BuildAudioStopResponse(idToken, root),
                "audio.pause"            => AudioHandler.BuildAudioPauseResponse(idToken, root),
                "audio.unpause"          => AudioHandler.BuildAudioUnpauseResponse(idToken, root),
                "audio.getIsPlaying"     => AudioHandler.BuildGetAudioIsPlayingResponse(idToken, root),
                "audio.getMixerSettings" => AudioHandler.BuildGetAudioMixerSettingsResponse(idToken, root),
                "audio.setMixerParameter" => AudioHandler.BuildSetAudioMixerParameterResponse(idToken, root),
                "audio.getListenerSettings" => AudioHandler.BuildGetAudioListenerSettingsResponse(idToken, root),
                "audio.setListenerSettings" => AudioHandler.BuildSetAudioListenerSettingsResponse(idToken, root),
                // Batch 7: Test Runner
                "testRunner.listTests" => TestRunnerHandler.BuildListTestsResponse(idToken, root),
                "testRunner.run" => TestRunnerHandler.BuildRunTestsResponse(idToken, root),
                "testRunner.getResults" => TestRunnerHandler.BuildGetTestResultsResponse(idToken),
                "testRunner.cancel" => TestRunnerHandler.BuildCancelTestRunResponse(idToken),
                // Batch 8: Material/Shader Properties
                "material.getProperties" => MaterialHandler.BuildGetMaterialPropertiesResponse(idToken, root),
                "material.getProperty" => MaterialHandler.BuildGetMaterialPropertyResponse(idToken, root),
                "material.setProperty" => MaterialHandler.BuildSetMaterialPropertyResponse(idToken, root),
                "material.getKeywords" => MaterialHandler.BuildGetMaterialKeywordsResponse(idToken, root),
                "material.setKeyword" => MaterialHandler.BuildSetMaterialKeywordResponse(idToken, root),
                "material.getShader" => MaterialHandler.BuildGetMaterialShaderResponse(idToken, root),
                "material.setShader" => MaterialHandler.BuildSetMaterialShaderResponse(idToken, root),
                "material.getRenderQueue" => MaterialHandler.BuildGetMaterialRenderQueueResponse(idToken, root),
                "material.setRenderQueue" => MaterialHandler.BuildSetMaterialRenderQueueResponse(idToken, root),
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


}
}

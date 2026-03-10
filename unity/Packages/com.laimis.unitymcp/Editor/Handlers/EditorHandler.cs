#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static UnityMcp.Editor.UnityMcpParameterHelpers;

namespace UnityMcp.Editor
{
    internal static class EditorHandler
    {
        // ── Editor Play Mode State ──────────────────────────────────────────

        internal static string BuildPlayModeStateResponse(JToken idToken)
        {
            return UnityMcpProtocol.CreateResult(idToken, BuildEditorStateResult());
        }

        // ── Editor Console Operations ───────────────────────────────────────

        internal static string BuildGetConsoleLogsResponse(JToken idToken, JObject root)
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

        internal static string BuildConsoleTailResponse(JToken idToken, JObject root)
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

        internal static string BuildClearConsoleResponse(JToken idToken)
        {
            var logEntries = System.Type.GetType("UnityEditor.LogEntries, UnityEditor");
            logEntries?.GetMethod("Clear")?.Invoke(null, null);
            return UnityMcpProtocol.CreateResult(idToken, new { cleared = true });
        }

        // ── Editor Play Mode Control ────────────────────────────────────────

        internal static string BuildSetPlayModeResponse(JToken idToken, bool shouldPlay)
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

        internal static string BuildPausePlayModeResponse(JToken idToken, JObject root)
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

        // ── Editor Undo/Redo ────────────────────────────────────────────────

        internal static string BuildUndoResponse(JToken idToken)
        {
            Undo.PerformUndo();
            return UnityMcpProtocol.CreateResult(idToken, new { applied = true });
        }

        internal static string BuildRedoResponse(JToken idToken)
        {
            Undo.PerformRedo();
            return UnityMcpProtocol.CreateResult(idToken, new { applied = true });
        }

        internal static string BuildGetUndoHistoryResponse(JToken idToken)
        {
            return UnityMcpProtocol.CreateResult(idToken, new
            {
                currentGroupName = Undo.GetCurrentGroupName(),
                currentGroup = Undo.GetCurrentGroup()
            });
        }

        // ── Editor Tags & Layers ────────────────────────────────────────────

        internal static string BuildGetTagsResponse(JToken idToken)
        {
            var tags = UnityEditorInternal.InternalEditorUtility.tags;
            return UnityMcpProtocol.CreateResult(idToken, new { count = tags.Length, tags });
        }

        internal static string BuildGetLayersResponse(JToken idToken)
        {
            var layers = UnityEditorInternal.InternalEditorUtility.layers;
            return UnityMcpProtocol.CreateResult(idToken, new { count = layers.Length, layers });
        }

        internal static string BuildAddTagResponse(JToken idToken, JObject root)
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

        internal static string BuildRemoveTagResponse(JToken idToken, JObject root)
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

        internal static string BuildAddLayerResponse(JToken idToken, JObject root)
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

        internal static string BuildRemoveLayerResponse(JToken idToken, JObject root)
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

        // ── Editor Screenshot Capture ───────────────────────────────────────

        internal static string BuildCaptureSceneViewResponse(JToken idToken, JObject root)
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

        internal static string BuildCaptureGameViewResponse(JToken idToken, JObject root)
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

        // ── Editor Script Compilation ───────────────────────────────────────

        internal static string BuildRecompileScriptsResponse(JToken idToken)
        {
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            return UnityMcpProtocol.CreateResult(idToken, new { requested = true });
        }

        // ── Private Helper Methods ──────────────────────────────────────────

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

        private static object CreateConsoleQueryResultPayload(object queryResult, List<string>? levels, string? contains)
        {
            // Cast to the actual type
            if (queryResult is not ConsoleLogQueryResult result)
                throw new ArgumentException("Invalid query result type");

            return new
            {
                bufferCapacity = result.BufferCapacity,
                totalBuffered = result.TotalBuffered,
                bufferStartSequence = result.BufferStartSequence,
                latestSequence = result.LatestSequence,
                afterSequence = result.AfterSequence,
                nextAfterSequence = result.NextAfterSequence,
                cursorBehindBuffer = result.CursorBehindBuffer,
                truncated = result.Truncated,
                includeStackTrace = result.IncludeStackTrace,
                count = result.Items.Count,
                items = result.Items,
                filters = new
                {
                    levels = levels,
                    contains = contains
                }
            };
        }

        private sealed class EditorStateSnapshot
        {
            public bool isPlaying { get; set; }
            public bool isPaused { get; set; }
            public bool isCompiling { get; set; }
            public bool isPlayingOrWillChangePlaymode { get; set; }
        }
    }
}
#nullable enable

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMcp.Editor
{
    internal sealed partial class UnityMcpClient
    {
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

        private static string BuildGetUndoHistoryResponse(JToken idToken)
        {
            return UnityMcpProtocol.CreateResult(idToken, new
            {
                currentGroupName = Undo.GetCurrentGroupName(),
                currentGroup = Undo.GetCurrentGroup()
            });
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

        private static string BuildRecompileScriptsResponse(JToken idToken)
        {
            UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
            return UnityMcpProtocol.CreateResult(idToken, new { requested = true });
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

        // ── SceneView Camera ─────────────────────────────────────────────────

        private static string BuildGetSceneViewCameraResponse(JToken idToken)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                throw new ArgumentException("No active Scene View window is available.");

            return UnityMcpProtocol.CreateResult(idToken, BuildSceneViewCameraSnapshot(sceneView));
        }

        private static string BuildSetSceneViewCameraResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "sceneView.setCamera");

            var pivot = ParseOptionalVector3Parameter(paramsObject, "pivot");
            var size = ParseOptionalFloatParameter(paramsObject, "size");
            var orthographic = ParseOptionalBooleanValueParameter(paramsObject, "orthographic");
            var in2DMode = ParseOptionalBooleanValueParameter(paramsObject, "in2DMode");

            Quaternion? rotation = null;
            if (paramsObject.TryGetValue("rotation", out var rotationToken))
            {
                var values = ParseFloatArrayToken(rotationToken, "rotation", 4);
                rotation = new Quaternion(values[0], values[1], values[2], values[3]);
            }

            if (!pivot.HasValue && rotation == null && !size.HasValue && !orthographic.HasValue && !in2DMode.HasValue)
                throw new ArgumentException("At least one Scene View camera setting must be provided: pivot, rotation, size, orthographic, or in2DMode.");

            if (size.HasValue && size.Value <= 0f)
                throw new ArgumentException("Parameter 'size' must be greater than 0.");

            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                throw new ArgumentException("No active Scene View window is available.");

            if (pivot.HasValue) sceneView.pivot = pivot.Value;
            if (rotation.HasValue) sceneView.rotation = rotation.Value;
            if (size.HasValue) sceneView.size = size.Value;
            if (orthographic.HasValue) sceneView.orthographic = orthographic.Value;
            if (in2DMode.HasValue) sceneView.in2DMode = in2DMode.Value;

            sceneView.Repaint();

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                pivotApplied = pivot.HasValue,
                rotationApplied = rotation.HasValue,
                sizeApplied = size.HasValue,
                orthographicApplied = orthographic.HasValue,
                in2DModeApplied = in2DMode.HasValue,
                camera = BuildSceneViewCameraSnapshot(sceneView)
            });
        }

        private static object BuildSceneViewCameraSnapshot(SceneView sceneView)
        {
            return new
            {
                pivot = new { x = sceneView.pivot.x, y = sceneView.pivot.y, z = sceneView.pivot.z },
                rotation = new { x = sceneView.rotation.x, y = sceneView.rotation.y, z = sceneView.rotation.z, w = sceneView.rotation.w },
                cameraPosition = new { x = sceneView.camera.transform.position.x, y = sceneView.camera.transform.position.y, z = sceneView.camera.transform.position.z },
                cameraRotation = new { x = sceneView.camera.transform.rotation.x, y = sceneView.camera.transform.rotation.y, z = sceneView.camera.transform.rotation.z, w = sceneView.camera.transform.rotation.w },
                size = sceneView.size,
                orthographic = sceneView.orthographic,
                in2DMode = sceneView.in2DMode,
                cameraDistance = sceneView.cameraDistance
            };
        }
    }
}

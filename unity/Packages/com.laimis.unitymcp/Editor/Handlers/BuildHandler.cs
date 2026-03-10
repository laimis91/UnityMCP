#nullable enable

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using static UnityMcp.Editor.UnityMcpParameterHelpers;

namespace UnityMcp.Editor
{
    internal static class BuildHandler
    {
        // ── Batch 3: Build Pipeline ─────────────────────────────────────────

        internal static string BuildGetBuildSettingsResponse(JToken idToken)
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

        internal static string BuildSetBuildSettingsResponse(JToken idToken, JObject root)
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

        internal static string BuildBuildResponse(JToken idToken, JObject root)
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
    }
}
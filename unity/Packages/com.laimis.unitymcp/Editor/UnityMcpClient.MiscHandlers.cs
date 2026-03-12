#nullable enable

using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityMcp.Editor
{
    internal sealed partial class UnityMcpClient
    {
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
    }
}

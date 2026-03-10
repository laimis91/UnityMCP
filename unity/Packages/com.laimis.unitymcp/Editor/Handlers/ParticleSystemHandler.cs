#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using static UnityMcp.Editor.UnityMcpParameterHelpers;
using static UnityMcp.Editor.UnityMcpResolvers;
using static UnityMcp.Editor.UnityMcpSnapshotHelpers;

namespace UnityMcp.Editor
{
    internal static class ParticleSystemHandler
    {
        internal static string BuildGetParticleSystemSettingsResponse(JToken idToken, JObject root)
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

        internal static string BuildSetParticleSystemSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "particleSystem.setSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var (ps, ownerGo) = ResolveComponentFromInstanceId<ParticleSystem>(instanceId, "particleSystem.setSettings");

            Undo.RecordObject(ps, "Set ParticleSystem Settings");
            var applied = new List<string>();

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

        internal static string BuildParticleSystemPlayResponse(JToken idToken, JObject root)
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

        internal static string BuildParticleSystemStopResponse(JToken idToken, JObject root)
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
    }
}
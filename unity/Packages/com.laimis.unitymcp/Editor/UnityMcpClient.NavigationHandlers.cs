#nullable enable

using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace UnityMcp.Editor
{
    internal sealed partial class UnityMcpClient
    {
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

        private static string BuildNavMeshBakeResponse(JToken idToken)
        {
#pragma warning disable CS0618 // UnityEditor.AI.NavMeshBuilder deprecated; replacement not available in Editor context
            UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
#pragma warning restore CS0618
            return UnityMcpProtocol.CreateResult(idToken, new { baked = true });
        }
    }
}

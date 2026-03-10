#nullable enable

using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static UnityMcp.Editor.UnityMcpParameterHelpers;

namespace UnityMcp.Editor
{
    internal static class TimeHandler
    {
        // ── Time Settings ───────────────────────────────────────────────────

        internal static string BuildGetTimeSettingsResponse(JToken idToken)
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

        internal static string BuildSetTimeSettingsResponse(JToken idToken, JObject root)
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
    }
}
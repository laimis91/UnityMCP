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
            var applied = new List<string>();

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

            var parameters = new List<object>();
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
    }
}

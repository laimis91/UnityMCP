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

        // ── AnimationClip ─────────────────────────────────────────────────────

        private static AnimationClip LoadAnimationClipFromAssetPath(string assetPath)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            if (clip == null)
                throw new ArgumentException($"No AnimationClip found at '{assetPath}'.");
            return clip;
        }

        private static string BuildGetAnimationClipPropertiesResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "animationClip.getProperties");
            var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
            var clip = LoadAnimationClipFromAssetPath(assetPath);
            var bindings = AnimationUtility.GetCurveBindings(clip);
            var ptrBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            var events = AnimationUtility.GetAnimationEvents(clip);

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                name = clip.name,
                assetPath,
                length = clip.length,
                frameRate = clip.frameRate,
                wrapMode = clip.wrapMode.ToString(),
                legacy = clip.legacy,
                isLooping = clip.isLooping,
                isEmpty = clip.empty,
                isHumanMotion = clip.humanMotion,
                hasMotionCurves = clip.hasMotionCurves,
                hasRootCurves = clip.hasRootCurves,
                eventCount = events.Length,
                curveBindingCount = bindings.Length + ptrBindings.Length
            });
        }

        private static string BuildSetAnimationClipPropertiesResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "animationClip.setProperties");
            var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
            var clip = LoadAnimationClipFromAssetPath(assetPath);

            Undo.RecordObject(clip, "Set AnimationClip Properties");
            var applied = new System.Collections.Generic.List<string>();

            if (TryGetFloat(paramsObject, "frameRate", out var frameRate))
            {
                if (frameRate <= 0f)
                    throw new ArgumentException("Parameter 'frameRate' must be greater than 0.");
                clip.frameRate = frameRate;
                applied.Add("frameRate");
            }
            if (paramsObject.TryGetValue("wrapMode", out var wm))
            { clip.wrapMode = ParseEnumToken<WrapMode>(wm, "wrapMode"); applied.Add("wrapMode"); }
            if (paramsObject.TryGetValue("legacy", out var leg) && leg.Type == JTokenType.Boolean)
            { clip.legacy = leg.Value<bool>(); applied.Add("legacy"); }

            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                assetPath,
                applied
            });
        }

        private static string BuildGetAnimationClipCurveBindingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "animationClip.getCurveBindings");
            var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
            var clip = LoadAnimationClipFromAssetPath(assetPath);

            var maxResults = 500;
            if (paramsObject.TryGetValue("maxResults", out var mrToken) && mrToken.Type == JTokenType.Integer)
                maxResults = System.Math.Clamp(mrToken.Value<int>(), 1, 500);

            var floatBindings = AnimationUtility.GetCurveBindings(clip);
            var ptrBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            var results = new System.Collections.Generic.List<object>();

            foreach (var b in floatBindings)
            {
                if (results.Count >= maxResults) break;
                results.Add(new
                {
                    path = b.path,
                    propertyName = b.propertyName,
                    type = b.type?.Name,
                    isDiscreteCurve = b.isDiscreteCurve,
                    isPPtrCurve = b.isPPtrCurve
                });
            }
            foreach (var b in ptrBindings)
            {
                if (results.Count >= maxResults) break;
                results.Add(new
                {
                    path = b.path,
                    propertyName = b.propertyName,
                    type = b.type?.Name,
                    isDiscreteCurve = b.isDiscreteCurve,
                    isPPtrCurve = b.isPPtrCurve
                });
            }

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                assetPath,
                curveBindingCount = results.Count,
                curveBindings = results
            });
        }

        private static string BuildGetAnimationClipEventsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "animationClip.getEvents");
            var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
            var clip = LoadAnimationClipFromAssetPath(assetPath);

            var events = AnimationUtility.GetAnimationEvents(clip);
            var eventList = new object[events.Length];
            for (var i = 0; i < events.Length; i++)
            {
                var e = events[i];
                eventList[i] = new
                {
                    time = e.time,
                    functionName = e.functionName,
                    stringParameter = e.stringParameter,
                    floatParameter = e.floatParameter,
                    intParameter = e.intParameter
                };
            }

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                assetPath,
                eventCount = events.Length,
                events = eventList
            });
        }

        private static string BuildSetAnimationClipEventsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "animationClip.setEvents");
            var assetPath = ParseRequiredStringParameter(paramsObject, "assetPath");
            var clip = LoadAnimationClipFromAssetPath(assetPath);

            if (!paramsObject.TryGetValue("events", out var eventsToken) || eventsToken.Type != JTokenType.Array)
                throw new ArgumentException("Parameter 'events' is required and must be an array.");

            var eventsArray = (JArray)eventsToken;
            var newEvents = new AnimationEvent[eventsArray.Count];
            for (var i = 0; i < eventsArray.Count; i++)
            {
                if (eventsArray[i] is not JObject evObj)
                    throw new ArgumentException($"Event at index {i} must be an object.");

                var evt = new AnimationEvent();
                if (TryGetFloat(evObj, "time", out var evTime)) evt.time = evTime;
                if (evObj.TryGetValue("functionName", out var fn) && fn.Type == JTokenType.String)
                    evt.functionName = fn.Value<string>()!;
                if (evObj.TryGetValue("stringParameter", out var sp) && sp.Type == JTokenType.String)
                    evt.stringParameter = sp.Value<string>()!;
                if (TryGetFloat(evObj, "floatParameter", out var fp)) evt.floatParameter = fp;
                if (evObj.TryGetValue("intParameter", out var ip) && ip.Type == JTokenType.Integer)
                    evt.intParameter = ip.Value<int>();
                newEvents[i] = evt;
            }

            Undo.RecordObject(clip, "Set AnimationClip Events");
            AnimationUtility.SetAnimationEvents(clip, newEvents);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                assetPath,
                eventCount = newEvents.Length,
                updated = true
            });
        }
    }
}

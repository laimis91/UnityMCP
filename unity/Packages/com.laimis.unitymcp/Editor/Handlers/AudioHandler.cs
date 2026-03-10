#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using static UnityMcp.Editor.UnityMcpParameterHelpers;
using static UnityMcp.Editor.UnityMcpResolvers;

namespace UnityMcp.Editor
{
    internal static class AudioHandler
    {
        // ── AudioSource (legacy methods) ──────────────────────────────────────

        internal static string BuildGetAudioSourceSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "audioSource.getSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var (audio, ownerGo) = ResolveComponentFromInstanceId<AudioSource>(instanceId, "audioSource.getSettings");

            var settings = new
            {
                enabled = audio.enabled,
                clipName = audio.clip != null ? audio.clip.name : null,
                volume = audio.volume,
                pitch = audio.pitch,
                loop = audio.loop,
                playOnAwake = audio.playOnAwake,
                mute = audio.mute,
                spatialBlend = audio.spatialBlend,
                spatialize = audio.spatialize,
                priority = audio.priority,
                dopplerLevel = audio.dopplerLevel,
                minDistance = audio.minDistance,
                maxDistance = audio.maxDistance,
                rolloffMode = audio.rolloffMode.ToString(),
                isPlaying = audio.isPlaying
            };

            return UnityMcpProtocol.CreateResult(idToken, new
            {
                target = CreateObjectSummary(ownerGo),
                component = CreateObjectSummary(audio),
                settings
            });
        }

        internal static string BuildSetAudioSourceSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "audioSource.setSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var (audio, ownerGo) = ResolveComponentFromInstanceId<AudioSource>(instanceId, "audioSource.setSettings");

            Undo.RecordObject(audio, "Set AudioSource Settings");
            var applied = new List<string>();

            if (paramsObject.TryGetValue("enabled", out var en) && en.Type == JTokenType.Boolean)
            { audio.enabled = en.Value<bool>(); applied.Add("enabled"); }
            if (TryGetFloat(paramsObject, "volume", out var vol))
            { audio.volume = vol; applied.Add("volume"); }
            if (TryGetFloat(paramsObject, "pitch", out var pitch))
            { audio.pitch = pitch; applied.Add("pitch"); }
            if (paramsObject.TryGetValue("loop", out var loop) && loop.Type == JTokenType.Boolean)
            { audio.loop = loop.Value<bool>(); applied.Add("loop"); }
            if (paramsObject.TryGetValue("playOnAwake", out var poa) && poa.Type == JTokenType.Boolean)
            { audio.playOnAwake = poa.Value<bool>(); applied.Add("playOnAwake"); }
            if (paramsObject.TryGetValue("mute", out var mute) && mute.Type == JTokenType.Boolean)
            { audio.mute = mute.Value<bool>(); applied.Add("mute"); }
            if (TryGetFloat(paramsObject, "spatialBlend", out var sb))
            { audio.spatialBlend = sb; applied.Add("spatialBlend"); }
            if (paramsObject.TryGetValue("spatialize", out var spat) && spat.Type == JTokenType.Boolean)
            { audio.spatialize = spat.Value<bool>(); applied.Add("spatialize"); }
            if (paramsObject.TryGetValue("priority", out var pri) && pri.Type == JTokenType.Integer)
            { audio.priority = pri.Value<int>(); applied.Add("priority"); }
            if (TryGetFloat(paramsObject, "dopplerLevel", out var dl))
            { audio.dopplerLevel = dl; applied.Add("dopplerLevel"); }
            if (TryGetFloat(paramsObject, "minDistance", out var minD))
            { audio.minDistance = minD; applied.Add("minDistance"); }
            if (TryGetFloat(paramsObject, "maxDistance", out var maxD))
            { audio.maxDistance = maxD; applied.Add("maxDistance"); }
            if (paramsObject.TryGetValue("rolloffMode", out var rom))
            { audio.rolloffMode = ParseEnumToken<AudioRolloffMode>(rom, "rolloffMode"); applied.Add("rolloffMode"); }

            EditorUtility.SetDirty(audio);
            return UnityMcpProtocol.CreateResult(idToken, new
            {
                target = CreateObjectSummary(ownerGo),
                component = CreateObjectSummary(audio),
                applied
            });
        }

        // ── Audio (Batch 6 methods) ───────────────────────────────────────────

        internal static string BuildAudioSourceGetSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "audio.getSourceSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var source = ResolveComponentOfTypeTarget<AudioSource>(resolvedObject, "instanceId", "AudioSource");

            var clipPath = source.clip != null ? AssetDatabase.GetAssetPath(source.clip) : "";
            var clipName = source.clip != null ? source.clip.name : "";
            string? mixerGroupPath = null;
            if (source.outputAudioMixerGroup != null)
            {
                var mixer = source.outputAudioMixerGroup.audioMixer;
                var groupName = source.outputAudioMixerGroup.name;
                mixerGroupPath = mixer != null ? $"{mixer.name}/{groupName}" : groupName;
            }

            var result = new
            {
                target = CreateObjectSummary(source.gameObject),
                component = CreateComponentSummary(source),
                settings = new
                {
                    clipAssetPath = clipPath,
                    clipName,
                    volume = source.volume,
                    pitch = source.pitch,
                    loop = source.loop,
                    mute = source.mute,
                    playOnAwake = source.playOnAwake,
                    spatialBlend = source.spatialBlend,
                    minDistance = source.minDistance,
                    maxDistance = source.maxDistance,
                    rolloffMode = source.rolloffMode.ToString(),
                    mixerGroupPath,
                    isPlaying = source.isPlaying
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildAudioSourceSetSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "audio.setSourceSettings");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var source = ResolveComponentOfTypeTarget<AudioSource>(resolvedObject, "instanceId", "AudioSource");

            var volume = ParseOptionalFloatParameter(paramsObject, "volume");
            var pitch = ParseOptionalFloatParameter(paramsObject, "pitch");
            var loop = ParseOptionalBooleanValueParameter(paramsObject, "loop");
            var mute = ParseOptionalBooleanValueParameter(paramsObject, "mute");
            var playOnAwake = ParseOptionalBooleanValueParameter(paramsObject, "playOnAwake");
            var spatialBlend = ParseOptionalFloatParameter(paramsObject, "spatialBlend");
            var minDistance = ParseOptionalFloatParameter(paramsObject, "minDistance");
            var maxDistance = ParseOptionalFloatParameter(paramsObject, "maxDistance");
            var rolloffMode = ParseOptionalStringParameter(paramsObject, "rolloffMode");
            var clipAssetPath = ParseOptionalStringParameter(paramsObject, "clipAssetPath");
            var mixerGroupPath = ParseOptionalStringParameter(paramsObject, "mixerGroupPath");

            if (!volume.HasValue && !pitch.HasValue && !loop.HasValue && !mute.HasValue &&
                !playOnAwake.HasValue && !spatialBlend.HasValue && !minDistance.HasValue &&
                !maxDistance.HasValue && rolloffMode == null && clipAssetPath == null && mixerGroupPath == null)
            {
                throw new ArgumentException(
                    "At least one AudioSource property must be specified: volume, pitch, loop, mute, playOnAwake, spatialBlend, minDistance, maxDistance, rolloffMode, clipAssetPath, or mixerGroupPath.");
            }

            if (minDistance.HasValue && minDistance.Value <= 0f)
                throw new ArgumentException("Parameter 'minDistance' must be greater than 0.");

            var effectiveMin = minDistance ?? source.minDistance;
            var effectiveMax = maxDistance ?? source.maxDistance;
            if (maxDistance.HasValue && effectiveMax <= effectiveMin)
                throw new ArgumentException("Parameter 'maxDistance' must be greater than 'minDistance'.");

            Undo.RecordObject(source, "UnityMCP Set AudioSource Settings");

            if (volume.HasValue) source.volume = Mathf.Clamp01(volume.Value);
            if (pitch.HasValue) source.pitch = Mathf.Clamp(pitch.Value, -3f, 3f);
            if (loop.HasValue) source.loop = loop.Value;
            if (mute.HasValue) source.mute = mute.Value;
            if (playOnAwake.HasValue) source.playOnAwake = playOnAwake.Value;
            if (spatialBlend.HasValue) source.spatialBlend = Mathf.Clamp01(spatialBlend.Value);
            if (minDistance.HasValue) source.minDistance = minDistance.Value;
            if (maxDistance.HasValue) source.maxDistance = maxDistance.Value;

            if (rolloffMode != null)
            {
                if (Enum.TryParse<AudioRolloffMode>(rolloffMode, ignoreCase: true, out var rm))
                    source.rolloffMode = rm;
                else if (int.TryParse(rolloffMode, out var rmi))
                    source.rolloffMode = (AudioRolloffMode)rmi;
                else
                    throw new ArgumentException($"Invalid rolloffMode '{rolloffMode}'. Expected Logarithmic, Linear, or Custom (or 0/1/2).");
            }

            bool clipApplied = false;
            if (clipAssetPath != null)
            {
                if (string.IsNullOrEmpty(clipAssetPath))
                {
                    source.clip = null;
                    clipApplied = true;
                }
                else
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipAssetPath);
                    if (clip == null)
                        throw new ArgumentException($"No AudioClip found at path '{clipAssetPath}'. Check the asset path and ensure it is imported.");
                    source.clip = clip;
                    clipApplied = true;
                }
            }

            bool mixerApplied = false;
            if (mixerGroupPath != null)
            {
                if (string.IsNullOrEmpty(mixerGroupPath))
                {
                    source.outputAudioMixerGroup = null;
                    mixerApplied = true;
                }
                else
                {
                    var parts = mixerGroupPath.Split('/', 2);
                    if (parts.Length != 2)
                        throw new ArgumentException($"No AudioMixerGroup found at path '{mixerGroupPath}'. Format must be 'MixerName/GroupName'.");
                    var mixerGuids = AssetDatabase.FindAssets($"t:AudioMixer {parts[0]}");
                    AudioMixerGroup? foundGroup = null;
                    foreach (var guid in mixerGuids)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
                        if (mixer != null)
                        {
                            var groups = mixer.FindMatchingGroups(parts[1]);
                            if (groups != null && groups.Length > 0)
                            {
                                foundGroup = groups[0];
                                break;
                            }
                        }
                    }
                    if (foundGroup == null)
                        throw new ArgumentException($"No AudioMixerGroup found at path '{mixerGroupPath}'. Format must be 'MixerName/GroupName'.");
                    source.outputAudioMixerGroup = foundGroup;
                    mixerApplied = true;
                }
            }

            EditorUtility.SetDirty(source);

            string resultClipPath = source.clip != null ? AssetDatabase.GetAssetPath(source.clip) : "";
            string? resultMixerGroupPath = null;
            if (source.outputAudioMixerGroup != null)
            {
                var mixer = source.outputAudioMixerGroup.audioMixer;
                var groupName = source.outputAudioMixerGroup.name;
                resultMixerGroupPath = mixer != null ? $"{mixer.name}/{groupName}" : groupName;
            }

            var result = new
            {
                target = CreateObjectSummary(source.gameObject),
                component = CreateComponentSummary(source),
                settings = new
                {
                    clipAssetPath = resultClipPath,
                    clipName = source.clip != null ? source.clip.name : "",
                    volume = source.volume,
                    pitch = source.pitch,
                    loop = source.loop,
                    mute = source.mute,
                    playOnAwake = source.playOnAwake,
                    spatialBlend = source.spatialBlend,
                    minDistance = source.minDistance,
                    maxDistance = source.maxDistance,
                    rolloffMode = source.rolloffMode.ToString(),
                    mixerGroupPath = resultMixerGroupPath,
                    isPlaying = source.isPlaying
                },
                applied = new
                {
                    volume = volume.HasValue,
                    pitch = pitch.HasValue,
                    loop = loop.HasValue,
                    mute = mute.HasValue,
                    playOnAwake = playOnAwake.HasValue,
                    spatialBlend = spatialBlend.HasValue,
                    minDistance = minDistance.HasValue,
                    maxDistance = maxDistance.HasValue,
                    rolloffMode = rolloffMode != null,
                    clipAssetPath = clipApplied,
                    mixerGroupPath = mixerApplied
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildAudioPlayResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "audio.play");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var delay = ParseOptionalFloatParameter(paramsObject, "delay");

            if (!Application.isPlaying)
                throw new ArgumentException("audio.play requires the Editor to be in Play mode. AudioSource.Play() is a no-op in Edit mode.");

            if (delay.HasValue && delay.Value < 0f)
                throw new ArgumentException("Parameter 'delay' must be >= 0.");

            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var source = ResolveComponentOfTypeTarget<AudioSource>(resolvedObject, "instanceId", "AudioSource");

            if (source.clip == null)
                throw new ArgumentException("AudioSource has no clip assigned. Assign a clip via audio.setSourceSettings before calling audio.play.");

            if (delay.HasValue && delay.Value > 0f)
                source.PlayDelayed(delay.Value);
            else
                source.Play();

            var result = new
            {
                target = CreateObjectSummary(source.gameObject),
                component = CreateComponentSummary(source),
                isPlaying = source.isPlaying,
                delay = delay ?? 0f
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildAudioStopResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "audio.stop");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");

            if (!Application.isPlaying)
                throw new ArgumentException("audio.stop requires the Editor to be in Play mode. AudioSource.Stop() is a no-op in Edit mode.");

            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var source = ResolveComponentOfTypeTarget<AudioSource>(resolvedObject, "instanceId", "AudioSource");

            source.Stop();

            var result = new
            {
                target = CreateObjectSummary(source.gameObject),
                component = CreateComponentSummary(source),
                isPlaying = source.isPlaying
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildAudioPauseResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "audio.pause");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");

            if (!Application.isPlaying)
                throw new ArgumentException("audio.pause requires the Editor to be in Play mode. AudioSource.Pause() is a no-op in Edit mode.");

            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var source = ResolveComponentOfTypeTarget<AudioSource>(resolvedObject, "instanceId", "AudioSource");

            source.Pause();

            var result = new
            {
                target = CreateObjectSummary(source.gameObject),
                component = CreateComponentSummary(source),
                isPlaying = source.isPlaying
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildAudioUnpauseResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "audio.unpause");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");

            if (!Application.isPlaying)
                throw new ArgumentException("audio.unpause requires the Editor to be in Play mode. AudioSource.UnPause() is a no-op in Edit mode.");

            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var source = ResolveComponentOfTypeTarget<AudioSource>(resolvedObject, "instanceId", "AudioSource");

            source.UnPause();

            var result = new
            {
                target = CreateObjectSummary(source.gameObject),
                component = CreateComponentSummary(source),
                isPlaying = source.isPlaying
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildGetAudioIsPlayingResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "audio.getIsPlaying");
            var instanceId = ParseRequiredIntegerParameter(paramsObject, "instanceId");
            var resolvedObject = ResolveObjectByInstanceId(instanceId, "instanceId");
            var source = ResolveComponentOfTypeTarget<AudioSource>(resolvedObject, "instanceId", "AudioSource");

            var result = new
            {
                target = CreateObjectSummary(source.gameObject),
                component = CreateComponentSummary(source),
                isPlaying = source.isPlaying,
                isPlayMode = Application.isPlaying
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildGetAudioMixerSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "audio.getMixerSettings");
            var mixerAssetPath = ParseRequiredStringParameter(paramsObject, "mixerAssetPath");

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(mixerAssetPath);
            if (mixer == null)
                throw new ArgumentException($"No AudioMixer found at path '{mixerAssetPath}'. Verify the asset path ends with '.mixer'.");

            var snapshots = AssetDatabase.LoadAllAssetsAtPath(mixerAssetPath)
                .OfType<AudioMixerSnapshot>()
                .Select(s => new { name = s.name })
                .ToArray();

            var exposedParams = new List<object>();
            var so = new SerializedObject(mixer);
            var exposedParamsProp = so.FindProperty("m_ExposedParameters");
            if (exposedParamsProp != null)
            {
                for (int i = 0; i < exposedParamsProp.arraySize; i++)
                {
                    var elem = exposedParamsProp.GetArrayElementAtIndex(i);
                    var nameProp = elem.FindPropertyRelative("name");
                    if (nameProp == null) continue;
                    var paramName = nameProp.stringValue;
                    float paramValue = 0f;
                    mixer.GetFloat(paramName, out paramValue);
                    exposedParams.Add(new { name = paramName, value = paramValue });
                }
            }

            var result = new
            {
                mixerAssetPath,
                name = mixer.name,
                snapshots,
                exposedParameters = exposedParams.ToArray()
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildSetAudioMixerParameterResponse(JToken idToken, JObject root)
        {
            var paramsObject = RequireParamsObject(root, "audio.setMixerParameter");
            var mixerAssetPath = ParseRequiredStringParameter(paramsObject, "mixerAssetPath");
            var parameterName = ParseRequiredStringParameter(paramsObject, "parameterName");
            var value = ParseRequiredFloatParameter(paramsObject, "value");

            if (string.IsNullOrEmpty(parameterName))
                throw new ArgumentException("Parameter 'parameterName' is required and must be non-empty.");

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(mixerAssetPath);
            if (mixer == null)
                throw new ArgumentException($"No AudioMixer found at path '{mixerAssetPath}'.");

            // Step 1: Find the GUID for this parameter from m_ExposedParameters.
            // m_ExposedParameters only stores { m_GUID, name } — no value field.
            // The actual float values live in each snapshot's m_FloatValues, keyed by GUID.
            var so = new SerializedObject(mixer);
            var exposedParams = so.FindProperty("m_ExposedParameters");

            string? targetGuid = null;
            for (int i = 0; i < exposedParams.arraySize; i++)
            {
                var param = exposedParams.GetArrayElementAtIndex(i);
                var nameProperty = param.FindPropertyRelative("name");
                if (nameProperty != null && nameProperty.stringValue == parameterName)
                {
                    var guidProperty = param.FindPropertyRelative("m_GUID");
                    targetGuid = guidProperty?.stringValue;
                    break;
                }
            }

            if (targetGuid == null)
                throw new ArgumentException($"Parameter '{parameterName}' not found in mixer exposed parameters on '{mixer.name}'. Use audio.getMixerSettings to list exposed parameters.");

            // Step 2: Read the current value and write the new value into all snapshots.
            // Values are stored in AudioMixerSnapshotController.m_FloatValues as a
            // serialized dictionary with keys "first" (GUID) and "second" (float value).
            float previousValue = 0f;
            bool valueWritten = false;
            var mixerPath = AssetDatabase.GetAssetPath(mixer);
            var snapshots = AssetDatabase.LoadAllAssetsAtPath(mixerPath)
                .OfType<AudioMixerSnapshot>()
                .ToArray();

            foreach (var snapshot in snapshots)
            {
                var snapshotSo = new SerializedObject(snapshot);
                var floatValues = snapshotSo.FindProperty("m_FloatValues");
                if (floatValues == null) continue;

                for (int i = 0; i < floatValues.arraySize; i++)
                {
                    var entry = floatValues.GetArrayElementAtIndex(i);
                    var key = entry.FindPropertyRelative("first");
                    if (key != null && key.stringValue == targetGuid)
                    {
                        var valueProperty = entry.FindPropertyRelative("second");
                        if (valueProperty != null)
                        {
                            snapshotSo.Update();
                            if (!valueWritten) previousValue = valueProperty.floatValue;
                            valueProperty.floatValue = value;
                            snapshotSo.ApplyModifiedProperties();
                            EditorUtility.SetDirty(snapshot);
                            valueWritten = true;
                        }
                        break;
                    }
                }
            }

            EditorUtility.SetDirty(mixer);
            AssetDatabase.SaveAssets();

            if (!valueWritten)
                throw new ArgumentException($"Parameter '{parameterName}' (GUID: {targetGuid}) found in exposed parameters but no matching entry in snapshot m_FloatValues. The parameter may not have a value set in any snapshot.");

            var result = new
            {
                mixerAssetPath,
                parameterName,
                value,
                previousValue
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildGetAudioListenerSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = root["params"] as JObject ?? new JObject();
            var listener = ResolveAudioListener(paramsObject, "audio.getListenerSettings");

            var result = new
            {
                target = CreateObjectSummary(listener.gameObject),
                component = CreateComponentSummary(listener),
                settings = new
                {
                    volume = AudioListener.volume,
                    pause = AudioListener.pause,
                    velocityUpdateMode = listener.velocityUpdateMode.ToString()
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }

        internal static string BuildSetAudioListenerSettingsResponse(JToken idToken, JObject root)
        {
            var paramsObject = root["params"] as JObject ?? new JObject();

            var volume = ParseOptionalFloatParameter(paramsObject, "volume");
            var pause = ParseOptionalBooleanValueParameter(paramsObject, "pause");
            var velocityUpdateMode = ParseOptionalStringParameter(paramsObject, "velocityUpdateMode");

            if (!volume.HasValue && !pause.HasValue && velocityUpdateMode == null)
                throw new ArgumentException("At least one AudioListener property must be specified: volume, pause, or velocityUpdateMode.");

            var listener = ResolveAudioListener(paramsObject, "audio.setListenerSettings");

            if (volume.HasValue)
                AudioListener.volume = Mathf.Clamp01(volume.Value);

            if (pause.HasValue)
                AudioListener.pause = pause.Value;

            if (velocityUpdateMode != null)
            {
                if (Enum.TryParse<AudioVelocityUpdateMode>(velocityUpdateMode, ignoreCase: true, out var vm))
                {
                    Undo.RecordObject(listener, "UnityMCP Set AudioListener Settings");
                    listener.velocityUpdateMode = vm;
                    EditorUtility.SetDirty(listener);
                }
                else if (int.TryParse(velocityUpdateMode, out var vmi))
                {
                    Undo.RecordObject(listener, "UnityMCP Set AudioListener Settings");
                    listener.velocityUpdateMode = (AudioVelocityUpdateMode)vmi;
                    EditorUtility.SetDirty(listener);
                }
                else
                {
                    throw new ArgumentException($"Parameter 'velocityUpdateMode' must be a valid AudioVelocityUpdateMode: Auto, Fixed, Dynamic (or integer 0, 1, 2).");
                }
            }

            var result = new
            {
                target = CreateObjectSummary(listener.gameObject),
                component = CreateComponentSummary(listener),
                settings = new
                {
                    volume = AudioListener.volume,
                    pause = AudioListener.pause,
                    velocityUpdateMode = listener.velocityUpdateMode.ToString()
                },
                applied = new
                {
                    volume = volume.HasValue,
                    pause = pause.HasValue,
                    velocityUpdateMode = velocityUpdateMode != null
                }
            };

            return UnityMcpProtocol.CreateResult(idToken, result);
        }
    }
}
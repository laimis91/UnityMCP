using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp;

public sealed partial class McpToolCatalog
{
    private static IEnumerable<McpToolDefinition> GetAudioTools()
    {
        yield return new McpToolDefinition(
            "audioSource.getSettings",
            "Returns AudioSource component settings for the target.",
            InstanceIdOnlySchema("Unity instance id of an AudioSource component or a GameObject with a single AudioSource."));

        yield return new McpToolDefinition(
            "audioSource.setSettings",
            "Mutates AudioSource component settings.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer" },
                    ["enabled"] = new JsonObject { ["type"] = "boolean" },
                    ["volume"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1 },
                    ["pitch"] = new JsonObject { ["type"] = "number" },
                    ["loop"] = new JsonObject { ["type"] = "boolean" },
                    ["playOnAwake"] = new JsonObject { ["type"] = "boolean" },
                    ["mute"] = new JsonObject { ["type"] = "boolean" },
                    ["spatialBlend"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1, ["description"] = "0 = 2D, 1 = 3D." },
                    ["spatialize"] = new JsonObject { ["type"] = "boolean" },
                    ["priority"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 256 },
                    ["dopplerLevel"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                    ["minDistance"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                    ["maxDistance"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                    ["rolloffMode"] = EnumLikeSchema("AudioRolloffMode enum name or integer value.")
                }
            });

        yield return new McpToolDefinition(
            "audio.getSourceSettings",
            "Get all AudioSource component settings (clip, volume, pitch, loop, mute, spatialBlend, distances, rolloffMode, mixerGroup, isPlaying) by instance id.",
            InstanceIdOnlySchema("Unity instance id of an AudioSource component or a GameObject with a single AudioSource."));

        yield return new McpToolDefinition(
            "audio.setSourceSettings",
            "Set any subset of AudioSource properties. Provide at least one property to change.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer", ["description"] = "Unity instance id of an AudioSource component or a GameObject with a single AudioSource." },
                    ["volume"] = new JsonObject { ["type"] = "number", ["description"] = "AudioSource volume, clamped to [0.0, 1.0]." },
                    ["pitch"] = new JsonObject { ["type"] = "number", ["description"] = "AudioSource pitch, clamped to [-3.0, 3.0]." },
                    ["loop"] = new JsonObject { ["type"] = "boolean", ["description"] = "Whether the AudioSource loops." },
                    ["mute"] = new JsonObject { ["type"] = "boolean", ["description"] = "Whether the AudioSource is muted." },
                    ["playOnAwake"] = new JsonObject { ["type"] = "boolean", ["description"] = "Whether the AudioSource plays on Awake." },
                    ["spatialBlend"] = new JsonObject { ["type"] = "number", ["description"] = "Spatial blend factor. 0.0 = fully 2D, 1.0 = fully 3D." },
                    ["minDistance"] = new JsonObject { ["type"] = "number", ["description"] = "Minimum distance for volume rolloff. Must be > 0." },
                    ["maxDistance"] = new JsonObject { ["type"] = "number", ["description"] = "Maximum distance for volume rolloff. Must be > minDistance." },
                    ["rolloffMode"] = EnumLikeSchema("AudioRolloffMode as enum name (Logarithmic/Linear/Custom) or integer (0/1/2)."),
                    ["clipAssetPath"] = new JsonObject { ["type"] = "string", ["description"] = "Asset path to load an AudioClip from (e.g. 'Assets/Audio/Music.mp3'). Pass empty string to clear." },
                    ["mixerGroupPath"] = new JsonObject { ["type"] = new JsonArray("string", "null"), ["description"] = "Mixer group path in format 'MixerName/GroupName'. Pass null or empty string to clear." }
                }
            });

        yield return new McpToolDefinition(
            "audio.play",
            "Call AudioSource.Play() on the target. Optionally delay by seconds. Requires the Editor to be in Play mode.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer", ["description"] = "Unity instance id of an AudioSource component or a GameObject with a single AudioSource." },
                    ["delay"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["description"] = "Delay in seconds before playback begins. Must be >= 0." }
                }
            });

        yield return new McpToolDefinition(
            "audio.stop",
            "Call AudioSource.Stop() on the target. Requires the Editor to be in Play mode.",
            InstanceIdOnlySchema("Unity instance id of an AudioSource component or a GameObject with a single AudioSource."));

        yield return new McpToolDefinition(
            "audio.pause",
            "Call AudioSource.Pause() on the target. Requires the Editor to be in Play mode.",
            InstanceIdOnlySchema("Unity instance id of an AudioSource component or a GameObject with a single AudioSource."));

        yield return new McpToolDefinition(
            "audio.unpause",
            "Call AudioSource.UnPause() on the target. Requires the Editor to be in Play mode.",
            InstanceIdOnlySchema("Unity instance id of an AudioSource component or a GameObject with a single AudioSource."));

        yield return new McpToolDefinition(
            "audio.getIsPlaying",
            "Return whether an AudioSource is currently playing audio. Always false outside play mode (isPlayMode included in response).",
            InstanceIdOnlySchema("Unity instance id of an AudioSource component or a GameObject with a single AudioSource."));

        yield return new McpToolDefinition(
            "audio.getMixerSettings",
            "Get AudioMixer info: name, snapshot names, and exposed parameter name/value pairs (values in dB).",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("mixerAssetPath"),
                ["properties"] = new JsonObject
                {
                    ["mixerAssetPath"] = new JsonObject { ["type"] = "string", ["description"] = "Asset path to the AudioMixer asset (e.g. 'Assets/Audio/MainMixer.mixer')." }
                }
            });

        yield return new McpToolDefinition(
            "audio.setMixerParameter",
            "Set a named exposed parameter on an AudioMixer by asset path. Parameters are in dB space.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("mixerAssetPath", "parameterName", "value"),
                ["properties"] = new JsonObject
                {
                    ["mixerAssetPath"] = new JsonObject { ["type"] = "string", ["description"] = "Asset path to the AudioMixer asset (e.g. 'Assets/Audio/MainMixer.mixer')." },
                    ["parameterName"] = new JsonObject { ["type"] = "string", ["description"] = "Name of the exposed parameter as defined in the AudioMixer (case-sensitive)." },
                    ["value"] = new JsonObject { ["type"] = "number", ["description"] = "New value for the parameter. AudioMixer parameters are in dB space; typical range -80.0 to 20.0." }
                }
            });

        yield return new McpToolDefinition(
            "audio.getListenerSettings",
            "Get AudioListener settings: global volume, pause state, and velocity update mode. instanceId optional; falls back to FindObjectOfType.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer", ["description"] = "Unity instance id of an AudioListener component or a GameObject with a single AudioListener. If omitted, falls back to FindObjectOfType." }
                }
            });

        yield return new McpToolDefinition(
            "audio.setListenerSettings",
            "Set AudioListener volume, pause state, or velocityUpdateMode. instanceId optional; falls back to FindObjectOfType.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer", ["description"] = "Unity instance id of an AudioListener component or a GameObject with a single AudioListener. If omitted, falls back to FindObjectOfType." },
                    ["volume"] = new JsonObject { ["type"] = "number", ["description"] = "Master listener volume, clamped to [0.0, 1.0]. Sets static AudioListener.volume." },
                    ["pause"] = new JsonObject { ["type"] = "boolean", ["description"] = "Whether to pause the AudioListener (pauses all audio). Sets static AudioListener.pause." },
                    ["velocityUpdateMode"] = EnumLikeSchema("AudioVelocityUpdateMode: Auto/0, Fixed/1, Dynamic/2.")
                }
            });
    }
}

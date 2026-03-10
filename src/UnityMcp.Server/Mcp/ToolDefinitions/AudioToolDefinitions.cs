using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp.ToolDefinitions;

internal static class AudioToolDefinitions
{
    internal static McpToolDefinition[] GetTools()
    {
        return new[]
        {
            new McpToolDefinition(
                "audioSource.getSettings",
                "Returns AudioSource settings for an AudioSource component target (or a GameObject with a single AudioSource).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of an AudioSource component or a GameObject with a single AudioSource.")),
            new McpToolDefinition(
                "audioSource.setSettings",
                "Mutates AudioSource settings using direct Unity AudioSource APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["clip"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity asset path to the AudioClip."
                        },
                        ["output"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity asset path to the AudioMixerGroup."
                        },
                        ["mute"] = new JsonObject { ["type"] = "boolean" },
                        ["bypassEffects"] = new JsonObject { ["type"] = "boolean" },
                        ["bypassListenerEffects"] = new JsonObject { ["type"] = "boolean" },
                        ["bypassReverbZones"] = new JsonObject { ["type"] = "boolean" },
                        ["playOnAwake"] = new JsonObject { ["type"] = "boolean" },
                        ["loop"] = new JsonObject { ["type"] = "boolean" },
                        ["priority"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 256 },
                        ["volume"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1 },
                        ["pitch"] = new JsonObject { ["type"] = "number", ["minimum"] = -3, ["maximum"] = 3 },
                        ["stereoPan"] = new JsonObject { ["type"] = "number", ["minimum"] = -1, ["maximum"] = 1 },
                        ["spatialBlend"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1 },
                        ["reverbZoneMix"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1.1 }
                    }
                }),
            new McpToolDefinition(
                "audio.getSourceSettings",
                "Returns AudioSource settings for an AudioSource component target (or a GameObject with a single AudioSource).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of an AudioSource component or a GameObject with a single AudioSource.")),
            new McpToolDefinition(
                "audio.setSourceSettings",
                "Mutates AudioSource settings using direct Unity AudioSource APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["clip"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity asset path to the AudioClip."
                        },
                        ["volume"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1 },
                        ["pitch"] = new JsonObject { ["type"] = "number", ["minimum"] = -3, ["maximum"] = 3 },
                        ["loop"] = new JsonObject { ["type"] = "boolean" },
                        ["playOnAwake"] = new JsonObject { ["type"] = "boolean" }
                    }
                }),
            new McpToolDefinition(
                "audio.play",
                "Plays an AudioSource component.",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of an AudioSource component or a GameObject with a single AudioSource.")),
            new McpToolDefinition(
                "audio.stop",
                "Stops an AudioSource component.",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of an AudioSource component or a GameObject with a single AudioSource.")),
            new McpToolDefinition(
                "audio.pause",
                "Pauses an AudioSource component.",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of an AudioSource component or a GameObject with a single AudioSource.")),
            new McpToolDefinition(
                "audio.unpause",
                "Unpauses an AudioSource component.",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of an AudioSource component or a GameObject with a single AudioSource.")),
            new McpToolDefinition(
                "audio.getIsPlaying",
                "Returns whether an AudioSource component is playing.",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of an AudioSource component or a GameObject with a single AudioSource.")),
            new McpToolDefinition(
                "audio.getMixerSettings",
                "Returns AudioMixer settings for an AudioMixer asset.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("mixerPath"),
                    ["properties"] = new JsonObject
                    {
                        ["mixerPath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity asset path to the AudioMixer."
                        }
                    }
                }),
            new McpToolDefinition(
                "audio.setMixerParameter",
                "Sets a parameter on an AudioMixer asset.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("mixerPath", "parameterName", "value"),
                    ["properties"] = new JsonObject
                    {
                        ["mixerPath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity asset path to the AudioMixer."
                        },
                        ["parameterName"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Name of the parameter to set."
                        },
                        ["value"] = new JsonObject
                        {
                            ["type"] = "number",
                            ["description"] = "Value to set for the parameter."
                        }
                    }
                }),
            new McpToolDefinition(
                "audio.getListenerSettings",
                "Returns AudioListener settings.",
                McpToolSchemaHelpers.EmptyObjectSchema()),
            new McpToolDefinition(
                "audio.setListenerSettings",
                "Mutates AudioListener settings using direct Unity AudioListener APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new JsonObject
                    {
                        ["volume"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1 },
                        ["pause"] = new JsonObject { ["type"] = "boolean" },
                        ["velocityUpdateMode"] = McpToolSchemaHelpers.EnumLikeSchema("AudioVelocityUpdateMode enum name or integer value.")
                    }
                })
        };
    }
}
using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp.ToolDefinitions;

internal static class ParticleSystemToolDefinitions
{
    internal static McpToolDefinition[] GetTools()
    {
        return new[]
        {
            new McpToolDefinition(
                "particleSystem.getSettings",
                "Returns ParticleSystem settings for a ParticleSystem component target (or a GameObject with a single ParticleSystem).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a ParticleSystem component or a GameObject with a single ParticleSystem.")),
            new McpToolDefinition(
                "particleSystem.setSettings",
                "Mutates ParticleSystem settings using direct Unity ParticleSystem APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["duration"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["looping"] = new JsonObject { ["type"] = "boolean" },
                        ["prewarm"] = new JsonObject { ["type"] = "boolean" },
                        ["startLifetime"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["startSpeed"] = new JsonObject { ["type"] = "number" },
                        ["startSize"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["startRotation"] = new JsonObject { ["type"] = "number" },
                        ["startColor"] = McpToolSchemaHelpers.ColorSchema("Start color RGBA array [r,g,b,a]."),
                        ["gravityModifier"] = new JsonObject { ["type"] = "number" },
                        ["simulationSpace"] = McpToolSchemaHelpers.EnumLikeSchema("ParticleSystemSimulationSpace enum name or integer value."),
                        ["simulationSpeed"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["deltaTime"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["scalingMode"] = McpToolSchemaHelpers.EnumLikeSchema("ParticleSystemScalingMode enum name or integer value."),
                        ["playOnAwake"] = new JsonObject { ["type"] = "boolean" },
                        ["emissionRate"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["maxParticles"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "particleSystem.play",
                "Plays a ParticleSystem.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["withChildren"] = new JsonObject
                        {
                            ["type"] = "boolean",
                            ["description"] = "Whether to also play child particle systems (default true)."
                        }
                    }
                }),
            new McpToolDefinition(
                "particleSystem.stop",
                "Stops a ParticleSystem.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["withChildren"] = new JsonObject
                        {
                            ["type"] = "boolean",
                            ["description"] = "Whether to also stop child particle systems (default true)."
                        },
                        ["stopBehavior"] = McpToolSchemaHelpers.EnumLikeSchema("ParticleSystemStopBehavior enum name or integer value.")
                    }
                })
        };
    }
}
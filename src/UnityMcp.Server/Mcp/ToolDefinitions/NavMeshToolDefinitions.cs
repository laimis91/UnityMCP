using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp.ToolDefinitions;

internal static class NavMeshToolDefinitions
{
    internal static McpToolDefinition[] GetTools()
    {
        return new[]
        {
            new McpToolDefinition(
                "navMeshAgent.getSettings",
                "Returns NavMeshAgent settings for a NavMeshAgent component target (or a GameObject with a single NavMeshAgent).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a NavMeshAgent component or a GameObject with a single NavMeshAgent.")),
            new McpToolDefinition(
                "navMeshAgent.setSettings",
                "Mutates NavMeshAgent settings using direct Unity NavMeshAgent APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["agentTypeID"] = new JsonObject { ["type"] = "integer" },
                        ["radius"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["height"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["quality"] = McpToolSchemaHelpers.EnumLikeSchema("ObstacleAvoidanceType enum name or integer value."),
                        ["priority"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 99 },
                        ["speed"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["angularSpeed"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["acceleration"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["stoppingDistance"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["autoBraking"] = new JsonObject { ["type"] = "boolean" },
                        ["autoTraverseOffMeshLink"] = new JsonObject { ["type"] = "boolean" },
                        ["autoRepath"] = new JsonObject { ["type"] = "boolean" },
                        ["areaMask"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "navMeshObstacle.getSettings",
                "Returns NavMeshObstacle settings for a NavMeshObstacle component target (or a GameObject with a single NavMeshObstacle).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a NavMeshObstacle component or a GameObject with a single NavMeshObstacle.")),
            new McpToolDefinition(
                "navMeshObstacle.setSettings",
                "Mutates NavMeshObstacle settings using direct Unity NavMeshObstacle APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["shape"] = McpToolSchemaHelpers.EnumLikeSchema("NavMeshObstacleShape enum name or integer value."),
                        ["center"] = McpToolSchemaHelpers.Vector3Schema("NavMeshObstacle center [x,y,z]."),
                        ["size"] = McpToolSchemaHelpers.Vector3Schema("NavMeshObstacle size [x,y,z]."),
                        ["carving"] = new JsonObject { ["type"] = "boolean" },
                        ["carveOnlyStationary"] = new JsonObject { ["type"] = "boolean" },
                        ["carvingMoveThreshold"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["carvingTimeToStationary"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "navMesh.bake",
                "Bakes the Unity NavMesh for the current scene.",
                McpToolSchemaHelpers.EmptyObjectSchema())
        };
    }
}
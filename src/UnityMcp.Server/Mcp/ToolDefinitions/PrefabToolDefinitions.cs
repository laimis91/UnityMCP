using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp.ToolDefinitions;

internal static class PrefabToolDefinitions
{
    internal static McpToolDefinition[] GetTools()
    {
        return new[]
        {
            new McpToolDefinition(
                "prefab.instantiate",
                "Instantiates a prefab asset into the active scene.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("assetPath"),
                    ["properties"] = new JsonObject
                    {
                        ["assetPath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Prefab asset path under Assets/."
                        },
                        ["parentInstanceId"] = McpToolSchemaHelpers.NullableIntegerSchema("Optional parent scene object or component instance id, or null to leave the instance at the scene root."),
                        ["position"] = McpToolSchemaHelpers.Vector3Schema("Optional world position [x,y,z]."),
                        ["rotationEuler"] = McpToolSchemaHelpers.Vector3Schema("Optional world euler rotation [x,y,z]."),
                        ["select"] = new JsonObject
                        {
                            ["type"] = "boolean",
                            ["description"] = "Whether to select the instantiated object after creation. Defaults to true."
                        },
                        ["ping"] = new JsonObject
                        {
                            ["type"] = "boolean",
                            ["description"] = "Optional. Ping/highlight the instantiated object in the Editor."
                        },
                        ["focus"] = new JsonObject
                        {
                            ["type"] = "boolean",
                            ["description"] = "Optional. Best-effort frame the instantiated object in the Scene view."
                        }
                    }
                }),
            new McpToolDefinition(
                "prefab.getSource",
                "Returns the prefab source asset path for a prefab instance.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Unity instance id of a prefab instance GameObject."
                        }
                    }
                }),
            new McpToolDefinition(
                "prefab.applyOverrides",
                "Applies prefab overrides from a scene prefab instance back to the prefab asset.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Unity instance id of a prefab instance object or component in a scene."
                        },
                        ["scope"] = McpToolSchemaHelpers.PrefabScopeSchema(),
                        ["componentInstanceId"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Component instance id to target when scope is component. If omitted, a component instanceId input may be reused."
                        }
                    }
                }),
            new McpToolDefinition(
                "prefab.revertOverrides",
                "Reverts prefab overrides on a scene prefab instance.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Unity instance id of a prefab instance object or component in a scene."
                        },
                        ["scope"] = McpToolSchemaHelpers.PrefabScopeSchema(),
                        ["componentInstanceId"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Component instance id to target when scope is component. If omitted, a component instanceId input may be reused."
                        }
                    }
                })
        };
    }
}
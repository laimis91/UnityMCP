using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp;

public sealed partial class McpToolCatalog
{
    private static IEnumerable<McpToolDefinition> GetPrefabTools()
    {
        yield return new McpToolDefinition(
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
                    ["parentInstanceId"] = NullableIntegerSchema("Optional parent scene object or component instance id, or null to leave the instance at the scene root."),
                    ["position"] = Vector3Schema("Optional world position [x,y,z]."),
                    ["rotationEuler"] = Vector3Schema("Optional world euler rotation [x,y,z]."),
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
            });

        yield return new McpToolDefinition(
            "prefab.getSource",
            "Resolves prefab source metadata for a prefab instance in a scene.",
            InstanceIdOnlySchema("Unity instance id of a prefab instance object or component in a scene."));

        yield return new McpToolDefinition(
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
                    ["scope"] = PrefabScopeSchema(),
                    ["componentInstanceId"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["description"] = "Component instance id to target when scope is component. If omitted, a component instanceId input may be reused."
                    }
                }
            });

        yield return new McpToolDefinition(
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
                    ["scope"] = PrefabScopeSchema(),
                    ["componentInstanceId"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["description"] = "Component instance id to target when scope is component. If omitted, a component instanceId input may be reused."
                    }
                }
            });
    }
}

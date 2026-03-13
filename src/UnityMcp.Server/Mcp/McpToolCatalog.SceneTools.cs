using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp;

public sealed partial class McpToolCatalog
{
    private static IEnumerable<McpToolDefinition> GetSceneTools()
    {
        yield return new McpToolDefinition(
            "scene.getActiveScene",
            "Returns metadata for the currently active Unity scene.",
            EmptyObjectSchema());

        yield return new McpToolDefinition(
            "scene.listOpenScenes",
            "Returns metadata for all currently open Unity scenes.",
            EmptyObjectSchema());

        yield return new McpToolDefinition(
            "scene.getSelection",
            "Returns metadata for the current Unity Editor selection.",
            EmptyObjectSchema());

        yield return new McpToolDefinition(
            "scene.selectObject",
            "Selects a single Unity object by instance id.",
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
                        ["description"] = "Unity instance id of the object to select."
                    },
                    ["ping"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Optional. Ping/highlight the selected object in the Editor."
                    },
                    ["focus"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Optional. Best-effort frame the selection in the Scene view."
                    }
                }
            });

        yield return new McpToolDefinition(
            "scene.selectByPath",
            "Selects a single Unity scene object by hierarchy path.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("path"),
                ["properties"] = new JsonObject
                {
                    ["path"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Hierarchy path matching the returned object summaries (for example 'Cube/Main Camera')."
                    },
                    ["scenePath"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Optional Unity scene path used to disambiguate duplicate hierarchy paths across open scenes."
                    },
                    ["ping"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Optional. Ping/highlight the selected object in the Editor."
                    },
                    ["focus"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Optional. Best-effort frame the selection in the Scene view."
                    }
                }
            });

        yield return new McpToolDefinition(
            "scene.findByPath",
            "Finds Unity scene objects by hierarchy path without changing selection.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("path"),
                ["properties"] = new JsonObject
                {
                    ["path"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Hierarchy path matching the returned object summaries (for example 'Cube/Main Camera')."
                    },
                    ["scenePath"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Optional Unity scene path used to scope/disambiguate matches."
                    }
                }
            });

        yield return new McpToolDefinition(
            "scene.getComponents",
            "Returns component metadata for the target GameObject (or a Component's owner GameObject).",
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
                        ["description"] = "Unity instance id of a GameObject or Component."
                    }
                }
            });

        yield return new McpToolDefinition(
            "scene.destroyObject",
            "Destroys a Unity scene object or Component by instance id (Undo-aware; Transform component targets are rejected).",
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
                        ["description"] = "Unity instance id of a scene GameObject or Component to destroy."
                    }
                }
            });

        yield return new McpToolDefinition(
            "scene.getComponentProperties",
            "Returns a constrained set of serialized properties for a Component by instance id.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("componentInstanceId"),
                ["properties"] = new JsonObject
                {
                    ["componentInstanceId"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["description"] = "Unity instance id of a Component."
                    }
                }
            });

        yield return new McpToolDefinition(
            "scene.setComponentProperties",
            "Sets a constrained set of serialized Component properties by instance id.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("componentInstanceId", "properties"),
                ["properties"] = new JsonObject
                {
                    ["componentInstanceId"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["description"] = "Unity instance id of a Component."
                    },
                    ["properties"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["description"] = "Property path/value map for supported serialized property types."
                    }
                }
            });

        yield return new McpToolDefinition(
            "scene.setTransform",
            "Mutates basic transform properties on a GameObject/Component target.",
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
                        ["description"] = "Unity instance id of a GameObject or Component."
                    },
                    ["position"] = Vector3Schema("Optional world-space position [x,y,z]."),
                    ["localPosition"] = Vector3Schema("Optional local-space position [x,y,z]."),
                    ["rotationEuler"] = Vector3Schema("Optional world-space euler rotation [x,y,z]."),
                    ["localRotationEuler"] = Vector3Schema("Optional local-space euler rotation [x,y,z]."),
                    ["localScale"] = Vector3Schema("Optional local-scale [x,y,z].")
                }
            });

        yield return new McpToolDefinition(
            "scene.addComponent",
            "Adds a Component to a GameObject (or a Component target's owner GameObject) by type name.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId", "typeName"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["description"] = "Unity instance id of a GameObject or Component."
                    },
                    ["typeName"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Component type name (short name, full name, or assembly-qualified name if needed)."
                    }
                }
            });

        yield return new McpToolDefinition(
            "scene.setSelection",
            "Replaces the Unity Editor selection with the specified object instance ids.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceIds"),
                ["properties"] = new JsonObject
                {
                    ["instanceIds"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["description"] = "Unity instance ids to set as the current selection (duplicates ignored).",
                        ["items"] = new JsonObject
                        {
                            ["type"] = "integer"
                        }
                    },
                    ["ping"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Optional. Ping/highlight the active selected object in the Editor."
                    },
                    ["focus"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Optional. Best-effort frame the selection in the Scene view."
                    }
                }
            });

        yield return new McpToolDefinition(
            "scene.pingObject",
            "Pings/highlights a Unity object in the Editor by instance id without changing selection.",
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
                        ["description"] = "Unity instance id of the object to ping."
                    }
                }
            });

        yield return new McpToolDefinition(
            "scene.frameSelection",
            "Best-effort frames the current Unity Editor selection in the Scene view.",
            EmptyObjectSchema());

        yield return new McpToolDefinition(
            "scene.frameObject",
            "Best-effort frames a Unity scene object in the Scene view by instance id without changing selection.",
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
                        ["description"] = "Unity instance id of the scene object to frame."
                    }
                }
            });

        yield return new McpToolDefinition(
            "scene.createGameObject",
            "Creates a GameObject in the active scene and optionally sets its world position.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JsonObject
                {
                    ["name"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Optional GameObject name."
                    },
                    ["position"] = new JsonObject
                    {
                        ["type"] = "array",
                        ["description"] = "Optional world position [x,y,z].",
                        ["minItems"] = 3,
                        ["maxItems"] = 3,
                        ["items"] = new JsonObject
                        {
                            ["type"] = "number"
                        }
                    }
                }
            });

        yield return new McpToolDefinition(
            "scene.setParent",
            "Reparents a scene object under another scene object or unparents it to the scene root.",
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
                        ["description"] = "Unity instance id of the scene object or component to reparent."
                    },
                    ["parentInstanceId"] = NullableIntegerSchema("Unity instance id of the new parent scene object or component, or null to unparent to the scene root."),
                    ["keepWorldTransform"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Whether to preserve world position/rotation/scale during reparenting. Defaults to true."
                    },
                    ["ping"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Optional. Ping/highlight the moved object in the Editor after selection."
                    },
                    ["focus"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Optional. Best-effort frame the moved object in the Scene view after selection."
                    }
                }
            });

        yield return new McpToolDefinition(
            "scene.duplicateObject",
            "Duplicates a scene object.",
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
                        ["description"] = "Unity instance id of the scene object or component to duplicate."
                    },
                    ["select"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Whether to select the duplicate after creation. Defaults to true."
                    },
                    ["ping"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Optional. Ping/highlight the duplicate in the Editor."
                    },
                    ["focus"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Optional. Best-effort frame the duplicate in the Scene view."
                    }
                }
            });

        yield return new McpToolDefinition(
            "scene.renameObject",
            "Renames a scene GameObject.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId", "name"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["description"] = "Unity instance id of the scene object or component to rename."
                    },
                    ["name"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "New non-empty GameObject name."
                    }
                }
            });

        yield return new McpToolDefinition(
            "scene.setActive",
            "Toggles active state of a scene object.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId", "active"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["description"] = "Unity instance id of the scene object or component to toggle."
                    },
                    ["active"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Whether the target GameObject should be active."
                    }
                }
            });

        yield return new McpToolDefinition(
            "scene.findByTag",
            "Finds active GameObjects with the specified Unity tag.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("tag"),
                ["properties"] = new JsonObject
                {
                    ["tag"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Unity tag to search for."
                    }
                }
            });

        yield return new McpToolDefinition(
            "scene.setTag",
            "Sets the tag on a scene GameObject.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId", "tag"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer", ["description"] = "Unity instance id of a GameObject or Component." },
                    ["tag"] = new JsonObject { ["type"] = "string", ["description"] = "Tag name to assign." }
                }
            });

        yield return new McpToolDefinition(
            "scene.setLayer",
            "Sets the layer on a scene GameObject (and optionally all children).",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("instanceId", "layer"),
                ["properties"] = new JsonObject
                {
                    ["instanceId"] = new JsonObject { ["type"] = "integer", ["description"] = "Unity instance id of a GameObject or Component." },
                    ["layer"] = new JsonObject
                    {
                        ["description"] = "Layer as integer index (0-31) or layer name string.",
                        ["oneOf"] = new JsonArray { new JsonObject { ["type"] = "integer" }, new JsonObject { ["type"] = "string" } }
                    },
                    ["includeChildren"] = new JsonObject { ["type"] = "boolean", ["description"] = "Whether to apply the layer to all child GameObjects as well (default false)." }
                }
            });

        yield return new McpToolDefinition(
            "scene.save",
            "Saves the active Unity scene (or a specified scene by path).",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JsonObject
                {
                    ["scenePath"] = new JsonObject { ["type"] = "string", ["description"] = "Optional scene path to save. Defaults to the active scene." }
                }
            });

        yield return new McpToolDefinition(
            "scene.openScene",
            "Opens a Unity scene from the project.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("scenePath"),
                ["properties"] = new JsonObject
                {
                    ["scenePath"] = new JsonObject { ["type"] = "string", ["description"] = "Project-relative scene path under Assets/." },
                    ["mode"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Open mode: Single (replaces current scenes) or Additive. Default: Single.",
                        ["enum"] = new JsonArray("Single", "Additive")
                    }
                }
            });

        yield return new McpToolDefinition(
            "scene.newScene",
            "Creates a new empty Unity scene.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JsonObject
                {
                    ["setup"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Scene setup: EmptyScene or DefaultGameObjects. Default: DefaultGameObjects.",
                        ["enum"] = new JsonArray("EmptyScene", "DefaultGameObjects")
                    },
                    ["mode"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Open mode: Single (replaces current) or Additive. Default: Single.",
                        ["enum"] = new JsonArray("Single", "Additive")
                    }
                }
            });

        yield return new McpToolDefinition(
            "scene.closeScene",
            "Closes/unloads an open Unity scene by path or name.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("scenePath"),
                ["properties"] = new JsonObject
                {
                    ["scenePath"] = new JsonObject { ["type"] = "string", ["description"] = "Project-relative scene path or scene name to close." },
                    ["removeScene"] = new JsonObject { ["type"] = "boolean", ["description"] = "Whether to remove the scene from the hierarchy entirely (default true)." }
                }
            });

        yield return new McpToolDefinition(
            "scene.setActiveScene",
            "Sets the active Unity scene among the currently open scenes.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("scenePath"),
                ["properties"] = new JsonObject
                {
                    ["scenePath"] = new JsonObject { ["type"] = "string", ["description"] = "Project-relative scene path or scene name to make active." }
                }
            });

        yield return new McpToolDefinition(
            "scene.getSelectionDetails",
            "Returns full component list and transform details for all currently selected objects.",
            EmptyObjectSchema());

        yield return new McpToolDefinition(
            "scene.selectByName",
            "Finds and selects GameObject(s) by name.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("name"),
                ["properties"] = new JsonObject
                {
                    ["name"] = new JsonObject { ["type"] = "string", ["description"] = "Name of the GameObject(s) to find and select." },
                    ["exactMatch"] = new JsonObject { ["type"] = "boolean", ["description"] = "If true (default), match name exactly; if false, match as substring." }
                }
            });

        yield return new McpToolDefinition(
            "scene.instantiatePrefab",
            "Instantiates a prefab into the active scene by asset path, with optional position and parent.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("assetPath"),
                ["properties"] = new JsonObject
                {
                    ["assetPath"] = new JsonObject { ["type"] = "string", ["description"] = "Project-relative path to the prefab asset (e.g. Assets/Prefabs/Enemy.prefab)." },
                    ["position"] = Vector3Schema("World-space position [x, y, z] for the instantiated prefab."),
                    ["parentInstanceId"] = NullableIntegerSchema("Instance ID of a parent GameObject, or null for scene root.")
                }
            });

        yield return new McpToolDefinition(
            "scene.getHierarchy",
            "Returns the full scene tree with object names, instance IDs, active states, hierarchy depth, and component lists.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JsonObject
                {
                    ["includeInactive"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Whether to include inactive GameObjects in the hierarchy (default true)."
                    },
                    ["maxDepth"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["minimum"] = 0,
                        ["description"] = "Maximum hierarchy depth to traverse (unlimited if not specified)."
                    },
                    ["rootFilter"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Name of specific root GameObject to start traversal from (searches all roots if not specified)."
                    },
                    ["scenePath"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Scene asset path to get hierarchy from (uses active scene if not specified)."
                    },
                    ["allScenes"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Whether to get hierarchy from all open scenes (default false)."
                    },
                    ["maxNodes"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["minimum"] = 1,
                        ["maximum"] = 10000,
                        ["description"] = "Maximum number of nodes to return (default 2000)."
                    }
                }
            });
    }
}

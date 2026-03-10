using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp.ToolDefinitions;

internal static class SceneToolDefinitions
{
    internal static McpToolDefinition[] GetTools()
    {
        return new[]
        {
            new McpToolDefinition(
                "scene.getActiveScene",
                "Returns metadata for the currently active Unity scene.",
                McpToolSchemaHelpers.EmptyObjectSchema()),
            new McpToolDefinition(
                "scene.listOpenScenes",
                "Returns metadata for all currently open Unity scenes.",
                McpToolSchemaHelpers.EmptyObjectSchema()),
            new McpToolDefinition(
                "scene.getSelection",
                "Returns metadata for the current Unity Editor selection.",
                McpToolSchemaHelpers.EmptyObjectSchema()),
            new McpToolDefinition(
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
                }),
            new McpToolDefinition(
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
                }),
            new McpToolDefinition(
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
                }),
            new McpToolDefinition(
                "scene.getComponents",
                "Returns components for a Unity scene object.",
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
                            ["description"] = "Unity instance id of a GameObject or component."
                        }
                    }
                }),
            new McpToolDefinition(
                "scene.destroyObject",
                "Destroys a Unity scene object by instance id.",
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
                            ["description"] = "Unity instance id of a GameObject or Component to destroy."
                        }
                    }
                }),
            new McpToolDefinition(
                "scene.getComponentProperties",
                "Returns a map of settable properties for a Unity component (or a GameObject with a single component of the requested type).",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["type"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity type name (for example 'Transform') used to search for a single component on a GameObject."
                        }
                    }
                }),
            new McpToolDefinition(
                "scene.setComponentProperties",
                "Sets properties on a Unity component (or a GameObject with a single component of the requested type).",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId", "properties"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["type"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity type name (for example 'Transform') used to search for a single component on a GameObject."
                        },
                        ["properties"] = new JsonObject
                        {
                            ["type"] = "object",
                            ["description"] = "Map from property name to new property value."
                        }
                    }
                }),
            new McpToolDefinition(
                "scene.setTransform",
                "Sets transform properties on a Unity GameObject or Transform component.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["position"] = McpToolSchemaHelpers.Vector3Schema("Transform position [x,y,z]."),
                        ["rotation"] = McpToolSchemaHelpers.Vector3Schema("Transform rotation [x,y,z] as Euler angles in degrees."),
                        ["scale"] = McpToolSchemaHelpers.Vector3Schema("Transform scale [x,y,z].")
                    }
                }),
            new McpToolDefinition(
                "scene.addComponent",
                "Adds a Unity component to a GameObject.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId", "componentType"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Unity instance id of a GameObject."
                        },
                        ["componentType"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity type name (for example 'Rigidbody') of the component to add."
                        }
                    }
                }),
            new McpToolDefinition(
                "scene.setSelection",
                "Sets the Unity Editor selection to specified object instance ids.",
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
                            ["description"] = "Unity instance ids of objects to select.",
                            ["items"] = new JsonObject { ["type"] = "integer" }
                        }
                    }
                }),
            new McpToolDefinition(
                "scene.pingObject",
                "Pings/highlights a Unity object in the Editor by instance id.",
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
                            ["description"] = "Unity instance id of the object to ping/highlight."
                        }
                    }
                }),
            new McpToolDefinition(
                "scene.frameSelection",
                "Frames the current Unity Editor selection in the Scene view.",
                McpToolSchemaHelpers.EmptyObjectSchema()),
            new McpToolDefinition(
                "scene.frameObject",
                "Frames a Unity object in the Scene view by instance id.",
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
                            ["description"] = "Unity instance id of the object to frame."
                        }
                    }
                }),
            new McpToolDefinition(
                "scene.createGameObject",
                "Creates a new Unity GameObject in the active scene.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new JsonObject
                    {
                        ["name"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Optional name for the new GameObject."
                        },
                        ["parentInstanceId"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Optional Unity instance id of a parent GameObject."
                        }
                    }
                }),
            new McpToolDefinition(
                "scene.setParent",
                "Sets the parent of a Unity GameObject.",
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
                            ["description"] = "Unity instance id of a GameObject to reparent."
                        },
                        ["parentInstanceId"] = McpToolSchemaHelpers.NullableIntegerSchema("Unity instance id of the parent GameObject (or null to unparent)."),
                        ["keepWorldTransform"] = new JsonObject
                        {
                            ["type"] = "boolean",
                            ["description"] = "Whether to preserve world position/rotation/scale during reparenting. Defaults to true."
                        }
                    }
                }),
            new McpToolDefinition(
                "scene.duplicateObject",
                "Duplicates a Unity scene object by instance id.",
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
                            ["description"] = "Unity instance id of a GameObject to duplicate."
                        }
                    }
                }),
            new McpToolDefinition(
                "scene.renameObject",
                "Renames a Unity scene object by instance id.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId", "newName"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Unity instance id of a GameObject to rename."
                        },
                        ["newName"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "New name for the GameObject."
                        }
                    }
                }),
            new McpToolDefinition(
                "scene.setActive",
                "Sets the active state of a Unity GameObject by instance id.",
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
                            ["description"] = "Unity instance id of a GameObject to activate/deactivate."
                        },
                        ["active"] = new JsonObject
                        {
                            ["type"] = "boolean",
                            ["description"] = "Whether the GameObject should be active."
                        }
                    }
                }),
            new McpToolDefinition(
                "scene.findByTag",
                "Finds Unity GameObjects by tag.",
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
                            ["description"] = "Unity tag name to search for."
                        }
                    }
                }),
            new McpToolDefinition(
                "scene.setTag",
                "Sets the tag for a Unity GameObject by instance id.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId", "tag"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Unity instance id of a GameObject to tag."
                        },
                        ["tag"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Tag name to assign."
                        }
                    }
                }),
            new McpToolDefinition(
                "scene.setLayer",
                "Sets the layer for a Unity GameObject by instance id.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId", "layer"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Unity instance id of a GameObject to set layer for."
                        },
                        ["layer"] = new JsonObject
                        {
                            ["description"] = "Layer index (0-31) or layer name.",
                            ["oneOf"] = new JsonArray
                            {
                                new JsonObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 31 },
                                new JsonObject { ["type"] = "string" }
                            }
                        }
                    }
                }),
            new McpToolDefinition(
                "scene.save",
                "Saves the currently active Unity scene.",
                McpToolSchemaHelpers.EmptyObjectSchema()),
            new McpToolDefinition(
                "scene.openScene",
                "Opens a Unity scene by asset path.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("scenePath"),
                    ["properties"] = new JsonObject
                    {
                        ["scenePath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity asset path to the scene file (e.g., 'Assets/Scenes/MyScene.unity')."
                        },
                        ["additive"] = new JsonObject
                        {
                            ["type"] = "boolean",
                            ["description"] = "Whether to open additively without closing current scene (default false)."
                        }
                    }
                }),
            new McpToolDefinition(
                "scene.newScene",
                "Creates a new Unity scene.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new JsonObject
                    {
                        ["template"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Scene template: empty, default, or defaultGameObjects.",
                            ["enum"] = new JsonArray("empty", "default", "defaultGameObjects")
                        }
                    }
                }),
            new McpToolDefinition(
                "scene.closeScene",
                "Closes a Unity scene by path.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("scenePath"),
                    ["properties"] = new JsonObject
                    {
                        ["scenePath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity asset path to the scene file to close."
                        },
                        ["removeDependencies"] = new JsonObject
                        {
                            ["type"] = "boolean",
                            ["description"] = "Whether to also close dependent scenes (default false)."
                        }
                    }
                }),
            new McpToolDefinition(
                "scene.setActiveScene",
                "Sets the active Unity scene by path.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("scenePath"),
                    ["properties"] = new JsonObject
                    {
                        ["scenePath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity asset path to the scene file to make active."
                        }
                    }
                }),
            new McpToolDefinition(
                "scene.getSelectionDetails",
                "Returns detailed information for the current Unity Editor selection.",
                McpToolSchemaHelpers.EmptyObjectSchema()),
            new McpToolDefinition(
                "scene.selectByName",
                "Selects Unity scene objects by name (supports wildcards).",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("name"),
                    ["properties"] = new JsonObject
                    {
                        ["name"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "GameObject name or pattern (supports * and ? wildcards)."
                        },
                        ["maxResults"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Maximum number of objects to select (default 50).",
                            ["minimum"] = 1,
                            ["maximum"] = 1000
                        }
                    }
                }),
            new McpToolDefinition(
                "scene.instantiatePrefab",
                "Instantiates a prefab in the current scene.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("prefabPath"),
                    ["properties"] = new JsonObject
                    {
                        ["prefabPath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity asset path to the prefab to instantiate."
                        },
                        ["parentInstanceId"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Optional Unity instance id of a parent GameObject."
                        },
                        ["position"] = McpToolSchemaHelpers.Vector3Schema("Position for the instantiated prefab [x,y,z]."),
                        ["rotation"] = McpToolSchemaHelpers.Vector3Schema("Rotation for the instantiated prefab [x,y,z] as Euler angles in degrees."),
                        ["scale"] = McpToolSchemaHelpers.Vector3Schema("Scale for the instantiated prefab [x,y,z].")
                    }
                }),
            new McpToolDefinition(
                "scene.getHierarchy",
                "Returns a hierarchical view of all GameObjects in the active scene.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new JsonObject
                    {
                        ["maxDepth"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Maximum hierarchy depth to traverse (default 10).",
                            ["minimum"] = 1,
                            ["maximum"] = 20
                        }
                    }
                })
        };
    }
}
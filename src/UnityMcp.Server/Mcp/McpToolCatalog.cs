using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp;

public sealed class McpToolCatalog
{
    private readonly Dictionary<string, McpToolDefinition> _byName;

    public McpToolCatalog()
    {
        var tools = new[]
        {
            new McpToolDefinition(
                "editor.getPlayModeState",
                "Returns the current Unity Editor play mode and compilation state.",
                EmptyObjectSchema()),
            new McpToolDefinition(
                "editor.getConsoleLogs",
                "Returns a bounded snapshot of recent Unity Editor console logs captured by UnityMCP.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new JsonObject
                    {
                        ["maxResults"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Optional cap for returned log entries (1-500).",
                            ["minimum"] = 1,
                            ["maximum"] = 500
                        },
                        ["includeStackTrace"] = new JsonObject
                        {
                            ["type"] = "boolean",
                            ["description"] = "Whether to include stack traces in each entry (default false)."
                        },
                        ["contains"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Optional case-insensitive substring filter applied to log messages."
                        },
                        ["levels"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["description"] = "Optional log level filters (info, warning, error, assert, exception).",
                            ["items"] = new JsonObject
                            {
                                ["type"] = "string"
                            }
                        }
                    }
                }),
            new McpToolDefinition(
                "editor.consoleTail",
                "Returns log entries captured after a given sequence cursor.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("afterSequence"),
                    ["properties"] = new JsonObject
                    {
                        ["afterSequence"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["minimum"] = 0,
                            ["description"] = "Return entries with sequence greater than this cursor."
                        },
                        ["maxResults"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Optional cap for returned log entries (1-500).",
                            ["minimum"] = 1,
                            ["maximum"] = 500
                        },
                        ["includeStackTrace"] = new JsonObject
                        {
                            ["type"] = "boolean",
                            ["description"] = "Whether to include stack traces in each entry (default false)."
                        },
                        ["contains"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Optional case-insensitive substring filter applied to log messages."
                        },
                        ["levels"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["description"] = "Optional log level filters (info, warning, error, assert, exception).",
                            ["items"] = new JsonObject
                            {
                                ["type"] = "string"
                            }
                        }
                    }
                }),
            new McpToolDefinition(
                "editor.enterPlayMode",
                "Requests the Unity Editor to enter play mode.",
                EmptyObjectSchema()),
            new McpToolDefinition(
                "editor.exitPlayMode",
                "Requests the Unity Editor to exit play mode.",
                EmptyObjectSchema()),
            new McpToolDefinition(
                "scene.getActiveScene",
                "Returns metadata for the currently active Unity scene.",
                EmptyObjectSchema()),
            new McpToolDefinition(
                "scene.listOpenScenes",
                "Returns metadata for all currently open Unity scenes.",
                EmptyObjectSchema()),
            new McpToolDefinition(
                "scene.getSelection",
                "Returns metadata for the current Unity Editor selection.",
                EmptyObjectSchema()),
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
                "camera.getSettings",
                "Returns common Camera settings for a Camera component target (or a GameObject with a single Camera).",
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
                            ["description"] = "Unity instance id of a Camera component or a GameObject with a single Camera."
                        }
                    }
                }),
            new McpToolDefinition(
                "camera.setSettings",
                "Mutates common Camera settings using direct Unity Camera APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["orthographic"] = new JsonObject { ["type"] = "boolean" },
                        ["fieldOfView"] = new JsonObject { ["type"] = "number", ["description"] = "Perspective FOV in degrees (0-179)." },
                        ["orthographicSize"] = new JsonObject { ["type"] = "number", ["description"] = "Orthographic half-size (>0)." },
                        ["nearClipPlane"] = new JsonObject { ["type"] = "number", ["description"] = "Near clip plane (>0)." },
                        ["farClipPlane"] = new JsonObject { ["type"] = "number", ["description"] = "Far clip plane (> nearClipPlane)." },
                        ["clearFlags"] = EnumLikeSchema("Camera clear flags as enum name or integer value."),
                        ["backgroundColor"] = ColorSchema("RGBA color array [r,g,b,a]."),
                        ["depth"] = new JsonObject { ["type"] = "number" }
                    }
                }),
            new McpToolDefinition(
                "light.getSettings",
                "Returns common Light settings for a Light component target (or a GameObject with a single Light).",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" }
                    }
                }),
            new McpToolDefinition(
                "light.setSettings",
                "Mutates common Light settings using direct Unity Light APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["type"] = EnumLikeSchema("Light type as enum name or integer value."),
                        ["color"] = ColorSchema("RGBA color array [r,g,b,a]."),
                        ["intensity"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["range"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["spotAngle"] = new JsonObject { ["type"] = "number", ["description"] = "Spot angle in degrees (only valid for Spot lights)." },
                        ["shadows"] = EnumLikeSchema("Light shadows mode as enum name or integer value.")
                    }
                }),
            new McpToolDefinition(
                "rigidbody.getSettings",
                "Returns common Rigidbody (3D) settings for a Rigidbody component target (or a GameObject with a single Rigidbody).",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" }
                    }
                }),
            new McpToolDefinition(
                "rigidbody.setSettings",
                "Mutates common Rigidbody (3D) settings using direct Unity Rigidbody APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["mass"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["useGravity"] = new JsonObject { ["type"] = "boolean" },
                        ["isKinematic"] = new JsonObject { ["type"] = "boolean" },
                        ["detectCollisions"] = new JsonObject { ["type"] = "boolean" },
                        ["constraints"] = EnumLikeSchema("RigidbodyConstraints enum name or integer flags value."),
                        ["interpolation"] = EnumLikeSchema("RigidbodyInterpolation enum name or integer value."),
                        ["collisionDetectionMode"] = EnumLikeSchema("CollisionDetectionMode enum name or integer value.")
                    }
                }),
            new McpToolDefinition(
                "collider.getSettings",
                "Returns common Collider settings for a Collider component target (or a GameObject with a single Collider).",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" }
                    }
                }),
            new McpToolDefinition(
                "collider.setSettings",
                "Mutates common Collider settings (with BoxCollider-specific fields in MVP).",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["isTrigger"] = new JsonObject { ["type"] = "boolean" },
                        ["contactOffset"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["center"] = Vector3Schema("BoxCollider center [x,y,z] (BoxCollider only in MVP)."),
                        ["size"] = Vector3Schema("BoxCollider size [x,y,z] (BoxCollider only in MVP).")
                    }
                }),
            new McpToolDefinition(
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
                }),
            new McpToolDefinition(
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
                }),
            new McpToolDefinition(
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
                }),
            new McpToolDefinition(
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
                }),
            new McpToolDefinition(
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
                }),
            new McpToolDefinition(
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
                }),
            new McpToolDefinition(
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
                }),
            new McpToolDefinition(
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
                }),
            new McpToolDefinition(
                "scene.frameSelection",
                "Best-effort frames the current Unity Editor selection in the Scene view.",
                EmptyObjectSchema()),
            new McpToolDefinition(
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
                }),
            new McpToolDefinition(
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
                }),
            new McpToolDefinition(
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
                }),
            new McpToolDefinition(
                "assets.find",
                "Searches Unity assets using AssetDatabase.FindAssets(query).",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("query"),
                    ["properties"] = new JsonObject
                    {
                        ["query"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity AssetDatabase.FindAssets query string."
                        },
                        ["maxResults"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Optional cap for returned results (1-500).",
                            ["minimum"] = 1,
                            ["maximum"] = 500
                        },
                        ["searchInFolders"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["description"] = "Optional list of Unity folders under Assets/ used to scope the search.",
                            ["items"] = new JsonObject
                            {
                                ["type"] = "string"
                            }
                        },
                        ["types"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["description"] = "Optional asset type filters appended as t:<type> tokens.",
                            ["items"] = new JsonObject
                            {
                                ["type"] = "string"
                            }
                        },
                        ["labels"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["description"] = "Optional asset label filters appended as l:<label> tokens.",
                            ["items"] = new JsonObject
                            {
                                ["type"] = "string"
                            }
                        }
                    }
                }),
            new McpToolDefinition(
                "assets.import",
                "Imports an existing asset inside the Unity project's Assets folder.",
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
                            ["description"] = "Project-relative asset path under Assets/."
                        }
                    }
                }),
            new McpToolDefinition(
                "assets.ping",
                "Pings/highlights an existing asset in the Unity Project window.",
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
                            ["description"] = "Project-relative asset path under Assets/."
                        }
                    }
                }),
            new McpToolDefinition(
                "assets.reveal",
                "Reveals an existing asset in the Unity Project window (focuses Project window and pings).",
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
                            ["description"] = "Project-relative asset path under Assets/."
                        }
                    }
                })
        };

        Tools = tools;
        _byName = tools.ToDictionary(tool => tool.Name, StringComparer.Ordinal);
    }

    public IReadOnlyList<McpToolDefinition> Tools { get; }

    public bool TryGet(string name, out McpToolDefinition definition)
    {
        return _byName.TryGetValue(name, out definition!);
    }

    private static JsonObject EmptyObjectSchema()
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false
        };
    }

    private static JsonObject Vector3Schema(string description)
    {
        return new JsonObject
        {
            ["type"] = "array",
            ["description"] = description,
            ["minItems"] = 3,
            ["maxItems"] = 3,
            ["items"] = new JsonObject
            {
                ["type"] = "number"
            }
        };
    }

    private static JsonObject ColorSchema(string description)
    {
        return new JsonObject
        {
            ["type"] = "array",
            ["description"] = description,
            ["minItems"] = 4,
            ["maxItems"] = 4,
            ["items"] = new JsonObject
            {
                ["type"] = "number"
            }
        };
    }

    private static JsonObject EnumLikeSchema(string description)
    {
        return new JsonObject
        {
            ["description"] = description,
            ["oneOf"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "string"
                },
                new JsonObject
                {
                    ["type"] = "integer"
                }
            }
        };
    }
}

public sealed record McpToolDefinition(string Name, string Description, JsonObject InputSchema);

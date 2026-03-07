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
                        ["drag"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["angularDrag"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["useGravity"] = new JsonObject { ["type"] = "boolean" },
                        ["isKinematic"] = new JsonObject { ["type"] = "boolean" },
                        ["detectCollisions"] = new JsonObject { ["type"] = "boolean" },
                        ["constraints"] = EnumLikeSchema("RigidbodyConstraints enum name or integer flags value."),
                        ["interpolation"] = EnumLikeSchema("RigidbodyInterpolation enum name or integer value."),
                        ["collisionDetectionMode"] = EnumLikeSchema("CollisionDetectionMode enum name or integer value.")
                    }
                }),
            new McpToolDefinition(
                "rigidbody2D.getSettings",
                "Returns common Rigidbody2D settings for a Rigidbody2D component target (or a GameObject with a single Rigidbody2D).",
                InstanceIdOnlySchema("Unity instance id of a Rigidbody2D component or a GameObject with a single Rigidbody2D.")),
            new McpToolDefinition(
                "rigidbody2D.setSettings",
                "Mutates common Rigidbody2D settings using direct Unity Rigidbody2D APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["bodyType"] = EnumLikeSchema("RigidbodyType2D enum name or integer value."),
                        ["simulated"] = new JsonObject { ["type"] = "boolean" },
                        ["useAutoMass"] = new JsonObject { ["type"] = "boolean" },
                        ["mass"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["gravityScale"] = new JsonObject { ["type"] = "number" },
                        ["constraints"] = EnumLikeSchema("RigidbodyConstraints2D enum name or integer flags value."),
                        ["interpolation"] = EnumLikeSchema("RigidbodyInterpolation2D enum name or integer value."),
                        ["collisionDetectionMode"] = EnumLikeSchema("CollisionDetectionMode2D enum name or integer value."),
                        ["sleepMode"] = EnumLikeSchema("RigidbodySleepMode2D enum name or integer value.")
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
                "collider2D.getSettings",
                "Returns common Collider2D settings for a Collider2D component target (or a GameObject with a single Collider2D).",
                InstanceIdOnlySchema("Unity instance id of a Collider2D component or a GameObject with a single Collider2D.")),
            new McpToolDefinition(
                "collider2D.setSettings",
                "Mutates common Collider2D settings using direct Unity Collider2D APIs.",
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
                        ["usedByEffector"] = new JsonObject { ["type"] = "boolean" },
                        ["offset"] = Vector2Schema("Collider2D offset [x,y]."),
                        ["density"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "boxCollider.getSettings",
                "Returns BoxCollider settings for a BoxCollider target (or a GameObject with a single BoxCollider).",
                InstanceIdOnlySchema("Unity instance id of a BoxCollider component or a GameObject with a single BoxCollider.")),
            new McpToolDefinition(
                "boxCollider.setSettings",
                "Mutates BoxCollider settings (includes base Collider fields and BoxCollider center/size).",
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
                        ["center"] = Vector3Schema("BoxCollider center [x,y,z]."),
                        ["size"] = Vector3Schema("BoxCollider size [x,y,z].")
                    }
                }),
            new McpToolDefinition(
                "boxCollider2D.getSettings",
                "Returns BoxCollider2D settings for a BoxCollider2D target (or a GameObject with a single BoxCollider2D).",
                InstanceIdOnlySchema("Unity instance id of a BoxCollider2D component or a GameObject with a single BoxCollider2D.")),
            new McpToolDefinition(
                "boxCollider2D.setSettings",
                "Mutates BoxCollider2D settings (includes base Collider2D fields and BoxCollider2D size/edgeRadius).",
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
                        ["usedByEffector"] = new JsonObject { ["type"] = "boolean" },
                        ["offset"] = Vector2Schema("BoxCollider2D offset [x,y]."),
                        ["density"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["size"] = Vector2Schema("BoxCollider2D size [x,y]."),
                        ["edgeRadius"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "sphereCollider.getSettings",
                "Returns SphereCollider settings for a SphereCollider target (or a GameObject with a single SphereCollider).",
                InstanceIdOnlySchema("Unity instance id of a SphereCollider component or a GameObject with a single SphereCollider.")),
            new McpToolDefinition(
                "sphereCollider.setSettings",
                "Mutates SphereCollider settings (includes base Collider fields and SphereCollider center/radius).",
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
                        ["center"] = Vector3Schema("SphereCollider center [x,y,z]."),
                        ["radius"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "circleCollider2D.getSettings",
                "Returns CircleCollider2D settings for a CircleCollider2D target (or a GameObject with a single CircleCollider2D).",
                InstanceIdOnlySchema("Unity instance id of a CircleCollider2D component or a GameObject with a single CircleCollider2D.")),
            new McpToolDefinition(
                "circleCollider2D.setSettings",
                "Mutates CircleCollider2D settings (includes base Collider2D fields and CircleCollider2D radius).",
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
                        ["usedByEffector"] = new JsonObject { ["type"] = "boolean" },
                        ["offset"] = Vector2Schema("CircleCollider2D offset [x,y]."),
                        ["density"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["radius"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "capsuleCollider.getSettings",
                "Returns CapsuleCollider settings for a CapsuleCollider target (or a GameObject with a single CapsuleCollider).",
                InstanceIdOnlySchema("Unity instance id of a CapsuleCollider component or a GameObject with a single CapsuleCollider.")),
            new McpToolDefinition(
                "capsuleCollider.setSettings",
                "Mutates CapsuleCollider settings (includes base Collider fields and CapsuleCollider center/radius/height/direction).",
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
                        ["center"] = Vector3Schema("CapsuleCollider center [x,y,z]."),
                        ["radius"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["height"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["direction"] = EnumLikeSchema("Capsule direction as enum name (X/Y/Z or 0/1/2) or integer value.")
                    }
                }),
            new McpToolDefinition(
                "capsuleCollider2D.getSettings",
                "Returns CapsuleCollider2D settings for a CapsuleCollider2D target (or a GameObject with a single CapsuleCollider2D).",
                InstanceIdOnlySchema("Unity instance id of a CapsuleCollider2D component or a GameObject with a single CapsuleCollider2D.")),
            new McpToolDefinition(
                "capsuleCollider2D.setSettings",
                "Mutates CapsuleCollider2D settings (includes base Collider2D fields and CapsuleCollider2D size/direction).",
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
                        ["usedByEffector"] = new JsonObject { ["type"] = "boolean" },
                        ["offset"] = Vector2Schema("CapsuleCollider2D offset [x,y]."),
                        ["density"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["size"] = Vector2Schema("CapsuleCollider2D size [x,y]."),
                        ["direction"] = EnumLikeSchema("CapsuleDirection2D enum name or integer value.")
                    }
                }),
            new McpToolDefinition(
                "meshCollider.getSettings",
                "Returns MeshCollider settings for a MeshCollider target (or a GameObject with a single MeshCollider).",
                InstanceIdOnlySchema("Unity instance id of a MeshCollider component or a GameObject with a single MeshCollider.")),
            new McpToolDefinition(
                "meshCollider.setSettings",
                "Mutates a safe subset of MeshCollider settings (no sharedMesh assignment in MVP).",
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
                        ["convex"] = new JsonObject { ["type"] = "boolean" },
                        ["cookingOptions"] = EnumLikeSchema("MeshColliderCookingOptions enum name or integer flags value.")
                    }
                }),
            new McpToolDefinition(
                "polygonCollider2D.getSettings",
                "Returns PolygonCollider2D settings for a PolygonCollider2D target (or a GameObject with a single PolygonCollider2D).",
                InstanceIdOnlySchema("Unity instance id of a PolygonCollider2D component or a GameObject with a single PolygonCollider2D.")),
            new McpToolDefinition(
                "polygonCollider2D.setSettings",
                "Mutates a safe subset of PolygonCollider2D settings (base Collider2D fields only in MVP).",
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
                        ["usedByEffector"] = new JsonObject { ["type"] = "boolean" },
                        ["offset"] = Vector2Schema("PolygonCollider2D offset [x,y]."),
                        ["density"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "edgeCollider2D.getSettings",
                "Returns EdgeCollider2D settings for an EdgeCollider2D target (or a GameObject with a single EdgeCollider2D).",
                InstanceIdOnlySchema("Unity instance id of an EdgeCollider2D component or a GameObject with a single EdgeCollider2D.")),
            new McpToolDefinition(
                "edgeCollider2D.setSettings",
                "Mutates a safe subset of EdgeCollider2D settings (base Collider2D fields plus edgeRadius).",
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
                        ["usedByEffector"] = new JsonObject { ["type"] = "boolean" },
                        ["offset"] = Vector2Schema("EdgeCollider2D offset [x,y]."),
                        ["density"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["edgeRadius"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "compositeCollider2D.getSettings",
                "Returns CompositeCollider2D settings for a CompositeCollider2D target (or a GameObject with a single CompositeCollider2D).",
                InstanceIdOnlySchema("Unity instance id of a CompositeCollider2D component or a GameObject with a single CompositeCollider2D.")),
            new McpToolDefinition(
                "compositeCollider2D.setSettings",
                "Mutates a safe subset of CompositeCollider2D settings (base Collider2D fields plus geometry/generation types).",
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
                        ["usedByEffector"] = new JsonObject { ["type"] = "boolean" },
                        ["offset"] = Vector2Schema("CompositeCollider2D offset [x,y]."),
                        ["density"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["geometryType"] = EnumLikeSchema("CompositeCollider2D.GeometryType enum name or integer value."),
                        ["generationType"] = EnumLikeSchema("CompositeCollider2D.GenerationType enum name or integer value.")
                    }
                }),
            new McpToolDefinition(
                "hingeJoint2D.getSettings",
                "Returns HingeJoint2D settings for a HingeJoint2D target (or a GameObject with a single HingeJoint2D).",
                InstanceIdOnlySchema("Unity instance id of a HingeJoint2D component or a GameObject with a single HingeJoint2D.")),
            new McpToolDefinition(
                "hingeJoint2D.setSettings",
                "Mutates HingeJoint2D settings using direct Unity HingeJoint2D APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["autoConfigureConnectedAnchor"] = new JsonObject { ["type"] = "boolean" },
                        ["anchor"] = Vector2Schema("HingeJoint2D anchor [x,y]."),
                        ["connectedAnchor"] = Vector2Schema("HingeJoint2D connected anchor [x,y]."),
                        ["enableCollision"] = new JsonObject { ["type"] = "boolean" },
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["connectedBodyInstanceId"] = NullableIntegerSchema("HingeJoint2D connected Rigidbody2D instance id, GameObject with one Rigidbody2D, or null to clear."),
                        ["connectedAnchorMode"] = ConnectedAnchorModeSchema(),
                        ["useConnectedAnchor"] = new JsonObject { ["type"] = "boolean" },
                        ["useMotor"] = new JsonObject { ["type"] = "boolean" },
                        ["motorSpeed"] = new JsonObject { ["type"] = "number" },
                        ["maxMotorTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["useLimits"] = new JsonObject { ["type"] = "boolean" },
                        ["lowerAngle"] = new JsonObject { ["type"] = "number" },
                        ["upperAngle"] = new JsonObject { ["type"] = "number" }
                    }
                }),
            new McpToolDefinition(
                "springJoint2D.getSettings",
                "Returns SpringJoint2D settings for a SpringJoint2D target (or a GameObject with a single SpringJoint2D).",
                InstanceIdOnlySchema("Unity instance id of a SpringJoint2D component or a GameObject with a single SpringJoint2D.")),
            new McpToolDefinition(
                "springJoint2D.setSettings",
                "Mutates SpringJoint2D settings using direct Unity SpringJoint2D APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["autoConfigureConnectedAnchor"] = new JsonObject { ["type"] = "boolean" },
                        ["anchor"] = Vector2Schema("SpringJoint2D anchor [x,y]."),
                        ["connectedAnchor"] = Vector2Schema("SpringJoint2D connected anchor [x,y]."),
                        ["enableCollision"] = new JsonObject { ["type"] = "boolean" },
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["connectedBodyInstanceId"] = NullableIntegerSchema("SpringJoint2D connected Rigidbody2D instance id, GameObject with one Rigidbody2D, or null to clear."),
                        ["connectedAnchorMode"] = ConnectedAnchorModeSchema(),
                        ["autoConfigureDistance"] = new JsonObject { ["type"] = "boolean" },
                        ["distance"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["dampingRatio"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1 },
                        ["frequency"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "distanceJoint2D.getSettings",
                "Returns DistanceJoint2D settings for a DistanceJoint2D target (or a GameObject with a single DistanceJoint2D).",
                InstanceIdOnlySchema("Unity instance id of a DistanceJoint2D component or a GameObject with a single DistanceJoint2D.")),
            new McpToolDefinition(
                "distanceJoint2D.setSettings",
                "Mutates DistanceJoint2D settings using direct Unity DistanceJoint2D APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["autoConfigureConnectedAnchor"] = new JsonObject { ["type"] = "boolean" },
                        ["anchor"] = Vector2Schema("DistanceJoint2D anchor [x,y]."),
                        ["connectedAnchor"] = Vector2Schema("DistanceJoint2D connected anchor [x,y]."),
                        ["enableCollision"] = new JsonObject { ["type"] = "boolean" },
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["connectedBodyInstanceId"] = NullableIntegerSchema("DistanceJoint2D connected Rigidbody2D instance id, GameObject with one Rigidbody2D, or null to clear."),
                        ["connectedAnchorMode"] = ConnectedAnchorModeSchema(),
                        ["autoConfigureDistance"] = new JsonObject { ["type"] = "boolean" },
                        ["distance"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["maxDistanceOnly"] = new JsonObject { ["type"] = "boolean" }
                    }
                }),
            new McpToolDefinition(
                "fixedJoint2D.getSettings",
                "Returns FixedJoint2D settings for a FixedJoint2D target (or a GameObject with a single FixedJoint2D).",
                InstanceIdOnlySchema("Unity instance id of a FixedJoint2D component or a GameObject with a single FixedJoint2D.")),
            new McpToolDefinition(
                "fixedJoint2D.setSettings",
                "Mutates FixedJoint2D settings using direct Unity FixedJoint2D APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["autoConfigureConnectedAnchor"] = new JsonObject { ["type"] = "boolean" },
                        ["anchor"] = Vector2Schema("FixedJoint2D anchor [x,y]."),
                        ["connectedAnchor"] = Vector2Schema("FixedJoint2D connected anchor [x,y]."),
                        ["enableCollision"] = new JsonObject { ["type"] = "boolean" },
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["connectedBodyInstanceId"] = NullableIntegerSchema("FixedJoint2D connected Rigidbody2D instance id, GameObject with one Rigidbody2D, or null to clear."),
                        ["connectedAnchorMode"] = ConnectedAnchorModeSchema(),
                        ["dampingRatio"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1 },
                        ["frequency"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "sliderJoint2D.getSettings",
                "Returns SliderJoint2D settings for a SliderJoint2D target (or a GameObject with a single SliderJoint2D).",
                InstanceIdOnlySchema("Unity instance id of a SliderJoint2D component or a GameObject with a single SliderJoint2D.")),
            new McpToolDefinition(
                "sliderJoint2D.setSettings",
                "Mutates SliderJoint2D settings using direct Unity SliderJoint2D APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["autoConfigureConnectedAnchor"] = new JsonObject { ["type"] = "boolean" },
                        ["anchor"] = Vector2Schema("SliderJoint2D anchor [x,y]."),
                        ["connectedAnchor"] = Vector2Schema("SliderJoint2D connected anchor [x,y]."),
                        ["enableCollision"] = new JsonObject { ["type"] = "boolean" },
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["connectedBodyInstanceId"] = NullableIntegerSchema("SliderJoint2D connected Rigidbody2D instance id, GameObject with one Rigidbody2D, or null to clear."),
                        ["connectedAnchorMode"] = ConnectedAnchorModeSchema(),
                        ["autoConfigureAngle"] = new JsonObject { ["type"] = "boolean" },
                        ["angle"] = new JsonObject { ["type"] = "number" },
                        ["useMotor"] = new JsonObject { ["type"] = "boolean" },
                        ["motorSpeed"] = new JsonObject { ["type"] = "number" },
                        ["maxMotorTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["useLimits"] = new JsonObject { ["type"] = "boolean" },
                        ["lowerTranslation"] = new JsonObject { ["type"] = "number" },
                        ["upperTranslation"] = new JsonObject { ["type"] = "number" }
                    }
                }),
            new McpToolDefinition(
                "wheelJoint2D.getSettings",
                "Returns WheelJoint2D settings for a WheelJoint2D target (or a GameObject with a single WheelJoint2D).",
                InstanceIdOnlySchema("Unity instance id of a WheelJoint2D component or a GameObject with a single WheelJoint2D.")),
            new McpToolDefinition(
                "wheelJoint2D.setSettings",
                "Mutates WheelJoint2D settings using direct Unity WheelJoint2D APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["autoConfigureConnectedAnchor"] = new JsonObject { ["type"] = "boolean" },
                        ["anchor"] = Vector2Schema("WheelJoint2D anchor [x,y]."),
                        ["connectedAnchor"] = Vector2Schema("WheelJoint2D connected anchor [x,y]."),
                        ["enableCollision"] = new JsonObject { ["type"] = "boolean" },
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["connectedBodyInstanceId"] = NullableIntegerSchema("WheelJoint2D connected Rigidbody2D instance id, GameObject with one Rigidbody2D, or null to clear."),
                        ["connectedAnchorMode"] = ConnectedAnchorModeSchema(),
                        ["useMotor"] = new JsonObject { ["type"] = "boolean" },
                        ["motorSpeed"] = new JsonObject { ["type"] = "number" },
                        ["maxMotorTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["suspensionDampingRatio"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1 },
                        ["suspensionFrequency"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["suspensionAngle"] = new JsonObject { ["type"] = "number" }
                    }
                }),
            new McpToolDefinition(
                "targetJoint2D.getSettings",
                "Returns TargetJoint2D settings for a TargetJoint2D target (or a GameObject with a single TargetJoint2D).",
                InstanceIdOnlySchema("Unity instance id of a TargetJoint2D component or a GameObject with a single TargetJoint2D.")),
            new McpToolDefinition(
                "targetJoint2D.setSettings",
                "Mutates TargetJoint2D settings using direct Unity TargetJoint2D APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["anchor"] = Vector2Schema("TargetJoint2D anchor [x,y]."),
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["autoConfigureTarget"] = new JsonObject { ["type"] = "boolean" },
                        ["target"] = Vector2Schema("TargetJoint2D world target [x,y]."),
                        ["maxForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["dampingRatio"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1 },
                        ["frequency"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "hingeJoint.getSettings",
                "Returns HingeJoint settings for a HingeJoint target (or a GameObject with a single HingeJoint).",
                InstanceIdOnlySchema("Unity instance id of a HingeJoint component or a GameObject with a single HingeJoint.")),
            new McpToolDefinition(
                "hingeJoint.setSettings",
                "Mutates HingeJoint settings using direct Unity HingeJoint APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["autoConfigureConnectedAnchor"] = new JsonObject { ["type"] = "boolean" },
                        ["anchor"] = Vector3Schema("HingeJoint anchor [x,y,z]."),
                        ["connectedAnchor"] = Vector3Schema("HingeJoint connected anchor [x,y,z]."),
                        ["axis"] = Vector3Schema("HingeJoint axis [x,y,z]."),
                        ["enableCollision"] = new JsonObject { ["type"] = "boolean" },
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["connectedBodyInstanceId"] = NullableIntegerSchema("HingeJoint connected Rigidbody instance id, GameObject with one Rigidbody, or null to clear."),
                        ["connectedAnchorMode"] = ConnectedAnchorModeSchema(),
                        ["useSpring"] = new JsonObject { ["type"] = "boolean" },
                        ["spring"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["damper"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["targetPosition"] = new JsonObject { ["type"] = "number" },
                        ["useMotor"] = new JsonObject { ["type"] = "boolean" },
                        ["motorTargetVelocity"] = new JsonObject { ["type"] = "number" },
                        ["motorForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["motorFreeSpin"] = new JsonObject { ["type"] = "boolean" },
                        ["useLimits"] = new JsonObject { ["type"] = "boolean" },
                        ["minLimit"] = new JsonObject { ["type"] = "number" },
                        ["maxLimit"] = new JsonObject { ["type"] = "number" }
                    }
                }),
            new McpToolDefinition(
                "springJoint.getSettings",
                "Returns SpringJoint settings for a SpringJoint target (or a GameObject with a single SpringJoint).",
                InstanceIdOnlySchema("Unity instance id of a SpringJoint component or a GameObject with a single SpringJoint.")),
            new McpToolDefinition(
                "springJoint.setSettings",
                "Mutates SpringJoint settings using direct Unity SpringJoint APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["autoConfigureConnectedAnchor"] = new JsonObject { ["type"] = "boolean" },
                        ["anchor"] = Vector3Schema("SpringJoint anchor [x,y,z]."),
                        ["connectedAnchor"] = Vector3Schema("SpringJoint connected anchor [x,y,z]."),
                        ["axis"] = Vector3Schema("SpringJoint axis [x,y,z]."),
                        ["enableCollision"] = new JsonObject { ["type"] = "boolean" },
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["connectedBodyInstanceId"] = NullableIntegerSchema("SpringJoint connected Rigidbody instance id, GameObject with one Rigidbody, or null to clear."),
                        ["connectedAnchorMode"] = ConnectedAnchorModeSchema(),
                        ["spring"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["damper"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["minDistance"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["maxDistance"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["tolerance"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "fixedJoint.getSettings",
                "Returns FixedJoint settings for a FixedJoint target (or a GameObject with a single FixedJoint).",
                InstanceIdOnlySchema("Unity instance id of a FixedJoint component or a GameObject with a single FixedJoint.")),
            new McpToolDefinition(
                "fixedJoint.setSettings",
                "Mutates FixedJoint settings using direct Unity FixedJoint APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["autoConfigureConnectedAnchor"] = new JsonObject { ["type"] = "boolean" },
                        ["anchor"] = Vector3Schema("FixedJoint anchor [x,y,z]."),
                        ["connectedAnchor"] = Vector3Schema("FixedJoint connected anchor [x,y,z]."),
                        ["axis"] = Vector3Schema("FixedJoint axis [x,y,z]."),
                        ["enableCollision"] = new JsonObject { ["type"] = "boolean" },
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["connectedBodyInstanceId"] = NullableIntegerSchema("FixedJoint connected Rigidbody instance id, GameObject with one Rigidbody, or null to clear."),
                        ["connectedAnchorMode"] = ConnectedAnchorModeSchema()
                    }
                }),
            new McpToolDefinition(
                "characterJoint.getSettings",
                "Returns CharacterJoint settings for a CharacterJoint target (or a GameObject with a single CharacterJoint).",
                InstanceIdOnlySchema("Unity instance id of a CharacterJoint component or a GameObject with a single CharacterJoint.")),
            new McpToolDefinition(
                "characterJoint.setSettings",
                "Mutates CharacterJoint settings using direct Unity CharacterJoint APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["autoConfigureConnectedAnchor"] = new JsonObject { ["type"] = "boolean" },
                        ["anchor"] = Vector3Schema("CharacterJoint anchor [x,y,z]."),
                        ["connectedAnchor"] = Vector3Schema("CharacterJoint connected anchor [x,y,z]."),
                        ["axis"] = Vector3Schema("CharacterJoint axis [x,y,z]."),
                        ["enableCollision"] = new JsonObject { ["type"] = "boolean" },
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["connectedBodyInstanceId"] = NullableIntegerSchema("CharacterJoint connected Rigidbody instance id, GameObject with one Rigidbody, or null to clear."),
                        ["connectedAnchorMode"] = ConnectedAnchorModeSchema(),
                        ["swingAxis"] = Vector3Schema("CharacterJoint swing axis [x,y,z]."),
                        ["enableProjection"] = new JsonObject { ["type"] = "boolean" },
                        ["enablePreprocessing"] = new JsonObject { ["type"] = "boolean" },
                        ["twistLimitSpring"] = SoftJointLimitSpringSchema("CharacterJoint twist limit spring."),
                        ["swingLimitSpring"] = SoftJointLimitSpringSchema("CharacterJoint swing limit spring."),
                        ["lowTwistLimit"] = SoftJointLimitSchema("CharacterJoint low twist limit."),
                        ["highTwistLimit"] = SoftJointLimitSchema("CharacterJoint high twist limit."),
                        ["swing1Limit"] = SoftJointLimitSchema("CharacterJoint swing1 limit."),
                        ["swing2Limit"] = SoftJointLimitSchema("CharacterJoint swing2 limit.")
                    }
                }),
            new McpToolDefinition(
                "configurableJoint.getSettings",
                "Returns ConfigurableJoint settings for a ConfigurableJoint target (or a GameObject with a single ConfigurableJoint).",
                InstanceIdOnlySchema("Unity instance id of a ConfigurableJoint component or a GameObject with a single ConfigurableJoint.")),
            new McpToolDefinition(
                "configurableJoint.setSettings",
                "Mutates a practical ConfigurableJoint settings subset using direct Unity ConfigurableJoint APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["autoConfigureConnectedAnchor"] = new JsonObject { ["type"] = "boolean" },
                        ["anchor"] = Vector3Schema("ConfigurableJoint anchor [x,y,z]."),
                        ["connectedAnchor"] = Vector3Schema("ConfigurableJoint connected anchor [x,y,z]."),
                        ["axis"] = Vector3Schema("ConfigurableJoint axis [x,y,z]."),
                        ["secondaryAxis"] = Vector3Schema("ConfigurableJoint secondary axis [x,y,z]."),
                        ["enableCollision"] = new JsonObject { ["type"] = "boolean" },
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["connectedBodyInstanceId"] = NullableIntegerSchema("ConfigurableJoint connected Rigidbody instance id, GameObject with one Rigidbody, or null to clear."),
                        ["connectedAnchorMode"] = ConnectedAnchorModeSchema(),
                        ["configuredInWorldSpace"] = new JsonObject { ["type"] = "boolean" },
                        ["swapBodies"] = new JsonObject { ["type"] = "boolean" },
                        ["xMotion"] = EnumLikeSchema("ConfigurableJointMotion enum name or integer value."),
                        ["yMotion"] = EnumLikeSchema("ConfigurableJointMotion enum name or integer value."),
                        ["zMotion"] = EnumLikeSchema("ConfigurableJointMotion enum name or integer value."),
                        ["angularXMotion"] = EnumLikeSchema("ConfigurableJointMotion enum name or integer value."),
                        ["angularYMotion"] = EnumLikeSchema("ConfigurableJointMotion enum name or integer value."),
                        ["angularZMotion"] = EnumLikeSchema("ConfigurableJointMotion enum name or integer value."),
                        ["linearLimit"] = SoftJointLimitSchema("ConfigurableJoint linear limit."),
                        ["lowAngularXLimit"] = SoftJointLimitSchema("ConfigurableJoint low angular X limit."),
                        ["highAngularXLimit"] = SoftJointLimitSchema("ConfigurableJoint high angular X limit."),
                        ["angularYLimit"] = SoftJointLimitSchema("ConfigurableJoint angular Y limit."),
                        ["angularZLimit"] = SoftJointLimitSchema("ConfigurableJoint angular Z limit."),
                        ["targetPosition"] = Vector3Schema("ConfigurableJoint target position [x,y,z]."),
                        ["targetVelocity"] = Vector3Schema("ConfigurableJoint target velocity [x,y,z]."),
                        ["targetAngularVelocity"] = Vector3Schema("ConfigurableJoint target angular velocity [x,y,z]."),
                        ["rotationDriveMode"] = EnumLikeSchema("RotationDriveMode enum name or integer value."),
                        ["xDrive"] = JointDriveSchema("ConfigurableJoint X drive."),
                        ["yDrive"] = JointDriveSchema("ConfigurableJoint Y drive."),
                        ["zDrive"] = JointDriveSchema("ConfigurableJoint Z drive."),
                        ["angularXDrive"] = JointDriveSchema("ConfigurableJoint angular X drive."),
                        ["angularYZDrive"] = JointDriveSchema("ConfigurableJoint angular YZ drive."),
                        ["slerpDrive"] = JointDriveSchema("ConfigurableJoint slerp drive."),
                        ["projectionMode"] = EnumLikeSchema("JointProjectionMode enum name or integer value."),
                        ["projectionDistance"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["projectionAngle"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 }
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
                }),
            new McpToolDefinition(
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
                }),
            new McpToolDefinition(
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
                }),
            new McpToolDefinition(
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
                }),
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
                }),
            new McpToolDefinition(
                "prefab.getSource",
                "Resolves prefab source metadata for a prefab instance in a scene.",
                InstanceIdOnlySchema("Unity instance id of a prefab instance object or component in a scene.")),
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
                        ["scope"] = PrefabScopeSchema(),
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
                        ["scope"] = PrefabScopeSchema(),
                        ["componentInstanceId"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Component instance id to target when scope is component. If omitted, a component instanceId input may be reused."
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
                }),

            // ── Editor utility ────────────────────────────────────────────
            new McpToolDefinition(
                "editor.clearConsole",
                "Clears all Unity Editor console log entries.",
                EmptyObjectSchema()),
            new McpToolDefinition(
                "editor.pausePlayMode",
                "Pauses or unpauses Unity Editor play mode.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("paused"),
                    ["properties"] = new JsonObject
                    {
                        ["paused"] = new JsonObject { ["type"] = "boolean", ["description"] = "True to pause play mode, false to unpause." }
                    }
                }),
            new McpToolDefinition(
                "editor.undo",
                "Performs an undo operation in the Unity Editor.",
                EmptyObjectSchema()),
            new McpToolDefinition(
                "editor.redo",
                "Performs a redo operation in the Unity Editor.",
                EmptyObjectSchema()),
            new McpToolDefinition(
                "editor.getTags",
                "Returns all tags defined in the Unity project.",
                EmptyObjectSchema()),
            new McpToolDefinition(
                "editor.getLayers",
                "Returns all layers defined in the Unity project.",
                EmptyObjectSchema()),

            // ── Scene object tag / layer ──────────────────────────────────
            new McpToolDefinition(
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
                }),
            new McpToolDefinition(
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
                }),

            // ── Scene management ──────────────────────────────────────────
            new McpToolDefinition(
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
                }),
            new McpToolDefinition(
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
                }),
            new McpToolDefinition(
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
                }),
            new McpToolDefinition(
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
                }),
            new McpToolDefinition(
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
                }),

            // ── Asset creation / management ───────────────────────────────
            new McpToolDefinition(
                "assets.createFolder",
                "Creates a folder inside the Unity project Assets folder.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("parentFolder", "folderName"),
                    ["properties"] = new JsonObject
                    {
                        ["parentFolder"] = new JsonObject { ["type"] = "string", ["description"] = "Parent folder path, e.g. 'Assets/Scripts'." },
                        ["folderName"] = new JsonObject { ["type"] = "string", ["description"] = "Name of the new folder to create." }
                    }
                }),
            new McpToolDefinition(
                "assets.createScript",
                "Creates a new C# MonoBehaviour script asset in the Unity project.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("assetPath"),
                    ["properties"] = new JsonObject
                    {
                        ["assetPath"] = new JsonObject { ["type"] = "string", ["description"] = "Project-relative path for the script, e.g. 'Assets/Scripts/MyScript.cs'." },
                        ["content"] = new JsonObject { ["type"] = "string", ["description"] = "Optional full file content. If omitted a default MonoBehaviour stub is generated." }
                    }
                }),
            new McpToolDefinition(
                "assets.createMaterial",
                "Creates a new Material asset in the Unity project.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("assetPath"),
                    ["properties"] = new JsonObject
                    {
                        ["assetPath"] = new JsonObject { ["type"] = "string", ["description"] = "Project-relative path for the material, e.g. 'Assets/Materials/MyMat.mat'." },
                        ["shaderName"] = new JsonObject { ["type"] = "string", ["description"] = "Shader name to use (default 'Standard')." }
                    }
                }),
            new McpToolDefinition(
                "assets.createPrefab",
                "Saves a scene GameObject as a new prefab asset.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId", "assetPath"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer", ["description"] = "Instance id of the scene GameObject to save as a prefab." },
                        ["assetPath"] = new JsonObject { ["type"] = "string", ["description"] = "Project-relative path for the prefab, e.g. 'Assets/Prefabs/MyPrefab.prefab'." }
                    }
                }),
            new McpToolDefinition(
                "assets.delete",
                "Deletes an asset from the Unity project.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("assetPath"),
                    ["properties"] = new JsonObject
                    {
                        ["assetPath"] = new JsonObject { ["type"] = "string", ["description"] = "Project-relative asset path to delete." }
                    }
                }),
            new McpToolDefinition(
                "assets.move",
                "Moves or renames an asset within the Unity project.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("sourcePath", "destinationPath"),
                    ["properties"] = new JsonObject
                    {
                        ["sourcePath"] = new JsonObject { ["type"] = "string", ["description"] = "Current project-relative asset path." },
                        ["destinationPath"] = new JsonObject { ["type"] = "string", ["description"] = "New project-relative asset path (including filename)." }
                    }
                }),

            // ── Animator ─────────────────────────────────────────────────
            new McpToolDefinition(
                "animator.getSettings",
                "Returns Animator component settings for the target.",
                InstanceIdOnlySchema("Unity instance id of an Animator component or a GameObject with a single Animator.")),
            new McpToolDefinition(
                "animator.setSettings",
                "Mutates Animator component settings.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["speed"] = new JsonObject { ["type"] = "number" },
                        ["applyRootMotion"] = new JsonObject { ["type"] = "boolean" },
                        ["updateMode"] = EnumLikeSchema("AnimatorUpdateMode enum name or integer value."),
                        ["cullingMode"] = EnumLikeSchema("AnimatorCullingMode enum name or integer value.")
                    }
                }),
            new McpToolDefinition(
                "animator.getParameters",
                "Returns the list of Animator controller parameters.",
                InstanceIdOnlySchema("Unity instance id of an Animator component or a GameObject with a single Animator.")),
            new McpToolDefinition(
                "animator.setParameter",
                "Sets an Animator parameter value by name.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId", "parameterName", "value"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["parameterName"] = new JsonObject { ["type"] = "string", ["description"] = "Animator parameter name." },
                        ["value"] = new JsonObject
                        {
                            ["description"] = "Parameter value. Use a boolean for Bool, integer for Int, number for Float. Trigger parameters ignore the value.",
                            ["oneOf"] = new JsonArray
                            {
                                new JsonObject { ["type"] = "boolean" },
                                new JsonObject { ["type"] = "integer" },
                                new JsonObject { ["type"] = "number" },
                                new JsonObject { ["type"] = "null" }
                            }
                        }
                    }
                }),

            // ── MeshRenderer ──────────────────────────────────────────────
            new McpToolDefinition(
                "meshRenderer.getSettings",
                "Returns MeshRenderer settings for the target.",
                InstanceIdOnlySchema("Unity instance id of a MeshRenderer component or a GameObject with a single MeshRenderer.")),
            new McpToolDefinition(
                "meshRenderer.setSettings",
                "Mutates MeshRenderer settings.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["shadowCastingMode"] = EnumLikeSchema("ShadowCastingMode enum name or integer value."),
                        ["receiveShadows"] = new JsonObject { ["type"] = "boolean" },
                        ["lightProbeUsage"] = EnumLikeSchema("LightProbeUsage enum name or integer value."),
                        ["reflectionProbeUsage"] = EnumLikeSchema("ReflectionProbeUsage enum name or integer value."),
                        ["motionVectorGenerationMode"] = EnumLikeSchema("MotionVectorGenerationMode enum name or integer value."),
                        ["staticShadowCaster"] = new JsonObject { ["type"] = "boolean" },
                        ["allowOcclusionWhenDynamic"] = new JsonObject { ["type"] = "boolean" }
                    }
                }),

            // ── AudioSource ───────────────────────────────────────────────
            new McpToolDefinition(
                "audioSource.getSettings",
                "Returns AudioSource component settings for the target.",
                InstanceIdOnlySchema("Unity instance id of an AudioSource component or a GameObject with a single AudioSource.")),
            new McpToolDefinition(
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
                }),

            // ── CharacterController ───────────────────────────────────────
            new McpToolDefinition(
                "characterController.getSettings",
                "Returns CharacterController component settings for the target.",
                InstanceIdOnlySchema("Unity instance id of a CharacterController component or a GameObject with a single CharacterController.")),
            new McpToolDefinition(
                "characterController.setSettings",
                "Mutates CharacterController component settings.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["height"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["radius"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["center"] = Vector3Schema("CharacterController center [x,y,z]."),
                        ["slopeLimit"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 90 },
                        ["stepOffset"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["skinWidth"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["minMoveDistance"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["enableOverlapRecovery"] = new JsonObject { ["type"] = "boolean" }
                    }
                }),

            // ── ParticleSystem ────────────────────────────────────────────
            new McpToolDefinition(
                "particleSystem.getSettings",
                "Returns ParticleSystem component settings for the target.",
                InstanceIdOnlySchema("Unity instance id of a ParticleSystem component or a GameObject with a single ParticleSystem.")),
            new McpToolDefinition(
                "particleSystem.setSettings",
                "Mutates ParticleSystem component settings.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["duration"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["loop"] = new JsonObject { ["type"] = "boolean" },
                        ["prewarm"] = new JsonObject { ["type"] = "boolean" },
                        ["startDelay"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["startLifetime"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["startSpeed"] = new JsonObject { ["type"] = "number" },
                        ["startSize"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["maxParticles"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0 },
                        ["playOnAwake"] = new JsonObject { ["type"] = "boolean" },
                        ["emissionRate"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "particleSystem.play",
                "Starts playing a ParticleSystem.",
                InstanceIdOnlySchema("Unity instance id of a ParticleSystem component or a GameObject with a single ParticleSystem.")),
            new McpToolDefinition(
                "particleSystem.stop",
                "Stops a ParticleSystem (stop emitting and clear).",
                InstanceIdOnlySchema("Unity instance id of a ParticleSystem component or a GameObject with a single ParticleSystem.")),

            // ── NavMeshAgent ──────────────────────────────────────────────
            new McpToolDefinition(
                "navMeshAgent.getSettings",
                "Returns NavMeshAgent component settings for the target.",
                InstanceIdOnlySchema("Unity instance id of a NavMeshAgent component or a GameObject with a single NavMeshAgent.")),
            new McpToolDefinition(
                "navMeshAgent.setSettings",
                "Mutates NavMeshAgent component settings.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["speed"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["angularSpeed"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["acceleration"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["stoppingDistance"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["radius"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["height"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["areaMask"] = new JsonObject { ["type"] = "integer" },
                        ["autoBraking"] = new JsonObject { ["type"] = "boolean" },
                        ["obstacleAvoidanceType"] = EnumLikeSchema("ObstacleAvoidanceType enum name or integer value."),
                        ["avoidancePriority"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 99 }
                    }
                }),

            // ── NavMeshObstacle ───────────────────────────────────────────
            new McpToolDefinition(
                "navMeshObstacle.getSettings",
                "Returns NavMeshObstacle component settings for the target.",
                InstanceIdOnlySchema("Unity instance id of a NavMeshObstacle component or a GameObject with a single NavMeshObstacle.")),
            new McpToolDefinition(
                "navMeshObstacle.setSettings",
                "Mutates NavMeshObstacle component settings.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["carving"] = new JsonObject { ["type"] = "boolean" },
                        ["carvingMoveThreshold"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["carvingTimeToStationary"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["shape"] = EnumLikeSchema("NavMeshObstacleShape enum name or integer value."),
                        ["center"] = Vector3Schema("NavMeshObstacle center [x,y,z]."),
                        ["size"] = Vector3Schema("NavMeshObstacle size [x,y,z]."),
                        ["radius"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["height"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 }
                    }
                }),

            // ── RectTransform ─────────────────────────────────────────────
            new McpToolDefinition(
                "rectTransform.getSettings",
                "Returns RectTransform component settings for the target.",
                InstanceIdOnlySchema("Unity instance id of a RectTransform component or a GameObject with a single RectTransform.")),
            new McpToolDefinition(
                "rectTransform.setSettings",
                "Mutates RectTransform component settings.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["anchorMin"] = Vector2Schema("RectTransform anchorMin [x,y]."),
                        ["anchorMax"] = Vector2Schema("RectTransform anchorMax [x,y]."),
                        ["anchoredPosition"] = Vector2Schema("RectTransform anchoredPosition [x,y]."),
                        ["sizeDelta"] = Vector2Schema("RectTransform sizeDelta [x,y]."),
                        ["pivot"] = Vector2Schema("RectTransform pivot [x,y]."),
                        ["offsetMin"] = Vector2Schema("RectTransform offsetMin [x,y]."),
                        ["offsetMax"] = Vector2Schema("RectTransform offsetMax [x,y].")
                    }
                }),

            // ── Canvas ────────────────────────────────────────────────────
            new McpToolDefinition(
                "canvas.getSettings",
                "Returns Canvas component settings for the target.",
                InstanceIdOnlySchema("Unity instance id of a Canvas component or a GameObject with a single Canvas.")),
            new McpToolDefinition(
                "canvas.setSettings",
                "Mutates Canvas component settings.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["renderMode"] = EnumLikeSchema("RenderMode enum name or integer value."),
                        ["sortingOrder"] = new JsonObject { ["type"] = "integer" },
                        ["targetDisplay"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0 },
                        ["pixelPerfect"] = new JsonObject { ["type"] = "boolean" },
                        ["planeDistance"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["overrideSorting"] = new JsonObject { ["type"] = "boolean" }
                    }
                }),

            // ── SkinnedMeshRenderer ───────────────────────────────────────
            new McpToolDefinition(
                "skinnedMeshRenderer.getSettings",
                "Returns SkinnedMeshRenderer component settings for the target (includes MeshRenderer fields plus rootBone, quality, updateWhenOffscreen).",
                InstanceIdOnlySchema("Unity instance id of a SkinnedMeshRenderer component or a GameObject with a single SkinnedMeshRenderer.")),
            new McpToolDefinition(
                "skinnedMeshRenderer.setSettings",
                "Mutates SkinnedMeshRenderer component settings.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["shadowCastingMode"] = EnumLikeSchema("ShadowCastingMode enum name or integer value."),
                        ["receiveShadows"] = new JsonObject { ["type"] = "boolean" },
                        ["lightProbeUsage"] = EnumLikeSchema("LightProbeUsage enum name or integer value."),
                        ["reflectionProbeUsage"] = EnumLikeSchema("ReflectionProbeUsage enum name or integer value."),
                        ["motionVectorGenerationMode"] = EnumLikeSchema("MotionVectorGenerationMode enum name or integer value."),
                        ["staticShadowCaster"] = new JsonObject { ["type"] = "boolean" },
                        ["allowOcclusionWhenDynamic"] = new JsonObject { ["type"] = "boolean" },
                        ["quality"] = EnumLikeSchema("SkinQuality enum name or integer value."),
                        ["updateWhenOffscreen"] = new JsonObject { ["type"] = "boolean" }
                    }
                }),

            // ── ScriptableObject Creation ─────────────────────────────────
            new McpToolDefinition(
                "assets.createScriptableObject",
                "Creates a new ScriptableObject asset at the given path.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("assetPath", "typeName"),
                    ["properties"] = new JsonObject
                    {
                        ["assetPath"] = new JsonObject { ["type"] = "string", ["description"] = "Project-relative asset path (must start with Assets/ and end with .asset)." },
                        ["typeName"] = new JsonObject { ["type"] = "string", ["description"] = "Fully qualified or short name of a ScriptableObject subclass." }
                    }
                }),

            // ── Batch 3: NavMesh ──────────────────────────────────────────────
            new McpToolDefinition(
                "navMesh.bake",
                "Bakes the NavMesh for the active scene using NavMeshBuilder.BuildNavMesh().",
                EmptyObjectSchema()),

            // ── Batch 3: Terrain ──────────────────────────────────────────────
            new McpToolDefinition(
                "terrain.getSettings",
                "Returns terrain settings for the Terrain component identified by instanceId.",
                InstanceIdOnlySchema("Instance ID of a GameObject with a Terrain component.")),
            new McpToolDefinition(
                "terrain.setSettings",
                "Sets terrain settings for the Terrain component identified by instanceId.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer", ["description"] = "Instance ID of a GameObject with a Terrain component." },
                        ["heightmapResolution"] = new JsonObject { ["type"] = "integer", ["description"] = "Heightmap resolution (power of two + 1)." },
                        ["size"] = Vector3Schema("Terrain size as [x, y, z]."),
                        ["basemapDistance"] = new JsonObject { ["type"] = "number", ["description"] = "Distance at which terrain textures switch to basemap." },
                        ["drawHeightmap"] = new JsonObject { ["type"] = "boolean" },
                        ["drawInstanced"] = new JsonObject { ["type"] = "boolean" },
                        ["detailObjectDistance"] = new JsonObject { ["type"] = "number" },
                        ["treeBillboardDistance"] = new JsonObject { ["type"] = "number" },
                        ["shadowCastingMode"] = EnumLikeSchema("ShadowCastingMode enum name or integer value.")
                    }
                }),

            // ── Batch 3: Build Pipeline ───────────────────────────────────────
            new McpToolDefinition(
                "build.getSettings",
                "Returns current build settings: scenes in build, active build target, development build flag.",
                EmptyObjectSchema()),
            new McpToolDefinition(
                "build.setSettings",
                "Sets build pipeline settings.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new JsonObject
                    {
                        ["developmentBuild"] = new JsonObject { ["type"] = "boolean", ["description"] = "Enable or disable development build." },
                        ["outputPath"] = new JsonObject { ["type"] = "string", ["description"] = "Build output path." }
                    }
                }),
            new McpToolDefinition(
                "build.build",
                "Triggers BuildPipeline.BuildPlayer with current settings.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("outputPath"),
                    ["properties"] = new JsonObject
                    {
                        ["outputPath"] = new JsonObject { ["type"] = "string", ["description"] = "Output path for the build." }
                    }
                }),

            // ── Batch 3: Tags & Layers Management ────────────────────────────
            new McpToolDefinition(
                "editor.addTag",
                "Adds a new tag to the project via the TagManager.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("tag"),
                    ["properties"] = new JsonObject
                    {
                        ["tag"] = new JsonObject { ["type"] = "string", ["description"] = "Tag name to add." }
                    }
                }),
            new McpToolDefinition(
                "editor.removeTag",
                "Removes a tag from the project by name.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("tag"),
                    ["properties"] = new JsonObject
                    {
                        ["tag"] = new JsonObject { ["type"] = "string", ["description"] = "Tag name to remove." }
                    }
                }),
            new McpToolDefinition(
                "editor.addLayer",
                "Adds a new layer to the project (finds first empty slot in layers 8-31).",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("layer"),
                    ["properties"] = new JsonObject
                    {
                        ["layer"] = new JsonObject { ["type"] = "string", ["description"] = "Layer name to add." }
                    }
                }),
            new McpToolDefinition(
                "editor.removeLayer",
                "Removes a layer from the project by name (clears the slot).",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("layer"),
                    ["properties"] = new JsonObject
                    {
                        ["layer"] = new JsonObject { ["type"] = "string", ["description"] = "Layer name to remove." }
                    }
                }),

            // ── Batch 3: Selection Utilities ──────────────────────────────────
            new McpToolDefinition(
                "scene.getSelectionDetails",
                "Returns full component list and transform details for all currently selected objects.",
                EmptyObjectSchema()),
            new McpToolDefinition(
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
                }),

            // ── Batch 3: Undo History ─────────────────────────────────────────
            new McpToolDefinition(
                "editor.getUndoHistory",
                "Returns the current undo group name and group index.",
                EmptyObjectSchema()),

            // ── Batch 4: Camera Projection ────────────────────────────────────
            new McpToolDefinition(
                "camera.getProjection",
                "Returns camera projection settings: orthographic, orthographicSize, fieldOfView, nearClipPlane, farClipPlane, aspect.",
                InstanceIdOnlySchema("Instance ID of a Camera component or a GameObject with a Camera.")),
            new McpToolDefinition(
                "camera.setProjection",
                "Sets camera projection settings: orthographic, orthographicSize, fieldOfView, nearClipPlane, farClipPlane.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer", ["description"] = "Instance ID of a Camera component or a GameObject with a Camera." },
                        ["orthographic"] = new JsonObject { ["type"] = "boolean", ["description"] = "Enable orthographic projection." },
                        ["orthographicSize"] = new JsonObject { ["type"] = "number", ["description"] = "Orthographic camera half-size." },
                        ["fieldOfView"] = new JsonObject { ["type"] = "number", ["description"] = "Field of view in degrees (perspective mode)." },
                        ["nearClipPlane"] = new JsonObject { ["type"] = "number", ["description"] = "Near clipping plane distance." },
                        ["farClipPlane"] = new JsonObject { ["type"] = "number", ["description"] = "Far clipping plane distance." }
                    }
                }),

            // ── Batch 4: SpriteRenderer ───────────────────────────────────────
            new McpToolDefinition(
                "spriteRenderer.getSettings",
                "Returns SpriteRenderer settings: spriteName, color, flipX, flipY, sortingLayerName, sortingOrder, drawMode, maskInteraction.",
                InstanceIdOnlySchema("Instance ID of a SpriteRenderer component or a GameObject with a SpriteRenderer.")),
            new McpToolDefinition(
                "spriteRenderer.setSettings",
                "Sets SpriteRenderer settings: color, flipX, flipY, sortingLayerName, sortingOrder.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer", ["description"] = "Instance ID of a SpriteRenderer component or a GameObject with a SpriteRenderer." },
                        ["color"] = ColorSchema("Sprite color as [r, g, b, a]."),
                        ["flipX"] = new JsonObject { ["type"] = "boolean", ["description"] = "Flip the sprite horizontally." },
                        ["flipY"] = new JsonObject { ["type"] = "boolean", ["description"] = "Flip the sprite vertically." },
                        ["sortingLayerName"] = new JsonObject { ["type"] = "string", ["description"] = "Sorting layer name." },
                        ["sortingOrder"] = new JsonObject { ["type"] = "integer", ["description"] = "Order within sorting layer." }
                    }
                }),

            // ── Batch 4: LineRenderer ─────────────────────────────────────────
            new McpToolDefinition(
                "lineRenderer.getSettings",
                "Returns LineRenderer settings: positionCount, positions, loop, startWidth, endWidth, useWorldSpace, startColor, endColor.",
                InstanceIdOnlySchema("Instance ID of a LineRenderer component or a GameObject with a LineRenderer.")),
            new McpToolDefinition(
                "lineRenderer.setSettings",
                "Sets LineRenderer settings: loop, startWidth, endWidth, useWorldSpace, startColor, endColor.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer", ["description"] = "Instance ID of a LineRenderer component or a GameObject with a LineRenderer." },
                        ["loop"] = new JsonObject { ["type"] = "boolean", ["description"] = "Connect the first and last positions." },
                        ["startWidth"] = new JsonObject { ["type"] = "number", ["description"] = "Width at the start of the line." },
                        ["endWidth"] = new JsonObject { ["type"] = "number", ["description"] = "Width at the end of the line." },
                        ["useWorldSpace"] = new JsonObject { ["type"] = "boolean", ["description"] = "Use world space coordinates." },
                        ["startColor"] = ColorSchema("Line start color as [r, g, b, a]."),
                        ["endColor"] = ColorSchema("Line end color as [r, g, b, a].")
                    }
                }),

            // ── Batch 4: LODGroup ─────────────────────────────────────────────
            new McpToolDefinition(
                "lodGroup.getSettings",
                "Returns LODGroup settings: lodCount, fadeMode, animateCrossFading, size.",
                InstanceIdOnlySchema("Instance ID of a LODGroup component or a GameObject with a LODGroup.")),
            new McpToolDefinition(
                "lodGroup.setSettings",
                "Sets LODGroup settings: fadeMode, animateCrossFading.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer", ["description"] = "Instance ID of a LODGroup component or a GameObject with a LODGroup." },
                        ["fadeMode"] = EnumLikeSchema("LODFadeMode enum name or integer value."),
                        ["animateCrossFading"] = new JsonObject { ["type"] = "boolean", ["description"] = "Enable cross-fade animation." }
                    }
                }),

            // ── Batch 4: CanvasGroup ──────────────────────────────────────────
            new McpToolDefinition(
                "canvasGroup.getSettings",
                "Returns CanvasGroup settings: alpha, interactable, blocksRaycasts, ignoreParentGroups.",
                InstanceIdOnlySchema("Instance ID of a CanvasGroup component or a GameObject with a CanvasGroup.")),
            new McpToolDefinition(
                "canvasGroup.setSettings",
                "Sets CanvasGroup settings: alpha, interactable, blocksRaycasts, ignoreParentGroups.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer", ["description"] = "Instance ID of a CanvasGroup component or a GameObject with a CanvasGroup." },
                        ["alpha"] = new JsonObject { ["type"] = "number", ["description"] = "Group alpha (0-1).", ["minimum"] = 0, ["maximum"] = 1 },
                        ["interactable"] = new JsonObject { ["type"] = "boolean", ["description"] = "Is the group interactable." },
                        ["blocksRaycasts"] = new JsonObject { ["type"] = "boolean", ["description"] = "Does the group block raycasts." },
                        ["ignoreParentGroups"] = new JsonObject { ["type"] = "boolean", ["description"] = "Ignore parent CanvasGroup settings." }
                    }
                }),

            // ── Batch 4: Editor Recompile ─────────────────────────────────────
            new McpToolDefinition(
                "editor.recompileScripts",
                "Requests Unity to recompile all scripts via CompilationPipeline.RequestScriptCompilation().",
                EmptyObjectSchema()),

            // ── Batch 4: Scene Instantiate Prefab ─────────────────────────────
            new McpToolDefinition(
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
                }),

            // ── Batch 5: Physics Queries ──────────────────────────────────────
            new McpToolDefinition(
                "physics.raycast",
                "Performs a physics raycast in the scene and returns hit information.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("origin", "direction"),
                    ["properties"] = new JsonObject
                    {
                        ["origin"] = Vector3Schema("Ray origin [x,y,z]."),
                        ["direction"] = Vector3Schema("Ray direction [x,y,z]."),
                        ["maxDistance"] = new JsonObject { ["type"] = "number", ["description"] = "Maximum raycast distance (default Infinity)." },
                        ["layerMask"] = new JsonObject { ["type"] = "integer", ["description"] = "Optional layer mask for filtering." }
                    }
                }),
            new McpToolDefinition(
                "physics.overlapSphere",
                "Finds all colliders within a sphere and returns their GameObject info.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("center", "radius"),
                    ["properties"] = new JsonObject
                    {
                        ["center"] = Vector3Schema("Sphere center [x,y,z]."),
                        ["radius"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0, ["description"] = "Sphere radius." },
                        ["layerMask"] = new JsonObject { ["type"] = "integer", ["description"] = "Optional layer mask for filtering." }
                    }
                }),

            // ── Batch 5: Time ─────────────────────────────────────────────────
            new McpToolDefinition(
                "time.getSettings",
                "Returns Unity Time settings: timeScale, fixedDeltaTime, maximumDeltaTime, maximumParticleDeltaTime.",
                EmptyObjectSchema()),
            new McpToolDefinition(
                "time.setSettings",
                "Sets Unity Time settings: timeScale, fixedDeltaTime.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new JsonObject
                    {
                        ["timeScale"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["description"] = "Time scale (0 = paused, 1 = normal)." },
                        ["fixedDeltaTime"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0, ["description"] = "Fixed timestep in seconds." }
                    }
                }),

            // ── Batch 5: Joint (base 3D) ──────────────────────────────────────
            new McpToolDefinition(
                "joint.getSettings",
                "Returns base Joint settings for any 3D Joint component (HingeJoint, SpringJoint, FixedJoint, etc.).",
                InstanceIdOnlySchema("Unity instance id of a Joint component or a GameObject with a single Joint.")),
            new McpToolDefinition(
                "joint.setSettings",
                "Mutates base Joint settings: breakForce, breakTorque, enableCollision.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["description"] = "Force needed to break the joint (Infinity = unbreakable)." },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["description"] = "Torque needed to break the joint (Infinity = unbreakable)." },
                        ["enableCollision"] = new JsonObject { ["type"] = "boolean", ["description"] = "Enable collision between connected bodies." }
                    }
                }),

            // ── Batch 5: Renderer ─────────────────────────────────────────────
            new McpToolDefinition(
                "renderer.getMaterials",
                "Returns array of material names and instance IDs for all materials on any Renderer component.",
                InstanceIdOnlySchema("Unity instance id of a Renderer component or a GameObject with a single Renderer.")),
            new McpToolDefinition(
                "renderer.setMaterial",
                "Assigns a material to a specific slot on a Renderer component by loading from asset path.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId", "materialIndex", "materialAssetPath"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer", ["description"] = "Unity instance id of a Renderer component or a GameObject with a single Renderer." },
                        ["materialIndex"] = new JsonObject { ["type"] = "integer", ["minimum"] = 0, ["description"] = "Zero-based index of the material slot to assign." },
                        ["materialAssetPath"] = new JsonObject { ["type"] = "string", ["description"] = "Project-relative path to the material asset (e.g. Assets/Materials/MyMat.mat)." }
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

    private static JsonObject Vector2Schema(string description)
    {
        return new JsonObject
        {
            ["type"] = "array",
            ["description"] = description,
            ["minItems"] = 2,
            ["maxItems"] = 2,
            ["items"] = new JsonObject
            {
                ["type"] = "number"
            }
        };
    }

    private static JsonObject InstanceIdOnlySchema(string description)
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = new JsonArray("instanceId"),
            ["properties"] = new JsonObject
            {
                ["instanceId"] = new JsonObject
                {
                    ["type"] = "integer",
                    ["description"] = description
                }
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

    private static JsonObject NullableIntegerSchema(string description)
    {
        return new JsonObject
        {
            ["description"] = description,
            ["type"] = new JsonArray("integer", "null")
        };
    }

    private static JsonObject ConnectedAnchorModeSchema()
    {
        return new JsonObject
        {
            ["type"] = "string",
            ["description"] = "Connection helper mode: preserve, auto, zero, or matchAnchor.",
            ["enum"] = new JsonArray("preserve", "auto", "zero", "matchAnchor")
        };
    }

    private static JsonObject PrefabScopeSchema()
    {
        return new JsonObject
        {
            ["type"] = "string",
            ["description"] = "Prefab override scope: instanceRoot, object, or component.",
            ["enum"] = new JsonArray("instanceRoot", "object", "component")
        };
    }

    private static JsonObject SoftJointLimitSchema(string description)
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["description"] = description,
            ["additionalProperties"] = false,
            ["properties"] = new JsonObject
            {
                ["limit"] = new JsonObject { ["type"] = "number" },
                ["bounciness"] = new JsonObject { ["type"] = "number" },
                ["contactDistance"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 }
            }
        };
    }

    private static JsonObject SoftJointLimitSpringSchema(string description)
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["description"] = description,
            ["additionalProperties"] = false,
            ["properties"] = new JsonObject
            {
                ["spring"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                ["damper"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 }
            }
        };
    }

    private static JsonObject JointDriveSchema(string description)
    {
        return new JsonObject
        {
            ["type"] = "object",
            ["description"] = description,
            ["additionalProperties"] = false,
            ["properties"] = new JsonObject
            {
                ["positionSpring"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                ["positionDamper"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                ["maximumForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 }
            }
        };
    }
}

public sealed record McpToolDefinition(string Name, string Description, JsonObject InputSchema);

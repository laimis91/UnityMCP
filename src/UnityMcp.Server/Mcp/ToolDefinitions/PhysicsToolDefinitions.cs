using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp.ToolDefinitions;

internal static class PhysicsToolDefinitions
{
    internal static McpToolDefinition[] GetTools()
    {
        return new[]
        {
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
                        ["constraints"] = McpToolSchemaHelpers.EnumLikeSchema("RigidbodyConstraints enum name or integer flags value."),
                        ["interpolation"] = McpToolSchemaHelpers.EnumLikeSchema("RigidbodyInterpolation enum name or integer value."),
                        ["collisionDetectionMode"] = McpToolSchemaHelpers.EnumLikeSchema("CollisionDetectionMode enum name or integer value.")
                    }
                }),
            new McpToolDefinition(
                "rigidbody2D.getSettings",
                "Returns common Rigidbody2D settings for a Rigidbody2D component target (or a GameObject with a single Rigidbody2D).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a Rigidbody2D component or a GameObject with a single Rigidbody2D.")),
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
                        ["bodyType"] = McpToolSchemaHelpers.EnumLikeSchema("RigidbodyType2D enum name or integer value."),
                        ["simulated"] = new JsonObject { ["type"] = "boolean" },
                        ["useAutoMass"] = new JsonObject { ["type"] = "boolean" },
                        ["mass"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["gravityScale"] = new JsonObject { ["type"] = "number" },
                        ["constraints"] = McpToolSchemaHelpers.EnumLikeSchema("RigidbodyConstraints2D enum name or integer flags value."),
                        ["interpolation"] = McpToolSchemaHelpers.EnumLikeSchema("RigidbodyInterpolation2D enum name or integer value."),
                        ["collisionDetectionMode"] = McpToolSchemaHelpers.EnumLikeSchema("CollisionDetectionMode2D enum name or integer value."),
                        ["sleepMode"] = McpToolSchemaHelpers.EnumLikeSchema("RigidbodySleepMode2D enum name or integer value.")
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
                        ["center"] = McpToolSchemaHelpers.Vector3Schema("BoxCollider center [x,y,z] (BoxCollider only in MVP)."),
                        ["size"] = McpToolSchemaHelpers.Vector3Schema("BoxCollider size [x,y,z] (BoxCollider only in MVP).")
                    }
                }),
            new McpToolDefinition(
                "collider2D.getSettings",
                "Returns common Collider2D settings for a Collider2D component target (or a GameObject with a single Collider2D).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a Collider2D component or a GameObject with a single Collider2D.")),
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
                        ["offset"] = McpToolSchemaHelpers.Vector2Schema("Collider2D offset [x,y]."),
                        ["density"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "boxCollider.getSettings",
                "Returns BoxCollider settings for a BoxCollider target (or a GameObject with a single BoxCollider).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a BoxCollider component or a GameObject with a single BoxCollider.")),
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
                        ["center"] = McpToolSchemaHelpers.Vector3Schema("BoxCollider center [x,y,z]."),
                        ["size"] = McpToolSchemaHelpers.Vector3Schema("BoxCollider size [x,y,z].")
                    }
                }),
            new McpToolDefinition(
                "boxCollider2D.getSettings",
                "Returns BoxCollider2D settings for a BoxCollider2D target (or a GameObject with a single BoxCollider2D).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a BoxCollider2D component or a GameObject with a single BoxCollider2D.")),
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
                        ["offset"] = McpToolSchemaHelpers.Vector2Schema("BoxCollider2D offset [x,y]."),
                        ["density"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["size"] = McpToolSchemaHelpers.Vector2Schema("BoxCollider2D size [x,y]."),
                        ["edgeRadius"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "sphereCollider.getSettings",
                "Returns SphereCollider settings for a SphereCollider target (or a GameObject with a single SphereCollider).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a SphereCollider component or a GameObject with a single SphereCollider.")),
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
                        ["center"] = McpToolSchemaHelpers.Vector3Schema("SphereCollider center [x,y,z]."),
                        ["radius"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "circleCollider2D.getSettings",
                "Returns CircleCollider2D settings for a CircleCollider2D target (or a GameObject with a single CircleCollider2D).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a CircleCollider2D component or a GameObject with a single CircleCollider2D.")),
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
                        ["offset"] = McpToolSchemaHelpers.Vector2Schema("CircleCollider2D offset [x,y]."),
                        ["density"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["radius"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "capsuleCollider.getSettings",
                "Returns CapsuleCollider settings for a CapsuleCollider target (or a GameObject with a single CapsuleCollider).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a CapsuleCollider component or a GameObject with a single CapsuleCollider.")),
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
                        ["center"] = McpToolSchemaHelpers.Vector3Schema("CapsuleCollider center [x,y,z]."),
                        ["radius"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["height"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["direction"] = McpToolSchemaHelpers.EnumLikeSchema("Capsule direction as enum name (X/Y/Z or 0/1/2) or integer value.")
                    }
                }),
            new McpToolDefinition(
                "capsuleCollider2D.getSettings",
                "Returns CapsuleCollider2D settings for a CapsuleCollider2D target (or a GameObject with a single CapsuleCollider2D).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a CapsuleCollider2D component or a GameObject with a single CapsuleCollider2D.")),
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
                        ["offset"] = McpToolSchemaHelpers.Vector2Schema("CapsuleCollider2D offset [x,y]."),
                        ["density"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["size"] = McpToolSchemaHelpers.Vector2Schema("CapsuleCollider2D size [x,y]."),
                        ["direction"] = McpToolSchemaHelpers.EnumLikeSchema("CapsuleDirection2D enum name or integer value.")
                    }
                }),
            new McpToolDefinition(
                "meshCollider.getSettings",
                "Returns MeshCollider settings for a MeshCollider target (or a GameObject with a single MeshCollider).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a MeshCollider component or a GameObject with a single MeshCollider.")),
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
                        ["cookingOptions"] = McpToolSchemaHelpers.EnumLikeSchema("MeshColliderCookingOptions enum name or integer flags value.")
                    }
                }),
            new McpToolDefinition(
                "polygonCollider2D.getSettings",
                "Returns PolygonCollider2D settings for a PolygonCollider2D target (or a GameObject with a single PolygonCollider2D).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a PolygonCollider2D component or a GameObject with a single PolygonCollider2D.")),
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
                        ["offset"] = McpToolSchemaHelpers.Vector2Schema("PolygonCollider2D offset [x,y]."),
                        ["density"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "edgeCollider2D.getSettings",
                "Returns EdgeCollider2D settings for an EdgeCollider2D target (or a GameObject with a single EdgeCollider2D).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of an EdgeCollider2D component or a GameObject with a single EdgeCollider2D.")),
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
                        ["offset"] = McpToolSchemaHelpers.Vector2Schema("EdgeCollider2D offset [x,y]."),
                        ["density"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["edgeRadius"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "compositeCollider2D.getSettings",
                "Returns CompositeCollider2D settings for a CompositeCollider2D target (or a GameObject with a single CompositeCollider2D).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a CompositeCollider2D component or a GameObject with a single CompositeCollider2D.")),
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
                        ["offset"] = McpToolSchemaHelpers.Vector2Schema("CompositeCollider2D offset [x,y]."),
                        ["density"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["geometryType"] = McpToolSchemaHelpers.EnumLikeSchema("CompositeCollider2D.GeometryType enum name or integer value."),
                        ["generationType"] = McpToolSchemaHelpers.EnumLikeSchema("CompositeCollider2D.GenerationType enum name or integer value.")
                    }
                }),
            new McpToolDefinition(
                "characterController.getSettings",
                "Returns CharacterController settings for a CharacterController target (or a GameObject with a single CharacterController).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a CharacterController component or a GameObject with a single CharacterController.")),
            new McpToolDefinition(
                "characterController.setSettings",
                "Mutates CharacterController settings using direct Unity CharacterController APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["enabled"] = new JsonObject { ["type"] = "boolean" },
                        ["slopeLimit"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 90 },
                        ["stepOffset"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["skinWidth"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["minMoveDistance"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["center"] = McpToolSchemaHelpers.Vector3Schema("CharacterController center [x,y,z]."),
                        ["radius"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["height"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["detectCollisions"] = new JsonObject { ["type"] = "boolean" },
                        ["enableOverlapRecovery"] = new JsonObject { ["type"] = "boolean" }
                    }
                }),
            new McpToolDefinition(
                "physics.raycast",
                "Performs a 3D physics raycast and returns hit information.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("origin", "direction"),
                    ["properties"] = new JsonObject
                    {
                        ["origin"] = McpToolSchemaHelpers.Vector3Schema("Ray origin [x,y,z]."),
                        ["direction"] = McpToolSchemaHelpers.Vector3Schema("Ray direction [x,y,z]."),
                        ["maxDistance"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0, ["description"] = "Maximum raycast distance (default: Infinity)." },
                        ["layerMask"] = new JsonObject { ["type"] = "integer", ["description"] = "Layer mask for filtering (default: all layers)." },
                        ["queryTriggerInteraction"] = McpToolSchemaHelpers.EnumLikeSchema("QueryTriggerInteraction enum name or integer value.")
                    }
                }),
            new McpToolDefinition(
                "physics.overlapSphere",
                "Finds all colliders overlapping a sphere and returns their information.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("position", "radius"),
                    ["properties"] = new JsonObject
                    {
                        ["position"] = McpToolSchemaHelpers.Vector3Schema("Sphere center position [x,y,z]."),
                        ["radius"] = new JsonObject { ["type"] = "number", ["exclusiveMinimum"] = 0 },
                        ["layerMask"] = new JsonObject { ["type"] = "integer", ["description"] = "Layer mask for filtering (default: all layers)." },
                        ["queryTriggerInteraction"] = McpToolSchemaHelpers.EnumLikeSchema("QueryTriggerInteraction enum name or integer value.")
                    }
                }),
            new McpToolDefinition(
                "physics2D.getSettings",
                "Returns Unity Physics2D global settings.",
                McpToolSchemaHelpers.EmptyObjectSchema()),
            new McpToolDefinition(
                "physics2D.setSettings",
                "Mutates Unity Physics2D global settings.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new JsonObject
                    {
                        ["gravity"] = McpToolSchemaHelpers.Vector2Schema("Physics2D gravity [x,y]."),
                        ["defaultMaterial"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Asset path to default PhysicsMaterial2D."
                        },
                        ["velocityIterations"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 },
                        ["positionIterations"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 },
                        ["velocityThreshold"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["maxLinearCorrection"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["maxAngularCorrection"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["maxTranslationSpeed"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["maxRotationSpeed"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["defaultContactOffset"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["baumgarteScale"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["baumgarteTOIScale"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["timeToSleep"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["linearSleepTolerance"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["angularSleepTolerance"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["defaultSolverIterations"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 },
                        ["defaultSolverVelocityIterations"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 },
                        ["queriesHitTriggers"] = new JsonObject { ["type"] = "boolean" },
                        ["queriesStartInColliders"] = new JsonObject { ["type"] = "boolean" },
                        ["callbacksOnDisable"] = new JsonObject { ["type"] = "boolean" },
                        ["reuseCollisionCallbacks"] = new JsonObject { ["type"] = "boolean" },
                        ["autoSyncTransforms"] = new JsonObject { ["type"] = "boolean" },
                        ["alwaysShowColliders"] = new JsonObject { ["type"] = "boolean" },
                        ["showColliderSleep"] = new JsonObject { ["type"] = "boolean" },
                        ["showColliderContacts"] = new JsonObject { ["type"] = "boolean" },
                        ["showColliderAABB"] = new JsonObject { ["type"] = "boolean" },
                        ["contactArrowScale"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["colliderAwakeColor"] = McpToolSchemaHelpers.ColorSchema("Color for awake colliders [r,g,b,a]."),
                        ["colliderAsleepColor"] = McpToolSchemaHelpers.ColorSchema("Color for asleep colliders [r,g,b,a]."),
                        ["colliderContactColor"] = McpToolSchemaHelpers.ColorSchema("Color for collider contacts [r,g,b,a]."),
                        ["colliderAABBColor"] = McpToolSchemaHelpers.ColorSchema("Color for collider AABBs [r,g,b,a].")
                    }
                })
        };
    }
}
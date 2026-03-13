using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp;

public sealed partial class McpToolCatalog
{
    private static IEnumerable<McpToolDefinition> GetPhysicsTools()
    {
        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "rigidbody2D.getSettings",
            "Returns common Rigidbody2D settings for a Rigidbody2D component target (or a GameObject with a single Rigidbody2D).",
            InstanceIdOnlySchema("Unity instance id of a Rigidbody2D component or a GameObject with a single Rigidbody2D."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "collider2D.getSettings",
            "Returns common Collider2D settings for a Collider2D component target (or a GameObject with a single Collider2D).",
            InstanceIdOnlySchema("Unity instance id of a Collider2D component or a GameObject with a single Collider2D."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "boxCollider.getSettings",
            "Returns BoxCollider settings for a BoxCollider target (or a GameObject with a single BoxCollider).",
            InstanceIdOnlySchema("Unity instance id of a BoxCollider component or a GameObject with a single BoxCollider."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "boxCollider2D.getSettings",
            "Returns BoxCollider2D settings for a BoxCollider2D target (or a GameObject with a single BoxCollider2D).",
            InstanceIdOnlySchema("Unity instance id of a BoxCollider2D component or a GameObject with a single BoxCollider2D."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "sphereCollider.getSettings",
            "Returns SphereCollider settings for a SphereCollider target (or a GameObject with a single SphereCollider).",
            InstanceIdOnlySchema("Unity instance id of a SphereCollider component or a GameObject with a single SphereCollider."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "circleCollider2D.getSettings",
            "Returns CircleCollider2D settings for a CircleCollider2D target (or a GameObject with a single CircleCollider2D).",
            InstanceIdOnlySchema("Unity instance id of a CircleCollider2D component or a GameObject with a single CircleCollider2D."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "capsuleCollider.getSettings",
            "Returns CapsuleCollider settings for a CapsuleCollider target (or a GameObject with a single CapsuleCollider).",
            InstanceIdOnlySchema("Unity instance id of a CapsuleCollider component or a GameObject with a single CapsuleCollider."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "capsuleCollider2D.getSettings",
            "Returns CapsuleCollider2D settings for a CapsuleCollider2D target (or a GameObject with a single CapsuleCollider2D).",
            InstanceIdOnlySchema("Unity instance id of a CapsuleCollider2D component or a GameObject with a single CapsuleCollider2D."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "meshCollider.getSettings",
            "Returns MeshCollider settings for a MeshCollider target (or a GameObject with a single MeshCollider).",
            InstanceIdOnlySchema("Unity instance id of a MeshCollider component or a GameObject with a single MeshCollider."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "polygonCollider2D.getSettings",
            "Returns PolygonCollider2D settings for a PolygonCollider2D target (or a GameObject with a single PolygonCollider2D).",
            InstanceIdOnlySchema("Unity instance id of a PolygonCollider2D component or a GameObject with a single PolygonCollider2D."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "edgeCollider2D.getSettings",
            "Returns EdgeCollider2D settings for an EdgeCollider2D target (or a GameObject with a single EdgeCollider2D).",
            InstanceIdOnlySchema("Unity instance id of an EdgeCollider2D component or a GameObject with a single EdgeCollider2D."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "compositeCollider2D.getSettings",
            "Returns CompositeCollider2D settings for a CompositeCollider2D target (or a GameObject with a single CompositeCollider2D).",
            InstanceIdOnlySchema("Unity instance id of a CompositeCollider2D component or a GameObject with a single CompositeCollider2D."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "hingeJoint2D.getSettings",
            "Returns HingeJoint2D settings for a HingeJoint2D target (or a GameObject with a single HingeJoint2D).",
            InstanceIdOnlySchema("Unity instance id of a HingeJoint2D component or a GameObject with a single HingeJoint2D."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "springJoint2D.getSettings",
            "Returns SpringJoint2D settings for a SpringJoint2D target (or a GameObject with a single SpringJoint2D).",
            InstanceIdOnlySchema("Unity instance id of a SpringJoint2D component or a GameObject with a single SpringJoint2D."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "distanceJoint2D.getSettings",
            "Returns DistanceJoint2D settings for a DistanceJoint2D target (or a GameObject with a single DistanceJoint2D).",
            InstanceIdOnlySchema("Unity instance id of a DistanceJoint2D component or a GameObject with a single DistanceJoint2D."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "fixedJoint2D.getSettings",
            "Returns FixedJoint2D settings for a FixedJoint2D target (or a GameObject with a single FixedJoint2D).",
            InstanceIdOnlySchema("Unity instance id of a FixedJoint2D component or a GameObject with a single FixedJoint2D."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "sliderJoint2D.getSettings",
            "Returns SliderJoint2D settings for a SliderJoint2D target (or a GameObject with a single SliderJoint2D).",
            InstanceIdOnlySchema("Unity instance id of a SliderJoint2D component or a GameObject with a single SliderJoint2D."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "wheelJoint2D.getSettings",
            "Returns WheelJoint2D settings for a WheelJoint2D target (or a GameObject with a single WheelJoint2D).",
            InstanceIdOnlySchema("Unity instance id of a WheelJoint2D component or a GameObject with a single WheelJoint2D."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "targetJoint2D.getSettings",
            "Returns TargetJoint2D settings for a TargetJoint2D target (or a GameObject with a single TargetJoint2D).",
            InstanceIdOnlySchema("Unity instance id of a TargetJoint2D component or a GameObject with a single TargetJoint2D."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "hingeJoint.getSettings",
            "Returns HingeJoint settings for a HingeJoint target (or a GameObject with a single HingeJoint).",
            InstanceIdOnlySchema("Unity instance id of a HingeJoint component or a GameObject with a single HingeJoint."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "springJoint.getSettings",
            "Returns SpringJoint settings for a SpringJoint target (or a GameObject with a single SpringJoint).",
            InstanceIdOnlySchema("Unity instance id of a SpringJoint component or a GameObject with a single SpringJoint."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "fixedJoint.getSettings",
            "Returns FixedJoint settings for a FixedJoint target (or a GameObject with a single FixedJoint).",
            InstanceIdOnlySchema("Unity instance id of a FixedJoint component or a GameObject with a single FixedJoint."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "characterJoint.getSettings",
            "Returns CharacterJoint settings for a CharacterJoint target (or a GameObject with a single CharacterJoint).",
            InstanceIdOnlySchema("Unity instance id of a CharacterJoint component or a GameObject with a single CharacterJoint."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "configurableJoint.getSettings",
            "Returns ConfigurableJoint settings for a ConfigurableJoint target (or a GameObject with a single ConfigurableJoint).",
            InstanceIdOnlySchema("Unity instance id of a ConfigurableJoint component or a GameObject with a single ConfigurableJoint."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "joint.getSettings",
            "Returns base Joint settings for any 3D Joint component (HingeJoint, SpringJoint, FixedJoint, etc.).",
            InstanceIdOnlySchema("Unity instance id of a Joint component or a GameObject with a single Joint."));

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "physics2D.getSettings",
            "Returns Unity Physics2D settings including gravity, iterations, thresholds, and simulation parameters.",
            EmptyObjectSchema());

        yield return new McpToolDefinition(
            "physics2D.setSettings",
            "Updates Unity Physics2D settings. Supports any subset of available parameters.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JsonObject
                {
                    ["gravity"] = Vector2Schema("Gravity vector applied to all 2D rigidbodies."),
                    ["defaultContactOffset"] = new JsonObject
                    {
                        ["type"] = "number",
                        ["minimum"] = 0,
                        ["description"] = "Contact offset value for newly created 2D colliders."
                    },
                    ["velocityIterations"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["minimum"] = 1,
                        ["description"] = "Number of velocity solver iterations per step."
                    },
                    ["positionIterations"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["minimum"] = 1,
                        ["description"] = "Number of position solver iterations per step."
                    },
                    ["velocityThreshold"] = new JsonObject
                    {
                        ["type"] = "number",
                        ["minimum"] = 0,
                        ["description"] = "Velocity threshold for collision response."
                    },
                    ["maxLinearCorrection"] = new JsonObject
                    {
                        ["type"] = "number",
                        ["minimum"] = 0,
                        ["description"] = "Maximum linear position correction per step."
                    },
                    ["maxAngularCorrection"] = new JsonObject
                    {
                        ["type"] = "number",
                        ["minimum"] = 0,
                        ["description"] = "Maximum angular correction per step in radians."
                    },
                    ["maxTranslationSpeed"] = new JsonObject
                    {
                        ["type"] = "number",
                        ["minimum"] = 0,
                        ["description"] = "Maximum translation speed for continuous collision detection."
                    },
                    ["maxRotationSpeed"] = new JsonObject
                    {
                        ["type"] = "number",
                        ["minimum"] = 0,
                        ["description"] = "Maximum rotation speed for continuous collision detection."
                    },
                    ["autoSimulation"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Whether 2D physics simulation runs automatically."
                    },
                    ["autoSyncTransforms"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Whether 2D transform changes are automatically synced with physics."
                    },
                    ["reuseCollisionCallbacks"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Whether to reuse collision callback objects."
                    }
                }
            });
    }
}

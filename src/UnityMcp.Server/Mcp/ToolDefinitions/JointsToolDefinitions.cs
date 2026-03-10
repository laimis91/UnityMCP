using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp.ToolDefinitions;

internal static class JointsToolDefinitions
{
    internal static McpToolDefinition[] GetTools()
    {
        return new[]
        {
            new McpToolDefinition(
                "hingeJoint2D.getSettings",
                "Returns HingeJoint2D settings for a HingeJoint2D target (or a GameObject with a single HingeJoint2D).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a HingeJoint2D component or a GameObject with a single HingeJoint2D.")),
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
                        ["anchor"] = McpToolSchemaHelpers.Vector2Schema("HingeJoint2D anchor [x,y]."),
                        ["connectedAnchor"] = McpToolSchemaHelpers.Vector2Schema("HingeJoint2D connected anchor [x,y]."),
                        ["enableCollision"] = new JsonObject { ["type"] = "boolean" },
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["connectedBodyInstanceId"] = McpToolSchemaHelpers.NullableIntegerSchema("HingeJoint2D connected Rigidbody2D instance id, GameObject with one Rigidbody2D, or null to clear."),
                        ["connectedAnchorMode"] = McpToolSchemaHelpers.ConnectedAnchorModeSchema(),
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
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a SpringJoint2D component or a GameObject with a single SpringJoint2D.")),
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
                        ["anchor"] = McpToolSchemaHelpers.Vector2Schema("SpringJoint2D anchor [x,y]."),
                        ["connectedAnchor"] = McpToolSchemaHelpers.Vector2Schema("SpringJoint2D connected anchor [x,y]."),
                        ["enableCollision"] = new JsonObject { ["type"] = "boolean" },
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["connectedBodyInstanceId"] = McpToolSchemaHelpers.NullableIntegerSchema("SpringJoint2D connected Rigidbody2D instance id, GameObject with one Rigidbody2D, or null to clear."),
                        ["connectedAnchorMode"] = McpToolSchemaHelpers.ConnectedAnchorModeSchema(),
                        ["autoConfigureDistance"] = new JsonObject { ["type"] = "boolean" },
                        ["distance"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["dampingRatio"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1 },
                        ["frequency"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "distanceJoint2D.getSettings",
                "Returns DistanceJoint2D settings for a DistanceJoint2D target (or a GameObject with a single DistanceJoint2D).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a DistanceJoint2D component or a GameObject with a single DistanceJoint2D.")),
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
                        ["anchor"] = McpToolSchemaHelpers.Vector2Schema("DistanceJoint2D anchor [x,y]."),
                        ["connectedAnchor"] = McpToolSchemaHelpers.Vector2Schema("DistanceJoint2D connected anchor [x,y]."),
                        ["enableCollision"] = new JsonObject { ["type"] = "boolean" },
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["connectedBodyInstanceId"] = McpToolSchemaHelpers.NullableIntegerSchema("DistanceJoint2D connected Rigidbody2D instance id, GameObject with one Rigidbody2D, or null to clear."),
                        ["connectedAnchorMode"] = McpToolSchemaHelpers.ConnectedAnchorModeSchema(),
                        ["autoConfigureDistance"] = new JsonObject { ["type"] = "boolean" },
                        ["distance"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["maxDistanceOnly"] = new JsonObject { ["type"] = "boolean" }
                    }
                }),
            new McpToolDefinition(
                "fixedJoint2D.getSettings",
                "Returns FixedJoint2D settings for a FixedJoint2D target (or a GameObject with a single FixedJoint2D).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a FixedJoint2D component or a GameObject with a single FixedJoint2D.")),
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
                        ["anchor"] = McpToolSchemaHelpers.Vector2Schema("FixedJoint2D anchor [x,y]."),
                        ["connectedAnchor"] = McpToolSchemaHelpers.Vector2Schema("FixedJoint2D connected anchor [x,y]."),
                        ["enableCollision"] = new JsonObject { ["type"] = "boolean" },
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["connectedBodyInstanceId"] = McpToolSchemaHelpers.NullableIntegerSchema("FixedJoint2D connected Rigidbody2D instance id, GameObject with one Rigidbody2D, or null to clear."),
                        ["connectedAnchorMode"] = McpToolSchemaHelpers.ConnectedAnchorModeSchema(),
                        ["dampingRatio"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1 },
                        ["frequency"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "sliderJoint2D.getSettings",
                "Returns SliderJoint2D settings for a SliderJoint2D target (or a GameObject with a single SliderJoint2D).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a SliderJoint2D component or a GameObject with a single SliderJoint2D.")),
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
                        ["anchor"] = McpToolSchemaHelpers.Vector2Schema("SliderJoint2D anchor [x,y]."),
                        ["connectedAnchor"] = McpToolSchemaHelpers.Vector2Schema("SliderJoint2D connected anchor [x,y]."),
                        ["enableCollision"] = new JsonObject { ["type"] = "boolean" },
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["connectedBodyInstanceId"] = McpToolSchemaHelpers.NullableIntegerSchema("SliderJoint2D connected Rigidbody2D instance id, GameObject with one Rigidbody2D, or null to clear."),
                        ["connectedAnchorMode"] = McpToolSchemaHelpers.ConnectedAnchorModeSchema(),
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
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a WheelJoint2D component or a GameObject with a single WheelJoint2D.")),
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
                        ["anchor"] = McpToolSchemaHelpers.Vector2Schema("WheelJoint2D anchor [x,y]."),
                        ["connectedAnchor"] = McpToolSchemaHelpers.Vector2Schema("WheelJoint2D connected anchor [x,y]."),
                        ["enableCollision"] = new JsonObject { ["type"] = "boolean" },
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["connectedBodyInstanceId"] = McpToolSchemaHelpers.NullableIntegerSchema("WheelJoint2D connected Rigidbody2D instance id, GameObject with one Rigidbody2D, or null to clear."),
                        ["connectedAnchorMode"] = McpToolSchemaHelpers.ConnectedAnchorModeSchema(),
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
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a TargetJoint2D component or a GameObject with a single TargetJoint2D.")),
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
                        ["anchor"] = McpToolSchemaHelpers.Vector2Schema("TargetJoint2D anchor [x,y]."),
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["autoConfigureTarget"] = new JsonObject { ["type"] = "boolean" },
                        ["target"] = McpToolSchemaHelpers.Vector2Schema("TargetJoint2D world target [x,y]."),
                        ["maxForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["dampingRatio"] = new JsonObject { ["type"] = "number", ["minimum"] = 0, ["maximum"] = 1 },
                        ["frequency"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "hingeJoint.getSettings",
                "Returns HingeJoint settings for a HingeJoint target (or a GameObject with a single HingeJoint).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a HingeJoint component or a GameObject with a single HingeJoint.")),
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
                        ["anchor"] = McpToolSchemaHelpers.Vector3Schema("HingeJoint anchor [x,y,z]."),
                        ["connectedAnchor"] = McpToolSchemaHelpers.Vector3Schema("HingeJoint connected anchor [x,y,z]."),
                        ["axis"] = McpToolSchemaHelpers.Vector3Schema("HingeJoint axis [x,y,z]."),
                        ["enableCollision"] = new JsonObject { ["type"] = "boolean" },
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["connectedBodyInstanceId"] = McpToolSchemaHelpers.NullableIntegerSchema("HingeJoint connected Rigidbody instance id, GameObject with one Rigidbody, or null to clear."),
                        ["connectedAnchorMode"] = McpToolSchemaHelpers.ConnectedAnchorModeSchema(),
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
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a SpringJoint component or a GameObject with a single SpringJoint.")),
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
                        ["anchor"] = McpToolSchemaHelpers.Vector3Schema("SpringJoint anchor [x,y,z]."),
                        ["connectedAnchor"] = McpToolSchemaHelpers.Vector3Schema("SpringJoint connected anchor [x,y,z]."),
                        ["axis"] = McpToolSchemaHelpers.Vector3Schema("SpringJoint axis [x,y,z]."),
                        ["enableCollision"] = new JsonObject { ["type"] = "boolean" },
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["connectedBodyInstanceId"] = McpToolSchemaHelpers.NullableIntegerSchema("SpringJoint connected Rigidbody instance id, GameObject with one Rigidbody, or null to clear."),
                        ["connectedAnchorMode"] = McpToolSchemaHelpers.ConnectedAnchorModeSchema(),
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
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a FixedJoint component or a GameObject with a single FixedJoint.")),
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
                        ["anchor"] = McpToolSchemaHelpers.Vector3Schema("FixedJoint anchor [x,y,z]."),
                        ["connectedAnchor"] = McpToolSchemaHelpers.Vector3Schema("FixedJoint connected anchor [x,y,z]."),
                        ["axis"] = McpToolSchemaHelpers.Vector3Schema("FixedJoint axis [x,y,z]."),
                        ["enableCollision"] = new JsonObject { ["type"] = "boolean" },
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["connectedBodyInstanceId"] = McpToolSchemaHelpers.NullableIntegerSchema("FixedJoint connected Rigidbody instance id, GameObject with one Rigidbody, or null to clear."),
                        ["connectedAnchorMode"] = McpToolSchemaHelpers.ConnectedAnchorModeSchema()
                    }
                }),
            new McpToolDefinition(
                "characterJoint.getSettings",
                "Returns CharacterJoint settings for a CharacterJoint target (or a GameObject with a single CharacterJoint).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a CharacterJoint component or a GameObject with a single CharacterJoint.")),
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
                        ["anchor"] = McpToolSchemaHelpers.Vector3Schema("CharacterJoint anchor [x,y,z]."),
                        ["connectedAnchor"] = McpToolSchemaHelpers.Vector3Schema("CharacterJoint connected anchor [x,y,z]."),
                        ["axis"] = McpToolSchemaHelpers.Vector3Schema("CharacterJoint axis [x,y,z]."),
                        ["enableCollision"] = new JsonObject { ["type"] = "boolean" },
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["connectedBodyInstanceId"] = McpToolSchemaHelpers.NullableIntegerSchema("CharacterJoint connected Rigidbody instance id, GameObject with one Rigidbody, or null to clear."),
                        ["connectedAnchorMode"] = McpToolSchemaHelpers.ConnectedAnchorModeSchema(),
                        ["swingAxis"] = McpToolSchemaHelpers.Vector3Schema("CharacterJoint swing axis [x,y,z]."),
                        ["enableProjection"] = new JsonObject { ["type"] = "boolean" },
                        ["enablePreprocessing"] = new JsonObject { ["type"] = "boolean" },
                        ["twistLimitSpring"] = McpToolSchemaHelpers.SoftJointLimitSpringSchema("CharacterJoint twist limit spring."),
                        ["swingLimitSpring"] = McpToolSchemaHelpers.SoftJointLimitSpringSchema("CharacterJoint swing limit spring."),
                        ["lowTwistLimit"] = McpToolSchemaHelpers.SoftJointLimitSchema("CharacterJoint low twist limit."),
                        ["highTwistLimit"] = McpToolSchemaHelpers.SoftJointLimitSchema("CharacterJoint high twist limit."),
                        ["swing1Limit"] = McpToolSchemaHelpers.SoftJointLimitSchema("CharacterJoint swing1 limit."),
                        ["swing2Limit"] = McpToolSchemaHelpers.SoftJointLimitSchema("CharacterJoint swing2 limit.")
                    }
                }),
            new McpToolDefinition(
                "configurableJoint.getSettings",
                "Returns ConfigurableJoint settings for a ConfigurableJoint target (or a GameObject with a single ConfigurableJoint).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a ConfigurableJoint component or a GameObject with a single ConfigurableJoint.")),
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
                        ["anchor"] = McpToolSchemaHelpers.Vector3Schema("ConfigurableJoint anchor [x,y,z]."),
                        ["connectedAnchor"] = McpToolSchemaHelpers.Vector3Schema("ConfigurableJoint connected anchor [x,y,z]."),
                        ["axis"] = McpToolSchemaHelpers.Vector3Schema("ConfigurableJoint axis [x,y,z]."),
                        ["secondaryAxis"] = McpToolSchemaHelpers.Vector3Schema("ConfigurableJoint secondary axis [x,y,z]."),
                        ["enableCollision"] = new JsonObject { ["type"] = "boolean" },
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["connectedBodyInstanceId"] = McpToolSchemaHelpers.NullableIntegerSchema("ConfigurableJoint connected Rigidbody instance id, GameObject with one Rigidbody, or null to clear."),
                        ["connectedAnchorMode"] = McpToolSchemaHelpers.ConnectedAnchorModeSchema(),
                        ["configuredInWorldSpace"] = new JsonObject { ["type"] = "boolean" },
                        ["swapBodies"] = new JsonObject { ["type"] = "boolean" },
                        ["xMotion"] = McpToolSchemaHelpers.EnumLikeSchema("ConfigurableJointMotion enum name or integer value."),
                        ["yMotion"] = McpToolSchemaHelpers.EnumLikeSchema("ConfigurableJointMotion enum name or integer value."),
                        ["zMotion"] = McpToolSchemaHelpers.EnumLikeSchema("ConfigurableJointMotion enum name or integer value."),
                        ["angularXMotion"] = McpToolSchemaHelpers.EnumLikeSchema("ConfigurableJointMotion enum name or integer value."),
                        ["angularYMotion"] = McpToolSchemaHelpers.EnumLikeSchema("ConfigurableJointMotion enum name or integer value."),
                        ["angularZMotion"] = McpToolSchemaHelpers.EnumLikeSchema("ConfigurableJointMotion enum name or integer value."),
                        ["linearLimit"] = McpToolSchemaHelpers.SoftJointLimitSchema("ConfigurableJoint linear limit."),
                        ["lowAngularXLimit"] = McpToolSchemaHelpers.SoftJointLimitSchema("ConfigurableJoint low angular X limit."),
                        ["highAngularXLimit"] = McpToolSchemaHelpers.SoftJointLimitSchema("ConfigurableJoint high angular X limit."),
                        ["angularYLimit"] = McpToolSchemaHelpers.SoftJointLimitSchema("ConfigurableJoint angular Y limit."),
                        ["angularZLimit"] = McpToolSchemaHelpers.SoftJointLimitSchema("ConfigurableJoint angular Z limit."),
                        ["targetPosition"] = McpToolSchemaHelpers.Vector3Schema("ConfigurableJoint target position [x,y,z]."),
                        ["targetVelocity"] = McpToolSchemaHelpers.Vector3Schema("ConfigurableJoint target velocity [x,y,z]."),
                        ["targetAngularVelocity"] = McpToolSchemaHelpers.Vector3Schema("ConfigurableJoint target angular velocity [x,y,z]."),
                        ["rotationDriveMode"] = McpToolSchemaHelpers.EnumLikeSchema("RotationDriveMode enum name or integer value."),
                        ["xDrive"] = McpToolSchemaHelpers.JointDriveSchema("ConfigurableJoint X drive."),
                        ["yDrive"] = McpToolSchemaHelpers.JointDriveSchema("ConfigurableJoint Y drive."),
                        ["zDrive"] = McpToolSchemaHelpers.JointDriveSchema("ConfigurableJoint Z drive."),
                        ["angularXDrive"] = McpToolSchemaHelpers.JointDriveSchema("ConfigurableJoint angular X drive."),
                        ["angularYZDrive"] = McpToolSchemaHelpers.JointDriveSchema("ConfigurableJoint angular YZ drive."),
                        ["slerpDrive"] = McpToolSchemaHelpers.JointDriveSchema("ConfigurableJoint slerp drive."),
                        ["projectionMode"] = McpToolSchemaHelpers.EnumLikeSchema("JointProjectionMode enum name or integer value."),
                        ["projectionDistance"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["projectionAngle"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 }
                    }
                }),
            new McpToolDefinition(
                "joint.getSettings",
                "Returns generic Joint settings for any Joint component target (or a GameObject with a single Joint).",
                McpToolSchemaHelpers.InstanceIdOnlySchema("Unity instance id of a Joint component or a GameObject with a single Joint.")),
            new McpToolDefinition(
                "joint.setSettings",
                "Mutates generic Joint settings using direct Unity Joint APIs.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("instanceId"),
                    ["properties"] = new JsonObject
                    {
                        ["instanceId"] = new JsonObject { ["type"] = "integer" },
                        ["autoConfigureConnectedAnchor"] = new JsonObject { ["type"] = "boolean" },
                        ["anchor"] = McpToolSchemaHelpers.Vector3Schema("Joint anchor [x,y,z]."),
                        ["connectedAnchor"] = McpToolSchemaHelpers.Vector3Schema("Joint connected anchor [x,y,z]."),
                        ["axis"] = McpToolSchemaHelpers.Vector3Schema("Joint axis [x,y,z]."),
                        ["enableCollision"] = new JsonObject { ["type"] = "boolean" },
                        ["breakForce"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["breakTorque"] = new JsonObject { ["type"] = "number", ["minimum"] = 0 },
                        ["connectedBodyInstanceId"] = McpToolSchemaHelpers.NullableIntegerSchema("Joint connected Rigidbody instance id, GameObject with one Rigidbody, or null to clear."),
                        ["connectedAnchorMode"] = McpToolSchemaHelpers.ConnectedAnchorModeSchema()
                    }
                })
        };
    }
}
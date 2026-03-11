using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp;

public sealed partial class McpToolCatalog
{
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

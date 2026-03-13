using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp;

public sealed partial class McpToolCatalog
{
    private static IEnumerable<McpToolDefinition> GetMaterialTools()
    {
        yield return new McpToolDefinition(
            "material.getProperties",
            "List all shader properties on a material including name, type, and current value.",
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
                        ["description"] = "Asset path of the material, e.g. 'Assets/Materials/MyMat.mat'."
                    }
                }
            });

        yield return new McpToolDefinition(
            "material.getProperty",
            "Get a specific shader property value by name from a material.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("assetPath", "propertyName"),
                ["properties"] = new JsonObject
                {
                    ["assetPath"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Asset path of the material."
                    },
                    ["propertyName"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Name of the shader property, e.g. '_Color', '_MainTex'."
                    }
                }
            });

        yield return new McpToolDefinition(
            "material.setProperty",
            "Set a shader property value on a material by name and type.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("assetPath", "propertyName", "propertyType", "value"),
                ["properties"] = new JsonObject
                {
                    ["assetPath"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Asset path of the material."
                    },
                    ["propertyName"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Name of the shader property to set."
                    },
                    ["propertyType"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Type of the property value.",
                        ["enum"] = new JsonArray("color", "float", "int", "vector", "texture")
                    },
                    ["value"] = new JsonObject
                    {
                        ["description"] = "The value to set. For color: {r,g,b,a}. For vector: {x,y,z,w}. For float/int: a number. For texture: asset path string or null."
                    }
                }
            });

        yield return new McpToolDefinition(
            "material.getKeywords",
            "List all enabled shader keywords on a material.",
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
                        ["description"] = "Asset path of the material."
                    }
                }
            });

        yield return new McpToolDefinition(
            "material.setKeyword",
            "Enable or disable a shader keyword on a material.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("assetPath", "keyword", "enabled"),
                ["properties"] = new JsonObject
                {
                    ["assetPath"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Asset path of the material."
                    },
                    ["keyword"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Shader keyword name, e.g. '_EMISSION'."
                    },
                    ["enabled"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "True to enable, false to disable the keyword."
                    }
                }
            });

        yield return new McpToolDefinition(
            "material.getShader",
            "Get the shader name assigned to a material.",
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
                        ["description"] = "Asset path of the material."
                    }
                }
            });

        yield return new McpToolDefinition(
            "material.setShader",
            "Change the shader assigned to a material.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("assetPath", "shaderName"),
                ["properties"] = new JsonObject
                {
                    ["assetPath"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Asset path of the material."
                    },
                    ["shaderName"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Full shader name, e.g. 'Standard', 'Universal Render Pipeline/Lit'."
                    }
                }
            });

        yield return new McpToolDefinition(
            "material.getRenderQueue",
            "Get the render queue value of a material.",
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
                        ["description"] = "Asset path of the material."
                    }
                }
            });

        yield return new McpToolDefinition(
            "material.setRenderQueue",
            "Set the render queue value of a material.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["required"] = new JsonArray("assetPath", "renderQueue"),
                ["properties"] = new JsonObject
                {
                    ["assetPath"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Asset path of the material."
                    },
                    ["renderQueue"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["description"] = "Render queue value. Common values: 2000 (Geometry), 2450 (AlphaTest), 3000 (Transparent)."
                    }
                }
            });
    }
}

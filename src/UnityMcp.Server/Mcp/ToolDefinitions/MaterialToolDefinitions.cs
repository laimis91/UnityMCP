using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp.ToolDefinitions;

internal static class MaterialToolDefinitions
{
    internal static McpToolDefinition[] GetTools()
    {
        return new[]
        {
            new McpToolDefinition(
                "material.getProperties",
                "Returns all settable properties for a Material asset.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("materialPath"),
                    ["properties"] = new JsonObject
                    {
                        ["materialPath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity asset path to the Material."
                        }
                    }
                }),
            new McpToolDefinition(
                "material.getProperty",
                "Returns a specific property value from a Material asset.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("materialPath", "propertyName"),
                    ["properties"] = new JsonObject
                    {
                        ["materialPath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity asset path to the Material."
                        },
                        ["propertyName"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Name of the material property to get."
                        }
                    }
                }),
            new McpToolDefinition(
                "material.setProperty",
                "Sets a property on a Material asset.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("materialPath", "propertyName", "value"),
                    ["properties"] = new JsonObject
                    {
                        ["materialPath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity asset path to the Material."
                        },
                        ["propertyName"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Name of the material property to set."
                        },
                        ["value"] = new JsonObject
                        {
                            ["description"] = "Property value (type depends on property: number, vector array, color array, or texture path)."
                        }
                    }
                }),
            new McpToolDefinition(
                "material.getKeywords",
                "Returns enabled shader keywords for a Material asset.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("materialPath"),
                    ["properties"] = new JsonObject
                    {
                        ["materialPath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity asset path to the Material."
                        }
                    }
                }),
            new McpToolDefinition(
                "material.setKeyword",
                "Enables or disables a shader keyword on a Material asset.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("materialPath", "keyword", "enabled"),
                    ["properties"] = new JsonObject
                    {
                        ["materialPath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity asset path to the Material."
                        },
                        ["keyword"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Shader keyword to enable or disable."
                        },
                        ["enabled"] = new JsonObject
                        {
                            ["type"] = "boolean",
                            ["description"] = "Whether the keyword should be enabled."
                        }
                    }
                }),
            new McpToolDefinition(
                "material.getShader",
                "Returns the shader used by a Material asset.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("materialPath"),
                    ["properties"] = new JsonObject
                    {
                        ["materialPath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity asset path to the Material."
                        }
                    }
                }),
            new McpToolDefinition(
                "material.setShader",
                "Sets the shader for a Material asset.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("materialPath", "shaderName"),
                    ["properties"] = new JsonObject
                    {
                        ["materialPath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity asset path to the Material."
                        },
                        ["shaderName"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Name of the shader to assign (e.g., 'Standard', 'Unlit/Color')."
                        }
                    }
                }),
            new McpToolDefinition(
                "material.getRenderQueue",
                "Returns the render queue value for a Material asset.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("materialPath"),
                    ["properties"] = new JsonObject
                    {
                        ["materialPath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity asset path to the Material."
                        }
                    }
                }),
            new McpToolDefinition(
                "material.setRenderQueue",
                "Sets the render queue value for a Material asset.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("materialPath", "renderQueue"),
                    ["properties"] = new JsonObject
                    {
                        ["materialPath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity asset path to the Material."
                        },
                        ["renderQueue"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Render queue value (typically 1000-5000).",
                            ["minimum"] = 0
                        }
                    }
                })
        };
    }
}
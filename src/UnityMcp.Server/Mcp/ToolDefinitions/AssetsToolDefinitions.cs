using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp.ToolDefinitions;

internal static class AssetsToolDefinitions
{
    internal static McpToolDefinition[] GetTools()
    {
        return new[]
        {
            new McpToolDefinition(
                "assets.find",
                "Searches for Unity assets by name, type, or path.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new JsonObject
                    {
                        ["name"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Asset name filter (supports wildcards)."
                        },
                        ["type"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Unity type filter (e.g., 'Material', 'Texture2D', 'GameObject')."
                        },
                        ["path"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Asset path filter (supports wildcards)."
                        },
                        ["labels"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["description"] = "Asset label filters.",
                            ["items"] = new JsonObject { ["type"] = "string" }
                        },
                        ["maxResults"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Maximum number of results to return (default 50).",
                            ["minimum"] = 1,
                            ["maximum"] = 1000
                        }
                    }
                }),
            new McpToolDefinition(
                "assets.import",
                "Forces Unity to reimport an asset by path.",
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
                            ["description"] = "Unity asset path to reimport."
                        }
                    }
                }),
            new McpToolDefinition(
                "assets.ping",
                "Highlights an asset in the Unity Project window.",
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
                            ["description"] = "Unity asset path to highlight."
                        }
                    }
                }),
            new McpToolDefinition(
                "assets.reveal",
                "Shows an asset in the Unity Project window and selects it.",
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
                            ["description"] = "Unity asset path to reveal."
                        }
                    }
                }),
            new McpToolDefinition(
                "assets.createFolder",
                "Creates a new folder in the Unity Project.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("parentFolder", "folderName"),
                    ["properties"] = new JsonObject
                    {
                        ["parentFolder"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Parent folder path (e.g., 'Assets', 'Assets/Scripts')."
                        },
                        ["folderName"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "New folder name."
                        }
                    }
                }),
            new McpToolDefinition(
                "assets.createScript",
                "Creates a new C# script in the Unity Project.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("scriptName"),
                    ["properties"] = new JsonObject
                    {
                        ["scriptName"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Name of the new C# script (without .cs extension)."
                        },
                        ["path"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Asset path where the script should be created (default: Assets)."
                        },
                        ["template"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Script template: MonoBehaviour, ScriptableObject, or Editor.",
                            ["enum"] = new JsonArray("MonoBehaviour", "ScriptableObject", "Editor")
                        }
                    }
                }),
            new McpToolDefinition(
                "assets.createMaterial",
                "Creates a new Material asset in the Unity Project.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("materialName"),
                    ["properties"] = new JsonObject
                    {
                        ["materialName"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Name of the new Material."
                        },
                        ["path"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Asset path where the material should be created (default: Assets)."
                        },
                        ["shaderName"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Shader name to use (default: Standard)."
                        }
                    }
                }),
            new McpToolDefinition(
                "assets.createPrefab",
                "Creates a new Prefab asset from a GameObject in the scene.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("gameObjectInstanceId", "prefabPath"),
                    ["properties"] = new JsonObject
                    {
                        ["gameObjectInstanceId"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Unity instance id of the GameObject to convert to a prefab."
                        },
                        ["prefabPath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Asset path for the new prefab (e.g., 'Assets/Prefabs/MyPrefab.prefab')."
                        }
                    }
                }),
            new McpToolDefinition(
                "assets.delete",
                "Deletes a Unity asset by path.",
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
                            ["description"] = "Unity asset path to delete."
                        }
                    }
                }),
            new McpToolDefinition(
                "assets.move",
                "Moves or renames a Unity asset.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("oldPath", "newPath"),
                    ["properties"] = new JsonObject
                    {
                        ["oldPath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Current Unity asset path."
                        },
                        ["newPath"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "New Unity asset path."
                        }
                    }
                }),
            new McpToolDefinition(
                "assets.createScriptableObject",
                "Creates a new ScriptableObject asset in the Unity Project.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("typeName", "assetName"),
                    ["properties"] = new JsonObject
                    {
                        ["typeName"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Full type name of the ScriptableObject to create."
                        },
                        ["assetName"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Name for the new ScriptableObject asset."
                        },
                        ["path"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Asset path where the ScriptableObject should be created (default: Assets)."
                        }
                    }
                })
        };
    }
}
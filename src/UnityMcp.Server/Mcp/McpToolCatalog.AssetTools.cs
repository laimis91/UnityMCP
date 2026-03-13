using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp;

public sealed partial class McpToolCatalog
{
    private static IEnumerable<McpToolDefinition> GetAssetTools()
    {
        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
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
            });
    }
}

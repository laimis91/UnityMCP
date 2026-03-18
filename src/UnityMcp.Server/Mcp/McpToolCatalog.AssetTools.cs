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

        yield return new McpToolDefinition(
            "textureImporter.getSettings",
            "Get the import settings for a texture asset.",
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
                        ["description"] = "Asset path of the texture (e.g. Assets/Textures/MyTexture.png)."
                    }
                }
            });

        yield return new McpToolDefinition(
            "textureImporter.setSettings",
            "Modify import settings for a texture asset.",
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
                        ["description"] = "Asset path of the texture (e.g. Assets/Textures/MyTexture.png)."
                    },
                    ["textureType"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Texture type. Values: Default, NormalMap, Editor GUI and Legacy GUI, Sprite, Cursor, Cookie, Lightmap, Single Channel."
                    },
                    ["maxTextureSize"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["description"] = "Maximum texture size. Must be a power of 2: 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384."
                    },
                    ["textureCompression"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Compression mode. Values: Uncompressed, Compressed, CompressedHQ, CompressedLQ."
                    },
                    ["filterMode"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Filter mode. Values: Point, Bilinear, Trilinear."
                    },
                    ["wrapMode"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Wrap mode. Values: Repeat, Clamp, Mirror, MirrorOnce."
                    },
                    ["mipmapEnabled"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Whether to generate mipmaps."
                    },
                    ["isReadable"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Whether the texture is readable from scripts at runtime (CPU access)."
                    },
                    ["sRGBTexture"] = new JsonObject
                    {
                        ["type"] = "boolean",
                        ["description"] = "Whether the texture uses sRGB color space."
                    },
                    ["alphaSource"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Alpha source. Values: None, FromInput, FromGrayScale."
                    },
                    ["npotScale"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Non-power-of-two scaling. Values: None, ToNearest, ToLarger, ToSmaller."
                    },
                    ["anisoLevel"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["minimum"] = 0,
                        ["maximum"] = 16,
                        ["description"] = "Anisotropic filtering level (0-16)."
                    },
                    ["spriteMode"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["description"] = "Sprite mode: 0=None, 1=Single, 2=Multiple."
                    },
                    ["spritePixelsPerUnit"] = new JsonObject
                    {
                        ["type"] = "number",
                        ["description"] = "Pixels per unit for sprite mode."
                    }
                }
            });
    }
}

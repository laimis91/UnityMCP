using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp;

public sealed partial class McpToolCatalog
{
    private static IEnumerable<McpToolDefinition> GetEditorTools()
    {
        yield return new McpToolDefinition(
            "editor.getPlayModeState",
            "Returns the current Unity Editor play mode and compilation state.",
            EmptyObjectSchema());

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "editor.enterPlayMode",
            "Requests the Unity Editor to enter play mode.",
            EmptyObjectSchema());

        yield return new McpToolDefinition(
            "editor.exitPlayMode",
            "Requests the Unity Editor to exit play mode.",
            EmptyObjectSchema());

        yield return new McpToolDefinition(
            "editor.clearConsole",
            "Clears all Unity Editor console log entries.",
            EmptyObjectSchema());

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "editor.undo",
            "Performs an undo operation in the Unity Editor.",
            EmptyObjectSchema());

        yield return new McpToolDefinition(
            "editor.redo",
            "Performs a redo operation in the Unity Editor.",
            EmptyObjectSchema());

        yield return new McpToolDefinition(
            "editor.getTags",
            "Returns all tags defined in the Unity project.",
            EmptyObjectSchema());

        yield return new McpToolDefinition(
            "editor.getLayers",
            "Returns all layers defined in the Unity project.",
            EmptyObjectSchema());

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
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
            });

        yield return new McpToolDefinition(
            "editor.getUndoHistory",
            "Returns the current undo group name and group index.",
            EmptyObjectSchema());

        yield return new McpToolDefinition(
            "editor.recompileScripts",
            "Requests Unity to recompile all scripts via CompilationPipeline.RequestScriptCompilation().",
            EmptyObjectSchema());

        yield return new McpToolDefinition(
            "editor.captureSceneView",
            "Captures the Unity Editor Scene View as a base64-encoded PNG image.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JsonObject
                {
                    ["width"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["minimum"] = 1,
                        ["description"] = "Capture width in pixels (default 1920)."
                    },
                    ["height"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["minimum"] = 1,
                        ["description"] = "Capture height in pixels (default 1080)."
                    }
                }
            });

        yield return new McpToolDefinition(
            "editor.captureGameView",
            "Captures the Unity Editor Game View as a base64-encoded PNG image. Only works in Play mode.",
            new JsonObject
            {
                ["type"] = "object",
                ["additionalProperties"] = false,
                ["properties"] = new JsonObject
                {
                    ["width"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["minimum"] = 1,
                        ["description"] = "Capture width in pixels (uses game view size if not specified)."
                    },
                    ["height"] = new JsonObject
                    {
                        ["type"] = "integer",
                        ["minimum"] = 1,
                        ["description"] = "Capture height in pixels (uses game view size if not specified)."
                    }
                }
            });
    }
}

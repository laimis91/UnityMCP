using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp.ToolDefinitions;

internal static class EditorToolDefinitions
{
    internal static McpToolDefinition[] GetTools()
    {
        return new[]
        {
            new McpToolDefinition(
                "editor.getPlayModeState",
                "Returns the current Unity Editor play mode and compilation state.",
                McpToolSchemaHelpers.EmptyObjectSchema()),
            new McpToolDefinition(
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
                }),
            new McpToolDefinition(
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
                }),
            new McpToolDefinition(
                "editor.enterPlayMode",
                "Requests the Unity Editor to enter play mode.",
                McpToolSchemaHelpers.EmptyObjectSchema()),
            new McpToolDefinition(
                "editor.exitPlayMode",
                "Requests the Unity Editor to exit play mode.",
                McpToolSchemaHelpers.EmptyObjectSchema()),
            new McpToolDefinition(
                "editor.clearConsole",
                "Clears the Unity Editor console.",
                McpToolSchemaHelpers.EmptyObjectSchema()),
            new McpToolDefinition(
                "editor.pausePlayMode",
                "Pauses or unpauses Unity Editor play mode.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("paused"),
                    ["properties"] = new JsonObject
                    {
                        ["paused"] = new JsonObject
                        {
                            ["type"] = "boolean",
                            ["description"] = "Whether play mode should be paused."
                        }
                    }
                }),
            new McpToolDefinition(
                "editor.undo",
                "Performs an undo operation in the Unity Editor.",
                McpToolSchemaHelpers.EmptyObjectSchema()),
            new McpToolDefinition(
                "editor.redo",
                "Performs a redo operation in the Unity Editor.",
                McpToolSchemaHelpers.EmptyObjectSchema()),
            new McpToolDefinition(
                "editor.getTags",
                "Returns all Unity tags.",
                McpToolSchemaHelpers.EmptyObjectSchema()),
            new McpToolDefinition(
                "editor.getLayers",
                "Returns all Unity layers.",
                McpToolSchemaHelpers.EmptyObjectSchema()),
            new McpToolDefinition(
                "editor.addTag",
                "Adds a new Unity tag.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("tag"),
                    ["properties"] = new JsonObject
                    {
                        ["tag"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "New tag name to add."
                        }
                    }
                }),
            new McpToolDefinition(
                "editor.removeTag",
                "Removes a Unity tag.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("tag"),
                    ["properties"] = new JsonObject
                    {
                        ["tag"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Tag name to remove."
                        }
                    }
                }),
            new McpToolDefinition(
                "editor.addLayer",
                "Adds a new Unity layer.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("layerName"),
                    ["properties"] = new JsonObject
                    {
                        ["layerName"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "New layer name to add."
                        }
                    }
                }),
            new McpToolDefinition(
                "editor.removeLayer",
                "Removes a Unity layer.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new JsonArray("layerIndex"),
                    ["properties"] = new JsonObject
                    {
                        ["layerIndex"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Layer index to remove (8-31 for user layers).",
                            ["minimum"] = 8,
                            ["maximum"] = 31
                        }
                    }
                }),
            new McpToolDefinition(
                "editor.getUndoHistory",
                "Returns the Unity Editor undo history.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new JsonObject
                    {
                        ["maxCount"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Maximum number of undo entries to return (default 20).",
                            ["minimum"] = 1,
                            ["maximum"] = 100
                        }
                    }
                }),
            new McpToolDefinition(
                "editor.recompileScripts",
                "Forces Unity to recompile scripts.",
                McpToolSchemaHelpers.EmptyObjectSchema()),
            new McpToolDefinition(
                "editor.captureSceneView",
                "Captures a screenshot of the Unity Scene view.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new JsonObject
                    {
                        ["width"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Capture width in pixels (uses scene view size if not specified)."
                        },
                        ["height"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Capture height in pixels (uses scene view size if not specified)."
                        }
                    }
                }),
            new McpToolDefinition(
                "editor.captureGameView",
                "Captures a screenshot of the Unity Game view.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new JsonObject
                    {
                        ["width"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Capture width in pixels (uses game view size if not specified)."
                        },
                        ["height"] = new JsonObject
                        {
                            ["type"] = "integer",
                            ["description"] = "Capture height in pixels (uses game view size if not specified)."
                        }
                    }
                })
        };
    }
}
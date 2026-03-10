using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp.ToolDefinitions;

internal static class TestRunnerToolDefinitions
{
    internal static McpToolDefinition[] GetTools()
    {
        return new[]
        {
            new McpToolDefinition(
                "testRunner.listTests",
                "Returns available Unity tests in the project.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new JsonObject
                    {
                        ["testMode"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Test mode to list (EditMode or PlayMode).",
                            ["enum"] = new JsonArray("EditMode", "PlayMode")
                        }
                    }
                }),
            new McpToolDefinition(
                "testRunner.run",
                "Runs Unity tests with optional filtering.",
                new JsonObject
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["properties"] = new JsonObject
                    {
                        ["testMode"] = new JsonObject
                        {
                            ["type"] = "string",
                            ["description"] = "Test mode to run (EditMode or PlayMode).",
                            ["enum"] = new JsonArray("EditMode", "PlayMode")
                        },
                        ["testNames"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["description"] = "Specific test names to run (default: all tests).",
                            ["items"] = new JsonObject { ["type"] = "string" }
                        },
                        ["categories"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["description"] = "Test categories to include.",
                            ["items"] = new JsonObject { ["type"] = "string" }
                        },
                        ["assemblyNames"] = new JsonObject
                        {
                            ["type"] = "array",
                            ["description"] = "Assembly names to include.",
                            ["items"] = new JsonObject { ["type"] = "string" }
                        }
                    }
                }),
            new McpToolDefinition(
                "testRunner.getResults",
                "Returns results from the last test run.",
                McpToolSchemaHelpers.EmptyObjectSchema()),
            new McpToolDefinition(
                "testRunner.cancel",
                "Cancels any running tests.",
                McpToolSchemaHelpers.EmptyObjectSchema())
        };
    }
}
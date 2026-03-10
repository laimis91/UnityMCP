using System.Text.Json.Nodes;
using UnityMcp.Server.Mcp.ToolDefinitions;

namespace UnityMcp.Server.Mcp;

public sealed class McpToolCatalog
{
    private readonly Dictionary<string, McpToolDefinition> _byName;

    public McpToolCatalog()
    {
        var tools = new List<McpToolDefinition>();

        // Aggregate all tool definitions from domain-specific files
        tools.AddRange(SceneToolDefinitions.GetTools());
        tools.AddRange(EditorToolDefinitions.GetTools());
        tools.AddRange(PhysicsToolDefinitions.GetTools());
        tools.AddRange(JointsToolDefinitions.GetTools());
        tools.AddRange(AssetsToolDefinitions.GetTools());
        tools.AddRange(AudioToolDefinitions.GetTools());
        tools.AddRange(RenderersToolDefinitions.GetTools());
        tools.AddRange(MaterialToolDefinitions.GetTools());
        tools.AddRange(ProjectSettingsToolDefinitions.GetTools());
        tools.AddRange(UIToolDefinitions.GetTools());
        tools.AddRange(NavMeshToolDefinitions.GetTools());
        tools.AddRange(AnimatorToolDefinitions.GetTools());
        tools.AddRange(CameraToolDefinitions.GetTools());
        tools.AddRange(ParticleSystemToolDefinitions.GetTools());
        tools.AddRange(PrefabToolDefinitions.GetTools());
        tools.AddRange(TestRunnerToolDefinitions.GetTools());
        tools.AddRange(BuildToolDefinitions.GetTools());
        tools.AddRange(LightToolDefinitions.GetTools());
        tools.AddRange(TerrainToolDefinitions.GetTools());
        tools.AddRange(TimeToolDefinitions.GetTools());

        var toolsArray = tools.ToArray();
        Tools = toolsArray;
        _byName = toolsArray.ToDictionary(tool => tool.Name, StringComparer.Ordinal);
    }

    public IReadOnlyList<McpToolDefinition> Tools { get; }

    public bool TryGet(string name, out McpToolDefinition definition)
    {
        return _byName.TryGetValue(name, out definition!);
    }
}

public sealed record McpToolDefinition(string Name, string Description, JsonObject InputSchema);
using System.Text.Json.Nodes;

namespace UnityMcp.Server.Mcp;

public sealed partial class McpToolCatalog
{
    private readonly Dictionary<string, McpToolDefinition> _byName;

    public McpToolCatalog()
    {
        var tools = new List<McpToolDefinition>();
        tools.AddRange(GetSceneTools());
        tools.AddRange(GetEditorTools());
        tools.AddRange(GetAudioTools());
        tools.AddRange(GetCameraLightTools());
        tools.AddRange(GetAnimatorTools());
        tools.AddRange(GetAssetTools());
        tools.AddRange(GetPrefabTools());
        tools.AddRange(GetRendererTools());
        tools.AddRange(GetPhysicsTools());
        tools.AddRange(GetMaterialTools());
        tools.AddRange(GetMiscTools());

        Tools = tools;
        _byName = tools.ToDictionary(tool => tool.Name, StringComparer.Ordinal);
    }

    public IReadOnlyList<McpToolDefinition> Tools { get; }

    public bool TryGet(string name, out McpToolDefinition definition)
    {
        return _byName.TryGetValue(name, out definition!);
    }
}

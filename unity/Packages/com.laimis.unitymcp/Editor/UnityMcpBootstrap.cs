#nullable enable

using UnityEditor;
using UnityEngine;

namespace UnityMcp.Editor
{

[InitializeOnLoad]
internal static class UnityMcpBootstrap
{
    static UnityMcpBootstrap()
    {
        UnityMcpConsoleLogBuffer.EnsureInitialized();
        EditorApplication.delayCall += StartBridgeOnDelayCall;
        AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        EditorApplication.quitting += OnEditorQuitting;
    }

    [MenuItem("Tools/Unity MCP/Connect")]
    private static void Connect()
    {
        UnityMcpClient.Instance.Start();
        Debug.Log($"[UnityMCP] Connect requested. Endpoint: {UnityMcpSettings.GetServerUriString()}");
    }

    [MenuItem("Tools/Unity MCP/Disconnect")]
    private static void Disconnect()
    {
        UnityMcpClient.Instance.Stop();
        Debug.Log("[UnityMCP] Disconnect requested.");
    }

    private static void OnBeforeAssemblyReload()
    {
        UnityMcpClient.Instance.Stop();
    }

    private static void OnEditorQuitting()
    {
        UnityMcpClient.Instance.Stop();
    }

    private static void StartBridgeOnDelayCall()
    {
        UnityMcpClient.Instance.Start();
    }
}
}

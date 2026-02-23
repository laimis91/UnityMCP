#nullable enable

using UnityEditor;
using UnityEngine;

namespace UnityMcp.Editor
{

internal sealed class UnityMcpSettingsWindow : EditorWindow
{
    private string _serverUri = string.Empty;

    [MenuItem("Tools/Unity MCP/Settings")]
    private static void Open()
    {
        var window = GetWindow<UnityMcpSettingsWindow>("Unity MCP Settings");
        window.minSize = new Vector2(520f, 180f);
        window.Show();
    }

    private void OnEnable()
    {
        _serverUri = UnityMcpSettings.GetServerUriString();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Unity MCP Connection", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        EditorGUILayout.HelpBox(
            "Configure the Unity Editor bridge WebSocket endpoint. Changes apply to new connection attempts. If currently connected, use Disconnect then Connect.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        _serverUri = EditorGUILayout.TextField("Server URI", _serverUri);
        if (EditorGUI.EndChangeCheck())
        {
            // Keep local state only until Save is clicked.
        }

        EditorGUILayout.Space(8f);

        if (UnityMcpSettings.TryValidateServerUri(_serverUri, out var _, out var validationError))
        {
            EditorGUILayout.HelpBox("URI is valid.", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox(validationError ?? "URI is invalid.", MessageType.Warning);
        }

        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Save"))
            {
                TrySave();
            }

            if (GUILayout.Button("Reset Default"))
            {
                UnityMcpSettings.ResetServerUri();
                _serverUri = UnityMcpSettings.GetServerUriString();
                Debug.Log($"[UnityMCP] Server URI reset to default: {_serverUri}");
            }
        }
    }

    private void TrySave()
    {
        try
        {
            UnityMcpSettings.SetServerUriString(_serverUri);
            _serverUri = UnityMcpSettings.GetServerUriString();
            Debug.Log($"[UnityMCP] Server URI saved: {_serverUri}");
        }
        catch (System.ArgumentException ex)
        {
            EditorUtility.DisplayDialog("Unity MCP Settings", ex.Message, "OK");
        }
    }
}
}

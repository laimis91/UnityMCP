#nullable enable

using System;
using UnityEditor;

namespace UnityMcp.Editor
{

internal static class UnityMcpSettings
{
    private const string ServerUriKey = "UnityMcp.ServerUri";
    private const string DefaultServerUri = "ws://127.0.0.1:5001/ws/unity";

    public static string GetServerUriString()
    {
        var value = EditorPrefs.GetString(ServerUriKey, DefaultServerUri)?.Trim();
        return string.IsNullOrWhiteSpace(value) ? DefaultServerUri : value!;
    }

    public static void SetServerUriString(string value)
    {
        if (!TryValidateServerUri(value, out var normalized, out var error))
        {
            throw new ArgumentException(error);
        }

        EditorPrefs.SetString(ServerUriKey, normalized!.ToString());
    }

    public static void ResetServerUri()
    {
        EditorPrefs.SetString(ServerUriKey, DefaultServerUri);
    }

    public static bool TryGetServerUri(out Uri? uri, out string? error)
    {
        return TryValidateServerUri(GetServerUriString(), out uri, out error);
    }

    public static bool TryValidateServerUri(string? value, out Uri? uri, out string? error)
    {
        uri = null;
        error = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            error = "Server URI cannot be empty.";
            return false;
        }

        var trimmedValue = value!.Trim();

        if (!Uri.TryCreate(trimmedValue, UriKind.Absolute, out var parsed))
        {
            error = "Server URI must be a valid absolute URI.";
            return false;
        }

        if (parsed == null)
        {
            error = "Server URI must be a valid absolute URI.";
            return false;
        }

        if (!string.Equals(parsed.Scheme, "ws", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(parsed.Scheme, "wss", StringComparison.OrdinalIgnoreCase))
        {
            error = "Server URI scheme must be 'ws' or 'wss'.";
            return false;
        }

        uri = parsed;
        return true;
    }
}
}

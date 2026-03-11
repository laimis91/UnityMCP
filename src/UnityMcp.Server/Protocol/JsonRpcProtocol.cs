using System.Text.Json;
using System.Text.Json.Nodes;

namespace UnityMcp.Server.Protocol;

public static class JsonRpcProtocol
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    public static bool TryParse(string json, out JsonDocument? document, out string? errorMessage)
    {
        document = null;
        errorMessage = null;

        try
        {
            document = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public static bool TryGetMethod(JsonElement root, out string? method)
    {
        method = null;

        if (!root.TryGetProperty("method", out var methodElement))
        {
            return false;
        }

        if (methodElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        method = methodElement.GetString();
        return !string.IsNullOrWhiteSpace(method);
    }

    public static bool TryGetId(JsonElement root, out JsonNode? idNode, out string? idKey)
    {
        idNode = null;
        idKey = null;

        if (!root.TryGetProperty("id", out var idElement))
        {
            return false;
        }

        if (!TryGetIdKey(idElement, out var key))
        {
            return false;
        }

        idKey = key;
        idNode = idElement.ValueKind == JsonValueKind.Null ? null : JsonNode.Parse(idElement.GetRawText());
        return true;
    }

    private static bool TryGetIdKey(JsonElement idElement, out string? idKey)
    {
        idKey = null;

        switch (idElement.ValueKind)
        {
            case JsonValueKind.String:
                idKey = $"s:{idElement.GetString()}";
                return true;
            case JsonValueKind.Number:
                idKey = $"n:{idElement.GetRawText()}";
                return true;
            case JsonValueKind.Null:
                idKey = "null";
                return true;
            default:
                return false;
        }
    }

    public static bool IsResponse(JsonElement root)
    {
        return root.ValueKind == JsonValueKind.Object &&
               root.TryGetProperty("id", out _) &&
               (root.TryGetProperty("result", out _) || root.TryGetProperty("error", out _));
    }

    public static string CreateResult(JsonNode? idNode, JsonNode resultNode)
    {
        var envelope = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = idNode?.DeepClone(),
            ["result"] = resultNode.DeepClone()
        };

        return envelope.ToJsonString(SerializerOptions);
    }

    public static string CreateError(JsonNode? idNode, int code, string message, JsonNode? data = null)
    {
        var errorObject = new JsonObject
        {
            ["code"] = code,
            ["message"] = message
        };

        if (data is not null)
        {
            errorObject["data"] = data.DeepClone();
        }

        var envelope = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = idNode?.DeepClone(),
            ["error"] = errorObject
        };

        return envelope.ToJsonString(SerializerOptions);
    }
}


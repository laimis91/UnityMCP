#nullable enable

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace UnityMcp.Editor
{

internal static class UnityMcpProtocol
{
    public static bool TryParse(string json, out JObject document, out string error)
    {
        try
        {
            var token = JToken.Parse(json);
            if (token is not JObject rootObject)
            {
                document = null!;
                error = "JSON-RPC payload must be a JSON object.";
                return false;
            }

            document = rootObject;
            error = string.Empty;
            return true;
        }
        catch (JsonReaderException ex)
        {
            document = null!;
            error = ex.Message;
            return false;
        }
    }

    public static bool TryGetMethod(JObject root, out string method)
    {
        method = string.Empty;

        if (!root.TryGetValue("method", out var methodToken) || methodToken.Type != JTokenType.String)
        {
            return false;
        }

        method = methodToken.Value<string>() ?? string.Empty;
        return method.Length > 0;
    }

    public static bool TryGetId(JObject root, out JToken idToken)
    {
        idToken = null!;

        if (!root.TryGetValue("id", out var rawIdToken))
        {
            return false;
        }

        switch (rawIdToken.Type)
        {
            case JTokenType.String:
            case JTokenType.Integer:
            case JTokenType.Float:
            case JTokenType.Null:
                idToken = rawIdToken.DeepClone();
                return true;
            default:
                return false;
        }
    }

    public static string CreateResult(JToken idToken, object resultPayload)
    {
        var envelope = new JObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = idToken?.DeepClone() ?? JValue.CreateNull(),
            ["result"] = resultPayload != null ? JToken.FromObject(resultPayload) : JValue.CreateNull()
        };

        return envelope.ToString(Formatting.None);
    }

    public static string CreateError(JToken idToken, int code, string message)
    {
        var envelope = new JObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = idToken?.DeepClone() ?? JValue.CreateNull(),
            ["error"] = new JObject
            {
                ["code"] = code,
                ["message"] = message
            }
        };

        return envelope.ToString(Formatting.None);
    }
}
}

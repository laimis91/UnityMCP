using System.Net.WebSockets;
using System.Text;

namespace UnityMcp.Server.Transport;

public static class WebSocketTextMessageReader
{
    public static async Task<string?> ReadAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        using var stream = new MemoryStream();

        while (socket.State == WebSocketState.Open)
        {
            var segment = new ArraySegment<byte>(buffer);
            var result = await socket.ReceiveAsync(segment, cancellationToken);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                continue;
            }

            stream.Write(buffer, 0, result.Count);

            if (result.EndOfMessage)
            {
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        return null;
    }
}


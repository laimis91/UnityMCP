namespace UnityMcp.Server.Tests.Protocol;

public sealed class JsonRpcProtocolTests
{
    [Fact]
    public void TryParse_ReturnsDocument_WhenJsonIsValid()
    {
        // Arrange
        const string json = """{"jsonrpc":"2.0","id":1,"method":"editor.getPlayModeState"}""";

        // Act
        var success = JsonRpcProtocol.TryParse(json, out var document, out var errorMessage);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);
        Assert.NotNull(document);

        using (document)
        {
            Assert.True(JsonRpcProtocol.TryGetMethod(document!.RootElement, out var method));
            Assert.Equal("editor.getPlayModeState", method);
            Assert.True(JsonRpcProtocol.TryGetId(document.RootElement, out var idNode, out var idKey));
            Assert.Equal("n:1", idKey);
            Assert.Equal("1", idNode!.ToJsonString());
        }
    }

    [Fact]
    public void TryGetId_ReturnsFalse_WhenIdHasUnsupportedType()
    {
        // Arrange
        using var document = JsonDocument.Parse("""{"jsonrpc":"2.0","id":{"nested":true},"method":"ping"}""");

        // Act
        var success = JsonRpcProtocol.TryGetId(document.RootElement, out var idNode, out var idKey);

        // Assert
        Assert.False(success);
        Assert.Null(idNode);
        Assert.Null(idKey);
    }

    [Fact]
    public void CreateError_ReturnsJsonRpcErrorEnvelope()
    {
        // Arrange
        var idNode = JsonNode.Parse("42");

        // Act
        var json = JsonRpcProtocol.CreateError(idNode, JsonRpcErrorCodes.UnityNotConnected, "Unity is not connected.");

        // Assert
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString());
        Assert.Equal(42, root.GetProperty("id").GetInt32());
        Assert.Equal(JsonRpcErrorCodes.UnityNotConnected, root.GetProperty("error").GetProperty("code").GetInt32());
        Assert.Equal("Unity is not connected.", root.GetProperty("error").GetProperty("message").GetString());
    }
}


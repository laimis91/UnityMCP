namespace UnityMcp.Server.Tests.Routing;

public sealed class PendingRequestStoreTests
{
    [Fact]
    public async Task TryRegister_TryComplete_CompletesPendingTask()
    {
        // Arrange
        var store = new PendingRequestStore();

        // Act
        var registered = store.TryRegister("n:7", out var completionSource);
        var completed = store.TryComplete("n:7", """{"jsonrpc":"2.0","id":7,"result":{"ok":true}}""");
        var payload = await completionSource.Task;

        // Assert
        Assert.True(registered);
        Assert.True(completed);
        Assert.Contains(@"""id"":7", payload);
    }

    [Fact]
    public void TryRegister_ReturnsFalse_WhenDuplicateIdIsUsed()
    {
        // Arrange
        var store = new PendingRequestStore();

        // Act
        var first = store.TryRegister("s:abc", out _);
        var second = store.TryRegister("s:abc", out _);

        // Assert
        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public async Task FailAll_FaultsAllPendingRequests()
    {
        // Arrange
        var store = new PendingRequestStore();
        store.TryRegister("n:1", out var one);
        store.TryRegister("n:2", out var two);
        var exception = new InvalidOperationException("Unity disconnected.");

        // Act
        store.FailAll(exception);

        // Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => one.Task);
        await Assert.ThrowsAsync<InvalidOperationException>(() => two.Task);
    }
}


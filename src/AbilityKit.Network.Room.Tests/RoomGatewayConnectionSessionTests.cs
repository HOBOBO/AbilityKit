using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Room;
using AbilityKit.Network.Transport.InMemory;
using Xunit;

namespace AbilityKit.Network.Room.Tests;

public sealed class RoomGatewayConnectionSessionTests
{
    [Fact]
    public async Task ConnectAsync_ExposesSdkAndRoomClient_AndOwnsTheirLifetime()
    {
        var (clientTransport, serverTransport) = InMemoryTransport.CreateConnectedPair();
        using (serverTransport)
        {
            var session = await RoomGatewayConnectionSession.ConnectAsync(
                "localhost", 1, () => clientTransport, timeout: TimeSpan.FromSeconds(1));
            Assert.True(session.SdkClient.IsConnected);
            Assert.NotNull(session.RoomClient);

            session.Dispose();
            session.Dispose();
            Assert.False(clientTransport.IsConnected);
            Assert.Throws<ObjectDisposedException>(() => session.Tick(0));
        }
    }

    [Fact]
    public async Task ConnectAsync_TimesOutAndDisposesUnconnectedTransport()
    {
        var transport = new SilentTransport();
        await Assert.ThrowsAsync<TimeoutException>(() => RoomGatewayConnectionSession.ConnectAsync(
            "localhost", 1, () => transport, timeout: TimeSpan.FromMilliseconds(50)));
        Assert.True(transport.Disposed);
    }

    [Fact]
    public async Task ConnectAsync_CancellationPropagatesAndDisposesTransport()
    {
        var transport = new SilentTransport();
        using var cancellation = new CancellationTokenSource();
        var pending = RoomGatewayConnectionSession.ConnectAsync(
            "localhost", 1, () => transport, timeout: TimeSpan.FromSeconds(1),
            cancellationToken: cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(transport.Disposed);
    }

    private sealed class SilentTransport : ITransport
    {
        public bool Disposed { get; private set; }
        public bool IsConnected => false;
        public event Action? Connected;
        public event Action? Disconnected;
        public event Action<Exception>? Error;
        public event Action<ArraySegment<byte>>? BytesReceived;
        public void Connect(string host, int port) { }
        public void Send(ArraySegment<byte> bytes) { }
        public void Close() { }
        public void Dispose() => Disposed = true;
    }
}

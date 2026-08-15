using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Network;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Host;
using AbilityKit.Network.Host.InProcess;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using Xunit;

namespace AbilityKit.Host.Network.Tests;

public sealed class HostNetworkConnectionManagerTests
{
    [Fact]
    public async Task InProcessSession_AttachesRoutesSendsAndDisconnects()
    {
        var listener = new InProcessChannelListener();
        var requestHandler = new RecordingRequestHandler();
        using var manager = new HostNetworkConnectionManager(
            () => listener,
            new TestMessageCodec(),
            requestHandler);
        var runtime = new HostRuntime(new TestWorldManager());
        var connected = new TaskCompletionSource<IServerConnection>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var disconnected = new TaskCompletionSource<ServerClientId>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        manager.OnClientConnected += connection => connected.TrySetResult(connection);
        manager.OnClientDisconnected += id => disconnected.TrySetResult(id);
        manager.Attach(runtime);
        manager.Start();

        var client = new ConnectionManager(() => listener.CreateClientTransport());
        client.Open("inprocess", 1);
        var serverConnection = await connected.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("1", serverConnection.ClientId.Value);

        var requestHandled = requestHandler.Handled.Task;
        client.Send(21, Bytes(4), (ushort)NetworkPacketFlags.Request, 31);
        var request = await requestHandled.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("1", request.ClientId.Value);
        Assert.Equal(21u, request.Header.OpCode);
        Assert.Equal(new byte[] { 4 }, request.Payload);

        var pushed = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.ServerPushReceived += (opCode, payload) =>
        {
            if (opCode == 99) pushed.TrySetResult(payload.ToArray());
        };
        runtime.Broadcast(new TestMessage(Bytes(8, 9)));
        Assert.Equal(new byte[] { 8, 9 }, await pushed.Task.WaitAsync(TimeSpan.FromSeconds(2)));

        client.Close();
        var closedId = await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("1", closedId.Value);
        Assert.Empty(manager.Connections);
        client.Dispose();
    }

    private static ArraySegment<byte> Bytes(params byte[] bytes) => new(bytes);

    private sealed class TestMessage : ServerMessage
    {
        public TestMessage(ArraySegment<byte> payload) => Payload = payload;
        public ArraySegment<byte> Payload { get; }
    }

    private sealed class TestMessageCodec : IHostMessageCodec
    {
        public bool TryEncode(
            ServerMessage message,
            out NetworkPacketHeader header,
            out ArraySegment<byte> payload)
        {
            if (message is TestMessage test)
            {
                payload = test.Payload;
                header = new NetworkPacketHeader(
                    NetworkPacketFlags.ServerPush,
                    99,
                    0,
                    (uint)payload.Count);
                return true;
            }
            header = default;
            payload = default;
            return false;
        }
    }

    private sealed class RecordingRequestHandler : IHostNetworkRequestHandler
    {
        public TaskCompletionSource<RequestRecord> Handled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Handle(
            HostRuntime runtime,
            ServerClientId clientId,
            IServerNetworkSession session,
            NetworkPacketHeader header,
            ArraySegment<byte> payload)
        {
            Handled.TrySetResult(new RequestRecord(clientId, header, payload.ToArray()));
        }
    }

    private sealed record RequestRecord(
        ServerClientId ClientId,
        NetworkPacketHeader Header,
        byte[] Payload);

    private sealed class TestWorldManager : IWorldManager
    {
        public IReadOnlyDictionary<WorldId, IWorld> Worlds { get; } =
            new Dictionary<WorldId, IWorld>();
        public IWorld Create(WorldCreateOptions options) => throw new NotSupportedException();
        public bool TryGet(WorldId id, out IWorld world) { world = null!; return false; }
        public bool Destroy(WorldId id) => false;
        public void Tick(float deltaTime) { }
        public void DisposeAll() { }
    }
}

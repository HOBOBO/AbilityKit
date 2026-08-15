using System.Collections.Concurrent;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Ability.Host.Network;
using AbilityKit.Ability.Host.Transport;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.Management;
using AbilityKit.Network.Host;
using AbilityKit.Network.Host.InProcess;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using Xunit;

namespace AbilityKit.Host.Network.Tests;

public sealed class HostNetworkAdvancedTests
{
    [Fact]
    public async Task TryBindClient_ReplacesTemporaryIdentityUsedByRuntimeBroadcast()
    {
        var listener = new InProcessChannelListener();
        using var manager = new HostNetworkConnectionManager(
            () => listener,
            new TestMessageCodec());
        var sentTo = new ConcurrentQueue<ServerClientId>();
        var runtime = CreateRuntime(id => sentTo.Enqueue(id));
        var connected = new TaskCompletionSource<HostNetworkServerConnection>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var rebound = new TaskCompletionSource<(ServerClientId OldId, ServerClientId NewId)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        manager.OnClientConnected += value => connected.TrySetResult((HostNetworkServerConnection)value);
        manager.OnClientRebound += (oldId, newId) => rebound.TrySetResult((oldId, newId));
        manager.Attach(runtime);
        manager.Start();
        using var client = Connect(listener);
        var connection = await connected.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var authenticated = new ServerClientId("account-42");
        Assert.True(manager.TryBindClient(connection.Session.Id, authenticated));
        var identityChange = await rebound.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("1", identityChange.OldId.Value);
        Assert.Equal("account-42", identityChange.NewId.Value);
        Assert.Equal("account-42", connection.ClientId.Value);
        Assert.True(connection.Session.Context.IsEstablished);

        runtime.Broadcast(new TestMessage(Bytes(7)));
        Assert.Equal(new[] { "account-42" }, sentTo.Select(value => value.Value));
    }

    [Fact]
    public async Task TryBindClient_RejectsIdentityAlreadyOwnedByAnotherSession()
    {
        var listener = new InProcessChannelListener();
        using var manager = new HostNetworkConnectionManager(
            () => listener,
            new TestMessageCodec());
        var connected = new ConcurrentQueue<HostNetworkServerConnection>();
        var bothConnected = Completion();
        manager.OnClientConnected += value =>
        {
            connected.Enqueue((HostNetworkServerConnection)value);
            if (connected.Count == 2) bothConnected.TrySetResult();
        };
        manager.Attach(CreateRuntime());
        manager.Start();
        using var firstClient = Connect(listener);
        using var secondClient = Connect(listener);
        await bothConnected.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var sessions = connected.OrderBy(value => value.Session.Id).ToArray();
        var authenticated = new ServerClientId("shared-account");

        Assert.True(manager.TryBindClient(sessions[0].Session.Id, authenticated));
        Assert.False(manager.TryBindClient(sessions[1].Session.Id, authenticated));
        Assert.Equal("shared-account", sessions[0].ClientId.Value);
        Assert.Equal("2", sessions[1].ClientId.Value);
        Assert.Equal(2, manager.Connections.Count);
    }

    [Fact]
    public async Task AsyncHostHandler_IsSerializedAndSeesReboundIdentity()
    {
        var listener = new InProcessChannelListener();
        var firstStarted = Completion();
        var releaseFirst = Completion();
        var completed = Completion();
        var records = new ConcurrentQueue<string>();
        var handler = new DelegateAsyncHostHandler(async (_, clientId, _, header, _, token) =>
        {
            records.Enqueue($"start:{clientId.Value}:{header.Seq}");
            if (header.Seq == 1)
            {
                firstStarted.TrySetResult();
                await releaseFirst.Task.WaitAsync(token);
            }
            records.Enqueue($"end:{clientId.Value}:{header.Seq}");
            if (header.Seq == 2) completed.TrySetResult();
        });
        using var manager = HostNetworkConnectionManager.CreateAsync(
            () => listener,
            new TestMessageCodec(),
            handler);
        var connected = new TaskCompletionSource<HostNetworkServerConnection>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        manager.OnClientConnected += value => connected.TrySetResult((HostNetworkServerConnection)value);
        manager.Attach(CreateRuntime());
        manager.Start();
        using var client = Connect(listener);
        var server = await connected.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(manager.TryBindClient(server.Session.Id, new ServerClientId("player-9")));

        client.Send(10, Bytes(1), (ushort)NetworkPacketFlags.Request, 1);
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        client.Send(10, Bytes(2), (ushort)NetworkPacketFlags.Request, 2);
        await Task.Delay(30);
        Assert.Single(records);
        releaseFirst.TrySetResult();
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(
            new[] { "start:player-9:1", "end:player-9:1", "start:player-9:2", "end:player-9:2" },
            records.ToArray());
    }

    [Fact]
    public async Task ManagerTick_ForwardsIdleMaintenance()
    {
        var clock = new FakeClock();
        var listener = new InProcessChannelListener();
        using var manager = new HostNetworkConnectionManager(
            () => listener,
            new TestMessageCodec(),
            networkOptions: new NetworkHostOptions
            {
                Clock = clock,
                IdleTimeout = TimeSpan.FromSeconds(5)
            });
        var disconnected = Completion();
        manager.OnClientDisconnected += _ => disconnected.TrySetResult();
        manager.Attach(CreateRuntime());
        manager.Start();
        using var client = Connect(listener);

        clock.Advance(TimeSpan.FromSeconds(5));
        manager.Tick();

        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Empty(manager.Connections);
        Assert.Equal(1, manager.GetDiagnostics().IdleTimeouts);
    }

    [Fact]
    public async Task InProcessComposition_CreatesAReadyClientWithoutExposingListenerWiring()
    {
        using var network = new InProcessHostNetwork(new TestMessageCodec());
        network.Connections.Attach(CreateRuntime());
        var connected = Completion();
        network.Connections.OnClientConnected += _ => connected.TrySetResult();
        network.Start();
        using var client = network.CreateClientConnection(new ConnectionOptions
        {
            EnableReconnect = false,
            HeartbeatInterval = TimeSpan.Zero,
            HeartbeatTimeout = TimeSpan.Zero
        });

        client.Open("inprocess", 1);
        await connected.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(network.Connections.IsListening);
        Assert.Single(network.Connections.Connections);
        Assert.Equal(1, network.GetDiagnostics().AcceptedSessions);
    }

    [Fact]
    public async Task StopAsync_DrainsHostHandlerAndRetainsTerminalDiagnostics()
    {
        var listener = new InProcessChannelListener();
        var started = Completion();
        var release = Completion();
        var handler = new DelegateAsyncHostHandler(async (_, _, _, _, _, token) =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(token);
        });
        using var manager = HostNetworkConnectionManager.CreateAsync(
            () => listener,
            new TestMessageCodec(),
            handler);
        manager.Attach(CreateRuntime());
        manager.Start();
        using var client = Connect(listener);
        client.Send(10, Bytes(1), (ushort)NetworkPacketFlags.Request, 1);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var stopping = manager.StopAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(30);
        Assert.False(stopping.IsCompleted);
        release.TrySetResult();
        await stopping.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Empty(manager.Connections);
        Assert.Equal(1, manager.GetDiagnostics().GracefulStops);
        Assert.Equal(1, manager.GetDiagnostics().RequestsCompleted);
    }

    [Fact]
    public async Task InProcessComposition_CanRestartWithANewListener()
    {
        using var network = new InProcessHostNetwork(new TestMessageCodec());
        network.Connections.Attach(CreateRuntime());
        network.Start();
        using (var first = network.CreateClientConnection())
        {
            first.Open("inprocess", 1);
            Assert.Single(network.Connections.Connections);
        }

        network.Stop();
        network.Start();
        using var second = network.CreateClientConnection();
        second.Open("inprocess", 1);

        Assert.True(network.Connections.IsListening);
        Assert.Single(network.Connections.Connections);
    }

    private static HostRuntime CreateRuntime(Action<ServerClientId>? beforeSend = null)
    {
        HostRuntimeOptions? options = null;
        if (beforeSend != null)
        {
            options = new HostRuntimeOptions
            {
                OnBeforeSendMessage = (clientId, _) => beforeSend(clientId)
            };
        }
        return options == null
            ? new HostRuntime(new TestWorldManager())
            : new HostRuntime(new TestWorldManager(), options);
    }

    private static ConnectionManager Connect(InProcessChannelListener listener)
    {
        var client = new ConnectionManager(() => listener.CreateClientTransport(), new ConnectionOptions
        {
            EnableReconnect = false,
            HeartbeatInterval = TimeSpan.Zero,
            HeartbeatTimeout = TimeSpan.Zero
        });
        client.Open("inprocess", 1);
        return client;
    }

    private static ArraySegment<byte> Bytes(params byte[] bytes) => new(bytes);
    private static TaskCompletionSource Completion() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class DelegateAsyncHostHandler : IAsyncHostNetworkRequestHandler
    {
        private readonly Func<HostRuntime, ServerClientId, IServerNetworkSession, NetworkPacketHeader,
            ArraySegment<byte>, CancellationToken, Task> _handle;

        public DelegateAsyncHostHandler(
            Func<HostRuntime, ServerClientId, IServerNetworkSession, NetworkPacketHeader,
                ArraySegment<byte>, CancellationToken, Task> handle)
        {
            _handle = handle;
        }

        public Task HandleAsync(
            HostRuntime runtime,
            ServerClientId clientId,
            IServerNetworkSession session,
            NetworkPacketHeader header,
            ArraySegment<byte> payload,
            CancellationToken cancellationToken)
        {
            return _handle(runtime, clientId, session, header, payload, cancellationToken);
        }
    }

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
                header = new NetworkPacketHeader(NetworkPacketFlags.ServerPush, 99, 0, (uint)payload.Count);
                return true;
            }
            header = default;
            payload = default;
            return false;
        }
    }

    private sealed class FakeClock : IMonotonicClock
    {
        public long Timestamp { get; private set; }
        public long Frequency => TimeSpan.TicksPerSecond;
        public void Advance(TimeSpan duration) => Timestamp += duration.Ticks;
    }

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

using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Battle.Config;
using AbilityKit.Network.Client;
using AbilityKit.Network.Room;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Sdk;
using AbilityKit.Network.Transport.InMemory;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Network.Client.Tests;

public sealed class MultiplayerClientSessionLifecycleTests
{
    private static readonly MultiplayerRoomSessionResult Session = new(
        "token", "room", "battle", 1, 1, default, subscribed: false);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StagedDispose_AttemptsRoomAfterBattleFailure_AndDoesNotRetry(bool roomFails)
    {
        var order = new List<string>();
        var battleError = new InvalidOperationException("battle dispose failed");
        var roomError = roomFails ? new InvalidOperationException("room dispose failed") : null;
        var room = new TestConnection(() => order.Add("room"), disposeError: roomError);
        var battle = new TestConnection(() => order.Add("battle"), disposeError: battleError);
        using var client = new MultiplayerClientSession(CreateRoomClient(room), Session, "127.0.0.1", 1);
        client.AttachBattle((config, _) => ConfigureBattle(config, battle), connect: false);

        var error = Record.Exception(client.Dispose);

        if (roomError == null)
            Assert.Same(battleError, error);
        else
            Assert.Equal(new Exception[] { battleError, roomError }, Assert.IsType<AggregateException>(error).InnerExceptions);
        Assert.Null(client.Battle);
        client.Dispose();
        Assert.Equal(new[] { "battle", "room" }, order);
    }

    [Fact]
    public void StagedAttach_ConnectAndCleanupFailure_PreservesErrorsAndRoomOwnership()
    {
        var connectError = new InvalidOperationException("connect failed");
        var cleanupError = new InvalidOperationException("dispose failed");
        var room = new TestConnection();
        var battle = new TestConnection(openError: connectError, disposeError: cleanupError);
        using var client = new MultiplayerClientSession(CreateRoomClient(room), Session, "127.0.0.1", 1);

        var error = Assert.Throws<AggregateException>(() => client.AttachBattle((config, _) => ConfigureBattle(config, battle)));

        Assert.Equal(new Exception[] { connectError, cleanupError }, error.InnerExceptions);
        Assert.Equal(1, battle.Disposals);
        Assert.Equal(0, room.Disposals);
        Assert.Null(client.Battle);

        var retry = new TestConnection();
        Assert.Same(client.AttachBattle((config, _) => ConfigureBattle(config, retry), connect: false), client.Battle);
        client.Dispose();
        Assert.Equal(1, retry.Disposals);
        Assert.Equal(1, room.Disposals);
    }

    [Fact]
    public async Task GroupedEntry_AttachFailure_ClosesBothPlanes()
    {
        using var gateway = new RoomGateway();
        var expected = new InvalidOperationException("battle connect failed");
        var battle = new TestConnection(openError: expected);
        var options = gateway.Options((config, _) => ConfigureBattle(config, battle));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => MultiplayerClientSession.EnterAsync(options));

        Assert.Same(expected, error);
        Assert.False(gateway.Transport.IsConnected);
        Assert.Equal(1, battle.Disposals);
    }

    [Fact]
    public async Task GroupedEntry_CancelledAtRoomHandoff_ClosesRoomWithoutAttachingBattle()
    {
        using var gateway = new RoomGateway();
        using var cancellation = new CancellationTokenSource();
        var options = gateway.Options((_, _) => throw new InvalidOperationException("unexpected battle attach"));
        options.RoomConnection.CancellationToken = cancellation.Token;
        options.RoomFlow.AfterReadyAndBeforeBattleStart = (_, _, _, _, _) =>
        {
            cancellation.Cancel();
            return Task.CompletedTask;
        };

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MultiplayerClientSession.EnterAsync(options));

        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.False(gateway.Transport.IsConnected);
    }

    [Fact]
    public async Task GroupedEntry_CancelledDuringBattleOpen_ClosesBothPlanesBeforeHandoff()
    {
        using var gateway = new RoomGateway();
        using var cancellation = new CancellationTokenSource();
        var battle = new TestConnection(onOpen: cancellation.Cancel);
        var options = gateway.Options((config, _) => ConfigureBattle(config, battle));
        options.RoomConnection.CancellationToken = cancellation.Token;

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MultiplayerClientSession.EnterAsync(options));

        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.False(gateway.Transport.IsConnected);
        Assert.Equal(1, battle.Disposals);
    }

    private static NetworkSdkClient CreateRoomClient(TestConnection room) =>
        new NetworkSdkBuilder().UseOwnedConnectionFactory(() => room).Build();

    private static void ConfigureBattle(NetworkBattleConfig config, TestConnection connection) =>
        config.WithInjectedConnection(() => connection).WithInputSerializer(_ => default, _ => default);

    private sealed class TestConnection(
        Action? onDispose = null, Exception? openError = null, Exception? disposeError = null,
        Action? onOpen = null) : IConnection
    {
        public int Disposals { get; private set; }
        public ConnectionState State => ConnectionState.Disconnected;
        public bool IsConnected => false;
        public event Action Connected { add { } remove { } }
        public event Action Disconnected { add { } remove { } }
        public event Action<Exception> Error { add { } remove { } }
        public event Action<uint, uint, ArraySegment<byte>> PacketReceived { add { } remove { } }
        public event Action<uint, ArraySegment<byte>> ServerPushReceived { add { } remove { } }
        public event Action<string, string> Kicked { add { } remove { } }
        public void Open(string host, int port)
        {
            if (openError != null) throw openError;
            onOpen?.Invoke();
        }
        public void Close() { }
        public void Tick(float deltaTime) { }
        public void Send(uint opCode, ArraySegment<byte> payload, ushort flags = 0, uint seq = 0) { }
        public void Dispose()
        {
            Disposals++;
            onDispose?.Invoke();
            if (disposeError != null) throw disposeError;
        }
    }

    private sealed class RoomGateway : IDisposable
    {
        private readonly NetworkSession _server;
        public InMemoryTransport Transport { get; }

        public RoomGateway()
        {
            (Transport, var serverTransport) = InMemoryTransport.CreateConnectedPair();
            _server = new NetworkSession(serverTransport);
            _server.PacketReceived += Reply;
            _server.Start();
        }

        public MultiplayerClientSessionOptions Options(Action<NetworkBattleConfig, MultiplayerRoomSessionResult> configure) => new()
        {
            RoomConnection = new MultiplayerRoomConnectionOptions
            {
                Host = "127.0.0.1", Port = 1, AccountId = "player", TransportFactory = () => Transport,
                Timeout = TimeSpan.FromSeconds(5),
                LaunchSpec = new RoomGatewayLaunchSpec("region", "server", "test", "test", 2, 1, 1, 1, 1, "world", "client")
            },
            RoomFlow = new MultiplayerRoomFlowOptions { JoinRoomId = "room", WaitForBattleStart = false },
            Battle = new MultiplayerBattleConnectionOptions { Configure = configure }
        };

        private void Reply(uint opCode, uint seq, ArraySegment<byte> payload)
        {
            var snapshot = new WireRoomSnapshot { CanStart = true, BattleId = "battle", WorldId = 1 };
            var response = opCode switch
            {
                RoomGatewayOpCodes.GuestLogin => WireRoomGatewayBinary.Serialize(new WireRoomGuestLoginRes
                { Success = true, SessionToken = "token", AccountId = "player", Message = string.Empty }),
                RoomGatewayOpCodes.JoinRoom => WireRoomGatewayBinary.Serialize(new WireJoinRoomRes
                { Success = true, RoomId = "room", NumericRoomId = 1, Snapshot = snapshot, CurrentPlayerId = 1 }),
                RoomGatewayOpCodes.SetReady => WireRoomGatewayBinary.Serialize(new WireRoomSnapshotRes
                { Success = true, RoomId = "room", NumericRoomId = 1, Snapshot = snapshot }),
                _ => default
            };
            if (response.Array == null) return;
            _server.Send(opCode, response, 0, seq);
        }

        public void Dispose() => _server.Dispose();
    }
}

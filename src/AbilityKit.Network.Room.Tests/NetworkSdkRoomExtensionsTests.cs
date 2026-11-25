using System.Collections.Generic;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Sdk;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Network.Room.Tests;

public sealed class NetworkSdkRoomExtensionsTests
{
    [Fact]
    public void CreateRoomClient_ReusesSdkRequestChainAndDisposesOnlyCapability()
    {
        var connection = new ObservableConnection();
        using var sdk = CreateSdk(connection);

        Assert.Equal(1, connection.PacketReceivedSubscriberCount);
        Assert.Equal(2, connection.DisconnectedSubscriberCount);
        Assert.Equal(2, connection.ErrorSubscriberCount);
        Assert.Equal(1, connection.ServerPushSubscriberCount);

        var room = sdk.CreateRoomClient();

        Assert.Equal(1, connection.PacketReceivedSubscriberCount);
        Assert.Equal(2, connection.DisconnectedSubscriberCount);
        Assert.Equal(2, connection.ErrorSubscriberCount);
        Assert.Equal(1, connection.ServerPushSubscriberCount);

        room.Dispose();
        room.Dispose();

        Assert.Equal(0, connection.CloseCount);
        Assert.Equal(0, connection.DisposeCount);
        Assert.Equal(1, connection.PacketReceivedSubscriberCount);
        Assert.Equal(1, connection.ServerPushSubscriberCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateRoomAsync_UsesSdkResponsePolicyAndRoomWireProtocol(bool useLegacyEnvelope)
    {
        var connection = new ObservableConnection();
        var builder = new NetworkSdkBuilder().UseConnectionFactory(() => connection);
        if (useLegacyEnvelope)
            builder.UseResponseEnvelopeCodec(TcpGatewayRequestResponseEnvelopeCodec.Instance);
        using var sdk = builder.Build();
        using var room = sdk.CreateRoomClient();

        var pending = room.CreateRoomAsync(new RoomGatewayCreateRequest(
            "session-1",
            "cn",
            "server-a",
            "shooter",
            "SDK Room",
            true,
            8,
            new Dictionary<string, string> { ["mode"] = "ranked" }, "create-command-1"));

        var send = Assert.Single(connection.Sends);
        Assert.Equal(RoomGatewayOpCodes.CreateRoom, send.OpCode);
        Assert.Equal((ushort)NetworkPacketFlags.Request, send.Flags);
        Assert.NotEqual(0u, send.Seq);

        var request = WireRoomGatewayBinary.Deserialize<WireCreateRoomReq>(
            new ArraySegment<byte>(send.Payload));
        Assert.Equal("session-1", request.SessionToken);
        Assert.Equal("cn", request.Region);
        Assert.Equal("server-a", request.ServerId);
        Assert.Equal("shooter", request.RoomType);
        Assert.Equal("SDK Room", request.Title);
        Assert.True(request.IsPublic);
        Assert.Equal(8, request.MaxPlayers);
        Assert.Equal("ranked", request.Tags!["mode"]);
        Assert.Equal("create-command-1", request.CommandId);

        var response = new WireCreateRoomRes
        {
            Success = true,
            RoomId = "room-1",
            NumericRoomId = 1001UL,
            Message = "created"
        };
        var body = WireRoomGatewayBinary.Serialize(in response);
        var payload = useLegacyEnvelope
            ? TcpGatewayRequestResponseEnvelopeCodec.Instance.EncodeSuccess(send.OpCode, send.Seq, body)
            : body;
        connection.CompleteResponse(send.OpCode, send.Seq, payload);

        var result = await pending;
        Assert.True(result.Success);
        Assert.Equal("room-1", result.RoomId);
        Assert.Equal(1001UL, result.NumericRoomId);
        Assert.Equal("created", result.Message);
    }

    [Fact]
    public void RoomPush_IsForwardedThroughSdkAndStopsAfterRoomDispose()
    {
        var connection = new ObservableConnection();
        using var sdk = CreateSdk(connection);
        var room = sdk.CreateRoomClient();
        var published = new List<RoomGatewaySnapshot>();
        room.SnapshotChanged += published.Add;

        connection.PushRoomSnapshot(CreateSnapshot("room-1", 7L));

        var first = Assert.Single(published);
        Assert.Equal("room-1", first.RoomId);
        Assert.Equal(7L, first.RoomRevision);
        Assert.Same(first, room.Current);

        room.Dispose();
        connection.PushRoomSnapshot(CreateSnapshot("room-1", 8L));

        Assert.Single(published);
        Assert.Null(room.Current);
    }

    [Fact]
    public async Task DisposedRoomClient_DoesNotOwnSdkAndRawRequestsStillWork()
    {
        var connection = new ObservableConnection();
        using var sdk = CreateSdk(connection);
        var room = sdk.CreateRoomClient();
        room.Dispose();

        var pending = sdk.SendRawRequestAsync(9901U, new ArraySegment<byte>(new byte[] { 1, 2 }));
        var send = Assert.Single(connection.Sends);
        connection.CompleteResponse(send.OpCode, send.Seq, new ArraySegment<byte>(new byte[] { 3, 4 }));

        var response = await pending;
        Assert.Equal(new byte[] { 3, 4 }, response.ToArray());
        Assert.Equal(0, connection.CloseCount);
        Assert.Equal(0, connection.DisposeCount);
    }

    private static NetworkSdkClient CreateSdk(ObservableConnection connection)
    {
        return new NetworkSdkBuilder()
            .UseConnectionFactory(() => connection)
            .Build();
    }

    private static WireRoomSnapshot CreateSnapshot(string roomId, long revision)
    {
        return new WireRoomSnapshot
        {
            Summary = new WireRoomSummary
            {
                Region = "cn",
                ServerId = "server-a",
                RoomId = roomId,
                RoomType = "shooter",
                Title = "SDK Room",
                MaxPlayers = 8,
                OwnerAccountId = "owner"
            },
            Members = new List<string> { "owner" },
            Players = new List<WireRoomPlayerSnapshot>(),
            RoomRevision = revision,
            Phase = (int)RoomGatewaySessionPhase.Lobby,
            PhaseReason = "ready"
        };
    }

    private sealed class ObservableConnection : IConnection
    {
        private Action? _connected;
        private Action? _disconnected;
        private Action<Exception>? _error;
        private Action<uint, uint, ArraySegment<byte>>? _packetReceived;
        private Action<uint, ArraySegment<byte>>? _serverPushReceived;
        private Action<string, string>? _kicked;

        public List<SendRecord> Sends { get; } = new();
        public ConnectionState State { get; private set; } = ConnectionState.Connected;
        public bool IsConnected => State == ConnectionState.Connected;
        public int CloseCount { get; private set; }
        public int DisposeCount { get; private set; }
        public int PacketReceivedSubscriberCount => _packetReceived?.GetInvocationList().Length ?? 0;
        public int DisconnectedSubscriberCount => _disconnected?.GetInvocationList().Length ?? 0;
        public int ErrorSubscriberCount => _error?.GetInvocationList().Length ?? 0;
        public int ServerPushSubscriberCount => _serverPushReceived?.GetInvocationList().Length ?? 0;

        public event Action? Connected
        {
            add => _connected += value;
            remove => _connected -= value;
        }

        public event Action? Disconnected
        {
            add => _disconnected += value;
            remove => _disconnected -= value;
        }

        public event Action<Exception>? Error
        {
            add => _error += value;
            remove => _error -= value;
        }

        public event Action<uint, uint, ArraySegment<byte>>? PacketReceived
        {
            add => _packetReceived += value;
            remove => _packetReceived -= value;
        }

        public event Action<uint, ArraySegment<byte>>? ServerPushReceived
        {
            add => _serverPushReceived += value;
            remove => _serverPushReceived -= value;
        }

        public event Action<string, string>? Kicked
        {
            add => _kicked += value;
            remove => _kicked -= value;
        }

        public void Open(string host, int port)
        {
            State = ConnectionState.Connected;
            _connected?.Invoke();
        }

        public void Close()
        {
            CloseCount++;
            State = ConnectionState.Disconnected;
        }

        public void Tick(float deltaTime)
        {
        }

        public void Send(uint opCode, ArraySegment<byte> payload, ushort flags = 0, uint seq = 0)
        {
            var bytes = payload.Array == null ? Array.Empty<byte>() : payload.ToArray();
            Sends.Add(new SendRecord(opCode, seq, flags, bytes));
        }

        public void CompleteResponse(uint opCode, uint seq, ArraySegment<byte> payload)
        {
            _packetReceived?.Invoke(opCode, seq, payload);
        }

        public void PushRoomSnapshot(WireRoomSnapshot snapshot)
        {
            var push = new WireRoomStateChangedPush
            {
                RoomId = snapshot.Summary.RoomId,
                Snapshot = snapshot,
                ServerNowTicks = 1234L
            };
            var payload = WireRoomGatewayBinary.Serialize(in push);
            _serverPushReceived?.Invoke(RoomGatewayOpCodes.RoomStateChanged, payload);
        }

        public void Dispose()
        {
            DisposeCount++;
            State = ConnectionState.Disconnected;
        }

        public readonly record struct SendRecord(uint OpCode, uint Seq, ushort Flags, byte[] Payload);
    }
}

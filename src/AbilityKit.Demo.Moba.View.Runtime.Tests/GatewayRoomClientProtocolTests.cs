using System;
using System.Buffers.Binary;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Protocol;
using AbilityKit.Network.Runtime.TcpGateway;
using AbilityKit.Network.Sdk;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class GatewayRoomClientProtocolTests
{
    [Fact]
    public async Task SubscribeStateSyncUsesCanonicalAuthenticatedRoomRequest()
    {
        const uint subscribeOpCode = 7301U;
        using var connection = new RecordingConnection();
        var opCodes = CreateOpCodes(subscribeOpCode);
        using var client = new GatewayRoomClient(connection, opCodes);

        var result = await client.SubscribeStateSyncAsync(
            "session-token",
            "battle-42",
            "room-42",
            timeout: TimeSpan.FromSeconds(1));

        Assert.True(result.Success);
        Assert.Equal(subscribeOpCode, connection.LastOpCode);
        var request = WireRoomGatewayBinary.Deserialize<WireSubscribeStateSyncReq>(connection.LastPayload);
        Assert.Equal("session-token", request.SessionToken);
        Assert.Equal("battle-42", request.BattleId);
        Assert.Equal("room-42", request.RoomId);
    }

    [Fact]
    public async Task SetReadyUsesConfiguredOpcodeAndPreservesSnapshotFields()
    {
        const uint setReadyOpCode = 7304U;
        using var connection = new RecordingConnection();
        var opCodes = CreateOpCodes(subscribeOpCode: 7301U, setReadyOpCode: setReadyOpCode);
        using var client = new GatewayRoomClient(connection, opCodes);

        var result = await client.SetReadyAsync("session-token", "room-42", true);

        Assert.True(result.Success);
        Assert.True(result.Applied);
        Assert.Equal(17, result.ErrorCode);
        Assert.Equal(setReadyOpCode, connection.LastOpCode);
        var request = WireRoomGatewayBinary.Deserialize<WireRoomReadyReq>(connection.LastPayload);
        Assert.Equal("session-token", request.SessionToken);
        Assert.Equal("room-42", request.RoomId);
        Assert.True(request.Ready);
    }

    [Fact]
    public async Task BeginLoadingMapsSharedSnapshotAndKeepsLoadingPayload()
    {
        const uint beginLoadingOpCode = 7310U;
        using var connection = new RecordingConnection();
        var opCodes = CreateOpCodes(subscribeOpCode: 7301U, beginLoadingOpCode: beginLoadingOpCode);
        using var client = new GatewayRoomClient(connection, opCodes);

        var result = await client.BeginLoadingAsync(
            "session-token", "room-42", 12L, "command-7");

        Assert.True(result.Success);
        Assert.Equal(13L, result.RoomRevision);
        Assert.Equal(beginLoadingOpCode, connection.LastOpCode);
        var request = WireRoomGatewayBinary.Deserialize<WireBeginLoadingReq>(connection.LastPayload);
        Assert.Equal("session-token", request.SessionToken);
        Assert.Equal("room-42", request.RoomId);
        Assert.Equal(12L, request.ExpectedRevision);
        Assert.Equal("command-7", request.CommandId);
        Assert.Equal("room-42", result.Snapshot.RoomId);
        Assert.True(result.Snapshot.Players[0].Ready);
        Assert.True(result.Snapshot.Players[0].LobbyReady);
    }

    [Fact]
    public async Task FailedOperationRetainsLegacyEmptySnapshotContract()
    {
        using var connection = new RecordingConnection { OperationSuccess = false };
        using var client = new GatewayRoomClient(connection, CreateOpCodes(7301U));

        var result = await client.CancelLoadingAsync(
            "session-token", "room-42", 12L, "command-8");

        Assert.False(result.Success);
        Assert.NotNull(result.Snapshot);
        Assert.Equal(string.Empty, result.Snapshot.RoomId);
    }

    [Fact]
    public void DisposeIsIdempotentAndDoesNotDisposeBorrowedConnection()
    {
        var connection = new RecordingConnection();
        var client = new GatewayRoomClient(connection, CreateOpCodes(7301U));

        Assert.Equal(1, connection.ServerPushSubscriberCount);
        Assert.Equal(1, connection.PacketSubscriberCount);
        client.Dispose();
        client.Dispose();

        Assert.Equal(0, connection.ServerPushSubscriberCount);
        Assert.Equal(0, connection.PacketSubscriberCount);
        Assert.Equal(0, connection.DisposeCount);
        connection.Dispose();
    }

    [Fact]
    public void SdkBackedClientUsesSingleRequestOwnerAndDoesNotOwnSdk()
    {
        var connection = new RecordingConnection();
        var sdkClient = new NetworkSdkBuilder()
            .UseConnectionFactory(() => connection)
            .Build();
        var client = new GatewayRoomClient(sdkClient, CreateOpCodes(7301U));

        Assert.Equal(1, connection.PacketSubscriberCount);
        Assert.Equal(1, connection.ServerPushSubscriberCount);

        client.Dispose();
        client.Dispose();

        Assert.Equal(1, connection.PacketSubscriberCount);
        Assert.Equal(1, connection.ServerPushSubscriberCount);
        Assert.Equal(0, connection.DisposeCount);

        sdkClient.Dispose();
        sdkClient.Dispose();

        Assert.Equal(0, connection.PacketSubscriberCount);
        Assert.Equal(0, connection.ServerPushSubscriberCount);
        Assert.Equal(1, connection.DisposeCount);
    }

    private static GatewayRoomOpCodes CreateOpCodes(
        uint subscribeOpCode,
        uint setReadyOpCode = 7104U,
        uint beginLoadingOpCode = 7108U)
    {
        return new GatewayRoomOpCodes(
            createRoom: 7101U,
            joinRoom: 7102U,
            subscribeStateSync: subscribeOpCode,
            setReady: setReadyOpCode,
            pickHero: 7105U,
            startBattle: 7106U,
            submitBattleInput: 7107U,
            snapshotPushed: 7201U,
            deltaSnapshotPushed: 7202U,
            beginLoading: beginLoadingOpCode,
            reportAssetsLoaded: 7109U,
            cancelLoading: 7110U,
            getSnapshot: 7111U,
            restoreRoom: 7112U,
            roomStateChanged: 7113U);
    }

    private sealed class RecordingConnection : IConnection
    {
        public ConnectionState State => ConnectionState.Connected;
        public bool IsConnected => true;
        public uint LastOpCode { get; private set; }
        public ArraySegment<byte> LastPayload { get; private set; }
        public bool OperationSuccess { get; set; } = true;
        public int DisposeCount { get; private set; }
        public int PacketSubscriberCount { get; private set; }
        public int ServerPushSubscriberCount { get; private set; }

        public event Action? Connected;
        public event Action? Disconnected;
        public event Action<Exception>? Error { add { } remove { } }
        public event Action<uint, uint, ArraySegment<byte>>? PacketReceived
        {
            add { _packetReceived += value; PacketSubscriberCount++; }
            remove { _packetReceived -= value; PacketSubscriberCount--; }
        }
        private event Action<uint, uint, ArraySegment<byte>>? _packetReceived;
        public event Action<uint, ArraySegment<byte>>? ServerPushReceived
        {
            add { ServerPushSubscriberCount++; }
            remove { ServerPushSubscriberCount--; }
        }
        public event Action<string, string>? Kicked { add { } remove { } }

        public void Open(string host, int port) => Connected?.Invoke();
        public void Close() => Disconnected?.Invoke();
        public void Tick(float deltaTime) { }

        public void Send(uint opCode, ArraySegment<byte> payload, ushort flags = 0, uint seq = 0)
        {
            LastOpCode = opCode;
            LastPayload = Copy(payload);

            ArraySegment<byte> responsePayload;
            if (opCode == 7301U)
            {
                var response = new WireSubscribeStateSyncRes
                {
                    Success = true,
                    Message = "subscribed"
                };
                responsePayload = WireRoomGatewayBinary.Serialize(in response);
            }
            else if (opCode == 7304U)
            {
                var response = new WireRoomSnapshotRes
                {
                    Success = true,
                    Applied = true,
                    ErrorCode = 17,
                    RoomId = "room-42",
                    NumericRoomId = 42U,
                    Message = "ready",
                    Snapshot = CreateSnapshot()
                };
                responsePayload = WireRoomGatewayBinary.Serialize(in response);
            }
            else if (opCode == 7310U)
            {
                var response = new WireRoomOperationRes
                {
                    Success = true,
                    Applied = true,
                    RoomRevision = 13L,
                    Message = "loading",
                    Snapshot = CreateSnapshot()
                };
                responsePayload = WireRoomGatewayBinary.Serialize(in response);
            }
            else
            {
                var response = new WireRoomOperationRes
                {
                    Success = OperationSuccess,
                    Applied = false,
                    ErrorCode = 99,
                    Message = "failed"
                };
                responsePayload = WireRoomGatewayBinary.Serialize(in response);
            }

            _packetReceived?.Invoke(opCode, seq, EncodeGatewayResponse(TcpGatewayStatusCode.Ok, responsePayload));
        }

        public void Dispose() => DisposeCount++;

        private static WireRoomSnapshot CreateSnapshot()
        {
            return new WireRoomSnapshot
            {
                Summary = new WireRoomSummary { RoomId = "room-42" },
                RoomRevision = 13L,
                CanStart = true,
                Players = new List<WireRoomPlayerSnapshot>
                {
                    new WireRoomPlayerSnapshot { AccountId = "player-1", Ready = true, LobbyReady = true }
                }
            };
        }

        private static ArraySegment<byte> Copy(ArraySegment<byte> source)
        {
            if (source.Array == null || source.Count == 0)
            {
                return default;
            }

            var copy = new byte[source.Count];
            Buffer.BlockCopy(source.Array, source.Offset, copy, 0, source.Count);
            return new ArraySegment<byte>(copy);
        }

        private static ArraySegment<byte> EncodeGatewayResponse(
            TcpGatewayStatusCode statusCode,
            ArraySegment<byte> payload)
        {
            var bytes = new byte[sizeof(int) + payload.Count];
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0, sizeof(int)), (int)statusCode);
            if (payload.Array != null && payload.Count > 0)
            {
                Buffer.BlockCopy(payload.Array, payload.Offset, bytes, sizeof(int), payload.Count);
            }

            return new ArraySegment<byte>(bytes);
        }
    }
}

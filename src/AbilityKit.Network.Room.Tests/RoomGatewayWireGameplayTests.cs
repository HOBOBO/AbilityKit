using System;
using System.Threading;
using System.Threading.Tasks;
using AbilityKit.Network.Room;
using AbilityKit.Protocol;
using AbilityKit.Protocol.Generated;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Network.Room.Tests;

public sealed class RoomGatewayWireGameplayTests
{
    [Fact]
    public async Task AccountLoginAsync_UsesProtocolDescriptorAndDecodesResponse()
    {
        var transport = new FakeTransport
        {
            Response = WireRoomGatewayBinary.Serialize(new WireRoomAccountLoginRes
            {
                Success = true, AccountId = "player", SessionToken = "token"
            })
        };
        using var client = new RoomGatewayWireSessionClient(transport);

        var result = await client.AccountLoginAsync("player", kickExisting: true);

        Assert.True(result.Success);
        Assert.Equal("token", result.SessionToken);
        Assert.Equal(ProtocolMessageDescriptor<WireRoomAccountLoginReq>.RequireOpCode(
            ProtocolDirection.ClientToServer), transport.LastOpCode);
        Assert.Equal(CatalogOpCode("account-login.request"), transport.LastOpCode);
        var request = WireRoomGatewayBinary.Deserialize<WireRoomAccountLoginReq>(transport.LastPayload);
        Assert.Equal("player", request.AccountId);
        Assert.True(request.KickExisting);
    }

    [Fact]
    public async Task SubmitBattleInputAsync_PreservesPayloadAndSequence()
    {
        var transport = new FakeTransport
        {
            Response = WireRoomGatewayBinary.Serialize(new WireSubmitBattleInputRes
            {
                Success = true, AcceptedFrame = 12
            })
        };
        using var client = new RoomGatewayWireSessionClient(transport);
        var input = new WireSubmitBattleInputReq
        {
            SessionToken = "token", BattleId = "battle", WorldId = 3,
            PlayerId = 2, InputOpCode = 17, Payload = new byte[] { 1, 2, 3 },
            CommandSequence = 8
        };

        var result = await client.SubmitBattleInputAsync(input);

        Assert.Equal(12, result.AcceptedFrame);
        Assert.Equal(ProtocolMessageDescriptor<WireSubmitBattleInputReq>.RequireOpCode(
            ProtocolDirection.ClientToServer), transport.LastOpCode);
        Assert.Equal(CatalogOpCode("submit-battle-input.request"), transport.LastOpCode);
        var request = WireRoomGatewayBinary.Deserialize<WireSubmitBattleInputReq>(transport.LastPayload);
        Assert.Equal(input.Payload, request.Payload);
        Assert.Equal(8ul, request.CommandSequence);
    }

    [Fact]
    public void StateSyncPush_DecodesKnownOpcodesOnly()
    {
        var transport = new FakeTransport();
        using var client = new RoomGatewayWireSessionClient(transport, transport);
        var received = 0;
        client.StateSyncSnapshotReceived += snapshot =>
        {
            Assert.Equal(15, snapshot.Frame);
            received++;
        };
        var payload = WireRoomGatewayBinary.Serialize(new WireStateSyncSnapshotPush
        {
            WorldId = 3, Frame = 15, IsFullSnapshot = true
        });

        transport.Push(RoomGatewayOpCodes.SnapshotPushed, payload);
        transport.Push(RoomGatewayOpCodes.DeltaSnapshotPushed, payload);
        transport.Push(99999, payload);

        Assert.Equal(2, received);
        Assert.Equal(RoomGatewayOpCodes.SnapshotPushed, CatalogOpCode("state-sync-snapshot.push"));
        Assert.Equal(RoomGatewayOpCodes.DeltaSnapshotPushed, CatalogOpCode("state-sync-delta.push"));
    }

    private static uint CatalogOpCode(string messageId)
    {
        var catalog = BuiltInProtocolCatalogs.CreateRegistry();
        Assert.True(catalog.TryGetMessage("abilitykit.room", messageId, out var message));
        return message!.OpCode;
    }

    private sealed class FakeTransport : IRoomGatewayRequestTransport, IRoomGatewayPushSource
    {
        public ArraySegment<byte> Response;
        public uint LastOpCode;
        public ArraySegment<byte> LastPayload;
        public event Action<uint, ArraySegment<byte>>? ServerPushReceived;

        public Task<ArraySegment<byte>> SendRequestAsync(uint opCode, ArraySegment<byte> payload,
            TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            LastOpCode = opCode;
            LastPayload = payload;
            return Task.FromResult(Response);
        }

        public void Push(uint opCode, ArraySegment<byte> payload) => ServerPushReceived?.Invoke(opCode, payload);
    }
}

using System.Collections.Generic;
using System.Threading.Tasks;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

public sealed class ShooterRoomGatewayRoomClientTests
{
    [Fact]
    public async Task RoomGatewayRoomClientCanCancelLoading()
    {
        var transport = new RecordingShooterRoomFlowTransport();
        using var roomClient = new ShooterRoomGatewayRoomClient(transport);
        transport.SetResponse(new WireRoomOperationRes
        {
            Success = true,
            Applied = true,
            RoomRevision = 12L,
            Message = "cancelled"
        });

        var result = await roomClient.CancelLoadingAsync(
            new ShooterGatewayCancelLoadingRequest("session-token", "room-1", 11L, "cancel-1"));

        Assert.Equal(RoomGatewayOpCodes.CancelLoading, transport.LastOpCode);
        var wire = WireRoomGatewayBinary.Deserialize<WireCancelLoadingReq>(transport.LastPayload);
        Assert.Equal("session-token", wire.SessionToken);
        Assert.Equal("room-1", wire.RoomId);
        Assert.Equal(11L, wire.ExpectedRevision);
        Assert.Equal("cancel-1", wire.CommandId);
        Assert.True(result.Success);
        Assert.True(result.Applied);
        Assert.Equal(12L, result.RoomRevision);
    }

    [Fact]
    public async Task RoomGatewayRoomClientCanLeaveAuthoritativeRoom()
    {
        var transport = new RecordingShooterRoomFlowTransport();
        var roomClient = new ShooterRoomGatewayRoomClient(transport);
        transport.SetResponse(new WireRoomOperationRes
        {
            Success = true,
            Applied = true,
            RoomRevision = 8L,
            Message = "left"
        });

        var result = await roomClient.LeaveRoomAsync(
            new ShooterGatewayLeaveRoomRequest("session-token", "room-1", 7L, "leave-1"));

        Assert.Equal(RoomGatewayOpCodes.LeaveRoom, transport.LastOpCode);
        var wire = WireRoomGatewayBinary.Deserialize<WireLeaveRoomReq>(transport.LastPayload);
        Assert.Equal("session-token", wire.SessionToken);
        Assert.Equal("room-1", wire.RoomId);
        Assert.Equal(7L, wire.ExpectedRevision);
        Assert.Equal("leave-1", wire.CommandId);
        Assert.True(result.Success);
        Assert.True(result.Applied);
        Assert.Equal(8L, result.RoomRevision);
        Assert.Equal("left", result.Message);
    }

    [Fact]
    public async Task RoomGatewayRoomClientUsesGenericRoomLifecycleProtocol()
    {
        var transport = new RecordingShooterRoomFlowTransport();
        var roomClient = new ShooterRoomGatewayRoomClient(transport);

        transport.SetResponse(new WireCreateRoomRes
        {
            Success = true,
            RoomId = "room-1",
            NumericRoomId = 1001ul,
            Message = "created"
        });
        var create = await roomClient.CreateRoomAsync(new ShooterGatewayCreateRoomRequest(
            "session-token",
            "cn",
            "server-a",
            "shooter",
            "Shooter Room",
            isPublic: true,
            maxPlayers: 4,
            tags: new Dictionary<string, string> { ["mode"] = "duo" }));
        Assert.Equal(RoomGatewayOpCodes.CreateRoom, transport.LastOpCode);
        var createWire = WireRoomGatewayBinary.Deserialize<WireCreateRoomReq>(transport.LastPayload);
        Assert.Equal("session-token", createWire.SessionToken);
        Assert.Equal("cn", createWire.Region);
        Assert.Equal("server-a", createWire.ServerId);
        Assert.Equal("shooter", createWire.RoomType);
        Assert.Equal("Shooter Room", createWire.Title);
        Assert.True(createWire.IsPublic);
        Assert.Equal(4, createWire.MaxPlayers);
        Assert.NotNull(createWire.Tags);
        Assert.Equal("duo", createWire.Tags!["mode"]);
        Assert.True(create.Success);
        Assert.Equal("room-1", create.RoomId);
        Assert.Equal(1001ul, create.NumericRoomId);
        Assert.Equal("created", create.Message);

        transport.SetResponse(new WireJoinRoomRes
        {
            Success = true,
            RoomId = "room-1",
            NumericRoomId = 1001ul,
            Snapshot = new WireRoomSnapshot { BattleId = "battle-1", CanStart = true, WorldId = 9001ul },
            WorldStartAnchor = new WireWorldStartAnchor
            {
                StartServerTicks = 123456L,
                ServerTickFrequency = 10000000L,
                StartFrame = 12,
                FixedDeltaSeconds = 1d / 30d
            },
            Message = "joined",
            JoinKind = WireRoomJoinKind.Reconnect,
            ServerNowTicks = 523456L,
            CurrentPlayerId = 121u
        });
        var join = await roomClient.JoinRoomAsync(new ShooterGatewayJoinRoomRequest("session-token", "cn", "server-a", "room-1"));
        Assert.Equal(RoomGatewayOpCodes.JoinRoom, transport.LastOpCode);
        var joinWire = WireRoomGatewayBinary.Deserialize<WireJoinRoomReq>(transport.LastPayload);
        Assert.Equal("session-token", joinWire.SessionToken);
        Assert.Equal("cn", joinWire.Region);
        Assert.Equal("server-a", joinWire.ServerId);
        Assert.Equal("room-1", joinWire.RoomId);
        Assert.True(join.Success);
        Assert.Equal("battle-1", join.BattleId);
        Assert.True(join.CanStart);
        Assert.Equal(ShooterGatewayRoomJoinKind.Reconnect, join.JoinKind);
        Assert.Equal(523456L, join.ServerNowTicks);
        Assert.Equal(9001ul, join.WorldId);
        Assert.Equal(121u, join.CurrentPlayerId);
        Assert.Equal(123456L, join.WorldStartAnchor.StartServerTicks);
        Assert.Equal(12, join.WorldStartAnchor.StartFrame);

        transport.SetResponse(new WireRoomSnapshotRes
        {
            Success = true,
            RoomId = "room-1",
            NumericRoomId = 1001ul,
            Snapshot = new WireRoomSnapshot { BattleId = "battle-1", CanStart = true },
            Message = "ready"
        });
        var ready = await roomClient.SetReadyAsync(new ShooterGatewayReadyRequest("session-token", "room-1", ready: true));
        Assert.Equal(RoomGatewayOpCodes.SetReady, transport.LastOpCode);
        var readyWire = WireRoomGatewayBinary.Deserialize<WireRoomReadyReq>(transport.LastPayload);
        Assert.Equal("session-token", readyWire.SessionToken);
        Assert.Equal("room-1", readyWire.RoomId);
        Assert.True(readyWire.Ready);
        Assert.True(ready.Success);
        Assert.Equal("battle-1", ready.BattleId);
        Assert.True(ready.CanStart);

        transport.SetResponse(new WireStartRoomBattleRes
        {
            Success = true,
            BattleId = "battle-1",
            WorldId = 9001ul,
            Started = true,
            WorldStartAnchor = new WireWorldStartAnchor
            {
                StartServerTicks = 223456L,
                ServerTickFrequency = 10000000L,
                StartFrame = 0,
                FixedDeltaSeconds = 1d / 30d
            },
            ServerNowTicks = 323456L,
            Message = "started"
        });
        var start = await roomClient.StartBattleAsync(new ShooterGatewayStartBattleRequest(
            "session-token",
            "room-1",
            2,
            1,
            3,
            1,
            "shooter-runtime",
            "client-a",
            "runtime-snapshot-interpolation",
            syncModel: 2,
            networkEnvironmentId: "wan-90ms",
            carrierName: "server",
            enableAuthoritativeWorld: true,
            interpolationEnabled: true,
            inputDelayFrames: 4));
        Assert.Equal(RoomGatewayOpCodes.StartBattle, transport.LastOpCode);
        var startWire = WireRoomGatewayBinary.Deserialize<WireStartRoomBattleReq>(transport.LastPayload);
        Assert.Equal("session-token", startWire.SessionToken);
        Assert.Equal("room-1", startWire.RoomId);
        Assert.Equal(2, startWire.GameplayId);
        Assert.Equal(1, startWire.RuleSetId);
        Assert.Equal(3, startWire.ConfigVersion);
        Assert.Equal(1, startWire.ProtocolVersion);
        Assert.Equal("shooter-runtime", startWire.WorldType);
        Assert.Equal("client-a", startWire.ClientId);
        Assert.Equal("runtime-snapshot-interpolation", startWire.SyncTemplateId);
        Assert.Equal(2, startWire.SyncModel);
        Assert.Equal("wan-90ms", startWire.NetworkEnvironmentId);
        Assert.Equal("server", startWire.CarrierName);
        Assert.True(startWire.EnableAuthoritativeWorld);
        Assert.True(startWire.InterpolationEnabled);
        Assert.Equal(4, startWire.InputDelayFrames);
        Assert.True(start.Success);
        Assert.True(start.Started);
        Assert.Equal("battle-1", start.BattleId);
        Assert.Equal(9001ul, start.WorldId);
        Assert.Equal(223456L, start.WorldStartAnchor.StartServerTicks);
        Assert.Equal(323456L, start.ServerNowTicks);

        transport.SetResponse(new WireSubscribeStateSyncRes
        {
            Success = true,
            Message = "subscribed"
        });
        var subscribe = await roomClient.SubscribeStateSyncAsync(new ShooterGatewayStateSyncSubscriptionRequest(
            "session-token",
            "battle-1",
            "room-1",
            "epoch-7",
            41L));
        Assert.Equal(RoomGatewayOpCodes.SubscribeStateSync, transport.LastOpCode);
        var subscribeWire = WireRoomGatewayBinary.Deserialize<WireSubscribeStateSyncReq>(transport.LastPayload);
        Assert.Equal("session-token", subscribeWire.SessionToken);
        Assert.Equal("battle-1", subscribeWire.BattleId);
        Assert.Equal("room-1", subscribeWire.RoomId);
        Assert.Equal("epoch-7", subscribeWire.EventEpoch);
        Assert.Equal(41L, subscribeWire.LastEventAck);
        Assert.True(subscribe.Success);
        Assert.Equal("subscribed", subscribe.Message);

        transport.SetResponse(new WireAckReliableBattleEventsRes
        {
            Success = true,
            AcceptedAckSequence = 42L,
            Message = "acknowledged"
        });
        var ack = await roomClient.AcknowledgeReliableBattleEventsAsync(
            new ShooterGatewayReliableBattleEventAckRequest("session-token", "battle-1", "room-1", "epoch-7", 42L));
        Assert.Equal(RoomGatewayOpCodes.AckReliableBattleEvents, transport.LastOpCode);
        var ackWire = WireRoomGatewayBinary.Deserialize<WireAckReliableBattleEventsReq>(transport.LastPayload);
        Assert.Equal("session-token", ackWire.SessionToken);
        Assert.Equal("battle-1", ackWire.BattleId);
        Assert.Equal("room-1", ackWire.RoomId);
        Assert.Equal("epoch-7", ackWire.Epoch);
        Assert.Equal(42L, ackWire.AckSequence);
        Assert.True(ack.Success);
        Assert.Equal(42L, ack.AcceptedAckSequence);
        Assert.Equal("acknowledged", ack.Message);

        transport.SetResponse(new WireRequestFullStateSyncRes
        {
            Success = true,
            Accepted = true,
            Message = "accepted",
            ServerTicks = 123456789L
        });
        var fullStateSync = await roomClient.RequestFullStateSyncAsync(new ShooterGatewayFullStateSyncRequest(
            "session-token",
            "battle-1",
            "room-1",
            worldId: 9001ul,
            clientFrame: 123,
            lastAuthoritativeFrame: 120,
            clientStateHash: 0xABCDEF01u,
            authoritativeStateHash: 0x12345678u,
            reason: "AuthoritativeHashMismatch"));
        Assert.Equal(RoomGatewayOpCodes.RequestFullStateSync, transport.LastOpCode);
        var fullStateSyncWire = WireRoomGatewayBinary.Deserialize<WireRequestFullStateSyncReq>(transport.LastPayload);
        Assert.Equal("session-token", fullStateSyncWire.SessionToken);
        Assert.Equal("battle-1", fullStateSyncWire.BattleId);
        Assert.Equal("room-1", fullStateSyncWire.RoomId);
        Assert.Equal(9001ul, fullStateSyncWire.WorldId);
        Assert.Equal(123, fullStateSyncWire.ClientFrame);
        Assert.Equal(120, fullStateSyncWire.LastAuthoritativeFrame);
        Assert.Equal(0xABCDEF01u, fullStateSyncWire.ClientStateHash);
        Assert.Equal(0x12345678u, fullStateSyncWire.AuthoritativeStateHash);
        Assert.Equal("AuthoritativeHashMismatch", fullStateSyncWire.Reason);
        Assert.True(fullStateSync.Success);
        Assert.True(fullStateSync.Accepted);
        Assert.Equal("accepted", fullStateSync.Message);
        Assert.Equal(123456789L, fullStateSync.ServerTicks);
    }
}

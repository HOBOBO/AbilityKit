using System;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Game.Battle.Requests;
using AbilityKit.Network.Abstractions;
using AbilityKit.Network.Battle;
using AbilityKit.Network.Battle.Config;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

/// <summary>
/// Contract tests for the MOBA battle data-plane options assembly
/// (<see cref="NetworkTransportOptionsFactory"/>). This is the MOBA network layer's first dotnet-level
/// regression net — pins the shared protocol preset (via BuildBattleOptions), the MOBA-specific input
/// serializers (framesync + statesync), snapshot/frame deserializers, and reliable-event cursor wiring.
/// </summary>
public sealed class MobaBattleNetworkOptionsContractTests
{
    private const string Host = "gw";
    private const int Port = 41001;
    private const ulong RoomId = 999UL;
    private const string Token = "token-moba";
    private const string BattleId = "battle-moba";
    private const string PublicRoomId = "room-moba";

    private static NetworkTransportOptions Build(bool useFrameSyncInput) =>
        NetworkTransportOptionsFactory.Create(
            host: Host,
            port: Port,
            transportFactory: () => new TcpTransport(),
            playerIdToUInt: pid => uint.Parse(pid.Value),
            playerIdFromUInt: n => new PlayerId(n.ToString()),
            worldIdToUlong: wid => ulong.Parse(wid.Value),
            worldIdFromUlong: n => new WorldId(n.ToString()),
            roomId: RoomId,
            sessionToken: Token,
            battleId: BattleId,
            publicRoomId: PublicRoomId,
            useFrameSyncInput: useFrameSyncInput,
            getReliableEventEpoch: () => "epoch-1",
            getReliableEventLastAcknowledgedSequence: () => 42L);

    [Fact]
    public void ProtocolPreset_RoomGatewayOpcodes_AndSession_AreApplied()
    {
        var options = Build(useFrameSyncInput: false);

        Assert.Equal((uint)RoomGatewayOpCodes.SubmitBattleInput, options.OpSubmitInput);
        Assert.Equal((uint)RoomGatewayOpCodes.SnapshotPushed, options.OpSnapshotPushed);
        Assert.Equal((uint)RoomGatewayOpCodes.DeltaSnapshotPushed, options.OpDeltaSnapshotPushed);
        Assert.Equal((uint)RoomGatewayOpCodes.RenewSession, options.OpRenewSession);
        Assert.Equal((uint)RoomGatewayOpCodes.SubscribeStateSync, options.OpPostAuthentication);
        Assert.Equal(Token, options.SessionToken);
        Assert.Equal(Host, options.Host);
        Assert.Equal(Port, options.Port);
    }

    [Fact]
    public void StatesyncInputSerializer_ProducesCorrectWireSubmitBattleInputReq()
    {
        var options = Build(useFrameSyncInput: false);

        var request = new SubmitInputRequest(
            new WorldId(RoomId.ToString()),
            new PlayerInputCommand(
                new FrameIndex(7),
                new PlayerId("123"),
                opCode: 1001,
                payload: new byte[] { 0xAB }));
        var prepared = options.PrepareSubmitInput!(request);
        var payload = options.SerializeSubmitInput!(prepared);

        var wire = WireRoomGatewayBinary.Deserialize<WireSubmitBattleInputReq>(payload);
        Assert.Equal(Token, wire.SessionToken);
        Assert.Equal(BattleId, wire.BattleId);
        Assert.Equal(RoomId, wire.WorldId);
        Assert.Equal(7, wire.Frame);
        Assert.Equal(123u, wire.PlayerId);
        Assert.Equal(1001, wire.InputOpCode);
        Assert.Equal(new byte[] { 0xAB }, wire.Payload);
    }

    [Fact]
    public void StatesyncInputDeserializer_MapsAllFields()
    {
        var options = Build(useFrameSyncInput: false);

        var wire = new WireSubmitBattleInputRes
        {
            Success = true,
            AcceptedFrame = 11,
            CurrentFrame = 12,
            Status = "Accepted",
            Message = "ok",
            ShouldResync = false,
            ServerTicks = 999L,
        };
        var payload = WireRoomGatewayBinary.Serialize(in wire);
        var response = options.DeserializeSubmitInputResponse!(payload);

        Assert.True(response.Accepted);
        Assert.Equal(12, response.ServerFrame);
        Assert.Equal(11, response.AcceptedFrame);
        Assert.Equal(999L, response.ServerTicks);
        Assert.False(response.ShouldResync);
    }

    [Fact]
    public void SnapshotDeserializer_DecodesWireStateSyncSnapshotPush()
    {
        var options = Build(useFrameSyncInput: false);

        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = RoomId,
            Frame = 42,
            Timestamp = 1.5,
            IsFullSnapshot = true,
            Actors = new List<WireStateSyncActorSnapshot>
            {
                new() { ActorId = 1, X = 10f, Y = 0f, Z = 20f, Hp = 100f, HpMax = 100f, TeamId = 1 },
            },
        };
        var payload = WireRoomGatewayBinary.Serialize(in wire);

        var snapshot = options.DeserializeSnapshotPushed!(payload);
        var gatewaySnapshot = Assert.IsType<GatewayStateSyncSnapshot>(snapshot);

        Assert.Equal(RoomId, gatewaySnapshot.WorldId);
        Assert.Equal(42, gatewaySnapshot.Frame);
        Assert.True(gatewaySnapshot.IsFullSnapshot);
        Assert.Single(gatewaySnapshot.Actors);
        Assert.Equal(1, gatewaySnapshot.Actors[0].ActorId);
        Assert.Equal(10f, gatewaySnapshot.Actors[0].X);
    }

    [Fact]
    public void ReliableEventCursor_CallbacksAreWired()
    {
        var options = Build(useFrameSyncInput: false);

        Assert.NotNull(options.GetReliableEventEpoch);
        Assert.NotNull(options.GetReliableEventLastAcknowledgedSequence);
        Assert.Equal("epoch-1", options.GetReliableEventEpoch!());
        Assert.Equal(42L, options.GetReliableEventLastAcknowledgedSequence!());
    }

    [Fact]
    public void FramesyncMode_InputSerializer_ProducesNonEmptyPayload()
    {
        var options = Build(useFrameSyncInput: true);

        var request = new SubmitInputRequest(
            new WorldId(RoomId.ToString()),
            new PlayerInputCommand(
                new FrameIndex(3),
                new PlayerId("1"),
                opCode: 500,
                payload: new byte[] { 1, 2 }));
        var prepared = options.PrepareSubmitInput!(request);
        var payload = options.SerializeSubmitInput!(prepared);

        Assert.True(payload.Count > 0);
    }
}

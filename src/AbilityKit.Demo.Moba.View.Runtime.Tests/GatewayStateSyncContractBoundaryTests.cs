using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Battle.Agent;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class GatewayStateSyncContractBoundaryTests
{
    [Fact]
    public void ContractsAreOwnedByPlatformNeutralAssembly()
    {
        var contractsAssembly = typeof(StateHashData).Assembly;

        Assert.Same(contractsAssembly, typeof(GatewayStateSyncSnapshot).Assembly);
        Assert.Same(contractsAssembly, typeof(GatewayStateSyncActorSnapshot).Assembly);
        Assert.Equal("AbilityKit.Demo.Moba.Presentation.Contracts", contractsAssembly.GetName().Name);
    }

    [Fact]
    public void GatewayMapperPreservesEveryWireField()
    {
        var payload = new byte[] { 5, 8, 13 };
        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = 101,
            Frame = 202,
            Timestamp = 3.5d,
            IsFullSnapshot = false,
            SchemaVersion = 3,
            RemovedActorIds = new List<int> { 303, 404 },
            EventWatermark = 505,
            EventEpoch = "epoch-606",
            PayloadOpCode = 707,
            Payload = payload,
            ServerTicks = 808,
            Actors = new List<WireStateSyncActorSnapshot>
            {
                new WireStateSyncActorSnapshot
                {
                    ActorId = 1,
                    X = 2f,
                    Y = 3f,
                    Z = 4f,
                    Rotation = 5f,
                    VelocityX = 6f,
                    VelocityZ = 7f,
                    Hp = 8f,
                    HpMax = 9f,
                    TeamId = 10,
                    Kind = 11,
                    Code = 12,
                    OwnerNetId = 13,
                },
            },
        };

        var actual = GatewayRoomResponseMapper.ToGatewaySnapshot(in wire);

        Assert.Equal(101ul, actual.WorldId);
        Assert.Equal(202, actual.Frame);
        Assert.Equal(3.5d, actual.Timestamp);
        Assert.False(actual.IsFullSnapshot);
        Assert.Equal(3, actual.SchemaVersion);
        Assert.Equal(new[] { 303, 404 }, actual.RemovedActorIds);
        Assert.Equal(505, actual.EventWatermark);
        Assert.Equal("epoch-606", actual.EventEpoch);
        Assert.Equal(707, actual.PayloadOpCode);
        Assert.Equal(payload, actual.Payload);
        Assert.Equal(808, actual.ServerTicks);

        var actor = Assert.Single(actual.Actors);
        Assert.Equal(1, actor.ActorId);
        Assert.Equal(2f, actor.X);
        Assert.Equal(3f, actor.Y);
        Assert.Equal(4f, actor.Z);
        Assert.Equal(5f, actor.Rotation);
        Assert.Equal(6f, actor.VelocityX);
        Assert.Equal(7f, actor.VelocityZ);
        Assert.Equal(8f, actor.Hp);
        Assert.Equal(9f, actor.HpMax);
        Assert.Equal(10, actor.TeamId);
        Assert.Equal(11, actor.Kind);
        Assert.Equal(12, actor.Code);
        Assert.Equal(13, actor.OwnerNetId);

        payload[0] = 99;
        Assert.Equal(5, actual.Payload[0]);
    }

    [Fact]
    public void MaterializedDeltaPreservesTransportMetadata()
    {
        var state = new MobaAuthoritativeSnapshotState();
        var delta = new GatewayStateSyncSnapshot(
            worldId: 11,
            frame: 12,
            timestamp: 13d,
            isFullSnapshot: false,
            actors: null,
            schemaVersion: 3,
            eventWatermark: 14,
            eventEpoch: "epoch",
            payloadOpCode: 15,
            payload: new byte[] { 16 },
            serverTicks: 17);

        var actual = state.Apply(in delta);

        Assert.True(actual.IsFullSnapshot);
        Assert.Empty(actual.Actors);
        Assert.Equal(17, actual.ServerTicks);
        Assert.Equal(14, actual.EventWatermark);
        Assert.Equal("epoch", actual.EventEpoch);
        Assert.Equal(15, actual.PayloadOpCode);
        Assert.Equal(new byte[] { 16 }, actual.Payload);
    }
}

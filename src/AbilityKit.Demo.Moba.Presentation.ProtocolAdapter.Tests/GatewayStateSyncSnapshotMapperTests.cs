using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.ProtocolAdapter.Tests;

public sealed class GatewayStateSyncSnapshotMapperTests
{
    /// <summary>
    /// 固定基线外壳与压缩投影的版本独立，解包不会丢失 Cue 或 HFSM 字节。
    /// </summary>
    [Fact]
    public void FixedBaselineDecodesBundledProjectionAndCompleteCueSet()
    {
        var generation = System.Guid.NewGuid();
        var projection = new byte[] { 15, 20, 25 };
        var baseline = MobaPresentationCueSnapshotCodec.Serialize(new[]
        {
            new MobaPresentationCueSnapshotEntry { SnapshotKind = (int)PresentationCueSnapshotKind.ActiveBaselineEnd },
        });
        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = 7,
            Frame = 10,
            IsFullSnapshot = true,
            SchemaVersion = FixedBaselineStateSyncProtocol.SchemaVersion,
            EventEpoch = "epoch",
            PayloadOpCode = MobaStateSyncCuePayloadCodec.OpCode,
            Payload = FixedBaselineStateSyncProtocol.Encode(generation, 2, 0,
                MobaStateSyncCuePayloadCodec.CarrierSchemaVersion,
                MobaStateSyncCuePayloadCodec.Encode(11017, 4, projection, baseline)),
        };
        var actual = GatewayStateSyncSnapshotDecoder.Decode(WireRoomGatewayBinary.Serialize(in wire));
        Assert.Equal(generation, actual.StateStreamGeneration);
        Assert.Equal(2, actual.SnapshotId);
        Assert.Equal(4, actual.SchemaVersion);
        Assert.Equal(11017, actual.PayloadOpCode);
        Assert.Equal(projection, actual.Payload);
        Assert.Equal(baseline, actual.PresentationCueBaseline);
    }

    [Fact]
    public void DecoderPreservesSnapshotAndActorFields()
    {
        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = 10,
            Frame = 20,
            Timestamp = 30.5d,
            IsFullSnapshot = true,
            SchemaVersion = 3,
            RemovedActorIds = new List<int> { 40 },
            EventWatermark = 50,
            EventEpoch = "epoch",
            PayloadOpCode = 60,
            Payload = new byte[] { 70 },
            ServerTicks = 80,
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
        var bytes = WireRoomGatewayBinary.Serialize(in wire);

        var actual = GatewayStateSyncSnapshotDecoder.Decode(bytes);

        Assert.Equal(10ul, actual.WorldId);
        Assert.Equal(20, actual.Frame);
        Assert.Equal(30.5d, actual.Timestamp);
        Assert.True(actual.IsFullSnapshot);
        Assert.Equal(3, actual.SchemaVersion);
        Assert.Equal(new[] { 40 }, actual.RemovedActorIds);
        Assert.Equal(50, actual.EventWatermark);
        Assert.Equal("epoch", actual.EventEpoch);
        Assert.Equal(60, actual.PayloadOpCode);
        Assert.Equal(new byte[] { 70 }, actual.Payload);
        Assert.Equal(80, actual.ServerTicks);

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
    }

    [Fact]
    public void MapperOwnsMutableWireCollections()
    {
        var payload = new byte[] { 1, 2 };
        var removed = new List<int> { 3 };
        var actors = new List<WireStateSyncActorSnapshot>
        {
            new WireStateSyncActorSnapshot { ActorId = 4 },
        };
        var wire = new WireStateSyncSnapshotPush
        {
            Payload = payload,
            RemovedActorIds = removed,
            Actors = actors,
        };

        var actual = GatewayStateSyncSnapshotMapper.Map(in wire);
        payload[0] = 99;
        removed[0] = 99;
        actors[0] = new WireStateSyncActorSnapshot { ActorId = 99 };

        Assert.Equal(new byte[] { 1, 2 }, actual.Payload);
        Assert.Equal(new[] { 3 }, actual.RemovedActorIds);
        Assert.Equal(4, Assert.Single(actual.Actors).ActorId);
    }
}

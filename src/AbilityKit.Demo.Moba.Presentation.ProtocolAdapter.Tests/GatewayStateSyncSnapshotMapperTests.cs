using System.Collections.Generic;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Protocol.Room;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.ProtocolAdapter.Tests;

public sealed class GatewayStateSyncSnapshotMapperTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ScopedMovementProof_OtherInputGenerationDoesNotDiscardAuthority(bool sameProducer)
    {
        var input = System.Guid.NewGuid();
        var stream = System.Guid.NewGuid();
        var push = new WireStateSyncSnapshotPush
        {
            WorldId = 7, Frame = 10, IsFullSnapshot = true, SchemaVersion = WireStateSyncMovementPayload.SchemaVersion,
            Actors = new List<WireStateSyncActorSnapshot> { new() { ActorId = 5, X = 9 } }
        };
        var scoped = new WireStateSyncMovementPayload
        {
            FixedBaselinePayload = FixedBaselineStateSyncProtocol.Encode(stream, 2, 0, 1, null),
            Proof = new WireStateSyncMovementProof
            {
                InputGeneration = sameProducer ? input : System.Guid.NewGuid(), PlayerId = 3, WorldId = 7,
                StreamGeneration = stream, SnapshotId = 2, Tick = 10, ActorId = 5, EntityVersion = 1,
                TerminalThrough = 4, X = 9, RotationW = 1, MoveZ = 1, MoveSpeed = 5,
                CanMove = true, HasIsolatedLocomotion = true
            }
        };

        var mapped = GatewayStateSyncSnapshotMapper.MapScoped(in push, in scoped, input, 7, 3);

        Assert.True(mapped.UsesFixedBaseline);
        Assert.Equal(2L, mapped.SnapshotId);
        Assert.Equal(9f, Assert.Single(mapped.Actors).X);
        Assert.Equal(sameProducer, mapped.MovementProof.HasValue);
        if (mapped.MovementProof is { } proof)
        {
            Assert.True(proof.Matches(in mapped));
            Assert.Equal(4L, proof.TerminalThrough);
            Assert.Equal(1f, proof.MoveZ);
        }
    }

    [Fact]
    public void FixedBaselinePreservesOverheadAndShieldEmptySetsAlongsideHfsmAndCues()
    {
        var indicators = MobaActorIndicatorSnapshotCodec.Serialize(new[] {
            new MobaActorIndicatorSnapshotEntry { ActorId = 7, LayoutId = 2, IndicatorId = 2,
                Current = 123, Max = 700, HasMax = true, Availability = 1 } });
        var shields = MobaShieldStateSnapshotCodec.Serialize(System.Array.Empty<MobaShieldStateSnapshotEntry>());
        var projection = new byte[] { 1, 2, 3 };
        var overhead = MobaStateSyncOverheadPayloadCodec.Encode(11017, 4, projection, indicators, shields);
        var cues = MobaPresentationCueSnapshotCodec.Serialize(new[] {
            new MobaPresentationCueSnapshotEntry { SnapshotKind = (int)PresentationCueSnapshotKind.ActiveBaselineEnd } });
        var wire = new WireStateSyncSnapshotPush { WorldId = 1, Frame = 12, IsFullSnapshot = true,
            SchemaVersion = FixedBaselineStateSyncProtocol.SchemaVersion,
            PayloadOpCode = MobaStateSyncCuePayloadCodec.OpCode,
            Payload = FixedBaselineStateSyncProtocol.Encode(System.Guid.NewGuid(), 1, 0,
                MobaStateSyncCuePayloadCodec.CarrierSchemaVersion,
                MobaStateSyncCuePayloadCodec.Encode(MobaStateSyncOverheadPayloadCodec.OpCode, 1, overhead, cues)) };
        var actual = GatewayStateSyncSnapshotDecoder.Decode(WireRoomGatewayBinary.Serialize(in wire));
        Assert.Equal(11017, actual.PayloadOpCode); Assert.Equal(4, actual.SchemaVersion);
        Assert.Equal(projection, actual.Payload); Assert.Equal(cues, actual.PresentationCueBaseline);
        Assert.Equal(indicators, actual.ActorIndicatorBaseline); Assert.Equal(shields, actual.ShieldStateBaseline);
        Assert.Empty(MobaShieldStateSnapshotCodec.Deserialize(actual.ShieldStateBaseline));
    }

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

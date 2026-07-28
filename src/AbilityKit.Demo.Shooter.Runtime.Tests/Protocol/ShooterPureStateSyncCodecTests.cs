using System;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;
using MemoryPack;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.Protocol;

public sealed class ShooterPureStateSyncCodecTests
{
    [Fact]
    public void TransientSerializationReusesExactLengthOutputBuffer()
    {
        var snapshot = CreateSnapshot();
        var buffer = new ReusableMemoryPackSerializationBuffer();

        var first = ShooterPureStateSyncCodec.SerializeTransient(in snapshot, buffer);
        var restored = ShooterPureStateSyncCodec.Deserialize(first);
        var second = ShooterPureStateSyncCodec.SerializeTransient(in snapshot, buffer);

        Assert.Same(first, second);
        Assert.Equal(buffer.WrittenCount, second.Length);
        Assert.Equal(snapshot.WorldId, restored.WorldId);
        Assert.Equal(snapshot.Entities, restored.Entities);
    }

    [Fact]
    public void TransientSerializationDoesNotAllocateAfterWarmup()
    {
        var snapshot = CreateSnapshot();
        var buffer = new ReusableMemoryPackSerializationBuffer();
        ShooterPureStateSyncCodec.SerializeTransient(in snapshot, buffer);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 64; i++)
        {
            ShooterPureStateSyncCodec.SerializeTransient(in snapshot, buffer);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated < 256, $"Expected allocation-free steady state, actual={allocated} bytes.");
    }

    [Fact]
    public void CapacityBackedSnapshotSerializesOnlyEffectivePrefixesInV1Layout()
    {
        var exact = CreateSnapshot();
        var entities = new ShooterPureStateEntityDelta[8];
        var hints = new ShooterPureStateVisibilityHint[8];
        entities[0] = exact.Entities[0];
        hints[0] = exact.VisibilityHints[0];
        var capacityBacked = new ShooterPureStateSnapshotPayload(
            exact.Version,
            exact.WorldId,
            exact.Frame,
            exact.ServerTick,
            exact.SnapshotKind,
            exact.BaselineFrame,
            exact.BaselineHash,
            exact.StateHash,
            exact.Settings,
            entities,
            hints);
        capacityBacked.SetTransientCounts(1, 1);

        var expected = MemoryPackSerializer.Serialize(exact);
        var actual = ShooterPureStateSyncCodec.Serialize(in capacityBacked);
        var restored = ShooterPureStateSyncCodec.Deserialize(actual);

        Assert.Equal(expected, actual);
        Assert.Single(restored.Entities);
        Assert.Single(restored.VisibilityHints);
        Assert.Equal(exact.Entities[0], restored.Entities[0]);
    }

    [Fact]
    public void SegmentSerializationReusesCapacityBufferAcrossPayloadLengths()
    {
        var snapshot = CreateSnapshot();
        var buffer = new ReusableMemoryPackSerializationBuffer();
        var first = ShooterPureStateSyncCodec.SerializeTransientSegment(in snapshot, buffer);
        snapshot.SetTransientCounts(0, 0);
        var second = ShooterPureStateSyncCodec.SerializeTransientSegment(in snapshot, buffer);

        Assert.Same(first.Array, second.Array);
        Assert.True(first.Count > second.Count);
        Assert.Equal(buffer.WrittenCount, second.Count);
        var restored = ShooterPureStateSyncCodec.Deserialize(second.ToArray());
        Assert.Empty(restored.Entities);
        Assert.Empty(restored.VisibilityHints);
    }

    [Fact]
    public void WireSegmentSerializationMatchesOwnedPayloadLayout()
    {
        var payloadCapacity = new byte[64];
        payloadCapacity[0] = 1;
        payloadCapacity[1] = 2;
        payloadCapacity[2] = 3;
        var wire = new WireStateSyncSnapshotPush
        {
            WorldId = 9,
            Frame = 10,
            Timestamp = 1.25,
            IsFullSnapshot = false,
            PayloadOpCode = 77,
            Payload = new byte[] { 1, 2, 3 },
            ServerTicks = 11
        };
        var expected = WireRoomGatewayBinary.Serialize(in wire);
        var buffer = new ReusableMemoryPackSerializationBuffer();

        var actual = WireRoomGatewayBinary.SerializeTransient(
            in wire,
            new ArraySegment<byte>(payloadCapacity, 0, 3),
            buffer);
        var restored = WireRoomGatewayBinary.Deserialize<WireStateSyncSnapshotPush>(actual);

        Assert.Equal(expected.ToArray(), actual.ToArray());
        Assert.Equal(new byte[] { 1, 2, 3 }, restored.Payload);
    }

    [Fact]
    public void RoundTripPreservesPureStateSnapshotEnvelope()
    {
        var snapshot = new ShooterPureStateSnapshotPayload(
            ShooterPureStateSyncCodec.CurrentVersion,
            99ul,
            12,
            3456,
            ShooterPureStateSnapshotKinds.Delta,
            10,
            111u,
            222u,
            ShooterPureStateSyncSettings.Default,
            new[]
            {
                new ShooterPureStateEntityDelta(
                    7,
                    ShooterPackedEntityKinds.Projectile,
                    ShooterPureStateEntityLayers.Combat,
                    ShooterPureStateDeltaKinds.Update,
                    1,
                    1200,
                    3400,
                    20,
                    -10,
                    0,
                    0,
                    12,
                    ShooterPureStateEntityFlags.Visible)
            },
            new[]
            {
                new ShooterPureStateVisibilityHint(
                    7,
                    ShooterPackedEntityKinds.Projectile,
                    ShooterPureStateEntityLayers.Combat,
                    ShooterPureStateEntityFlags.Visible,
                    100)
            });

        var bytes = ShooterPureStateSyncCodec.Serialize(in snapshot);
        var restored = ShooterPureStateSyncCodec.Deserialize(bytes);

        Assert.Equal(snapshot.WorldId, restored.WorldId);
        Assert.Equal(snapshot.Frame, restored.Frame);
        Assert.Equal(snapshot.SnapshotKind, restored.SnapshotKind);
        Assert.Equal(snapshot.BaselineFrame, restored.BaselineFrame);
        Assert.Equal(snapshot.Settings.MaxEntityCount, restored.Settings.MaxEntityCount);
        Assert.Single(restored.Entities);
        Assert.Single(restored.VisibilityHints);
        Assert.Equal(ShooterPureStateDeltaKinds.Update, restored.Entities[0].DeltaKind);
    }

    [Fact]
    public void EmptyPayloadUsesDefaultSettings()
    {
        var restored = ShooterPureStateSyncCodec.Deserialize(null!);

        Assert.Equal(ShooterPureStateSyncCodec.CurrentVersion, restored.Version);
        Assert.Equal(ShooterPureStateSnapshotKinds.FullBaseline, restored.SnapshotKind);
        Assert.Equal(10000, restored.Settings.MaxEntityCount);
        Assert.Empty(restored.Entities);
        Assert.Empty(restored.VisibilityHints);
    }

    private static ShooterPureStateSnapshotPayload CreateSnapshot()
    {
        return new ShooterPureStateSnapshotPayload(
            ShooterPureStateSyncCodec.CurrentVersion,
            101ul,
            20,
            20,
            ShooterPureStateSnapshotKinds.Delta,
            18,
            123u,
            456u,
            ShooterPureStateSyncSettings.Default,
            new[]
            {
                new ShooterPureStateEntityDelta(
                    7,
                    ShooterPackedEntityKinds.Projectile,
                    ShooterPureStateEntityLayers.Combat,
                    ShooterPureStateDeltaKinds.Update,
                    1,
                    1200,
                    3400,
                    20,
                    -10,
                    0,
                    0,
                    12,
                    ShooterPureStateEntityFlags.Visible)
            },
            new[]
            {
                new ShooterPureStateVisibilityHint(
                    7,
                    ShooterPackedEntityKinds.Projectile,
                    ShooterPureStateEntityLayers.Combat,
                    ShooterPureStateEntityFlags.Visible,
                    100)
            });
    }
}

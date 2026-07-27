using System;
using AbilityKit.Protocol.Room;
using AbilityKit.Protocol.Shooter;
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

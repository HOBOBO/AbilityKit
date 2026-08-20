using System;
using System.Collections.Generic;
using AbilityKit.Demo.Shooter.View;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Shooter;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests;

public sealed class RemoteSnapshotInterpolationTests
{
    private sealed class TickSample : IRemoteSnapshotSample
    {
        public TickSample(long ticks, float value)
        {
            TimelineTicks = ticks;
            Value = value;
        }

        public long TimelineTicks { get; }

        public float Value { get; }
    }

    [Fact]
    public void ShooterRemoteSampleExcludesActorWithoutCopyingRemainingOrder()
    {
        var actors = new[]
        {
            new ShooterGatewayActorSnapshot(1, 1f, 0f, 0f, 0f, 0f, 100f, 100f, 1),
            new ShooterGatewayActorSnapshot(2, 2f, 0f, 0f, 0f, 0f, 100f, 100f, 1),
            new ShooterGatewayActorSnapshot(3, 3f, 0f, 0f, 0f, 0f, 100f, 100f, 1)
        };

        var sample = new ShooterRemoteSnapshotSample(1ul, frame: 1, serverTicks: 100L, actors, excludedActorId: 2);

        Assert.Same(sample, sample.Actors);
        Assert.Equal(2, sample.Count);
        Assert.Equal(1, sample[0].ActorId);
        Assert.Equal(3, sample[1].ActorId);
    }

    [Fact]
    public void ShooterProjectorPreservesPackedEnemyLifecycleForPresentation()
    {
        var enemyLifecycle = new ShooterPackedComponentChunk(
            ShooterPackedComponentKinds.EntityLifecycle,
            ShooterPackedEntityKinds.Enemy,
            count: 1,
            entityIds: new[] { 10001 },
            valueX: Array.Empty<float>(),
            valueY: Array.Empty<float>(),
            valueZ: Array.Empty<float>(),
            valueW: Array.Empty<float>(),
            intValues: Array.Empty<int>(),
            flags: new[] { ShooterPackedEntityFlags.Alive },
            ownerIds: Array.Empty<int>(),
            aux: Array.Empty<int>());
        var packed = new ShooterPackedSnapshotPayload(
            ShooterPackedSnapshotCodec.CurrentVersion,
            worldId: 88ul,
            frame: 6,
            serverTick: 600L,
            snapshotFlags: ShooterPackedSnapshotFlags.Full,
            stateHash: 0x1234u,
            entityCount: 1,
            extensionPayload: Array.Empty<byte>(),
            componentChunks: new[] { enemyLifecycle });
        var sample = new ShooterRemoteSnapshotSample(
            88ul,
            frame: 6,
            serverTicks: 600L,
            Array.Empty<ShooterGatewayActorSnapshot>(),
            packed);
        var interpolation = new RemoteSnapshotInterpolation<ShooterRemoteSnapshotSample>(
            sample,
            sample,
            alpha: 0f,
            extrapolationTicks: 0L);

        var projected = new ShooterRemoteSnapshotProjector().Project(in interpolation);
        var batch = new ShooterSnapshotViewModelMapper().Map(in projected);

        Assert.True(projected.IsFullSnapshot);
        Assert.Equal(ShooterOpCodes.Snapshot.PackedState, projected.PayloadOpCode);
        Assert.True(projected.PackedSnapshot.HasValue);
        Assert.Same(packed.ComponentChunks, projected.PackedSnapshot.Value.ComponentChunks);
        Assert.Contains(batch.EntityChanges, change =>
            change.Kind == ShooterViewEntityKind.Enemy && change.EntityId == 10001);
    }

    [Fact]
    public void BufferRejectsStaleAndDuplicateSamples()
    {
        var buffer = new RemoteSnapshotBuffer<TickSample>(8);

        Assert.True(buffer.TryAdd(new TickSample(100, 1f)));
        Assert.True(buffer.TryAdd(new TickSample(200, 2f)));
        Assert.False(buffer.TryAdd(new TickSample(200, 9f)));
        Assert.False(buffer.TryAdd(new TickSample(150, 9f)));
        Assert.Equal(2, buffer.Count);
        Assert.Equal(100L, buffer.OldestTimelineTicks);
        Assert.Equal(200L, buffer.NewestTimelineTicks);
    }

    [Fact]
    public void BufferTrimsToCapacityKeepingNewest()
    {
        var buffer = new RemoteSnapshotBuffer<TickSample>(3);
        for (int i = 1; i <= 5; i++)
        {
            buffer.TryAdd(new TickSample(i * 100, i));
        }

        Assert.Equal(3, buffer.Count);
        Assert.Equal(300L, buffer.OldestTimelineTicks);
        Assert.Equal(500L, buffer.NewestTimelineTicks);
    }

    [Fact]
    public void BufferInterpolatesBetweenBracketingSamples()
    {
        var buffer = new RemoteSnapshotBuffer<TickSample>(8);
        buffer.TryAdd(new TickSample(100, 0f));
        buffer.TryAdd(new TickSample(200, 10f));

        Assert.True(buffer.TrySample(150, out var interpolation));
        Assert.True(interpolation.IsInterpolating);
        Assert.Equal(0.5f, interpolation.Alpha, 3);
        Assert.Equal(0f, interpolation.From.Value);
        Assert.Equal(10f, interpolation.To.Value);
        Assert.Equal(0L, interpolation.ExtrapolationTicks);
    }

    [Fact]
    public void BufferClampsBeforeOldestSample()
    {
        var buffer = new RemoteSnapshotBuffer<TickSample>(8);
        buffer.TryAdd(new TickSample(100, 1f));
        buffer.TryAdd(new TickSample(200, 2f));

        Assert.True(buffer.TrySample(50, out var interpolation));
        Assert.False(interpolation.IsInterpolating);
        Assert.False(interpolation.IsExtrapolating);
        Assert.Equal(1f, interpolation.From.Value);
        Assert.Same(interpolation.From, interpolation.To);
    }

    [Fact]
    public void BufferReportsExtrapolationPastNewestSample()
    {
        var buffer = new RemoteSnapshotBuffer<TickSample>(8);
        buffer.TryAdd(new TickSample(100, 1f));
        buffer.TryAdd(new TickSample(200, 2f));

        Assert.True(buffer.TrySample(260, out var interpolation));
        Assert.True(interpolation.IsExtrapolating);
        Assert.Equal(60L, interpolation.ExtrapolationTicks);
        Assert.Equal(2f, interpolation.To.Value);
    }

    [Fact]
    public void TimelineHoldsPlaybackBehindNewestServerTime()
    {
        var timeline = new InterpolationTimeline(ticksPerSecond: 1000L, interpolationDelayTicks: 100L);
        Assert.False(timeline.HasServerTime);

        timeline.ObserveServerTicks(1000L);
        Assert.True(timeline.HasServerTime);
        Assert.Equal(1000L, timeline.EstimatedServerTicks);
        Assert.Equal(900L, timeline.PlaybackTicks);

        timeline.Advance(0.05f);
        Assert.Equal(1050L, timeline.EstimatedServerTicks);
        Assert.Equal(950L, timeline.PlaybackTicks);
    }

    [Fact]
    public void TimelineSnapsForwardToNewerServerTime()
    {
        var timeline = new InterpolationTimeline(ticksPerSecond: 1000L, interpolationDelayTicks: 0L);
        timeline.ObserveServerTicks(500L);
        timeline.ObserveServerTicks(400L); // stale, ignored
        Assert.Equal(500L, timeline.EstimatedServerTicks);

        timeline.ObserveServerTicks(900L);
        Assert.Equal(900L, timeline.EstimatedServerTicks);
    }

    [Fact]
    public void TimelineWithSoftCatchUpConvergesGraduallyTowardServerTime()
    {
        // Soft mode: a positive catch-up rate must not snap the estimate to a freshly observed,
        // much-newer server time. The target jumps immediately, but the estimate lags and only
        // converges as the timeline advances.
        var timeline = new InterpolationTimeline(ticksPerSecond: 1000L, interpolationDelayTicks: 0L, maxCatchUpRate: 0.1d);
        timeline.ObserveServerTicks(1000L);
        Assert.Equal(1000L, timeline.EstimatedServerTicks);

        // A large forward observation moves the target but not the estimate.
        timeline.ObserveServerTicks(2000L);
        Assert.Equal(2000L, timeline.TargetServerTicks);
        Assert.Equal(1000L, timeline.EstimatedServerTicks);

        // Advancing applies real-time progress plus a bounded correction toward the target, so the
        // estimate moves forward by more than the raw advance yet stays behind the target.
        timeline.Advance(0.1f); // advance = 100 ticks
        long afterFirst = timeline.EstimatedServerTicks;
        Assert.True(afterFirst > 1100L, $"estimate {afterFirst} should exceed raw advance 1100");
        Assert.True(afterFirst < timeline.TargetServerTicks, $"estimate {afterFirst} should stay behind target {timeline.TargetServerTicks}");
    }

    [Fact]
    public void TimelineSoftCatchUpCorrectionIsBounded()
    {
        // The per-advance correction is clamped to advance * rate so convergence cannot overshoot or
        // produce a backwards jump even when the outstanding error is large.
        var timeline = new InterpolationTimeline(ticksPerSecond: 1000L, interpolationDelayTicks: 0L, maxCatchUpRate: 0.5d);
        timeline.ObserveServerTicks(0L);
        timeline.ObserveServerTicks(10_000L); // huge error

        timeline.Advance(0.1f); // advance = 100, maxCorrection = 50
        // estimate += advance + min(error*rate, maxCorrection) = 0 + 100 + 50 = 150
        Assert.Equal(150L, timeline.EstimatedServerTicks);
    }

    [Fact]
    public void AngleLerpTakesShortestArcAcrossSeam()
    {
        // Going from +170° to -170° is a 20° step the short way (crossing 180°), not a 340° spin.
        float result = InterpolationMath.LerpAngleDegrees(170f, -170f, 0.5f);
        // Halfway along the short arc lands on the 180°/-180° seam.
        float normalized = InterpolationMath.Repeat(result + 360f, 360f);
        Assert.True(Math.Abs(normalized - 180f) < 0.01f, $"expected ~180, got {normalized}");
    }

    [Fact]
    public void AngleLerpRadiansBlendsAcrossPiSeam()
    {
        float nearPi = (float)(Math.PI - 0.1d);
        float nearNegPi = (float)(-Math.PI + 0.1d);
        float result = InterpolationMath.LerpAngleRadians(nearPi, nearNegPi, 0.5f);
        float normalized = InterpolationMath.Repeat(result, InterpolationMath.TwoPiRadians);
        // Midpoint of the short arc sits on the ±π seam.
        Assert.True(Math.Abs(normalized - Math.PI) < 0.01f, $"expected ~PI, got {normalized}");
    }

    [Fact]
    public void AngleLerpMatchesLinearLerpWhenWithinArc()
    {
        // When the two angles are close, shortest-arc lerp equals plain linear lerp.
        float result = InterpolationMath.LerpAngleDegrees(10f, 50f, 0.25f);
        Assert.Equal(20f, result, 3);
    }
}

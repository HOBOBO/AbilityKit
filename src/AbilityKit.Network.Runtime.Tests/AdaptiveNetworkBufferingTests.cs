using AbilityKit.Core.Buffers;
using AbilityKit.Network.Runtime;
using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class AdaptiveNetworkBufferingTests
{
    [Fact]
    public void MetricsEstimator_SmoothsRttVariationAndLoss()
    {
        var estimator = new NetworkBufferMetricsEstimator(60, smoothingFactor: 0.5d);

        var first = estimator.Observe(0.1d, packetLossRate: 0.2d);
        var second = estimator.Observe(0.3d, packetLossRate: 0d);

        Assert.Equal(0.1d, first.RoundTripSeconds, precision: 9);
        Assert.Equal(0d, first.JitterSeconds, precision: 9);
        Assert.Equal(0.2d, second.RoundTripSeconds, precision: 9);
        Assert.Equal(0.1d, second.JitterSeconds, precision: 9);
        Assert.Equal(0.1d, second.PacketLossRate, precision: 9);
        Assert.Equal(2, estimator.SampleCount);
    }

    [Fact]
    public void SizingSample_FromDiagnosticsUsesRttAndExplicitTransportLoss()
    {
        var diagnostics = new NetworkDiagnosticsSnapshot(
            estimatedRttMs: 120d,
            clockOffsetMs: 0d,
            currentFrame: 10,
            lastAuthoritativeFrame: 9,
            frameGap: 1,
            resyncCount: 0,
            snapshotsReceived: 5,
            inputsSubmitted: 20,
            inputsRejected: 5,
            reconnectPhase: FastReconnectPhase.Connected,
            recentHealthEvents: null);

        var sample = NetworkBufferSizingSample.FromDiagnostics(
            in diagnostics,
            tickRate: 60,
            jitterSeconds: 0.01d,
            packetLossRate: 0.25d);

        Assert.True(sample.HasRoundTripSample);
        Assert.Equal(0.12d, sample.RoundTripSeconds, precision: 9);
        Assert.Equal(0.25d, sample.PacketLossRate, precision: 9);
    }

    [Fact]
    public void Policy_ExpandsImmediatelyFromRttJitterLossAndStarvation()
    {
        var policy = new AdaptiveNetworkBufferCapacityPolicy(CreateOptions());
        var normal = new NetworkBufferSizingSample(60, 0.1d, 0.01d, 0.1d);
        var starved = new NetworkBufferSizingSample(60, 0.1d, 0.01d, 0.1d, isBufferStarved: true);

        Assert.Equal(11, policy.GetTargetCapacity(normal, currentCapacity: 8));
        Assert.Equal(16, policy.GetTargetCapacity(starved, currentCapacity: 11));
    }

    [Fact]
    public void Policy_RequiresSustainedDemandDropAndShrinksGradually()
    {
        var policy = new AdaptiveNetworkBufferCapacityPolicy(CreateOptions());
        var lowDemand = new NetworkBufferSizingSample(60, 0d, 0d, 0d);

        Assert.Equal(10, policy.GetTargetCapacity(lowDemand, currentCapacity: 10));
        Assert.Equal(10, policy.GetTargetCapacity(lowDemand, currentCapacity: 10));
        Assert.Equal(8, policy.GetTargetCapacity(lowDemand, currentCapacity: 10));
        Assert.Equal(0, policy.PendingShrinkSamples);
    }

    [Fact]
    public void BufferController_DoesNotAdjustBeforeFirstRttSample()
    {
        var capacity = new TestCapacityControl(30);
        var controller = new AdaptiveNetworkBufferController(capacity, CreateOptions());
        var unavailable = new NetworkBufferSizingSample(
            60,
            hasRoundTripSample: false,
            roundTripSeconds: 0d,
            jitterSeconds: 0d,
            packetLossRate: 0d);

        Assert.False(controller.Observe(unavailable));
        Assert.Equal(30, capacity.Capacity);
        Assert.Equal(0, capacity.SetAttempts);
    }

    [Fact]
    public void BufferController_ConvertsUnboundedServerBufferToMeasuredWindow()
    {
        var capacity = new TestCapacityControl(int.MaxValue);
        var controller = new AdaptiveNetworkBufferController(
            capacity,
            NetworkBufferCapacityPolicyOptions.ServerInputDefault);
        var sample = new NetworkBufferSizingSample(60, 0.1d, 0.01d, 0d);

        Assert.True(controller.Observe(sample));

        Assert.Equal(7, capacity.Capacity);
    }

    [Fact]
    public void FrameDelayController_DrivesJitterBufferWithoutChangingItsDefaultAssembly()
    {
        var buffer = new FrameJitterBuffer<int>(delayFrames: 2);
        var controller = new AdaptiveNetworkFrameDelayController(buffer);
        var sample = new NetworkBufferSizingSample(60, 0.2d, 0d, 0d);

        Assert.True(controller.Observe(sample));

        Assert.Equal(7, buffer.DelayFrames);
        Assert.Equal(7, controller.TargetDelayFrames);
        Assert.False(buffer.TrySetDelayFrames(-1));
        Assert.Equal(7, buffer.DelayFrames);
    }

    [Fact]
    public void InvalidMetrics_DoNotPoisonEstimatorAndInfiniteDemandClampsToMaximum()
    {
        var estimator = new NetworkBufferMetricsEstimator(60, smoothingFactor: double.NaN);
        var normalized = estimator.Observe(double.NaN, packetLossRate: double.NaN);
        var policy = new AdaptiveNetworkBufferCapacityPolicy(CreateOptions());
        var infinite = new NetworkBufferSizingSample(
            60,
            double.PositiveInfinity,
            0d,
            0d);

        Assert.Equal(0d, normalized.RoundTripSeconds);
        Assert.Equal(0d, normalized.PacketLossRate);
        Assert.Equal(100, policy.GetTargetCapacity(infinite, currentCapacity: 1));
    }

    [Fact]
    public void RemoteSnapshotBuffer_RuntimeShrinkRetainsNewestSamples()
    {
        var buffer = new RemoteSnapshotBuffer<TestSnapshot>(4);
        for (var tick = 1L; tick <= 4L; tick++)
        {
            buffer.TryAdd(new TestSnapshot(tick));
        }

        Assert.True(((IBufferCapacityControl)buffer).TrySetCapacity(2));

        Assert.Equal(2, buffer.Count);
        Assert.Equal(3L, buffer.OldestTimelineTicks);
        Assert.Equal(4L, buffer.NewestTimelineTicks);
    }

    [Fact]
    public void RemoteSnapshotBuffer_AllowsListStorageAsAnOptionalBackend()
    {
        var storage = new ListSequentialBuffer<TestSnapshot>(3);
        var buffer = new RemoteSnapshotBuffer<TestSnapshot>(storage);
        for (var tick = 1L; tick <= 4L; tick++)
        {
            Assert.True(buffer.TryAdd(new TestSnapshot(tick)));
        }

        Assert.Equal(3, storage.Count);
        Assert.Equal(2L, buffer.OldestTimelineTicks);
        Assert.Equal(4L, buffer.NewestTimelineTicks);
    }

    [Fact]
    public void RemoteInterpolationPlayback_AcceptsAnAssembledSnapshotBackend()
    {
        var storage = new ListSequentialBuffer<TestSnapshot>(4);
        var snapshots = new RemoteSnapshotBuffer<TestSnapshot>(storage);
        var playback = new RemoteInterpolationPlayback<TestSnapshot>(
            new InterpolationConfig(60L, 2L, bufferCapacity: 32),
            snapshots);

        Assert.Equal(4, playback.SnapshotCapacityControl.Capacity);
        Assert.True(playback.Observe(new TestSnapshot(10L)));
        Assert.Equal(1, storage.Count);
    }

    [Fact]
    public void TimelineDelayController_ConvergesWithoutMovingPlaybackBackwards()
    {
        var playback = new RemoteInterpolationPlayback<TestSnapshot>(new InterpolationConfig(
            ticksPerSecond: 1000L,
            interpolationDelayTicks: 100L,
            bufferCapacity: 4));
        playback.Observe(new TestSnapshot(1000L));
        var controller = new AdaptiveNetworkTimelineDelayController(playback.TimelineDelayControl);
        var sample = new NetworkBufferSizingSample(60, 0.2d, 0d, 0d);
        var before = playback.PlaybackTicks;

        Assert.True(controller.Observe(sample));
        Assert.Equal(117L, playback.TimelineDelayControl.TargetDelayTicks);
        Assert.Equal(100L, playback.TimelineDelayControl.DelayTicks);

        playback.Advance(0.01f);
        var during = playback.PlaybackTicks;
        playback.Advance(0.01f);

        Assert.True(during >= before);
        Assert.True(playback.PlaybackTicks >= during);
        Assert.Equal(117L, playback.TimelineDelayControl.DelayTicks);
    }

    private static NetworkBufferCapacityPolicyOptions CreateOptions()
    {
        return new NetworkBufferCapacityPolicyOptions(
            minCapacity: 1,
            maxCapacity: 100,
            roundTripCoverage: 1d,
            jitterMultiplier: 2d,
            safetyFrames: 2,
            packetLossFrameScale: 10,
            starvationBoostFrames: 5,
            shrinkThresholdFrames: 2,
            shrinkDelaySamples: 3,
            maxShrinkFramesPerUpdate: 2);
    }

    private sealed class TestCapacityControl : IBufferCapacityControl
    {
        public TestCapacityControl(int capacity) => Capacity = capacity;

        public int Capacity { get; private set; }

        public int SetAttempts { get; private set; }

        public bool TrySetCapacity(int capacity)
        {
            SetAttempts++;
            if (capacity <= 0) return false;
            Capacity = capacity;
            return true;
        }
    }

    private sealed class TestSnapshot : IRemoteSnapshotSample
    {
        public TestSnapshot(long timelineTicks) => TimelineTicks = timelineTicks;

        public long TimelineTicks { get; }
    }
}

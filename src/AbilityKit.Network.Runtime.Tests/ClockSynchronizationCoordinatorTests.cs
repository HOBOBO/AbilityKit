using System;
using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Network.Runtime.Tests;

public sealed class ClockSynchronizationCoordinatorTests
{
    [Fact]
    public void ObserveResponse_NormalizesDifferentTickFrequencies()
    {
        var coordinator = new ClockSynchronizationCoordinator(30, smoothingFactor: 0.5d);

        var estimate = coordinator.ObserveResponse(
            clientSendTicks: 1_000L,
            clientReceiveTicks: 1_300L,
            localTickFrequency: 1_000d,
            serverNowTicks: 20_000L,
            serverTickFrequency: 10_000L);

        Assert.True(estimate.HasSample);
        Assert.Equal(0.3d, estimate.RoundTripSeconds, precision: 9);
        Assert.Equal(-0.85d, estimate.LocalMinusServerOffsetSeconds, precision: 9);
        Assert.Equal(1, estimate.SampleCount);
    }

    [Fact]
    public void ObserveResponse_ClampsNegativeRoundTrip()
    {
        var coordinator = new ClockSynchronizationCoordinator(30);

        var estimate = coordinator.ObserveResponse(
            clientSendTicks: 1_300L,
            clientReceiveTicks: 1_000L,
            localTickFrequency: 1_000d,
            serverNowTicks: 2_000L,
            serverTickFrequency: 1_000L);

        Assert.Equal(0d, estimate.RoundTripSeconds);
    }

    [Fact]
    public void Observe_AppliesConfiguredEwma()
    {
        var coordinator = new ClockSynchronizationCoordinator(30, smoothingFactor: 0.5d);
        var first = new ClockSynchronizationSample(0.3d, -0.8d);
        var second = new ClockSynchronizationSample(0.5d, -0.4d);

        coordinator.Observe(in first);
        var estimate = coordinator.Observe(in second);

        Assert.Equal(-0.6d, estimate.LocalMinusServerOffsetSeconds, precision: 9);
        Assert.Equal(0.4d, estimate.RoundTripSeconds, precision: 9);
        Assert.Equal(2, estimate.SampleCount);
    }

    [Fact]
    public void AdvanceAndReset_ManageLocalAnchorTimelineAndSamples()
    {
        var coordinator = new ClockSynchronizationCoordinator(30);
        var sample = new ClockSynchronizationSample(0.2d, 0.1d);
        coordinator.Observe(in sample);
        coordinator.AdvanceLocal();
        var second = coordinator.AdvanceLocal();

        Assert.Equal(1, second.LocalFrame);
        Assert.Equal(1d / 30d, second.ElapsedSeconds, precision: 9);

        coordinator.Reset(60);
        var reset = coordinator.AdvanceLocal();

        Assert.False(coordinator.Estimate.HasSample);
        Assert.Equal(0, reset.LocalFrame);
        Assert.Equal(0d, reset.ElapsedSeconds);
    }

    [Fact]
    public void ProjectAuthoritative_CreatesServerStampedAnchor()
    {
        var projection = ClockSynchronizationCoordinator.ProjectAuthoritative(
            startServerTicks: 200_000L,
            serverTickFrequency: 10_000_000L,
            startFrame: 30,
            fixedDeltaSeconds: 1d / 30d,
            serverNowTicks: 1_200_000L);

        Assert.True(projection.AnchorValid);
        Assert.Equal(33, projection.TargetFrame);
        Assert.Equal(3, projection.CatchUpFrames);
        Assert.Equal(0.1d, projection.ElapsedSeconds, precision: 9);
        Assert.Equal(33, projection.TimeAnchor.LocalFrame);
        Assert.Equal(3L, projection.TimeAnchor.TimelineTicks);
        Assert.Equal(33, projection.TimeAnchor.AuthoritativeFrame);
        Assert.Equal(1_200_000L, projection.TimeAnchor.ServerTicks);
    }

    [Theory]
    [InlineData(0L, 0L, 1d / 30d, 1_000L)]
    [InlineData(0L, 10_000_000L, 0d, 1_000L)]
    [InlineData(0L, 10_000_000L, 1d / 30d, 0L)]
    public void ProjectAuthoritative_ReturnsDefaultForInvalidTiming(
        long startServerTicks,
        long serverTickFrequency,
        double fixedDeltaSeconds,
        long serverNowTicks)
    {
        var projection = ClockSynchronizationCoordinator.ProjectAuthoritative(
            startServerTicks,
            serverTickFrequency,
            startFrame: 0,
            fixedDeltaSeconds,
            serverNowTicks);

        Assert.False(projection.AnchorValid);
    }

    [Fact]
    public void ProjectAuthoritative_ClampsTimeBeforeStartToStartFrame()
    {
        var projection = ClockSynchronizationCoordinator.ProjectAuthoritative(
            startServerTicks: 2_000L,
            serverTickFrequency: 1_000L,
            startFrame: 12,
            fixedDeltaSeconds: 0.1d,
            serverNowTicks: 1_000L);

        Assert.True(projection.AnchorValid);
        Assert.Equal(12, projection.TargetFrame);
        Assert.Equal(0, projection.CatchUpFrames);
        Assert.Equal(0d, projection.ElapsedSeconds);
    }

    [Fact]
    public void Constructor_RejectsInvalidTickRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ClockSynchronizationCoordinator(0));
    }
}

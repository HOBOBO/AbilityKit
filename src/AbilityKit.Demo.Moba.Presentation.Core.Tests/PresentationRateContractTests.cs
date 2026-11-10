using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.Core.Tests;

public sealed class PresentationRateContractTests
{
    [Fact]
    public void DefaultRequest_CompletesOnePlayAtNormalSpeed()
    {
        var request = new PresentationRequest(
            "default-vfx", 1, PresentationVector3.Zero, durationMs: 100);
        Assert.Equal(1f, request.Playback.Speed);
        Assert.Equal(1, request.Playback.PlayCount);
        Assert.True(request.Playback.PlayOnReady);
        var lifecycle = new PresentationPlaybackLifecycle(request.Playback, 0.1f);
        Assert.False(lifecycle.Tick(0.05f));
        Assert.True(lifecycle.Tick(0.05f));
        Assert.Equal(1, lifecycle.Snapshot.CompletedPlays);
    }

    [Fact]
    public void ExplicitPause_RemainsPausedUntilAnEffectiveRateIsProvided()
    {
        var request = new PresentationRequest(
            "paused-vfx", 1, PresentationVector3.Zero, durationMs: 100,
            playback: new PresentationPlaybackSpec(speed: 0f));
        var lifecycle = new PresentationPlaybackLifecycle(request.Playback, 0.1f);
        Assert.False(lifecycle.Tick(1f));
        Assert.Equal(0f, lifecycle.Snapshot.ElapsedSeconds);
        Assert.True(lifecycle.Tick(0.1f, 1f));
    }

    [Fact]
    public void Resolver_ComposesMultiplyAndPriorityControlSources()
    {
        var resolver = new PresentationRateResolver();
        resolver.Set(new PresentationRateSource("global", PresentationRateChannel.Vfx, 2f));
        resolver.Set(new PresentationRateSource("skill", PresentationRateChannel.Vfx, 0.5f));
        resolver.Set(new PresentationRateSource("override", PresentationRateChannel.Vfx, 3f,
            PresentationRateMode.Override, 4));

        Assert.Equal(3f, resolver.ResolveRate(PresentationRateChannel.Vfx));

        resolver.Set(new PresentationRateSource("pause", PresentationRateChannel.Vfx, 1f,
            PresentationRateMode.Pause, 5));
        Assert.Equal(0f, resolver.ResolveRate(PresentationRateChannel.Vfx));
    }

    [Fact]
    public void Lifecycle_UsesExplicitEffectivePlaybackRate()
    {
        var lifecycle = new PresentationPlaybackLifecycle(
            new PresentationPlaybackSpec(playCount: 1), 1f);

        Assert.False(lifecycle.Tick(0.25f, 2f));
        Assert.Equal(0.5f, lifecycle.Snapshot.ElapsedSeconds);
        Assert.True(lifecycle.Tick(0.25f, 2f));
        Assert.Equal(PresentationPlaybackPhase.Completed, lifecycle.Snapshot.Phase);
    }
}

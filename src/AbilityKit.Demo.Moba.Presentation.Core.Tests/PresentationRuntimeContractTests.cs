using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.Presentation.Core.Tests;

public sealed class PresentationRuntimeContractTests
{
    [Fact]
    public void PolicyLedger_EnforcesUniqueMaxActiveAndMinimumInterval()
    {
        var ledger = new PresentationEffectPolicyLedger<string>();
        var policy = new PresentationEffectPolicy("impact", unique: true, minIntervalMs: 100, maxActive: 1);

        Assert.True(ledger.TryAcquire("owner:impact", in policy, 1000));
        Assert.False(ledger.TryAcquire("owner:impact", in policy, 1050));
        ledger.Release("owner:impact");
        Assert.False(ledger.TryAcquire("owner:impact", in policy, 1099));
        Assert.True(ledger.TryAcquire("owner:impact", in policy, 1100));
    }

    [Fact]
    public void EmissionQueue_DrainsByPriorityAndPreservesSequenceForTies()
    {
        var queue = new PresentationEmissionQueue<string>(capacity: 4);
        queue.Enqueue("low", 0);
        queue.Enqueue("high-a", 2);
        queue.Enqueue("high-b", 2);
        var emitted = new List<string>();

        var count = queue.Drain(2, TimeSpan.Zero, item => { emitted.Add(item); return true; });

        Assert.Equal(2, count);
        Assert.Equal(new[] { "high-a", "high-b" }, emitted);
        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public void EmissionQueue_WhenFullReplacesLowestPriorityAndCountsDrops()
    {
        var queue = new PresentationEmissionQueue<string>(capacity: 2);
        queue.Enqueue("low", 0);
        queue.Enqueue("medium", 1);

        Assert.True(queue.Enqueue("high", 2));
        Assert.Equal(1, queue.DroppedCount);
        var emitted = new List<string>();
        queue.Drain(2, TimeSpan.Zero, item => { emitted.Add(item); return true; });
        Assert.Equal(new[] { "high", "medium" }, emitted);
    }

    [Fact]
    public void PlaybackLifecycle_ObservesDelayRepeatedPlayIntervalAndCompletion()
    {
        var lifecycle = new PresentationPlaybackLifecycle(
            new PresentationPlaybackSpec(playCount: 2, delayMs: 100, intervalMs: 50), 0.2f);
        Assert.Equal(PresentationPlaybackPhase.Delayed, lifecycle.Snapshot.Phase);
        Assert.False(lifecycle.Tick(0.1f));
        Assert.Equal(PresentationPlaybackPhase.Playing, lifecycle.Snapshot.Phase);
        Assert.False(lifecycle.Tick(0.2f));
        Assert.Equal(PresentationPlaybackPhase.Interval, lifecycle.Snapshot.Phase);
        Assert.False(lifecycle.Tick(0.05f));
        Assert.True(lifecycle.Tick(0.2f));
        Assert.Equal(PresentationPlaybackPhase.Completed, lifecycle.Snapshot.Phase);
    }

    [Fact]
    public void PlaybackLifecycle_ConsumesOvershootAcrossDelayAndInterval()
    {
        var lifecycle = new PresentationPlaybackLifecycle(
            new PresentationPlaybackSpec(playCount: 2, delayMs: 100, intervalMs: 50), 0.2f);

        Assert.False(lifecycle.Tick(0.25f));
        Assert.Equal(PresentationPlaybackPhase.Playing, lifecycle.Snapshot.Phase);
        Assert.False(lifecycle.Tick(0.2f));
        Assert.Equal(PresentationPlaybackPhase.Playing, lifecycle.Snapshot.Phase);
        Assert.True(lifecycle.Tick(0.2f));
    }
}

using AbilityKit.Network.Runtime.Sync;
using Xunit;

namespace AbilityKit.Network.Sdk.Tests;

public sealed class ReliableEventCheckpointStoreTests
{
    [Fact]
    public void SaveDoesNotMoveSameTimelineBackward()
    {
        var store = new InMemoryReliableEventCheckpointStore();
        var latest = new ReliableEventCheckpoint("battle-1", "epoch-1", 8L);
        var stale = new ReliableEventCheckpoint("battle-1", "epoch-1", 3L);

        store.Save(in latest);
        store.Save(in stale);

        Assert.True(store.TryLoad("battle-1", out var loaded));
        Assert.Equal(8L, loaded.LastAcknowledgedSequence);
    }

    [Fact]
    public void SaveAllowsNewTimelineToReplaceOldTimeline()
    {
        var store = new InMemoryReliableEventCheckpointStore();
        var previous = new ReliableEventCheckpoint("battle-1", "epoch-1", 8L);
        var current = new ReliableEventCheckpoint("battle-1", "epoch-2", 1L);

        store.Save(in previous);
        store.Save(in current);

        Assert.True(store.TryLoad("battle-1", out var loaded));
        Assert.Equal("epoch-2", loaded.TimelineId);
        Assert.Equal(1L, loaded.LastAcknowledgedSequence);
    }

    [Fact]
    public void RemoveAndClearDeleteStoredCheckpoints()
    {
        var store = new InMemoryReliableEventCheckpointStore();
        var first = new ReliableEventCheckpoint("battle-1", "epoch-1", 1L);
        var second = new ReliableEventCheckpoint("battle-2", "epoch-1", 2L);
        store.Save(in first);
        store.Save(in second);

        Assert.True(store.Remove("battle-1"));
        Assert.False(store.TryLoad("battle-1", out _));
        store.Clear();
        Assert.False(store.TryLoad("battle-2", out _));
    }
}

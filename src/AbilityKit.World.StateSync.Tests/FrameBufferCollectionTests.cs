using AbilityKit.Ability.Host;
using AbilityKit.Ability.StateSync;
using AbilityKit.Ability.StateSync.Buffer;
using AbilityKit.Core.Buffers;
using Xunit;
using StateSnapshot = AbilityKit.Ability.StateSync.Snapshot.WorldStateSnapshot;

namespace AbilityKit.World.StateSync.Tests;

public sealed class SnapshotBufferCollectionTests
{
    [Fact]
    public void RingBackendPreservesSnapshotBufferSemantics()
    {
        var storage = new RingFrameIndexedBuffer<StateSnapshot>(3);
        var buffer = new SnapshotBuffer(storage);
        buffer.Store(30, Snapshot(30, 1));
        buffer.Store(10, Snapshot(10, 2));
        buffer.Store(20, Snapshot(20, 3));
        buffer.Store(40, Snapshot(40, 4));

        Assert.Equal(new[] { 20, 30, 40 }, buffer.GetCapturedFrames());
        Assert.True(buffer.TryGet(20, out var retained));
        Assert.Equal(3u, retained.WorldFlags);

        Assert.True(buffer.TrySetCapacity(2));
        Assert.Equal(new[] { 30, 40 }, buffer.GetCapturedFrames());
        Assert.Equal(2, storage.Capacity);
    }

    [Fact]
    public void Out_of_order_and_duplicate_frames_remain_unique_and_sorted()
    {
        var buffer = new SnapshotBuffer(3);
        buffer.Store(30, Snapshot(30, 1));
        buffer.Store(10, Snapshot(10, 2));
        buffer.Store(20, Snapshot(20, 3));
        buffer.Store(20, Snapshot(20, 9));

        Assert.Equal(new[] { 10, 20, 30 }, buffer.GetCapturedFrames());
        Assert.Equal(10, buffer.GetEarliestFrame());
        Assert.Equal(30, buffer.GetLatestFrame());
        Assert.True(buffer.TryGet(20, out var replacement));
        Assert.Equal(9u, replacement.WorldFlags);
    }

    [Fact]
    public void Capacity_and_range_removal_use_numeric_frame_boundaries()
    {
        var buffer = new SnapshotBuffer(3);
        buffer.Store(40, Snapshot(40));
        buffer.Store(10, Snapshot(10));
        buffer.Store(30, Snapshot(30));
        buffer.Store(20, Snapshot(20));

        Assert.Equal(new[] { 20, 30, 40 }, buffer.GetCapturedFrames());

        buffer.RemoveBefore(30);
        Assert.Equal(new[] { 30, 40 }, buffer.GetCapturedFrames());

        buffer.RemoveAfter(30);
        Assert.Equal(new[] { 30 }, buffer.GetCapturedFrames());
    }

    private static StateSnapshot Snapshot(int frame, uint flags = 0) => new()
    {
        Frame = frame,
        WorldFlags = flags
    };
}

public sealed class InputBufferCollectionTests
{
    [Fact]
    public void RingBackendPreservesInputBufferRangeSemantics()
    {
        var storage = new RingFrameIndexedBuffer<TestInput>(3);
        var buffer = new InputBuffer<TestInput>(localPlayerId: 7, storage);
        buffer.Store(30, new TestInput(30));
        buffer.Store(10, new TestInput(10));
        buffer.Store(20, new TestInput(20));
        buffer.Store(40, new TestInput(40));

        Assert.Equal(new[] { 20, 30 }, buffer.GetInputsInRange(15, 35).Select(input => input.Value));
        Assert.Equal(40, buffer.GetLatestFrame());

        buffer.RemoveBefore(30);
        Assert.Equal(2, storage.Count);
        Assert.False(buffer.Contains(20));
    }

    [Fact]
    public void Range_reads_are_sorted_and_duplicate_frames_replace_values()
    {
        var buffer = new InputBuffer<TestInput>(localPlayerId: 7, maxBufferSize: 3);
        buffer.Store(30, new TestInput(30));
        buffer.Store(10, new TestInput(10));
        buffer.Store(20, new TestInput(20));
        buffer.Store(20, new TestInput(200));

        Assert.Equal(new[] { 10, 200 }, buffer.GetInputsInRange(10, 20).Select(input => input.Value));
        Assert.Equal(30, buffer.GetLatestFrame());

        buffer.RemoveBefore(20);
        Assert.False(buffer.Contains(10));
        Assert.True(buffer.Contains(20));
        Assert.Equal(2, buffer.Count);
    }

    private sealed record TestInput(int Value) : IInputCommand;
}

public sealed class RemoteFrameBufferCollectionTests
{
    [Fact]
    public void Trim_is_exclusive_and_replacement_does_not_duplicate_the_index()
    {
        var buffer = new RemoteFrameBuffer<string>(4);
        buffer.Add(30, "thirty");
        buffer.Add(10, "ten");
        buffer.Add(20, "twenty");
        buffer.Add(20, "replacement");

        buffer.TrimBefore(20);

        Assert.False(buffer.TryGet(10, out _));
        Assert.True(buffer.TryGet(20, out var value));
        Assert.Equal("replacement", value);
        Assert.True(buffer.TryGet(30, out _));
        Assert.Equal(30, buffer.MaxReceivedFrame);
    }
}

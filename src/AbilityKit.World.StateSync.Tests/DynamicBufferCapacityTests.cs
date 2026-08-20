using AbilityKit.Ability.StateSync;
using AbilityKit.Ability.StateSync.Buffer;
using AbilityKit.Ability.StateSync.Prediction;
using AbilityKit.Core.Buffers;
using Xunit;
using StateSnapshot = AbilityKit.Ability.StateSync.Snapshot.WorldStateSnapshot;

namespace AbilityKit.World.StateSync.Tests;

public sealed class DynamicBufferCapacityTests
{
    [Fact]
    public void InputBuffer_RuntimeShrinkRetainsNewestFrames()
    {
        var buffer = new InputBuffer<TestInput>(7, 4);
        buffer.Store(40, new TestInput(40));
        buffer.Store(10, new TestInput(10));
        buffer.Store(30, new TestInput(30));
        buffer.Store(20, new TestInput(20));

        Assert.True(((IBufferCapacityControl)buffer).TrySetCapacity(2));

        Assert.False(buffer.Contains(10));
        Assert.False(buffer.Contains(20));
        Assert.True(buffer.Contains(30));
        Assert.True(buffer.Contains(40));
    }

    [Fact]
    public void SnapshotBuffer_RuntimeShrinkRetainsNewestFrames()
    {
        var buffer = new SnapshotBuffer(4);
        buffer.Store(40, Snapshot(40));
        buffer.Store(10, Snapshot(10));
        buffer.Store(30, Snapshot(30));
        buffer.Store(20, Snapshot(20));

        Assert.True(((IBufferCapacityControl)buffer).TrySetCapacity(2));

        Assert.Equal(new[] { 30, 40 }, buffer.GetCapturedFrames());
    }

    [Fact]
    public void PredictionHistories_RuntimeShrinkRetainsNewestFrames()
    {
        var snapshots = new DictionarySnapshotStore(4);
        var inputs = new InputHistory(4);
        for (var frame = 1; frame <= 4; frame++)
        {
            var slots = new StateSlots();
            slots.Set("frame", frame);
            snapshots.Record(new Frame(frame), slots);
            inputs.Record(new Frame(frame), new TestInput(frame));
        }

        Assert.True(((IBufferCapacityControl)snapshots).TrySetCapacity(2));
        Assert.True(((IBufferCapacityControl)inputs).TrySetCapacity(2));

        Assert.Null(snapshots.Get(new Frame(2)));
        Assert.Equal(3, snapshots.Get(new Frame(3))!.GetInt("frame"));
        var batches = inputs.GetFrameBatches(Frame.Zero, new Frame(4));
        Assert.Empty(batches[1].Inputs);
        Assert.Single(batches[2].Inputs);
        Assert.Single(batches[3].Inputs);
    }

    [Fact]
    public void PredictionHistories_AcceptRingStorageBackends()
    {
        var snapshotStorage = new RingFrameIndexedBuffer<StateSlots>(3);
        var inputStorage = new RingFrameIndexedBuffer<List<IInputCommand>>(3);
        var snapshots = new DictionarySnapshotStore(snapshotStorage);
        var inputs = new InputHistory(inputStorage);
        for (var frame = 1; frame <= 4; frame++)
        {
            var slots = new StateSlots();
            slots.Set("frame", frame);
            snapshots.Record(new Frame(frame), slots);
            inputs.Record(new Frame(frame), new TestInput(frame));
        }

        Assert.Null(snapshots.Get(new Frame(1)));
        Assert.Equal(2, snapshots.Get(new Frame(2))!.GetInt("frame"));
        Assert.Empty(inputs.GetFrameBatches(Frame.Zero, new Frame(1))[0].Inputs);
        Assert.Single(inputs.GetFrameBatches(new Frame(1), new Frame(2))[0].Inputs);
        Assert.Equal(3, snapshotStorage.Count);
        Assert.Equal(3, inputStorage.Count);
    }

    [Fact]
    public void PredictionCoordinator_ExposesOnlyAvailableCapacityControls()
    {
        var defaults = new PredictionCoordinator(7);
        var disabled = new PredictionCoordinator(
            7,
            bufferOptions: PredictionCoordinatorBufferOptions.Disabled);

        Assert.NotNull(defaults.PredictedStateHistoryCapacityControl);
        Assert.NotNull(defaults.InputHistoryCapacityControl);
        Assert.Null(disabled.PredictedStateHistoryCapacityControl);
        Assert.Null(disabled.InputHistoryCapacityControl);
    }

    private static StateSnapshot Snapshot(int frame) => new() { Frame = frame };

    private sealed record TestInput(int Value) : IInputCommand;
}

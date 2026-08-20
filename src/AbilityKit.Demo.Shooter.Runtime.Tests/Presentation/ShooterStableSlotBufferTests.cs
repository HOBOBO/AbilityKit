using AbilityKit.Demo.Shooter.View;
using Xunit;

namespace AbilityKit.Demo.Shooter.Runtime.Tests.Presentation;

public sealed class ShooterStableSlotBufferTests
{
    [Fact]
    public void RemoveUsesSwapRemoveAndMarksMovedSlotDirty()
    {
        var buffer = new ShooterStableSlotBuffer<int>();
        var first = new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, 1);
        var removed = new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, 2);
        var moved = new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, 3);
        buffer.Upsert(first, 10);
        buffer.Upsert(removed, 20);
        buffer.Upsert(moved, 30);

        buffer.BeginUpdate();
        var didRemove = buffer.Remove(removed);

        Assert.True(didRemove);
        Assert.Equal(2, buffer.Count);
        Assert.False(buffer.Contains(removed));
        Assert.True(buffer.TryGetValue(moved, out var movedValue));
        Assert.Equal(30, movedValue);
        Assert.Equal(30, buffer.Values[1]);
        Assert.True(buffer.CountChanged);
        Assert.Equal(new[] { 1 }, buffer.DirtySlots);
    }

    [Fact]
    public void SparseSteadyStateUpdatesAreAllocationFreeAfterWarmup()
    {
        const int entityCount = 2048;
        const int changedEntityStride = 20;
        var buffer = new ShooterStableSlotBuffer<int>();
        var keys = new ShooterViewEntityKey[entityCount];
        buffer.EnsureCapacity(entityCount);
        for (var i = 0; i < entityCount; i++)
        {
            keys[i] = new ShooterViewEntityKey(ShooterViewEntityKind.Enemy, i + 1);
            buffer.Upsert(keys[i], i);
        }

        for (var warmup = 0; warmup < 8; warmup++)
        {
            ApplySparseUpdate(buffer, keys, warmup, changedEntityStride);
        }

        var checksum = 0;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 64; iteration++)
        {
            ApplySparseUpdate(buffer, keys, iteration, changedEntityStride);
            checksum += buffer.DirtySlots.Count;
        }

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(103 * 64, checksum);
        Assert.Equal(entityCount, buffer.Count);
        Assert.Equal(0, allocatedBytes);
    }

    private static void ApplySparseUpdate(
        ShooterStableSlotBuffer<int> buffer,
        ShooterViewEntityKey[] keys,
        int iteration,
        int stride)
    {
        buffer.BeginUpdate();
        for (var i = 0; i < keys.Length; i += stride)
        {
            var value = iteration + i;
            buffer.Upsert(keys[i], in value);
        }
    }
}

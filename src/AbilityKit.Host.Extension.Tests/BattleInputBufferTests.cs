using AbilityKit.Ability.Host.Extensions.Server.BattleHost;
using AbilityKit.Core.Buffers;
using Xunit;

namespace AbilityKit.Host.Extension.Tests;

public sealed class BattleInputBufferTests
{
    [Fact]
    public void DefaultBuffer_RemainsUnboundedForCompatibility()
    {
        var buffer = new BattleInputBuffer<int>();

        for (var frame = 0; frame < 64; frame++)
        {
            Assert.True(buffer.Enqueue(frame, frame));
        }

        Assert.False(buffer.IsCapacityBounded);
        Assert.Equal(64, buffer.PendingFrameCount);
    }

    [Fact]
    public void BoundedBuffer_RejectsOnlyNewFramesAtCapacity()
    {
        var buffer = new BattleInputBuffer<int>(maxPendingFrames: 2);

        Assert.True(buffer.Enqueue(10, 1));
        Assert.True(buffer.Enqueue(20, 2));
        Assert.True(buffer.Enqueue(20, 3));
        Assert.False(buffer.Enqueue(30, 4));

        Assert.Equal(2, buffer.PendingFrameCount);
        Assert.Equal(2, buffer.Drain(20).Count);
    }

    [Fact]
    public void RuntimeShrink_RejectsCapacityThatWouldDiscardPendingInputs()
    {
        var buffer = new BattleInputBuffer<int>();
        buffer.Enqueue(10, 1);
        buffer.Enqueue(20, 2);
        var capacity = Assert.IsAssignableFrom<IBufferCapacityControl>(buffer);

        Assert.False(capacity.TrySetCapacity(1));
        Assert.Equal(int.MaxValue, capacity.Capacity);

        buffer.Drain(10);
        Assert.True(capacity.TrySetCapacity(1));
        Assert.Equal(1, capacity.Capacity);
        Assert.False(buffer.Enqueue(30, 3));
    }
}

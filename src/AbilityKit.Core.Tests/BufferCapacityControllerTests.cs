using AbilityKit.Core.Buffers;
using System;
using Xunit;

namespace AbilityKit.Core.Tests;

public sealed class BufferCapacityControllerTests
{
    [Fact]
    public void Update_ClampsPolicyTargetAndAppliesCapacity()
    {
        var buffer = new TestCapacityControl(8);
        var controller = new BufferCapacityController<int>(
            buffer,
            new OffsetPolicy(),
            minCapacity: 4,
            maxCapacity: 16);

        Assert.True(controller.Update(20));

        Assert.Equal(16, buffer.Capacity);
        Assert.Equal(16, controller.LastTargetCapacity);
    }

    [Fact]
    public void Update_DoesNotWriteWhenTargetMatchesCurrentCapacity()
    {
        var buffer = new TestCapacityControl(8);
        var controller = new BufferCapacityController<int>(buffer, new OffsetPolicy());

        Assert.False(controller.Update(0));
        Assert.Equal(0, buffer.SetAttempts);
    }

    [Fact]
    public void TrySetTargetCapacity_PreservesRejectedControlCapacity()
    {
        var buffer = new TestCapacityControl(8) { RejectChanges = true };
        var controller = new BufferCapacityController<int>(buffer, new OffsetPolicy());

        Assert.False(controller.TrySetTargetCapacity(12));

        Assert.Equal(8, buffer.Capacity);
        Assert.Equal(12, controller.LastTargetCapacity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNonPositiveControlCapacity(int capacity)
    {
        var control = new TestCapacityControl(capacity);

        var exception = Assert.Throws<ArgumentException>(() =>
            new BufferCapacityController<int>(control, new OffsetPolicy()));

        Assert.Equal("capacityControl", exception.ParamName);
    }

    [Fact]
    public void TrySetTargetCapacity_ClampsIntegerExtremesWithoutOverflow()
    {
        var buffer = new TestCapacityControl(8);
        var controller = new BufferCapacityController<int>(
            buffer,
            new OffsetPolicy(),
            minCapacity: 4,
            maxCapacity: 16);

        Assert.True(controller.TrySetTargetCapacity(int.MinValue));
        Assert.Equal(4, buffer.Capacity);
        Assert.True(controller.TrySetTargetCapacity(int.MaxValue));
        Assert.Equal(16, buffer.Capacity);
    }

    private sealed class OffsetPolicy : IBufferCapacityPolicy<int>
    {
        public int GetTargetCapacity(int sample, int currentCapacity) => currentCapacity + sample;
    }

    private sealed class TestCapacityControl : IBufferCapacityControl
    {
        public TestCapacityControl(int capacity) => Capacity = capacity;

        public int Capacity { get; private set; }

        public int SetAttempts { get; private set; }

        public bool RejectChanges { get; set; }

        public bool TrySetCapacity(int capacity)
        {
            SetAttempts++;
            if (RejectChanges || capacity <= 0) return false;
            Capacity = capacity;
            return true;
        }
    }
}

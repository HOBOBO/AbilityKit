using AbilityKit.Core.Collections;
using Xunit;

namespace AbilityKit.Core.Tests;

public sealed class SortedIntSetTests
{
    [Fact]
    public void Add_keeps_unique_values_in_ascending_order()
    {
        var values = new SortedIntSet();

        Assert.True(values.Add(30));
        Assert.True(values.Add(10));
        Assert.True(values.Add(20));
        Assert.False(values.Add(20));

        Assert.Equal(3, values.Count);
        Assert.Equal(10, values[0]);
        Assert.Equal(20, values[1]);
        Assert.Equal(30, values[2]);
    }

    [Theory]
    [InlineData(5, 0, 0)]
    [InlineData(10, 0, 1)]
    [InlineData(15, 1, 1)]
    [InlineData(20, 1, 2)]
    [InlineData(30, 2, 3)]
    [InlineData(35, 3, 3)]
    public void Bounds_locate_half_open_ranges(int value, int lower, int upper)
    {
        var values = Create(10, 20, 30);

        Assert.Equal(lower, values.LowerBound(value));
        Assert.Equal(upper, values.UpperBound(value));
    }

    [Fact]
    public void Remove_and_remove_range_preserve_order_and_membership()
    {
        var values = Create(10, 20, 30, 40, 50);

        Assert.True(values.Remove(30));
        Assert.False(values.Remove(30));
        values.RemoveRange(0, 2);

        Assert.Equal(2, values.Count);
        Assert.Equal(40, values[0]);
        Assert.Equal(50, values[1]);
        Assert.True(values.Contains(40));
        Assert.False(values.Contains(20));
    }

    [Fact]
    public void Reused_capacity_has_no_steady_state_allocation()
    {
        var values = new SortedIntSet(32);
        Exercise(values);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 100; iteration++)
            Exercise(values);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Negative_capacity_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SortedIntSet(-1));
    }

    private static SortedIntSet Create(params int[] source)
    {
        var values = new SortedIntSet(source.Length);
        for (var index = 0; index < source.Length; index++)
            values.Add(source[index]);
        return values;
    }

    private static void Exercise(SortedIntSet values)
    {
        values.Clear();
        for (var value = 31; value >= 0; value--)
            values.Add(value);

        var removeCount = values.LowerBound(16);
        values.RemoveRange(0, removeCount);
    }
}

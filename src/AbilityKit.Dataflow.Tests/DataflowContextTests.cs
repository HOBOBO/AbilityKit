using AbilityKit.Dataflow;
using Xunit;

namespace AbilityKit.Dataflow.Tests;

public sealed class DataflowContextTests
{
    [Fact]
    public void Slots_with_same_name_and_different_types_are_isolated()
    {
        var intSlot = new DataflowSlot<int>("Shared");
        var stringSlot = new DataflowSlot<string>("Shared");
        var context = new DataflowContext();

        context.SetData(intSlot, 42);
        context.SetData(stringSlot, "value");

        Assert.Equal(42, context.GetData(intSlot));
        Assert.Equal("value", context.GetData(stringSlot));
    }

    [Fact]
    public void Distinct_slot_instances_with_same_name_and_type_share_a_value()
    {
        var context = new DataflowContext();
        context.SetData(new DataflowSlot<int>("Shared"), 42);

        Assert.True(context.ContainsData(new DataflowSlot<int>("Shared")));
        Assert.Equal(42, context.GetData(new DataflowSlot<int>("Shared")));
    }

    [Fact]
    public void Explicit_null_reference_is_present_and_retrievable()
    {
        var slot = new DataflowSlot<string?>("Optional");
        var context = new DataflowContext();
        context.SetData(slot, null);

        var found = context.TryGetData(slot, out var value);

        Assert.True(context.ContainsData(slot));
        Assert.True(found);
        Assert.Null(value);
    }

    [Fact]
    public void Clear_dispatches_to_derived_reset_and_clears_all_state()
    {
        var slot = new DataflowSlot<int>("Value");
        var context = new DerivedContext { DerivedValue = 7 };
        context.SetSource(new object());
        context.SetData(slot, 42);
        context.Abort();

        context.Clear();

        Assert.Equal(1, context.ResetCount);
        Assert.Equal(0, context.DerivedValue);
        Assert.Null(context.Source);
        Assert.False(context.ContainsData(slot));
        Assert.False(context.IsAborted);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Slot_requires_a_non_whitespace_name(string name)
    {
        Assert.Throws<ArgumentException>(() => new DataflowSlot<int>(name));
    }

    [Fact]
    public void Null_slot_cannot_be_converted_to_name()
    {
        DataflowSlot<int> slot = null!;
        Assert.Throws<ArgumentNullException>(() => _ = (string)slot);
    }

    private sealed class DerivedContext : DataflowContext
    {
        public int DerivedValue { get; set; }
        public int ResetCount { get; private set; }

        public override void Reset()
        {
            base.Reset();
            DerivedValue = 0;
            ResetCount++;
        }
    }
}

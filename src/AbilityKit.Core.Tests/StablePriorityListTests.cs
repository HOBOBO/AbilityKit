using AbilityKit.Core.Collections;
using Xunit;

namespace AbilityKit.Core.Tests;

public sealed class StablePriorityListTests
{
    [Theory]
    [InlineData(PriorityDirection.Ascending)]
    [InlineData(PriorityDirection.Descending)]
    public void Extreme_priorities_preserve_registration_order_after_updates(PriorityDirection direction)
    {
        var list = new StablePriorityList<string>(direction);
        list.Add("first", int.MaxValue);
        list.Add("second", int.MinValue);
        list.Add("third", int.MaxValue);

        Assert.True(list.TryUpdatePriority(item => item == "first", int.MinValue));
        Assert.True(list.TryUpdatePriority(item => item == "third", int.MinValue));
        list.Add("fourth", int.MinValue);

        Assert.Equal(new[] { "first", "second", "third", "fourth" }, list);
        Assert.False(list.TryUpdatePriority(item => item == "missing", 0));
        Assert.False(list.RemoveFirst(item => item == "missing"));
        list.Clear();
        list.Add("new.first");
        list.Add("new.second");
        Assert.Equal(new[] { "new.first", "new.second" }, list);
    }

    [Fact]
    public void Predicate_failures_propagate_without_removing_or_reordering_items()
    {
        var list = new StablePriorityList<string>();
        list.Add("first");
        list.Add("second", 1);
        var failure = new InvalidOperationException("匹配回调失败");

        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => list.RemoveFirst(item =>
            item == "first" ? false : throw failure)));
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => list.TryUpdatePriority(item =>
            item == "first" ? false : throw failure, -1)));
        Assert.Throws<ArgumentNullException>(() => list.RemoveFirst(null!));
        Assert.Throws<ArgumentNullException>(() => list.TryUpdatePriority(null!, 0));
        Assert.Equal(new[] { "first", "second" }, list);
    }

    [Fact]
    public void Ascending_order_is_stable_for_equal_priorities()
    {
        var list = new StablePriorityList<string>();
        list.Add("late", 100);
        list.Add("first", 0);
        list.Add("stable.second", 100);

        Assert.Equal(new[] { "first", "late", "stable.second" }, list);
    }

    [Fact]
    public void Descending_order_places_higher_priority_first()
    {
        var list = new StablePriorityList<string>(PriorityDirection.Descending);
        list.Add("low", 0);
        list.Add("high.first", 10);
        list.Add("high.second", 10);

        Assert.Equal(new[] { "high.first", "high.second", "low" }, list);
    }

    [Fact]
    public void Priority_update_preserves_original_registration_sequence()
    {
        var list = new StablePriorityList<string>();
        list.Add("first", 10);
        list.Add("second", 20);
        list.Add("third", 30);

        Assert.True(list.TryUpdatePriority(item => item == "third", 10));

        Assert.Equal(new[] { "first", "third", "second" }, list);
    }

    [Fact]
    public void Remove_first_removes_only_the_matching_registration()
    {
        var list = new StablePriorityList<string>();
        list.Add("duplicate", 0);
        list.Add("duplicate", 0);

        Assert.True(list.RemoveFirst(item => item == "duplicate"));
        Assert.Single(list);
    }
}

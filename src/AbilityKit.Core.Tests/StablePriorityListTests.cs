using AbilityKit.Core.Collections;
using Xunit;

namespace AbilityKit.Core.Tests;

public sealed class StablePriorityListTests
{
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

using AbilityKit.Ability.Host.Hooks;
using Xunit;

namespace AbilityKit.Host.Tests;

public sealed class HookTests
{
    [Fact]
    public void Invoke_uses_stable_order_and_remove_targets_registration()
    {
        var calls = new List<string>();
        var hook = new Hook<int>();
        Action<int> late = _ => calls.Add("late");
        Action<int> first = _ => calls.Add("first");
        Action<int> stableSecond = _ => calls.Add("stable.second");

        hook.Add(late, order: 100);
        hook.Add(first, order: 0);
        hook.Add(stableSecond, order: 100);
        hook.Invoke(1);

        Assert.Equal(new[] { "first", "late", "stable.second" }, calls);
        Assert.True(hook.Remove(late));
        Assert.False(hook.Remove(late));

        calls.Clear();
        hook.Invoke(2);
        Assert.Equal(new[] { "first", "stable.second" }, calls);
    }
}

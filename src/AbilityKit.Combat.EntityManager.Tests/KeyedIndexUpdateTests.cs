using AbilityKit.Ability.Battle.EntityManager;
using Xunit;

namespace AbilityKit.Combat.EntityManager.Tests;

public sealed class KeyedIndexUpdateTests
{
    [Fact]
    public void SetKeyUpdate_construct_and_read()
    {
        var u = new SetKeyUpdate<int>(1, 42);
        Assert.Equal(1, u.UpdateType);
        Assert.Equal(42, u.Key);
    }

    [Fact]
    public void AddKeyUpdate_construct_and_read()
    {
        var u = new AddKeyUpdate<string>(2, "hero");
        Assert.Equal(2, u.UpdateType);
        Assert.Equal("hero", u.Key);
    }

    [Fact]
    public void RemoveKeyUpdate_construct_and_read()
    {
        var u = new RemoveKeyUpdate<long>(3, 99L);
        Assert.Equal(3, u.UpdateType);
        Assert.Equal(99L, u.Key);
    }
}

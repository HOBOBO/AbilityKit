using AbilityKit.Game.Battle;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class BattleDebugEntityIdTests
{
    [Fact]
    public void ActorId_DefinesValueEqualityAndHashCode()
    {
        var left = new BattleDebugEntityId(42);
        var right = new BattleDebugEntityId(42);
        var other = new BattleDebugEntityId(43);

        Assert.Equal(left, right);
        Assert.True(left == right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.NotEqual(left, other);
        Assert.True(left != other);
    }

    [Fact]
    public void CompareTo_OrdersByActorId()
    {
        var ids = new[]
        {
            new BattleDebugEntityId(30),
            new BattleDebugEntityId(10),
            new BattleDebugEntityId(20)
        };

        Array.Sort(ids);

        Assert.Equal(new[] { 10, 20, 30 }, ids.Select(id => id.ActorId));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(-1, true)]
    public void IsValid_RejectsOnlyCompatibilitySentinelZero(
        int actorId,
        bool expected)
    {
        var id = new BattleDebugEntityId(actorId);

        Assert.Equal(expected, id.IsValid);
        Assert.Equal(actorId.ToString(), id.ToString());
    }
}

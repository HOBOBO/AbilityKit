using AbilityKit.Game.Flow;
using Xunit;

namespace AbilityKit.Demo.Moba.View.Runtime.Tests;

public sealed class ReferenceBindingOwnerTests
{
    [Fact]
    public void Bind_AdvancesGenerationAndPublishesOwnership()
    {
        var owner = new ReferenceBindingOwner<object>();
        var value = new object();

        var generation = owner.Bind(value, ownsValue: true);

        Assert.Same(value, owner.Value);
        Assert.True(owner.OwnsValue);
        Assert.Equal(generation, owner.Generation);
        Assert.True(owner.IsCurrent(generation, value));
    }

    [Fact]
    public void TryClear_RejectsStaleGenerationAndDifferentReference()
    {
        var owner = new ReferenceBindingOwner<object>();
        var stale = new object();
        var current = new object();
        var staleGeneration = owner.Bind(stale);
        var currentGeneration = owner.Bind(current);

        Assert.False(owner.TryClear(staleGeneration, stale, out _, out _));
        Assert.False(owner.TryClear(currentGeneration, stale, out _, out _));
        Assert.Same(current, owner.Value);
    }

    [Fact]
    public void TryClear_CurrentBindingReturnsReleasedValueAndOwnership()
    {
        var owner = new ReferenceBindingOwner<object>();
        var value = new object();
        var generation = owner.Bind(value, ownsValue: true);

        var cleared = owner.TryClear(
            generation,
            value,
            out var released,
            out var owned);

        Assert.True(cleared);
        Assert.Same(value, released);
        Assert.True(owned);
        Assert.Null(owner.Value);
        Assert.False(owner.OwnsValue);
        Assert.False(owner.IsCurrent(generation, value));
    }

    [Fact]
    public void Reset_IsIdempotentAndInvalidatesPreviousGeneration()
    {
        var owner = new ReferenceBindingOwner<object>();
        var value = new object();
        var generation = owner.Bind(value);

        Assert.True(owner.Reset(out var released, out var owned));
        Assert.Same(value, released);
        Assert.False(owned);
        Assert.False(owner.Reset(out released, out owned));
        Assert.Null(released);
        Assert.False(owned);
        Assert.False(owner.IsCurrent(generation, value));
    }
}

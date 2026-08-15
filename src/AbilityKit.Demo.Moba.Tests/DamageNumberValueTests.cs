using AbilityKit.Demo.Moba;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests;

public sealed class DamageNumberValueTests
{
    [Fact]
    public void BaseAddMul_EvaluatesDamagePipelineStages()
    {
        var value = new DamageNumberValue(DamageNumberValueMode.BaseAddMul, 10f);
        value.Apply(new DamageNumberModifier(DamageNumberModifierOp.Add, 5f));
        value.Apply(new DamageNumberModifier(DamageNumberModifierOp.Mul, 0.5f));
        value.Apply(new DamageNumberModifier(DamageNumberModifierOp.FinalAdd, 2f));

        Assert.Equal(24.5f, value.Value);
    }

    [Fact]
    public void Remove_PreservesLatestOverrideByRegistrationOrder()
    {
        var value = new DamageNumberValue(DamageNumberValueMode.OverrideOnly, 5f);
        var first = value.Apply(new DamageNumberModifier(DamageNumberModifierOp.Override, 10f));
        var second = value.Apply(new DamageNumberModifier(DamageNumberModifierOp.Override, 20f));
        var unrelated = value.Apply(new DamageNumberModifier(DamageNumberModifierOp.Add, 3f));

        Assert.Equal(20f, value.Value);
        Assert.True(value.Remove(unrelated));
        Assert.Equal(20f, value.Value);
        Assert.True(value.Remove(second));
        Assert.Equal(10f, value.Value);
        Assert.True(value.Remove(first));
        Assert.Equal(5f, value.Value);
    }

    [Fact]
    public void Clear_RemovesOnlyMatchingSourceWhenSourceIsSpecified()
    {
        var value = new DamageNumberValue(DamageNumberValueMode.BaseAdd, 1f);
        value.Apply(new DamageNumberModifier(DamageNumberModifierOp.Add, 2f, sourceId: 10));
        value.Apply(new DamageNumberModifier(DamageNumberModifierOp.FinalAdd, 3f, sourceId: 20));

        value.Clear(sourceId: 10);

        Assert.Equal(4f, value.Value);
    }
}

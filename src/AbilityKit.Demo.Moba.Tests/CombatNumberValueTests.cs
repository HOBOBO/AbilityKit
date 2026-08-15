using AbilityKit.Demo.Moba;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests;

public sealed class CombatNumberValueTests
{
    [Fact]
    public void BaseAddMul_EvaluatesDamagePipelineStages()
    {
        var value = new CombatNumberValue(CombatNumberValueMode.BaseAddMul, 10f);
        value.Apply(new CombatNumberModifier(CombatNumberModifierOp.Add, 5f));
        value.Apply(new CombatNumberModifier(CombatNumberModifierOp.Mul, 0.5f));
        value.Apply(new CombatNumberModifier(CombatNumberModifierOp.FinalAdd, 2f));

        Assert.Equal(24.5f, value.Value);
    }

    [Fact]
    public void Remove_PreservesLatestOverrideByRegistrationOrder()
    {
        var value = new CombatNumberValue(CombatNumberValueMode.OverrideOnly, 5f);
        var first = value.Apply(new CombatNumberModifier(CombatNumberModifierOp.Override, 10f));
        var second = value.Apply(new CombatNumberModifier(CombatNumberModifierOp.Override, 20f));
        var unrelated = value.Apply(new CombatNumberModifier(CombatNumberModifierOp.Add, 3f));

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
        var value = new CombatNumberValue(CombatNumberValueMode.BaseAdd, 1f);
        value.Apply(new CombatNumberModifier(CombatNumberModifierOp.Add, 2f, sourceId: 10));
        value.Apply(new CombatNumberModifier(CombatNumberModifierOp.FinalAdd, 3f, sourceId: 20));

        value.Clear(sourceId: 10);

        Assert.Equal(4f, value.Value);
    }
}

using AbilityKit.Core.Numerics;
using Xunit;

#pragma warning disable CS0618 // Compatibility tests cover the deprecated Core numerics surface until its major-version removal.

namespace AbilityKit.Core.Tests;

public sealed class NumberValueTests
{
    [Fact]
    public void Base_add_mul_evaluates_documented_stages()
    {
        var value = new NumberValue(NumberValueMode.BaseAddMul, 10f);
        value.Apply(new NumberModifier(NumberModifierOp.Add, 5f));
        value.Apply(new NumberModifier(NumberModifierOp.Mul, 0.5f));
        value.Apply(new NumberModifier(NumberModifierOp.FinalAdd, 2f));

        Assert.Equal(24.5f, value.Value);
    }

    [Fact]
    public void Rebuild_preserves_latest_override_by_handle_order()
    {
        var value = new NumberValue(NumberValueMode.OverrideOnly, 5f);
        var firstOverride = value.Apply(new NumberModifier(NumberModifierOp.Override, 10f));
        var secondOverride = value.Apply(new NumberModifier(NumberModifierOp.Override, 20f));
        var unrelated = value.Apply(new NumberModifier(NumberModifierOp.Add, 3f));

        Assert.Equal(20f, value.Value);
        Assert.True(value.Remove(unrelated));
        Assert.Equal(20f, value.Value);
        Assert.True(value.Remove(secondOverride));
        Assert.Equal(10f, value.Value);
        Assert.True(value.Remove(firstOverride));
        Assert.Equal(5f, value.Value);
    }

    [Fact]
    public void Effect_handle_removes_all_effect_modifiers_idempotently()
    {
        var value = new NumberValue(NumberValueMode.BaseAdd, 1f);
        var handle = value.ApplyEffect(new NumberEffect(
            new NumberEffect.Entry(new NumberModifier(NumberModifierOp.Add, 2f)),
            new NumberEffect.Entry(new NumberModifier(NumberModifierOp.FinalAdd, 3f))));

        Assert.Equal(6f, value.Value);
        handle!.Dispose();
        handle.Dispose();
        Assert.Equal(1f, value.Value);
    }
}

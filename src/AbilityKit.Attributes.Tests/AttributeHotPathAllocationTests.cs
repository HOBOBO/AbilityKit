using AbilityKit.Attributes.Core;
using AbilityKit.Attributes.Formula;
using AbilityKit.Modifiers;
using Xunit;

namespace AbilityKit.Attributes.Tests;

public sealed class AttributeHotPathAllocationTests
{
    private const int Iterations = 1024;

    [Fact]
    public void Default_formula_with_modifiers_has_zero_steady_state_recompute_allocations()
    {
        var (context, attribute) = CreateAttribute();
        context.AddModifier(attribute, ModifierOp.Add, 5f);

        WarmUp(context, attribute);

        var allocated = MeasureDirtyRecomputes(context, attribute);

        Assert.Equal(0, allocated);
        Assert.Equal(106f, context.GetValue(attribute));
    }

    [Fact]
    public void Expression_formula_has_zero_steady_state_recompute_allocations()
    {
        var formula = new AttributeExpressionFormula("(base + add) * max(2, 1)");
        var (context, attribute) = CreateAttribute(formula);
        context.AddModifier(attribute, ModifierOp.Add, 5f);

        WarmUp(context, attribute);

        var allocated = MeasureDirtyRecomputes(context, attribute);

        Assert.Equal(0, allocated);
        Assert.Equal(212f, context.GetValue(attribute));
    }

    [Fact]
    public void Modifier_removal_does_not_reintroduce_recompute_allocations()
    {
        var (context, attribute) = CreateAttribute();
        var retained = context.AddModifier(attribute, ModifierOp.Add, 5f);
        var removed = context.AddModifier(attribute, ModifierOp.Mul, 2f);
        Assert.True(context.RemoveModifier(attribute, removed));
        Assert.True(retained > 0);

        WarmUp(context, attribute);

        var allocated = MeasureDirtyRecomputes(context, attribute);

        Assert.Equal(0, allocated);
        Assert.Equal(106f, context.GetValue(attribute));
    }

    [Fact]
    public void Existing_group_lookup_by_name_has_zero_steady_state_allocations()
    {
        var (context, _) = CreateAttribute();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Iterations; i++)
        {
            _ = context.GetOrCreateGroup(string.Empty);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Modifier_calculator_has_zero_steady_state_allocations()
    {
        var calculator = new ModifierCalculator();
        var modifiers = new[] { ModifierData.Add(ModifierKey.None, 5f) };
        var context = new AttributeContext();

        for (var i = 0; i < 256; i++)
        {
            _ = calculator.Calculate(modifiers, (i & 1) == 0 ? 100f : 101f, context);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Iterations; i++)
        {
            _ = calculator.Calculate(modifiers, (i & 1) == 0 ? 100f : 101f, context);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Public_modifier_data_snapshot_is_not_overwritten_by_recompute_scratch()
    {
        var (context, attribute) = CreateAttribute();
        context.AddModifier(attribute, ModifierOp.Add, 5f, sourceId: 11);
        var instance = context.GetGroupFor(attribute).GetOrCreate(attribute);
        var snapshot = instance.GetActiveModifierData().ToArray();

        context.AddModifier(attribute, ModifierOp.Add, 7f, sourceId: 22);
        context.SetBase(attribute, 100f);
        _ = context.GetValue(attribute);

        Assert.Single(snapshot);
        Assert.Equal(11, snapshot[0].SourceId);
        Assert.Equal(5f, snapshot[0].Magnitude.Calculate());
    }

    private static (AttributeContext Context, AttributeId Attribute) CreateAttribute(
        IAttributeFormula? formula = null)
    {
        var name = $"allocation_{Guid.NewGuid():N}";
        var definition = formula == null
            ? new AttributeDef(name, defaultBaseValue: 100f)
            : new AttributeDef(name, defaultBaseValue: 100f, formula: formula);
        var attribute = AttributeRegistry.DefaultRegistry.Register(definition);
        var context = new AttributeContext();
        _ = context.GetValue(attribute);
        return (context, attribute);
    }

    private static void WarmUp(AttributeContext context, AttributeId attribute)
    {
        for (var i = 0; i < 256; i++)
        {
            context.SetBase(attribute, (i & 1) == 0 ? 100f : 101f);
            _ = context.GetValue(attribute);
        }
    }

    private static long MeasureDirtyRecomputes(AttributeContext context, AttributeId attribute)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Iterations; i++)
        {
            context.SetBase(attribute, (i & 1) == 0 ? 100f : 101f);
            _ = context.GetValue(attribute);
        }
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}

using AbilityKit.Attributes.Core;
using AbilityKit.Attributes.Constraint;
using AbilityKit.Attributes.Formula;
using AbilityKit.Modifiers;
using Xunit;

namespace AbilityKit.Attributes.Tests;

public sealed class SilentAttributeEvaluationTests
{
    [Fact]
    public void Silent_read_updates_cache_with_modifiers_and_constraints_without_any_notifications()
    {
        var registry = new TestRegistry();
        var id = registry.Register(new AttributeDef("max", constraint: new UpperBound()));
        var context = new AttributeContext { Registry = registry };
        var events = Observe(context, id);
        context.SetBase(id, 100);
        context.AddModifier(id, ModifierOp.Add, 30);
        Assert.Equal(120, context.GetValueSilently(id));
        Assert.Equal((0, 0, 0), events());
        Assert.Equal(120, context.GetValue(id));
        Assert.Equal((0, 0, 0), events());
        context.SetBase(id, 50);
        Assert.Equal(80, context.GetValue(id));
        Assert.Equal((1, 1, 1), events());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Nested_reads_remain_silent_and_dependency_invalidation_is_preserved(bool nestedSilent)
    {
        var registry = new TestRegistry();
        var source = registry.Register(new AttributeDef("source"));
        var derived = registry.Register(new AttributeDef("derived", "other", formula: new ReadFormula(source, nestedSilent), dependsOn: source));
        var context = new AttributeContext { Registry = registry };
        context.SetBase(source, 10);
        Assert.Equal(20, context.GetValue(derived));
        var sourceEvents = Observe(context, source);
        var derivedEvents = Observe(context, derived);
        context.SetBase(source, 20);
        Assert.Equal(20, context.GetValueSilently(source));
        Assert.Equal(40, context.GetValueSilently(derived));
        Assert.Equal((0, 0, 0), sourceEvents());
        Assert.Equal((0, 0, 0), derivedEvents());

        /*
         * 显式标脏 derived，而 source 的再次变化尚未求值。
         * derived 内的普通/静默嵌套读取都必须继承本次静默范围。
         */
        context.SetBase(source, 30);
        context.SetBase(derived, 1);
        Assert.Equal(60, context.GetValueSilently(derived));
        Assert.Equal((0, 0, 0), sourceEvents());
        Assert.Equal((0, 0, 0), derivedEvents());
        Assert.Equal(60, context.GetValue(derived));
        Assert.Equal((0, 0, 0), derivedEvents());
        context.SetBase(source, 40);
        Assert.Equal(40, context.GetValue(source));
        Assert.Equal(80, context.GetValue(derived));
        Assert.Equal((1, 1, 1), sourceEvents());
        Assert.Equal((1, 1, 1), derivedEvents());
    }

    [Fact]
    public void Formula_exception_unwinds_nested_silent_scope_and_does_not_replay_notifications()
    {
        var registry = new TestRegistry();
        var source = registry.Register(new AttributeDef("source"));
        var formula = new ThrowAfterReadFormula(source);
        var broken = registry.Register(new AttributeDef("broken", formula: formula));
        var context = new AttributeContext { Registry = registry };
        context.SetBase(source, 10);
        var events = Observe(context, source);
        Assert.Throws<InvalidOperationException>(() => context.GetValueSilently(broken));
        Assert.Equal((0, 0, 0), events());
        context.SetBase(source, 20);
        Assert.Equal(20, context.GetValue(source));
        Assert.Equal((1, 1, 1), events());
        formula.Throws = false;
        Assert.Equal(20, context.GetValue(broken));
        Assert.Equal((1, 1, 1), events());
    }

    [Fact]
    public void Silent_scope_does_not_propagate_to_other_contexts()
    {
        var registry = new TestRegistry();
        var source = registry.Register(new AttributeDef("source"));
        var other = new AttributeContext { Registry = registry };
        other.SetBase(source, 15);
        var id = registry.Register(new AttributeDef("cross", formula: new CrossContextFormula(other, source)));
        var context = new AttributeContext { Registry = registry };
        var localEvents = Observe(context, id);
        var otherEvents = Observe(other, source);
        Assert.Equal(15, context.GetValueSilently(id));
        Assert.Equal((0, 0, 0), localEvents());
        Assert.Equal((1, 1, 1), otherEvents());
    }

    private static Func<(int, int, int)> Observe(AttributeContext context, AttributeId id)
    {
        int instance = 0, group = 0, all = 0;
        var g = context.GetGroupFor(id);
        g.GetOrCreate(id).Changed += (_, _, _) => instance++;
        g.AttributeChanged += (changed, _, _) => { if (changed == id) group++; };
        context.AttributeChanged += (_, changed, _, _) => { if (changed == id) all++; };
        return () => (instance, group, all);
    }

    private sealed class UpperBound : IAttributeConstraint
    {
        public float Apply(AttributeId id, float value) => Math.Min(120, value);
    }

    private sealed class ReadFormula(AttributeId source, bool silent) : IAttributeFormula
    {
        public float Evaluate(AttributeContext ctx, AttributeId self, float baseValue, ModifierResult result)
            => 2 * (silent ? ctx.GetValueSilently(source) : ctx.GetValue(source));
    }

    private sealed class ThrowAfterReadFormula(AttributeId source) : IAttributeFormula
    {
        public bool Throws = true;
        public float Evaluate(AttributeContext ctx, AttributeId self, float baseValue, ModifierResult result)
        {
            var value = ctx.GetValueSilently(source);
            if (Throws) throw new InvalidOperationException("公式故障注入");
            return value;
        }
    }

    private sealed class CrossContextFormula(AttributeContext other, AttributeId source) : IAttributeFormula
    {
        public float Evaluate(AttributeContext ctx, AttributeId self, float baseValue, ModifierResult result)
            => other.GetValue(source);
    }

    private sealed class TestRegistry : IAttributeRegistry
    {
        private readonly List<AttributeDef> _defs = new();
        public bool IsFrozen => false;
        public AttributeId Register(AttributeDef def)
        {
            def.Id = AttributeId.FromRaw(_defs.Count + 1);
            _defs.Add(def);
            return def.Id;
        }
        public AttributeId Request(string name) => throw new NotSupportedException();
        public AttributeId Request(string name, out bool created) => throw new NotSupportedException();
        public void Freeze() => throw new NotSupportedException();
        public AttributeDef GetDef(AttributeId id) => _defs[id.RawId - 1];
        public string GetName(AttributeId id) => GetDef(id).Name;
        public string GetGroup(AttributeId id) => GetDef(id).Group;
        public float GetDefaultBaseValue(AttributeId id) => GetDef(id).DefaultBaseValue;
        public IAttributeFormula GetFormula(AttributeId id) => GetDef(id).Formula ?? DefaultAttributeFormula.Instance;
        public IAttributeConstraint GetConstraint(AttributeId id) => GetDef(id).Constraint;
        public IEnumerable<AttributeId> GetDependencies(AttributeId id) => GetDef(id).GetDependencies();
        public IReadOnlyList<AttributeId> GetDependents(AttributeId id)
            => _defs.Where(def => def.GetDependencies().Contains(id)).Select(def => def.Id).ToArray();
    }
}

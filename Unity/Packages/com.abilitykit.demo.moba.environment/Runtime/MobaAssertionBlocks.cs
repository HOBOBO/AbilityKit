using AbilityKit.BattleFlow;

namespace AbilityKit.Demo.Moba.EnvironmentModel
{
    /// <summary>MOBA 断言积木基类：多个断言积木累积到一个 <see cref="MobaBattleFlowAssertions"/>（opaque），塞进 TestScenario.Expectations。纯 C#，.NET 与 Unity 共用。</summary>
    public abstract class MobaAssertionBlock : BattleAtomicBlock
    {
        public override BattleBlockSection Section => BattleBlockSection.Assertion;

        public override void Compile(BattleFlowBuilder builder)
        {
            var assertions = builder.Expectations as MobaBattleFlowAssertions ?? new MobaBattleFlowAssertions();
            Apply(assertions);
            builder.SetExpectations(assertions);
        }

        protected abstract void Apply(MobaBattleFlowAssertions assertions);
    }

    /// <summary>断言：trace 必须出现（mustContain）。</summary>
    public sealed class AssertTraceBlock : MobaAssertionBlock
    {
        public string Kind { get; set; } = string.Empty;
        public int ConfigId { get; set; }
        public int MinCount { get; set; } = 1;
        public int MaxCount { get; set; }
        public int UnderEffectId { get; set; }

        protected override void Apply(MobaBattleFlowAssertions assertions)
        {
            assertions.MustContain.Add(new MobaTraceAssertion
            {
                Kind = Kind, ConfigId = ConfigId, MinCount = MinCount, MaxCount = MaxCount, UnderEffectId = UnderEffectId,
            });
        }
    }

    /// <summary>断言：trace 禁止出现（mustNotContain）。</summary>
    public sealed class AssertNoTraceBlock : MobaAssertionBlock
    {
        public string Kind { get; set; } = string.Empty;
        public int ConfigId { get; set; }
        public int UnderEffectId { get; set; }

        protected override void Apply(MobaBattleFlowAssertions assertions)
        {
            assertions.MustNotContain.Add(new MobaTraceAssertion
            {
                Kind = Kind, ConfigId = ConfigId, UnderEffectId = UnderEffectId,
            });
        }
    }

    /// <summary>断言：状态（stateExpectations，如 caster.hp &lt; 500）。</summary>
    public sealed class AssertStateBlock : MobaAssertionBlock
    {
        public string Alias { get; set; } = string.Empty;
        public string Property { get; set; } = string.Empty;
        public string Comparator { get; set; } = "eq";
        public string? ExpectedValue { get; set; }

        protected override void Apply(MobaBattleFlowAssertions assertions)
        {
            assertions.State.Add(new MobaStateAssertion
            {
                Alias = Alias, Property = Property, Comparator = Comparator, ExpectedValue = ExpectedValue,
            });
        }
    }

    /// <summary>断言：上下文（contextExpectations）。</summary>
    public sealed class AssertContextBlock : MobaAssertionBlock
    {
        public string Alias { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Property { get; set; } = string.Empty;
        public string Comparator { get; set; } = "eq";
        public string? ExpectedValue { get; set; }

        protected override void Apply(MobaBattleFlowAssertions assertions)
        {
            assertions.Context.Add(new MobaContextAssertion
            {
                Alias = Alias, Kind = Kind, Property = Property, Comparator = Comparator, ExpectedValue = ExpectedValue,
            });
        }
    }

    /// <summary>断言：因果关系（relationships）。</summary>
    public sealed class AssertRelationshipBlock : MobaAssertionBlock
    {
        public string ParentKind { get; set; } = string.Empty;
        public int ParentConfigId { get; set; }
        public string ChildKind { get; set; } = string.Empty;
        public int ChildConfigId { get; set; }

        protected override void Apply(MobaBattleFlowAssertions assertions)
        {
            assertions.Relationships.Add(new MobaRelationshipAssertion
            {
                ParentKind = ParentKind, ParentConfigId = ParentConfigId, ChildKind = ChildKind, ChildConfigId = ChildConfigId,
            });
        }
    }
}

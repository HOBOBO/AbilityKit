using AbilityKit.BattleFlow;

namespace AbilityKit.Demo.Moba.EnvironmentModel
{
    public enum MobaSkillOutcomeKind
    {
        TraceOccurs = 0,
        TraceAbsent = 1,
        State = 2,
    }

    /// <summary>
    /// Complete self-contained MOBA skill recipe: deterministic execution, duel setup, cast, and outcome.
    /// SkillId is author metadata; Slot remains the runtime input contract.
    /// </summary>
    public sealed class MobaCompleteSkillTestBlock : BattleAuthorBlock
    {
        public string EnvironmentProfileId { get; set; } = string.Empty;
        public int CasterHeroId { get; set; }
        public int CasterAttributeTemplateId { get; set; }
        public int TargetHeroId { get; set; }
        public int TargetAttributeTemplateId { get; set; }
        public float TargetDistance { get; set; } = 3f;

        public int SkillId { get; set; }
        public int Slot { get; set; } = 1;
        public int AtMs { get; set; } = 100;

        public MobaSkillOutcomeKind Outcome { get; set; }
        public string TraceKind { get; set; } = "DamageApply";
        public int TraceConfigId { get; set; }
        public int MinCount { get; set; } = 1;
        public string StateProperty { get; set; } = "hp";
        public string Comparator { get; set; } = "lt";
        public string ExpectedValue { get; set; } = string.Empty;

        public int TickRate { get; set; } = 30;
        public int MaxDurationMs { get; set; } = 30_000;
        public int SettleDurationMs { get; set; } = 500;

        public override System.Collections.Generic.IReadOnlyList<string> Validate()
        {
            var errors = new System.Collections.Generic.List<string>();
            if (CasterHeroId <= 0) errors.Add("caster hero id must be positive");
            if (CasterAttributeTemplateId <= 0) errors.Add("caster attribute template id must be positive");
            if (TargetHeroId <= 0) errors.Add("target hero id must be positive");
            if (TargetAttributeTemplateId <= 0) errors.Add("target attribute template id must be positive");
            if (TargetDistance < 0f) errors.Add("target distance cannot be negative");
            if (SkillId <= 0) errors.Add("skill id must be positive");
            if (Slot <= 0) errors.Add("skill slot must be positive");
            if (AtMs < 0) errors.Add("cast time cannot be negative");
            if (TickRate <= 0) errors.Add("tick rate must be positive");
            if (MaxDurationMs <= 0) errors.Add("maximum duration must be positive");
            if (SettleDurationMs < 0) errors.Add("settle duration cannot be negative");

            if (Outcome == MobaSkillOutcomeKind.State)
            {
                if (string.IsNullOrWhiteSpace(StateProperty)) errors.Add("state property is required");
                if (string.IsNullOrWhiteSpace(Comparator)) errors.Add("state comparator is required");
                if (string.IsNullOrWhiteSpace(ExpectedValue)) errors.Add("expected state value is required");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(TraceKind)) errors.Add("trace kind is required");
                if (TraceConfigId <= 0) errors.Add("trace config id must be positive");
                if (Outcome == MobaSkillOutcomeKind.TraceOccurs && MinCount <= 0)
                    errors.Add("minimum trace count must be positive");
            }
            return errors;
        }

        public override System.Collections.Generic.IReadOnlyList<BattleBlock> Expand() => new BattleBlock[]
        {
            new ExecutionSettingsBlock
            {
                TickRate = TickRate,
                MaxDurationMs = MaxDurationMs,
                SettleDurationMs = SettleDurationMs,
            },
            new DuelSetupBlock
            {
                EnvironmentProfileId = EnvironmentProfileId,
                CasterHeroId = CasterHeroId,
                CasterAttributeTemplateId = CasterAttributeTemplateId,
                TargetHeroId = TargetHeroId,
                TargetAttributeTemplateId = TargetAttributeTemplateId,
                TargetDistance = TargetDistance,
            },
            new MobaSkillOutcomeTestBlock
            {
                Slot = Slot,
                AtMs = AtMs,
                Outcome = Outcome,
                TraceKind = TraceKind,
                TraceConfigId = TraceConfigId,
                MinCount = MinCount,
                StateProperty = StateProperty,
                Comparator = Comparator,
                ExpectedValue = ExpectedValue,
            },
        };
    }

    /// <summary>Author-facing MOBA skill test with one selectable outcome contract.</summary>
    public sealed class MobaSkillOutcomeTestBlock : BattleAuthorBlock
    {
        public string CasterAlias { get; set; } = "caster";
        public string TargetAlias { get; set; } = "target";
        public int Slot { get; set; } = 1;
        public int AtMs { get; set; } = 100;
        public MobaSkillOutcomeKind Outcome { get; set; }
        public string TraceKind { get; set; } = "DamageApply";
        public int TraceConfigId { get; set; }
        public int MinCount { get; set; } = 1;
        public string StateAlias { get; set; } = "target";
        public string StateProperty { get; set; } = "hp";
        public string Comparator { get; set; } = "lt";
        public string ExpectedValue { get; set; } = string.Empty;

        public override System.Collections.Generic.IReadOnlyList<string> Validate()
        {
            var errors = new System.Collections.Generic.List<string>();
            if (string.IsNullOrWhiteSpace(CasterAlias)) errors.Add("caster alias is required");
            if (string.IsNullOrWhiteSpace(TargetAlias)) errors.Add("target alias is required");
            if (Slot < 0) errors.Add("skill slot cannot be negative");
            if (AtMs < 0) errors.Add("cast time cannot be negative");
            if (Outcome == MobaSkillOutcomeKind.State)
            {
                if (string.IsNullOrWhiteSpace(StateAlias)) errors.Add("state alias is required");
                if (string.IsNullOrWhiteSpace(StateProperty)) errors.Add("state property is required");
                if (string.IsNullOrWhiteSpace(Comparator)) errors.Add("state comparator is required");
                if (string.IsNullOrWhiteSpace(ExpectedValue)) errors.Add("expected state value is required");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(TraceKind)) errors.Add("trace kind is required");
                if (Outcome == MobaSkillOutcomeKind.TraceOccurs && MinCount <= 0)
                    errors.Add("minimum trace count must be positive");
            }
            return errors;
        }

        public override System.Collections.Generic.IReadOnlyList<BattleBlock> Expand()
        {
            var cast = new TimelineStepBlock
            {
                AtMs = AtMs,
                Action = "cast_skill",
                ActorAlias = CasterAlias,
                TargetAlias = TargetAlias,
                Slot = Slot,
            };
            BattleBlock assertion;
            switch (Outcome)
            {
                case MobaSkillOutcomeKind.TraceAbsent:
                    assertion = new AssertNoTraceBlock { Kind = TraceKind, ConfigId = TraceConfigId };
                    break;
                case MobaSkillOutcomeKind.State:
                    assertion = new AssertStateBlock
                    {
                        Alias = StateAlias,
                        Property = StateProperty,
                        Comparator = Comparator,
                        ExpectedValue = ExpectedValue,
                    };
                    break;
                default:
                    assertion = new AssertTraceBlock
                    {
                        Kind = TraceKind,
                        ConfigId = TraceConfigId,
                        MinCount = MinCount,
                    };
                    break;
            }
            return new[] { cast, assertion };
        }
    }

    /// <summary>Author-facing MOBA intent: cast one skill and verify its damage trace.</summary>
    public sealed class MobaSkillDamageTestBlock : BattleAuthorBlock
    {
        public string CasterAlias { get; set; } = "caster";
        public string TargetAlias { get; set; } = "target";
        public int Slot { get; set; } = 1;
        public int AtMs { get; set; } = 100;
        public int DamageConfigId { get; set; }
        public int MinCount { get; set; } = 1;

        public override System.Collections.Generic.IReadOnlyList<string> Validate()
        {
            var errors = new System.Collections.Generic.List<string>();
            if (string.IsNullOrWhiteSpace(CasterAlias)) errors.Add("caster alias is required");
            if (string.IsNullOrWhiteSpace(TargetAlias)) errors.Add("target alias is required");
            if (Slot < 0) errors.Add("skill slot cannot be negative");
            if (AtMs < 0) errors.Add("cast time cannot be negative");
            if (DamageConfigId <= 0) errors.Add("damage config id must be positive");
            if (MinCount <= 0) errors.Add("minimum damage count must be positive");
            return errors;
        }

        public override System.Collections.Generic.IReadOnlyList<BattleBlock> Expand() => new BattleBlock[]
        {
            new TimelineStepBlock
            {
                AtMs = AtMs,
                Action = "cast_skill",
                ActorAlias = CasterAlias,
                TargetAlias = TargetAlias,
                Slot = Slot,
            },
            new AssertTraceBlock
            {
                Kind = "DamageApply",
                ConfigId = DamageConfigId,
                MinCount = MinCount,
            },
        };
    }

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

    /// <summary>Asserts deterministic prediction/reconciliation telemetry.</summary>
    public sealed class AssertPredictionBlock : MobaAssertionBlock
    {
        public string Property { get; set; } = string.Empty;
        public string Comparator { get; set; } = "eq";
        public string ExpectedValue { get; set; } = string.Empty;

        protected override void Apply(MobaBattleFlowAssertions assertions)
        {
            assertions.Prediction.Add(new MobaPredictionAssertion
            {
                Property = Property,
                Comparator = Comparator,
                ExpectedValue = ExpectedValue,
            });
        }
    }
}

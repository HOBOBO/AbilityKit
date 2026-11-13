using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Console.Bootstrap;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Combat.Projectile;
using AbilityKit.Ability.World.DI;
using AbilityKit.Triggering.Runtime.Config.Plans;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Skill;

public sealed class MobaSkillConfigurationContractTests
{
    private const int SkillId = 9901001;
    private const int LevelTableId = 9902001;
    private const int CastFlowId = 9903001;

    [Fact]
    public void Console_configuration_satisfies_all_skill_resource_contracts()
    {
        var loader = new ConsoleTextAssetLoader();
        var configs = new MobaConfigDatabase(textAssetLoader: loader);
        configs.LoadFromGroups(MobaLubanConfigGroups.Create(loader, Array.Empty<string>()));

        var report = Validate(configs);
        var contractErrors = report.Entries
            .Where(entry => entry.Code.StartsWith("moba.skill.configuration.", StringComparison.Ordinal) ||
                            entry.Code.StartsWith("moba.skill.contract.", StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            contractErrors.Length == 0,
            string.Join(Environment.NewLine, contractErrors.Select(entry => report.FormatEntry(in entry))));
    }

    [Fact]
    public void Negative_skill_level_cost_blocks_startup()
    {
        var configs = CreateConfigDatabase(
            cost: -1,
            phases: CreateResourceContractPhases());

        var report = Validate(configs);

        var error = Assert.Single(report.Entries, entry =>
            entry.Code == "moba.skill.configuration.negative_cost");
        Assert.True(error.BlocksStartup);
        Assert.True(report.ShouldBlockStartup);
    }

    [Fact]
    public void Positive_cost_active_skill_without_release_and_commit_blocks_startup()
    {
        var configs = CreateConfigDatabase(
            cost: 10,
            phases: Array.Empty<SkillPhaseDTO>());

        var report = Validate(configs);

        var error = Assert.Single(report.Entries, entry =>
            entry.Code == "moba.skill.contract.release_commit_required");
        Assert.True(error.BlocksStartup);
        Assert.True(report.ShouldBlockStartup);
    }

    [Fact]
    public void Duplicate_trigger_reference_in_one_phase_blocks_startup()
    {
        var duplicateTriggerId = 900101011;
        var configs = CreateConfigDatabase(
            cost: 0,
            phases: new[]
            {
                new SkillPhaseDTO
                {
                    Type = (int)SkillPhaseType.RulePlan,
                    PhaseId = "release",
                    RulePlan = new SkillRulePlanPhaseDTO
                    {
                        TriggerIds = new[] { duplicateTriggerId, duplicateTriggerId },
                    },
                },
                CreateRulePlanPhase("commit", 900101012),
            });

        var report = Validate(configs);

        var error = Assert.Single(report.Entries, entry =>
            entry.Code == "moba.skill.contract.duplicate_trigger_reference");
        Assert.True(error.BlocksStartup);
        Assert.Equal("skill.9901001.castFlow.9903001.phases[0].rulePlan.triggerIds[1]", error.Path);
        Assert.True(report.ShouldBlockStartup);
    }

    [Fact]
    public void Rule_plan_owner_bound_trigger_blocks_startup_with_scope_contract_error()
    {
        const int triggerId = 9904001;
        var configs = CreateConfigDatabase(
            cost: 0,
            phases: new[]
            {
                CreateRulePlanPhase("release", triggerId),
            });
        var triggers = CreateTriggerDatabase(triggerId, TriggerPlanScope.OwnerBound);

        var report = Validate(configs, triggers);

        var error = Assert.Single(report.Entries, entry =>
            entry.Code == "moba.skill.contract.invalid_trigger_scope");
        Assert.True(error.BlocksStartup);
        Assert.Equal("skill.9901001.castFlow.9903001.phases[0].rulePlan.triggerIds[0]", error.Path);
        Assert.True(report.ShouldBlockStartup);
    }

    [Fact]
    public void Rule_plan_global_trigger_satisfies_scope_contract()
    {
        const int triggerId = 9904002;
        var configs = CreateConfigDatabase(
            cost: 0,
            phases: new[]
            {
                CreateRulePlanPhase("release", triggerId),
            });
        var triggers = CreateTriggerDatabase(triggerId, TriggerPlanScope.Global);

        var report = Validate(configs, triggers);

        Assert.DoesNotContain(report.Entries, entry =>
            entry.Code == "moba.skill.contract.invalid_trigger_scope");
    }

    [Fact]
    public void Window_trigger_owner_bound_scope_blocks_startup()
    {
        const int triggerId = 9904003;
        var configs = CreateConfigDatabase(
            cost: 0,
            phases: new[]
            {
                new SkillPhaseDTO
                {
                    Type = (int)SkillPhaseType.Window,
                    PhaseId = "charge",
                    Window = new SkillWindowPhaseDTO
                    {
                        WindowId = "charge.primary",
                        Kind = (int)SkillWindowKind.Charge,
                        OpenTriggerIds = new[] { triggerId },
                    },
                },
            });
        var report = Validate(configs, CreateTriggerDatabase(triggerId, TriggerPlanScope.OwnerBound));

        var error = Assert.Single(report.Entries, entry =>
            entry.Code == "moba.skill.contract.invalid_trigger_scope");
        Assert.True(error.BlocksStartup);
        Assert.Equal("skill.9901001.castFlow.9903001.phases[0].window.openTriggerIds[0]", error.Path);
    }

    [Fact]
    public void Timeline_effect_owner_bound_scope_blocks_startup()
    {
        const int triggerId = 9904004;
        var configs = CreateConfigDatabase(
            cost: 0,
            phases: new[]
            {
                new SkillPhaseDTO
                {
                    Type = (int)SkillPhaseType.Timeline,
                    PhaseId = "cast",
                    Timeline = new SkillTimelinePhaseDTO
                    {
                        Events = new[]
                        {
                            new SkillTimelineEventDTO
                            {
                                AtMs = 0,
                                EffectId = triggerId,
                                ExecuteMode = (int)EffectExecuteMode.InternalOnly,
                            },
                        },
                    },
                },
            });
        var report = Validate(configs, CreateTriggerDatabase(triggerId, TriggerPlanScope.OwnerBound));

        var error = Assert.Single(report.Entries, entry =>
            entry.Code == "moba.skill.contract.invalid_trigger_scope");
        Assert.True(error.BlocksStartup);
        Assert.Equal("skill.9901001.castFlow.9903001.phases[0].timeline.events[0].effectId", error.Path);
    }

    [Fact]
    public void Area_direct_trigger_owner_bound_scope_blocks_startup()
    {
        const int triggerId = 9904005;
        var configs = CreateConfigDatabase(
            cost: 0,
            phases: CreateResourceContractPhases(),
            areas: new[]
            {
                new AoeDTO
                {
                    Id = 9905001,
                    DurationMs = 1000,
                    Radius = 1f,
                    OnEnterTriggerIds = new[] { triggerId },
                },
            });

        var report = Validate(configs, CreateTriggerDatabase(triggerId, TriggerPlanScope.OwnerBound));

        var error = Assert.Single(report.Entries, entry =>
            entry.Code == "moba.skill.contract.invalid_trigger_scope");
        Assert.True(error.BlocksStartup);
        Assert.Equal("aoe.9905001.onEnterTriggerIds[0]", error.Path);
    }

    [Fact]
    public void Unsupported_phase_type_blocks_startup_before_runtime_construction()
    {
        var configs = CreateConfigDatabase(
            cost: 0,
            phases: new[]
            {
                new SkillPhaseDTO { Type = 9999, PhaseId = "unsupported" },
            });

        var report = Validate(configs);

        var error = Assert.Single(report.Entries, entry =>
            entry.Code == "moba.skill.configuration.unsupported_phase");
        Assert.True(error.BlocksStartup);
        Assert.True(report.ShouldBlockStartup);
    }

    [Fact]
    public void Empty_composite_phase_blocks_startup_before_runtime_construction()
    {
        var configs = CreateConfigDatabase(
            cost: 0,
            phases: new[]
            {
                new SkillPhaseDTO
                {
                    Type = (int)SkillPhaseType.Sequence,
                    PhaseId = "empty_sequence",
                    Children = Array.Empty<SkillPhaseDTO>(),
                },
            });

        var report = Validate(configs);

        var error = Assert.Single(report.Entries, entry =>
            entry.Code == "moba.skill.configuration.empty_composite_phase");
        Assert.True(error.BlocksStartup);
        Assert.True(report.ShouldBlockStartup);
    }

    [Fact]
    public void Empty_skill_flow_blocks_startup_before_runtime_construction()
    {
        var configs = CreateConfigDatabase(cost: 0, phases: Array.Empty<SkillPhaseDTO>());

        var report = Validate(configs);

        var error = Assert.Single(report.Entries, entry =>
            entry.Code == "moba.skill.configuration.empty_flow");
        Assert.True(error.BlocksStartup);
        Assert.True(report.ShouldBlockStartup);
    }

    [Fact]
    public void Explicit_reservation_inside_race_cannot_be_guaranteed_by_a_later_commit()
    {
        var reserve = new SkillPhaseDTO
        {
            Type = (int)SkillPhaseType.Economy,
            Economy = new SkillEconomyPhaseDTO { RequireExplicitCommit = true },
        };
        var commit = new SkillPhaseDTO
        {
            Type = (int)SkillPhaseType.CommitPoint,
            CommitPoint = new SkillCommitPointPhaseDTO { RequireEconomyReservation = true },
        };
        var configs = CreateConfigDatabase(0, new[]
        {
            new SkillPhaseDTO { Type = (int)SkillPhaseType.Race, Children = new[] { reserve,
                new SkillPhaseDTO { Type = (int)SkillPhaseType.Delay,
                    Delay = new SkillDelayPhaseDTO { DelayMs = 100 } } } },
            commit,
        });

        var report = Validate(configs);
        Assert.Contains(report.Entries, entry => entry.Code == "moba.skill.contract.explicit_commit_order");
        Assert.True(report.ShouldBlockStartup);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    public void Positive_cost_explicit_commit_requires_a_prior_explicit_reservation(bool reserve, bool reverse, bool blocked)
    {
        var reservation = new SkillPhaseDTO { Type = (int)SkillPhaseType.Economy, PhaseId = "reserve",
            Economy = new SkillEconomyPhaseDTO { RequireExplicitCommit = reserve } };
        var commit = new SkillPhaseDTO { Type = (int)SkillPhaseType.CommitPoint, PhaseId = "commit",
            CommitPoint = new SkillCommitPointPhaseDTO { CommitId = "cast", RequireEconomyReservation = true } };
        var phases = reverse ? new[] { commit, reservation } : new[] { reservation, commit };
        var configs = CreateConfigDatabase(10, new[] { CreateRulePlanPhase("release", 900101011) }.Concat(phases).ToArray());
        var report = Validate(configs);
        Assert.Equal(blocked, report.Entries.Any(e => e.Code == "moba.skill.contract.release_commit_required"));
    }

    [Fact]
    public void Reacquire_without_search_query_or_range_blocks_startup()
    {
        var configs = CreateConfigDatabase(0, CreateResourceContractPhases(),
            skill => skill.TargetLostPolicy = (int)SkillTargetLostPolicy.Reacquire);

        var report = Validate(configs);
        Assert.Contains(report.Entries, entry => entry.Code == "moba.skill.contract.invalid_cast_policy");
        Assert.True(report.ShouldBlockStartup);
    }

    [Theory]
    [InlineData(new[] { "damage" }, new float[0])]
    [InlineData(new[] { "damage", "DAMAGE" }, new[] { 10f, 20f })]
    [InlineData(new[] { "damage" }, new[] { float.NaN })]
    [InlineData(new string[0], new[] { 10f })]
    public void Invalid_named_skill_level_parameters_block_startup(string[] paramNames, float[] values)
    {
        var configs = CreateConfigDatabase(
            cost: 0,
            phases: CreateResourceContractPhases(),
            paramNames: paramNames,
            paramsValues: values);

        var report = Validate(configs);

        var error = Assert.Single(report.Entries, entry =>
            entry.Code == "moba.skill.configuration.invalid_level_parameters");
        Assert.True(error.BlocksStartup);
        Assert.True(report.ShouldBlockStartup);
        Assert.Contains("skillId=", error.Message);
    }

    [Fact]
    public void Pipeline_payload_uses_the_frozen_cast_configuration_snapshot()
    {
        var aimPos = Vec3.Zero;
        var aimDir = Vec3.Forward;
        var request = new SkillCastRequest(
            SkillId,
            skillSlot: 2,
            casterActorId: 101,
            targetActorId: 202,
            in aimPos,
            in aimDir,
            worldServices: null,
            eventBus: null,
            casterUnit: null,
            targetUnit: null);
        var castContext = new SkillCastContext();
        castContext.Initialize(in request, skillLevel: 2);
        castContext.ResolvedConfiguration = new ResolvedSkillCastConfiguration(
            SkillId,
            skillLevel: 2,
            ResourceType.Mana,
            resourceCost: 35,
            cooldownMs: 2400,
            hasLevelConfiguration: true);
        var pipelineContext = new SkillPipelineContext();
        pipelineContext.Initialize(new object(), in request, castContext);

        castContext.ResolvedConfiguration = new ResolvedSkillCastConfiguration(
            SkillId,
            skillLevel: 2,
            ResourceType.Mana,
            resourceCost: 99,
            cooldownMs: 9900,
            hasLevelConfiguration: true);

        var accessor = new SkillPipelineContextPayloadAccessor(
            configs: null,
            actors: null);
        var costField = SkillRulePayloadFields.FieldId(SkillRulePayloadFields.SkillCost);
        var cooldownField = SkillRulePayloadFields.FieldId(SkillRulePayloadFields.SkillCooldownMs);

        Assert.True(accessor.TryGet(in pipelineContext, costField, out int cost));
        Assert.True(accessor.TryGet(in pipelineContext, cooldownField, out int cooldownMs));
        Assert.Equal(35, cost);
        Assert.Equal(2400, cooldownMs);
    }

    [Theory]
    [InlineData(ProjectileHitPolicyKind.ExitOnHit, 0, 0, 1)]
    [InlineData(ProjectileHitPolicyKind.Pierce, 3, 3, 0)]
    [InlineData(ProjectileHitPolicyKind.Pierce, -1, -1, 0)]
    [InlineData(ProjectileHitPolicyKind.Bounce, 3, 3, 3)]
    public void Projectile_hit_policy_resolves_authored_values_to_runtime_contract(
        ProjectileHitPolicyKind policy, int authoredHits, int expectedPolicyParam, int expectedHitsRemaining)
    {
        var projectile = new ProjectileMO(new ProjectileDTO
        {
            Id = 9001,
            HitPolicyKind = (int)policy,
            HitsRemaining = authoredHits,
        });

        Assert.True(projectile.TryResolveRuntimeSettings(out var resolvedPolicy, out var policyParam,
            out var hitsRemaining, out var error), error);
        Assert.Equal(policy, resolvedPolicy);
        Assert.Equal(expectedPolicyParam, policyParam);
        Assert.Equal(expectedHitsRemaining, hitsRemaining);
    }

    [Theory]
    [InlineData(ProjectileHitPolicyKind.Pierce, 0)]
    [InlineData(ProjectileHitPolicyKind.Bounce, 0)]
    [InlineData((ProjectileHitPolicyKind)9876, 1)]
    public void Invalid_projectile_hit_policy_is_rejected_by_runtime_config_validation(
        ProjectileHitPolicyKind policy, int authoredHits)
    {
        var configs = new MobaConfigDatabase();
        var result = configs.ReloadFromDtoArrays(
            new Dictionary<Type, Array>
            {
                [typeof(ProjectileDTO)] = new[]
                {
                    new ProjectileDTO
                    {
                        Id = 9002,
                        HitPolicyKind = (int)policy,
                        HitsRemaining = authoredHits,
                    },
                },
            },
            strict: false);
        Assert.True(result.Succeeded, result.Error);

        var report = Validate(configs);

        var error = Assert.Single(report.Entries, entry =>
            entry.Path == "projectile.9002.hitPolicy");
        Assert.True(error.BlocksStartup);
        Assert.True(report.ShouldBlockStartup);
    }

    [Fact]
    public void Non_finite_projectile_arc_configuration_is_rejected()
    {
        var projectile = new ProjectileMO(new ProjectileDTO
        {
            Id = 9003,
            TrackingArcHeightPerUnit = float.NaN,
        });

        Assert.False(projectile.TryResolveRuntimeSettings(out _, out _, out _, out var error));
        Assert.Contains("TrackingArcHeightPerUnit", error);
    }

    [Fact]
    public void Projectile_hit_unit_subtype_defaults_to_any_and_rejects_negative_values()
    {
        var anyUnit = new ProjectileMO(new ProjectileDTO { Id = 9004 });
        Assert.True(anyUnit.TryResolveRuntimeSettings(out _, out _, out _, out var anyError), anyError);
        Assert.Equal(0, anyUnit.HitUnitSubType);

        var heroOnly = new ProjectileMO(new ProjectileDTO { Id = 9005, HitUnitSubType = (int)UnitSubType.Hero });
        Assert.True(heroOnly.TryResolveRuntimeSettings(out _, out _, out _, out var heroError), heroError);
        Assert.Equal((int)UnitSubType.Hero, heroOnly.HitUnitSubType);

        var invalid = new ProjectileMO(new ProjectileDTO { Id = 9006, HitUnitSubType = -1 });
        Assert.False(invalid.TryResolveRuntimeSettings(out _, out _, out _, out var error));
        Assert.Contains("HitUnitSubType", error);
    }

    private static MobaRuntimeValidationReport Validate(
        MobaConfigDatabase configs,
        TriggerPlanJsonDatabase triggers = null)
    {
        var report = new MobaRuntimeValidationReport();
        var context = new MobaRuntimeValidationContext(
            new TestWorldResolver(configs, triggers),
            "test",
            MobaRuntimeValidationInvocation.Manual);
        new MobaBattleConfigReferenceValidator().Validate(in context, report);
        return report;
    }

    private static TriggerPlanJsonDatabase CreateTriggerDatabase(int triggerId, TriggerPlanScope scope)
    {
        var plans = new TriggerPlanJsonDatabase();
        var plan = new TriggerPlan<object>(phase: 0, priority: 0, triggerId: triggerId);
        plans.AddRecord(new TriggerPlanJsonDatabase.Record(
            triggerId,
            eventName: string.Empty,
            eventId: 0,
            scope: scope,
            in plan));
        return plans;
    }

    private static MobaConfigDatabase CreateConfigDatabase(
        int cost,
        SkillPhaseDTO[] phases,
        Action<SkillDTO> configureSkill = null,
        string[] paramNames = null,
        float[] paramsValues = null,
        AoeDTO[] areas = null)
    {
        var configs = new MobaConfigDatabase();
        var skill = new SkillDTO
        {
            Id = SkillId,
            Name = "contract_test_skill",
            CooldownMs = 1000,
            SkillType = (int)SkillType.Active,
            Tags = Array.Empty<int>(),
            LevelTableId = LevelTableId,
            CastFlowId = CastFlowId,
        };
        configureSkill?.Invoke(skill);
        var result = configs.ReloadFromDtoArrays(
            new Dictionary<Type, Array>
            {
                [typeof(SkillDTO)] = new[] { skill },
                [typeof(SkillLevelTableDTO)] = new[]
                {
                    new SkillLevelTableDTO
                    {
                        Id = LevelTableId,
                        Levels = new[]
                        {
                        new SkillLevelDTO
                        {
                            CooldownMs = 1000,
                            Cost = cost,
                            Params = paramsValues ?? Array.Empty<float>(),
                        },
                    },
                    ParamNames = paramNames ?? Array.Empty<string>(),
                },
                },
                [typeof(SkillFlowDTO)] = new[]
                {
                    new SkillFlowDTO
                    {
                        Id = CastFlowId,
                        Name = "contract_test_flow",
                        Phases = phases,
                    },
                },
                [typeof(AoeDTO)] = areas ?? Array.Empty<AoeDTO>(),
            },
            strict: false);

        Assert.True(result.Succeeded, result.Error);
        return configs;
    }

    private static SkillPhaseDTO[] CreateResourceContractPhases()
    {
        return new[]
        {
            CreateRulePlanPhase("release", 900101011),
            CreateRulePlanPhase("commit", 900101012),
        };
    }

    private static SkillPhaseDTO CreateRulePlanPhase(string phaseId, int triggerId)
    {
        return new SkillPhaseDTO
        {
            Type = (int)SkillPhaseType.RulePlan,
            PhaseId = phaseId,
            RulePlan = new SkillRulePlanPhaseDTO
            {
                TriggerIds = new[] { triggerId },
                AbortOnFailure = true,
            },
        };
    }

    private sealed class TestWorldResolver : IWorldResolver
    {
        private readonly Dictionary<Type, object> _services = new();

        public TestWorldResolver(MobaConfigDatabase configs, TriggerPlanJsonDatabase triggers = null)
        {
            _services[typeof(MobaConfigDatabase)] = configs;
            if (triggers != null)
            {
                _services[typeof(TriggerPlanJsonDatabase)] = triggers;
            }
        }

        public object Resolve(Type serviceType)
        {
            return _services[serviceType];
        }

        public T Resolve<T>()
        {
            return (T)Resolve(typeof(T));
        }

        public bool TryResolve(Type serviceType, out object instance)
        {
            return _services.TryGetValue(serviceType, out instance);
        }

        public bool TryResolve<T>(out T instance)
        {
            if (_services.TryGetValue(typeof(T), out var value))
            {
                instance = (T)value;
                return true;
            }

            instance = default;
            return false;
        }
    }
}

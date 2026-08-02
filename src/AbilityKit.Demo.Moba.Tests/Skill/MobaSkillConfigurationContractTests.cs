using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Console.Bootstrap;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Ability.World.DI;
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
        configs.LoadFromResources("moba", strict: true);

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

    private static MobaRuntimeValidationReport Validate(MobaConfigDatabase configs)
    {
        var report = new MobaRuntimeValidationReport();
        var context = new MobaRuntimeValidationContext(
            new TestWorldResolver(configs),
            "test",
            MobaRuntimeValidationInvocation.Manual);
        new MobaBattleConfigReferenceValidator().Validate(in context, report);
        return report;
    }

    private static MobaConfigDatabase CreateConfigDatabase(
        int cost,
        SkillPhaseDTO[] phases)
    {
        var configs = new MobaConfigDatabase();
        var result = configs.ReloadFromDtoArrays(
            new Dictionary<Type, Array>
            {
                [typeof(SkillDTO)] = new[]
                {
                    new SkillDTO
                    {
                        Id = SkillId,
                        Name = "contract_test_skill",
                        CooldownMs = 1000,
                        SkillType = (int)SkillType.Active,
                        Tags = Array.Empty<int>(),
                        LevelTableId = LevelTableId,
                        CastFlowId = CastFlowId,
                    },
                },
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
                                Params = Array.Empty<float>(),
                            },
                        },
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

        public TestWorldResolver(MobaConfigDatabase configs)
        {
            _services[typeof(MobaConfigDatabase)] = configs;
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

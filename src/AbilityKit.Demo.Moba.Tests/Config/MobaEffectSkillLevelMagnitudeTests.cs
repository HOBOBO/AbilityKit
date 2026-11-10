using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Combat.Magnitude;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Ability.World.DI;
using AbilityKit.Modifiers;
using AbilityKit.Triggering.Runtime;
using System;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Config;

public sealed class MobaEffectSkillLevelMagnitudeTests
{
    [Fact]
    public void Hero_level_growth_table_reads_each_named_column_and_fails_for_missing_row_or_column()
    {
        var configs = new MobaConfigDatabase();
        var rows = new[]
        {
            new HeroLevelSkillGrowthParameterDTO
            {
                Id = 910001,
                SkillId = 2001,
                ChampionLevel = 12,
                Damage = 135f,
                Shield = 80f,
                SummonHealth = 900f
            }
        };
        var provider = new MobaConfigDtoDictionaryProvider(new System.Collections.Generic.Dictionary<Type, Array>
        {
            [typeof(HeroLevelSkillGrowthParameterDTO)] = rows
        });
        Assert.True(configs.ReloadFromDtoProvider(provider, strict: false).Succeeded);

        Assert.True(configs.TryGetHeroLevelSkillGrowthParameter(2001, 12, "dAmAgE", out var damage));
        Assert.Equal(135f, damage);
        Assert.True(configs.TryGetHeroLevelSkillGrowthParameter(2001, 12, "Shield", out var shield));
        Assert.Equal(80f, shield);
        Assert.False(configs.TryGetHeroLevelSkillGrowthParameter(2001, 12, "UnknownEffect", out _));
        Assert.False(configs.TryGetHeroLevelSkillGrowthParameter(2001, 11, "Damage", out _));
    }

    [Fact]
    public void Skill_level_parameter_source_reads_frozen_runtime_value_and_applies_coefficient()
    {
        var runtimes = new MobaSkillCastRuntimeService();
        var table = new SkillLevelTableMO(new SkillLevelTableDTO
        {
            Id = 9002,
            ParamNames = new[] { "damage" },
            Levels = new[] { new SkillLevelDTO { Params = new[] { 40f } } }
        });
        Assert.True(table.TryCreateParameterSnapshot(1, 2001, out var snapshot, out var snapshotError), snapshotError);
        var runtime = runtimes.Create(new MobaSkillCastRuntimeCreateRequest(
            skillId: 2001, skillSlot: 1, skillLevel: 1, sequence: 1,
            casterActorId: 7, targetActorId: 8, aimPos: Vec3.Zero, aimDir: Vec3.Forward,
            rootContextId: 9002, levelParameters: snapshot));
        var world = new TestWorldResolver(runtimes);
        var exec = new ExecCtx<IWorldResolver>(world, null, null, null, null, null, null, null, null, default, default);
        var execution = new MobaCombatExecutionContext(
            payload: null, lineageInput: default, origin: default, executionSnapshot: default,
            skillRuntimeHandle: runtime.Handle, frame: 0);
        var source = new MagnitudeSource { Type = MagnitudeSourceType.SkillLevelParameter, Data1 = 1.5f };
        var spec = new MobaEffectMagnitudeSpec(
            in source, default,
            sourceRole: MobaEffectSourceRole.Self,
            baseSkillLevelParameter: "damage");

        var success = MobaEffectMagnitudeResolver.TryEvaluate(
            in spec, in execution, in exec, attributionActorId: 0, selfActorId: 7, targetActorId: 8,
            default, out var value, out _, out var failure);

        Assert.True(success, failure);
        Assert.Equal(60f, value);
    }

    [Fact]
    public void Skill_level_parameter_source_fails_explicitly_without_skill_runtime_handle()
    {
        var source = new MagnitudeSource { Type = MagnitudeSourceType.SkillLevelParameter, Data1 = 2f };
        var spec = new MobaEffectMagnitudeSpec(
            in source, default,
            sourceRole: MobaEffectSourceRole.Self,
            baseSkillLevelParameter: "damage");

        var success = MobaEffectMagnitudeResolver.TryEvaluate(
            in spec, default, default, attributionActorId: 0, selfActorId: 7, targetActorId: 0,
            default, out _, out _, out var failure);

        Assert.False(success);
        Assert.Contains("damage", failure);
        Assert.Contains("unavailable", failure);
    }

    [Fact]
    public void Skill_level_parameter_source_fails_explicitly_for_unknown_parameter_on_valid_runtime()
    {
        var runtimes = new MobaSkillCastRuntimeService();
        var snapshot = new MobaSkillLevelParameterSnapshot(new[]
        {
            new MobaSkillLevelParameterValue("damage", 40f)
        });
        var runtime = runtimes.Create(new MobaSkillCastRuntimeCreateRequest(
            skillId: 2001, skillSlot: 1, skillLevel: 1, sequence: 1,
            casterActorId: 7, targetActorId: 8, aimPos: Vec3.Zero, aimDir: Vec3.Forward,
            rootContextId: 9002, levelParameters: snapshot));
        var world = new TestWorldResolver(runtimes);
        var exec = new ExecCtx<IWorldResolver>(world, null, null, null, null, null, null, null, null, default, default);
        var execution = new MobaCombatExecutionContext(
            payload: null, lineageInput: default, origin: default, executionSnapshot: default,
            skillRuntimeHandle: runtime.Handle, frame: 0);
        var source = new MagnitudeSource { Type = MagnitudeSourceType.SkillLevelParameter, Data1 = 1f };
        var spec = new MobaEffectMagnitudeSpec(
            in source, default,
            sourceRole: MobaEffectSourceRole.Self,
            baseSkillLevelParameter: "missing_damage");

        var success = MobaEffectMagnitudeResolver.TryEvaluate(
            in spec, in execution, in exec, attributionActorId: 0, selfActorId: 7, targetActorId: 8,
            default, out _, out _, out var failure);

        Assert.False(success);
        Assert.Contains("missing_damage", failure);
        Assert.Contains("unavailable", failure);
    }

    [Fact]
    public void Skill_level_parameter_source_rejects_empty_parameter_name()
    {
        var source = new MagnitudeSource { Type = MagnitudeSourceType.SkillLevelParameter, Data1 = 1f };
        var spec = new MobaEffectMagnitudeSpec(in source, default, sourceRole: MobaEffectSourceRole.Self);

        var success = MobaEffectMagnitudeResolver.TryEvaluate(
            in spec, default, default, attributionActorId: 0, selfActorId: 7, targetActorId: 0,
            default, out _, out _, out var failure);

        Assert.False(success);
        Assert.Contains("name is empty", failure);
    }

    [Fact]
    public void Existing_context_float_source_keeps_its_fallback_value()
    {
        var source = MagnitudeSource.ContextFloat("legacy:key", coefficient: 3f, baseValue: 12f);
        var spec = new MobaEffectMagnitudeSpec(in source, default, sourceRole: MobaEffectSourceRole.Self);

        var success = MobaEffectMagnitudeResolver.TryEvaluate(
            in spec, default, default, attributionActorId: 0, selfActorId: 7, targetActorId: 0,
            default, out var value, out _, out var failure);

        Assert.True(success, failure);
        Assert.Equal(36f, value);
    }

    private sealed class TestWorldResolver : IWorldResolver
    {
        private readonly MobaSkillCastRuntimeService _runtimes;

        public TestWorldResolver(MobaSkillCastRuntimeService runtimes) => _runtimes = runtimes;
        public object Resolve(Type serviceType) => serviceType == typeof(MobaSkillCastRuntimeService) ? _runtimes : null;
        public T Resolve<T>() => TryResolve<T>(out var instance) ? instance : default;
        public bool TryResolve(Type serviceType, out object instance)
        {
            instance = Resolve(serviceType);
            return instance != null;
        }
        public bool TryResolve<T>(out T instance)
        {
            if (typeof(T) == typeof(MobaSkillCastRuntimeService))
            {
                instance = (T)(object)_runtimes;
                return true;
            }

            instance = default;
            return false;
        }
    }
}

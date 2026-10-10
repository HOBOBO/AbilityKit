using System;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.World.DI;
using AbilityKit.Attributes.Core;
using AbilityKit.Continuous;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Demo.Moba.Services.EntityManager;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services.Projectile.Launch;
using AbilityKit.Demo.Moba.Services.Buffs.Core;
using AbilityKit.Demo.Moba.Services.Triggering.PlanActions;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.GameplayTags;
using AbilityKit.Modifiers;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Config;

[CollectionDefinition("Moba modifier allocation", DisableParallelization = true)]
public sealed class MobaModifierAllocationCollection { }

[Collection("Moba modifier allocation")]
public sealed class MobaModifierAttributeSourceTests
{
    [Fact]
    public void Modifier_capture_records_are_isolated_between_worlds()
    {
        const int sourceId = 71;
        var continuous = new Continuous();
        var spec = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.Attribute,
            TargetId = (int)BattleAttributeType.MAX_HP,
            Op = (int)ModifierOp.Add,
            EvaluationPolicy = MobaContinuousModifierEvaluationPolicy.OnApplySnapshot,
        });
        var first = new MobaContinuousModifierCaptureStore();
        var second = new MobaContinuousModifierCaptureStore();

        MobaContinuousModifierCaptureRecord Capture(float value) => new(
            continuous, spec, 10, spec.TargetKind, spec.TargetId, spec.Op, value,
            spec.EvaluationPolicy, spec.Priority, 1, sourceId,
            spec.Magnitude, spec.Magnitude, MagnitudeSource.Fixed(value),
            value, true, "OnApplySnapshot", "Test capture");

        first.Record(Capture(25f));
        second.Record(Capture(50f));
        Assert.True(first.TryGet(continuous, sourceId, spec, out var firstCapture));
        Assert.True(second.TryGet(continuous, sourceId, spec, out var secondCapture));
        Assert.Equal(25f, firstCapture.CapturedValue);
        Assert.Equal(50f, secondCapture.CapturedValue);

        first.Clear(sourceId, spec.TargetKind, 10);
        Assert.False(first.TryGet(continuous, sourceId, spec, out _));
        Assert.True(second.TryGet(continuous, sourceId, spec, out secondCapture));
        Assert.Equal(50f, secondCapture.CapturedValue);
        first.Dispose();
        second.Dispose();
    }

    [Fact]
    public void Modifier_queries_use_their_own_world_capture_store()
    {
        using var firstParameters = new MobaSkillParamModifierService();
        using var secondParameters = new MobaSkillParamModifierService();
        using var firstManager = new MobaContinuousManager();
        using var secondManager = new MobaContinuousManager();
        var firstResolver = new Resolver(null, firstParameters, firstManager);
        var secondResolver = new Resolver(null, secondParameters, secondManager);
        firstManager.OnInit(firstResolver);
        secondManager.OnInit(secondResolver);
        using var firstQuery = new MobaContinuousModifierQueryService();
        using var secondQuery = new MobaContinuousModifierQueryService();
        firstQuery.OnInit(firstResolver);
        secondQuery.OnInit(secondResolver);

        SnapshotContinuous Activate(MobaContinuousManager manager, float amount)
        {
            var spec = new ContinuousModifierMO(new ContinuousModifierDTO
            {
                TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                TargetId = MobaSkillParamModifierKeys.Projectile.Ids.Speed,
                Op = (int)ModifierOp.Add,
                Value = amount,
                EvaluationPolicy = MobaContinuousModifierEvaluationPolicy.OnApplySnapshot,
            });
            var continuous = new SnapshotContinuous(new SnapshotConfig(
                new IMobaContinuousModifierSpec[] { spec }));
            continuous.SetModifierCapture(0, 1, amount);
            Assert.True(manager.TryActivate(continuous));
            return continuous;
        }

        var first = Activate(firstManager, 5f);
        var second = Activate(secondManager, 9f);
        var firstResult = Assert.Single(firstQuery.ExplainActiveModifiers(10,
            MobaContinuousModifierTargetKind.SkillParameter));
        var secondResult = Assert.Single(secondQuery.ExplainActiveModifiers(10,
            MobaContinuousModifierTargetKind.SkillParameter));
        Assert.Equal(firstResult.ModifierSourceId, secondResult.ModifierSourceId);
        Assert.Equal(5f, firstResult.CapturedValue);
        Assert.Equal(9f, secondResult.CapturedValue);

        Assert.True(firstManager.TryEnd(first));
        Assert.Empty(firstQuery.ExplainActiveModifiers(10));
        secondResult = Assert.Single(secondQuery.ExplainActiveModifiers(10,
            MobaContinuousModifierTargetKind.SkillParameter));
        Assert.Equal(9f, secondResult.CapturedValue);
        Assert.True(secondManager.TryEnd(second));
    }

    [Fact]
    public void Attribute_capture_is_released_after_owner_entity_is_destroyed()
    {
        var contexts = new Contexts();
        using var index = new ActorIdIndex(contexts);
        using var registry = new MobaActorRegistry();
        using var entities = new MobaEntityManager(null);
        var lookup = new MobaActorLookupService(index, registry, entities, contexts);
        var resolver = new Resolver(lookup);
        var projector = new MobaAttributeContinuousModifierProjector();
        projector.OnInit(resolver);
        CreateActor(contexts, 10, 40f);
        var projection = new Projection();
        var continuous = new Continuous();
        var spec = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.Attribute,
            TargetId = (int)BattleAttributeType.MAX_HP,
            Op = (int)ModifierOp.Add,
            Value = 15f,
            EvaluationPolicy = MobaContinuousModifierEvaluationPolicy.OnApplySnapshot,
        });

        projector.Apply(continuous, projection, new IMobaContinuousModifierSpec[] { spec });
        Assert.True(resolver.CaptureStore.TryGet(continuous, projection.ModifierSourceId, spec, out _));
        contexts.actor.DestroyAllEntities();
        projector.Clear(projection);
        Assert.False(resolver.CaptureStore.TryGet(continuous, projection.ModifierSourceId, spec, out _));
    }

    [Fact]
    public void Indirect_damage_uses_only_explicit_projectile_hit_lineage()
    {
        var projectile = new MobaEffectLineageInput(EffectContextKind.Projectile,
            MobaExecutionKind.ProjectileHit, 10, 20, 1, 1, 1, 30020101);
        var context = new MobaCombatExecutionContext(null, projectile, default, default,
            default, 0);
        Assert.Equal(30020101, GiveDamagePlanActionModule.ResolveProjectileTemplateId(in context));

        var unrelated = new MobaEffectLineageInput(EffectContextKind.Trigger,
            MobaExecutionKind.EffectExecution, 10, 20, 1, 1, 1, 30020101);
        context = new MobaCombatExecutionContext(null, unrelated, default, default,
            default, 0);
        Assert.Equal(0, GiveDamagePlanActionModule.ResolveProjectileTemplateId(in context));
    }

    [Fact]
    public void Projectile_identity_survives_nested_effect_nodes_and_payload_replacement()
    {
        var projectile = new MobaEffectLineageInput(EffectContextKind.Projectile,
            MobaExecutionKind.ProjectileHit, 10, 20, 1, 1, 1, 30020101);
        var context = new MobaCombatExecutionContext(null, projectile, default, default,
            default, 42);
        var firstEffect = context.WithEffectExecutionNode(2, 9001, true);
        var forwarded = firstEffect.WithPayload(new object())
            .WithEffectExecutionNode(3, 9002, false);
        Assert.Equal(30020101, forwarded.ProjectileTemplateId);
        Assert.Equal(30020101, GiveDamagePlanActionModule.ResolveProjectileTemplateId(in forwarded));
        Assert.Equal(9002, forwarded.ConfigId);

        var resumed = MobaCombatExecutionContextFactory.WithSnapshot(in forwarded,
            default, 43);
        Assert.Equal(30020101, resumed.ProjectileTemplateId);
        Assert.Equal(43, resumed.Frame);

        var unrelated = new MobaEffectLineageInput(EffectContextKind.Trigger,
            MobaExecutionKind.EffectExecution, 10, 20, 1, 1, 1, 30020101);
        var unrelatedContext = new MobaCombatExecutionContext(null, unrelated, default,
            default, default, 42).WithEffectExecutionNode(2, 9001, true);
        Assert.Equal(0, unrelatedContext.ProjectileTemplateId);
    }

    [Fact]
    public void Recorded_parameter_rejects_wrong_stage_and_matches_base_skill_selector()
    {
        using var modifiers = new MobaSkillParamModifierService();
        var key = MobaSkillParamModifierKeys.Projectile.Speed;
        var modifier = new ModifierData
        {
            Key = key,
            Op = ModifierOp.Add,
            Magnitude = MagnitudeSource.Fixed(5f),
            SourceId = 17,
        };
        modifiers.AddModifier(MobaModifierOwnerRef.Actor(10), in modifier,
            new MobaParameterSelector(baseSkillId: 1001, projectileTemplateId: 3001));
        var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn,
            baseSkillId: 1001, effectiveSkillId: 1002, projectileTemplateId: 3001);
        var wrongStage = new MobaParameterRecord(MobaParameterStage.Hit,
            baseSkillId: 1001, projectileTemplateId: 3001);

        Assert.Equal(15f, modifiers.ResolveFloat(MobaModifierOwnerRef.Actor(10), key, 10f, in record));
        Assert.Throws<InvalidOperationException>(() =>
            modifiers.ResolveFloat(MobaModifierOwnerRef.Actor(10), key, 10f, in wrongStage));
        var otherProjectile = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn,
            baseSkillId: 1001, projectileTemplateId: 3002);
        Assert.Equal(10f, modifiers.ResolveFloat(MobaModifierOwnerRef.Actor(10), key, 10f,
            in otherProjectile));
    }

    [Fact]
    public void Parameter_resolution_rejects_non_finite_values_before_recording_them()
    {
        using var modifiers = new MobaSkillParamModifierService();
        var owner = MobaModifierOwnerRef.Actor(10);
        var key = MobaSkillParamModifierKeys.Projectile.Speed;
        var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);

        Assert.Throws<InvalidOperationException>(() =>
            modifiers.ResolveFloat(owner, key, float.NaN, in record));
        Assert.Throws<InvalidOperationException>(() =>
            modifiers.ResolveFloat(owner, key, float.PositiveInfinity, in record));
        var overflow = new ModifierData
        {
            Key = key,
            Op = ModifierOp.Mul,
            Magnitude = MagnitudeSource.Fixed(float.MaxValue),
            SourceId = 17,
        };
        modifiers.AddModifier(owner, in overflow);
        Assert.Throws<InvalidOperationException>(() =>
            modifiers.ResolveFloat(owner, key, float.MaxValue, in record));
        Assert.Throws<InvalidOperationException>(() =>
            modifiers.ExplainRecorded(owner, key, float.MaxValue, in record));
        var recent = new List<MobaParameterResolutionTrace>();
        modifiers.CopyRecentResolutionsTo(recent);
        Assert.Empty(recent);
    }

    [Fact]
    public void Projection_rejects_selector_fields_unavailable_at_the_consumer_stage()
    {
        using var modifiers = new MobaSkillParamModifierService();
        var modifier = new ModifierData
        {
            Key = MobaSkillParamModifierKeys.Projectile.Speed,
            Op = ModifierOp.Add, Magnitude = MagnitudeSource.Fixed(2f), SourceId = 17,
        };
        Assert.Throws<InvalidOperationException>(() => modifiers.AddModifier(
            MobaModifierOwnerRef.Actor(10), in modifier,
            new MobaParameterSelector(areaTemplateId: 301)));
        Assert.Empty(modifiers.CaptureRollbackSnapshot().Entries);
    }

    [Fact]
    public void Parameter_projection_rejects_non_actor_owner()
    {
        using var modifiers = new MobaSkillParamModifierService();
        var modifier = new ModifierData
        {
            Key = MobaSkillParamModifierKeys.Projectile.Speed,
            Op = ModifierOp.Add, Magnitude = MagnitudeSource.Fixed(2f), SourceId = 18,
        };
        Assert.Throws<InvalidOperationException>(() => modifiers.AddModifier(
            new MobaModifierOwnerRef(MobaModifierOwnerScope.Launcher, 20), in modifier));
    }

    [Fact]
    public void Recorded_parameter_resolution_rejects_non_actor_owner()
    {
        using var modifiers = new MobaSkillParamModifierService();
        var key = MobaSkillParamModifierKeys.Projectile.Speed;
        var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);
        var launcher = new MobaModifierOwnerRef(MobaModifierOwnerScope.Launcher, 20);

        Assert.Throws<InvalidOperationException>(() =>
            modifiers.ResolveFloat(launcher, key, 10f, in record));
        Assert.Throws<InvalidOperationException>(() =>
            modifiers.ExplainRecorded(launcher, key, 10f, in record));
        Assert.Throws<InvalidOperationException>(() =>
            modifiers.ResolveFloat(default, key, 10f, in record));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Legacy_non_actor_rollback_owner_is_rejected_without_clearing_live_projection(
        bool restoreProjection)
    {
        using var modifiers = new MobaSkillParamModifierService();
        var key = MobaSkillParamModifierKeys.Projectile.Speed;
        var modifier = new ModifierData
        {
            Key = key, Op = ModifierOp.Add, Magnitude = MagnitudeSource.Fixed(5f), SourceId = 17,
        };
        modifiers.AddModifier(MobaModifierOwnerRef.Actor(10), in modifier);
        var rollback = new MobaSkillParamModifierRollbackProvider(modifiers, restoreProjection);
        var payload = rollback.ExportState(new FrameIndex(1));
        BitConverter.GetBytes((int)MobaModifierOwnerScope.Launcher).CopyTo(payload, 8);
        var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);

        Assert.Throws<InvalidOperationException>(() =>
            rollback.ImportState(new FrameIndex(1), payload));
        Assert.Equal(15f, modifiers.ResolveFloat(MobaModifierOwnerRef.Actor(10), key, 10f,
            in record));
    }

    [Fact]
    public void Invalid_later_snapshot_entry_does_not_partially_replace_live_projection()
    {
        using var modifiers = new MobaSkillParamModifierService();
        var owner = MobaModifierOwnerRef.Actor(10);
        var key = MobaSkillParamModifierKeys.Projectile.Speed;
        var modifier = new ModifierData
        {
            Key = key, Op = ModifierOp.Add, Magnitude = MagnitudeSource.Fixed(5f), SourceId = 17,
        };
        modifiers.AddModifier(owner, in modifier);
        var entries = new[]
        {
            new MobaSkillParamModifierSnapshotEntry(MobaModifierOwnerScope.Actor, 20, modifier),
            new MobaSkillParamModifierSnapshotEntry(MobaModifierOwnerScope.Actor, 20, modifier,
                new MobaParameterSelector(areaTemplateId: 301)),
        };
        var snapshot = new MobaSkillParamModifierServiceSnapshot(entries);
        var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);

        Assert.Throws<InvalidOperationException>(() => modifiers.RestoreRollbackSnapshot(in snapshot));
        Assert.Equal(15f, modifiers.ResolveFloat(owner, key, 10f, in record));
        Assert.Single(modifiers.CaptureRollbackSnapshot().Entries);
    }

    [Fact]
    public void Cast_runtime_preserves_base_skill_lineage_through_restore()
    {
        using var runtimes = new MobaSkillCastRuntimeService();
        var request = new MobaSkillCastRuntimeCreateRequest(1002, 2, 1, 1, 10, 0,
            Vec3.Zero, Vec3.Forward, 0, baseSkillId: 1001);
        var runtime = runtimes.Create(in request);
        var handle = runtime.Handle;
        var rollback = new MobaSkillRuntimeRollbackProvider(runtimes);
        var payload = rollback.Export(new FrameIndex(1));

        Assert.True(runtimes.ForceTerminate(in handle));
        rollback.Import(new FrameIndex(1), payload);
        var record = MobaParameterRecord.FromSkillRuntime(MobaParameterStage.BuffApply,
            runtimes, in handle, buffTemplateId: 2001);
        Assert.Equal(1001, record.BaseSkillId);
        Assert.Equal(1002, record.EffectiveSkillId);
        Assert.Equal(2, record.Slot);
        Assert.Equal(2001, record.BuffTemplateId);
    }

    [Fact]
    public void Stacked_multiplier_compounds_and_override_keeps_its_declared_value()
    {
        using var modifiers = new MobaSkillParamModifierService();
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null, modifiers));
        var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);
        var multiply = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
            TargetId = MobaSkillParamModifierKeys.Projectile.Ids.Speed,
            Op = (int)ModifierOp.Mul, Value = 1.2f,
        });
        var multiplyConfig = new SnapshotConfig(new IMobaContinuousModifierSpec[] { multiply }, 701)
        { Stack = 2 };
        var multiplyRuntime = new SnapshotContinuous(multiplyConfig);
        Assert.True(manager.TryActivate(multiplyRuntime));
        Assert.Equal(14.4f, modifiers.Projectile.ResolveSpeed(10, 10f, in record), 3);
        Assert.True(manager.TryEnd(multiplyRuntime));

        var replace = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
            TargetId = MobaSkillParamModifierKeys.Projectile.Ids.Speed,
            Op = (int)ModifierOp.Override, Value = 30f,
        });
        var replaceConfig = new SnapshotConfig(new IMobaContinuousModifierSpec[] { replace }, 702)
        { Stack = 3 };
        var replaceRuntime = new SnapshotContinuous(replaceConfig);
        Assert.True(manager.TryActivate(replaceRuntime));
        Assert.Equal(30f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));
    }

    [Fact]
    public void Captured_magnitude_rescales_with_its_operation_on_stack_change()
    {
        var runtime = new SnapshotContinuous(new SnapshotConfig(Array.Empty<IMobaContinuousModifierSpec>()));
        runtime.SetModifierCapture(0, 2, 1.44f);
        runtime.SetModifierCapture(1, 2, 30f);
        runtime.SetModifierCapture(2, 2, 10f);

        Assert.True(runtime.TryGetModifierCapture(0, 3, (int)ModifierOp.Mul, out var multiplier));
        Assert.Equal(1.728f, multiplier, 3);
        Assert.True(runtime.TryGetModifierCapture(1, 3, (int)ModifierOp.Override, out var replacement));
        Assert.Equal(30f, replacement);
        Assert.True(runtime.TryGetModifierCapture(2, 3, (int)ModifierOp.Add, out var addition));
        Assert.Equal(15f, addition);
    }

    [Fact]
    public void Captured_negative_multiplier_keeps_its_sign_after_even_to_odd_stack_change()
    {
        var runtime = new SnapshotContinuous(new SnapshotConfig(Array.Empty<IMobaContinuousModifierSpec>()));
        runtime.SetModifierCapture(0, 2, 4f, -2f);

        Assert.True(runtime.TryGetModifierCapture(0, 3, (int)ModifierOp.Mul, out var multiplier));
        Assert.Equal(-8f, multiplier);
    }

    [Fact]
    public void Recorded_parameter_index_survives_source_removal_and_rollback_restore()
    {
        using var service = new MobaSkillParamModifierService();
        var owner = MobaModifierOwnerRef.Actor(10);
        var key = MobaSkillParamModifierKeys.Projectile.Speed;
        foreach (var (source, template, amount) in new[] { (1, 101, 2f), (2, 102, 3f), (3, 101, 5f) })
        {
            var modifier = new ModifierData
            {
                Key = key, Op = ModifierOp.Add, Magnitude = MagnitudeSource.Fixed(amount), SourceId = source,
            };
            service.AddModifier(owner, in modifier, new MobaParameterSelector(projectileTemplateId: template));
        }
        var first = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn, projectileTemplateId: 101);
        var second = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn, projectileTemplateId: 102);
        var snapshot = service.CaptureRollbackSnapshot();

        service.ClearSource(owner, 1);
        Assert.Equal(15f, service.ResolveFloat(owner, key, 10f, in first));
        Assert.Equal(13f, service.ResolveFloat(owner, key, 10f, in second));
        service.RestoreRollbackSnapshot(in snapshot);
        Assert.Equal(17f, service.ResolveFloat(owner, key, 10f, in first));
        Assert.Equal(13f, service.ResolveFloat(owner, key, 10f, in second));
    }

    [Fact]
    public void Recorded_override_order_survives_source_removal_and_rollback_restore()
    {
        using var service = new MobaSkillParamModifierService();
        var owner = MobaModifierOwnerRef.Actor(10);
        var key = MobaSkillParamModifierKeys.Projectile.Speed;
        foreach (var (source, priority, value) in new[]
                 { (30, 10, 30f), (20, 20, 20f), (10, 20, 10f) })
        {
            var modifier = new ModifierData
            {
                Key = key, Op = ModifierOp.Override, Magnitude = MagnitudeSource.Fixed(value),
                SourceId = source, Priority = priority,
            };
            service.AddModifier(owner, in modifier, new MobaParameterSelector(projectileTemplateId: 101));
        }
        var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn,
            projectileTemplateId: 101);
        var snapshot = service.CaptureRollbackSnapshot();

        Assert.Equal(10f, service.ResolveFloat(owner, key, 1f, in record));
        service.ClearSource(owner, 10);
        Assert.Equal(20f, service.ResolveFloat(owner, key, 1f, in record));
        service.RestoreRollbackSnapshot(in snapshot);
        Assert.Equal(10f, service.ResolveFloat(owner, key, 1f, in record));
    }

    [Fact]
    public void Duplicate_modifier_projector_target_kind_is_rejected()
    {
        var registry = new MobaContinuousModifierProjectorRegistry();
        registry.Register(new MobaSkillParamContinuousModifierProjector());
        Assert.Throws<InvalidOperationException>(() =>
            registry.Register(new MobaSkillParamContinuousModifierProjector()));
    }

    [Fact]
    public void Projector_cleanup_continues_after_one_target_kind_fails()
    {
        var registry = new MobaContinuousModifierProjectorRegistry();
        var healthy = new CleanupProbeProjector(100, false);
        var failing = new CleanupProbeProjector(101, true);
        registry.Register(healthy);
        registry.Register(failing);

        Assert.Throws<InvalidOperationException>(() => registry.Clear(new Projection()));
        Assert.Equal(1, failing.ClearCount);
        Assert.Equal(1, healthy.ClearCount);
    }

    [Fact]
    public void Summon_inheritance_scale_matches_template_and_rejects_negative_values()
    {
        using var modifiers = new MobaSkillParamModifierService();
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null, modifiers));
        var spec = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
            TargetId = MobaSkillParamModifierKeys.Summon.Ids.InheritRatioScale,
            Op = (int)ModifierOp.Mul, Value = 1.5f,
            SelectorSummonTemplateId = 101,
        });
        var continuous = new SnapshotContinuous(new SnapshotConfig(
            new IMobaContinuousModifierSpec[] { spec }, 703));
        var matching = new MobaParameterRecord(MobaParameterStage.SummonCreate,
            summonTemplateId: 101);
        var other = new MobaParameterRecord(MobaParameterStage.SummonCreate,
            summonTemplateId: 102);

        Assert.True(manager.TryActivate(continuous));
        Assert.Equal(1.5f, modifiers.Summon.ResolveInheritRatioScale(10, 1f, in matching));
        Assert.Equal(1f, modifiers.Summon.ResolveInheritRatioScale(10, 1f, in other));
        Assert.Throws<InvalidOperationException>(() =>
            modifiers.Summon.ResolveInheritRatioScale(10, -1f, in other));
        Assert.True(manager.TryEnd(continuous));
        Assert.Equal(1f, modifiers.Summon.ResolveInheritRatioScale(10, 1f, in matching));
    }

    [Fact]
    public void Custom_time_curve_keeps_its_last_multiplier_until_the_provider_ends()
    {
        var source = MagnitudeSource.TimeDecay(100f, 2f, new[] { 1f, 0.5f, 0.2f });
        Assert.Equal(50f, source.CalculateAtElapsed(1f));
        Assert.Equal(20f, source.CalculateAtElapsed(2f));
        Assert.Equal(20f, source.CalculateAtElapsed(3f));
    }

    [Fact]
    public void Modifier_debug_curve_samples_are_an_immutable_view_of_the_source()
    {
        var source = MagnitudeSource.TimeDecay(100f, 2f, new[] { 1f, 0.5f, 0.2f });
        var view = new MobaContinuousModifierMagnitudeView(source, 100f, true);
        source.ArrayData[0] = 0f;

        Assert.Equal(new[] { 1f, 0.5f, 0.2f }, view.CurveSamples);
    }

    [Fact]
    public void Continuous_time_curve_samples_its_own_elapsed_time_and_clears_on_end()
    {
        var process = new ContinuousProcessMO(new ContinuousProcessDTO
        {
            Id = 701,
            DurationMs = 5000,
            Modifiers = new[]
            {
                new ContinuousModifierDTO
                {
                    TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                    TargetId = 20001,
                    Op = 0,
                    Value = 100f,
                    MagnitudeSourceType = (int)MagnitudeSourceType.TimeDecay,
                    MagnitudeDuration = 2f,
                    MagnitudeCurve = new[] { 1f, 0.25f, 0f },
                    EvaluationPolicy = MobaContinuousModifierEvaluationPolicy.Realtime,
                }
            }
        });
        var runtime = new MobaTriggerIntervalContinuousRuntime(process, 10, 10, 701L, null);
        using var service = new MobaSkillParamModifierService();
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null, service));
        var record = new MobaParameterRecord(MobaParameterStage.BuffApply);
        Assert.True(manager.TryActivate(runtime));
        Assert.Equal(1100, service.Buff.ResolveDurationMs(10, 1000, in record));
        runtime.TickManaged(1f);
        manager.Reproject(runtime);
        Assert.Equal(1025, service.Buff.ResolveDurationMs(10, 1000, in record));
        Assert.True(manager.TryPause(runtime));
        runtime.TickManaged(1f);
        Assert.Equal(1f, runtime.ElapsedSeconds);
        Assert.True(manager.TryResume(runtime));
        runtime.TickManaged(1f);
        manager.Reproject(runtime);
        Assert.Equal(1000, service.Buff.ResolveDurationMs(10, 1000, in record));

        Assert.True(manager.TryEnd(runtime));
        Assert.Equal(1000, service.Buff.ResolveDurationMs(10, 1000, in record));
    }

    [Fact]
    public void Empty_continuous_modifier_snapshot_has_stable_named_sections()
    {
        using var manager = new MobaContinuousManager();
        var rollback = new MobaContinuousModifierCaptureRollbackProvider(manager);
        var payload = rollback.ExportState(new FrameIndex(0));

        Assert.Equal(
            "060000000300000009616C6C6F6361746F720400000001000000" +
            "09696E74657276616C730400000000000000" +
            "0863617074757265730400000000000000",
            Convert.ToHexString(payload));
        rollback.ImportState(new FrameIndex(0), payload);
        rollback.ImportState(new FrameIndex(0),
            Convert.FromHexString("05000000010000000000000000000000"));
    }

    [Fact]
    public void Runtime_source_ids_distinguish_identical_contexts_and_replay_after_rollback()
    {
        var process = new ContinuousProcessMO(new ContinuousProcessDTO
        {
            Id = 701,
            Modifiers = new[]
            {
                new ContinuousModifierDTO
                {
                    TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                    TargetId = 20001,
                    Op = (int)ModifierOp.Add,
                    Value = 100f,
                },
            },
        });
        using var parameters = new MobaSkillParamModifierService();
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null, parameters));
        var first = new MobaTriggerIntervalContinuousRuntime(process, 10, 10, 701L, null);
        var second = new MobaTriggerIntervalContinuousRuntime(process, 10, 10, 701L, null);
        var record = new MobaParameterRecord(MobaParameterStage.BuffApply);

        Assert.True(manager.TryActivate(first));
        Assert.True(manager.TryActivate(second));
        Assert.True(first.ModifierSourceId > 0);
        Assert.NotEqual(first.ModifierSourceId, second.ModifierSourceId);
        Assert.Equal(1200, parameters.Buff.ResolveDurationMs(10, 1000, in record));

        var rollback = new MobaContinuousModifierCaptureRollbackProvider(manager);
        var snapshot = rollback.ExportState(new FrameIndex(1));
        var invalidSnapshot = (byte[])snapshot.Clone();
        BitConverter.GetBytes(1).CopyTo(invalidSnapshot, 4);
        Assert.Throws<InvalidDataException>(() =>
            rollback.ImportState(new FrameIndex(1), invalidSnapshot));
        using var encoded = new MemoryStream(snapshot);
        using var reader = new BinaryReader(encoded);
        Assert.Equal(6, reader.ReadInt32());
        Assert.Equal(3, reader.ReadInt32());
        var sections = new Dictionary<string, byte[]>();
        for (var i = 0; i < 3; i++)
        {
            var name = reader.ReadString();
            sections.Add(name, reader.ReadBytes(reader.ReadInt32()));
        }
        Assert.Equal(new[] { "allocator", "intervals", "captures" }, sections.Keys);
        using var legacyStream = new MemoryStream();
        using (var writer = new BinaryWriter(legacyStream, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(5);
            writer.Write(sections["allocator"]);
            writer.Write(sections["intervals"]);
            writer.Write(sections["captures"]);
        }
        var third = new MobaTriggerIntervalContinuousRuntime(process, 10, 10, 701L, null);
        Assert.True(manager.TryActivate(third));
        var thirdId = third.ModifierSourceId;
        Assert.True(manager.TryEnd(third));
        rollback.ImportState(new FrameIndex(1), snapshot);
        rollback.ImportState(new FrameIndex(1), legacyStream.ToArray());
        var replayed = new MobaTriggerIntervalContinuousRuntime(process, 10, 10, 701L, null);
        Assert.True(manager.TryActivate(replayed));
        Assert.Equal(thirdId, replayed.ModifierSourceId);
        Assert.True(manager.TryEnd(first));
        Assert.Equal(1200, parameters.Buff.ResolveDurationMs(10, 1000, in record));
    }

    [Fact]
    public void Allocated_source_id_clears_numeric_slot_and_reference_projections_together()
    {
        var process = new ContinuousProcessMO(new ContinuousProcessDTO
        {
            Id = 702,
            Modifiers = new[]
            {
                new ContinuousModifierDTO
                {
                    TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                    TargetId = MobaSkillParamModifierKeys.Projectile.Ids.Speed,
                    Op = (int)ModifierOp.Add,
                    Value = 2f,
                },
                new ContinuousModifierDTO
                {
                    TargetKind = MobaContinuousModifierTargetKind.SkillSlotCandidate,
                    TargetId = 4,
                    CandidateSkillId = 101,
                },
                new ContinuousModifierDTO
                {
                    TargetKind = MobaContinuousModifierTargetKind.ParameterReferenceCandidate,
                    TargetId = MobaSkillParamModifierKeys.Projectile.Ids.ProjectileId,
                    CandidateReferenceId = 102,
                },
            },
        });
        using var parameters = new MobaSkillParamModifierService();
        using var slots = new MobaSkillSlotCandidateService();
        using var references = new MobaReferenceCandidateService();
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null, parameters, candidates: slots, references: references));
        var first = new MobaTriggerIntervalContinuousRuntime(process, 10, 10, 800L, null);
        var second = new MobaTriggerIntervalContinuousRuntime(process, 10, 10, 800L, null);
        var spawn = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);
        var launch = new MobaParameterRecord(MobaParameterStage.LauncherCreate);

        Assert.True(manager.TryActivate(first));
        Assert.True(manager.TryActivate(second));
        Assert.Equal(14f, parameters.Projectile.ResolveSpeed(10, 10f, in spawn));
        Assert.Equal(first.ModifierSourceId, slots.Resolve(10, 4, 100).ProviderSourceId);
        Assert.Equal(102, references.Resolve(10,
            MobaSkillParamModifierKeys.Projectile.Ids.ProjectileId, 101, in launch));

        Assert.True(manager.TryEnd(first));
        Assert.Equal(12f, parameters.Projectile.ResolveSpeed(10, 10f, in spawn));
        Assert.Equal(second.ModifierSourceId, slots.Resolve(10, 4, 100).ProviderSourceId);
        Assert.Equal(102, references.Resolve(10,
            MobaSkillParamModifierKeys.Projectile.Ids.ProjectileId, 101, in launch));

        Assert.True(manager.TryEnd(second));
        Assert.Equal(10f, parameters.Projectile.ResolveSpeed(10, 10f, in spawn));
        Assert.Equal(100, slots.Resolve(10, 4, 100).EffectiveSkillId);
        Assert.Equal(101, references.Resolve(10,
            MobaSkillParamModifierKeys.Projectile.Ids.ProjectileId, 101, in launch));
    }

    [Fact]
    public void Reused_actor_id_does_not_inherit_the_previous_continuous_projections()
    {
        var process = new ContinuousProcessMO(new ContinuousProcessDTO
        {
            Id = 704,
            Modifiers = new[]
            {
                new ContinuousModifierDTO
                {
                    TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                    TargetId = MobaSkillParamModifierKeys.Projectile.Ids.Speed,
                    Op = (int)ModifierOp.Add, Value = 2f,
                },
                new ContinuousModifierDTO
                {
                    TargetKind = MobaContinuousModifierTargetKind.SkillSlotCandidate,
                    TargetId = 4, CandidateSkillId = 101,
                },
                new ContinuousModifierDTO
                {
                    TargetKind = MobaContinuousModifierTargetKind.ParameterReferenceCandidate,
                    TargetId = MobaSkillParamModifierKeys.Projectile.Ids.ProjectileId,
                    CandidateReferenceId = 102,
                },
            },
        });
        using var parameters = new MobaSkillParamModifierService();
        using var slots = new MobaSkillSlotCandidateService();
        using var references = new MobaReferenceCandidateService();
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null, parameters, candidates: slots, references: references));
        var oldActor = new MobaTriggerIntervalContinuousRuntime(process, 10, 10, 800L, null);
        var spawn = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);
        var launch = new MobaParameterRecord(MobaParameterStage.LauncherCreate);

        Assert.True(manager.TryActivate(oldActor));
        var oldSourceId = oldActor.ModifierSourceId;
        manager.EndOwnerActor(10);
        Assert.True(oldActor.IsTerminated);
        Assert.Equal(0, manager.ActiveCount);
        Assert.Equal(10f, parameters.Projectile.ResolveSpeed(10, 10f, in spawn));
        Assert.Equal(100, slots.Resolve(10, 4, 100).EffectiveSkillId);
        Assert.Equal(101, references.Resolve(10,
            MobaSkillParamModifierKeys.Projectile.Ids.ProjectileId, 101, in launch));

        var reusedActor = new MobaTriggerIntervalContinuousRuntime(process, 10, 10, 800L, null);
        Assert.True(manager.TryActivate(reusedActor));
        Assert.True(reusedActor.ModifierSourceId > oldSourceId);
        Assert.Equal(12f, parameters.Projectile.ResolveSpeed(10, 10f, in spawn));
        Assert.Equal(reusedActor.ModifierSourceId, slots.Resolve(10, 4, 100).ProviderSourceId);
        Assert.Equal(102, references.Resolve(10,
            MobaSkillParamModifierKeys.Projectile.Ids.ProjectileId, 101, in launch));
    }

    [Fact]
    public void Many_identical_continuous_sources_can_be_removed_in_any_order()
    {
        var process = new ContinuousProcessMO(new ContinuousProcessDTO
        {
            Id = 703,
            Modifiers = new[]
            {
                new ContinuousModifierDTO
                {
                    TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                    TargetId = MobaSkillParamModifierKeys.Projectile.Ids.Speed,
                    Op = (int)ModifierOp.Add,
                    Value = 0.25f,
                },
            },
        });
        using var parameters = new MobaSkillParamModifierService();
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null, parameters));
        var sources = new MobaTriggerIntervalContinuousRuntime[256];
        var ids = new HashSet<int>();
        var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);
        for (var i = 0; i < sources.Length; i++)
        {
            sources[i] = new MobaTriggerIntervalContinuousRuntime(process, 10, 10, 900L, null);
            Assert.True(manager.TryActivate(sources[i]));
            Assert.True(ids.Add(sources[i].ModifierSourceId));
        }
        Assert.Equal(74f, parameters.Projectile.ResolveSpeed(10, 10f, in record));

        for (var i = sources.Length - 1; i >= 0; i -= 2)
            Assert.True(manager.TryEnd(sources[i]));
        Assert.Equal(42f, parameters.Projectile.ResolveSpeed(10, 10f, in record));
        for (var i = 0; i < sources.Length; i += 2)
            Assert.True(manager.TryEnd(sources[i]));
        Assert.Equal(10f, parameters.Projectile.ResolveSpeed(10, 10f, in record));
    }

    [Fact]
    public void Attribute_time_curve_updates_actor_value_and_debug_explanation()
    {
        var contexts = new Contexts();
        using var index = new ActorIdIndex(contexts);
        using var registry = new MobaActorRegistry();
        using var entities = new MobaEntityManager(null);
        var lookup = new MobaActorLookupService(index, registry, entities, contexts);
        try
        {
            var actor = CreateActor(contexts, 10, 40f);
            using var manager = new MobaContinuousManager();
            using var candidates = new MobaSkillSlotCandidateService();
            var services = new Resolver(lookup, manager: manager,
                candidates: candidates, tags: new MobaEffectiveTagQueryService());
            manager.OnInit(services);
            using var query = new MobaContinuousModifierQueryService();
            query.OnInit(services);
            var spec = new ContinuousModifierMO(new ContinuousModifierDTO
            {
                TargetKind = MobaContinuousModifierTargetKind.Attribute,
                TargetId = (int)BattleAttributeType.MAX_HP,
                Op = (int)ModifierOp.Add,
                Value = 100f,
                MagnitudeSourceType = (int)MagnitudeSourceType.TimeDecay,
                MagnitudeDuration = 2f,
                MagnitudeCurve = new[] { 1f, 0.25f, 0f },
                EvaluationPolicy = MobaContinuousModifierEvaluationPolicy.Realtime,
            });
            var skillCandidate = new ContinuousModifierMO(new ContinuousModifierDTO
            {
                TargetKind = MobaContinuousModifierTargetKind.SkillSlotCandidate,
                TargetId = 4,
                CandidateSkillId = 777,
            });
            var continuous = new SnapshotContinuous(new SnapshotConfig(
                new IMobaContinuousModifierSpec[] { spec, skillCandidate }));

            Assert.True(manager.TryActivate(continuous));
            Assert.Equal(200f, actor.GetValue(MobaAttributeIds.MAX_HP));
            var candidateRevision = candidates.Resolve(10, 4, 100).Revision;
            manager.Tick(1f);
            Assert.Equal(125f, actor.GetValue(MobaAttributeIds.MAX_HP));
            Assert.Equal(candidateRevision, candidates.Resolve(10, 4, 100).Revision);
            var explained = Assert.Single(query.ExplainActiveModifiers(
                10, MobaContinuousModifierTargetKind.Attribute));
            Assert.Equal(25f, explained.CurrentValue);
            Assert.Equal(25f, explained.ProjectedMagnitude.CalculatedValue);
            Assert.Contains("multipliers [1, 0.25, 0]", explained.Reason);

            Assert.True(manager.TryPause(continuous));
            Assert.Equal(100f, actor.GetValue(MobaAttributeIds.MAX_HP));
            Assert.True(manager.TryResume(continuous));
            Assert.Equal(125f, actor.GetValue(MobaAttributeIds.MAX_HP));
            manager.Tick(1f);
            Assert.Equal(100f, actor.GetValue(MobaAttributeIds.MAX_HP));
            Assert.True(manager.TryEnd(continuous));
            Assert.Equal(100f, actor.GetValue(MobaAttributeIds.MAX_HP));
        }
        finally
        {
            contexts.actor.DestroyAllEntities();
        }
    }

    [Fact]
    public void Basic_attack_slot_selects_integer_candidate_and_falls_back()
    {
        using var candidates = new MobaSkillSlotCandidateService();
        candidates.Add(10, 101, 4, 10040112, priority: 20, order: 0);
        candidates.Add(10, 102, 4, 10040113, priority: 30, order: 0);
        Assert.Equal(10040113, candidates.Resolve(10, 4, 10040101).EffectiveSkillId);
        candidates.ClearSource(10, 102);
        Assert.Equal(10040112, candidates.Resolve(10, 4, 10040101).EffectiveSkillId);
        candidates.ClearSource(10, 101);
        Assert.Equal(10040101, candidates.Resolve(10, 4, 10040101).EffectiveSkillId);
    }

    [Fact]
    public void Hit_damage_modifier_matches_skill_record_and_rejects_invalid_result()
    {
        using var service = new MobaSkillParamModifierService();
        var key = MobaSkillParamModifierKeys.Damage.RequestedBase;
        var modifier = new ModifierData
        {
            Key = key,
            Op = ModifierOp.Add,
            Magnitude = MagnitudeSource.Fixed(5f),
            Priority = 20,
            SourceId = 101,
        };
        service.AddModifier(MobaModifierOwnerRef.Actor(10), in modifier,
            new MobaParameterSelector(slot: 2, effectiveSkillId: 2002));
        var matching = new MobaParameterRecord(MobaParameterStage.Hit, slot: 2,
            effectiveSkillId: 2002);
        var otherSkill = new MobaParameterRecord(MobaParameterStage.Hit, slot: 1,
            effectiveSkillId: 2001);

        Assert.Equal(15f, service.Damage.ResolveRequestedBase(10, 10f, in matching));
        var explained = service.ExplainRecorded(MobaModifierOwnerRef.Actor(10), key, 10f, in matching);
        Assert.Equal(10f, explained.BaseValue);
        Assert.Equal(15f, explained.FinalValue);
        Assert.Single(explained.Contributions);
        Assert.Equal(101, explained.Contributions[0].SourceId);
        Assert.Equal(10f, service.Damage.ResolveRequestedBase(10, 10f, in otherSkill));
        Assert.Equal(10f, service.Damage.ResolveRequestedBase(11, 10f, in matching));
        var wrongStage = new MobaParameterRecord(MobaParameterStage.AreaSpawn, slot: 2,
            effectiveSkillId: 2002);
        Assert.Throws<ArgumentException>(() => service.Damage.ResolveRequestedBase(10, 10f, in wrongStage));

        service.ClearSource(10, 101);
        Assert.Equal(10f, service.Damage.ResolveRequestedBase(10, 10f, in matching));
    }

    [Fact]
    public void Summon_lifetime_matches_selected_template_and_preserves_zero_lifetime()
    {
        using var service = new MobaSkillParamModifierService();
        var modifier = new ModifierData
        {
            Key = MobaSkillParamModifierKeys.Summon.LifetimeMs,
            Op = ModifierOp.Add,
            Magnitude = MagnitudeSource.Fixed(500f),
            Priority = 10,
            SourceId = 102,
        };
        service.AddModifier(MobaModifierOwnerRef.Actor(10), in modifier,
            new MobaParameterSelector(summonTemplateId: 200));
        var selected = new MobaParameterRecord(MobaParameterStage.SummonCreate,
            summonTemplateId: 200);
        var other = new MobaParameterRecord(MobaParameterStage.SummonCreate,
            summonTemplateId: 201);

        Assert.Equal(1500, service.Summon.ResolveLifetimeMs(10, 1000, in selected));
        Assert.Equal(1000, service.Summon.ResolveLifetimeMs(10, 1000, in other));
        service.ClearSource(10, 102);
        Assert.Equal(0, service.Summon.ResolveLifetimeMs(10, 0, in selected));
    }

    [Fact]
    public void Buff_duration_matches_template_and_selector_survives_rollback()
    {
        using var service = new MobaSkillParamModifierService();
        var modifier = new ModifierData
        {
            Key = MobaSkillParamModifierKeys.Buff.DurationMs,
            Op = ModifierOp.Add,
            Magnitude = MagnitudeSource.Fixed(500f),
            Priority = 10,
            SourceId = 102,
        };
        service.AddModifier(MobaModifierOwnerRef.Actor(10), in modifier,
            new MobaParameterSelector(buffTemplateId: 200));
        var selected = new MobaParameterRecord(MobaParameterStage.BuffApply,
            buffTemplateId: 200);
        var other = new MobaParameterRecord(MobaParameterStage.BuffApply,
            buffTemplateId: 201);

        Assert.Equal(1500, service.Buff.ResolveDurationMs(10, 1000, in selected));
        Assert.Equal(1000, service.Buff.ResolveDurationMs(10, 1000, in other));
        var rollback = new MobaSkillParamModifierRollbackProvider(service, restoreProjection: true);
        var payload = rollback.ExportState(new FrameIndex(1));
        service.ClearActor(10);
        rollback.ImportState(new FrameIndex(1), payload);
        Assert.Equal(1500, service.Buff.ResolveDurationMs(10, 1000, in selected));
        Assert.Equal(1000, service.Buff.ResolveDurationMs(10, 1000, in other));
    }

    [Fact]
    public void New_stage_parameters_match_their_record_and_reject_wrong_stage()
    {
        using var service = new MobaSkillParamModifierService();
        foreach (var key in new[]
                 {
                     MobaSkillParamModifierKeys.Emitter.IntervalMs,
                     MobaSkillParamModifierKeys.Projectile.LifetimeMs,
                     MobaSkillParamModifierKeys.Aoe.IntervalMs,
                 })
        {
            var modifier = new ModifierData
            {
                Key = key, Op = ModifierOp.Add,
                Magnitude = MagnitudeSource.Fixed(100f), SourceId = key.GetHashCode(),
            };
            service.AddModifier(MobaModifierOwnerRef.Actor(10), in modifier);
        }
        var launcher = new MobaParameterRecord(MobaParameterStage.LauncherCreate,
            launcherTemplateId: 201);
        var projectile = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn,
            projectileTemplateId: 101);
        var area = new MobaParameterRecord(MobaParameterStage.AreaSpawn,
            areaTemplateId: 301);

        Assert.Equal(300, service.Projectile.ResolveIntervalMs(10, 200, in launcher));
        Assert.Equal(1100, service.Projectile.ResolveLifetimeMs(10, 1000, in projectile));
        Assert.Equal(600, service.Aoe.ResolveIntervalMs(10, 500, in area));
        Assert.Throws<ArgumentException>(() => service.Projectile.ResolveIntervalMs(10, 200, in projectile));
        Assert.Throws<ArgumentException>(() => service.Aoe.ResolveIntervalMs(10, 500, in launcher));
    }

    [Fact]
    public void Recorded_parameter_diagnostics_are_bounded_and_reset_on_rollback()
    {
        using var service = new MobaSkillParamModifierService();
        var record = new MobaParameterRecord(MobaParameterStage.AreaSpawn, areaTemplateId: 301);
        for (var i = 0; i < 140; i++)
            service.Aoe.ResolveIntervalMs(10, i, in record);
        var traces = new List<MobaParameterResolutionTrace>();
        service.CopyRecentResolutionsTo(traces);
        Assert.Equal(128, traces.Count);
        Assert.Equal(12f, traces[0].BaseValue);
        Assert.Equal(139f, traces[127].FinalValue);
        Assert.Equal(0, traces[127].MatchedCount);

        service.RestoreRollbackSnapshot(default);
        service.CopyRecentResolutionsTo(traces);
        Assert.Empty(traces);
    }

    [Fact]
    public void Recorded_projectile_resolution_has_no_per_shot_managed_allocation()
    {
        using var service = new MobaSkillParamModifierService();
        for (var i = 1; i <= 200; i++)
        {
            var unrelated = new ModifierData
            {
                Key = ModifierKey.Create(100, (byte)i),
                Op = ModifierOp.Add,
                Magnitude = MagnitudeSource.Fixed(i),
                SourceId = 1000 + i,
            };
            service.AddModifier(MobaModifierOwnerRef.Actor(10), in unrelated);
        }
        for (var source = 1; source <= 2; source++)
        {
            var modifier = new ModifierData
            {
                Key = MobaSkillParamModifierKeys.Projectile.Speed,
                Op = ModifierOp.Add,
                Magnitude = MagnitudeSource.Fixed(source),
                SourceId = source,
            };
            service.AddModifier(MobaModifierOwnerRef.Actor(10), in modifier);
        }
        var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn,
            projectileTemplateId: 101);
        for (var i = 0; i < 2000; i++)
            service.Projectile.ResolveSpeed(10, 10f, in record);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
            service.Projectile.ResolveSpeed(10, 10f, in record);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.InRange(allocated, 0L, 1024L);
    }

    [Fact]
    public void Many_owners_and_continuous_sources_keep_hot_reads_bounded()
    {
        using var modifiers = new MobaSkillParamModifierService();
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null, modifiers));
        var active = new List<SnapshotContinuous>();
        for (var owner = 1; owner <= 16; owner++)
        {
            for (var source = 1; source <= 16; source++)
            {
                var specs = new IMobaContinuousModifierSpec[]
                {
                    new ContinuousModifierMO(new ContinuousModifierDTO
                    {
                        TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                        TargetId = MobaSkillParamModifierKeys.Projectile.Ids.Speed,
                        Op = (int)ModifierOp.Add, Value = 1f,
                    }),
                    new ContinuousModifierMO(new ContinuousModifierDTO
                    {
                        TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                        TargetId = MobaSkillParamModifierKeys.Aoe.Ids.Radius,
                        Op = (int)ModifierOp.Add, Value = 0.5f,
                    }),
                };
                var continuous = new SnapshotContinuous(new SnapshotConfig(specs,
                    sourceId: source, ownerActorId: owner));
                Assert.True(manager.TryActivate(continuous));
                active.Add(continuous);
            }
        }
        Assert.Equal(512, modifiers.CaptureRollbackSnapshot().Entries.Length);
        var projectile = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);
        var area = new MobaParameterRecord(MobaParameterStage.AreaSpawn);
        for (var i = 0; i < 8000; i++)
        {
            var owner = i % 16 + 1;
            modifiers.Projectile.ResolveSpeed(owner, 10f, in projectile);
            modifiers.Aoe.ResolveRadius(owner, 4f, in area);
        }

        var speedSum = 0f;
        var radiusSum = 0f;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 8000; i++)
        {
            var owner = i % 16 + 1;
            speedSum += modifiers.Projectile.ResolveSpeed(owner, 10f, in projectile);
            radiusSum += modifiers.Aoe.ResolveRadius(owner, 4f, in area);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(208000f, speedSum);
        Assert.Equal(96000f, radiusSum);
        Assert.InRange(allocated, 0L, 1024L);
        var traces = new List<MobaParameterResolutionTrace>();
        modifiers.CopyRecentResolutionsTo(traces);
        Assert.Equal(128, traces.Count);
        Assert.True(modifiers.EvictedResolutionTraceCount > 0);

        for (var i = 0; i < active.Count; i++)
            Assert.True(manager.TryEnd(active[i]));
        Assert.Empty(modifiers.CaptureRollbackSnapshot().Entries);
    }

    [Fact]
    public void Attribute_magnitude_reads_registered_attribute_and_can_be_captured()
    {
        var context = new AttributeContext();
        context.SetBase(MobaAttributeIds.PHYSICS_ATTACK, 120f);
        var modifier = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            MagnitudeSourceType = (int)MagnitudeSourceType.Attribute,
            MagnitudeAttributeTypeId = (int)BattleAttributeType.PHYSICS_ATTACK,
            MagnitudeCoefficient = 0.25f,
            EvaluationPolicy = MobaContinuousModifierEvaluationPolicy.OnApplySnapshot,
            AttributeSourceRole = MobaModifierAttributeSourceRole.Source,
        });

        Assert.Equal(30f, modifier.Magnitude.Calculate(context.Level, context));
        var captured = MagnitudeSource.Fixed(modifier.Magnitude.Calculate(context.Level, context));
        context.SetBase(MobaAttributeIds.PHYSICS_ATTACK, 200f);
        Assert.Equal(50f, modifier.Magnitude.Calculate(context.Level, context));
        Assert.Equal(30f, captured.Calculate(context.Level, context));
    }

    [Fact]
    public void Attribute_source_role_selects_owner_source_or_target_actor()
    {
        var projection = new Projection();
        Assert.Equal(10, MobaModifierAttributeSourceResolver.ResolveActorId(projection, MobaModifierAttributeSourceRole.Owner));
        Assert.Equal(20, MobaModifierAttributeSourceResolver.ResolveActorId(projection, MobaModifierAttributeSourceRole.Source));
        Assert.Equal(30, MobaModifierAttributeSourceResolver.ResolveActorId(projection, MobaModifierAttributeSourceRole.Target));
        Assert.Equal(0, MobaModifierAttributeSourceResolver.ResolveActorId(projection, 99));
    }

    [Theory]
    [InlineData(MobaModifierAttributeSourceRole.Owner, 10f)]
    [InlineData(MobaModifierAttributeSourceRole.Source, 30f)]
    [InlineData(MobaModifierAttributeSourceRole.Target, 20f)]
    public void Attribute_projector_captures_selected_actor_at_projection(int role, float expectedBonus)
    {
        var contexts = new Contexts();
        using var index = new ActorIdIndex(contexts);
        using var registry = new MobaActorRegistry();
        using var entities = new MobaEntityManager(null);
        var lookup = new MobaActorLookupService(index, registry, entities, contexts);
        try
        {
            var owner = CreateActor(contexts, 10, 40f);
            var source = CreateActor(contexts, 20, 120f);
            CreateActor(contexts, 30, 80f);
            var projector = new MobaAttributeContinuousModifierProjector();
            projector.OnInit(new Resolver(lookup));
            var projection = new Projection();
            var continuous = new Continuous();
            var spec = new ContinuousModifierMO(new ContinuousModifierDTO
            {
                TargetKind = MobaContinuousModifierTargetKind.Attribute,
                TargetId = (int)BattleAttributeType.MAX_HP,
                MagnitudeSourceType = (int)MagnitudeSourceType.Attribute,
                MagnitudeAttributeTypeId = (int)BattleAttributeType.PHYSICS_ATTACK,
                MagnitudeCoefficient = 0.25f,
                EvaluationPolicy = MobaContinuousModifierEvaluationPolicy.OnApplySnapshot,
                AttributeSourceRole = role,
            });

            projector.Apply(continuous, projection, new IMobaContinuousModifierSpec[] { spec });
            var active = owner.GetGroupFor(MobaAttributeIds.MAX_HP)
                .GetOrCreate(MobaAttributeIds.MAX_HP).GetActiveModifierData().ToArray();
            Assert.Equal(1, active.Length);
            Assert.Equal(MagnitudeSourceType.Fixed, active[0].Magnitude.Type);
            Assert.Equal(expectedBonus, active[0].Magnitude.Calculate());

            source.SetBase(MobaAttributeIds.PHYSICS_ATTACK, 200f);
            Assert.Equal(expectedBonus, active[0].Magnitude.Calculate());
            projector.Clear(projection);
        }
        finally
        {
            contexts.actor.DestroyAllEntities();
        }
    }

    [Theory]
    [InlineData(MobaModifierAttributeSourceRole.Owner, 10f)]
    [InlineData(MobaModifierAttributeSourceRole.Source, 30f)]
    [InlineData(MobaModifierAttributeSourceRole.Target, 20f)]
    public void Snapshot_survives_reprojection_and_rollback(int role, float expected)
    {
        var contexts = new Contexts();
        using var index = new ActorIdIndex(contexts);
        using var registry = new MobaActorRegistry();
        using var entities = new MobaEntityManager(null);
        var lookup = new MobaActorLookupService(index, registry, entities, contexts);
        try
        {
            var owner = CreateActor(contexts, 10, 40f);
            var source = CreateActor(contexts, 20, 120f);
            var target = CreateActor(contexts, 30, 80f);
            var skillParams = new MobaSkillParamModifierService();
            var resolver = new Resolver(lookup, skillParams);
            var attributeProjector = new MobaAttributeContinuousModifierProjector();
            var skillProjector = new MobaSkillParamContinuousModifierProjector();
            attributeProjector.OnInit(resolver);
            skillProjector.OnInit(resolver);

            var specs = new IMobaContinuousModifierSpec[]
            {
                CreateSnapshotSpec(MobaContinuousModifierTargetKind.Attribute, (int)BattleAttributeType.MAX_HP, role, 0.25f),
                CreateSnapshotSpec(MobaContinuousModifierTargetKind.Attribute, (int)BattleAttributeType.MAX_HP, role, 0.5f),
                CreateSnapshotSpec(MobaContinuousModifierTargetKind.SkillParameter,
                    MobaSkillParamModifierKeys.Skill.Ids.CastRange, role, 0.25f),
            };
            var config = new SnapshotConfig(specs);
            var continuous = new SnapshotContinuous(config);
            using var manager = new MobaContinuousManager();
            Assert.True(manager.TryActivate(continuous));
            attributeProjector.Apply(continuous, config, specs);
            skillProjector.Apply(continuous, config, specs);
            Assert.Equal(expected, continuous.ModifierCaptures[0].Value);
            Assert.Equal(expected * 2f, continuous.ModifierCaptures[1].Value);
            Assert.Equal(expected, continuous.ModifierCaptures[2].Value);
            Assert.True(continuous.ModifierCaptures[1].HasUnitValue);
            Assert.Equal(expected * 2f, continuous.ModifierCaptures[1].UnitValue);

            var rollback = new MobaContinuousModifierCaptureRollbackProvider(manager);
            var snapshot = rollback.ExportState(new FrameIndex(1));
            owner.SetBase(MobaAttributeIds.PHYSICS_ATTACK, 400f);
            source.SetBase(MobaAttributeIds.PHYSICS_ATTACK, 400f);
            target.SetBase(MobaAttributeIds.PHYSICS_ATTACK, 400f);
            attributeProjector.Clear(config);
            skillProjector.Clear(config);
            config.Stack = 2;
            attributeProjector.Apply(continuous, config, specs);
            skillProjector.Apply(continuous, config, specs);
            var stacked = owner.GetGroupFor(MobaAttributeIds.MAX_HP)
                .GetOrCreate(MobaAttributeIds.MAX_HP).GetActiveModifierData().ToArray();
            Assert.Equal(2, stacked.Length);
            Assert.Contains(stacked, modifier => modifier.Magnitude.Calculate() == expected * 2f);
            Assert.Contains(stacked, modifier => modifier.Magnitude.Calculate() == expected * 4f);
            Assert.Equal(expected * 2f, skillParams.CaptureRollbackSnapshot().Entries[0].Modifier.Magnitude.Calculate());

            continuous.RestoreModifierCaptures(new Dictionary<int, MobaModifierCapturedValue>
            {
                [0] = new MobaModifierCapturedValue(1, 999f),
                [1] = new MobaModifierCapturedValue(1, 999f),
                [2] = new MobaModifierCapturedValue(1, 999f),
            });
            rollback.ImportState(new FrameIndex(1), snapshot);
            Assert.Equal(expected, continuous.ModifierCaptures[0].Value);
            Assert.Equal(expected * 2f, continuous.ModifierCaptures[1].Value);
            Assert.Equal(expected, continuous.ModifierCaptures[2].Value);
            Assert.True(continuous.ModifierCaptures[1].HasUnitValue);
            Assert.Equal(expected * 2f, continuous.ModifierCaptures[1].UnitValue);

            attributeProjector.Clear(config);
            skillProjector.Clear(config);
            config.Stack = 1;
            attributeProjector.Apply(continuous, config, specs);
            skillProjector.Apply(continuous, config, specs);
            var active = owner.GetGroupFor(MobaAttributeIds.MAX_HP)
                .GetOrCreate(MobaAttributeIds.MAX_HP).GetActiveModifierData().ToArray();
            Assert.Equal(2, active.Length);
            Assert.Contains(active, modifier => modifier.Magnitude.Calculate() == expected);
            Assert.Contains(active, modifier => modifier.Magnitude.Calculate() == expected * 2f);
            Assert.Equal(expected, skillParams.CaptureRollbackSnapshot().Entries[0].Modifier.Magnitude.Calculate());
            continuous.End(ContinuousEndReason.Completed);
            Assert.Empty(continuous.ModifierCaptures);
        }
        finally
        {
            contexts.actor.DestroyAllEntities();
        }
    }

    private static ContinuousModifierMO CreateSnapshotSpec(int kind, int targetId, int role, float coefficient)
    {
        return new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = kind,
            TargetId = targetId,
            MagnitudeSourceType = (int)MagnitudeSourceType.Attribute,
            MagnitudeAttributeTypeId = (int)BattleAttributeType.PHYSICS_ATTACK,
            MagnitudeCoefficient = coefficient,
            EvaluationPolicy = MobaContinuousModifierEvaluationPolicy.OnApplySnapshot,
            AttributeSourceRole = role,
        });
    }

    [Fact]
    public void Rollback_ignores_new_continuous_without_captured_modifiers()
    {
        using var manager = new MobaContinuousManager();
        var rollback = new MobaContinuousModifierCaptureRollbackProvider(manager);
        var snapshot = rollback.ExportState(new FrameIndex(1));
        Assert.True(manager.TryActivate(new SnapshotContinuous(new SnapshotConfig(Array.Empty<IMobaContinuousModifierSpec>()))));
        rollback.ImportState(new FrameIndex(1), snapshot);
    }

    [Fact]
    public void Trigger_interval_rollback_restores_service_membership_and_modifier_projection()
    {
        var configs = new MobaConfigDatabase();
        var loaded = configs.ReloadFromDtoArrays(new Dictionary<Type, Array>
        {
            [typeof(ContinuousProcessDTO)] = new[]
            {
                new ContinuousProcessDTO
                {
                    Id = 701,
                    DurationMs = 5000,
                    Modifiers = new[]
                    {
                        new ContinuousModifierDTO
                        {
                            TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                            TargetId = MobaSkillParamModifierKeys.Projectile.Ids.Speed,
                            Op = (int)ModifierOp.Add,
                            Value = 2f,
                        }
                    }
                }
            }
        }, strict: false);
        Assert.True(loaded.Succeeded, loaded.Error);
        using var parameters = new MobaSkillParamModifierService();
        using var manager = new MobaContinuousManager();
        var resolver = new Resolver(null, parameters, manager, configs: configs);
        manager.OnInit(resolver);
        var processes = new MobaTriggerIntervalContinuousService();
        processes.OnInit(resolver);
        var frame = new FrameIndex(1);
        var provider = new MobaContinuousModifierCaptureRollbackProvider(manager, processes);
        var probe = new RollbackImportProbe { Value = 1 };
        var registry = new RollbackRegistry();
        registry.Register(probe);
        registry.Register(new MobaSkillParamModifierRollbackProvider(parameters));
        registry.Register(provider);
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
        var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);
        var source = new MobaContextSourceView(MobaContextSourceResolveKind.DirectProvider,
            MobaContextSourceBoundary.Execution, EffectContextKind.ContinuousPeriodic,
            MobaExecutionKind.EffectExecution, 10, 10, 111L, 101L, 100L, 111L,
            701, 77, 3, "RollbackTest", 701, false, default);
        var unrelated = new SnapshotContinuous(new SnapshotConfig(
            new IMobaContinuousModifierSpec[] { SpeedModifier(5f) }, 40, 20));
        Assert.True(manager.TryActivate(unrelated));

        Assert.True(processes.UpsertProcess(701, 10, 10, 111L, source));
        var original = Assert.IsType<MobaTriggerIntervalContinuousRuntime>(
            Assert.Single(manager.GetOwnerActiveContinuous(10)));
        original.TickManaged(0.5f);
        original.IntervalRemainingSeconds = 0.75f;
        Assert.True(manager.TryPause(original));
        var elapsed = original.ElapsedSeconds;
        var snapshot = provider.ExportState(frame);
        var worldSnapshot = coordinator.Capture(frame);
        var hash = new MobaStateHashBuilder(2166136261u);
        provider.AddStateHash(frame, ref hash);
        var expectedHash = hash.Value;

        processes.EndOwnerProcesses(111L);
        Assert.True(processes.UpsertProcess(701, 10, 10, 222L, default));
        Assert.Equal(12f, parameters.Projectile.ResolveSpeed(10, 10f, in record));
        var invalidEntries = (WorldRollbackSnapshotEntry[])worldSnapshot.Entries.Clone();
        var captureIndex = Array.FindIndex(invalidEntries,
            entry => entry.Key == MobaContinuousModifierCaptureRollbackProvider.DefaultKey);
        var invalidPayload = (byte[])invalidEntries[captureIndex].Payload.Clone();
        BitConverter.GetBytes(999999).CopyTo(invalidPayload, 12);
        invalidEntries[captureIndex] = new WorldRollbackSnapshotEntry(
            invalidEntries[captureIndex].Key, invalidPayload);
        probe.Value = 2;
        Assert.False(coordinator.TryRestore(new WorldRollbackSnapshot(worldSnapshot.Version,
            worldSnapshot.Frame, invalidEntries), out var invalidResult));
        Assert.Equal(RollbackOperationStatus.ProviderFailed, invalidResult.Status);
        Assert.Equal(2, probe.Value);
        Assert.Equal(222L, Assert.IsType<MobaTriggerIntervalContinuousRuntime>(
            Assert.Single(manager.GetOwnerActiveContinuous(10))).SourceContextId);
        Assert.Equal(12f, parameters.Projectile.ResolveSpeed(10, 10f, in record));
        provider.ValidateImport(frame, snapshot);
        Assert.True(coordinator.TryRestore(worldSnapshot, out var result), result.Message);

        var restored = Assert.IsType<MobaTriggerIntervalContinuousRuntime>(
            Assert.Single(manager.GetOwnerContinuous(10)));
        Assert.Equal(111L, restored.SourceContextId);
        Assert.Equal(elapsed, restored.ElapsedSeconds);
        Assert.Equal(0.75f, restored.IntervalRemainingSeconds);
        Assert.True(restored.IsPaused);
        Assert.True(restored.TryGetContextSource(out var restoredSource));
        Assert.Equal(101L, restoredSource.ParentContextId);
        Assert.Equal(100L, restoredSource.RootContextId);
        Assert.Equal(77, restoredSource.TriggerId);
        Assert.Equal(3, restoredSource.Frame);
        Assert.Equal(original.ModifierSourceId, restored.ModifierSourceId);
        Assert.Equal(10f, parameters.Projectile.ResolveSpeed(10, 10f, in record));
        Assert.Equal(15f, parameters.Projectile.ResolveSpeed(20, 10f, in record));
        Assert.Same(unrelated, Assert.Single(manager.GetOwnerActiveContinuous(20)));
        hash = new MobaStateHashBuilder(2166136261u);
        provider.AddStateHash(frame, ref hash);
        Assert.Equal(expectedHash, hash.Value);
        restored.IntervalRemainingSeconds = 0.25f;
        provider.ImportState(frame, snapshot);
        Assert.Same(restored, Assert.Single(manager.GetOwnerContinuous(10)));
        Assert.Equal(0.75f, restored.IntervalRemainingSeconds);
        Assert.True(manager.TryResume(restored));
        Assert.Equal(12f, parameters.Projectile.ResolveSpeed(10, 10f, in record));
        Assert.True(processes.UpsertProcess(701, 10, 10, 111L, default));
        Assert.Single(manager.GetOwnerActiveContinuous(10));
        processes.EndOwnerProcesses(111L);
        Assert.Empty(manager.GetOwnerActiveContinuous(10));
        Assert.Equal(10f, parameters.Projectile.ResolveSpeed(10, 10f, in record));
        Assert.Equal(15f, parameters.Projectile.ResolveSpeed(20, 10f, in record));
        processes.Dispose();
    }

    [Fact]
    public void Rollback_preflight_rejects_missing_continuous_before_other_providers_import()
    {
        using var parameters = new MobaSkillParamModifierService();
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null, parameters));
        var continuous = new SnapshotContinuous(new SnapshotConfig(
            new IMobaContinuousModifierSpec[] { SpeedModifier(2f) }));
        Assert.True(manager.TryActivate(continuous));

        var probe = new RollbackImportProbe { Value = 10 };
        var registry = new RollbackRegistry();
        registry.Register(probe);
        registry.Register(new MobaContinuousModifierCaptureRollbackProvider(manager));
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
        var snapshot = coordinator.Capture(new FrameIndex(1));
        probe.Value = 20;
        Assert.True(manager.TryEnd(continuous));

        Assert.False(coordinator.TryRestore(snapshot, out var result));
        Assert.Equal(RollbackOperationStatus.ProviderFailed, result.Status);
        Assert.Equal(20, probe.Value);
    }

    [Fact]
    public void Rollback_preflight_rejects_invalid_parameter_projection_before_other_imports()
    {
        using var parameters = new MobaSkillParamModifierService();
        var modifier = new ModifierData
        {
            Key = MobaSkillParamModifierKeys.Projectile.Speed,
            Op = ModifierOp.Add,
            Magnitude = MagnitudeSource.Fixed(2f),
            SourceId = 17,
        };
        parameters.AddModifier(MobaModifierOwnerRef.Actor(10), in modifier);
        var probe = new RollbackImportProbe { Value = 10 };
        var registry = new RollbackRegistry();
        registry.Register(probe);
        registry.Register(new MobaSkillParamModifierRollbackProvider(parameters,
            restoreProjection: true));
        var coordinator = new RollbackCoordinator(registry, new RollbackSnapshotRingBuffer(2));
        var snapshot = coordinator.Capture(new FrameIndex(1));
        var entries = (WorldRollbackSnapshotEntry[])snapshot.Entries.Clone();
        var invalidPayload = (byte[])entries[1].Payload.Clone();
        BitConverter.GetBytes((int)MobaModifierOwnerScope.Launcher).CopyTo(invalidPayload, 8);
        entries[1] = new WorldRollbackSnapshotEntry(entries[1].Key, invalidPayload);
        var invalid = new WorldRollbackSnapshot(snapshot.Version, snapshot.Frame, entries);
        probe.Value = 20;

        Assert.False(coordinator.TryRestore(invalid, out var result));
        Assert.Equal(RollbackOperationStatus.ProviderFailed, result.Status);
        Assert.Equal(20, probe.Value);
        Assert.Single(parameters.CaptureRollbackSnapshot().Entries);
    }

    [Fact]
    public void Two_worlds_restore_identical_modifier_hash_with_matching_continuous_instances()
    {
        using var sourceParameters = new MobaSkillParamModifierService();
        using var targetParameters = new MobaSkillParamModifierService();
        using var sourceManager = new MobaContinuousManager();
        using var targetManager = new MobaContinuousManager();
        sourceManager.OnInit(new Resolver(null, sourceParameters));
        targetManager.OnInit(new Resolver(null, targetParameters));
        var specs = new IMobaContinuousModifierSpec[] { SpeedModifier(2f) };
        Assert.True(sourceManager.TryActivate(new SnapshotContinuous(new SnapshotConfig(specs))));
        Assert.True(targetManager.TryActivate(new SnapshotContinuous(new SnapshotConfig(specs))));

        MobaDeterministicCheckpointCoordinator Coordinator(
            MobaSkillParamModifierService parameters, MobaContinuousManager manager) =>
            new MobaDeterministicCheckpointCoordinator("modifier-replay", "Moba", 30,
                new IMobaStateRecoveryProvider[]
                {
                    new MobaSkillParamModifierRollbackProvider(parameters),
                    new MobaContinuousModifierCaptureRollbackProvider(manager),
                });
        var source = Coordinator(sourceParameters, sourceManager);
        var target = Coordinator(targetParameters, targetManager);
        var frame = new FrameIndex(1);
        var checkpoint = source.Capture(frame);
        Assert.Equal(checkpoint.StateHash, target.ComputeStateHash(frame));

        var extra = new ModifierData
        {
            Key = MobaSkillParamModifierKeys.Projectile.Speed,
            Op = ModifierOp.Add,
            Magnitude = MagnitudeSource.Fixed(5f),
            SourceId = 99,
        };
        targetParameters.AddModifier(MobaModifierOwnerRef.Actor(10), in extra);
        Assert.NotEqual(checkpoint.StateHash, target.ComputeStateHash(frame));
        target.Restore(checkpoint);
        Assert.Equal(checkpoint.StateHash, target.ComputeStateHash(frame));
        var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);
        Assert.Equal(12f, targetParameters.Projectile.ResolveSpeed(10, 10f, in record));
    }

    [Fact]
    public void Continuous_modifier_source_collision_does_not_clear_existing_owner_effect()
    {
        using var modifiers = new MobaSkillParamModifierService();
        var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null, modifiers));
        var spec = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
            TargetId = 40002,
            Op = (int)ModifierOp.Add,
            Value = 3f,
            Priority = 10,
        });
        var first = new SnapshotContinuous(new SnapshotConfig(new IMobaContinuousModifierSpec[] { spec }));
        var second = new SnapshotContinuous(new SnapshotConfig(new IMobaContinuousModifierSpec[] { spec }));

        Assert.True(manager.TryActivate(first));
        Assert.Equal(13f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));
        Assert.Throws<InvalidOperationException>(() => manager.TryActivate(second));
        Assert.Equal(13f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));

        Assert.True(manager.TryEnd(first));
        Assert.Equal(10f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));
        Assert.True(manager.TryActivate(second));
        Assert.Equal(13f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));
    }

    [Fact]
    public void Trigger_modifier_bridge_tracks_lifecycle_and_restores_from_rollback()
    {
        using var modifiers = new MobaSkillParamModifierService();
        var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);
        using var manager = new MobaContinuousManager();
        var resolver = new Resolver(null, modifiers, manager);
        manager.OnInit(resolver);
        using var bridge = new MobaTriggerModifierContinuousService();
        bridge.OnInit(resolver);

        Assert.True(bridge.Add(10, 501, MobaSkillParamModifierKeys.Projectile.Ids.Speed,
            ModifierOp.Add, 3f, 10));
        Assert.Single(((IMobaContinuousModifierConfig)bridge.GetActive()[0].Config).Modifiers);
        Assert.Single(modifiers.CaptureRollbackSnapshot().Entries);
        Assert.Equal(13f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));
        Assert.True(bridge.Add(10, 501, MobaSkillParamModifierKeys.Projectile.Ids.Speed,
            ModifierOp.Add, 2f, 10));
        Assert.Single(bridge.GetActive());
        Assert.Equal(15f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));

        var runtime = bridge.GetActive()[0];
        Assert.True(manager.TryPause(runtime));
        Assert.Equal(10f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));
        Assert.True(manager.TryResume(runtime));
        Assert.Equal(15f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));

        var rollback = new MobaTriggerModifierContinuousRollbackProvider(bridge);
        var snapshot = rollback.ExportState(new FrameIndex(1));
        var invalidSnapshot = (byte[])snapshot.Clone();
        BitConverter.GetBytes(1).CopyTo(invalidSnapshot, 20);
        Assert.Throws<InvalidDataException>(() => rollback.ValidateImport(new FrameIndex(1), invalidSnapshot));
        Assert.Equal(15f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));
        Assert.True(bridge.Remove(10, 501));
        Assert.Equal(10f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));
        rollback.ImportState(new FrameIndex(1), snapshot);
        Assert.Equal(15f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));
        Assert.True(manager.TryPause(bridge.GetActive()[0]));
        var pausedSnapshot = rollback.ExportState(new FrameIndex(2));
        Assert.True(bridge.Remove(10, 501));
        rollback.ImportState(new FrameIndex(2), pausedSnapshot);
        Assert.Equal(10f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));
        Assert.True(manager.TryResume(bridge.GetActive()[0]));
        Assert.Equal(15f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));
        Assert.True(bridge.Remove(10, 501));
        Assert.Equal(10f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));
    }

    [Fact]
    public void Projectile_record_filters_two_continuous_sources_and_survives_rollback()
    {
        using var modifiers = new MobaSkillParamModifierService();
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null, modifiers));
        var global = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
            TargetId = 40002, Op = (int)ModifierOp.Add, Value = 2f, Priority = 10,
        });
        var selected = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
            TargetId = 40002, Op = (int)ModifierOp.Add, Value = 3f, Priority = 20,
            SelectorProjectileTemplateId = 101,
        });
        var first = new SnapshotContinuous(new SnapshotConfig(new IMobaContinuousModifierSpec[] { global }, 40));
        var second = new SnapshotContinuous(new SnapshotConfig(new IMobaContinuousModifierSpec[] { selected }, 41));
        var p1 = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn, projectileTemplateId: 101);
        var p2 = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn, projectileTemplateId: 102);

        Assert.True(manager.TryActivate(first));
        Assert.True(manager.TryActivate(second));
        Assert.Equal(15f, modifiers.Projectile.ResolveSpeed(10, 10f, in p1));
        Assert.Equal(12f, modifiers.Projectile.ResolveSpeed(10, 10f, in p2));

        var rollback = new MobaSkillParamModifierRollbackProvider(modifiers, restoreProjection: true);
        var snapshot = rollback.ExportState(new FrameIndex(1));
        Assert.True(manager.TryPause(second));
        Assert.Equal(12f, modifiers.Projectile.ResolveSpeed(10, 10f, in p1));
        Assert.True(manager.TryResume(second));
        Assert.Equal(15f, modifiers.Projectile.ResolveSpeed(10, 10f, in p1));
        modifiers.ClearActor(10);
        rollback.ImportState(new FrameIndex(1), snapshot);
        Assert.Equal(15f, modifiers.Projectile.ResolveSpeed(10, 10f, in p1));
        Assert.True(manager.TryEnd(second));
        Assert.Equal(12f, modifiers.Projectile.ResolveSpeed(10, 10f, in p1));
    }

    [Fact]
    public void One_owner_combines_emit_projectile_and_area_modifiers_without_cross_stage_leakage()
    {
        using var modifiers = new MobaSkillParamModifierService();
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null, modifiers));
        var upgrade = new IMobaContinuousModifierSpec[]
        {
            new ContinuousModifierMO(new ContinuousModifierDTO
            {
                TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                TargetId = MobaSkillParamModifierKeys.Emitter.Ids.CountPerShot,
                Op = (int)ModifierOp.Add, Value = 2f, SelectorSlot = 2,
            }),
            new ContinuousModifierMO(new ContinuousModifierDTO
            {
                TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                TargetId = MobaSkillParamModifierKeys.Projectile.Ids.Speed,
                Op = (int)ModifierOp.Add, Value = 5f,
                SelectorSlot = 2, SelectorProjectileTemplateId = 101,
            }),
            new ContinuousModifierMO(new ContinuousModifierDTO
            {
                TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                TargetId = MobaSkillParamModifierKeys.Aoe.Ids.Radius,
                Op = (int)ModifierOp.Add, Value = 3f,
                SelectorSlot = 2, SelectorAreaTemplateId = 301,
            }),
        };
        var globalSpeed = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
            TargetId = MobaSkillParamModifierKeys.Projectile.Ids.Speed,
            Op = (int)ModifierOp.Add, Value = 1f,
        });
        var first = new SnapshotContinuous(new SnapshotConfig(upgrade, 401));
        var second = new SnapshotContinuous(new SnapshotConfig(
            new IMobaContinuousModifierSpec[] { globalSpeed }, 402));
        var emit = new MobaParameterRecord(MobaParameterStage.Emit, slot: 2,
            launcherTemplateId: 201, projectileTemplateId: 101);
        var spawn = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn, slot: 2,
            launcherTemplateId: 201, projectileTemplateId: 101);
        var otherProjectile = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn,
            slot: 2, projectileTemplateId: 102);
        var area = new MobaParameterRecord(MobaParameterStage.AreaSpawn, slot: 2,
            areaTemplateId: 301);

        Assert.True(manager.TryActivate(first));
        Assert.True(manager.TryActivate(second));
        Assert.Equal(3, modifiers.Projectile.ResolveCountPerShot(10, 1, in emit));
        Assert.Equal(16f, modifiers.Projectile.ResolveSpeed(10, 10f, in spawn));
        Assert.Equal(11f, modifiers.Projectile.ResolveSpeed(10, 10f, in otherProjectile));
        Assert.Equal(7f, modifiers.Aoe.ResolveRadius(10, 4f, in area));
        Assert.True(manager.TryPause(first));
        Assert.Equal(1, modifiers.Projectile.ResolveCountPerShot(10, 1, in emit));
        Assert.Equal(11f, modifiers.Projectile.ResolveSpeed(10, 10f, in spawn));
        Assert.Equal(4f, modifiers.Aoe.ResolveRadius(10, 4f, in area));
        Assert.True(manager.TryEnd(second));
        Assert.Equal(10f, modifiers.Projectile.ResolveSpeed(10, 10f, in spawn));
    }

    [Fact]
    public void Projectile_spawn_pattern_samples_speed_and_lifetime_for_each_emit()
    {
        using var modifiers = new MobaSkillParamModifierService();
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null, modifiers));
        var spec = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
            TargetId = 40002, Op = (int)ModifierOp.Add, Value = 5f,
            SelectorProjectileTemplateId = 101,
        });
        var lifetime = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
            TargetId = 40003, Op = (int)ModifierOp.Add, Value = 500f,
            SelectorProjectileTemplateId = 101,
        });
        var pierceHits = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
            TargetId = MobaSkillParamModifierKeys.Projectile.Ids.PierceHits,
            Op = (int)ModifierOp.Add, Value = 2f,
            SelectorProjectileTemplateId = 101,
        });
        var continuous = new SnapshotContinuous(new SnapshotConfig(
            new IMobaContinuousModifierSpec[] { spec, lifetime, pierceHits }));
        var projectile = new ProjectileMO(new ProjectileDTO { Id = 101, Speed = 10f, LifetimeMs = 500 });
        var pattern = new MobaModifierProjectileSpawnPatternProvider(
            modifiers, projectile, 201, null, 1, 0f, frameDurationMs: 50f);
        var spawn = new ProjectileSpawnParams(ownerId: 10, templateId: 101,
            launcherActorId: 30, rootActorId: 10, spawnFrame: 1,
            position: Vec3.Zero, direction: Vec3.Forward, speed: 10f,
            returnAfterFrames: 0, returnSpeed: 0f, returnStopDistance: 0f,
            lifetimeFrames: 10, maxDistance: 100f, collisionLayerMask: 0,
            ignoreCollider: default,
            hitPolicyKind: ProjectileHitPolicyKind.Pierce,
            hitPolicyParam: 2,
            hitsRemaining: 0);
        var results = new List<ProjectileSpawnParams>();

        Assert.True(manager.TryActivate(continuous));
        pattern.GetPattern(in spawn, 1).Build(in spawn, results);
        Assert.Equal(15f, results[0].Speed);
        Assert.Equal(20, results[0].LifetimeFrames);
        Assert.Equal(4, results[0].HitPolicyParam);
        results.Clear();
        Assert.True(manager.TryPause(continuous));
        pattern.GetPattern(in spawn, 2).Build(in spawn, results);
        Assert.Equal(10f, results[0].Speed);
        Assert.Equal(10, results[0].LifetimeFrames);
        Assert.Equal(2, results[0].HitPolicyParam);
        results.Clear();
        Assert.True(manager.TryResume(continuous));
        pattern.GetPattern(in spawn, 3).Build(in spawn, results);
        Assert.Equal(15f, results[0].Speed);
        Assert.Equal(20, results[0].LifetimeFrames);
        Assert.Equal(4, results[0].HitPolicyParam);
    }

    [Fact]
    public void Slot_candidates_select_highest_priority_and_revert_with_lifecycle()
    {
        using var candidates = new MobaSkillSlotCandidateService();
        using var manager = new MobaContinuousManager();
        var resolver = new Resolver(null, candidates: candidates);
        manager.OnInit(resolver);
        using var loadout = new MobaSkillLoadoutService(null);
        loadout.OnInit(resolver);
        loadout.SetLoadout(10, new[] { 100 });
        var low = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.SkillSlotCandidate,
            TargetId = 1, CandidateSkillId = 101, Priority = 10,
        });
        var high = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.SkillSlotCandidate,
            TargetId = 1, CandidateSkillId = 102, Priority = 20,
        });
        var first = new SnapshotContinuous(new SnapshotConfig(new IMobaContinuousModifierSpec[] { low }, 40));
        var second = new SnapshotContinuous(new SnapshotConfig(new IMobaContinuousModifierSpec[] { high }, 41));

        Assert.True(manager.TryActivate(first));
        Assert.True(manager.TryActivate(second));
        Assert.True(loadout.TryResolveSlot(10, 1, out var selected));
        Assert.Equal(102, selected.EffectiveSkillId);
        Assert.Equal(100, selected.BaseSkillId);
        Assert.True(manager.TryPause(second));
        Assert.True(loadout.TryGetSkillId(10, 1, out var skillId));
        Assert.Equal(101, skillId);
        Assert.True(manager.TryResume(second));
        Assert.True(manager.TryEnd(second));
        Assert.True(manager.TryEnd(first));
        Assert.True(loadout.TryGetSkillId(10, 1, out skillId));
        Assert.Equal(100, skillId);
    }

    [Fact]
    public void Recorded_override_uses_priority_independent_of_activation_order()
    {
        using var modifiers = new MobaSkillParamModifierService();
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null, modifiers));
        var high = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
            TargetId = 40002, Op = (int)ModifierOp.Override, Value = 30f, Priority = 20,
        });
        var low = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
            TargetId = 40002, Op = (int)ModifierOp.Override, Value = 20f, Priority = 10,
        });
        var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn, projectileTemplateId: 101);
        var first = new SnapshotContinuous(new SnapshotConfig(new IMobaContinuousModifierSpec[] { high }, 40));
        var second = new SnapshotContinuous(new SnapshotConfig(new IMobaContinuousModifierSpec[] { low }, 41));
        Assert.True(manager.TryActivate(first));
        Assert.True(manager.TryActivate(second));
        Assert.Equal(30f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));
        Assert.True(manager.TryEnd(first));
        Assert.Equal(20f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));
    }

    [Fact]
    public void Multi_actor_formula_snapshots_source_and_target_base_attributes()
    {
        var contexts = new Contexts();
        using var index = new ActorIdIndex(contexts);
        using var registry = new MobaActorRegistry();
        using var entities = new MobaEntityManager(null);
        var lookup = new MobaActorLookupService(index, registry, entities, contexts);
        try
        {
            var owner = CreateActor(contexts, 10, 40f);
            owner.Level = 2f;
            var source = CreateActor(contexts, 20, 120f);
            var target = CreateActor(contexts, 30, 80f);
            target.SetBase(MobaAttributeIds.MAX_HP, 500f);
            using var manager = new MobaContinuousManager();
            manager.OnInit(new Resolver(lookup));
            var spec = new ContinuousModifierMO(new ContinuousModifierDTO
            {
                TargetKind = MobaContinuousModifierTargetKind.Attribute,
                TargetId = (int)BattleAttributeType.MAX_HP,
                Op = (int)ModifierOp.Add,
                MagnitudeSourceType = 6,
                EvaluationPolicy = MobaContinuousModifierEvaluationPolicy.OnApplySnapshot,
                FormulaConstant = 5f,
                FormulaLevelCoefficient = 2.5f,
                FormulaTerms = new[]
                {
                    new ModifierFormulaTermDTO
                    {
                        ActorRole = MobaModifierAttributeSourceRole.Source,
                        AttributeTypeId = (int)BattleAttributeType.PHYSICS_ATTACK,
                        Coefficient = 0.25f,
                    },
                    new ModifierFormulaTermDTO
                    {
                        ActorRole = MobaModifierAttributeSourceRole.Target,
                        AttributeTypeId = (int)BattleAttributeType.MAX_HP,
                        Coefficient = 0.02f,
                    },
                },
            });
            var continuous = new SnapshotContinuous(new SnapshotConfig(new IMobaContinuousModifierSpec[] { spec }));
            Assert.True(manager.TryActivate(continuous));
            Assert.Equal(150f, owner.GetValue(MobaAttributeIds.MAX_HP));
            source.SetBase(MobaAttributeIds.PHYSICS_ATTACK, 200f);
            target.SetBase(MobaAttributeIds.MAX_HP, 1000f);
            Assert.True(manager.TryPause(continuous));
            Assert.Equal(100f, owner.GetValue(MobaAttributeIds.MAX_HP));
            Assert.True(manager.TryResume(continuous));
            Assert.Equal(150f, owner.GetValue(MobaAttributeIds.MAX_HP));
        }
        finally
        {
            contexts.actor.DestroyAllEntities();
        }
    }

    [Fact]
    public void Reference_candidates_select_integer_ids_by_stage_and_launcher()
    {
        using var references = new MobaReferenceCandidateService();
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null, references: references));
        var spec = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.ParameterReferenceCandidate,
            TargetId = MobaSkillParamModifierKeys.Projectile.Ids.ProjectileId,
            CandidateReferenceId = 102, Priority = 30,
            SelectorLauncherTemplateId = 201,
        });
        var continuous = new SnapshotContinuous(new SnapshotConfig(new IMobaContinuousModifierSpec[] { spec }));
        var match = new MobaParameterRecord(MobaParameterStage.LauncherCreate, launcherTemplateId: 201);
        var other = new MobaParameterRecord(MobaParameterStage.LauncherCreate, launcherTemplateId: 202);
        Assert.True(manager.TryActivate(continuous));
        Assert.Equal(102, references.Resolve(10, MobaSkillParamModifierKeys.Projectile.Ids.ProjectileId, 101, in match));
        Assert.Equal(101, references.Resolve(10, MobaSkillParamModifierKeys.Projectile.Ids.ProjectileId, 101, in other));
        var wrongStage = new MobaParameterRecord(MobaParameterStage.Hit, launcherTemplateId: 201);
        Assert.Throws<InvalidOperationException>(() => references.Resolve(10,
            MobaSkillParamModifierKeys.Projectile.Ids.ProjectileId, 101, in wrongStage));
        Assert.True(manager.TryPause(continuous));
        Assert.Equal(101, references.Resolve(10, MobaSkillParamModifierKeys.Projectile.Ids.ProjectileId, 101, in match));
        Assert.True(manager.TryResume(continuous));
        Assert.Equal(102, references.Resolve(10, MobaSkillParamModifierKeys.Projectile.Ids.ProjectileId, 101, in match));
        Assert.True(manager.TryEnd(continuous));
        Assert.Equal(101, references.Resolve(10, MobaSkillParamModifierKeys.Projectile.Ids.ProjectileId, 101, in match));
    }

    [Fact]
    public void Invalid_reference_selector_is_rejected_before_projection()
    {
        using var references = new MobaReferenceCandidateService();
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null, references: references));
        var spec = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.ParameterReferenceCandidate,
            TargetId = MobaSkillParamModifierKeys.Projectile.Ids.ProjectileId,
            CandidateReferenceId = 102,
            SelectorAreaTemplateId = 501,
        });
        var continuous = new SnapshotContinuous(new SnapshotConfig(new IMobaContinuousModifierSpec[] { spec }));
        Assert.Throws<InvalidOperationException>(() => manager.TryActivate(continuous));
        var record = new MobaParameterRecord(MobaParameterStage.LauncherCreate, launcherTemplateId: 201);
        Assert.Equal(101, references.Resolve(10, MobaSkillParamModifierKeys.Projectile.Ids.ProjectileId, 101, in record));
    }

    [Fact]
    public void Invalid_slot_candidate_and_unknown_target_kind_are_rejected()
    {
        using var candidates = new MobaSkillSlotCandidateService();
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null, candidates: candidates));
        var slotSpec = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.SkillSlotCandidate,
            TargetId = 0,
            CandidateSkillId = 101,
        });
        Assert.Throws<InvalidOperationException>(() => manager.TryActivate(
            new SnapshotContinuous(new SnapshotConfig(new IMobaContinuousModifierSpec[] { slotSpec }))));

        var unknownSpec = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = 9999,
            TargetId = 40002,
        });
        Assert.Throws<InvalidOperationException>(() => manager.TryActivate(
            new SnapshotContinuous(new SnapshotConfig(new IMobaContinuousModifierSpec[] { unknownSpec }))));
    }

    [Fact]
    public void Numeric_parameter_projection_requires_its_consumer_service()
    {
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null));
        var spec = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
            TargetId = MobaSkillParamModifierKeys.Projectile.Ids.Speed,
            Op = (int)ModifierOp.Add,
            Value = 2f,
        });
        Assert.Throws<InvalidOperationException>(() => manager.TryActivate(
            new SnapshotContinuous(new SnapshotConfig(new IMobaContinuousModifierSpec[] { spec }))));
        Assert.Equal(0, manager.ActiveCount);
        Assert.Equal(0, manager.TotalCount);
    }

    [Fact]
    public void Attribute_projection_rejects_missing_owner_without_affecting_other_domains()
    {
        var resolver = new Resolver(null);
        var projector = new MobaAttributeContinuousModifierProjector();
        projector.OnInit(resolver);
        var parameter = SpeedModifier(2f);
        projector.Apply(new Continuous(), new Projection(),
            new IMobaContinuousModifierSpec[] { parameter });

        using var manager = new MobaContinuousManager();
        manager.OnInit(resolver);
        var attribute = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.Attribute,
            TargetId = (int)BattleAttributeType.MAX_HP,
            Op = (int)ModifierOp.Add,
            Value = 10f,
        });
        var rejected = new SnapshotContinuous(new SnapshotConfig(
            new IMobaContinuousModifierSpec[] { attribute }));
        Assert.Throws<InvalidOperationException>(() => manager.TryActivate(rejected));
        Assert.True(rejected.IsTerminated);
        Assert.Equal(0, manager.ActiveCount);
        Assert.Equal(0, manager.TotalCount);
    }

    [Theory]
    [InlineData(49999, (int)ModifierOp.Add)]
    [InlineData(MobaSkillParamModifierKeys.Projectile.Ids.Speed, 99)]
    public void Invalid_numeric_declaration_rejects_only_its_continuous_source(int parameterId, int operation)
    {
        using var modifiers = new MobaSkillParamModifierService();
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null, modifiers));
        var valid = new SnapshotContinuous(new SnapshotConfig(
            new IMobaContinuousModifierSpec[] { SpeedModifier(2f) }, 401));
        Assert.True(manager.TryActivate(valid));

        var invalid = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
            TargetId = parameterId,
            Op = operation,
            Value = 5f,
        });
        var rejected = new SnapshotContinuous(new SnapshotConfig(
            new IMobaContinuousModifierSpec[] { invalid }, 402));
        Assert.Throws<InvalidOperationException>(() => manager.TryActivate(rejected));
        Assert.True(rejected.IsTerminated);
        Assert.False(valid.IsTerminated);
        Assert.Equal(1, manager.ActiveCount);
        var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);
        Assert.Equal(12f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));
    }

    [Fact]
    public void Failed_later_projection_removes_the_whole_continuous_effect()
    {
        using var modifiers = new MobaSkillParamModifierService();
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null, modifiers));
        var valid = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
            TargetId = MobaSkillParamModifierKeys.Projectile.Ids.Speed,
            Op = (int)ModifierOp.Add, Value = 5f,
        });
        var invalid = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
            TargetId = MobaSkillParamModifierKeys.Projectile.Ids.Speed,
            Op = (int)ModifierOp.Add, Value = 2f,
            SelectorAreaTemplateId = 301,
        });
        var continuous = new SnapshotContinuous(new SnapshotConfig(
            new IMobaContinuousModifierSpec[] { valid, invalid }));
        var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);

        Assert.Throws<InvalidOperationException>(() => manager.TryActivate(continuous));
        Assert.True(continuous.IsTerminated);
        Assert.Equal(0, manager.ActiveCount);
        Assert.Equal(0, manager.TotalCount);
        Assert.Empty(modifiers.CaptureRollbackSnapshot().Entries);
        Assert.Equal(10f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));
    }

    [Fact]
    public void Failed_refresh_ends_only_its_own_continuous_source()
    {
        using var modifiers = new MobaSkillParamModifierService();
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null, modifiers));
        var entries = new List<IMobaContinuousModifierSpec>
        {
            SpeedModifier(5f),
        };
        var first = new SnapshotContinuous(new SnapshotConfig(entries, 401));
        var second = new SnapshotContinuous(new SnapshotConfig(
            new IMobaContinuousModifierSpec[] { SpeedModifier(1f) }, 402));
        var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);
        Assert.True(manager.TryActivate(first));
        Assert.True(manager.TryActivate(second));
        Assert.Equal(16f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));

        entries.Add(new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
            TargetId = MobaSkillParamModifierKeys.Projectile.Ids.Speed,
            Op = (int)ModifierOp.Add, Value = 2f,
            SelectorAreaTemplateId = 301,
        }));
        Assert.Throws<InvalidOperationException>(() => manager.Reproject(first));
        Assert.True(first.IsTerminated);
        Assert.False(second.IsTerminated);
        Assert.Equal(1, manager.ActiveCount);
        Assert.Equal(11f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));
    }

    [Fact]
    public void Failed_resume_ends_the_continuous_instead_of_leaving_it_active()
    {
        using var modifiers = new MobaSkillParamModifierService();
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null, modifiers));
        var entries = new List<IMobaContinuousModifierSpec> { SpeedModifier(5f) };
        var continuous = new SnapshotContinuous(new SnapshotConfig(entries));
        var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);
        Assert.True(manager.TryActivate(continuous));
        Assert.True(manager.TryPause(continuous));
        entries.Add(new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
            TargetId = MobaSkillParamModifierKeys.Projectile.Ids.Speed,
            Op = (int)ModifierOp.Add, Value = 2f,
            SelectorAreaTemplateId = 301,
        }));

        Assert.Throws<InvalidOperationException>(() => manager.TryResume(continuous));
        Assert.True(continuous.IsTerminated);
        Assert.Equal(0, manager.ActiveCount);
        Assert.Equal(0, manager.TotalCount);
        Assert.Equal(10f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));
    }

    [Fact]
    public void Failed_curve_refresh_ends_the_continuous_and_retracts_its_projection()
    {
        using var modifiers = new MobaSkillParamModifierService();
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null, modifiers, tags: new AllowAllTags()));
        var entries = new List<IMobaContinuousModifierSpec>
        {
            new ContinuousModifierMO(new ContinuousModifierDTO
            {
                TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                TargetId = MobaSkillParamModifierKeys.Projectile.Ids.Speed,
                Op = (int)ModifierOp.Add, Value = 5f,
                MagnitudeSourceType = (int)MagnitudeSourceType.TimeDecay,
                MagnitudeDuration = 2f,
                MagnitudeCurve = new[] { 1f, 0f },
                EvaluationPolicy = MobaContinuousModifierEvaluationPolicy.Realtime,
            }),
        };
        var continuous = new SnapshotContinuous(new SnapshotConfig(entries));
        var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);
        Assert.True(manager.TryActivate(continuous));
        Assert.Equal(15f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));
        entries.Add(new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
            TargetId = MobaSkillParamModifierKeys.Projectile.Ids.Speed,
            Op = (int)ModifierOp.Add, Value = 2f,
            SelectorAreaTemplateId = 301,
        }));

        Assert.Throws<InvalidOperationException>(() => manager.Tick(0.1f));
        Assert.True(continuous.IsTerminated);
        Assert.Equal(0, manager.TotalCount);
        Assert.Equal(10f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));
    }

    [Fact]
    public void Projectile_lineage_can_select_derived_area_and_buff_parameters()
    {
        using var modifiers = new MobaSkillParamModifierService();
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null, modifiers));
        var specs = new IMobaContinuousModifierSpec[]
        {
            new ContinuousModifierMO(new ContinuousModifierDTO
            {
                TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                TargetId = MobaSkillParamModifierKeys.Aoe.Ids.Radius,
                Op = (int)ModifierOp.Add, Value = 3f,
                SelectorProjectileTemplateId = 101, SelectorAreaTemplateId = 301,
            }),
            new ContinuousModifierMO(new ContinuousModifierDTO
            {
                TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                TargetId = MobaSkillParamModifierKeys.Buff.Ids.DurationMs,
                Op = (int)ModifierOp.Add, Value = 500f,
                SelectorProjectileTemplateId = 101, SelectorBuffTemplateId = 201,
            }),
        };
        var continuous = new SnapshotContinuous(new SnapshotConfig(specs));
        Assert.True(manager.TryActivate(continuous));
        var projectileLineage = new MobaEffectLineageInput(EffectContextKind.Projectile,
            MobaExecutionKind.ProjectileHit, 10, 20, 1, 1, 1, 101);
        var forwarded = new MobaCombatExecutionContext(null, projectileLineage,
                default, default, default, 42)
            .WithEffectExecutionNode(2, 9001, true)
            .WithPayload(new object())
            .WithEffectExecutionNode(3, 9002, false);
        var area = new MobaParameterRecord(MobaParameterStage.AreaSpawn,
            projectileTemplateId: forwarded.ProjectileTemplateId, areaTemplateId: 301);
        var unrelatedArea = new MobaParameterRecord(MobaParameterStage.AreaSpawn,
            projectileTemplateId: 102, areaTemplateId: 301);
        var buff = new MobaParameterRecord(MobaParameterStage.BuffApply,
            projectileTemplateId: forwarded.ProjectileTemplateId, buffTemplateId: 201);
        var unrelatedBuff = new MobaParameterRecord(MobaParameterStage.BuffApply,
            buffTemplateId: 201);

        Assert.Equal(7f, modifiers.Aoe.ResolveRadius(10, 4f, in area));
        Assert.Equal(4f, modifiers.Aoe.ResolveRadius(10, 4f, in unrelatedArea));
        Assert.Equal(1500, modifiers.Buff.ResolveDurationMs(10, 1000, in buff));
        Assert.Equal(1000, modifiers.Buff.ResolveDurationMs(10, 1000, in unrelatedBuff));
        Assert.True(manager.TryEnd(continuous));
        Assert.Equal(4f, modifiers.Aoe.ResolveRadius(10, 4f, in area));

        var origin = new MobaGameplayOrigin(10, 20, MobaExecutionKind.EffectAction,
            0, 1, 1, 1, 1);
        Assert.Equal(101, BuffOriginContext.FromOrigin(in origin, 101).ProjectileTemplateId);
    }

    [Fact]
    public void Parameter_resolution_trace_records_the_battle_frame()
    {
        using var modifiers = new MobaSkillParamModifierService();
        modifiers.OnInit(new Resolver(null, frameTime: new FixedFrameTime(42)));
        var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);
        Assert.Equal(10f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));
        var traces = new List<MobaParameterResolutionTrace>();
        modifiers.CopyRecentResolutionsTo(traces);
        Assert.Equal(42, Assert.Single(traces).Frame);
    }

    [Fact]
    public void Parameter_resolution_reports_missing_selector_lineage_separately_from_id_mismatch()
    {
        using var modifiers = new MobaSkillParamModifierService();
        var modifier = new ModifierData
        {
            Key = MobaSkillParamModifierKeys.Projectile.Speed,
            Op = ModifierOp.Add,
            Magnitude = MagnitudeSource.Fixed(5f),
            SourceId = 94,
        };
        modifiers.AddModifier(MobaModifierOwnerRef.Actor(10), in modifier,
            new MobaParameterSelector(projectileTemplateId: 3001));
        var missing = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);
        var other = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn,
            projectileTemplateId: 3002);

        Assert.Equal(10f, modifiers.Projectile.ResolveSpeed(10, 10f, in missing));
        Assert.Equal(MobaParameterIdentityFields.ProjectileTemplateId,
            modifiers.ExplainRecorded(MobaModifierOwnerRef.Actor(10), modifier.Key,
                10f, in missing).MissingSelectorFields);
        Assert.Equal(10f, modifiers.Projectile.ResolveSpeed(10, 10f, in other));
        var traces = new List<MobaParameterResolutionTrace>();
        modifiers.CopyRecentResolutionsTo(traces);
        Assert.Equal(MobaParameterIdentityFields.ProjectileTemplateId,
            traces[0].MissingSelectorFields);
        Assert.Equal(MobaParameterIdentityFields.None, traces[1].MissingSelectorFields);
        var artifact = MobaModifierResolutionArtifactCodec.Import(
            MobaModifierResolutionArtifactCodec.Export(modifiers));
        Assert.Equal(MobaParameterIdentityFields.ProjectileTemplateId,
            artifact.RecentResolutions[0].MissingSelectorFields);
    }

    [Fact]
    public void Modifier_resolution_artifact_round_trips_recent_records_and_current_projection()
    {
        using var modifiers = new MobaSkillParamModifierService();
        modifiers.OnInit(new Resolver(null, frameTime: new FixedFrameTime(42)));
        var modifier = new ModifierData
        {
            Key = MobaSkillParamModifierKeys.Projectile.Speed,
            Op = ModifierOp.Add,
            Magnitude = MagnitudeSource.Fixed(5f),
            Priority = 7,
            SourceId = 93,
        };
        modifiers.AddModifier(MobaModifierOwnerRef.Actor(10), in modifier,
            new MobaParameterSelector(baseSkillId: 1001, projectileTemplateId: 3001));
        var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn,
            baseSkillId: 1001, effectiveSkillId: 1002, projectileTemplateId: 3001);
        for (var i = 0; i < 140; i++)
            Assert.Equal(i + 5f, modifiers.Projectile.ResolveSpeed(10, i, in record));

        var artifact = MobaModifierResolutionArtifactCodec.Import(
            MobaModifierResolutionArtifactCodec.Export(modifiers));
        Assert.Equal(MobaModifierResolutionArtifactCodec.Version, artifact.SchemaVersion);
        Assert.Equal(12, artifact.EvictedResolutionCount);
        Assert.Equal(128, artifact.RecentResolutions.Count);
        Assert.Equal(42, artifact.RecentResolutions[0].Frame);
        Assert.Equal(12f, artifact.RecentResolutions[0].BaseValue);
        Assert.Equal(17f, artifact.RecentResolutions[0].FinalValue);
        Assert.Equal(1, artifact.RecentResolutions[0].MatchedCount);
        Assert.Equal(1002, artifact.RecentResolutions[0].Identity.EffectiveSkillId);
        Assert.Equal(3001, artifact.RecentResolutions[0].Identity.ProjectileTemplateId);
        var projection = Assert.Single(artifact.CurrentProjections);
        Assert.Equal(10, projection.OwnerActorId);
        Assert.Equal(93, projection.SourceId);
        Assert.Equal(7, projection.Priority);
        Assert.Equal(5f, projection.MagnitudeBaseValue);
        Assert.Equal(1001, projection.Selector.BaseSkillId);
        Assert.Equal(3001, projection.Selector.ProjectileTemplateId);

        Assert.Throws<InvalidOperationException>(() => MobaModifierResolutionArtifactCodec.Import(
            "{\"schemaVersion\":\"unknown\",\"recentResolutions\":[],\"currentProjections\":[]}"));
    }

    [Fact]
    public void Modifier_resolution_artifact_imports_v1_without_missing_field_metadata()
    {
        const string json = "{\"schemaVersion\":\"moba-modifier-resolutions.v1\"," +
            "\"recentResolutions\":[{\"ownerActorId\":10,\"parameterId\":40002," +
            "\"identity\":{\"projectileTemplateId\":3001}}],\"currentProjections\":[]}";
        var artifact = MobaModifierResolutionArtifactCodec.Import(json);
        Assert.Equal("moba-modifier-resolutions.v1", artifact.SchemaVersion);
        var resolution = Assert.Single(artifact.RecentResolutions);
        Assert.Equal(3001, resolution.Identity.ProjectileTemplateId);
        Assert.Equal(MobaParameterIdentityFields.None, resolution.MissingSelectorFields);
    }

    private static ContinuousModifierMO SpeedModifier(float amount) =>
        new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
            TargetId = MobaSkillParamModifierKeys.Projectile.Ids.Speed,
            Op = (int)ModifierOp.Add, Value = amount,
        });

    [Fact]
    public void Production_rollback_rebuilds_parameter_projection_from_continuous()
    {
        using var modifiers = new MobaSkillParamModifierService();
        var record = new MobaParameterRecord(MobaParameterStage.ProjectileSpawn);
        using var manager = new MobaContinuousManager();
        manager.OnInit(new Resolver(null, modifiers));
        var spec = new ContinuousModifierMO(new ContinuousModifierDTO
        {
            TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
            TargetId = 40002, Op = (int)ModifierOp.Add, Value = 3f,
        });
        var continuous = new SnapshotContinuous(new SnapshotConfig(new IMobaContinuousModifierSpec[] { spec }));
        Assert.True(manager.TryActivate(continuous));
        var frame = new FrameIndex(1);
        var projectionProvider = new MobaSkillParamModifierRollbackProvider(modifiers);
        var continuousProvider = new MobaContinuousModifierCaptureRollbackProvider(manager);
        var projectionState = projectionProvider.ExportState(frame);
        var continuousState = continuousProvider.ExportState(frame);

        projectionProvider.ImportState(frame, projectionState);
        Assert.Equal(10f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));
        continuousProvider.ImportState(frame, continuousState);
        Assert.Equal(13f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));
        Assert.True(manager.TryEnd(continuous));
        Assert.Equal(10f, modifiers.Projectile.ResolveSpeed(10, 10f, in record));
    }

    private static AttributeContext CreateActor(Contexts contexts, int actorId, float attack)
    {
        var actor = contexts.actor.CreateEntity();
        actor.AddActorId(actorId);
        var attributes = new AttributeContext();
        attributes.SetBase(MobaAttributeIds.PHYSICS_ATTACK, attack);
        attributes.SetBase(MobaAttributeIds.MAX_HP, 100f);
        actor.AddAttributeGroup(attributes.GetGroupFor(MobaAttributeIds.MAX_HP), attributes);
        return attributes;
    }

    private sealed class Resolver : IWorldResolver
    {
        private readonly MobaActorLookupService _lookup;
        private readonly MobaSkillParamModifierService _skillParams;
        private readonly MobaContinuousManager _manager;
        private readonly MobaSkillSlotCandidateService _candidates;
        private readonly MobaReferenceCandidateService _references;
        private readonly IMobaEffectiveTagQueryService _tags;
        private readonly IFrameTime _frameTime;
        private readonly MobaConfigDatabase _configs;
        private readonly MobaContinuousModifierCaptureStore _captureStore = new MobaContinuousModifierCaptureStore();
        public MobaContinuousModifierCaptureStore CaptureStore => _captureStore;
        public Resolver(MobaActorLookupService lookup, MobaSkillParamModifierService skillParams = null,
            MobaContinuousManager manager = null, MobaSkillSlotCandidateService candidates = null,
            MobaReferenceCandidateService references = null, IMobaEffectiveTagQueryService tags = null,
            IFrameTime frameTime = null, MobaConfigDatabase configs = null)
        {
            _lookup = lookup;
            _skillParams = skillParams;
            _manager = manager;
            _candidates = candidates;
            _references = references;
            _tags = tags;
            _frameTime = frameTime;
            _configs = configs;
        }
        public object Resolve(Type type) => TryResolve(type, out var value) ? value : throw new InvalidOperationException();
        public T Resolve<T>() => (T)Resolve(typeof(T));
        public bool TryResolve(Type type, out object instance)
        {
            instance = type == typeof(MobaActorLookupService) ? _lookup :
                type == typeof(MobaSkillParamModifierService) ? _skillParams :
                type == typeof(MobaContinuousManager) || type == typeof(IContinuousManager) ? _manager : null;
            if (type == typeof(MobaSkillSlotCandidateService)) instance = _candidates;
            if (type == typeof(MobaReferenceCandidateService)) instance = _references;
            if (type == typeof(IMobaEffectiveTagQueryService)) instance = _tags;
            if (type == typeof(IFrameTime)) instance = _frameTime;
            if (type == typeof(MobaConfigDatabase)) instance = _configs;
            if (type == typeof(MobaContinuousModifierCaptureStore)) instance = _captureStore;
            return instance != null;
        }
        public bool TryResolve<T>(out T instance)
        {
            var found = TryResolve(typeof(T), out var value);
            instance = found ? (T)value : default;
            return found;
        }
    }

    private sealed class AllowAllTags : IMobaEffectiveTagQueryService
    {
        public GameplayTagContainer GetEffectiveTags(int ownerActorId) => null;
        public bool HasTag(int ownerActorId, GameplayTag tag, bool exact = false) => false;
        public bool HasAnyTag(int ownerActorId, GameplayTagContainer tags, bool exact = false) => false;
        public bool CanExecute(IContinuous continuous) => true;
        public bool CanActivate(int ownerActorId, ContinuousTagRequirements requirements) => true;
        public bool ShouldRemove(int ownerActorId, ContinuousTagRequirements requirements) => false;
        public MobaTagRequirementEvaluation EvaluateRequirements(int ownerActorId,
            ContinuousTagRequirements requirements) => default;
        public void MarkDirty(int ownerActorId) { }
        public bool RemoveActor(int ownerActorId) => false;
    }

    private sealed class FixedFrameTime : IFrameTime
    {
        public FixedFrameTime(int frame) => Frame = new FrameIndex(frame);
        public FrameIndex Frame { get; }
        public float DeltaTime => 0.05f;
        public float Time => Frame.Value * DeltaTime;
        public float FrameToTime(FrameIndex frame) => frame.Value * DeltaTime;
        public FrameIndex TimeToFrame(float time) => new FrameIndex((int)(time / DeltaTime));
    }

    private sealed class CleanupProbeProjector : IMobaContinuousModifierProjector
    {
        private readonly bool _failsOnClear;
        public CleanupProbeProjector(int targetKind, bool failsOnClear)
        {
            TargetKind = targetKind;
            _failsOnClear = failsOnClear;
        }
        public int TargetKind { get; }
        public int ClearCount { get; private set; }
        public void OnInit(IWorldResolver services) { }
        public void Apply(IContinuous continuous, IMobaContinuousProjectionConfig projection,
            IReadOnlyList<IMobaContinuousModifierSpec> modifiers) { }
        public void Clear(IMobaContinuousProjectionConfig projection)
        {
            ClearCount++;
            if (_failsOnClear) throw new InvalidOperationException("Injected cleanup failure.");
        }
    }

    private sealed class SnapshotContinuous : MobaContinuousRuntimeBase, IMobaTickableContinuous
    {
        private readonly SnapshotConfig _config;
        public SnapshotContinuous(SnapshotConfig config) => _config = config;
        public override IContinuousConfig Config => _config;
        public void AdvanceForTest(float seconds) => AdvanceElapsed(seconds);
        public void TickManaged(float seconds) => AdvanceElapsed(seconds);
    }

    private sealed class SnapshotConfig : MobaContinuousConfigBase
    {
        private readonly int _sourceId;
        private readonly int _ownerActorId;
        public SnapshotConfig(IReadOnlyList<IMobaContinuousModifierSpec> specs, int sourceId = 40,
            int ownerActorId = 10)
            : base(0f, null, specs)
        {
            _sourceId = sourceId;
            _ownerActorId = ownerActorId;
        }
        public override string Id => "snapshot-test";
        public override long OwnerId => _ownerActorId;
        public override int OwnerActorId => _ownerActorId;
        public override int SourceActorId => 20;
        public override int TargetActorId => 30;
        public override int ModifierSourceId => _sourceId;
        public override GameplayTagSource TagSource => GameplayTagSource.System;
    }

    private sealed class Continuous : IContinuous
    {
        public IContinuousConfig Config => null;
        public ContinuousState State => ContinuousState.Active;
        public bool IsActive => true;
        public bool IsTerminated => false;
        public bool IsPaused => false;
        public float ElapsedSeconds => 0f;
        public event Action<IContinuous, ContinuousEndReason> OnEnded;
        public void Activate() { }
        public void Pause() { }
        public void Resume() { }
        public void End(ContinuousEndReason reason) => OnEnded?.Invoke(this, reason);
        public void Abort(string reason) { }
    }

    private sealed class Projection : IMobaContinuousProjectionConfig
    {
        public int OwnerActorId => 10;
        public int SourceActorId => 20;
        public int TargetActorId => 30;
        public int ModifierSourceId => 40;
        public GameplayTagSource TagSource => GameplayTagSource.System;
    }

    private sealed class RollbackImportProbe : IRollbackStateProvider
    {
        public int Key => 1;
        public int Value { get; set; }
        public byte[] Export(FrameIndex frame) => BitConverter.GetBytes(Value);
        public void Import(FrameIndex frame, byte[] payload) => Value = BitConverter.ToInt32(payload);
    }
}

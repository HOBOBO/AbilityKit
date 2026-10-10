using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Events.Summon;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Services.Buffs;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Modifiers;
using Xunit;
using Xunit.Abstractions;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class MobaModifierRealObjectSmokeTests
{
    private readonly ITestOutputHelper _output;

    public MobaModifierRealObjectSmokeTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Mixed_projectile_load_reports_battle_cost_and_modifier_snapshot_size()
    {
        const int sourceCount = 16;
        const int projectileCount = 64;
        const int measuredFrames = 20;
        using var battle = StartBattle();
        var services = battle.RuntimeServices!;
        var registry = services.Resolve<MobaActorRegistry>();
        var continuous = services.Resolve<MobaContinuousManager>();
        var modifiers = services.Resolve<MobaSkillParamModifierService>();
        var projectiles = services.Resolve<IProjectileService>();
        var shooter = services.Resolve<MobaProjectileService>();
        var frameTime = services.Resolve<IFrameTime>();
        var caster = registry.Entries.First(entry =>
            entry.Value != null && entry.Value.hasTeam && entry.Value.hasTransform);
        var sources = new List<MobaTriggerIntervalContinuousRuntime>(sourceCount);
        for (var i = 0; i < sourceCount; i++)
        {
            var process = new ContinuousProcessMO(new ContinuousProcessDTO
            {
                Id = 800 + i,
                Modifiers = new[]
                {
                    new ContinuousModifierDTO
                    {
                        TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                        TargetId = MobaSkillParamModifierKeys.Projectile.Ids.Speed,
                        Op = (int)ModifierOp.Add,
                        Value = 0.1f,
                        SelectorProjectileTemplateId = 30020101,
                    },
                },
            });
            var source = new MobaTriggerIntervalContinuousRuntime(process,
                caster.Key, caster.Key, 800L + i, null);
            Assert.True(continuous.TryActivate(source));
            sources.Add(source);
        }

        var spawnPosition = caster.Value.transform.Value.Position + new Vec3(0f, 15f, 0f);
        var forward = Vec3.Forward;
        for (var i = 0; i < projectileCount; i++)
            Assert.True(shooter.Shoot(caster.Key, ProjectileEmitterType.Linear,
                30020101, 10f, 1000, 1000f, in spawnPosition, in forward));
        Assert.True(projectiles.ActiveCount >= projectileCount);

        var frame = new FrameIndex(frameTime.Frame.Value);
        var parameterSnapshotBytes = new MobaSkillParamModifierRollbackProvider(modifiers)
            .ExportState(frame).Length;
        var captureSnapshotBytes = new MobaContinuousModifierCaptureRollbackProvider(continuous)
            .ExportState(frame).Length;
        battle.Tick();
        var frameTimes = new double[measuredFrames];
        var frameAllocations = new long[measuredFrames];
        for (var i = 0; i < measuredFrames; i++)
        {
            var beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            battle.Tick();
            frameTimes[i] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            frameAllocations[i] = GC.GetAllocatedBytesForCurrentThread() - beforeAllocated;
        }
        var sortedFrameTimes = (double[])frameTimes.Clone();
        System.Array.Sort(sortedFrameTimes);
        _output.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = "moba-modifier-load.v2",
            runtime = Environment.Version.ToString(),
            operatingSystem = Environment.OSVersion.ToString(),
            processArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            sources = sourceCount,
            projectiles = projectileCount,
            frames = measuredFrames,
            averageBattleFrameMs = frameTimes.Average(),
            p95BattleFrameMs = sortedFrameTimes[(int)System.Math.Ceiling(measuredFrames * 0.95) - 1],
            maximumBattleFrameMs = sortedFrameTimes[measuredFrames - 1],
            battleAllocatedBytes = frameAllocations.Sum(),
            peakFrameAllocatedBytes = frameAllocations.Max(),
            parameterSnapshotBytes,
            captureSnapshotBytes,
        }));
        Assert.Equal(sourceCount, modifiers.CaptureRollbackSnapshot().Entries.Length);
        foreach (var source in sources) Assert.True(continuous.TryEnd(source));
    }

    [Fact]
    public void One_continuous_source_changes_projectile_motion_and_hit_damage_then_reverts()
    {
        using var battle = StartBattle();
        var services = battle.RuntimeServices!;
        var registry = services.Resolve<MobaActorRegistry>();
        var continuous = services.Resolve<MobaContinuousManager>();
        var projectiles = services.Resolve<IProjectileService>();
        var shooter = services.Resolve<MobaProjectileService>();
        var combatants = registry.Entries.Where(entry =>
            entry.Value != null && entry.Value.hasTeam && entry.Value.hasTransform &&
            entry.Value.hasCollisionId && entry.Value.hasResourceContainer).ToArray();
        var caster = combatants.First();
        var target = combatants.First(entry => entry.Value.team.Value != caster.Value.team.Value);
        var targetPosition = caster.Value.transform.Value.Position + new Vec3(0f, 0f, 4f);
        target.Value.ReplaceTransform(new Transform3(targetPosition,
            target.Value.transform.Value.Rotation, target.Value.transform.Value.Scale));
        var hp = target.Value.resourceContainer.Value.Map[ResourceType.Hp];
        var process = new ContinuousProcessMO(new ContinuousProcessDTO
        {
            Id = 718,
            Modifiers = new[]
            {
                new ContinuousModifierDTO
                {
                    TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                    TargetId = MobaSkillParamModifierKeys.Projectile.Ids.Speed,
                    Op = (int)ModifierOp.Add,
                    Value = 4f,
                    SelectorProjectileTemplateId = 30060301,
                },
                new ContinuousModifierDTO
                {
                    TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                    TargetId = MobaSkillParamModifierKeys.Damage.Ids.RequestedBase,
                    Op = (int)ModifierOp.Add,
                    Value = 50f,
                    SelectorProjectileTemplateId = 30060301,
                },
            },
        });
        var source = new MobaTriggerIntervalContinuousRuntime(process,
            caster.Key, caster.Key, 718L, null);
        Assert.True(continuous.TryActivate(source));

        (float displacement, float damage) ShootAndMeasure()
        {
            var events = new List<ProjectileSpawnEvent>();
            projectiles.DrainSpawnEvents(events);
            var beforeHp = hp.Current;
            var origin = caster.Value.transform.Value.Position + new Vec3(0f, 0f, 1f);
            var forward = Vec3.Forward;
            Assert.True(shooter.Shoot(caster.Key, ProjectileEmitterType.Linear,
                30060301, 30f, 30, 14f, in origin, in forward));
            events.Clear();
            projectiles.PeekSpawnEvents(events);
            var spawned = Assert.Single(events, entry => entry.OwnerId == caster.Key &&
                entry.TemplateId == 30060301);
            Assert.True(projectiles.TryGetRuntimeState(spawned.Projectile, out var before));
            battle.Tick();
            Assert.True(projectiles.TryGetRuntimeState(spawned.Projectile, out var after));
            for (var i = 0; i < 11 && hp.Current == beforeHp; i++) battle.Tick();
            Assert.True(hp.Current < beforeHp);
            return (after.Position.Z - before.Position.Z, (float)(beforeHp - hp.Current));
        }

        var modified = ShootAndMeasure();
        Assert.True(continuous.TryEnd(source));
        var baseline = ShootAndMeasure();
        Assert.True(modified.displacement > baseline.displacement);
        Assert.True(modified.damage > baseline.damage);
    }

    [Fact]
    public void Projectile_collision_preserves_template_selector_through_hit_damage()
    {
        using var battle = StartBattle();
        var services = battle.RuntimeServices!;
        var registry = services.Resolve<MobaActorRegistry>();
        var continuous = services.Resolve<MobaContinuousManager>();
        var shooter = services.Resolve<MobaProjectileService>();
        var modifiers = services.Resolve<MobaSkillParamModifierService>();
        var combatants = registry.Entries.Where(entry =>
            entry.Value != null && entry.Value.hasTeam && entry.Value.hasTransform &&
            entry.Value.hasCollisionId && entry.Value.hasResourceContainer).ToArray();
        var caster = combatants.First();
        var target = combatants.First(entry => entry.Value.team.Value != caster.Value.team.Value);
        var targetPosition = caster.Value.transform.Value.Position + new Vec3(0f, 0f, 4f);
        target.Value.ReplaceTransform(new Transform3(targetPosition,
            target.Value.transform.Value.Rotation, target.Value.transform.Value.Scale));
        var hp = target.Value.resourceContainer.Value.Map[ResourceType.Hp];
        var process = new ContinuousProcessMO(new ContinuousProcessDTO
        {
            Id = 716,
            Modifiers = new[]
            {
                new ContinuousModifierDTO
                {
                    TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                    TargetId = MobaSkillParamModifierKeys.Damage.Ids.RequestedBase,
                    Op = (int)ModifierOp.Add,
                    Value = 50f,
                    SelectorProjectileTemplateId = 30060301,
                },
            },
        });
        var source = new MobaTriggerIntervalContinuousRuntime(process,
            caster.Key, caster.Key, 716L, null);
        Assert.True(continuous.TryActivate(source));

        float ShootAndMeasure()
        {
            var before = hp.Current;
            var origin = caster.Value.transform.Value.Position + new Vec3(0f, 0f, 1f);
            var forward = Vec3.Forward;
            Assert.True(shooter.Shoot(caster.Key, ProjectileEmitterType.Linear,
                30060301, 30f, 30, 14f, in origin, in forward));
            for (var i = 0; i < 12 && hp.Current == before; i++) battle.Tick();
            Assert.True(hp.Current < before, "Published projectile did not hit the target.");
            return (float)(before - hp.Current);
        }

        var modifiedLoss = ShootAndMeasure();
        var resolutions = new List<MobaParameterResolutionTrace>();
        modifiers.CopyRecentResolutionsTo(resolutions);
        Assert.Contains(resolutions, entry =>
            entry.Key.Equals(MobaSkillParamModifierKeys.Damage.RequestedBase) &&
            entry.Record.Stage == MobaParameterStage.Hit &&
            entry.Record.ProjectileTemplateId == 30060301 && entry.MatchedCount == 1);
        Assert.True(continuous.TryEnd(source));
        var baseLoss = ShootAndMeasure();
        Assert.True(modifiedLoss > baseLoss);
    }

    [Fact]
    public void Projectile_hit_replays_same_damage_and_modifier_resolution_after_rollback()
    {
        using var battle = StartBattle();
        var services = battle.RuntimeServices!;
        var registry = services.Resolve<MobaActorRegistry>();
        var continuous = services.Resolve<MobaContinuousManager>();
        var shooter = services.Resolve<MobaProjectileService>();
        var modifiers = services.Resolve<MobaSkillParamModifierService>();
        var frameTime = services.Resolve<IFrameTime>();
        var combatants = registry.Entries.Where(entry =>
            entry.Value != null && entry.Value.hasTeam && entry.Value.hasTransform &&
            entry.Value.hasCollisionId && entry.Value.hasResourceContainer).ToArray();
        var caster = combatants.First();
        var target = combatants.First(entry => entry.Value.team.Value != caster.Value.team.Value);
        var targetPosition = caster.Value.transform.Value.Position + new Vec3(0f, 0f, 4f);
        target.Value.ReplaceTransform(new Transform3(targetPosition,
            target.Value.transform.Value.Rotation, target.Value.transform.Value.Scale));
        AbilityKit.Demo.Moba.Components.ResourceState Hp() =>
            target.Value.resourceContainer.Value.Map[ResourceType.Hp];
        var process = new ContinuousProcessMO(new ContinuousProcessDTO
        {
            Id = 717,
            Modifiers = new[]
            {
                new ContinuousModifierDTO
                {
                    TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                    TargetId = MobaSkillParamModifierKeys.Damage.Ids.RequestedBase,
                    Op = (int)ModifierOp.Add,
                    Value = 50f,
                    SelectorProjectileTemplateId = 30060301,
                },
            },
        });
        var source = new MobaTriggerIntervalContinuousRuntime(process,
            caster.Key, caster.Key, 717L, null);
        Assert.True(continuous.TryActivate(source));

        var providers = new RollbackRegistry();
        providers.Register(new MobaActorTransformRollbackProvider(registry));
        providers.Register(new MobaActorHpRollbackProvider(registry));
        providers.Register(new MobaActorResourceRollbackProvider(registry));
        if (frameTime is FrameTime mutableFrameTime)
            providers.Register(new FrameTimeRollbackStateProvider(mutableFrameTime));
        var parameterProvider = new MobaSkillParamModifierRollbackProvider(modifiers);
        providers.Register(parameterProvider);
        var captureProvider = new MobaContinuousModifierCaptureRollbackProvider(continuous);
        providers.Register(captureProvider);
        var rollback = new RollbackCoordinator(providers, new RollbackSnapshotRingBuffer(2));
        var frame = new FrameIndex(frameTime.Frame.Value);
        var snapshot = rollback.Capture(frame);
        var originalHp = Hp().Current;
        var parameterState = parameterProvider.ExportState(frame);
        var captureState = captureProvider.ExportState(frame);

        float ShootAndMeasure()
        {
            var before = Hp().Current;
            var origin = caster.Value.transform.Value.Position + new Vec3(0f, 0f, 1f);
            var forward = Vec3.Forward;
            Assert.True(shooter.Shoot(caster.Key, ProjectileEmitterType.Linear,
                30060301, 30f, 30, 14f, in origin, in forward));
            for (var i = 0; i < 12 && Hp().Current == before; i++) battle.Tick();
            Assert.True(Hp().Current < before, "Published projectile did not hit the target.");
            return (float)(before - Hp().Current);
        }

        var firstLoss = ShootAndMeasure();
        MobaParameterResolutionTrace LastHitResolution()
        {
            var traces = new List<MobaParameterResolutionTrace>();
            modifiers.CopyRecentResolutionsTo(traces);
            return traces.Last(entry => entry.Key.Equals(MobaSkillParamModifierKeys.Damage.RequestedBase) &&
                entry.Record.Stage == MobaParameterStage.Hit &&
                entry.Record.ProjectileTemplateId == 30060301);
        }
        var firstResolution = LastHitResolution();
        rollback.Restore(snapshot);
        Assert.Equal(originalHp, Hp().Current);
        Assert.True(parameterState.SequenceEqual(parameterProvider.ExportState(frame)));
        Assert.True(captureState.SequenceEqual(captureProvider.ExportState(frame)));
        Assert.False(source.IsTerminated);
        var replayLoss = ShootAndMeasure();
        var replayResolution = LastHitResolution();
        Assert.True(firstLoss > 0f && replayLoss > 0f);
        Assert.Equal(firstLoss, replayLoss);
        Assert.Equal(1, firstResolution.MatchedCount);
        Assert.Equal(firstResolution.BaseValue, replayResolution.BaseValue);
        Assert.Equal(firstResolution.FinalValue, replayResolution.FinalValue);
        Assert.True(continuous.TryEnd(source));
    }

    [Fact]
    public void Continuous_requested_damage_modifier_changes_real_hp_and_reverts_on_end()
    {
        using var battle = StartBattle();
        var services = battle.RuntimeServices!;
        var registry = services.Resolve<MobaActorRegistry>();
        var continuous = services.Resolve<MobaContinuousManager>();
        var effects = services.Resolve<MobaEffectExecutionService>();
        var combatants = registry.Entries.Where(entry =>
            entry.Value != null && entry.Value.hasTeam && entry.Value.hasResourceContainer).ToArray();
        var caster = combatants.First();
        var target = combatants.First(entry => entry.Value.team.Value != caster.Value.team.Value);
        var hp = target.Value.resourceContainer.Value.Map[ResourceType.Hp];
        var process = new ContinuousProcessMO(new ContinuousProcessDTO
        {
            Id = 714,
            Modifiers = new[]
            {
                new ContinuousModifierDTO
                {
                    TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                    TargetId = MobaSkillParamModifierKeys.Damage.Ids.RequestedBase,
                    Op = (int)ModifierOp.Add,
                    Value = 50f,
                },
            },
        });
        var source = new MobaTriggerIntervalContinuousRuntime(process,
            caster.Key, caster.Key, 714L, null);
        Assert.True(continuous.TryActivate(source));

        var beforeModified = hp.Current;
        Assert.True(effects.ExecuteRulePlan(10060313, new ActorPayload(caster.Key, target.Key)));
        var modifiedLoss = beforeModified - hp.Current;
        Assert.True(modifiedLoss > 0);
        Assert.True(continuous.TryEnd(source));

        var beforeBase = hp.Current;
        Assert.True(effects.ExecuteRulePlan(10060313, new ActorPayload(caster.Key, target.Key)));
        var baseLoss = beforeBase - hp.Current;
        Assert.True(baseLoss > 0);
        Assert.True(modifiedLoss > baseLoss);
    }

    [Fact]
    public void Continuous_emit_count_modifier_resamples_after_launcher_creation()
    {
        using var battle = StartBattle();
        var services = battle.RuntimeServices!;
        var registry = services.Resolve<MobaActorRegistry>();
        var continuous = services.Resolve<MobaContinuousManager>();
        var configs = services.Resolve<MobaConfigDatabase>();
        var projectiles = services.Resolve<IProjectileService>();
        var shooter = services.Resolve<MobaProjectileService>();
        var frameTime = services.Resolve<IFrameTime>();
        var caster = registry.Entries.First(entry =>
            entry.Value != null && entry.Value.hasTeam && entry.Value.hasTransform);
        var launcher = configs.GetProjectileLauncher(2);
        var projectile = configs.GetProjectile(30020101);
        Assert.NotNull(launcher);
        Assert.NotNull(projectile);
        var process = new ContinuousProcessMO(new ContinuousProcessDTO
        {
            Id = 715,
            Modifiers = new[]
            {
                new ContinuousModifierDTO
                {
                    TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                    TargetId = MobaSkillParamModifierKeys.Emitter.Ids.CountPerShot,
                    Op = (int)ModifierOp.Add,
                    Value = 2f,
                },
            },
        });
        var source = new MobaTriggerIntervalContinuousRuntime(process,
            caster.Key, caster.Key, 715L, null);
        Assert.True(continuous.TryActivate(source));

        var spawnPosition = caster.Value.transform.Value.Position + new Vec3(0f, 15f, 0f);
        var forward = Vec3.Forward;
        Assert.True(shooter.Launch(caster.Key, launcher, projectile, 1, 0f, 1200,
            in spawnPosition, in forward, default));
        var launcherEntity = Assert.Single(registry.Entries.Where(entry =>
            entry.Value != null && entry.Value.hasProjectileLauncher &&
            entry.Value.projectileLauncher.LauncherId == launcher.Id)).Value;
        var intervalFrames = launcherEntity.projectileLauncher.IntervalFrames;
        Assert.True(intervalFrames > 0);

        var spawned = new List<ProjectileSpawnEvent>();
        var firstFrame = frameTime.Frame.Value + 1;
        projectiles.Tick(firstFrame, frameTime.DeltaTime);
        projectiles.DrainSpawnEvents(spawned);
        Assert.Equal(3, spawned.Count(e => e.LauncherActorId == launcherEntity.actorId.Value));
        Assert.True(continuous.TryEnd(source));

        spawned.Clear();
        for (var frame = firstFrame + 1; frame <= firstFrame + intervalFrames; frame++)
        {
            projectiles.Tick(frame, frameTime.DeltaTime);
            projectiles.DrainSpawnEvents(spawned);
        }
        Assert.Single(spawned, e => e.LauncherActorId == launcherEntity.actorId.Value);
    }

    [Fact]
    public void Continuous_radius_modifier_changes_real_area_and_reverts_on_end()
    {
        using var battle = StartBattle();
        var services = battle.RuntimeServices!;
        var registry = services.Resolve<MobaActorRegistry>();
        var continuous = services.Resolve<MobaContinuousManager>();
        var effects = services.Resolve<MobaEffectExecutionService>();
        var areas = services.Resolve<MobaAreaRuntimeService>();
        var caster = registry.Entries.First(entry =>
            entry.Value != null && entry.Value.hasTeam && entry.Value.hasTransform);
        var process = new ContinuousProcessMO(new ContinuousProcessDTO
        {
            Id = 712,
            Modifiers = new[]
            {
                new ContinuousModifierDTO
                {
                    TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                    TargetId = MobaSkillParamModifierKeys.Aoe.Ids.Radius,
                    Op = (int)ModifierOp.Add,
                    Value = 1.5f,
                },
            },
        });
        var source = new MobaTriggerIntervalContinuousRuntime(process,
            caster.Key, caster.Key, 712L, null);
        Assert.True(continuous.TryActivate(source));

        var active = new List<MobaAreaRuntimeInfo>();
        Assert.True(effects.ExecuteRulePlan(10010302, new ActorPayload(caster.Key)));
        Assert.True(areas.TryGetAreas(active, caster.Key, 40010301));
        var modified = Assert.Single(active);
        Assert.Equal(6f, modified.Radius, 3);
        Assert.True(continuous.TryEnd(source));

        Assert.True(effects.ExecuteRulePlan(10010302, new ActorPayload(caster.Key)));
        Assert.True(areas.TryGetAreas(active, caster.Key, 40010301));
        Assert.Equal(2, active.Count);
        Assert.Contains(active, area => System.MathF.Abs(area.Radius - 4.5f) < 0.001f);
    }

    [Fact]
    public void Continuous_interval_modifier_changes_launcher_rounds_and_real_emissions()
    {
        using var battle = StartBattle();
        var services = battle.RuntimeServices!;
        var registry = services.Resolve<MobaActorRegistry>();
        var continuous = services.Resolve<MobaContinuousManager>();
        var configs = services.Resolve<MobaConfigDatabase>();
        var projectiles = services.Resolve<IProjectileService>();
        var shooter = services.Resolve<MobaProjectileService>();
        var frameTime = services.Resolve<IFrameTime>();
        var caster = registry.Entries.First(entry =>
            entry.Value != null && entry.Value.hasTeam && entry.Value.hasTransform);
        var launcher = configs.GetProjectileLauncher(2);
        var projectile = configs.GetProjectile(30020101);
        Assert.NotNull(launcher);
        Assert.NotNull(projectile);
        var process = new ContinuousProcessMO(new ContinuousProcessDTO
        {
            Id = 713,
            Modifiers = new[]
            {
                new ContinuousModifierDTO
                {
                    TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                    TargetId = MobaSkillParamModifierKeys.Emitter.Ids.IntervalMs,
                    Op = (int)ModifierOp.Add,
                    Value = -400f,
                },
            },
        });
        var source = new MobaTriggerIntervalContinuousRuntime(process,
            caster.Key, caster.Key, 713L, null);
        Assert.True(continuous.TryActivate(source));

        var spawnPosition = caster.Value.transform.Value.Position + new Vec3(0f, 15f, 0f);
        var forward = Vec3.Forward;
        Assert.True(shooter.Launch(caster.Key, launcher, projectile, 1, 0f, 200,
            in spawnPosition, in forward, default));
        var launcherEntity = Assert.Single(registry.Entries.Where(entry =>
            entry.Value != null && entry.Value.hasProjectileLauncher &&
            entry.Value.projectileLauncher.LauncherId == launcher.Id)).Value;
        var intervalFrames = System.Math.Max(1,
            (int)System.MathF.Round(100f / (frameTime.DeltaTime * 1000f)));
        Assert.Equal(intervalFrames, launcherEntity.projectileLauncher.IntervalFrames);
        Assert.Equal(3, launcherEntity.projectileLauncher.TotalCount);

        var spawned = new List<ProjectileSpawnEvent>();
        for (var frame = frameTime.Frame.Value + 1;
             frame <= frameTime.Frame.Value + 1 + 2 * intervalFrames; frame++)
        {
            projectiles.Tick(frame, frameTime.DeltaTime);
            projectiles.DrainSpawnEvents(spawned);
        }
        Assert.Equal(3, spawned.Count(e => e.LauncherActorId == launcherEntity.actorId.Value));
        Assert.True(continuous.TryEnd(source));

        Assert.True(shooter.Launch(caster.Key, launcher, projectile, 1, 0f, 200,
            in spawnPosition, in forward, default));
        var restoredLauncher = Assert.Single(registry.Entries, entry =>
            entry.Value != null && entry.Value.hasProjectileLauncher &&
            entry.Value.projectileLauncher.LauncherId == launcher.Id &&
            entry.Key != launcherEntity.actorId.Value).Value;
        Assert.Equal(1, restoredLauncher.projectileLauncher.TotalCount);
        Assert.Equal(0, restoredLauncher.projectileLauncher.IntervalFrames);
    }

    [Fact]
    public void Continuous_lifetime_modifier_changes_real_summon_and_reverts_on_end()
    {
        using var battle = StartBattle();
        var services = battle.RuntimeServices!;
        var registry = services.Resolve<MobaActorRegistry>();
        var continuous = services.Resolve<MobaContinuousManager>();
        var summons = services.Resolve<MobaSummonService>();
        var frameTime = services.Resolve<IFrameTime>();
        var caster = registry.Entries.First(entry =>
            entry.Value != null && entry.Value.hasTeam && entry.Value.hasTransform);
        var process = new ContinuousProcessMO(new ContinuousProcessDTO
        {
            Id = 711,
            Modifiers = new[]
            {
                new ContinuousModifierDTO
                {
                    TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                    TargetId = MobaSkillParamModifierKeys.Summon.Ids.LifetimeMs,
                    Op = (int)ModifierOp.Add,
                    Value = 1000f,
                },
            },
        });
        var source = new MobaTriggerIntervalContinuousRuntime(process,
            caster.Key, caster.Key, 711L, null);
        Assert.True(continuous.TryActivate(source));

        var spawnPosition = caster.Value.transform.Value.Position + new Vec3(2f, 0f, 0f);
        var forward = Vec3.Forward;
        var sourceContext = default(SummonSourceContext);
        Assert.True(summons.TrySummon(caster.Key, 1, in spawnPosition, in forward,
            in sourceContext, out var firstId, durationOverrideMs: 1000));
        Assert.True(registry.TryGet(firstId, out var first));
        Assert.Equal((long)System.MathF.Round(frameTime.Time * 1000f) + 2000L,
            first.lifetime.EndTimeMs);

        Assert.True(summons.TryDespawn(firstId, SummonDespawnReason.ManualRemove));
        for (var i = 0; i < 8 && registry.TryGet(firstId, out _); i++)
            battle.Tick();
        Assert.False(registry.TryGet(firstId, out _));
        Assert.True(continuous.TryEnd(source));

        Assert.True(summons.TrySummon(caster.Key, 1, in spawnPosition, in forward,
            in sourceContext, out var secondId, durationOverrideMs: 1000));
        Assert.True(registry.TryGet(secondId, out var second));
        Assert.Equal((long)System.MathF.Round(frameTime.Time * 1000f) + 1000L,
            second.lifetime.EndTimeMs);
    }

    [Fact]
    public void Continuous_duration_modifier_changes_real_buff_and_reverts_on_end()
    {
        using var battle = StartBattle();
        var services = battle.RuntimeServices!;
        var registry = services.Resolve<MobaActorRegistry>();
        var continuous = services.Resolve<MobaContinuousManager>();
        var buffs = services.Resolve<MobaBuffService>();
        var caster = registry.Entries.First(entry =>
            entry.Value != null && entry.Value.hasTeam && entry.Value.hasTransform);
        var process = new ContinuousProcessMO(new ContinuousProcessDTO
        {
            Id = 710,
            Modifiers = new[]
            {
                new ContinuousModifierDTO
                {
                    TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                    TargetId = MobaSkillParamModifierKeys.Buff.Ids.DurationMs,
                    Op = (int)ModifierOp.Add,
                    Value = 1000f,
                },
            },
        });
        var source = new MobaTriggerIntervalContinuousRuntime(process,
            caster.Key, caster.Key, 710L, null);
        Assert.True(continuous.TryActivate(source));

        Assert.True(buffs.ApplyBuffImmediate(caster.Key, 1, caster.Key, 1000));
        var applied = Assert.Single(caster.Value.buffs.Active, buff => buff.BuffId == 1);
        Assert.Equal(2f, applied.Remaining, 3);
        Assert.True(buffs.RemoveBuffImmediate(caster.Key, 1, caster.Key,
            MobaExecutionEndReason.Completed));
        Assert.True(continuous.TryEnd(source));

        Assert.True(buffs.ApplyBuffImmediate(caster.Key, 1, caster.Key, 1000));
        var restored = Assert.Single(caster.Value.buffs.Active, buff => buff.BuffId == 1);
        Assert.Equal(1f, restored.Remaining, 3);
    }

    [Fact]
    public void Existing_buff_refresh_resamples_duration_after_modifier_source_ends()
    {
        using var battle = StartBattle();
        var services = battle.RuntimeServices!;
        var registry = services.Resolve<MobaActorRegistry>();
        var continuous = services.Resolve<MobaContinuousManager>();
        var buffs = services.Resolve<MobaBuffService>();
        var caster = registry.Entries.First(entry =>
            entry.Value != null && entry.Value.hasTeam && entry.Value.hasTransform);
        var process = new ContinuousProcessMO(new ContinuousProcessDTO
        {
            Id = 718,
            Modifiers = new[]
            {
                new ContinuousModifierDTO
                {
                    TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                    TargetId = MobaSkillParamModifierKeys.Buff.Ids.DurationMs,
                    Op = (int)ModifierOp.Add,
                    Value = 1000f,
                    SelectorBuffTemplateId = 10020001,
                },
            },
        });
        var source = new MobaTriggerIntervalContinuousRuntime(process,
            caster.Key, caster.Key, 718L, null);
        Assert.True(continuous.TryActivate(source));
        Assert.True(buffs.ApplyBuffImmediate(caster.Key, 10020001, caster.Key, 1000));
        var applied = Assert.Single(caster.Value.buffs.Active, buff => buff.BuffId == 10020001);
        Assert.Equal(2f, applied.Remaining, 3);

        Assert.True(continuous.TryEnd(source));
        Assert.Equal(2f, applied.Remaining, 3);
        Assert.True(buffs.ApplyBuffImmediate(caster.Key, 10020001, caster.Key, 1000));
        var refreshed = Assert.Single(caster.Value.buffs.Active, buff => buff.BuffId == 10020001);
        Assert.Equal(1f, refreshed.Remaining, 3);
    }

    [Fact]
    public void Continuous_speed_modifier_changes_projectile_motion_and_reverts_on_end()
    {
        using var battle = StartBattle();
        var services = battle.RuntimeServices!;
        var registry = services.Resolve<MobaActorRegistry>();
        var continuous = services.Resolve<MobaContinuousManager>();
        var projectiles = services.Resolve<IProjectileService>();
        var shooter = services.Resolve<MobaProjectileService>();
        var frameTime = services.Resolve<IFrameTime>();
        var caster = registry.Entries.First(entry =>
            entry.Value != null && entry.Value.hasTeam && entry.Value.hasTransform);
        var process = new ContinuousProcessMO(new ContinuousProcessDTO
        {
            Id = 709,
            Modifiers = new[]
            {
                new ContinuousModifierDTO
                {
                    TargetKind = MobaContinuousModifierTargetKind.SkillParameter,
                    TargetId = MobaSkillParamModifierKeys.Projectile.Ids.Speed,
                    Op = (int)ModifierOp.Add,
                    Value = 2.5f,
                },
            },
        });
        var source = new MobaTriggerIntervalContinuousRuntime(process,
            caster.Key, caster.Key, 709L, null);
        Assert.True(continuous.TryActivate(source));

        var spawnPosition = caster.Value.transform.Value.Position + new Vec3(0f, 15f, 0f);
        var forward = Vec3.Forward;
        float ShootAndStep(int frame)
        {
            var events = new List<ProjectileSpawnEvent>();
            projectiles.DrainSpawnEvents(events);
            Assert.True(shooter.Shoot(caster.Key, ProjectileEmitterType.Linear,
                30020101, 10f, 1000, 100f, in spawnPosition, in forward));
            events.Clear();
            projectiles.PeekSpawnEvents(events);
            var spawned = Assert.Single(events, e => e.OwnerId == caster.Key &&
                e.TemplateId == 30020101);
            Assert.True(projectiles.TryGetRuntimeState(spawned.Projectile, out var before));
            projectiles.Tick(frame, frameTime.DeltaTime);
            Assert.True(projectiles.TryGetRuntimeState(spawned.Projectile, out var after));
            return after.Position.Z - before.Position.Z;
        }

        var modifiedDistance = ShootAndStep(frameTime.Frame.Value + 1);
        Assert.True(continuous.TryEnd(source));
        var baseDistance = ShootAndStep(frameTime.Frame.Value + 2);
        Assert.Equal(12.5f * frameTime.DeltaTime, modifiedDistance, 3);
        Assert.Equal(10f * frameTime.DeltaTime, baseDistance, 3);
    }

    private static ConsoleBattleBootstrapper StartBattle()
    {
        var battle = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault());
        battle.Initialize();
        battle.Start();
        for (var i = 0; i < 8 && battle.Context.EcsWorld == null; i++)
            battle.Tick();
        battle.SetupBattle();
        for (var i = 0; i < 10; i++)
            battle.Tick();
        Assert.NotNull(battle.RuntimeServices);
        return battle;
    }

    private sealed class ActorPayload : IMobaActorContextProvider
    {
        private readonly int _actorId;
        private readonly int _targetId;
        public ActorPayload(int actorId, int targetId = 0)
        {
            _actorId = actorId;
            _targetId = targetId;
        }
        public bool TryGetSourceActorId(out int actorId)
        {
            actorId = _actorId;
            return actorId > 0;
        }
        public bool TryGetTargetActorId(out int actorId)
        {
            actorId = _targetId;
            return actorId > 0;
        }
    }
}

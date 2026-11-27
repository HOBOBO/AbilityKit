using System.Reflection;
using System.Security.Cryptography;
using MemoryPack;
using Newtonsoft.Json;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Combat.Projectile;
using AbilityKit.Combat.MotionSystem.Core;
using AbilityKit.Combat.MotionSystem.Generic;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Acceptance.LiveSim;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Services.Motion;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Trace;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Eventing;
using Xunit;
using Xunit.Abstractions;

namespace AbilityKit.Demo.Moba.Tests.StateSync;

/// <summary>
/// 在正式 Console 世界组合已发布内容，验证帧末结算及正式回滚注册表。
/// </summary>
[Collection("Moba lifecycle preparation")]
public sealed partial class MobaProjectileAreaMotionCompositionTests(ITestOutputHelper output)
{
    [Fact]
    public void Ordinary_locomotion_moves_and_cleans_up_without_rollback_coordinator()
    {
        using var b = new Battle(1001, "locomotion-without-rollback", enableRollback: false);
        var actor = b.Actor(b.Caster);
        var pipeline = actor.motion.Pipeline;
        var source = Assert.Single(pipeline.CaptureSourceOrder().OfType<LocomotionMotionSource>());
        actor.ReplaceMoveInput(1, 0);
        var before = b.Setup.GetActorPosition(b.Caster);
        b.Setup.Tick(1);
        Assert.Same(source, Assert.Single(pipeline.CaptureSourceOrder().OfType<LocomotionMotionSource>()));
        Assert.True(b.Setup.GetActorPosition(b.Caster).X > before.X);
        Assert.Equal(new Vec3(1, 0, 0), source.Input);
        actor.ReplaceMoveInput(0, 0);
        b.Setup.Tick(1);
        Assert.Equal(Vec3.Zero, source.Input);
        actor.RemoveMoveInput();
        b.Setup.Tick(1);
        Assert.DoesNotContain(source, pipeline.CaptureSourceOrder());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Active_repeat_launcher_restores_command_proven_projectiles_and_replays_twice(bool afterFirstEmit)
    {
        using var b = new Battle(1001, "active-schedule-" + afterFirstEmit);
        b.SpawnTestRepeatLauncher();
        if (afterFirstEmit) b.Setup.Tick(1);
        b.RejectMismatchedScheduleIdentity();
        b.VerifyActiveScheduleReplay(output, afterFirstEmit);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Last_emit_boundary_restores_schedule_presence_and_replays_twice(bool afterLastEmit)
    {
        using var b = new Battle(1001, "last-emit-" + afterLastEmit);
        b.SpawnTestRepeatLauncher(durationMs: 500, endPipeline: true);
        b.VerifyLastEmitReplay(afterLastEmit, output);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 30060010)]
    [InlineData(true, 30060010)]
    public void Last_projectile_exit_restores_cleaned_launcher_and_replays_twice(bool beforeEmit, int processId)
    {
        using var b = new Battle(1001, "last-projectile-exit-" + beforeEmit + "-" + processId);
        b.SpawnTestRepeatLauncher(durationMs: 0, endPipeline: true, processId: processId);
        b.VerifyLastExitAndSettledReplay(output, beforeEmit: beforeEmit);
    }

    [Fact]
    public void Earlier_projectile_exit_restores_missing_target_while_launcher_and_source_still_live()
    {
        using var b = new Battle(1001, "earlier-projectile-exit");
        b.SpawnTestRepeatLauncher(durationMs: 500, endPipeline: true);
        b.VerifyEarlierExitReplay(output);
    }

    [Fact]
    public void Unit_hit_keeps_source_until_exit_and_settled_checkpoint_does_not_hit_again()
    {
        using var b = new Battle(1001, "projectile-hit-cleanup");
        var target = b.Spawn("hit-target", 2, 200, 206);
        b.Setup.Tick(2);
        b.SpawnTestRepeatLauncher(durationMs: 0, endPipeline: true, bulletsPerShot: 1, fanAngleDeg: 0);
        b.VerifyLastExitAndSettledReplay(output, expectedCount: 1, hitTarget: target);
    }

    [Theory]
    [InlineData("missing-command")]
    [InlineData("old-command-key")]
    [InlineData("epoch")]
    [InlineData("future-order")]
    [InlineData("no-creation-proof")]
    [InlineData("expired")]
    [InlineData("unsupported-type")]
    [InlineData("unsupported-version")]
    [InlineData("missing-destruction-tombstone")]
    [InlineData("wrong-tombstone-version")]
    [InlineData("stale-command-frame")]
    [InlineData("unknown-extra")]
    [InlineData("wrong-kind")]
    [InlineData("wrong-kind-tombstone")]
    [InlineData("target-missing")]
    [InlineData("target-version")]
    [InlineData("lifecycle-roster")]
    [InlineData("combat-roster")]
    [InlineData("extra-source")]
    [InlineData("extra-retain")]
    public void Projectile_roster_projection_rejects_invalid_evidence_before_any_import(string defect)
    {
        using var b = new Battle(1001, "roster-proof-" + defect);
        b.SpawnTestRepeatLauncher();
        b.VerifyInvalidRosterProof(defect);
    }

    [Theory]
    [InlineData(0, 1001)]
    [InlineData(0, 3005)]
    [InlineData(1, 1001)]
    [InlineData(1, 3005)]
    [InlineData(5, 1001)]
    [InlineData(5, 3005)]
    public void Arrival_and_active_radial_motion_recovery_replays_full_lifecycle(
        int checkpointOffset, int targetHero)
        => VerifyArrivalReplay(checkpointOffset, targetHero);

    private void VerifyArrivalReplay(int checkpointOffset, int targetHero)
    {
        using var b = new Battle(3005, "arrival-" + checkpointOffset + "-" + targetHero);
        var left = b.Spawn("left", 2, 98, 106, targetHero);
        var right = b.Spawn("right", 2, 102, 106, targetHero);
        var center = b.Spawn("center", 2, 100, 106, targetHero);
        var ally = b.Spawn("ally", 1, 101, 106, targetHero);
        var outside = b.Spawn("outside", 2, 110, 106, targetHero);
        var targets = new[] { left, right, center, ally, outside };
        b.Set(b.Caster, "MAGIC_ATTACK", 100);
        b.Setup.Tick(2);
        var hp = targets.Select(b.Setup.GetActorHp).ToArray();
        var positions = targets.Select(b.Setup.GetActorPosition).ToArray();
        var areaEvents = new List<AreaEventArgs>();
        using var observer = b.ObserveAreas(areaEvents);
        b.Cast(4, new Vec3(100, 0, 106));
        if (checkpointOffset == 0)
        {
            b.Until(b.HasLauncher);
            b.RejectMismatchedLauncherTemplates();
            b.AssertArrivalMotionRejectsReplay(() => b.Until(() => b.Count(MobaExecutionKind.DamageApply, 30050501) != 0));
        }
        b.Until(() => b.Count(MobaExecutionKind.DamageApply, 30050501) != 0);

        // Motion runs in Execute; arrival effects run later in ProjectileSync.
        // All three query targets settle on the same frame, with no movement yet.
        Assert.Equal(3, b.Count(MobaExecutionKind.DamageApply, 30050501));
        for (var i = 0; i < targets.Length; i++)
        {
            Assert.Equal(positions[i], b.Setup.GetActorPosition(targets[i]));
            Assert.Equal(i < 3 ? 280f : 0f, hp[i] - b.Setup.GetActorHp(targets[i]), 2);
        }
        var impact = Assert.Single(b.Areas(42050501));
        Assert.False(b.HasLauncher());
        var arrivalFrame = b.Frame;
        Assert.Equal(arrivalFrame, impact.SpawnFrame);
        var expireFrame = b.GetPublishedAreaExpireFrame(impact);
        Assert.Single(areaEvents, e => e.AreaId == impact.AreaId && e.EventId == "area.spawn");
        Assert.DoesNotContain(areaEvents, e => e.TemplateId == 42050501 && e.EventId == "area.enter");
        b.AssertInstalledSystemOrder(output);
        b.Setup.Tick(checkpointOffset);
        if (checkpointOffset > 0)
            Assert.Contains(areaEvents, e => e.TemplateId == 42050501 && e.EventId == "area.enter" && e.Frame == arrivalFrame + 1);
        b.AssertNoPendingEnter();
        var verifyMotion = b.ObserveSurvivingMotionBindings(new[] { left, right, center });
        /*
         * 到达当帧完成发射器清理。第零帧可原地恢复；跨后续帧仍遵守
         * 存活 Continuous 的既有恢复范围，不能偷偷扩大成对象重建。
         */
        if (checkpointOffset == 0)
        {
            b.VerifyReplay(0, output, verifyMotion: verifyMotion);
            b.AssertRecoveryRejectedAfterAdvance(8);
        }
        else b.VerifyReplay(8, output, verifyMotion: verifyMotion);
        Assert.True(b.Setup.GetActorPosition(left).X < positions[0].X);
        Assert.True(b.Setup.GetActorPosition(right).X > positions[1].X);
        Assert.True(b.Setup.GetActorPosition(center).Z > positions[2].Z);
        Assert.Equal(positions[3], b.Setup.GetActorPosition(ally));
        Assert.Equal(positions[4], b.Setup.GetActorPosition(outside));
        var ticksUntilLastActiveFrame = expireFrame - b.Frame - 1;
        Assert.True(ticksUntilLastActiveFrame >= 0);
        b.Setup.Tick(ticksUntilLastActiveFrame);
        Assert.Equal(impact.AreaId, Assert.Single(b.Areas(impact.TemplateId)).AreaId);
        Assert.DoesNotContain(areaEvents, e => e.AreaId == impact.AreaId && e.EventId == "area.expire");
        b.Setup.Tick(1);
        var expire = Assert.Single(areaEvents, e => e.AreaId == impact.AreaId && e.EventId == "area.expire");
        Assert.Equal(expireFrame, expire.Frame);
        b.AssertAreaOwnershipReleased(impact);
        b.Setup.Tick(1);
        Assert.Single(areaEvents, e => e.AreaId == impact.AreaId && e.EventId == "area.expire");
        Assert.Equal(3, b.Count(MobaExecutionKind.DamageApply, 30050501));
        Assert.Equal(280f, hp[0] - b.Setup.GetActorHp(left), 2);
        Assert.Empty(b.Areas(42050501));
    }

    [Theory]
    [InlineData(UnitSubType.Hero, 2, true)]
    [InlineData(UnitSubType.Minion, 2, true)]
    [InlineData(UnitSubType.Neutral, 2, true)]
    [InlineData(UnitSubType.Boss, 2, true)]
    [InlineData(UnitSubType.Tower, 2, false)]
    [InlineData(UnitSubType.Base, 2, false)]
    [InlineData(UnitSubType.Bullet, 2, false)]
    [InlineData(UnitSubType.None, 2, false)]
    [InlineData(UnitSubType.Hero, 1, false)]
    public void Motion_crossing_qualifies_before_group_claim_and_restores_both_claim_states(
        UnitSubType subtype, int team, bool eligible)
    {
        using var b = new Battle(3006, $"wall-{subtype}-{team}");
        var target = b.Spawn("crossing", team, 102, 102);
        if (subtype == UnitSubType.None) b.Actor(target).RemoveUnitSubType();
        else b.Actor(target).ReplaceUnitSubType(subtype);
        b.Set(target, "MAGIC_DEFENSE", 100);
        b.Setup.Tick(2);
        b.Cast(2, new Vec3(100, 0, 106));
        b.Setup.TickMilliseconds(700);
        Assert.NotEmpty(b.Areas(42060301));
        Assert.Equal(0, b.Setup.CountActorBuffs(target, 30060391));

        /*
         * 通过正式 MoveInput 和原服务持有的普通来源穿墙；每次重演提交相同输入。
         * 墙体减速真实参与后续位移，不再直接挂接无恢复所有者的强制位移来源。
         */
        b.VerifyWalkReplay(target, 80, output);
        Assert.InRange(b.Setup.GetActorPosition(target).Z, 108f, 110.01f);
        Assert.Equal(eligible ? 1 : 0, b.Setup.CountActorBuffs(target, 30060391));
        Assert.Equal(eligible ? 75f : 100f, new MobaAttrs(b.Actor(target)).MagicDefense, 3);

        // Rejected contacts must not consume a future valid opportunity for this actor.
        if (!eligible)
        {
            b.Actor(target).ReplaceUnitSubType(UnitSubType.Hero);
            b.Actor(target).ReplaceTeam((Team)2);
        }
        var applications = b.Count(MobaExecutionKind.BuffApply, 30060391);
        b.VerifyWalkReplay(target, -80, output);
        Assert.InRange(b.Setup.GetActorPosition(target).Z, 101.99f, 105.99f);
        Assert.Equal(1, b.Setup.CountActorBuffs(target, 30060391));
        Assert.Equal(75f, new MobaAttrs(b.Actor(target)).MagicDefense, 3);
        // Trace nodes reuse their execution identity; actual Buff/projection state is compared on every replay.
        Assert.Equal(applications + (eligible ? 0 : 1), b.Count(MobaExecutionKind.BuffApply, 30060391));

        // A third physical crossing after restoring a claimed checkpoint cannot reapply.
        applications = b.Count(MobaExecutionKind.BuffApply, 30060391);
        b.VerifyWalkReplay(target, 80, output);
        Assert.Equal(applications, b.Count(MobaExecutionKind.BuffApply, 30060391));
    }

    [Fact]
    public void Checkpoint_after_wall_contact_contains_buff_and_replays_without_enter_history()
    {
        using var b = new Battle(3006, "wall-pending-enter");
        var target = b.Spawn("crossing", 2, 102, 102);
        b.Set(target, "MAGIC_DEFENSE", 100);
        b.Setup.Tick(2);
        b.Cast(2, new Vec3(100, 0, 106));
        b.Setup.TickMilliseconds(700);
        Assert.NotEmpty(b.Areas(42060301));
        b.VerifySettledWallCrossing(target, output);
    }

    [Fact]
    public void Enter_removing_area_suppresses_later_enters_and_consumes_expire_in_same_frame()
    {
        using var b = new Battle(1001, "enter-remove");
        b.Spawn("first", 2, 101, 106);
        b.Spawn("second", 2, 102, 106);
        b.Setup.Tick(2);
        var seen = new List<AreaEventArgs>();
        using var listener = b.ObserveAreas(seen, evt =>
        {
            if (evt.EventId == "area.enter") Assert.True(b.RemoveArea(evt.AreaId));
        });
        var area = b.SpawnTestArea(lifetime: 10, delay: 5);
        b.Setup.Tick(1);
        Assert.Single(seen, e => e.AreaId == area.Value && e.EventId == "area.enter");
        var expire = Assert.Single(seen, e => e.AreaId == area.Value && e.EventId == "area.expire");
        Assert.Equal(b.Frame, expire.Frame);
        Assert.Empty(b.Areas(1));
        b.AssertNoPendingAreaEvents();
    }

    [Fact]
    public void Jhin_trap_replacement_replays_from_the_armed_area_checkpoint()
    {
        const int trapTemplate = 42010301;
        const int fuseTemplate = 42010302;
        const int blastTemplate = 42010303;
        using var battle = new Battle(3001, "jhin-e-area-replacement");
        var target = battle.Spawn("entrant", 2, 106, 108);
        battle.Setup.Tick(2);
        battle.Cast(3, new Vec3(100, 0, 108));
        for (var tick = 0; tick < 90 && battle.Areas(trapTemplate).Count == 0; tick++)
            battle.Setup.Tick(1);
        var trap = Assert.Single(battle.Areas(trapTemplate));
        battle.Setup.Tick(20);
        var initialHp = battle.Setup.GetActorHp(target);
        var seen = new List<AreaEventArgs>();
        using var listener = battle.ObserveAreas(seen);
        battle.ExecuteAreaTrigger(30010311, new AreaEventArgs
        {
            EventId = "area.tick",
            AreaId = trap.AreaId,
            OwnerActorId = trap.OwnerActorId,
            TargetActorId = target,
            TemplateId = trapTemplate,
            Center = trap.Center,
            SourceContextId = trap.SourceContextId,
            RootContextId = trap.RootContextId,
            OwnerContextId = trap.OwnerContextId,
            SkillRuntimeHandle = trap.SkillRuntimeHandle,
            ExecutionKind = MobaExecutionKind.AreaStay,
            Frame = battle.Frame
        });
        Assert.Equal(trap.AreaId, Assert.Single(battle.Areas(trapTemplate)).AreaId);
        Assert.Empty(battle.Areas(fuseTemplate));
        battle.VerifyReplay(8, output,
            command: () =>
            {
                seen.Clear();
                battle.Setup.MoveScenarioActor(target, new MobaAcceptanceVector3Expectation
                {
                    x = trap.Center.X,
                    y = battle.Setup.GetActorPosition(target).Y,
                    z = trap.Center.Z
                });
            },
            restored: () =>
            {
                Assert.Equal(trap.AreaId, Assert.Single(battle.Areas(trapTemplate)).AreaId);
                Assert.Empty(battle.Areas(fuseTemplate));
                Assert.Equal(initialHp, battle.Setup.GetActorHp(target));
            },
            frameResult: index =>
            {
                if (index != 7) return;
                Assert.Empty(battle.Areas(trapTemplate));
                Assert.Single(seen, entry => entry.AreaId == trap.AreaId && entry.EventId == "area.expire");
                Assert.Single(seen, entry => entry.TemplateId == fuseTemplate && entry.EventId == "area.spawn");
                Assert.Single(battle.Areas(fuseTemplate));
                Assert.Equal(initialHp, battle.Setup.GetActorHp(target));
            }, saveArtifacts: false);
        battle.ExecuteAreaTrigger(30010311, Assert.Single(seen,
            entry => entry.AreaId == trap.AreaId && entry.EventId == "area.enter"));
        Assert.Single(battle.Areas(fuseTemplate));
        battle.Setup.Tick(60);
        Assert.Single(seen, entry => entry.TemplateId == blastTemplate && entry.EventId == "area.spawn");
        Assert.True(battle.Setup.GetActorHp(target) < initialHp);
    }

    [Fact]
    public void Area_lifetime_stay_and_expiry_advance_once_and_delay_replays_as_data()
    {
        using var b = new Battle(1001, "area-lifetime-delay");
        b.Spawn("target", 2, 101, 106);
        b.Setup.Tick(2);
        var seen = new List<AreaEventArgs>();
        using var listener = b.ObserveAreas(seen);
        var area = b.SpawnTestArea(lifetime: 5, delay: 4);
        var spawned = b.Frame;
        b.Setup.Tick(1);
        Assert.Single(seen, e => e.AreaId == area.Value && e.EventId == "area.enter");
        Assert.DoesNotContain(seen, e => e.EventId == "area.delay");
        Assert.Single(seen, e => e.AreaId == area.Value && e.EventId == "area.tick");
        seen.Clear();
        b.VerifyReplay(2, output);
        Assert.Equal(6, seen.Count(e => e.AreaId == area.Value && e.EventId == "area.tick"));
        /*
         * 未到期延迟由已有区域数据保存；恢复本身不得发布区域事件。
         */
        Assert.DoesNotContain(seen, e => e.EventId == "area.delay");
        seen.Clear();
        b.Setup.Tick(1);
        var delayed = Assert.Single(seen, e => e.EventId == "area.delay");
        Assert.Equal(spawned + 4, delayed.Frame);
        seen.Clear();
        b.Setup.Tick(2);
        var exit = Assert.Single(seen, e => e.EventId == "area.exit");
        var expire = Assert.Single(seen, e => e.EventId == "area.expire");
        Assert.Equal(spawned + 6, expire.Frame);
        Assert.Equal(expire.Frame, exit.Frame);
        Assert.True(seen.IndexOf(exit) < seen.IndexOf(expire));
        Assert.Empty(b.Areas(1));
        b.AssertNoPendingAreaEvents();
    }

    [Fact]
    public void Late_enter_created_area_settles_spawn_now_and_first_samples_next_tick()
    {
        using var b = new Battle(1001, "derived-spawn-residual");
        b.Spawn("target", 2, 101, 106);
        b.Setup.Tick(2);
        var seen = new List<AreaEventArgs>();
        AreaId child = default;
        using var listener = b.ObserveAreas(seen, evt =>
        {
            if (evt.EventId == "area.enter" && child.Value == 0)
                child = b.SpawnTestArea(lifetime: 5, delay: 4);
        });
        b.SpawnTestArea(lifetime: 5, delay: 4);
        b.Setup.Tick(1);
        Assert.True(child.Value > 0);
        Assert.Single(seen, e => e.AreaId == child.Value && e.EventId == "area.spawn");
        Assert.DoesNotContain(seen, e => e.AreaId == child.Value && e.EventId == "area.enter");
        b.AssertNoPendingAreaEvents();
        var produced = b.Frame;
        b.Setup.Tick(1);
        Assert.Single(seen, e => e.AreaId == child.Value && e.EventId == "area.enter" && e.Frame == produced + 1);
        b.AssertNoPendingAreaEvents();
        output.WriteLine("Derived Spawn settles in the producing frame; first spatial sampling remains in the next normal physics tick.");
    }

    [Fact]
    public void Late_spawn_chain_completes_before_checkpoint_and_replays_twice()
    {
        using var b = new Battle(1001, "derived-spawn-settled");
        b.Spawn("target", 2, 101, 106);
        b.Setup.Tick(2);
        var seen = new List<AreaEventArgs>();
        var parent = b.SpawnRetainedTestArea(lifetime: 10, delay: 5);
        AreaId child = default;
        using var listener = b.ObserveAreas(seen, evt =>
        {
            if (evt.AreaId == parent.Value && evt.EventId == "area.enter")
                child = b.SpawnRetainedTestArea(lifetime: 10, delay: 5);
            if (evt.AreaId == child.Value && evt.EventId == "area.spawn")
                b.SpawnRetainedTestArea(lifetime: 10, delay: 5);
        });
        b.Setup.Tick(1);
        var checkpointAreas = b.Areas(1).ToArray();
        Assert.Equal(3, checkpointAreas.Length);
        b.AssertAreaMembership(checkpointAreas);
        Assert.Single(seen, evt => evt.AreaId == child.Value && evt.EventId == "area.spawn");
        b.AssertNoPendingAreaEvents();
        seen.Clear();
        b.VerifyReplay(1, output, restored: () => b.AssertAreaMembership(checkpointAreas));
        Assert.Equal(3, b.Areas(1).Count);
        Assert.DoesNotContain(seen, evt => evt.EventId == "area.spawn");
        Assert.Equal(3, seen.Count(evt => evt.AreaId == child.Value && evt.EventId == "area.enter"));
        b.AssertNoPendingAreaEvents();
    }

    [Theory]
    [InlineData("area.enter")]
    [InlineData("area.expire")]
    public void Late_create_then_remove_settles_spawn_before_expire_in_same_frame(string stage)
    {
        using var b = new Battle(1001, "derived-create-remove-" + stage);
        b.Spawn("target", 2, 101, 106);
        b.Setup.Tick(2);
        var seen = new List<AreaEventArgs>();
        AreaId parent = default;
        AreaId child = default;
        MobaAreaRuntimeInfo childInfo = default;
        using var listener = b.ObserveAreas(seen, evt =>
        {
            if (evt.AreaId != parent.Value || evt.EventId != stage) return;
            child = b.SpawnRetainedTestArea(lifetime: 10, delay: 5);
            childInfo = Assert.Single(b.Areas(1), info => info.AreaId == child.Value);
            Assert.True(b.RemoveArea(child.Value));
        });
        b.VerifyReplay(2, output, command: () =>
        {
            parent = b.SpawnRetainedTestArea(lifetime: 1, delay: 5);
            seen.Clear();
        });
        var spawn = Assert.Single(seen, evt => evt.AreaId == child.Value && evt.EventId == "area.spawn");
        var expire = Assert.Single(seen, evt => evt.AreaId == child.Value && evt.EventId == "area.expire");
        Assert.Equal(spawn.Frame, expire.Frame);
        Assert.True(seen.IndexOf(spawn) < seen.IndexOf(expire));
        b.AssertAreaReleased(childInfo);
        b.AssertNoPendingAreaEvents();
    }

    private sealed class SimulationErrorCapture : AbilityKit.Core.Logging.ILogSink, IDisposable
    {
        private readonly AbilityKit.Core.Logging.ILogSink previous;
        public List<string> Messages { get; } = new();

        public SimulationErrorCapture()
        {
            previous = AbilityKit.Core.Logging.Log.Sink;
            AbilityKit.Core.Logging.Log.SetSink(this);
        }

        public void Info(string message) => previous.Info(message);
        public void Warning(string message) => previous.Warning(message);
        public void Error(string message)
        {
            Messages.Add(message);
            previous.Error(message);
        }

        public void Exception(Exception exception, string? message = null)
        {
            Messages.Add(message + ": " + exception.Message);
            previous.Exception(exception, message);
        }

        public void Dispose() => AbilityKit.Core.Logging.Log.SetSink(previous);
    }

    [Fact]
    public void Capture_rejects_between_frame_notifications_and_allows_complete_frames()
    {
        using var b = new Battle(1001, "capture-completion-gate");
        b.AssertCaptureAllowed();
        var area = b.SpawnTestArea(lifetime: 10, delay: 5);
        b.AssertCaptureBlocked();
        Assert.True(b.Guard.CanAdvance);
        b.Setup.Tick(1);
        b.AssertCaptureAllowed();
        Assert.True(b.RemoveArea(area.Value));
        b.AssertCaptureBlocked();
        b.Setup.Tick(1);
        b.AssertCaptureAllowed();
        b.AssertNoPendingAreaEvents();
    }

    [Fact]
    public void Zero_delay_for_child_waits_for_its_spawn_and_does_not_change_spawn_visible_state()
    {
        using var b = new Battle(1001, "delay-round-candidates");
        var seen = new List<AreaEventArgs>();
        AreaId parent = default;
        AreaId child = default;
        using var listener = b.ObserveAreas(seen, evt =>
        {
            if (evt.AreaId == parent.Value && evt.EventId == "area.spawn")
                child = b.SpawnTestArea(lifetime: 8, delay: 0);
            if (evt.EventId == "area.spawn") Assert.False(b.IsDelayTriggered(evt.AreaId));
            if (evt.EventId == "area.delay") Assert.True(b.IsDelayTriggered(evt.AreaId));
        });
        b.VerifyReplay(1, output, command: () =>
        {
            seen.Clear();
            parent = b.SpawnTestArea(lifetime: 8, delay: 0);
        });
        Assert.Equal(new[] { "area.spawn", "area.delay", "area.spawn", "area.delay" }, seen.Select(e => e.EventId));
        Assert.Equal(new[] { parent.Value, parent.Value, child.Value, child.Value }, seen.Select(e => e.AreaId));
        b.AssertNoPendingAreaEvents();
    }

    [Theory]
    [InlineData(16384, true)]
    [InlineData(16385, false)]
    public void Notification_budget_counts_items_and_blocks_failed_frame_commit(int items, bool succeeds)
    {
        using var b = new Battle(1001, "settlement-budget-" + items);
        for (var i = 0; i < items / 2; i++)
            Assert.True(b.RemoveArea(b.SpawnTestArea(lifetime: 100, delay: 90).Value));
        if ((items & 1) != 0) b.SpawnTestArea(lifetime: 100, delay: 90);
        Assert.Equal(items, b.PendingNotifications);
        var seen = new List<AreaEventArgs>();
        using var listener = b.ObserveAreas(seen);
        var frame = b.Frame;
        using var errors = new SimulationErrorCapture();
        b.Setup.Tick(1);
        if (succeeds)
        {
            Assert.Equal(frame + 1, b.Frame);
            Assert.Equal(items, seen.Count);
            Assert.True(b.Guard.CanCommit);
            Assert.Empty(b.Areas(1));
            b.AssertNoPendingAreaEvents();
        }
        else
        {
            Assert.Contains("16384", b.Guard.Failure.Message);
            Assert.Contains(errors.Messages, message => message.Contains("[WorldManager] World.Tick failed", StringComparison.Ordinal));
            b.AssertFailedSessionBlocked(frame);
        }
    }

    [Theory]
    [InlineData("missing-metadata")]
    [InlineData("reentry")]
    [InlineData("post-settlement")]
    [InlineData("cleanup")]
    public void Failed_world_is_blocked_even_when_world_manager_swallows_the_exception(string defect)
    {
        using var b = new Battle(1001, "settlement-failure-" + defect);
        var seen = new List<AreaEventArgs>();
        using var listener = b.ObserveAreas(seen, evt =>
        {
            if (defect == "reentry" && evt.EventId == "area.spawn") b.SettlementSystem.Execute();
        });
        if (defect == "missing-metadata") b.SpawnAreaWithoutMetadata();
        else if (defect == "post-settlement") b.World.Systems.Add(new LateDerivedArea(() => b.SpawnTestArea(8, 5), cleanup: false));
        else if (defect == "cleanup") b.World.Systems.Add(new LateDerivedArea(() => b.SpawnTestArea(8, 5), cleanup: true));
        else b.SpawnTestArea(8, 5);
        var before = b.Frame;
        using var errors = new SimulationErrorCapture();
        b.Setup.Tick(1);
        Assert.NotNull(b.Guard.Failure);
        Assert.Contains(errors.Messages, message => message.Contains("[WorldManager] World.Tick failed", StringComparison.Ordinal));
        b.AssertFailedSessionBlocked(before);
    }

    private sealed class LateDerivedArea(Action create, bool cleanup) : Entitas.IExecuteSystem, Entitas.ICleanupSystem
    {
        public void Execute() { if (!cleanup) create(); }
        public void Cleanup() { if (cleanup) create(); }
    }

    [Fact]
    public void Projectile_and_area_derivations_freeze_all_categories_before_any_callback()
    {
        using var b = new Battle(3005, "cross-projectile-area-rounds");
        var order = new List<string>();
        AreaId first = default, next = default, last = default;
        var template = 0;
        using var projectiles = b.ObserveProjectileSpawns(evt =>
        {
            if (template == 0)
            {
                template = evt.TemplateId;
                order.Add("projectile.first");
                first = b.SpawnTestArea(20, 0);
            }
            else
            {
                order.Add("projectile.second");
                last = b.SpawnTestArea(20, 0);
            }
        });
        var seen = new List<AreaEventArgs>();
        using var areas = b.ObserveAreas(seen, evt =>
        {
            if (evt.EventId is not ("area.spawn" or "area.delay")) return;
            order.Add((evt.AreaId == first.Value ? "first" : evt.AreaId == next.Value ? "next" : "last") + "." + evt.EventId);
            if (evt.AreaId == first.Value && evt.EventId == "area.spawn")
            {
                b.SpawnTestProjectile(template);
                next = b.SpawnTestArea(20, 0);
            }
        });
        b.Cast(4, new Vec3(100, 0, 150));
        b.Until(() => template != 0);
        Assert.Equal(new[]
        {
            "projectile.first", "first.area.spawn", "first.area.delay", "projectile.second",
            "next.area.spawn", "next.area.delay", "last.area.spawn", "last.area.delay",
        }, order);
        Assert.True(b.Guard.CanCommit);
        Assert.Equal(0, b.PendingNotifications);
        b.VerifyReplay(1, output);
    }

    /// <summary>
    /// 保持正式消费顺序，只观察投射物通知以验证恢复过程不会重放历史业务。
    /// </summary>
    private sealed class ProjectileSpawnObserver(
        AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering.IMobaStageTriggerService inner,
        Action<ProjectileSpawnEvent> spawn,
        Action<ProjectileExitEvent>? exit = null,
        Action<ProjectileHitEvent, int>? hit = null) : AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering.IMobaStageTriggerService
    {
        public void ExecuteAreaStage(AreaEventArgs e) => inner.ExecuteAreaStage(e);
        public void ExecuteProjectileSpawn(in ProjectileSpawnEvent e) { inner.ExecuteProjectileSpawn(in e); spawn(e); }
        public void ExecuteProjectileArrival(in ProjectileArrivalEvent e) => inner.ExecuteProjectileArrival(in e);
        public void ExecuteProjectileTick(in ProjectileTickEvent e) => inner.ExecuteProjectileTick(in e);
        public void ExecuteProjectileExit(in ProjectileExitEvent e) { inner.ExecuteProjectileExit(in e); exit?.Invoke(e); }
        public void ExecuteProjectileHit(in ProjectileHitEvent e, int actor) { inner.ExecuteProjectileHit(in e, actor); hit?.Invoke(e, actor); }
    }

    private sealed class RestoreField(object target, FieldInfo field, object value) : IDisposable
    {
        public void Dispose() => field.SetValue(target, value);
    }

    [Fact]
    public void Natural_expiry_keeps_last_skill_retain_through_exit_and_expire_before_release()
    {
        using var b = new Battle(1001, "area-expire-last-retain");
        b.Spawn("target", 2, 101, 106);
        b.Setup.Tick(2);
        var area = b.SpawnRetainedTestArea(lifetime: 1, delay: 5);
        var info = Assert.Single(b.Areas(1));
        var seen = new List<AreaEventArgs>();
        using var listener = b.ObserveAreas(seen, evt =>
        {
            if (evt.AreaId != area.Value || evt.EventId is not ("area.exit" or "area.expire")) return;
            b.AssertAreaAwaitingCleanup(info);
            Assert.Equal(info.SkillRuntimeHandle, evt.SkillRuntimeHandle);
            Assert.Equal(info.RootContextId, evt.RootContextId);
        });
        b.Setup.Tick(1);
        seen.Clear();
        b.VerifyReplay(1, output, restored: () => b.AssertAreaMembership(info));
        var endings = seen.Where(evt => evt.EventId is "area.exit" or "area.expire").ToArray();
        Assert.Equal(6, endings.Length);
        for (var i = 0; i < endings.Length; i += 2)
        {
            Assert.Equal("area.exit", endings[i].EventId);
            Assert.Equal("area.expire", endings[i + 1].EventId);
            Assert.Equal(endings[i].Frame, endings[i + 1].Frame);
        }
        b.AssertAreaReleased(info);
        b.AssertNoPendingAreaEvents();
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void Derived_expiry_releases_source_in_same_frame_and_cancels_future_delay(int delay)
    {
        using var b = new Battle(1001, "derived-expire-retain-delay-" + delay);
        var first = b.SpawnRetainedTestArea(lifetime: 1, delay: 5);
        var second = b.SpawnRetainedTestArea(lifetime: 10, delay: delay);
        var firstInfo = Assert.Single(b.Areas(1), info => info.AreaId == first.Value);
        var secondInfo = Assert.Single(b.Areas(1), info => info.AreaId == second.Value);
        var seen = new List<AreaEventArgs>();
        var expireDispatchFrame = -1;
        using var listener = b.ObserveAreas(seen, evt =>
        {
            if (evt.AreaId == first.Value && evt.EventId == "area.expire")
            {
                b.AssertAreaAwaitingCleanup(firstInfo);
                Assert.True(b.RemoveArea(second.Value));
                b.AssertAreaAwaitingCleanup(secondInfo);
            }
            if (evt.AreaId == second.Value && evt.EventId == "area.expire")
            {
                b.AssertAreaAwaitingCleanup(secondInfo);
                Assert.Equal(secondInfo.SkillRuntimeHandle, evt.SkillRuntimeHandle);
                expireDispatchFrame = b.Frame;
            }
        });
        b.Setup.Tick(1);
        b.VerifyReplay(1, output, restored: () => b.AssertAreaMembership(firstInfo, secondInfo));
        var expiry = seen.Last(evt => evt.AreaId == second.Value && evt.EventId == "area.expire");
        Assert.Equal(b.Frame, expiry.Frame);
        Assert.Equal(b.Frame, expireDispatchFrame);
        b.AssertAreaReleased(firstInfo);
        b.AssertAreaReleased(secondInfo);
        b.Setup.Tick(2);
        Assert.DoesNotContain(seen, evt => evt.AreaId == second.Value && evt.EventId == "area.delay");
        b.AssertNoPendingAreaEvents();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Area_created_after_checkpoint_is_removed_with_source_and_retain_before_replay(bool removeImmediately)
    {
        using var b = new Battle(1001, "area-before-create-" + removeImmediately);
        var seen = new List<AreaEventArgs>();
        using var listener = b.ObserveAreas(seen);
        MobaAreaRuntimeInfo? first = null;
        b.VerifyReplay(2, output, () =>
        {
            b.AssertAreaMembership();
            /*
             * 输入在检查点之后的第一帧提交；创建与移除都调用正式领域入口。
             * 重演时比较逻辑身份，不依赖对象池是否返回同一托管实例。
             */
            b.Setup.Tick(1);
            var area = b.SpawnRetainedTestArea(lifetime: 10, delay: 5);
            var info = Assert.Single(b.Areas(1));
            if (first.HasValue)
            {
                Assert.Equal(first.Value.AreaId, info.AreaId);
                Assert.Equal(first.Value.SourceContextId, info.SourceContextId);
                Assert.Equal(first.Value.SkillRuntimeHandle, info.SkillRuntimeHandle);
            }
            first = info;
            b.AssertAreaMembership(info);
            if (removeImmediately) Assert.True(b.RemoveArea(area.Value));
        }, restored: () =>
        {
            b.AssertAreaMembership();
            Assert.True(first.HasValue);
            b.AssertAreaSourceAbsent(first.Value);
        });
        Assert.True(first.HasValue);
        if (removeImmediately)
        {
            b.AssertAreaMembership();
            b.AssertAreaReleased(first.Value);
        }
        else b.AssertAreaMembership(first.Value);
        b.AssertNoPendingAreaEvents();
        Assert.Equal(3, seen.Count(e => e.EventId == "area.spawn"));
        Assert.Equal(removeImmediately ? 3 : 0, seen.Count(e => e.EventId == "area.expire"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Area_destroyed_after_checkpoint_rebuilds_physics_source_retain_and_survives_pool_churn(bool explicitRemoval)
    {
        using var b = new Battle(1001, "area-after-destroy-" + explicitRemoval);
        var seen = new List<AreaEventArgs>();
        using var listener = b.ObserveAreas(seen);
        b.SpawnRetainedTestArea(lifetime: 3, delay: 2);
        b.Setup.Tick(1);
        var original = Assert.Single(b.Areas(1));
        b.AssertAreaMembership(original);
        b.VerifyReplay(1, output, () =>
        {
            b.AssertAreaMembership(original);
            if (explicitRemoval) Assert.True(b.RemoveArea(original.AreaId));
            b.Setup.Tick(3);
            b.AssertAreaMembership();
            b.AssertAreaReleased(original);
            /*
             * 原对象已经归池，再创建另一个存活对象。恢复必须用目标名单重建
             * 原对象并移除这个后建对象，同时恢复各自的来源及保留关系。
             */
            var replacement = b.SpawnRetainedTestArea(lifetime: 10, delay: 5);
            Assert.True(replacement.Value > original.AreaId);
        }, restored: () => b.AssertAreaMembership(original));
        Assert.Single(b.Areas(1));
        b.AssertAreaReleased(original);
        b.AssertNoPendingAreaEvents();
        Assert.Equal(3, seen.Count(e => e.AreaId == original.AreaId && e.EventId == "area.expire"));
        /*
         * 原对象 Spawn 在检查点之前已结算，恢复和后续重演不得再次发布。
         */
        Assert.Single(seen, e => e.AreaId == original.AreaId && e.EventId == "area.spawn");
    }

    [Fact]
    public void Area_live_checkpoint_restores_remaining_lifetime_overlap_delay_and_retained_identity()
    {
        using var b = new Battle(1001, "area-live-retained");
        b.Spawn("target", 2, 101, 106);
        b.Setup.Tick(2);
        var seen = new List<AreaEventArgs>();
        using var listener = b.ObserveAreas(seen);
        b.SpawnRetainedTestArea(lifetime: 8, delay: 4);
        b.Setup.Tick(1);
        var original = Assert.Single(b.Areas(1));
        b.VerifyReplay(2, output, restored: () => b.AssertAreaMembership(original));
        b.AssertAreaMembership(original);
        Assert.Single(seen, e => e.EventId == "area.enter");
        Assert.DoesNotContain(seen, e => e.EventId == "area.delay");
        b.VerifyReplay(1, output, restored: () => b.AssertAreaMembership(original));
        Assert.Equal(3, seen.Count(e => e.EventId == "area.delay"));
        b.AssertAreaMembership(original);
        b.AssertNoPendingAreaEvents();
    }

    [Fact]
    public void Expire_removing_another_area_finishes_cleanup_before_checkpoint()
    {
        using var b = new Battle(1001, "derived-expire-settled");
        var first = b.SpawnTestArea(lifetime: 1, delay: 5);
        var second = b.SpawnTestArea(lifetime: 10, delay: 5);
        var seen = new List<AreaEventArgs>();
        using var listener = b.ObserveAreas(seen, evt =>
        {
            if (evt.AreaId == first.Value && evt.EventId == "area.expire")
                Assert.True(b.RemoveArea(second.Value));
        });
        b.Setup.Tick(1);
        b.VerifyReplay(1, output);
        Assert.Equal(3, seen.Count(e => e.AreaId == first.Value && e.EventId == "area.expire"));
        Assert.Equal(3, seen.Count(e => e.AreaId == second.Value && e.EventId == "area.expire"));
        Assert.Empty(b.Areas(1));
        b.AssertNoPendingAreaEvents();
    }

    [Theory]
    [InlineData("missing-physical")]
    [InlineData("extra-physical")]
    [InlineData("different-id")]
    [InlineData("different-owner")]
    public void Area_physical_metadata_mismatch_is_rejected_without_changing_the_live_world(string defect)
    {
        using var b = new Battle(1001, "area-preflight-" + defect);
        b.SpawnRetainedTestArea(lifetime: 10, delay: 5);
        b.Setup.Tick(1);
        b.RejectMismatchedAreaPhysics(defect);
        b.AssertNoPendingAreaEvents();
    }

    private sealed partial class Battle : IDisposable
    {
        private readonly ConsoleBattleBootstrapper bootstrap;
        private readonly RollbackCoordinator rollback;
        private readonly RollbackRegistry rollbackRegistry;
        private readonly string artifactDirectory;
        private int areaNotifications;
        public LiveSimSetupActionExecutor Setup { get; }
        public int Caster { get; }
        private AbilityKit.Ability.World.DI.IWorldResolver Services => bootstrap.RuntimeServices!;

        public Battle(int hero, string scenario, bool enableRollback = true)
        {
            artifactDirectory = Path.Combine(AppContext.BaseDirectory, "artifacts", "composition", scenario);
            bootstrap = new ConsoleBattleBootstrapper(BattleStartConfig.CreateDefault(),
                additionalModules: new[] { new MobaTraceAdapterModule() });
            bootstrap.Initialize();
            bootstrap.Start();
            for (var i = 0; i < 8 && bootstrap.Context.EcsWorld == null; i++) bootstrap.Tick();
            bootstrap.SetupBattle();
            for (var i = 0; i < 10; i++) bootstrap.Tick();
            Setup = new LiveSimSetupActionExecutor(bootstrap);
            Caster = Spawn("caster", 1, 100, 100, hero);
            Setup.Tick(2);
            var world = (IWorld)typeof(ConsoleBattleBootstrapper)
                .GetField("_runtimeWorld", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bootstrap)!;
            rollbackRegistry = enableRollback ? MobaRollbackRegistryBuilder.Create(world) : null!;
            rollback = enableRollback ? new RollbackCoordinator(rollbackRegistry, new RollbackSnapshotRingBuffer(2)) : null!;
        }

        public int Spawn(string alias, int team, float x, float z, int hero = 1001)
        {
            Setup.Execute(new MobaAcceptanceSetupActionExpectation
            {
                action = "spawn_actor", alias = alias, heroId = hero, attributeTemplateId = hero,
                playerId = "composition_" + alias, teamId = team,
                position = new MobaAcceptanceVector3Expectation { x = x, z = z },
            });
            Assert.True(Setup.TryGetActorId(alias, out var id));
            Setup.DisableActorBrain(id);
            Set(id, "MAGIC_DEFENSE", 0);
            Set(id, "PHYSICS_DEFENSE", 0);
            Set(id, "PER_SECOND_BLOOD_R", 0);
            Set(id, "PER_SECOND_MANA_R", 0);
            return id;
        }

        public void Set(int actor, string attribute, float value) => Setup.SetScenarioActorAttribute(actor, attribute, value);
        public ActorEntity Actor(int id)
        {
            Assert.True(Services.Resolve<MobaActorLookupService>().TryGetActorEntity(id, out var actor));
            return actor;
        }
        public void Cast(int slot, Vec3 point)
        {
            var result = Services.Resolve<SkillCastCoordinator>().TryCastBySlot(Caster, slot, point, Vec3.Forward, 0);
            Assert.True(result.Success, result.FailReason);
        }
        public int Count(MobaExecutionKind kind, int config) => Services.Resolve<MobaTraceRegistry>()
            .GetNodesByKind((int)kind).Count(node => node.Metadata?.ConfigId == config);
        public List<MobaAreaRuntimeInfo> Areas(int template)
        {
            var result = new List<MobaAreaRuntimeInfo>();
            Services.Resolve<MobaAreaRuntimeService>().TryGetAreas(result, Caster, template);
            return result;
        }

        public int GetPublishedAreaExpireFrame(MobaAreaRuntimeInfo impact)
        {
            Assert.True(Services.Resolve<MobaConfigDatabase>().TryGetAoe(impact.TemplateId, out var config));
            Assert.True(config.DurationMs > 0);
            var frameTime = Assert.IsType<FrameTime>(Services.Resolve<IFrameTime>());
            Assert.Equal(impact.SpawnFrame, frameTime.Frame.Value);
            var lastActiveFrame = Math.Max(impact.SpawnFrame + 1,
                frameTime.FrameAfterSeconds(config.DurationMs / 1000f).Value);
            return lastActiveFrame + 1;
        }
        public void Move(int actor, float speed) => Actor(actor).ReplaceMoveInput(0, Math.Sign(speed));

        public void VerifyWalkReplay(int actorId, float speed, ITestOutputHelper output)
        {
            Set(actorId, "MOVE_SPEED", Math.Abs(speed));
            var origin = Setup.GetActorPosition(actorId);
            var expectedZ = origin.Z;
            VerifyReplay(4, output,
                frameInput: i => Move(actorId, i < 3 ? speed : 0),
                frameResult: i =>
                {
                    if (i == 0) expectedZ = origin.Z;
                    var actor = Actor(actorId);
                    var pipeline = actor.motion.Pipeline;
                    var registry = Services.Resolve<MobaActorRegistry>();
                    Assert.True(Services.Resolve<MobaLocomotionRuntimeService>().TryGetBinding(
                        actorId, actor, registry.GetEntityVersion(actorId), pipeline, out var source));
                    Assert.Equal(i < 3 ? Math.Sign(speed) : 0, source.Input.Z);
                    if (i < 3) expectedZ += Math.Sign(speed) * source.Speed / 30f;
                    var actual = Setup.GetActorPosition(actorId);
                    Assert.Equal(origin.X, actual.X, 3);
                    Assert.Equal(expectedZ, actual.Z, 3);
                });
            Assert.Equal(0f, Actor(actorId).moveInput.Dz);
        }
        public void Until(Func<bool> condition)
        {
            for (var i = 0; i < 120 && !condition(); i++) Setup.Tick(1);
            Assert.True(condition());
        }

        public void RejectMismatchedLauncherTemplates()
        {
            var entry = Assert.Single(Services.Resolve<MobaProjectileLinkService>().CaptureRollbackState().Launchers);
            var actor = Actor(entry.ActorId);
            var p = actor.projectileLauncher;
            var launcherId = p.LauncherId;
            var projectileId = p.ProjectileId;
            var frame = new FrameIndex(bootstrap.Context.LastFrame);
            var before = rollback.Capture(frame);
            foreach (var corruptLauncher in new[] { true, false })
            {
                actor.ReplaceProjectileLauncher(corruptLauncher ? launcherId + 1 : launcherId,
                    corruptLauncher ? projectileId : projectileId + 1, p.RootActorId, p.EndTimeMs,
                    p.ActiveBullets, p.ScheduleId, p.IntervalFrames, p.TotalCount);
                var invalid = rollback.Capture(frame);
                actor.ReplaceProjectileLauncher(launcherId, projectileId, p.RootActorId, p.EndTimeMs,
                    p.ActiveBullets, p.ScheduleId, p.IntervalFrames, p.TotalCount);
                Assert.False(rollback.TryRestore(in invalid, out var rejected));
                Assert.Equal(MobaSkillLifecycleRollbackProvider.DefaultKey, rejected.ProviderKey);
                Assert.Equal(0, rejected.ProviderCount);
                EqualState(before, rollback.Capture(frame));
            }
        }

        public Action ObserveSurvivingMotionBindings(int[] ids)
        {
            var registry = Services.Resolve<MobaActorRegistry>();
            var locomotion = Services.Resolve<MobaLocomotionRuntimeService>();
            var saved = ids.Select(id =>
            {
                var actor = Actor(id);
                var pipeline = actor.motion.Pipeline;
                var version = registry.GetEntityVersion(id);
                Assert.True(locomotion.TryGetBinding(id, actor, version, pipeline, out var source));
                Assert.Contains(source, pipeline.CaptureSourceOrder());
                Assert.Contains(pipeline.CaptureSourceOrder(), s => s is FixedDeltaMotionSource);
                return (Id: id, Actor: actor, Version: version, Pipeline: pipeline,
                    Sources: pipeline.CaptureSourceOrder(), Locomotion: source);
            }).ToArray();
            return () =>
            {
                foreach (var item in saved)
                {
                    Assert.Same(item.Actor, Actor(item.Id));
                    Assert.Same(item.Pipeline, item.Actor.motion.Pipeline);
                    Assert.Equal(item.Sources, item.Pipeline.CaptureSourceOrder());
                    Assert.True(locomotion.TryGetBinding(item.Id, item.Actor, item.Version, item.Pipeline, out var source));
                    Assert.Same(item.Locomotion, source);
                }
            };
        }

        public void VerifyReplay(int ticks, ITestOutputHelper output, Action? command = null, Action? restored = null,
            Action? verifyMotion = null, Action<int>? frameInput = null, Action<int>? frameResult = null,
            bool saveArtifacts = true)
        {
            var checkpoint = new FrameIndex(bootstrap.Context.LastFrame);
            Assert.True(rollback.CaptureAndStore(checkpoint));
            var before = rollback.Capture(checkpoint);
            command?.Invoke();
            var expectedFrames = new List<WorldRollbackSnapshot>();
            for (var i = 0; i < ticks; i++)
            {
                frameInput?.Invoke(i);
                Setup.Tick(1);
                frameResult?.Invoke(i);
                verifyMotion?.Invoke();
                expectedFrames.Add(rollback.Capture(new(bootstrap.Context.LastFrame)));
            }
            var end = new FrameIndex(bootstrap.Context.LastFrame);
            var expected = rollback.Capture(end);
            if (saveArtifacts)
            {
                Save("checkpoint", before);
                Save("baseline", expected);
            }
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var notifications = areaNotifications;
                Assert.True(bootstrap.TryRestoreSimulationFrame(rollback, checkpoint, out var result), result.ToString());
                Assert.Equal(notifications, areaNotifications);
                EqualState(before, rollback.Capture(checkpoint));
                restored?.Invoke();
                verifyMotion?.Invoke();
                command?.Invoke();
                for (var i = 0; i < ticks; i++)
                {
                    frameInput?.Invoke(i);
                    Setup.Tick(1);
                    frameResult?.Invoke(i);
                    verifyMotion?.Invoke();
                    EqualState(expectedFrames[i], rollback.Capture(new(bootstrap.Context.LastFrame)));
                }
                Assert.Equal(end.Value, bootstrap.Context.LastFrame);
                var actual = rollback.Capture(end);
                EqualState(expected, actual);
            }
            output.WriteLine($"checkpoint={checkpoint.Value} end={end.Value} replays=2 sha256=" +
                Convert.ToHexString(SHA256.HashData(WorldRollbackSnapshotCodec.Serialize(in expected))));
        }

        public void AssertInstalledSystemOrder(ITestOutputHelper output)
        {
            var world = (AbilityKit.Ability.World.EntitasWorld)typeof(ConsoleBattleBootstrapper)
                .GetField("_runtimeWorld", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bootstrap)!;
            var rows = Flatten(world.Systems).Select((system, index) =>
            {
                var type = system.GetType();
                var attribute = type.GetCustomAttribute<AbilityKit.Ability.World.WorldSystemAttribute>();
                return new
                {
                    ExecuteIndex = index,
                    Type = type.FullName,
                    DeclaredPhase = attribute?.Phase.ToString(),
                    DeclaredOrder = attribute?.Order,
                };
            }).ToArray();
            Assert.All(rows, row => Assert.NotNull(row.DeclaredPhase));
            Assert.Equal(rows.Length, rows.Select(row => row.Type).Distinct().Count());
            foreach (var name in new[] { "ProjectileTickSystem", "MobaProjectileSyncSystem", "MobaActorDespawnCleanupSystem" })
                Assert.Single(rows, row => row.Type!.EndsWith("." + name, StringComparison.Ordinal));
            var sorted = rows.OrderBy(row => Enum.Parse<AbilityKit.Ability.World.WorldSystemPhase>(row.DeclaredPhase!))
                .ThenBy(row => row.DeclaredOrder).ThenBy(row => row.Type, StringComparer.Ordinal).ToArray();
            Assert.Equal(sorted.Select(row => row.Type), rows.Select(row => row.Type));
            Directory.CreateDirectory(artifactDirectory);
            File.WriteAllText(Path.Combine(artifactDirectory, "installed-system-order.json"),
                JsonConvert.SerializeObject(rows, Formatting.Indented));
            foreach (var row in rows)
                output.WriteLine($"Execute[{row.ExecuteIndex}] {row.Type} phase={row.DeclaredPhase} order={row.DeclaredOrder}");

            static IEnumerable<Entitas.IExecuteSystem> Flatten(Entitas.Systems systems)
            {
                var field = typeof(Entitas.Systems).GetField("_executeSystems", BindingFlags.Instance | BindingFlags.NonPublic)!;
                foreach (var child in (IEnumerable<Entitas.IExecuteSystem>)field.GetValue(systems)!)
                {
                    if (child is Entitas.Systems nested)
                    {
                        foreach (var leaf in Flatten(nested)) yield return leaf;
                    }
                    else yield return child;
                }
            }
        }

        public void AssertNoPendingEnter()
        {
            var pending = new List<AreaEnterEvent>();
            Assert.IsType<ProjectileService>(Services.Resolve<IProjectileService>()).PeekAreaEnterEvents(pending);
            Assert.Empty(pending);
        }

        public void VerifySettledWallCrossing(int targetActorId, ITestOutputHelper output)
        {
            Set(targetActorId, "MOVE_SPEED", 80);
            Move(targetActorId, 80);
            Until(() => Setup.CountActorBuffs(targetActorId, 30060391) == 1);
            Move(targetActorId, 0);
            Assert.InRange(Setup.GetActorPosition(targetActorId).Z, 109.99f, 110.01f);
            Assert.Equal(75f, new MobaAttrs(Actor(targetActorId)).MagicDefense, 3);
            AssertNoPendingEnter();
            var checkpoint = rollback.Capture(new FrameIndex(bootstrap.Context.LastFrame));
            var lifecycle = MobaSkillLifecycleCheckpointCodec.Decode<MobaSkillLifecycleCheckpoint>(checkpoint.Entries
                .Single(e => e.Key == MobaSkillLifecycleRollbackProvider.DefaultKey).Payload);
            var contexts = MobaSkillLifecycleCheckpointCodec.Decode<MobaExecutionRegistryCheckpoint>(lifecycle.Sections
                .Single(s => s.Name == "execution-contexts").Payload);
            var nodes = MemoryPackSerializer.Deserialize<MobaSkillExecutionContextRollbackEntry[]>(contexts.Nodes);
            Assert.Single(nodes, n => n.Kind == (int)MobaExecutionKind.AreaEnter &&
                n.CreatedFrame == checkpoint.Frame.Value && n.TargetActorId == targetActorId);
            VerifyReplay(4, output);
            Assert.Equal(1, Setup.CountActorBuffs(targetActorId, 30060391));
            Assert.Equal(75f, new MobaAttrs(Actor(targetActorId)).MagicDefense, 3);
            var applications = Count(MobaExecutionKind.BuffApply, 30060391);
            VerifyWalkReplay(targetActorId, -80, output);
            Assert.Equal(applications, Count(MobaExecutionKind.BuffApply, 30060391));
            Assert.Equal(1, Setup.CountActorBuffs(targetActorId, 30060391));
            AssertNoPendingEnter();
        }

        public int Frame => bootstrap.Context.LastFrame;

        public AbilityKit.Ability.World.EntitasWorld World => (AbilityKit.Ability.World.EntitasWorld)typeof(ConsoleBattleBootstrapper)
            .GetField("_runtimeWorld", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(bootstrap)!;
        public MobaCombatFrameGuard Guard => Services.Resolve<MobaCombatFrameGuard>();
        public int PendingNotifications => ((IProjectilePendingNotifications)Services.Resolve<IProjectileService>()).PendingNotificationCount;
        public Entitas.IExecuteSystem SettlementSystem => FindSettlement(World.Systems);

        private static Entitas.IExecuteSystem FindSettlement(Entitas.Systems systems)
        {
            var field = typeof(Entitas.Systems).GetField("_executeSystems", BindingFlags.Instance | BindingFlags.NonPublic)!;
            foreach (var child in (IEnumerable<Entitas.IExecuteSystem>)field.GetValue(systems)!)
            {
                if (child is AbilityKit.Demo.Moba.Runtime.Application.Systems.Projectile.MobaProjectileSyncSystem) return child;
                if (child is Entitas.Systems nested)
                {
                    var found = FindSettlement(nested);
                    if (found != null) return found;
                }
            }
            return null!;
        }

        public void VerifyActiveScheduleReplay(ITestOutputHelper output, bool afterFirstEmit)
        {
            var notifications = 0;
            using var observer = ObserveProjectileSpawns(_ => notifications++);
            var physical = Services.Resolve<IProjectileService>();
            var tasks = ProjectileRollbackProvider.ReadScheduleSnapshot(physical.ExportRollback(new(Frame)), new(Frame));
            var task = Assert.Single(tasks);
            Assert.True(physical.HasSchedule(new(task.Id)));
            var checkpoint = new FrameIndex(Frame);
            Assert.True(rollback.CaptureAndStore(checkpoint));
            var before = rollback.Capture(checkpoint);
            var expected = new List<WorldRollbackSnapshot>();
            var beforeCount = Services.Resolve<MobaProjectileLinkService>().ActiveCount;
            var emissions = new List<(int, int, Vec3)[]>();
            for (var i = 0; i < 24; i++)
            {
                Setup.Tick(1);
                expected.Add(rollback.Capture(new(Frame)));
                emissions.Add(ReadProjectileResults());
            }
            Assert.True(Services.Resolve<MobaProjectileLinkService>().ActiveCount > beforeCount);
            for (var replay = 0; replay < 2; replay++)
            {
                var live = rollback.Capture(new(Frame));
                var context = new RollbackSnapshotPreflightContext(checkpoint, before.Entries,
                    rollbackRegistry.Providers.ToDictionary(p => p.Key));
                var seen = notifications;
                foreach (var provider in rollbackRegistry.Providers.OfType<IRollbackSnapshotPreflightProvider>())
                    provider.ValidateSnapshot(context);
                EqualState(live, rollback.Capture(new(Frame)));
                Assert.Equal(seen, notifications);
                var lifecycle = (MobaSkillLifecycleRollbackProvider)rollbackRegistry.Providers.Single(p =>
                    p.Key == MobaSkillLifecycleRollbackProvider.DefaultKey);
                Assert.Contains("same actor roster", Assert.Throws<InvalidOperationException>(() =>
                    lifecycle.ValidateImport(checkpoint, before.Entries.Single(p => p.Key == lifecycle.Key).Payload)).Message);
                Assert.True(bootstrap.TryRestoreSimulationFrame(rollback, checkpoint, out var result), result.ToString());
                Assert.Equal(seen, notifications);
                EqualState(before, rollback.Capture(checkpoint));
                Assert.True(physical.HasSchedule(new(task.Id)));
                Assert.Equal(beforeCount, Services.Resolve<MobaProjectileLinkService>().ActiveCount);
                Assert.Equal(0, ((IProjectilePendingNotifications)physical).PendingNotificationCount);
                for (var i = 0; i < expected.Count; i++)
                {
                    Setup.Tick(1);
                    EqualState(expected[i], rollback.Capture(new(Frame)));
                    Assert.Equal(emissions[i], ReadProjectileResults());
                }
            }
            output.WriteLine($"active schedule checkpoint={checkpoint.Value}, frames={expected.Count}, replays=2");
        }

        public void VerifyInvalidRosterProof(string defect)
        {
            var checkpoint = rollback.Capture(new(Frame));
            Setup.Tick(1);
            var entities = Services.Resolve<AbilityKit.Demo.Moba.Services.EntityManager.MobaEntityManager>();
            Assert.True(entities.TryGetActorRollbackCommands(out var commands));
            var commandPayload = checkpoint.Entries.Single(p => p.Key == commands.StateProvider.Key).Payload;
            var command = CommandJournalCheckpointCodec.Decode(commandPayload);
            var invalid = checkpoint;
            void Replace(int key, byte[] payload) => invalid = new WorldRollbackSnapshot(checkpoint.Version,
                checkpoint.Frame, invalid.Entries.Select(p => p.Key == key ? new WorldRollbackSnapshotEntry(key, payload) : p).ToArray());
            var links = Services.Resolve<MobaProjectileLinkService>();
            var extra = links.CaptureRollbackState().Projectiles.First();
            void RemoveWithoutJournal(int actorId)
            {
                var actorRegistry = Services.Resolve<MobaActorRegistry>();
                var actor = Actor(actorId);
                Assert.True(entities.UnregisterSilently(actorId, out _, publishObjectLifecycle: false));
                actorRegistry.Unregister(actorId);
                actorRegistry.ForgetEntityIdentity(actorId);
                actor.Destroy();
            }
            switch (defect)
            {
                case "missing-command":
                    invalid = new(checkpoint.Version, checkpoint.Frame,
                        checkpoint.Entries.Where(p => p.Key != commands.StateProvider.Key).ToArray());
                    break;
                case "old-command-key":
                    invalid = new(checkpoint.Version, checkpoint.Frame, checkpoint.Entries
                        .Where(p => p.Key != commands.StateProvider.Key)
                        .Append(new WorldRollbackSnapshotEntry(CommandRollbackStateProvider.DefaultKey, commandPayload))
                        .ToArray());
                    break;
                case "epoch":
                    var epoch = new CommandJournalCheckpoint(command.Version, command.Epoch + 1, command.NextOrder);
                    Replace(commands.StateProvider.Key, CommandJournalCheckpointCodec.Encode(epoch));
                    break;
                case "future-order":
                    var future = new CommandJournalCheckpoint(command.Version, command.Epoch, commands.Log.NextOrder + 1);
                    Replace(commands.StateProvider.Key, CommandJournalCheckpointCodec.Encode(future));
                    break;
                case "no-creation-proof":
                    Replace(commands.StateProvider.Key, commands.StateProvider.Export(new(Frame)));
                    break;
                case "expired": commands.Log.TrimBefore(new(Frame + 1)); break;
                case "unsupported-type": commands.Log.Record(new(Frame), 123456789, 1, Array.Empty<byte>()); break;
                case "unsupported-version":
                    commands.Log.Record(new(Frame), MobaActorRollbackCommandRuntime.DestroyCreatedActorCommand,
                        MobaActorRollbackCommandRuntime.PayloadVersion + 1,
                        MemoryPackSerializer.Serialize(new MobaActorRollbackIdPayload(extra.ActorId)));
                    break;
                case "missing-destruction-tombstone":
                    RemoveWithoutJournal(extra.ActorId);
                    break;
                case "wrong-tombstone-version":
                    var current = Actor(extra.ActorId);
                    var original = MemoryPackSerializer.Deserialize<MobaActorRollbackTombstone>(
                        commands.CaptureDestroyed(current));
                    var wrongVersion = new MobaActorRollbackTombstone(original.ActorId, original.Kind,
                        original.Transform, original.Team, original.MainType, original.UnitSubType,
                        original.OwnerPlayerId, original.ModelId, original.HasOwnerLink, original.OwnerActorId,
                        original.RootOwnerActorId, original.EntityVersion + 1);
                    RemoveWithoutJournal(extra.ActorId);
                    commands.RecordDestroyed(extra.ActorId, MemoryPackSerializer.Serialize(wrongVersion));
                    Replace(commands.StateProvider.Key, commands.StateProvider.Export(checkpoint.Frame));
                    break;
                case "stale-command-frame":
                    var staleActor = Actor(extra.ActorId);
                    var staleTombstone = new MobaActorRollbackTombstone(extra.ActorId,
                        AbilityKit.Demo.Moba.Services.EntityConstruction.MobaEntityKind.Projectile,
                        staleActor.transform.Value, staleActor.team.Value, staleActor.entityMainType.Value,
                        staleActor.unitSubType.Value, staleActor.ownerPlayerId.Value.Value,
                        staleActor.hasModelId ? staleActor.modelId.Value : 0, staleActor.hasOwnerLink,
                        staleActor.hasOwnerLink ? staleActor.ownerLink.OwnerActorId : 0,
                        staleActor.hasOwnerLink ? staleActor.ownerLink.RootOwnerActorId : 0,
                        Services.Resolve<MobaActorRegistry>().GetEntityVersion(extra.ActorId));
                    commands.Log.Record(checkpoint.Frame, MobaActorRollbackCommandRuntime.RestoreDestroyedActorCommand,
                        MobaActorRollbackCommandRuntime.PayloadVersion,
                        MemoryPackSerializer.Serialize(staleTombstone));
                    Replace(commands.StateProvider.Key, commands.StateProvider.Export(checkpoint.Frame));
                    break;
                case "wrong-kind-tombstone":
                    var wrongKind = new MobaActorRollbackTombstone(extra.ActorId,
                        AbilityKit.Demo.Moba.Services.EntityConstruction.MobaEntityKind.Hero,
                        default, default, default, default, string.Empty, 0, false, 0, 0, 1);
                    commands.Log.Record(new(Frame), MobaActorRollbackCommandRuntime.RestoreDestroyedActorCommand,
                        MobaActorRollbackCommandRuntime.PayloadVersion,
                        MemoryPackSerializer.Serialize(wrongKind));
                    break;
                case "unknown-extra": Spawn("unknown", 2, 150, 150); break;
                case "wrong-kind": Actor(extra.ActorId).isFlyingProjectileTag = false; break;
                case "extra-source":
                case "extra-retain":
                    var state = links.CaptureRollbackState();
                    var broken = state.Projectiles.Select(p => p.ActorId == extra.ActorId
                        ? new MobaProjectileLinkRollbackEntry(p.ProjectileId, p.ActorId,
                            defect == "extra-source" ? default : p.Source,
                            defect == "extra-retain" ? default : p.Retain) : p).ToArray();
                    links.RestoreRollbackState(new(broken, state.Retired, state.Launchers));
                    break;
                case "target-missing":
                case "target-version":
                    var entity = MemoryPackSerializer.Deserialize<MobaEntitasEntityRollbackPayload>(checkpoint.Entries
                        .Single(p => p.Key == MobaEntitasEntityRollbackProvider.DefaultKey).Payload);
                    if (defect == "target-version") entity.ActorEntityVersions[0]++;
                    else entity.ActorIds[^1] = int.MaxValue;
                    Replace(MobaEntitasEntityRollbackProvider.DefaultKey, MemoryPackSerializer.Serialize(entity));
                    if (defect == "target-missing")
                    {
                        var lifecycle = MobaSkillLifecycleCheckpointCodec.Decode<MobaSkillLifecycleCheckpoint>(checkpoint.Entries
                            .Single(p => p.Key == MobaSkillLifecycleRollbackProvider.DefaultKey).Payload);
                        lifecycle.ActorIds[^1] = int.MaxValue;
                        Replace(MobaSkillLifecycleRollbackProvider.DefaultKey, MobaSkillLifecycleCheckpointCodec.Encode(lifecycle));
                    }
                    break;
                case "lifecycle-roster":
                    var life = MobaSkillLifecycleCheckpointCodec.Decode<MobaSkillLifecycleCheckpoint>(checkpoint.Entries
                        .Single(p => p.Key == MobaSkillLifecycleRollbackProvider.DefaultKey).Payload);
                    life.ActorIds[^1] = int.MaxValue;
                    Replace(MobaSkillLifecycleRollbackProvider.DefaultKey, MobaSkillLifecycleCheckpointCodec.Encode(life));
                    break;
                case "combat-roster":
                    var combat = MemoryPackSerializer.Deserialize<MobaCombatActivityRollbackPayload>(checkpoint.Entries
                        .Single(p => p.Key == MobaCombatActivityRollbackProvider.DefaultKey).Payload);
                    combat.ActorIds[^1] = int.MaxValue;
                    Replace(MobaCombatActivityRollbackProvider.DefaultKey, MemoryPackSerializer.Serialize(combat));
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(defect));
            }
            var live = rollback.Capture(new(Frame));
            var records = commands.Log.Count;
            Assert.False(rollback.TryRestore(invalid, out var result));
            Assert.Equal(0, result.ProviderCount);
            Assert.Equal(records, commands.Log.Count);
            EqualState(live, rollback.Capture(new(Frame)));
        }

        private (int, int, Vec3)[] ReadProjectileResults()
        {
            var physical = Services.Resolve<IProjectileService>();
            return Services.Resolve<MobaProjectileLinkService>().CaptureRollbackState().Projectiles.Select(p =>
            {
                Assert.True(physical.TryGetRuntimeState(new ProjectileId(p.ProjectileId), out var state));
                return (p.ProjectileId, p.ActorId, state.Position);
            }).ToArray();
        }

        /// <summary>
        /// 使用正式发布模板创建测试发射请求，允许选择发射次数和弹型并标记父技能已结束。
        /// </summary>
        public void SpawnTestRepeatLauncher(int durationMs = 3000, bool endPipeline = false,
            int bulletsPerShot = 2, float fanAngleDeg = 30, int processId = 0)
        {
            var configs = Services.Resolve<MobaConfigDatabase>();
            Assert.True(configs.TryGetProjectileLauncher(2, out var launcher));
            Assert.True(configs.TryGetProjectile(1, out var projectile));
            var sourceNode = Services.Resolve<MobaExecutionContextRegistry>().Create(new MobaExecutionContextCreateRequest(
                MobaExecutionKind.SkillCast, 10010101, Caster, 0, frame: Frame));
            var point = new Vec3(200, 0, 200);
            var skills = Services.Resolve<MobaSkillCastRuntimeService>();
            var skill = skills.Create(new MobaSkillCastRuntimeCreateRequest(10010101, 1, 1, 1,
                Caster, 0, point, Vec3.Forward, sourceNode.ContextId));
            var source = new ProjectileSourceContext(Caster, 0, projectile.Id, sourceNode.ContextId,
                sourceNode.ContextId, sourceNode.ContextId, skill.Handle, default);
            Assert.True(Services.Resolve<MobaProjectileService>().LaunchFromSpawn(Caster, launcher, projectile,
                bulletsPerShot, fanAngleDeg, durationMs, processId, in point, Vec3.Forward, in source));
            Assert.True(HasLauncher());
            if (endPipeline)
                Assert.True(skills.MarkPipelineEnded(skill.Handle, MobaSkillRuntimeEndReason.PipelineCompleted));
        }

        /// <summary>
        /// 在最后一次到期发射前后保存检查点，验证任务存在性与实际弹体逐帧重演一致。
        /// </summary>
        public void VerifyLastEmitReplay(bool afterLastEmit, ITestOutputHelper output)
        {
            var links = Services.Resolve<MobaProjectileLinkService>();
            var launcher = Actor(Assert.Single(links.CaptureRollbackState().Launchers).ActorId);
            var component = launcher.projectileLauncher;
            Assert.Equal(4, component.TotalCount);
            var intervalFrames = component.IntervalFrames;
            var scheduleId = component.ScheduleId;
            Setup.Tick(intervalFrames + (afterLastEmit ? 1 : 0));
            var physical = Services.Resolve<IProjectileService>();
            Assert.Equal(!afterLastEmit, physical.HasSchedule(new(scheduleId)));
            Assert.Equal(afterLastEmit ? 4 : 2, launcher.projectileLauncher.ActiveBullets);
            VerifyProjectileBoundaryReplay(4, output);
            Assert.False(physical.HasSchedule(new(scheduleId)));
            Assert.Empty(ProjectileRollbackProvider.ReadScheduleSnapshot(physical.ExportRollback(new(Frame)), new(Frame)));
            Assert.True(HasLauncher());
            Assert.Equal(4, launcher.projectileLauncher.ActiveBullets);
        }

        /// <summary>
        /// 验证最后弹体退出时来源仍有效，帧末完成释放；跨 Launcher 清理可完整恢复。
        /// 区分 Actor 清理当帧和 Continuous 也已移除的稳定检查点，后者重演不重发通知。
        /// </summary>
        public void VerifyLastExitAndSettledReplay(ITestOutputHelper output, int expectedCount = 2, int? hitTarget = null, bool beforeEmit = false)
        {
            var links = Services.Resolve<MobaProjectileLinkService>();
            var skills = Services.Resolve<MobaSkillCastRuntimeService>();
            var contexts = Services.Resolve<MobaExecutionContextRegistry>();
            var launcher = Assert.Single(links.CaptureRollbackState().Launchers);
            var source = launcher.Source;
            var exits = new List<ProjectileExitEvent>();
            var hits = new List<(ProjectileHitEvent Event, int Target)>();
            var spawns = 0;
            using var observer = ObserveProjectileSpawns(_ => spawns++, evt =>
            {
                Assert.True(links.TryGetSource(evt.Projectile, out var child));
                Assert.Equal(source.SkillRuntimeHandle, child.SkillRuntimeHandle);
                Assert.True(contexts.TryGet(child.SourceContextId, out var node));
                Assert.False(node.IsEnded);
                Assert.True(skills.TryGet(child.SkillRuntimeHandle, out var runtime));
                Assert.True(runtime.PendingChildren > 0);
                exits.Add(evt);
            }, (evt, target) =>
            {
                Assert.True(links.TryGetSource(evt.Projectile, out var child));
                Assert.Equal(source.SkillRuntimeHandle, child.SkillRuntimeHandle);
                Assert.True(skills.TryGet(child.SkillRuntimeHandle, out _));
                hits.Add((evt, target));
            });
            var checkpoint = new FrameIndex(Frame);
            Assert.True(rollback.CaptureAndStore(checkpoint));
            var beforeExit = rollback.Capture(checkpoint);
            var beforeJournal = ReadActorJournal();
            var checkpointNotifications = spawns + exits.Count + hits.Count;
            var checkpointSpawns = spawns;
            var checkpointExits = exits.Count;
            var checkpointHits = hits.Count;
            var expected = new List<WorldRollbackSnapshot>();
            var projectileStates = new List<(int, int, Vec3)[]>();
            var notificationDeltas = new List<int>();
            var journals = new List<string[]>();
            for (var i = 0; i < 120 && HasLauncher(); i++)
            {
                if (!beforeEmit)
                {
                    checkpoint = new FrameIndex(Frame);
                    Assert.True(rollback.CaptureAndStore(checkpoint));
                    beforeExit = rollback.Capture(checkpoint);
                    beforeJournal = ReadActorJournal();
                    checkpointNotifications = spawns + exits.Count + hits.Count;
                    checkpointSpawns = spawns;
                    checkpointExits = exits.Count;
                    checkpointHits = hits.Count;
                    expected.Clear();
                    projectileStates.Clear();
                    notificationDeltas.Clear();
                    journals.Clear();
                }
                Setup.Tick(1);
                expected.Add(rollback.Capture(new(Frame)));
                projectileStates.Add(ReadProjectileResults());
                notificationDeltas.Add(spawns + exits.Count + hits.Count - checkpointNotifications);
                journals.Add(ReadActorJournal());
            }
            Assert.False(HasLauncher());
            Assert.Equal(expectedCount, spawns);
            Assert.Equal(expectedCount, exits.Count);
            Assert.All(exits, e => Assert.Equal(Frame, e.Frame));
            if (hitTarget.HasValue)
            {
                Assert.Equal(hitTarget.Value, Assert.Single(hits).Target);
                Assert.All(exits, e => Assert.Equal(ProjectileExitReason.Hit, e.Reason));
            }
            else Assert.Empty(hits);
            Assert.Empty(links.CaptureRollbackState().Projectiles);
            Assert.Equal(0, links.ActiveCount);
            Assert.False(skills.TryGet(source.SkillRuntimeHandle, out _));
            Assert.True(contexts.TryGet(source.SourceContextId, out var endedLauncher));
            Assert.True(endedLauncher.IsEnded);
            Assert.True(contexts.TryGet(source.RootContextId, out var endedRoot));
            Assert.True(endedRoot.IsEnded);
            Assert.Equal(0, PendingNotifications);
            var launcherCleanup = rollback.Capture(new(Frame));
            Setup.Tick(1);
            expected.Add(rollback.Capture(new(Frame)));
            projectileStates.Add(ReadProjectileResults());
            notificationDeltas.Add(spawns + exits.Count + hits.Count - checkpointNotifications);
            journals.Add(ReadActorJournal());
            for (var replay = 0; replay < 2; replay++)
            {
                var seen = spawns + exits.Count + hits.Count;
                Assert.True(bootstrap.TryRestoreSimulationFrame(rollback, checkpoint, out var result), result.ToString());
                Assert.Equal(seen, spawns + exits.Count + hits.Count);
                Assert.Equal(0, PendingNotifications);
                EqualState(beforeExit, rollback.Capture(checkpoint));
                Assert.Equal(beforeJournal, ReadActorJournal());
                var restoredLauncher = Assert.Single(links.CaptureRollbackState().Launchers);
                Assert.Equal(launcher.ActorId, restoredLauncher.ActorId);
                Assert.Equal(source, restoredLauncher.Source);
                Assert.True(skills.TryGet(source.SkillRuntimeHandle, out var restoredRuntime));
                Assert.True(restoredRuntime.PendingChildren > 0);
                for (var i = 0; i < expected.Count; i++)
                {
                    Setup.Tick(1);
                    EqualState(expected[i], rollback.Capture(new(Frame)));
                    Assert.Equal(projectileStates[i], ReadProjectileResults());
                    Assert.Equal(notificationDeltas[i], spawns + exits.Count + hits.Count - seen);
                    Assert.Equal(journals[i], ReadActorJournal());
                }
            }
            AssertRejectedWithoutMutation(launcherCleanup, "Local Continuous membership is outside declared recovery ownership");
            VerifyProjectileBoundaryReplay(4, output);
            Assert.Equal(expectedCount + 2 * (expectedCount - checkpointSpawns), spawns);
            Assert.Equal(expectedCount + 2 * (expectedCount - checkpointExits), exits.Count);
            var expectedHits = hitTarget.HasValue ? 1 : 0;
            Assert.Equal(expectedHits + 2 * (expectedHits - checkpointHits), hits.Count);
            output.WriteLine($"cleaned Launcher checkpoint={checkpoint.Value}, beforeEmit={beforeEmit}, frames={expected.Count}, replays=2");
            Assert.False(skills.TryGet(source.SkillRuntimeHandle, out _));
        }

        /// <summary>
        /// 较早弹体退出时发射器及来源仍在，使用同帧命令证明重建弹体后逐帧重演。
        /// </summary>
        public void VerifyEarlierExitReplay(ITestOutputHelper output)
        {
            var links = Services.Resolve<MobaProjectileLinkService>();
            var launcher = Assert.Single(links.CaptureRollbackState().Launchers);
            var exits = new List<ProjectileExitEvent>();
            var notifications = 0;
            using var observer = ObserveProjectileSpawns(_ => notifications++, e =>
            {
                notifications++;
                exits.Add(e);
            });
            var checkpoint = default(FrameIndex);
            var checkpointNotifications = 0;
            WorldRollbackSnapshot before = default;
            var expected = new List<WorldRollbackSnapshot>();
            var projectiles = new List<(int, int, Vec3)[]>();
            var notificationCounts = new List<int>();
            for (var i = 0; i < 120 && exits.Count == 0; i++)
            {
                checkpoint = new FrameIndex(Frame);
                Assert.True(rollback.CaptureAndStore(checkpoint));
                before = rollback.Capture(checkpoint);
                checkpointNotifications = notifications;
                Setup.Tick(1);
                if (exits.Count != 0)
                {
                    expected.Add(rollback.Capture(new(Frame)));
                    projectiles.Add(ReadProjectileResults());
                    notificationCounts.Add(notifications);
                }
            }
            Assert.Equal(2, exits.Count);
            Assert.True(HasLauncher());
            Assert.Equal(2, Actor(launcher.ActorId).projectileLauncher.ActiveBullets);
            Assert.True(Services.Resolve<MobaSkillCastRuntimeService>().TryGet(launcher.Source.SkillRuntimeHandle, out var runtime));
            Assert.Equal(3, runtime.PendingChildren);

            for (var i = expected.Count; i < 4; i++)
            {
                Setup.Tick(1);
                expected.Add(rollback.Capture(new(Frame)));
                projectiles.Add(ReadProjectileResults());
                notificationCounts.Add(notifications);
            }

            for (var replay = 0; replay < 2; replay++)
            {
                var seen = notifications;
                Assert.True(bootstrap.TryRestoreSimulationFrame(rollback, checkpoint, out var result), result.ToString());
                Assert.Equal(seen, notifications);
                Assert.Equal(0, PendingNotifications);
                EqualState(before, rollback.Capture(checkpoint));
                Assert.True(HasLauncher());
                Assert.True(Services.Resolve<MobaSkillCastRuntimeService>().TryGet(launcher.Source.SkillRuntimeHandle, out _));
                for (var i = 0; i < expected.Count; i++)
                {
                    Setup.Tick(1);
                    EqualState(expected[i], rollback.Capture(new(Frame)));
                    Assert.Equal(projectiles[i], ReadProjectileResults());
                    Assert.Equal(seen + notificationCounts[i] - checkpointNotifications, notifications);
                }
            }
            output.WriteLine($"earlier exit checkpoint={checkpoint.Value}, launcher and source remain live, replayed=2");
        }

        private void VerifyProjectileBoundaryReplay(int ticks, ITestOutputHelper output)
        {
            var notifications = 0;
            using var observer = ObserveProjectileSpawns(_ => notifications++, _ => notifications++);
            var checkpoint = new FrameIndex(Frame);
            Assert.True(rollback.CaptureAndStore(checkpoint));
            var before = rollback.Capture(checkpoint);
            Save("checkpoint", before);
            var expected = new List<WorldRollbackSnapshot>();
            var projectiles = new List<(int, int, Vec3)[]>();
            var notificationCounts = new List<int>();
            for (var i = 0; i < ticks; i++)
            {
                Setup.Tick(1);
                expected.Add(rollback.Capture(new(Frame)));
                projectiles.Add(ReadProjectileResults());
                notificationCounts.Add(notifications);
            }
            Save("baseline", expected[^1]);
            for (var replay = 0; replay < 2; replay++)
            {
                var seen = notifications;
                Assert.True(bootstrap.TryRestoreSimulationFrame(rollback, checkpoint, out var result), result.ToString());
                Assert.Equal(seen, notifications);
                Assert.Equal(0, PendingNotifications);
                EqualState(before, rollback.Capture(checkpoint));
                for (var i = 0; i < ticks; i++)
                {
                    Setup.Tick(1);
                    EqualState(expected[i], rollback.Capture(new(Frame)));
                    Assert.Equal(projectiles[i], ReadProjectileResults());
                    Assert.Equal(notificationCounts[i], notifications - seen);
                }
            }
            output.WriteLine($"projectile boundary checkpoint={checkpoint.Value}, frames={ticks}, replays=2");
        }

        private void AssertRejectedWithoutMutation(WorldRollbackSnapshot checkpoint, string? reason = null)
        {
            var live = rollback.Capture(new(Frame));
            var entities = Services.Resolve<AbilityKit.Demo.Moba.Services.EntityManager.MobaEntityManager>();
            Assert.True(entities.TryGetActorRollbackCommands(out var commands));
            var records = commands.Log.Count;
            Assert.False(rollback.TryRestore(checkpoint, out var rejected));
            Assert.Equal(0, rejected.ProviderCount);
            Assert.Equal(MobaSkillLifecycleRollbackProvider.DefaultKey, rejected.ProviderKey);
            if (reason != null) Assert.Contains(reason, rejected.ToString());
            Assert.Equal(records, commands.Log.Count);
            EqualState(live, rollback.Capture(new(Frame)));
        }

        public void RejectMismatchedScheduleIdentity()
        {
            var task = Assert.Single(ProjectileRollbackProvider.ReadScheduleSnapshot(
                Services.Resolve<IProjectileService>().ExportRollback(new(Frame)), new(Frame)));
            var actor = Actor(task.LauncherActorId);
            var p = actor.projectileLauncher;
            var live = rollback.Capture(new(Frame));
            var original = p.ScheduleId;
            try
            {
                p.ScheduleId = original + 100;
                var invalid = rollback.Capture(new(Frame));
                p.ScheduleId = original;
                Assert.False(rollback.TryRestore(invalid, out var result));
                Assert.Equal(0, result.ProviderCount);
                Assert.Equal(MobaSkillLifecycleRollbackProvider.DefaultKey, result.ProviderKey);
                EqualState(live, rollback.Capture(new(Frame)));
            }
            finally { p.ScheduleId = original; }
        }

        public bool IsDelayTriggered(int areaId)
        {
            var state = Services.Resolve<MobaAreaRuntimeService>();
            return ((HashSet<int>)typeof(MobaAreaRuntimeService).GetField("_delayTriggeredAreas", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(state)!).Contains(areaId);
        }

        public void SpawnAreaWithoutMetadata()
        {
            var p = new AreaSpawnParams(Caster, new Vec3(100, 0, 106), 4, 10, 1, 1);
            Services.Resolve<IProjectileService>().SpawnArea(in p, Frame);
        }

        /// <summary>
        /// 包装正式触发入口观察生成、退出与命中，恢复原入口时不改变业务消费顺序。
        /// </summary>
        public IDisposable ObserveProjectileSpawns(Action<ProjectileSpawnEvent> action,
            Action<ProjectileExitEvent>? exit = null, Action<ProjectileHitEvent, int>? hit = null)
        {
            var system = SettlementSystem;
            var field = system.GetType().GetField("_stageTriggers", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var original = (AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering.IMobaStageTriggerService)field.GetValue(system)!;
            field.SetValue(system, new ProjectileSpawnObserver(original, action, exit, hit));
            return new RestoreField(system, field, original);
        }

        public ProjectileId SpawnTestProjectile(int template)
        {
            var node = Services.Resolve<MobaExecutionContextRegistry>().Create(new MobaExecutionContextCreateRequest(
                MobaExecutionKind.ProjectileLaunch, template, Caster, 0, frame: Frame));
            var source = new ProjectileSourceContext(Caster, 0, template, node.ContextId, node.ContextId, node.ContextId, default, default);
            var p = new ProjectileSpawnParams(Caster, template, 0, Caster, Frame,
                new Vec3(200, 0, 200), Vec3.Forward, 1f, 0, 0f, 0f, 50, 100, 0, default);
            var id = Services.Resolve<IProjectileService>().Spawn(in p);
            Services.Resolve<MobaProjectileLinkService>().BindSource(id, in source);
            return id;
        }

        public void AssertCaptureAllowed()
        {
            Assert.True(Guard.CanCommit);
            Assert.True(rollback.TryCaptureAndStore(new FrameIndex(Frame), out var result), result.ToString());
            rollback.Capture(new FrameIndex(Frame));
        }

        public void AssertCaptureBlocked()
        {
            Assert.False(Guard.CanCommit);
            Assert.Throws<InvalidOperationException>(() => rollback.Capture(new FrameIndex(Frame)));
            Assert.False(rollback.TryCaptureAndStore(new FrameIndex(Frame), out _));
        }

        public void AssertFailedSessionBlocked(int committedFrame)
        {
            Assert.Equal(committedFrame, Frame);
            Assert.False(Guard.CanAdvance);
            Assert.Same(Guard, Services.Resolve<IWorldFrameGuard>());
            AssertCaptureBlocked();
            var frameTime = Services.Resolve<IFrameTime>();
            var attemptedFrame = frameTime.Frame;
            var clock = Services.Resolve<AbilityKit.Ability.World.Services.IWorldClock>().Time;
            Setup.Tick(2);
            Assert.Equal(committedFrame, Frame);
            Assert.Equal(attemptedFrame, frameTime.Frame);
            Assert.Throws<InvalidOperationException>(() => World.Tick(.033f));
            Assert.Equal(clock, Services.Resolve<AbilityKit.Ability.World.Services.IWorldClock>().Time);
            var provider = Services.Resolve<AbilityKit.Ability.Host.IWorldStateSnapshotProvider>();
            Assert.Throws<InvalidOperationException>(() => provider.TryGetSnapshot(attemptedFrame, out _));
            var snapshots = new List<AbilityKit.Ability.Host.WorldStateSnapshot>();
            Assert.Throws<InvalidOperationException>(() => Services.Resolve<MobaSnapshotRouter>().CollectSnapshots(attemptedFrame, snapshots));
            Assert.Throws<InvalidOperationException>(() => Services.Resolve<MobaSnapshotRouter>().CollectActiveBaselines(snapshots));
            Assert.Empty(snapshots);
        }

        public void AssertRecoveryRejectedAfterAdvance(int ticks)
        {
            var checkpoint = new FrameIndex(Frame);
            Assert.True(rollback.CaptureAndStore(checkpoint));
            Setup.Tick(ticks);
            var end = new FrameIndex(Frame);
            var before = rollback.Capture(end);
            Assert.False(rollback.TryRestore(checkpoint, out var result));
            Assert.Equal(0, result.ProviderCount);
            Assert.Equal(MobaSkillLifecycleRollbackProvider.DefaultKey, result.ProviderKey);
            Assert.Contains("Local Continuous membership", result.Message);
            EqualState(before, rollback.Capture(end));
        }

        public AreaId SpawnTestArea(int lifetime, int delay)
        {
            var point = new Vec3(100, 0, 106);
            var contexts = Services.Resolve<MobaExecutionContextRegistry>();
            var source = contexts.Create(new MobaExecutionContextCreateRequest(
                MobaExecutionKind.AreaSpawn, 1, Caster, 0, frame: Frame));
            var p = new AreaSpawnParams(Caster, point, 4, lifetime, 1, 1);
            var area = Services.Resolve<IProjectileService>().SpawnArea(in p, Frame);
            Services.Resolve<MobaAreaRuntimeService>().RegisterSpawn(area, 1, Caster, in point,
                4, 1, 10, Frame, delay, source.ContextId, source.ContextId, source.ContextId);
            return area;
        }

        public AreaId SpawnRetainedTestArea(int lifetime, int delay)
        {
            var point = new Vec3(100, 0, 106);
            var contexts = Services.Resolve<MobaExecutionContextRegistry>();
            var root = contexts.Create(new MobaExecutionContextCreateRequest(
                MobaExecutionKind.SkillCast, 10010101, Caster, 0, frame: Frame));
            var skills = Services.Resolve<MobaSkillCastRuntimeService>();
            var skill = skills.Create(new MobaSkillCastRuntimeCreateRequest(10010101, 1, 1, 1,
                Caster, 0, point, Vec3.Forward, root.ContextId));
            var source = contexts.Create(new MobaExecutionContextCreateRequest(
                MobaExecutionKind.AreaSpawn, 1, Caster, 0, parentContextId: root.ContextId,
                rootContextId: root.ContextId, ownerContextId: root.ContextId, frame: Frame));
            var p = new AreaSpawnParams(Caster, point, 4, lifetime, 1, 1);
            var area = Services.Resolve<IProjectileService>().SpawnArea(in p, Frame);
            Services.Resolve<MobaAreaRuntimeService>().RegisterSpawn(area, 1, Caster, in point,
                4, 1, 10, Frame, delay, source.ContextId, root.ContextId, root.ContextId, skill.Handle);
            Assert.True(skills.MarkPipelineEnded(skill.RuntimeId, MobaSkillRuntimeEndReason.PipelineCompleted));
            return area;
        }

        public void AssertAreaMembership(params MobaAreaRuntimeInfo[] expected)
        {
            var metadata = Areas(1).OrderBy(info => info.AreaId).ToArray();
            var world = AreaPhysics();
            var physical = MemoryPackSerializer.Deserialize<AreaWorldSnapshot>(world.ExportRollback(Frame));
            Assert.Equal(expected.Select(info => info.AreaId).Order(), metadata.Select(info => info.AreaId));
            Assert.Equal(metadata.Select(info => info.AreaId), physical.Items.Select(item => item.Id).Order());
            foreach (var info in expected)
            {
                var actual = Assert.Single(metadata, candidate => candidate.AreaId == info.AreaId);
                Assert.Equal(info.SourceContextId, actual.SourceContextId);
                Assert.Equal(info.SkillRuntimeHandle, actual.SkillRuntimeHandle);
                Assert.Equal(info.SpawnFrame, actual.SpawnFrame);
                var item = Assert.Single(physical.Items, candidate => candidate.Id == info.AreaId);
                Assert.Equal(info.OwnerActorId, item.OwnerId);
                AssertAreaSourceAndRetain(info);
            }
        }

        public void AssertAreaAwaitingCleanup(MobaAreaRuntimeInfo info)
        {
            Assert.Single(Areas(1), candidate => candidate.AreaId == info.AreaId);
            var physical = MemoryPackSerializer.Deserialize<AreaWorldSnapshot>(AreaPhysics().ExportRollback(Frame));
            Assert.DoesNotContain(physical.Items, item => item.Id == info.AreaId);
            AssertAreaSourceAndRetain(info);
        }

        private void AssertAreaSourceAndRetain(MobaAreaRuntimeInfo info)
        {
            Assert.True(Services.Resolve<MobaExecutionContextRegistry>().TryGet(info.SourceContextId, out var source));
            Assert.False(source.IsEnded);
            Assert.Equal(info.RootContextId, source.RootContextId);
            var skills = Services.Resolve<MobaSkillCastRuntimeService>();
            Assert.True(skills.TryGet(info.SkillRuntimeHandle, out var runtime));
            Assert.True(runtime.PipelineEnded);
            Assert.Equal(1, runtime.PendingChildren);
            var child = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Area,
                info.AreaId, info.SourceContextId, info.TemplateId);
            Assert.True(skills.TryFindRetain(info.SkillRuntimeHandle, in child, out var retain));
            Assert.Equal(info.SkillRuntimeHandle, retain.Runtime);
        }

        public void AssertAreaSourceAbsent(MobaAreaRuntimeInfo info)
        {
            Assert.False(Services.Resolve<MobaExecutionContextRegistry>().TryGet(info.SourceContextId, out _));
            Assert.False(Services.Resolve<MobaSkillCastRuntimeService>().TryGet(info.SkillRuntimeHandle, out _));
        }

        public void AssertAreaReleased(MobaAreaRuntimeInfo info)
        {
            AssertAreaOwnershipReleased(info);
            Assert.False(Services.Resolve<MobaSkillCastRuntimeService>().TryGet(info.SkillRuntimeHandle, out _));
        }

        public void AssertAreaOwnershipReleased(MobaAreaRuntimeInfo info)
        {
            Assert.DoesNotContain(Areas(info.TemplateId), candidate => candidate.AreaId == info.AreaId);
            var physical = MemoryPackSerializer.Deserialize<AreaWorldSnapshot>(AreaPhysics().ExportRollback(Frame));
            Assert.DoesNotContain(physical.Items, item => item.Id == info.AreaId);
            var contexts = Services.Resolve<MobaExecutionContextRegistry>();
            Assert.True(contexts.TryGet(info.SourceContextId, out var source));
            Assert.True(source.IsEnded);
            var child = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Area,
                info.AreaId, info.SourceContextId, info.TemplateId);
            Assert.False(Services.Resolve<MobaSkillCastRuntimeService>().TryFindRetain(info.SkillRuntimeHandle, in child, out _));
        }

        private AreaWorld AreaPhysics()
        {
            var service = Assert.IsType<ProjectileService>(Services.Resolve<IProjectileService>());
            return (AreaWorld)typeof(ProjectileService)
                .GetField("_areas", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(service)!;
        }

        public void RejectMismatchedAreaPhysics(string defect)
        {
            var frame = new FrameIndex(Frame);
            var before = rollback.Capture(frame);
            var world = AreaPhysics();
            var physical = world.ExportRollback(Frame);
            var info = Assert.Single(Areas(1));
            WorldRollbackSnapshot invalid;
            try
            {
                if (defect is "missing-physical" or "different-id") Assert.True(world.Cancel(new AreaId(info.AreaId)));
                if (defect is "extra-physical" or "different-id")
                {
                    var p = new AreaSpawnParams(Caster, Vec3.Zero, 4, 10, 1, 1);
                    world.Spawn(in p, Frame, null);
                }
                if (defect == "different-owner")
                {
                    var active = (System.Collections.IList)typeof(AreaWorld)
                        .GetField("_active", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)!;
                    var area = active[0]!;
                    area.GetType().GetField("OwnerId")!.SetValue(area, Caster + 1);
                }
                invalid = rollback.Capture(frame);
            }
            finally
            {
                world.ImportRollback(physical);
            }
            EqualState(before, rollback.Capture(frame));
            for (var attempt = 0; attempt < 2; attempt++) AssertAreaSnapshotRejected(invalid, before);
            AssertAreaMembership(info);
        }

        private void AssertAreaSnapshotRejected(WorldRollbackSnapshot invalid, WorldRollbackSnapshot live)
        {
            var notifications = areaNotifications;
            Assert.False(rollback.TryRestore(in invalid, out var result));
            Assert.Equal(MobaSkillLifecycleRollbackProvider.DefaultKey, result.ProviderKey);
            Assert.Equal(0, result.ProviderCount);
            Assert.Contains("Area physical", result.Message);
            Assert.Equal(notifications, areaNotifications);
            EqualState(live, rollback.Capture(live.Frame));
        }

        public bool RemoveArea(int areaId) => Services.Resolve<MobaAreaRuntimeService>().DespawnArea(areaId);

        public void ExecuteAreaTrigger(int triggerId, AreaEventArgs payload)
        {
            var request = MobaTriggerExecutionRequest<AreaEventArgs>.Create(triggerId, payload, payload.EventId);
            Services.Resolve<AbilityKit.Demo.Moba.Runtime.Application.Services.Triggering.MobaTriggerExecutionGateway>()
                .ExecuteDirectTrigger(in request);
        }

        public IDisposable ObserveAreas(List<AreaEventArgs> seen, Action<AreaEventArgs>? onEvent = null)
        {
            var listeners = new List<IDisposable>();
            foreach (var name in new[] { "area.spawn", "area.delay", "area.enter", "area.cross", "area.exit", "area.tick", "area.expire" })
                listeners.Add(Services.Resolve<IEventBus>().Subscribe(
                    new EventKey<AreaEventArgs>(TriggeringIdUtil.GetEventEid(name)), evt =>
                    {
                        areaNotifications++;
                        seen.Add(evt);
                        onEvent?.Invoke(evt);
                    }));
            return new Listeners(listeners);
        }

        private sealed class Listeners(List<IDisposable> listeners) : IDisposable
        {
            public void Dispose() { foreach (var listener in listeners) listener.Dispose(); }
        }

        public List<AreaSpawnEvent> PendingAreaSpawns()
        {
            var events = new List<AreaSpawnEvent>();
            Assert.IsType<ProjectileService>(Services.Resolve<IProjectileService>()).PeekAreaSpawnEvents(events);
            return events;
        }

        public void AssertNoPendingAreaEvents()
        {
            var service = Assert.IsType<ProjectileService>(Services.Resolve<IProjectileService>());
            Assert.Empty(PendingAreaSpawns());
            AssertNoPendingEnter();
            var stays = new List<AreaStayEvent>(); service.PeekAreaStayEvents(stays); Assert.Empty(stays);
            var exits = new List<AreaExitEvent>(); service.PeekAreaExitEvents(exits); Assert.Empty(exits);
            var expires = new List<AreaExpireEvent>(); service.PeekAreaExpireEvents(expires); Assert.Empty(expires);
        }

        public bool HasLauncher() => Services.Resolve<MobaProjectileLinkService>().CaptureRollbackState().Launchers.Length != 0;

        /// <summary>
        /// 正式到达后产生额外 Motion Continuous，验证其仍不在本批 Launcher 恢复所有权内。
        /// </summary>
        public void AssertArrivalMotionRejectsReplay(Action advance)
        {
            var checkpoint = new FrameIndex(bootstrap.Context.LastFrame);
            Assert.True(rollback.CaptureAndStore(checkpoint));
            var before = rollback.Capture(checkpoint);
            advance();
            Assert.False(HasLauncher());
            Assert.Contains(Services.Resolve<MobaContinuousManager>().GetAllContinuous(),
                c => c is AbilityKit.Demo.Moba.Services.Motion.MobaMotionContinuousRuntime);
            AssertRejectedWithoutMutation(before, "Local Continuous membership is incomplete");
        }

        private void EqualState(WorldRollbackSnapshot expected, WorldRollbackSnapshot actual)
        {
            Save("actual", actual);
            var differences = new List<string>();
            foreach (var entry in expected.Entries)
            {
                var candidate = actual.Entries.FirstOrDefault(value => value.Key == entry.Key);
                if (candidate.Payload != null && candidate.Payload.SequenceEqual(entry.Payload)) continue;
                var offset = 0;
                if (candidate.Payload != null)
                    while (offset < entry.Payload.Length && offset < candidate.Payload.Length &&
                           entry.Payload[offset] == candidate.Payload[offset]) offset++;
                differences.Add($"{entry.Key}({entry.Payload.Length}/{candidate.Payload?.Length ?? -1}@{offset})");
            }
            Assert.True(differences.Count == 0, "Different rollback providers: " + string.Join(",", differences));
            Assert.Equal(WorldRollbackSnapshotCodec.Serialize(in expected), WorldRollbackSnapshotCodec.Serialize(in actual));
        }

        private void Save(string name, WorldRollbackSnapshot snapshot)
        {
            var directory = Path.Combine(artifactDirectory, snapshot.Frame.Value.ToString());
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, name + ".bin"), WorldRollbackSnapshotCodec.Serialize(in snapshot));
            foreach (var entry in snapshot.Entries)
                File.WriteAllBytes(Path.Combine(directory, $"{name}-{entry.Key}.bin"), entry.Payload);
            var lifecycle = MobaSkillLifecycleCheckpointCodec.Decode<MobaSkillLifecycleCheckpoint>(snapshot.Entries
                .Single(entry => entry.Key == MobaSkillLifecycleRollbackProvider.DefaultKey).Payload);
            foreach (var section in lifecycle.Sections)
            {
                File.WriteAllBytes(Path.Combine(directory, $"{name}-lifecycle-{section.Name}.bin"), section.Payload);
                if (section.Name == "execution-contexts")
                {
                    var state = MobaSkillLifecycleCheckpointCodec.Decode<MobaExecutionRegistryCheckpoint>(section.Payload);
                    WriteJson(section.Name, new
                    {
                        state.NextContextId,
                        Nodes = MemoryPackSerializer.Deserialize<MobaSkillExecutionContextRollbackEntry[]>(state.Nodes),
                    });
                }
                if (section.Name == "skill-runtime")
                {
                    var state = MemoryPackSerializer.Deserialize<MobaSkillRuntimeLocalRollbackPayload>(section.Payload);
                    WriteJson(section.Name, new
                    {
                        State = MemoryPackSerializer.Deserialize<MobaSkillRuntimeRollbackPayload>(state.RuntimeState),
                        state.ExecutionContextNodes,
                        state.ExecutionContextNextId,
                    });
                }
                if (section.Name == "continuous-local")
                    WriteJson(section.Name, MobaSkillLifecycleCheckpointCodec.Decode<MobaContinuousLocalCheckpoint[]>(section.Payload));
            }

            void WriteJson(string section, object value) => File.WriteAllText(
                Path.Combine(directory, $"{name}-lifecycle-{section}.json"),
                JsonConvert.SerializeObject(value, Formatting.Indented));
        }

        public void Dispose() => bootstrap.Dispose();
    }
}

using System;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Acceptance.LiveSim;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Triggering.Cue;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Game.Test.UnitTest;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class SevenHeroAlignmentRuntimeTests
{
    [Theory]
    [InlineData(3003, 3, 2)]
    [InlineData(3007, 2, 1)]
    public void Authored_recovery_keeps_failed_input_and_accepts_a_new_skill_without_refunding_old_payment(int hero, int slot, int nextSlot)
    {
        using var battle = new Battle(hero);
        battle.Set(battle.Caster, "mana", 500);
        battle.Setup.Tick(2);
        var cast = battle.Skills.TryCastBySlot(battle.Caster, slot, new Vec3(0, 0, 8), Vec3.Forward);
        Assert.True(cast.Success, cast.FailReason);
        battle.Setup.TickMilliseconds(400);
        var cooldown = battle.Actor(battle.Caster).skillLoadout.ActiveSkills[slot - 1].CooldownEndTimeMs;
        var shield = battle.Services.Resolve<MobaShieldService>().GetTotalRemaining(battle.Caster);
        Assert.True(shield > 0);
        var runtimes = battle.Services.Resolve<MobaSkillCastRuntimeService>();
        Assert.True(runtimes.TryGet(cast.RuntimeHandle, out var old));
        var oldDirection = old.AimDir;
        battle.Set(battle.Caster, "mana", 0);
        var actionBeforeFailure = battle.Actor(battle.Caster).characterHfsm.Runtime.Action;
        Assert.False(battle.Skills.TryCastBySlot(battle.Caster, nextSlot, new Vec3(5, 0, 0), Vec3.Right).Success);
        Assert.Equal(actionBeforeFailure, battle.Actor(battle.Caster).characterHfsm.Runtime.Action);
        Assert.True(battle.Skills.TryGetRunningBySlot(battle.Caster, slot, out _));
        Assert.False(old.PipelineEnded);
        battle.Set(battle.Caster, "mana", 500);
        var next = battle.Skills.TryCastBySlot(battle.Caster, nextSlot, new Vec3(5, 0, 0), Vec3.Right);
        Assert.True(next.Success, next.FailReason);
        Assert.False(battle.Skills.TryGetRunningBySlot(battle.Caster, slot, out _));
        Assert.True(old.PipelineEnded);
        Assert.Equal(MobaSkillRuntimeEndReason.PipelineCompleted, old.EndReason);
        Assert.Equal(oldDirection, old.AimDir);
        Assert.Equal(cooldown, battle.Actor(battle.Caster).skillLoadout.ActiveSkills[slot - 1].CooldownEndTimeMs);
        Assert.Equal(shield, battle.Services.Resolve<MobaShieldService>().GetTotalRemaining(battle.Caster), .01f);
        battle.Setup.Tick(1);
        Assert.Equal(next.RuntimeHandle.RootContextId,
            battle.Actor(battle.Caster).characterHfsm.Runtime.Action.CastInstanceId);
    }

    [Theory]
    [InlineData(3003, 3)]
    [InlineData(3007, 2)]
    public void Authored_recovery_can_handoff_to_movement_while_committed_shields_continue(int hero, int slot)
    {
        using var battle = new Battle(hero);
        var cast = battle.Skills.TryCastBySlot(battle.Caster, slot, new Vec3(0, 0, 8), Vec3.Forward);
        Assert.True(cast.Success, cast.FailReason);
        if (hero == 3007) Assert.False(battle.Skills.TryFinishRecoveryForMovement(battle.Caster));
        battle.Setup.TickMilliseconds(400);
        var shield = battle.Services.Resolve<MobaShieldService>().GetTotalRemaining(battle.Caster);
        Assert.True(shield > 0);
        Assert.True(battle.Skills.TryFinishRecoveryForMovement(battle.Caster));
        Assert.False(battle.Skills.TryGetRunningBySlot(battle.Caster, slot, out _));
        Assert.Equal(shield, battle.Services.Resolve<MobaShieldService>().GetTotalRemaining(battle.Caster), .01f);
        battle.Setup.Tick(1);
        Assert.Equal(0L, battle.Actor(battle.Caster).characterHfsm.Runtime.Action.CastInstanceId);
    }

    [Theory]
    [InlineData(3003, 1, "MAGIC_ATTACK", 75f)]
    [InlineData(3003, 2, "MAGIC_ATTACK", 85f)]
    [InlineData(3003, 4, "MAGIC_ATTACK", 65f)]
    [InlineData(3002, 2, "PHYSICS_ATTACK", 140f)]
    [InlineData(3001, 1, "PHYSICS_ATTACK", 80f)]
    public void Spell_scaling_reads_the_authored_combat_attribute(int hero, int slot, string attribute, float expectedBonus)
    {
        static float Damage(int hero, int slot, string attribute, float value)
        {
            using var battle = new Battle(hero);
            battle.Set(battle.Caster, attribute, value);
            battle.Set(battle.Caster, "EXTRA_PHYSICS_ATTACK", 0);
            battle.Set(battle.Target, "MAGIC_DEFENSE", 0);
            battle.Set(battle.Target, "PHYSICS_DEFENSE", 0);
            battle.Set(battle.Target, "MAX_HP", 5000);
            battle.Set(battle.Target, "hp", 5000);
            battle.Setup.Tick(2);
            var hpBefore = battle.Setup.GetActorHp(battle.Target);
            var cast = battle.Skills.TryCastBySlot(battle.Caster, slot, new Vec3(0, 0, 3), Vec3.Forward, battle.Target);
            Assert.True(cast.Success, cast.FailReason);
            for (var tick = 0; tick < 50 && battle.Setup.GetActorHp(battle.Target) >= hpBefore; tick++) battle.Setup.Tick(1);
            var damage = hpBefore - battle.Setup.GetActorHp(battle.Target);
            Assert.True(damage > 0, $"hero={hero} slot={slot} did not damage its target");
            return damage;
        }

        Assert.Equal(expectedBonus, Damage(hero, slot, attribute, 100) - Damage(hero, slot, attribute, 0), .02f);
    }

    [Fact]
    public void Annie_E_shield_consumes_its_configured_AP_ratio()
    {
        using var battle = new Battle(3003);
        battle.Set(battle.Caster, "MAGIC_ATTACK", 100);
        battle.Setup.Tick(2);
        var cast = battle.Skills.TryCastBySlot(battle.Caster, 3, Vec3.Zero, Vec3.Forward);
        Assert.True(cast.Success, cast.FailReason);
        battle.Setup.Tick(2);
        Assert.Equal(140f, battle.Services.Resolve<MobaShieldService>().GetTotalRemaining(battle.Caster), .01f);
    }

    [Fact]
    public void Annie_Q_kill_refunds_paid_mana_and_half_the_original_effective_cooldown()
    {
        using var battle = new Battle(3003);
        battle.Set(battle.Caster, "mana", 200);
        battle.Set(battle.Target, "hp", 1);
        battle.Setup.Tick(2);
        var manaBefore = battle.Setup.GetActorMana(battle.Caster);
        var cast = battle.Skills.TryCastBySlot(battle.Caster, 1, new Vec3(0, 0, 3), Vec3.Forward, battle.Target);
        Assert.True(cast.Success, cast.FailReason);
        var cooldownBefore = battle.Actor(battle.Caster).skillLoadout.ActiveSkills[0].CooldownEndTimeMs;
        Assert.True(cooldownBefore > 0);
        for (var tick = 0; tick < 60 && battle.Setup.GetActorHp(battle.Target) > 0; tick++) battle.Setup.Tick(1);
        Assert.Equal(0f, battle.Setup.GetActorHp(battle.Target));
        Assert.Equal(manaBefore, battle.Setup.GetActorMana(battle.Caster), .01f);
        Assert.Equal(cooldownBefore - 2000, battle.Actor(battle.Caster).skillLoadout.ActiveSkills[0].CooldownEndTimeMs);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(200, true)]
    public void Jhin_R_checks_and_pays_mana_before_entering_its_persistent_mode(int mana, bool accepted)
    {
        using var battle = new Battle(3001);
        battle.Set(battle.Caster, "mana", mana);
        battle.Setup.Tick(2);
        var cast = battle.Skills.TryCastBySlot(battle.Caster, 4, new Vec3(0, 0, 3), Vec3.Forward, battle.Target);
        battle.Setup.Tick(2);
        Assert.Equal(accepted, cast.Success);
        Assert.Equal(accepted ? 100f : 0f, battle.Setup.GetActorMana(battle.Caster), .01f);
        Assert.Equal(accepted ? 1 : 0, battle.Setup.CountActorBuffs(battle.Caster, 32010404));
        Assert.Equal(accepted, battle.Actor(battle.Caster).skillLoadout.ActiveSkills[3].CooldownEndTimeMs > 0);
    }

    [Theory]
    [InlineData(2, false, false)]
    [InlineData(5, false, false)]
    [InlineData(5, true, false)]
    [InlineData(2, false, true)]
    [InlineData(5, false, true)]
    [InlineData(5, true, true)]
    public void Jhin_W_and_R_damage_the_line_but_stop_on_the_first_hero(int slot, bool finalShot, bool shielded)
    {
        using var b = new Battle(3001, targetZ: 12);
        var units = new List<int>();
        for (var i = 0; i < 6; i++)
        {
            var id = b.Spawn("line" + i, 3007, 2, 2 + i * 1.4f);
            b.Actor(id).ReplaceUnitSubType(i == 2 ? UnitSubType.Neutral : UnitSubType.Minion);
            if (i == 2) b.Actor(id).ReplaceTeam(Team.None);
            units.Add(id);
        }
        units.Add(b.Target);
        var behind = b.Spawn("behind", 3007, 2, 16);
        foreach (var id in units.Append(behind))
        {
            b.Set(id, "MAX_HP", 100000); b.Set(id, "hp", 100000);
            b.Set(id, "PHYSICS_DEFENSE", 0); b.Set(id, "MAGIC_DEFENSE", 0);
        }
        b.Setup.Tick(2);
        if (slot == 5)
        {
            Assert.True(b.Skills.TryCastBySlot(b.Caster, 4, new Vec3(0, 0, 18), Vec3.Forward).Success);
            b.Setup.TickMilliseconds(700);
            for (var shot = 0; finalShot && shot < 3; shot++)
            {
                var result = b.Skills.TryCastBySlot(b.Caster, 5, new Vec3(0, 0, 18), Vec3.Forward);
                Assert.True(result.Success, result.FailReason);
                b.Setup.TickMilliseconds(1100);
            }
        }
        if (shielded) b.Services.Resolve<MobaShieldService>().AddShield(b.Target, new AbilityKit.Demo.Moba.Components.ShieldLayer
        {
            ShieldId = 900001, SourceActorId = b.Target, OwnerActorId = b.Target, TargetActorId = b.Target,
            CurrentValue = 100000, MaxValue = 100000, InitialValue = 100000, AbsorbRatio = 1,
            DamageTypeMask = 7, ExpireFrame = int.MaxValue,
        });
        var before = units.ToDictionary(id => id, id => b.Setup.GetActorHp(id));
        var cast = b.Skills.TryCastBySlot(b.Caster, slot, new Vec3(0, 0, 18), Vec3.Forward);
        Assert.True(cast.Success, cast.FailReason);
        b.Setup.TickMilliseconds(1800);
        foreach (var id in units)
            if (shielded && id == b.Target) Assert.Equal(before[id], b.Setup.GetActorHp(id), .01f);
            else Assert.True(b.Setup.GetActorHp(id) < before[id], $"Line unit {id} was skipped");
        if (shielded) Assert.True(b.Services.Resolve<MobaShieldService>().GetTotalRemaining(b.Target) < 100000);
        Assert.Equal(100000f, b.Setup.GetActorHp(behind), .01f);
        if (finalShot) Assert.Equal(0, b.Setup.CountActorBuffs(b.Caster, 32010404));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Jhin_R_ground_entry_receives_the_submitted_facing_before_its_cue(bool targetInSector)
    {
        using var b = new Battle(3001);
        if (targetInSector) b.Spawn("off_axis", 3007, 2, .5f, 10);
        var snapshots = new List<WorldStateSnapshot>();
        var provider = b.Services.Resolve<IWorldStateSnapshotBatchProvider>();
        // Drain startup events, then turn away from the previous +Z pose.
        provider.CollectSnapshots(new AbilityKit.Ability.FrameSync.FrameIndex(100), snapshots, 32);
        snapshots.Clear();
        Assert.True(b.Skills.TryCastBySlot(b.Caster, 4, new Vec3(20, 0, 0), Vec3.Right).Success);
        provider.CollectSnapshots(new AbilityKit.Ability.FrameSync.FrameIndex(101), snapshots, 32);
        var transformIndex = snapshots.FindIndex(s => s.OpCode == AbilityKit.Protocol.Moba.MobaOpCodes.Snapshot.ActorTransform);
        var cueIndex = snapshots.FindIndex(s => s.OpCode == AbilityKit.Protocol.Moba.MobaOpCodes.Snapshot.PresentationCue);
        Assert.True(transformIndex >= 0 && cueIndex >= 0);
        var pose = AbilityKit.Protocol.Moba.StateSync.MobaActorTransformSnapshotCodec
            .Deserialize(snapshots[transformIndex].Payload).Single(e => e.ActorId == b.Caster);
        Assert.True(pose.ForwardX > .999f);
        Assert.True(transformIndex < cueIndex,
            "A one-time actor attachment must see this frame's facing before consuming its entry cue.");
    }

    [Fact]
    public void Jhin_R_respects_new_input_rejects_outside_without_consumption_and_expires_into_normal_mode()
    {
        using var b = new Battle(3001, targetZ: 25);
        var left = b.Spawn("left", 3007, 2, 8, -4);
        var right = b.Spawn("right", 3007, 2, 8, 4);
        foreach (var id in new[] { left, right }) { b.Set(id, "MAX_HP", 100000); b.Set(id, "hp", 100000); }
        b.Setup.Tick(2);
        Assert.True(b.Skills.TryCastBySlot(b.Caster, 4, new Vec3(0, 0, 20), Vec3.Forward).Success);
        b.Setup.TickMilliseconds(700);
        var hfsm = b.Actor(b.Caster).characterHfsm.Runtime;
        Assert.True(hfsm.TryGetAimSnapshot("curtain_call", out _, out var axis));
        var action = hfsm.Action;
        foreach (var invalid in new[] { Vec3.Right, Vec3.Zero, Vec3.Up })
        {
            var rejected = b.Skills.TryCastBySlot(b.Caster, 5, new Vec3(10, 0, 0), invalid);
            Assert.False(rejected.Success);
            Assert.Equal(action, hfsm.Action);
        }
        var hpLeft = b.Setup.GetActorHp(left); var hpRight = b.Setup.GetActorHp(right);
        var leftDirection = DeterministicMathBridge.Normalize(new Vec3(-4, 0, 8));
        Assert.True(b.Skills.TryCastBySlot(b.Caster, 5, new Vec3(-4, 0, 8), leftDirection).Success);
        b.Setup.TickMilliseconds(1100);
        Assert.True(b.Setup.GetActorHp(left) < hpLeft);
        Assert.Equal(hpRight, b.Setup.GetActorHp(right), .01f);
        var rightDirection = DeterministicMathBridge.Normalize(new Vec3(4, 0, 8));
        Assert.True(b.Skills.TryCastBySlot(b.Caster, 5, new Vec3(4, 0, 8), rightDirection).Success);
        b.Setup.TickMilliseconds(1100);
        Assert.True(b.Setup.GetActorHp(right) < hpRight);
        Assert.True(hfsm.TryGetAimSnapshot("curtain_call", out _, out var retained));
        Assert.Equal(axis, retained);
        // 45 degrees is inside a cone recentered on the previous right shot,
        // but outside the original 30-degree half cone. It must still fail.
        var outsideOriginal = b.Skills.TryCastBySlot(b.Caster, 5, new Vec3(8, 0, 8),
            DeterministicMathBridge.Normalize(new Vec3(8, 0, 8)));
        Assert.False(outsideOriginal.Success);
        Assert.True(hfsm.TryGetAimSnapshot("curtain_call", out _, out retained));
        Assert.Equal(axis, retained);
        Assert.True(b.Services.Resolve<MobaSkillLoadoutService>().TryGetSkillId(b.Caster, 5, out var modeAttack));
        Assert.Equal(30010501, modeAttack);
        b.Setup.TickMilliseconds(7500);
        Assert.Equal(0, b.Setup.CountActorBuffs(b.Caster, 32010404));
        Assert.Equal(0, b.Setup.CountActorBuffs(b.Caster, 32010405));
        Assert.Equal(0, b.Setup.CountActorBuffs(b.Caster, 32010406));
        Assert.False(hfsm.TryGetAimSnapshot("curtain_call", out _, out _));
        Assert.Equal("normal", hfsm.Action.ModeId);
        Assert.True(b.Services.Resolve<MobaCombatRulesService>().CanMove(b.Caster));
        Assert.True(b.Services.Resolve<MobaSkillLoadoutService>().TryGetSkillId(b.Caster, 5, out var normalAttack));
        Assert.Equal(30010601, normalAttack);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Jhin_R_expiry_resets_partial_counter_and_reentry_starts_with_three_regular_shots(int shots)
    {
        using var b = new Battle(3001, targetZ: 25);
        Assert.True(b.Skills.TryCastBySlot(b.Caster, 4, default, Vec3.Forward).Success);
        b.Setup.TickMilliseconds(700);
        for (var i = 0; i < shots; i++)
        {
            var result = b.Skills.TryCastBySlot(b.Caster, 5, default, Vec3.Forward);
            Assert.True(result.Success, result.FailReason);
            b.Setup.TickMilliseconds(1100);
        }
        b.Setup.TickMilliseconds(10500 - 700 - shots * 1100);
        Assert.Equal(0, b.Setup.CountActorBuffs(b.Caster, 32010404));
        Assert.Equal(0, b.Setup.CountActorBuffs(b.Caster, 32010405));
        var hfsm = b.Actor(b.Caster).characterHfsm.Runtime;
        Assert.False(hfsm.TryGetAimSnapshot("curtain_call", out _, out _));
        Assert.Equal("normal", hfsm.Action.ModeId);
        Assert.True(b.Services.Resolve<MobaCombatRulesService>().CanMove(b.Caster));
        Assert.Equal(0d, b.Services.Resolve<AbilityKit.Demo.Moba.Gameplay.MobaGameplayVariableService>().Get(
            AbilityKit.Demo.Moba.Gameplay.MobaGameplayVariableKeys.ResolveScopedKey(30010401, b.Caster)));
        var cues = b.Services.Resolve<MobaPresentationCueSnapshotService>();
        Assert.False(cues.TryGetActiveCue($"gameplay-counter:{b.Caster}:30010401", out _));
        b.Actor(b.Caster).skillLoadout.ActiveSkills[3].CooldownEndTimeMs = 0;
        b.Set(b.Caster, "mana", 500);
        Assert.True(b.Skills.TryCastBySlot(b.Caster, 4, default, Vec3.Right).Success);
        b.Setup.TickMilliseconds(700);
        Assert.True(hfsm.TryGetAimSnapshot("curtain_call", out _, out var newAxis));
        Assert.Equal(Vec3.Right, newAxis);
        Assert.False(b.Skills.TryCastBySlot(b.Caster, 5, default, Vec3.Forward).Success);
        for (var i = 0; i < 3; i++)
        {
            Assert.True(b.Services.Resolve<MobaSkillLoadoutService>().TryGetSkillId(b.Caster, 5, out var skill));
            Assert.Equal(30010501, skill);
            Assert.True(b.Skills.TryCastBySlot(b.Caster, 5, default, Vec3.Right).Success);
            b.Setup.TickMilliseconds(1100);
        }
        Assert.True(b.Services.Resolve<MobaSkillLoadoutService>().TryGetSkillId(b.Caster, 5, out var finalSkill));
        Assert.Equal(30010502, finalSkill);
    }

    [Fact]
    public void Locked_basic_attack_faces_its_resolved_target_immediately_through_authored_generic_button()
    {
        using var b = new Battle(3001);
        var target = b.Spawn("reverse", 3007, 2, -3);
        b.Setup.Tick(2);
        var cast = b.Skills.TryCastBySlot(b.Caster, 5, new Vec3(0, 0, -3), Vec3.Forward, target);
        Assert.True(cast.Success, cast.FailReason);
        var forward = b.Actor(b.Caster).transform.Value.Rotation.Rotate(Vec3.Forward);
        Assert.True(forward.Z < -.999f, "Successful locked attack should snap authoritative facing to its target, not the stale input ray");
    }

    private sealed class Battle : IDisposable
    {
        private readonly ConsoleBattleBootstrapper _bootstrapper;
        public LiveSimSetupActionExecutor Setup { get; }
        public int Caster { get; }
        public int Target { get; }
        public IWorldResolver Services => _bootstrapper.RuntimeServices!;
        public SkillCastCoordinator Skills => Services.Resolve<SkillCastCoordinator>();

        public Battle(int hero, float targetZ = 3)
        {
            var config = BattleStartConfig.CreateDefault();
            foreach (var player in config.Players) player.EnableBrainOnSpawn = false;
            _bootstrapper = new ConsoleBattleBootstrapper(config);
            _bootstrapper.Initialize();
            _bootstrapper.Start();
            for (var i = 0; i < 8 && _bootstrapper.Context.EcsWorld == null; i++) _bootstrapper.Tick();
            _bootstrapper.SetupBattle();
            for (var i = 0; i < 10; i++) _bootstrapper.Tick();
            Setup = new LiveSimSetupActionExecutor(_bootstrapper);
            Caster = Spawn("caster", hero, 1, 0);
            Target = Spawn("target", 3007, 2, targetZ);
            Setup.Tick(2);
        }

        public int Spawn(string alias, int hero, int team, float z, float x = 0)
        {
            Setup.Execute(new MobaAcceptanceSetupActionExpectation
            {
                action = "spawn_actor", alias = alias, playerId = "alignment_" + alias,
                heroId = hero, attributeTemplateId = hero, teamId = team,
                position = new MobaAcceptanceVector3Expectation { z = z, x = x },
            });
            Assert.True(Setup.TryGetActorId(alias, out var actor));
            Setup.DisableActorBrain(actor);
            Set(actor, "PER_SECOND_BLOOD_R", 0);
            Set(actor, "PER_SECOND_MANA_R", 0);
            return actor;
        }

        public ActorEntity Actor(int id)
        {
            Assert.True(Services.Resolve<MobaActorLookupService>().TryGetActorEntity(id, out var actor));
            return actor;
        }
        public void Set(int actor, string attribute, float value) => Setup.SetScenarioActorAttribute(actor, attribute, value);
        public void Dispose() => _bootstrapper.Dispose();
    }
}

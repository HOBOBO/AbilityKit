using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.World;
using AbilityKit.Ability.World.Abstractions;
using AbilityKit.Ability.World.DI;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Acceptance.LiveSim;
using AbilityKit.Demo.Moba.Console;
using AbilityKit.Demo.Moba.Console.Battle.Config;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Combat.Transactions;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Plan.Json;
using AbilityKit.Demo.Moba.Services.Triggering.Cue;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Smoke;

public sealed class SevenHeroAlignmentRuntimeTests
{
    [Fact]
    public void Jhin_Q_same_projectile_impact_kills_amplify_only_later_targets()
    {
        var (baseline, bonusPerKill) = Run(3000, 3000);
        var nonlethal = Run(1000, 3000).Damage;
        var unrelatedDeath = Run(1000, 3000, killFirstExternally: true).Damage;
        var firstKill = Run(1, 3000).Damage;
        var twoKills = Run(1, 1).Damage;

        for (var index = 1; index < baseline.Length; index++)
        {
            Assert.Equal(baseline[index], nonlethal[index], .02f);
            Assert.Equal(baseline[index], unrelatedDeath[index], .02f);
            Assert.Equal(baseline[index] * (1f + bonusPerKill), firstKill[index], .05f);
        }
        for (var index = 2; index < baseline.Length; index++)
            Assert.Equal(baseline[index] * (1f + 2f * bonusPerKill), twoKills[index], .05f);

        static (float[] Damage, float BonusPerKill) Run(float firstHp, float secondHp, bool killFirstExternally = false)
        {
            using var battle = new Battle(3001, targetZ: 4);
            var plans = battle.Services.Resolve<TriggerPlanJsonDatabase>();
            Assert.True(plans.TryGetPlanByTriggerId(30010111, out var hitPlan));
            var damageAction = Assert.Single(hitPlan.Actions, action => action.Args?.ContainsKey("projectile_impact_kill_bonus") == true);
            Assert.True(damageAction.Args.TryGetValue("projectile_impact_kill_bonus", out var configuredBonus));
            Assert.Equal(ENumericValueRefKind.Const, configuredBonus.Ref.Kind);
            Assert.True(configuredBonus.Ref.ConstValue > 0d && configuredBonus.Ref.ConstValue <= 1d);

            var targets = new[] { battle.Target, battle.Spawn("second", 3007, 2, 3, 2),
                battle.Spawn("third", 3007, 2, 7, 2), battle.Spawn("fourth", 3007, 2, 10, 2) };
            var hp = new[] { firstHp, secondHp, 3000f, 3000f };
            for (var index = 0; index < targets.Length; index++)
            {
                battle.Set(targets[index], "max_hp", 3000);
                battle.Set(targets[index], "hp", hp[index]);
            }
            battle.Setup.Tick(2);
            Assert.True(battle.Skills.TryCastBySlot(battle.Caster, 1, new Vec3(0, 0, 4), Vec3.Forward, battle.Target).Success);
            for (var tick = 0; tick < 120 && battle.Setup.GetActorHp(targets[0]) == firstHp; tick++)
                battle.Setup.Tick(1);
            Assert.True(battle.Setup.GetActorHp(targets[0]) < firstHp);
            if (killFirstExternally)
            {
                Assert.True(battle.Setup.GetActorHp(targets[0]) > 0);
                var death = battle.Services.Resolve<MobaDamageService>().CommitDamage(
                    battle.Caster, targets[0], 1, 3000f, 0, 0, default);
                Assert.True(death.BecameDead);
            }
            battle.Setup.Tick(120);
            Assert.Equal(firstHp == 1 || killFirstExternally, battle.Setup.GetActorHp(targets[0]) == 0);
            Assert.Equal(secondHp == 1, battle.Setup.GetActorHp(targets[1]) == 0);
            var damage = targets.Select((target, index) => hp[index] - battle.Setup.GetActorHp(target)).ToArray();
            Assert.All(damage, value => Assert.True(value > 0));
            return (damage, (float)configuredBonus.Ref.ConstValue);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Annie_summon_cast_uses_the_query_result_instead_of_the_input_target(bool hasMatchingEnemy)
    {
        using var battle = new Battle(3003, targetZ: 40);
        var selectedTarget = hasMatchingEnemy ? battle.Spawn("impact", 3007, 2, 4) : 0;
        battle.Setup.Tick(2);

        var cast = battle.Skills.TryCastBySlot(battle.Caster, 4, new Vec3(0, 0, 4), Vec3.Forward, battle.Target);

        Assert.True(cast.Success, cast.FailReason);
        var runtimes = battle.Services.Resolve<MobaSkillCastRuntimeService>();
        Assert.True(runtimes.TryGet(cast.RuntimeHandle, out var runtime));
        Assert.Equal(selectedTarget, runtime.TargetActorId);
    }

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
    [InlineData(3001, 1, "PHYSICS_ATTACK", 44f)]
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

    [Theory]
    [InlineData(1, 3, true)]
    [InlineData(2, 3, false)]
    [InlineData(1, 9, false)]
    public void Annie_E_targets_an_explicit_allied_hero_only_within_range(int team, float distance, bool accepted)
    {
        using var battle = new Battle(3003);
        var recipient = battle.Spawn("shield_recipient", 3007, team, distance, 1);
        battle.Set(battle.Caster, "MAGIC_ATTACK", 100);
        battle.Setup.Tick(2);
        var manaBefore = battle.Setup.GetActorMana(battle.Caster);
        var cast = battle.Skills.TryCastBySlot(battle.Caster, 3, new Vec3(1, 0, distance), Vec3.Forward, recipient);
        Assert.Equal(accepted, cast.Success);
        battle.Setup.Tick(2);

        var shields = battle.Services.Resolve<MobaShieldService>();
        Assert.Equal(0f, shields.GetTotalRemaining(battle.Caster));
        Assert.Equal(accepted ? 140f : 0f, shields.GetTotalRemaining(recipient), .01f);
        Assert.Equal(manaBefore - (accepted ? 60f : 0f), battle.Setup.GetActorMana(battle.Caster), .01f);
    }

    [Fact]
    public void Annie_E_rejects_direct_targeting_of_her_own_living_summon()
    {
        using var battle = new Battle(3003, targetZ: 40);
        var summonPosition = new Vec3(0, 0, 3);
        var summons = battle.Services.Resolve<MobaSummonService>();
        Assert.True(summons.TrySummon(battle.Caster, 30030502, in summonPosition, Vec3.Forward,
            default, out var summonActorId));
        var owned = new List<int>();
        Assert.True(summons.TryGetSummons(battle.Caster, owned, 30030502));
        Assert.Equal(summonActorId, Assert.Single(owned));

        var manaBefore = battle.Setup.GetActorMana(battle.Caster);
        var cooldownBefore = battle.Actor(battle.Caster).skillLoadout.ActiveSkills[2].CooldownEndTimeMs;
        var cast = battle.Skills.TryCastBySlot(battle.Caster, 3, summonPosition, Vec3.Forward, summonActorId);
        Assert.False(cast.Success);
        Assert.Equal(manaBefore, battle.Setup.GetActorMana(battle.Caster));
        Assert.Equal(cooldownBefore, battle.Actor(battle.Caster).skillLoadout.ActiveSkills[2].CooldownEndTimeMs);
        Assert.Equal(0f, battle.Services.Resolve<MobaShieldService>().GetTotalRemaining(battle.Caster));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Annie_E_shields_only_her_own_summon_and_the_resolved_hero(bool targetAlly)
    {
        using var battle = new Battle(3003, targetZ: 40);
        var ally = battle.Spawn("other_annie", 3003, 1, 3, 1);
        var summonPosition = new Vec3(0, 0, 12);
        var summons = battle.Services.Resolve<MobaSummonService>();
        Assert.True(summons.TrySummon(battle.Caster, 30030502, in summonPosition, Vec3.Forward, default, out var own));
        Assert.True(summons.TrySummon(ally, 30030502, in summonPosition, Vec3.Forward, default, out var other));
        battle.Set(battle.Caster, "MAGIC_ATTACK", 100);
        battle.Set(battle.Target, "MAGIC_DEFENSE", 0);
        battle.Setup.Tick(2);
        var manaBefore = battle.Setup.GetActorMana(battle.Caster);
        var hero = targetAlly ? ally : battle.Caster;
        var petBaseSpeed = new MobaAttrs(battle.Actor(own)).MoveSpeed;
        var shields = battle.Services.Resolve<MobaShieldService>();
        Assert.False(shields.GetPresentationSnapshot(own).HasShield);
        var time = battle.Services.Resolve<IFrameTime>();
        var frame = new FrameIndex(time.Frame.Value + 1);
        var actors = battle.Services.Resolve<MobaActorRegistry>();
        var input = new SkillInputEvent(3, SkillInputPhase.Press, targetActorId: targetAlly ? ally : 0,
            aimPos: Vec3.Zero, aimDir: Vec3.Forward, predictionKey: 37, targetFrame: frame.Value,
            inputSequence: 1, entityVersion: actors.GetEntityVersion(battle.Caster));
        var command = new PlayerInputCommand(frame, new PlayerId("alignment_caster"),
            MobaOpCodes.Input.SkillInput, SkillInputCodec.Serialize(in input));
        var submitted = battle.Services.Resolve<IMobaBattleInputPort>().Submit(frame, new[] { command });
        Assert.True(submitted.Succeeded, submitted.ToString());
        battle.Setup.Tick(2);

        Assert.Equal(manaBefore - 60, battle.Setup.GetActorMana(battle.Caster), .01f);
        Assert.Equal(140f, shields.GetTotalRemaining(hero), .01f);
        Assert.Equal(140f, shields.GetTotalRemaining(own), .01f);
        Assert.Equal(0f, shields.GetTotalRemaining(other));
        Assert.Equal(0f, shields.GetTotalRemaining(targetAlly ? battle.Caster : ally));
        Assert.True(new MobaAttrs(battle.Actor(own)).MoveSpeed > petBaseSpeed);

        var snapshots = new List<WorldStateSnapshot>();
        battle.Services.Resolve<IMobaBattleOutputPort>().CollectSnapshots(time.Frame, snapshots, 128);
        var shieldRows = snapshots.Where(snapshot => snapshot.OpCode == MobaOpCodes.Snapshot.ShieldState)
            .SelectMany(snapshot => MobaShieldStateSnapshotCodec.Deserialize(snapshot.Payload));
        foreach (var recipient in new[] { hero, own })
            Assert.Contains(shieldRows, row => row.ActorId == recipient && row.CurrentShield == 140f);
        var cueRows = snapshots.Where(snapshot => snapshot.OpCode == MobaOpCodes.Snapshot.PresentationCue)
            .SelectMany(snapshot => MobaPresentationCueSnapshotCodec.Deserialize(snapshot.Payload));
        foreach (var recipient in new[] { hero, own })
            Assert.Contains(cueRows, row => row.TemplateId == 90033004 && row.TargetActorId == recipient);

        var enemyHp = battle.Setup.GetActorHp(battle.Target);
        HitShield(battle, battle.Target, own, 5);
        Assert.Equal(140f, shields.GetTotalRemaining(hero), .01f);
        Assert.Equal(enemyHp - 65f, battle.Setup.GetActorHp(battle.Target), .01f);
        HitShield(battle, battle.Target, own, 5);
        Assert.Equal(enemyHp - 65f, battle.Setup.GetActorHp(battle.Target), .01f);
        HitShield(battle, battle.Target, hero, 5);
        Assert.Equal(enemyHp - 130f, battle.Setup.GetActorHp(battle.Target), .01f);

        battle.Setup.TickMilliseconds(2700);
        Assert.False(shields.GetPresentationSnapshot(hero).HasShield);
        Assert.False(shields.GetPresentationSnapshot(own).HasShield);
        Assert.Equal(petBaseSpeed, new MobaAttrs(battle.Actor(own)).MoveSpeed, .01f);
    }

    [Fact]
    public void Annie_E_owned_summon_shield_replays_after_restoring_the_precast_frame()
    {
        using var battle = new Battle(3003, targetZ: 40);
        var position = new Vec3(0, 0, 1);
        var summons = battle.Services.Resolve<MobaSummonService>();
        var summonCast = battle.Skills.TryCastBySlot(battle.Caster, 4, position, Vec3.Forward);
        Assert.True(summonCast.Success, summonCast.FailReason);
        battle.Set(battle.Caster, "MAGIC_ATTACK", 100);
        battle.Setup.TickMilliseconds(1600);
        var owned = new List<int>();
        Assert.True(summons.TryGetSummons(battle.Caster, owned, 30030502));
        var pet = Assert.Single(owned);
        var frame = battle.Services.Resolve<IFrameTime>().Frame;
        var coordinator = battle.CreateRollbackCoordinator();
        Assert.True(coordinator.TryCaptureAndStore(frame, out var capture), capture.ToString());
        var shields = battle.Services.Resolve<MobaShieldService>();
        var initialSpeed = new MobaAttrs(battle.Actor(pet)).MoveSpeed;

        var expected = Apply();
        Assert.Equal(140f, expected.HeroShield, .01f);
        Assert.Equal(140f, expected.PetShield, .01f);
        Assert.True(expected.PetSpeed > initialSpeed);

        battle.Restore(coordinator, frame);
        Assert.True(summons.TryGetSummons(battle.Caster, owned, 30030502));
        Assert.Equal(pet, Assert.Single(owned));
        Assert.False(shields.GetPresentationSnapshot(battle.Caster).HasShield);
        Assert.False(shields.GetPresentationSnapshot(pet).HasShield);
        Assert.Equal(initialSpeed, new MobaAttrs(battle.Actor(pet)).MoveSpeed);
        Assert.Equal(expected, Apply());

        (float HeroShield, float PetShield, float PetSpeed, float Mana) Apply()
        {
            var cast = battle.Skills.TryCastBySlot(battle.Caster, 3, Vec3.Zero, Vec3.Forward);
            Assert.True(cast.Success, cast.FailReason);
            battle.Setup.Tick(2);
            return (shields.GetTotalRemaining(battle.Caster), shields.GetTotalRemaining(pet),
                new MobaAttrs(battle.Actor(pet)).MoveSpeed, battle.Setup.GetActorMana(battle.Caster));
        }
    }

    [Theory]
    [InlineData("dead")]
    [InlineData("expiring")]
    [InlineData("pending")]
    public void Annie_E_skips_inactive_owned_summons(string state)
    {
        using var battle = new Battle(3003, targetZ: 40);
        var position = new Vec3(0, 0, 3);
        Assert.True(battle.Services.Resolve<MobaSummonService>().TrySummon(battle.Caster, 30030502,
            in position, Vec3.Forward, default, out var pet));
        var actor = battle.Actor(pet);
        var time = battle.Services.Resolve<IFrameTime>();
        if (state == "dead") battle.Set(pet, "hp", 0);
        if (state == "expiring") actor.ReplaceLifetime((long)System.MathF.Round(time.Time * 1000f));
        if (state == "pending") ActorLifecycleRequests.RequestDespawn(actor, time.Frame.Value, ActorDespawnReason.SummonManualRemove);
        var cast = battle.Skills.TryCastBySlot(battle.Caster, 3, Vec3.Zero, Vec3.Forward);
        Assert.True(cast.Success, cast.FailReason);
        battle.Setup.Tick(2);
        var shields = battle.Services.Resolve<MobaShieldService>();
        Assert.True(shields.GetPresentationSnapshot(battle.Caster).HasShield);
        Assert.False(shields.GetPresentationSnapshot(pet).HasShield);
    }

    [Fact]
    public void Annie_E_retaliates_from_the_grantor_once_per_attacker_even_on_a_breaking_hit()
    {
        using var battle = new Battle(3003);
        var ally = battle.Spawn("shield_ally", 3007, 1, 4);
        var secondEnemy = battle.Spawn("shield_enemy", 3007, 2, 5);
        battle.Set(battle.Caster, "MAGIC_ATTACK", 100);
        battle.Set(battle.Target, "MAGIC_DEFENSE", 0);
        battle.Set(secondEnemy, "MAGIC_DEFENSE", 0);
        var cast = battle.Skills.TryCastBySlot(battle.Caster, 3, new Vec3(0, 0, 4), Vec3.Forward, ally);
        Assert.True(cast.Success, cast.FailReason);
        battle.Setup.TickMilliseconds(400);
        battle.Set(battle.Caster, "MAGIC_ATTACK", 500);
        var shields = battle.Services.Resolve<MobaShieldService>();
        Assert.Equal(140f, shields.GetTotalRemaining(ally), .01f);

        var allyHp = battle.Setup.GetActorHp(ally);
        var enemyHp = battle.Setup.GetActorHp(battle.Target);
        var secondHp = battle.Setup.GetActorHp(secondEnemy);
        Assert.Equal(30f, HitShield(battle, battle.Target, ally, 30).ShieldAbsorb, .01f);
        Assert.Equal(allyHp, battle.Setup.GetActorHp(ally), .01f);
        Assert.Equal(enemyHp - 65f, battle.Setup.GetActorHp(battle.Target), .01f);
        HitShield(battle, battle.Target, ally, 30);
        Assert.Equal(enemyHp - 65f, battle.Setup.GetActorHp(battle.Target), .01f);

        Assert.Equal(80f, HitShield(battle, secondEnemy, ally, 90).ShieldAbsorb, .01f);
        Assert.Equal(0f, shields.GetTotalRemaining(ally), .01f);
        Assert.Equal(allyHp - 10f, battle.Setup.GetActorHp(ally), .01f);
        Assert.Equal(secondHp - 65f, battle.Setup.GetActorHp(secondEnemy), .01f);
        HitShield(battle, secondEnemy, ally, 10);
        Assert.Equal(secondHp - 65f, battle.Setup.GetActorHp(secondEnemy), .01f);
    }

    [Fact]
    public void Annie_E_shield_checkpoint_restores_attacker_eligibility_without_dealing_damage()
    {
        using var battle = new Battle(3003);
        var secondEnemy = battle.Spawn("second_enemy", 3007, 2, 5);
        battle.Set(battle.Target, "MAGIC_DEFENSE", 0);
        battle.Set(secondEnemy, "MAGIC_DEFENSE", 0);
        var cast = battle.Skills.TryCastBySlot(battle.Caster, 3, Vec3.Zero, Vec3.Forward);
        Assert.True(cast.Success, cast.FailReason);
        battle.Setup.TickMilliseconds(400);
        HitShield(battle, battle.Target, battle.Caster, 5);
        var provider = new MobaShieldRollbackProvider(battle.Services.Resolve<MobaActorRegistry>(),
            battle.Services.Resolve<MobaShieldService>());
        var frame = battle.Services.Resolve<IFrameTime>().Frame;
        var checkpoint = provider.Export(frame);
        var secondBefore = battle.Setup.GetActorHp(secondEnemy);
        HitShield(battle, secondEnemy, battle.Caster, 5);
        var secondAfter = battle.Setup.GetActorHp(secondEnemy);
        Assert.True(secondAfter < secondBefore);
        var firstAfter = battle.Setup.GetActorHp(battle.Target);

        provider.Import(frame, checkpoint);

        Assert.Equal(checkpoint, provider.Export(frame));
        Assert.Equal(secondAfter, battle.Setup.GetActorHp(secondEnemy), .01f);
        HitShield(battle, battle.Target, battle.Caster, 5);
        HitShield(battle, secondEnemy, battle.Caster, 5);
        Assert.Equal(firstAfter, battle.Setup.GetActorHp(battle.Target), .01f);
        Assert.Equal(secondAfter - (secondBefore - secondAfter), battle.Setup.GetActorHp(secondEnemy), .01f);
    }

    [Fact]
    public void Annie_E_full_frame_restore_replays_new_attacker_but_not_previously_consumed_eligibility()
    {
        using var battle = new Battle(3003);
        var secondEnemy = battle.Spawn("rollback_enemy", 3007, 2, 5);
        battle.Set(battle.Target, "MAGIC_DEFENSE", 0);
        battle.Set(secondEnemy, "MAGIC_DEFENSE", 0);
        var cast = battle.Skills.TryCastBySlot(battle.Caster, 3, Vec3.Zero, Vec3.Forward);
        Assert.True(cast.Success, cast.FailReason);
        battle.Setup.TickMilliseconds(400);
        HitShield(battle, battle.Target, battle.Caster, 5);
        var checkpointFrame = battle.Services.Resolve<IFrameTime>().Frame;
        var coordinator = battle.CreateRollbackCoordinator();
        Assert.True(coordinator.CaptureAndStore(checkpointFrame));
        var firstBefore = battle.Setup.GetActorHp(battle.Target);
        var secondBefore = battle.Setup.GetActorHp(secondEnemy);

        var baseline = Advance();
        Assert.Equal(firstBefore, baseline.First, .01f);
        Assert.True(baseline.Second < secondBefore);

        battle.Restore(coordinator, checkpointFrame);

        Assert.Equal(firstBefore, battle.Setup.GetActorHp(battle.Target), .01f);
        Assert.Equal(secondBefore, battle.Setup.GetActorHp(secondEnemy), .01f);
        Assert.Equal(baseline, Advance());

        (float First, float Second, float Shield) Advance()
        {
            battle.Setup.Tick(1);
            HitShield(battle, battle.Target, battle.Caster, 5);
            HitShield(battle, secondEnemy, battle.Caster, 5);
            return (battle.Setup.GetActorHp(battle.Target), battle.Setup.GetActorHp(secondEnemy),
                battle.Services.Resolve<MobaShieldService>().GetTotalRemaining(battle.Caster));
        }
    }

    [Fact]
    public void Annie_E_world_ports_consume_targeted_input_and_emit_shield_and_retaliation()
    {
        using var battle = new Battle(3003);
        var ally = battle.Spawn("state_sync_ally", 3007, 1, 4);
        battle.Set(battle.Caster, "MAGIC_ATTACK", 100f);
        battle.Set(battle.Target, "MAGIC_DEFENSE", 0f);
        var time = battle.Services.Resolve<IFrameTime>();
        var frame = new FrameIndex(time.Frame.Value + 1);
        var actors = battle.Services.Resolve<MobaActorRegistry>();
        var input = new SkillInputEvent(3, SkillInputPhase.Press, targetActorId: ally,
            aimPos: new Vec3(0, 0, 4), aimDir: Vec3.Forward, predictionKey: 37, targetFrame: frame.Value,
            inputSequence: 1, entityVersion: actors.GetEntityVersion(battle.Caster));
        var command = new PlayerInputCommand(frame, new PlayerId("alignment_caster"),
            MobaOpCodes.Input.SkillInput, SkillInputCodec.Serialize(in input));
        var submitted = battle.Services.Resolve<IMobaBattleInputPort>().Submit(frame, new[] { command });
        Assert.True(submitted.Succeeded, submitted.ToString());
        battle.Setup.TickMilliseconds(400);
        HitShield(battle, battle.Target, ally, 10f);
        var snapshots = new List<WorldStateSnapshot>();
        battle.Services.Resolve<IMobaBattleOutputPort>().CollectSnapshots(time.Frame, snapshots, 128);

        var shield = Assert.Single(snapshots, snapshot => snapshot.OpCode == MobaOpCodes.Snapshot.ShieldState);
        var value = Assert.Single(MobaShieldStateSnapshotCodec.Deserialize(shield.Payload), entry => entry.ActorId == ally);
        Assert.Equal(130f, value.CurrentShield, .01f);
        var damage = Assert.Single(snapshots, snapshot => snapshot.OpCode == MobaOpCodes.Snapshot.DamageEvent);
        var retaliation = Assert.Single(MobaDamageEventSnapshotCodec.Deserialize(damage.Payload), entry =>
            entry.AttackerActorId == battle.Caster && entry.TargetActorId == battle.Target);
        Assert.Equal((int)DamageType.Magic, retaliation.DamageType);
        Assert.Equal(65f, retaliation.Value, .01f);
    }

    [Fact]
    public void Annie_E_retaliation_can_be_absorbed_without_triggering_another_retaliation()
    {
        using var battle = new Battle(3003);
        var opposingAnnie = battle.Spawn("opposing_annie", 3003, 2, 4);
        foreach (var actorId in new[] { opposingAnnie, battle.Caster })
        {
            battle.Set(actorId, "MAGIC_ATTACK", 100f);
            battle.Set(actorId, "MAGIC_DEFENSE", 0f);
            var cast = battle.Skills.TryCastBySlot(actorId, 3, Vec3.Zero, Vec3.Forward);
            Assert.True(cast.Success, cast.FailReason);
        }
        battle.Setup.TickMilliseconds(400);
        HitShield(battle, opposingAnnie, battle.Caster, 10f);
        var shields = battle.Services.Resolve<MobaShieldService>();
        Assert.Equal(130f, shields.GetTotalRemaining(battle.Caster), .01f);
        Assert.Equal(75f, shields.GetTotalRemaining(opposingAnnie), .01f);
    }

    [Fact]
    public void Annie_E_rejected_damage_does_not_consume_retaliation_eligibility()
    {
        using var battle = new Battle(3003);
        battle.Set(battle.Caster, "MAGIC_ATTACK", 100f);
        battle.Set(battle.Target, "MAGIC_DEFENSE", 0f);
        var cast = battle.Skills.TryCastBySlot(battle.Caster, 3, Vec3.Zero, Vec3.Forward);
        Assert.True(cast.Success, cast.FailReason);
        battle.Setup.TickMilliseconds(400);
        var transactions = battle.Services.Resolve<MobaCombatTransactionPipeline>();
        var rejectedHit = new RejectDamageBeforeCommit();
        var attack = CreateShieldHit(battle.Target, battle.Caster, 10f);
        transactions.Register(rejectedHit);
        var enemyHp = battle.Setup.GetActorHp(battle.Target);
        try
        {
            Assert.Null(battle.Services.Resolve<DamagePipelineService>().Execute(attack));
            Assert.Equal(140f, battle.Services.Resolve<MobaShieldService>().GetTotalRemaining(battle.Caster), .01f);
            Assert.Equal(enemyHp, battle.Setup.GetActorHp(battle.Target), .01f);
        }
        finally
        {
            transactions.Unregister(rejectedHit);
        }
        HitShield(battle, battle.Target, battle.Caster, 10f);
        Assert.Equal(enemyHp - 65f, battle.Setup.GetActorHp(battle.Target), .01f);
    }

    private sealed class RejectDamageBeforeCommit : IMobaCombatTransactionInterceptor<MobaDamageTransaction>
    {
        public void OnStage(MobaDamageTransaction transaction, MobaCombatTransactionStage stage,
            in MobaCombatTransactionContext context)
        {
            if (stage == MobaCombatTransactionStage.Commit) transaction.Cancel("test rejects original damage");
        }
    }

    private static DamageResult HitShield(Battle battle, int attacker, int target, float amount)
    {
        var result = battle.Services.Resolve<DamagePipelineService>().Execute(CreateShieldHit(attacker, target, amount));
        Assert.NotNull(result);
        return result;
    }

    private static AttackInfo CreateShieldHit(int attacker, int target, float amount)
    {
        var attack = new AttackInfo
        {
            AttackerActorId = attacker,
            TargetActorId = target,
            DamageType = DamageType.True,
            ReasonKind = DamageReasonKind.Skill,
        };
        attack.BaseDamage.BaseValue = amount;
        return attack;
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
    [InlineData(0)]
    [InlineData(100)]
    public void Jinx_Q_switches_the_next_basic_attack_in_both_directions_without_paying_or_firing(int mana)
    {
        using var battle = new Battle(3002);
        battle.Set(battle.Caster, "mana", mana);
        battle.Setup.Tick(2);

        var loadout = battle.Services.Resolve<MobaSkillLoadoutService>();
        var projectiles = battle.Services.Resolve<AbilityKit.Combat.Projectile.IProjectileService>();
        var targetHp = battle.Setup.GetActorHp(battle.Target);
        Assert.True(loadout.TryResolveSlot(battle.Caster, 5, out var initialAttack));
        Assert.Equal(30020002, initialAttack.EffectiveSkillId);

        foreach (var expectedAttack in new[] { 30020003, 30020002 })
        {
            var cast = battle.Skills.TryCastBySlot(battle.Caster, 1, Vec3.Zero, Vec3.Forward);
            Assert.True(cast.Success, cast.FailReason);
            for (var tick = 0; tick < 65; tick++)
            {
                battle.Setup.Tick(1);
                Assert.Equal(0, projectiles.ActiveCount);
            }
            Assert.True(loadout.TryResolveSlot(battle.Caster, 5, out var nextAttack));
            Assert.Equal(expectedAttack, nextAttack.EffectiveSkillId);
            Assert.Equal((float)mana, battle.Setup.GetActorMana(battle.Caster), .01f);
            Assert.Equal(targetHp, battle.Setup.GetActorHp(battle.Target), .01f);
        }
    }

    [Fact]
    public void Jinx_Fishbones_uses_Q_level_range_for_explicit_targets_without_retargeting()
    {
        using var battle = new Battle(3002, targetZ: 7.7f);
        battle.Spawn("near", 3007, 2, 4, 3);
        var far = battle.Spawn("far", 3007, 2, 8.05f);
        var q = battle.Actor(battle.Caster).skillLoadout.ActiveSkills[0];
        q.Level = 1;
        Assert.False(battle.Skills.TryCastBySlot(battle.Caster, 5, default, Vec3.Forward, battle.Target).Success);
        Assert.True(battle.Skills.TryCastBySlot(battle.Caster, 1, default, Vec3.Forward).Success);
        battle.Setup.Tick(65);
        Assert.False(battle.Skills.TryCastBySlot(battle.Caster, 5, default, Vec3.Forward, battle.Target).Success);

        q.Level = 5;
        Assert.False(battle.Skills.TryCastBySlot(battle.Caster, 5, default, Vec3.Forward, far).Success);
        var attack = battle.Skills.TryCastBySlot(battle.Caster, 5, default, Vec3.Forward, battle.Target);
        Assert.True(attack.Success, attack.FailReason);
        var projectiles = battle.Services.Resolve<AbilityKit.Combat.Projectile.IProjectileService>();
        for (var tick = 0; tick < 20 && projectiles.ActiveCount == 0; tick++) battle.Setup.Tick(1);
        Assert.Equal(1, projectiles.ActiveCount);
        var targetHp = battle.Setup.GetActorHp(battle.Target);
        battle.Setup.Tick(65);
        Assert.True(battle.Setup.GetActorHp(battle.Target) < targetHp);
    }

    [Fact]
    public void Jinx_Fishbones_automatic_search_uses_the_same_Q_range()
    {
        using var automatic = new Battle(3002, targetZ: 6.7f);
        automatic.Actor(automatic.Caster).skillLoadout.ActiveSkills[0].Level = 1;
        Assert.False(automatic.Skills.TryCastBySlot(automatic.Caster, 5, default, Vec3.Forward).Success);
        Assert.True(automatic.Skills.TryCastBySlot(automatic.Caster, 1, default, Vec3.Forward).Success);
        automatic.Setup.Tick(65);
        Assert.True(automatic.Skills.TryCastBySlot(automatic.Caster, 5, default, Vec3.Forward).Success);
    }

    [Fact]
    public void Jinx_basic_attacks_share_a_launch_deadline_across_switch_and_sample_attack_speed()
    {
        using var battle = new Battle(3002, targetZ: 5);
        var time = battle.Services.Resolve<IFrameTime>();
        var slot = battle.Actor(battle.Caster).skillLoadout.ActiveSkills[4];
        var loadout = battle.Services.Resolve<MobaSkillLoadoutService>();
        battle.Set(battle.Caster, "ATTACK_SPEED_R", 0);
        var first = Fire();
        Assert.Equal(800, slot.CooldownDurationMs);
        var firstDeadline = slot.CooldownEndTimeMs;
        Assert.True(battle.Skills.TryCastBySlot(battle.Caster, 1, default, Vec3.Forward).Success);
        battle.Setup.Tick(3);
        Assert.True(loadout.TryResolveSlot(battle.Caster, 5, out var rocket));
        Assert.Equal(30020003, rocket.EffectiveSkillId);
        Assert.Equal(firstDeadline, slot.CooldownEndTimeMs);
        Assert.False(TryAttack().Success);

        battle.Set(battle.Caster, "ATTACK_SPEED_R", 100);
        var second = Fire();
        Assert.InRange(second - first, 800L, 866L);
        Assert.Equal(400, slot.CooldownDurationMs);
        var third = Fire();
        Assert.InRange(third - second, 400L, 466L);

        MobaSkillCastResult TryAttack() => battle.Skills.TryCastBySlot(battle.Caster, 5,
            default, Vec3.Forward, battle.Target);

        long Fire()
        {
            var previousDeadline = slot.CooldownEndTimeMs;
            for (var tick = 0; tick < 80; tick++)
            {
                if (TryAttack().Success)
                {
                    Assert.False(TryAttack().Success);
                    for (var windup = 0; windup < 20; windup++)
                    {
                        battle.Setup.Tick(1);
                        if (slot.CooldownEndTimeMs > previousDeadline)
                            return MobaSkillRuntimeAccess.GetCurrentTimeMs(time);
                    }
                    Assert.Fail("Accepted attack never launched.");
                }
                battle.Setup.Tick(1);
            }
            throw new InvalidOperationException("No attack admitted within the test window.");
        }
    }

    [Fact]
    public void Jinx_attack_deadline_remains_authoritative_after_frame_restore()
    {
        using var battle = new Battle(3002, targetZ: 2);
        battle.Set(battle.Caster, "ATTACK_SPEED_R", 0);
        Assert.True(Attack().Success);
        battle.Setup.TickMilliseconds(400);
        var projectiles = battle.Services.Resolve<AbilityKit.Combat.Projectile.IProjectileService>();
        Assert.Equal(0, projectiles.ActiveCount);
        var time = battle.Services.Resolve<IFrameTime>();
        var checkpoint = time.Frame;
        var coordinator = battle.CreateRollbackCoordinator();
        Assert.True(coordinator.CaptureAndStore(checkpoint));
        var expectedDeadline = Deadline();
        Assert.True(expectedDeadline > MobaSkillRuntimeAccess.GetCurrentTimeMs(time) + 100);

        battle.Setup.Tick(2);
        Assert.False(Attack().Success);
        battle.Restore(coordinator, checkpoint);
        Assert.Equal(expectedDeadline, Deadline());
        battle.Setup.Tick(2);
        Assert.False(Attack().Success);

        for (var tick = 0; tick < 24 && Deadline() == expectedDeadline; tick++)
        {
            Attack();
            battle.Setup.Tick(1);
        }
        Assert.True(Deadline() >= expectedDeadline + 800);

        MobaSkillCastResult Attack() => battle.Skills.TryCastBySlot(battle.Caster, 5, default, Vec3.Forward, battle.Target);
        long Deadline() => battle.Actor(battle.Caster).skillLoadout.ActiveSkills[4].CooldownEndTimeMs;
    }

    [Fact]
    public void Jinx_basic_attack_ports_emit_shared_cooldown_and_variant_after_switch()
    {
        using var battle = new Battle(3002, targetZ: 5);
        battle.Set(battle.Caster, "ATTACK_SPEED_R", 0);
        var time = battle.Services.Resolve<IFrameTime>();
        var actors = battle.Services.Resolve<MobaActorRegistry>();
        var inputPort = battle.Services.Resolve<IMobaBattleInputPort>();
        var outputPort = battle.Services.Resolve<IMobaBattleOutputPort>();
        var slot = battle.Actor(battle.Caster).skillLoadout.ActiveSkills[4];
        var frame = new FrameIndex(time.Frame.Value + 1);
        var input = new SkillInputEvent(5, SkillInputPhase.Press, targetActorId: battle.Target,
            aimPos: new Vec3(0, 0, 5), aimDir: Vec3.Forward, predictionKey: 38, targetFrame: frame.Value,
            inputSequence: 1, entityVersion: actors.GetEntityVersion(battle.Caster));
        var command = new PlayerInputCommand(frame, new PlayerId("alignment_caster"),
            MobaOpCodes.Input.SkillInput, SkillInputCodec.Serialize(in input));
        Assert.True(inputPort.Submit(frame, new[] { command }).Succeeded);
        for (var tick = 0; tick < 15 && slot.CooldownEndTimeMs == 0; tick++) battle.Setup.Tick(1);
        Assert.True(slot.CooldownEndTimeMs > 0);
        var deadline = slot.CooldownEndTimeMs;
        Assert.True(battle.Skills.TryCastBySlot(battle.Caster, 1, default, Vec3.Forward).Success);
        battle.Setup.Tick(3);
        var snapshots = new List<WorldStateSnapshot>();
        outputPort.CollectSnapshots(time.Frame, snapshots, 128);
        var skillState = Assert.Single(snapshots, snapshot => snapshot.OpCode == MobaOpCodes.Snapshot.SkillState);
        var attack = Assert.Single(MobaSkillStateSnapshotCodec.Deserialize(skillState.Payload),
            entry => entry.ActorId == battle.Caster && entry.Slot == 5);
        Assert.Equal(30020003, attack.SkillId);
        Assert.Equal(deadline, attack.CooldownEndTimeMs);
        Assert.Equal(800, attack.CooldownTotalMs);
        Assert.True(attack.CooldownRemainingMs > 0);
    }

    [Fact]
    public void Jinx_Fishbones_hits_the_primary_and_nearby_enemies_once()
    {
        using var battle = new Battle(3002, targetZ: 4);
        var secondary = battle.Spawn("splash_enemy", 3007, 2, 4, 1.5f);
        var outside = battle.Spawn("outside_enemy", 3007, 2, 4, 3);
        var ally = battle.Spawn("splash_ally", 3007, 1, 4, -1.5f);
        foreach (var actor in new[] { battle.Target, secondary, outside, ally })
            battle.Set(actor, "PHYSICS_DEFENSE", 0);
        battle.Set(battle.Caster, "PHYSICS_ATTACK", 100);
        battle.Set(battle.Caster, "EXTRA_PHYSICS_ATTACK", 0);
        battle.Set(battle.Caster, "mana", 100);
        battle.Setup.Tick(2);
        var primaryHp = battle.Setup.GetActorHp(battle.Target);
        var secondaryHp = battle.Setup.GetActorHp(secondary);
        var outsideHp = battle.Setup.GetActorHp(outside);
        var allyHp = battle.Setup.GetActorHp(ally);

        var toggle = battle.Skills.TryCastBySlot(battle.Caster, 1, Vec3.Zero, Vec3.Forward);
        Assert.True(toggle.Success, toggle.FailReason);
        battle.Setup.Tick(65);
        var attack = battle.Skills.TryCastBySlot(battle.Caster, 5, new Vec3(0, 0, 4), Vec3.Forward, battle.Target);
        Assert.True(attack.Success, attack.FailReason);
        var projectiles = battle.Services.Resolve<AbilityKit.Combat.Projectile.IProjectileService>();
        for (var tick = 0; tick < 20 && projectiles.ActiveCount == 0; tick++) battle.Setup.Tick(1);
        Assert.Equal(1, projectiles.ActiveCount);
        Assert.Equal(primaryHp, battle.Setup.GetActorHp(battle.Target), .01f);
        battle.Set(battle.Caster, "PHYSICS_ATTACK", 200);
        battle.Setup.Tick(60);

        Assert.Equal(110f, primaryHp - battle.Setup.GetActorHp(battle.Target), .02f);
        Assert.Equal(110f, secondaryHp - battle.Setup.GetActorHp(secondary), .02f);
        Assert.Equal(outsideHp, battle.Setup.GetActorHp(outside), .01f);
        Assert.Equal(allyHp, battle.Setup.GetActorHp(ally), .01f);
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

    [Fact]
    public void Jhin_W_roots_the_marked_hero_but_only_damages_marked_nonheroes_on_its_path()
    {
        using var battle = new Battle(3001, targetZ: 8);
        var minion = battle.Spawn("marked_minion", 3007, 2, 3f);
        var monster = battle.Spawn("marked_monster", 3007, 2, 5f);
        battle.Actor(minion).ReplaceUnitSubType(UnitSubType.Minion);
        battle.Actor(monster).ReplaceUnitSubType(UnitSubType.Neutral);
        foreach (var target in new[] { minion, monster, battle.Target })
        {
            battle.Set(target, "MAX_HP", 5000);
            battle.Set(target, "hp", 5000);
            battle.Setup.AddScenarioBuff(target, 30010221, battle.Caster, 4000);
            Assert.Equal(1, battle.Setup.CountActorBuffs(target, 30010221));
        }

        var cast = battle.Skills.TryCastBySlot(battle.Caster, 2, new Vec3(0, 0, 8), Vec3.Forward);
        Assert.True(cast.Success, cast.FailReason);
        for (var tick = 0; tick < 60 && battle.Setup.GetActorHp(battle.Target) == 5000f; tick++)
            battle.Setup.Tick(1);

        foreach (var target in new[] { minion, monster, battle.Target })
            Assert.True(battle.Setup.GetActorHp(target) < 5000f);
        Assert.Equal(0, battle.Setup.CountActorBuffs(minion, 30010222));
        Assert.Equal(0, battle.Setup.CountActorBuffs(monster, 30010222));
        Assert.Equal(1, battle.Setup.CountActorBuffs(battle.Target, 30010222));
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

        public RollbackCoordinator CreateRollbackCoordinator()
        {
            var world = (IWorld)typeof(ConsoleBattleBootstrapper)
                .GetField("_runtimeWorld", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_bootstrapper)!;
            return new RollbackCoordinator(MobaRollbackRegistryBuilder.Create(world), new RollbackSnapshotRingBuffer(2));
        }

        public void Restore(RollbackCoordinator coordinator, FrameIndex frame) =>
            Assert.True(_bootstrapper.TryRestoreSimulationFrame(coordinator, frame, out var result), result.ToString());

        public void Set(int actor, string attribute, float value) => Setup.SetScenarioActorAttribute(actor, attribute, value);
        public void Dispose() => _bootstrapper.Dispose();
    }
}

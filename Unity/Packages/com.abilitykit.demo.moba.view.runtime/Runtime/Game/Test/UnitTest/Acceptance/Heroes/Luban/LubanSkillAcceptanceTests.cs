using AbilityKit.Core.Mathematics;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Demo.Moba.View.Abstractions.Shared.Types;
using AbilityKit.Demo.Moba.Share;
using NUnit.Framework;
using System.Collections.Generic;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class LubanSkillAcceptanceTests : MobaAcceptanceTestBase
    {
        private static readonly HeroSkillContract Luban = new HeroSkillContract(
            "Luban", 1008, 1008, new[] { 10080101, 10080201, 10080301 });
        private static readonly HeroSkillSlotContract Skill1 = new HeroSkillSlotContract(1, 10080101, 10080101, 10080101);
        private static readonly HeroSkillSlotContract Skill2 = new HeroSkillSlotContract(2, 10080201, 10080201, 10080201);
        private static readonly HeroSkillSlotContract Skill3 = new HeroSkillSlotContract(3, 10080301, 10080301, 10080301);

        [Test]
        public void LubanSkills_ShouldExposeGrenadeCannonAndAirSupportIndicators()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Luban, "luban_skill_indicator_contract_world"))
            {
                AssertIndicator(harness, 10080101, 14, 2, 3.5f, expectPoint: true);
                AssertIndicator(harness, 10080201, 15, 1, 1.2f, expectPoint: false);
                AssertIndicator(harness, 10080301, 16, 5, 4.5f, expectPoint: false);
            }
        }

        private static void AssertIndicator(MobaSkillConfigTestHarness harness, int skillId, int templateId, int shape, float width, bool expectPoint)
        {
            Assert.IsTrue(harness.Config.TryGetSkill(skillId, out var skill));
            Assert.AreEqual(templateId, skill.SkillButtonTemplateId);
            Assert.IsTrue(harness.Config.TryGetSkillButtonTemplate(templateId, out var template));
            Assert.AreEqual(shape, template.IndicatorShape);
            Assert.AreEqual(width, template.IndicatorWorldWidth);
            Assert.AreEqual(expectPoint ? 2 : 1, template.UsePointMode);
            if (skillId == 10080301)
            {
                Assert.AreEqual(0, template.AimMode,
                    "Luban air support must submit a direction, not a target point.");
                Assert.IsTrue(template.FaceToAim,
                    "Luban airship must face the submitted flight direction.");
            }
            Assert.IsTrue(template.EnableAim);
        }

        [Test]
        public void Passive10080000_ShouldCountReleasedBasicAttacksEvenWhenTheyMiss()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Luban, "luban_passive_sweep_contract_world"))
            {
                Assert.IsTrue(harness.Config.TryGetPassiveSkill(10080000, out _));
                Assert.IsTrue(harness.Config.TryGetProjectile(30080001, out var basicProjectile));
                Assert.AreEqual(ProjectileHitPolicyKind.ExitOnHit, basicProjectile.HitPolicyKind,
                    "Luban normal attacks must resolve against one target rather than pierce the lane.");
                Assert.AreEqual(1, basicProjectile.HitsRemaining,
                    "Luban normal attacks should consume a single projectile hit.");
                Assert.AreEqual(10080012, basicProjectile.OnHitEffectId,
                    "Luban basic projectile must resolve the damage trigger rather than recursively firing another projectile.");
                Assert.IsTrue(harness.Config.TryGetProjectile(30080301, out var sweepProjectile));
                Assert.AreEqual(10080004, sweepProjectile.OnHitEffectId,
                    "Luban sweep projectile must resolve the sweep damage trigger rather than recursively firing another sweep.");
                Assert.AreEqual(-1, sweepProjectile.HitsRemaining,
                    "Luban sweep projectiles must pierce all targets in the fan for area damage.");
                Assert.IsTrue(harness.Config.TryGetProjectileLauncher(31080301, out var sweepLauncher));
                Assert.AreEqual(3, sweepLauncher.CountPerShot,
                    "Luban passive sweep should emit three projectiles.");
                Assert.AreEqual(35f, sweepLauncher.FanAngleDeg,
                    "Luban passive sweep should use a spread fan rather than a single straight projectile.");
                HeroSkillHeadlessContract.AssertTriggerActions(harness, 10080002,
                    (int)TriggeringConstants.AdvanceGameplayCounterId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, 10080003,
                    (int)TriggeringConstants.ShootProjectileId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, 10080005,
                    (int)TriggeringConstants.AdvanceGameplayCounterId.Value);
                harness.EnterGameAndWarmup(reason: "luban passive fifth basic attack sweep contract");
                var actorId = harness.AssertPlayerActorBound();
                const int basicAttackSlot = 4;
                harness.AssertSlotSkill(basicAttackSlot, 10080001);
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 0f, z: 3f);
                for (var i = 0; i < 4; i++)
                {
                    var cast = skills.TryCastBySlot(actorId, basicAttackSlot, aimPos: default, aimDir: Vec3.Right, targetActorId: targetActorId);
                    Assert.IsTrue(cast.Success, $"Luban basic attack {i + 1} should release successfully. failReason={cast.FailReason}");
                    harness.TickUntilSkillStops(actorId, basicAttackSlot, maxTicks: 20, message: $"Luban basic attack {i + 1} should finish its release.");
                    harness.Tick(50);
                    var counters = harness.World.Services.Resolve<MobaPresentationCueSnapshotService>();
                    Assert.IsTrue(counters.TryGetActiveCue($"gameplay-counter:{actorId}:10080001", out var counterCue),
                        $"Luban should publish a persistent counter cue after released basic attack {i + 1}.");
                    Assert.AreEqual(i + 1, counterCue.Entry.StackCount,
                        $"Luban counter cue should display {i + 1}/5 after released basic attack {i + 1}.");
                }
                var sweepBeforeFifth = false;
                foreach (var node in harness.Trace.GetNodesByKind((int)MobaExecutionKind.EffectExecution))
                {
                    if (node.Metadata?.ConfigId == 10080003) sweepBeforeFifth = true;
                }
                Assert.IsFalse(sweepBeforeFifth, "Luban passive must not sweep before the fifth released basic attack.");
                var fifthCast = skills.TryCastBySlot(actorId, basicAttackSlot, aimPos: default, aimDir: Vec3.Right, targetActorId: targetActorId);
                Assert.IsTrue(fifthCast.Success, $"Luban fifth basic attack should release successfully. failReason={fifthCast.FailReason}");
                var trace = harness.TickUntilTraceNode(MobaExecutionKind.EffectExecution, 10080003, 10,
                    "Luban fifth released basic attack should execute the sweep trigger even though the previous attacks missed.");
                Assert.IsFalse(harness.World.Services.Resolve<MobaPresentationCueSnapshotService>()
                        .TryGetActiveCue($"gameplay-counter:{actorId}:10080001", out _),
                    "The counter cue should be removed after the fifth attack consumes the sweep counter.");
                harness.AssertActionExecutedUnderEffect(trace.RootId,
                    (int)TriggeringConstants.ShootProjectileId.Value, TriggeringConstants.Actions.ShootProjectile);
                harness.AssertProjectileLaunchedUnderEffect(trace.RootId, 31080301, 30080301);
                Assert.AreEqual(3, TickAndCountProjectileSpawns(harness, 30080301, 30),
                    "Luban passive sweep should create all three projectiles in its fan.");

                for (var i = 0; i < 4; i++)
                {
                    var cast = skills.TryCastBySlot(actorId, basicAttackSlot, aimPos: default, aimDir: Vec3.Right, targetActorId: targetActorId);
                    Assert.IsTrue(cast.Success, $"Luban post-sweep basic attack {i + 1} should release successfully. failReason={cast.FailReason}");
                    harness.TickUntilSkillStops(actorId, basicAttackSlot, maxTicks: 20, message: $"Luban post-sweep basic attack {i + 1} should finish its release.");
                    harness.Tick(50);
                }

                Assert.AreEqual(1, CountSweepExecutions(harness),
                    "The sweep replacement attack must not advance the normal-basic-attack counter; four following normal attacks are still below the next threshold.");

                var tenthCastBaseline = harness.CaptureTraceBaseline();
                var tenthCast = skills.TryCastBySlot(actorId, basicAttackSlot, aimPos: default, aimDir: Vec3.Right, targetActorId: targetActorId);
                Assert.IsTrue(tenthCast.Success, $"Luban tenth basic attack should release successfully. failReason={tenthCast.FailReason}");
                var secondSweep = harness.TickUntilTraceNodeAfter(tenthCastBaseline, MobaExecutionKind.EffectExecution, 10080003, 10,
                    "Luban should trigger its next sweep on the fifth normal basic attack after the previous sweep.");
                harness.AssertActionExecutedUnderEffect(secondSweep.RootId,
                    (int)TriggeringConstants.ShootProjectileId.Value, TriggeringConstants.Actions.ShootProjectile);
            }
        }

        [Test]
        public void Passive10080000_ShouldPrimeSweepAfterSkillCast()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Luban, "luban_passive_skill_prime_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "luban passive skill prime contract");
                var actorId = harness.AssertPlayerActorBound();
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 0f, z: 3f);

                var skillCast = skills.TryCastBySlot(actorId, 1, aimPos: default, aimDir: Vec3.Right, targetActorId: targetActorId);
                Assert.IsTrue(skillCast.Success, $"Luban skill 1 should release successfully. failReason={skillCast.FailReason}");
                harness.TickUntilSkillStops(actorId, 1, maxTicks: 30,
                    message: "Luban skill 1 should finish before the primed basic attack.");
                harness.Tick(5);
                Assert.AreEqual(0, CountSweepExecutions(harness),
                    "Priming from a skill cast must not fire the sweep immediately.");

                var baseline = harness.CaptureTraceBaseline();
                var basicCast = skills.TryCastBySlot(actorId, 4, aimPos: default, aimDir: Vec3.Right, targetActorId: targetActorId);
                Assert.IsTrue(basicCast.Success, $"Luban primed basic attack should release successfully. failReason={basicCast.FailReason}");
                var sweep = harness.TickUntilTraceNodeAfter(baseline, MobaExecutionKind.EffectExecution, 10080003, 20,
                    "The first basic attack after a skill cast should consume the primed sweep.");
                harness.AssertActionExecutedUnderEffect(sweep.RootId,
                    (int)TriggeringConstants.ShootProjectileId.Value, TriggeringConstants.Actions.ShootProjectile);
            }
        }

        [Test]
        public void BasicAttack_ShouldSpawnDedicatedBulletAndPublishCounterCue()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Luban, "luban_basic_attack_presentation_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "luban basic attack presentation contract");
                var actorId = harness.AssertPlayerActorBound();
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, actorId: 9011, x: 0f, z: 4f);
                var cast = skills.TryCastBySlot(actorId, 4, aimPos: default, aimDir: Vec3.Forward, targetActorId: targetActorId);
                Assert.IsTrue(cast.Success, "Luban basic attack should release successfully. failReason=" + cast.FailReason);

                var spawn = TickUntilProjectileSnapshot(harness, 30080001, ProjectileEventKind.Spawn, 30);
                Assert.AreEqual(90008001, new BattleProjectileVfxResolver().ResolveSnapshotVfxId(
                    spawn.TemplateId, (ProjectilePresentationEventKind)spawn.Kind),
                    "Luban basic attack should resolve the dedicated bullet VFX.");

                var counter = TickUntilCounterCue(harness, actorId, 10080001, 1, 30);
                Assert.AreEqual(1, counter.StackCount);
                Assert.AreEqual(5, counter.MaxStackCount);
                Assert.AreEqual(actorId, counter.TargetActorId);
            }
        }

        [Test]
        public void Passive10080000_ShouldDamageMultipleTargetsWithSweepFan()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Luban, "luban_passive_sweep_multi_target_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "luban sweep multi target contract");
                var actorId = harness.AssertPlayerActorBound();
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var left = HeroSkillHeadlessContract.SpawnEnemyHero(harness, actorId: 9011, x: -1.1f, z: 4f);
                var right = HeroSkillHeadlessContract.SpawnEnemyHero(harness, actorId: 9012, x: 1.1f, z: 4f);
                var leftHp = harness.GetActorHp(left);
                var rightHp = harness.GetActorHp(right);

                for (var i = 0; i < 5; i++)
                {
                    var cast = skills.TryCastBySlot(actorId, 4, aimPos: default, aimDir: Vec3.Forward, targetActorId: 0);
                    Assert.IsTrue(cast.Success, $"Luban basic attack {i + 1} should release successfully. failReason={cast.FailReason}");
                    harness.TickUntilSkillStops(actorId, 4, 20, "Luban basic attack should finish before the next shot.");
                    harness.Tick(4);
                }

                harness.Tick(35);
                Assert.Less(harness.GetActorHp(left), leftHp, "Luban sweep should damage the left target in its fan.");
                Assert.Less(harness.GetActorHp(right), rightHp, "Luban sweep should damage the right target in its fan.");
            }
        }

        [Test]
        public void Skill10080201_ShouldHitMultipleTargetsAndScaleKnockbackByDistance()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Luban, "luban_skill_2_multi_target_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "luban cannon multi target contract");
                var actorId = harness.AssertPlayerActorBound();
                var near = HeroSkillHeadlessContract.SpawnEnemyHero(harness, actorId: 9011, x: 0f, z: -4f);
                var far = HeroSkillHeadlessContract.SpawnEnemyHero(harness, actorId: 9012, x: 0f, z: -8f);
                var nearHp = harness.GetActorHp(near);
                var farHp = harness.GetActorHp(far);
                var nearStart = harness.AssertActorEntity(near).transform.Value.Position;
                var farStart = harness.AssertActorEntity(far).transform.Value.Position;
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(actorId, Skill2.Slot, aimPos: default, aimDir: new Vec3(0f, 0f, -8f), targetActorId: near);
                Assert.IsTrue(cast.Success, "Luban cannon should release successfully. failReason=" + cast.FailReason);

                harness.Tick(60);
                Assert.Less(harness.GetActorHp(near), nearHp, "Luban cannon should damage the near target.");
                Assert.Less(harness.GetActorHp(far), farHp, "Luban cannon should continue through and damage the far target.");
                var nearEnd = harness.AssertActorEntity(near).transform.Value.Position;
                var farEnd = harness.AssertActorEntity(far).transform.Value.Position;
                Assert.Less(nearEnd.Z, nearStart.Z - 0.1f, "Near cannon target should be knocked away from the caster.");
                Assert.Less(farEnd.Z, farStart.Z - 0.1f, "Far cannon target should also be knocked away from the caster.");
                Assert.Greater(nearStart.Z - nearEnd.Z, farStart.Z - farEnd.Z + 0.02f,
                    "Cannon knockback should be stronger for targets closer to Luban.");
            }
        }

        private static int CountSweepExecutions(MobaSkillConfigTestHarness harness)
        {
            var count = 0;
            foreach (var node in harness.Trace.GetNodesByKind((int)MobaExecutionKind.EffectExecution))
            {
                if (node.Metadata?.ConfigId == 10080003) count++;
            }

            return count;
        }

        private static MobaPresentationCueSnapshotEntry TickUntilCounterCue(
            MobaSkillConfigTestHarness harness,
            int actorId,
            int keyId,
            int expectedStack,
            int maxTicks)
        {
            for (var tick = 0; tick <= maxTicks; tick++)
            {
                var cues = harness.World.Services.Resolve<MobaPresentationCueSnapshotService>();
                if (cues.TryGetActiveCue($"gameplay-counter:{actorId}:{keyId}", out var active) &&
                    active.Entry.StackCount == expectedStack)
                {
                    return active.Entry;
                }

                if (tick < maxTicks) harness.Tick(1);
            }

            Assert.Fail($"Counter cue missing. actor={actorId}, key={keyId}, stack={expectedStack}, maxTicks={maxTicks}");
            return default;
        }

        [Test]
        public void Skill10080101_ShouldLaunchGrenadeAndSpawnSlowArea()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Luban, "luban_skill_1_grenade_contract_world"))
            {
                Assert.IsTrue(harness.Config.TryGetAoe(40080101, out var grenadeArea));
                Assert.AreEqual(3.5f, grenadeArea.Radius, 0.01f);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, Skill1.EffectId,
                    (int)TriggeringConstants.ShootProjectileId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, 10080111,
                    (int)TriggeringConstants.SpawnAreaId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, 10080112,
                    (int)TriggeringConstants.GiveDamageId.Value,
                    (int)TriggeringConstants.AddBuffId.Value);
                harness.EnterGameAndWarmup(reason: "luban grenade area contract");
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 3f);
                var targetHp = harness.GetActorHp(targetActorId);
                var effect = HeroSkillHeadlessContract.CastSlotAndAssertEffect(harness, Skill1,
                    "luban grenade area contract", aimDir: Vec3.Right);
                harness.AssertProjectileLaunchedUnderEffect(effect.RootId, 31080101, 30080101);
                harness.TickUntilTraceNode(MobaExecutionKind.AreaSpawn, 40080101, 40,
                    "Luban grenade projectile should create its delayed explosion area.");
                harness.TickUntilTraceNode(MobaExecutionKind.DamageApply, 10080101, 20,
                    "Luban grenade area should apply damage on entry.");
                Assert.Less(harness.GetActorHp(targetActorId), targetHp,
                    "Luban grenade explosion should damage the enemy.");
                HeroSkillHeadlessContract.AssertFreshBuff(harness, targetActorId, 10080101, 1.5f,
                    "Luban grenade explosion should apply the two-second slow Buff.");
            }
        }

        [Test]
        public void Skill10080101_ShouldFlyToSelectedPointBeforeSpawningArea()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Luban, "luban_skill_1_selected_point_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "luban grenade selected point contract");
                var actorId = harness.AssertPlayerActorBound();
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var aimPoint = new Vec3(0f, 0f, 8f);
                var cast = skills.TryCastBySlot(actorId, Skill1.Slot, in aimPoint, Vec3.Forward);
                Assert.IsTrue(cast.Success, $"Luban skill 1 should cast toward its selected point. failReason={cast.FailReason}");

                var spawn = TickUntilProjectileSnapshot(harness, 30080101, ProjectileEventKind.Spawn, 30);
                var exit = TickUntilProjectileSnapshot(harness, spawn.ProjectileId, ProjectileEventKind.Exit, 60, byProjectileId: true);
                Assert.Greater(exit.ProjectileId, 0, "Luban grenade should terminate at the selected point.");
                Assert.AreEqual((int)ProjectileExitReason.MaxDistance, exit.ExitReason,
                    "An unblocked grenade should complete its selected travel distance.");
                var areas = harness.World.Services.Resolve<MobaAreaRuntimeService>();
                var activeAreas = new List<MobaAreaRuntimeInfo>(4);
                Assert.IsTrue(areas.TryGetAreas(activeAreas, actorId, 40080101),
                    "The grenade landing should leave its configured impact area active.");
                Assert.AreEqual(1, activeAreas.Count);
                Assert.AreEqual(aimPoint.X, activeAreas[0].Center.X, 0.05f);
                Assert.AreEqual(aimPoint.Z, activeAreas[0].Center.Z, 0.1f,
                    $"Impact area should land at the indicator point. exit=({exit.X:F2},{exit.Z:F2}) center=({activeAreas[0].Center.X:F2},{activeAreas[0].Center.Z:F2})");
            }
        }

        [Test]
        public void Skill10080201_ShouldLaunchSharkCannonAndKnockBackOnHit()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Luban, "luban_skill_2_cannon_contract_world"))
            {
                HeroSkillHeadlessContract.AssertTriggerActions(harness, Skill2.EffectId,
                    (int)TriggeringConstants.ShootProjectileId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, 10080213,
                    (int)TriggeringConstants.GetActionId(TriggeringConstants.Actions.AdjustDamageNumber).Value);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, 10080211,
                    (int)TriggeringConstants.GiveDamageId.Value,
                    (int)TriggeringConstants.PullId.Value);
                harness.EnterGameAndWarmup(reason: "luban shark cannon contract");
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 0f, z: -8f);
                var targetHp = harness.GetActorHp(targetActorId);
                var targetStart = harness.AssertActorEntity(targetActorId).transform.Value.Position;
                var effect = HeroSkillHeadlessContract.CastSlotAndAssertEffect(harness, Skill2,
                    "luban shark cannon contract", aimDir: new Vec3(0f, 0f, -8f), targetActorId: targetActorId);
                harness.AssertProjectileLaunchedUnderEffect(effect.RootId, 31080201, 30080201);
                var spawn = TickUntilProjectileSnapshot(harness, 30080201, ProjectileEventKind.Spawn, 30);
                Assert.Less(spawn.ForwardZ, -0.9f, $"Luban shark cannon should use the requested clear downward lane. forward=({spawn.ForwardX:F2},{spawn.ForwardZ:F2})");
                CaptureProjectileHitAndExit(harness, spawn.ProjectileId, 40, out var hit, out var exit);
                Assert.Greater(hit.ProjectileId, 0,
                    $"Luban shark cannon should produce a hit event. exitReason={exit.ExitReason}, hitCollider={hit.HitCollider}, spawn=({spawn.X:F2},{spawn.Z:F2}), exit=({exit.X:F2},{exit.Z:F2})");
                Assert.AreEqual((int)ProjectileExitReason.Hit, exit.ExitReason,
                    $"Luban shark cannon should hit before max distance. exitReason={exit.ExitReason}, hitCollider={exit.HitCollider}, hit=({hit.X:F2},{hit.Z:F2}), spawn=({spawn.X:F2},{spawn.Z:F2}), exit=({exit.X:F2},{exit.Z:F2}), target=({targetStart.X:F2},{targetStart.Z:F2})");
                harness.TickUntilTraceNode(MobaExecutionKind.EffectExecution, 10080211, 5,
                    "Luban shark cannon hit should execute its OnHit damage trigger.");
                harness.Tick(2);
                AssertDamageTraceExists(harness, effect.RootId, 10080201,
                    "Luban shark cannon should damage its first enemy hit.");
                Assert.Less(harness.GetActorHp(targetActorId), targetHp,
                    "Luban shark cannon should damage the enemy.");
                var targetEnd = harness.AssertActorEntity(targetActorId).transform.Value.Position;
                Assert.Less(targetEnd.Z, targetStart.Z - 0.1f,
                    "Luban shark cannon should knock the enemy away from the caster.");
            }
        }

        [Test]
        public void Skill10080301_ShouldSpawnAirshipWithBombSkill()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Luban, "luban_skill_3_airship_contract_world"))
            {
                Assert.IsTrue(harness.Config.TryGetSummon(1008, out var summon));
                CollectionAssert.Contains(summon.SkillIds, 10080401);
                Assert.AreEqual(100, summon.BrainId);
                Assert.AreEqual(14000, summon.LifetimeMs,
                    "The airship lifetime must match the official fourteen-second ultimate window.");
                Assert.IsTrue(harness.Config.TryGetSkillLevelTable(10080301, out var airSupportTable));
                Assert.IsNotEmpty(airSupportTable.Levels);
                var airSupportLevel = airSupportTable.Levels[0];
                Assert.AreEqual(1000f, airSupportLevel.Params[1], 0.01f,
                    "Luban air support should release one bomb per second.");
                Assert.AreEqual(14000f, airSupportLevel.Params[2], 0.01f,
                    "Luban air support should remain active for fourteen seconds.");
                Assert.IsTrue(harness.Config.TryGetAoe(40080401, out var airSupportAoe));
                Assert.AreEqual(650, airSupportAoe.DelayMs,
                    "Luban air support bombs should fall after the official 0.65 second warning.");
                CollectionAssert.Contains(airSupportAoe.OnDelayTriggerIds, 10080412);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, Skill3.EffectId,
                    (int)TriggeringConstants.SpawnSummonId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, 10080401,
                    (int)TriggeringConstants.ShootProjectileId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, 10080411,
                    (int)TriggeringConstants.SpawnAreaId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, 10080412,
                    (int)TriggeringConstants.GiveDamageId.Value);
                harness.EnterGameAndWarmup(reason: "luban airship summon contract");
                var actorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 0f, z: -8f);
                var targetHp = harness.GetActorHp(targetActorId);
                var effect = HeroSkillHeadlessContract.CastSlotAndAssertEffect(harness, Skill3,
                    "luban airship summon contract", aimDir: new Vec3(0f, 0f, -8f), targetActorId: targetActorId);
                harness.TickUntilTraceNode(MobaExecutionKind.SummonSpawn, 1008, 20,
                    "Luban ultimate should spawn the airship summon.");
                harness.AssertActionExecutedUnderEffect(effect.RootId,
                    (int)TriggeringConstants.SpawnSummonId.Value, TriggeringConstants.Actions.SpawnSummon);
                var summons = harness.World.Services.Resolve<MobaSummonService>();
                var summonActorIds = new List<int>(1);
                Assert.IsTrue(summons.TryGetSummons(actorId, summonActorIds, 1008));
                Assert.AreEqual(1, summonActorIds.Count, "Luban ultimate should create one airship summon.");
                var airshipStart = harness.AssertActorEntity(summonActorIds[0]).transform.Value.Position;
                var airshipForward = harness.AssertActorEntity(summonActorIds[0]).transform.Value.Forward;
                harness.Tick(10);
                var airshipAfterCruise = harness.AssertActorEntity(summonActorIds[0]).transform.Value.Position;
                var firstCruiseDistance = (airshipAfterCruise - airshipStart).Magnitude;
                Assert.Greater(firstCruiseDistance, 0.1f,
                    "Luban air-support summon should move along its launch direction while active.");
                var airshipForwardAfterCruise = harness.AssertActorEntity(summonActorIds[0]).transform.Value.Forward;
                Assert.Greater(Vec3.Dot(in airshipForward, in airshipForwardAfterCruise), 0.99f,
                    "Luban airship should preserve the direction selected when the ultimate was cast.");
                harness.Tick(30);
                var secondCruisePosition = harness.AssertActorEntity(summonActorIds[0]).transform.Value.Position;
                var secondCruiseDistance = (secondCruisePosition - airshipAfterCruise).Magnitude;
                Assert.AreEqual(firstCruiseDistance * 3f, secondCruiseDistance, 0.2f,
                    "Luban airship should cover the same distance per frame across different sampling windows.");
                harness.TickUntilTraceNode(MobaExecutionKind.ProjectileLaunch, 30080401, 120,
                    "The airship Brain should periodically cast its bomb skill.");
                var bombSpawn = TickUntilProjectileSnapshot(harness, 30080401, ProjectileEventKind.Spawn, 30);
                Assert.Greater(bombSpawn.ProjectileId, 0,
                    "The airship should create a visible bomb projectile before its delayed area resolves.");
                harness.TickUntilTraceNode(MobaExecutionKind.AreaSpawn, 40080401, 120,
                    "The moving airship should periodically drop a delayed bomb area at its current position.");
                harness.TickUntilTraceNode(MobaExecutionKind.DamageApply, 10080401, 120,
                    "The air-support bomb area should periodically apply its configured AOE damage.");
                Assert.Less(harness.GetActorHp(targetActorId), targetHp,
                    "The air-support bomb area should damage the enemy hero it detects.");

                var damageCountBeforeExpiry = CountDamageTraces(harness, 10080401);
                Assert.AreEqual(1, damageCountBeforeExpiry,
                    "A single bomb must settle damage through its delayed AOE exactly once.");
                TickUntilSummonDespawned(harness, summons, actorId, 1008, 500);
                var actors = harness.World.Services.Resolve<MobaActorRegistry>();
                Assert.IsFalse(actors.TryGet(summonActorIds[0], out _),
                    "The expired airship actor must be removed from the authoritative actor registry.");
                var launchCountAfterExpiry = CountTraceNodes(harness, MobaExecutionKind.ProjectileLaunch, 30080401);
                harness.Tick(30);
                Assert.AreEqual(0, summons.GetAliveCount(actorId, 1008),
                    "The airship must be removed when its configured lifetime expires.");
                Assert.AreEqual(launchCountAfterExpiry,
                    CountTraceNodes(harness, MobaExecutionKind.ProjectileLaunch, 30080401),
                    "An expired airship must stop releasing bomb projectiles.");
            }
        }

        private static MobaProjectileEventSnapshotEntry TickUntilProjectileSnapshot(
            MobaSkillConfigTestHarness harness,
            int id,
            ProjectileEventKind kind,
            int maxTicks,
            bool byProjectileId = false)
        {
            for (var i = 0; i <= maxTicks; i++)
            {
                var provider = harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>();
                var snapshots = new List<WorldStateSnapshot>(16);
                provider.CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
                for (var s = 0; s < snapshots.Count; s++)
                {
                    if (snapshots[s].OpCode != MobaOpCodes.Snapshot.ProjectileEvent) continue;
                    var entries = MobaProjectileEventSnapshotCodec.Deserialize(snapshots[s].Payload);
                    for (var e = 0; e < entries.Length; e++)
                    {
                        if (entries[e].Kind != (int)kind) continue;
                        if (byProjectileId ? entries[e].ProjectileId != id : entries[e].TemplateId != id) continue;
                        return entries[e];
                    }
                }

                if (i < maxTicks) harness.Tick(1);
            }

            Assert.Fail($"Projectile snapshot missing. id={id}, kind={kind}, maxTicks={maxTicks}");
            return default;
        }

        private static int TickAndCountProjectileSpawns(MobaSkillConfigTestHarness harness, int templateId, int maxTicks)
        {
            var provider = harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>();
            var count = 0;
            for (var tick = 0; tick <= maxTicks; tick++)
            {
                var snapshots = new List<WorldStateSnapshot>(16);
                provider.CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
                for (var s = 0; s < snapshots.Count; s++)
                {
                    if (snapshots[s].OpCode != MobaOpCodes.Snapshot.ProjectileEvent) continue;
                    var entries = MobaProjectileEventSnapshotCodec.Deserialize(snapshots[s].Payload);
                    for (var e = 0; e < entries.Length; e++)
                    {
                        if (entries[e].Kind == (int)ProjectileEventKind.Spawn && entries[e].TemplateId == templateId)
                            count++;
                    }
                }

                if (tick < maxTicks) harness.Tick(1);
            }

            return count;
        }

        private static void CaptureProjectileHitAndExit(
            MobaSkillConfigTestHarness harness,
            int projectileId,
            int maxTicks,
            out MobaProjectileEventSnapshotEntry hit,
            out MobaProjectileEventSnapshotEntry exit)
        {
            hit = default;
            exit = default;
            for (var i = 0; i <= maxTicks; i++)
            {
                var provider = harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>();
                var snapshots = new List<WorldStateSnapshot>(16);
                provider.CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
                for (var s = 0; s < snapshots.Count; s++)
                {
                    if (snapshots[s].OpCode != MobaOpCodes.Snapshot.ProjectileEvent) continue;
                    var entries = MobaProjectileEventSnapshotCodec.Deserialize(snapshots[s].Payload);
                    for (var e = 0; e < entries.Length; e++)
                    {
                        if (entries[e].ProjectileId != projectileId) continue;
                        if (entries[e].Kind == (int)ProjectileEventKind.Hit) hit = entries[e];
                        else if (entries[e].Kind == (int)ProjectileEventKind.Exit) exit = entries[e];
                    }
                }

                if (hit.ProjectileId > 0 && exit.ProjectileId > 0) return;
                if (i < maxTicks) harness.Tick(1);
            }

            Assert.Fail($"Projectile hit/exit snapshots incomplete. projectileId={projectileId}, hit={hit.ProjectileId}, exit={exit.ProjectileId}, maxTicks={maxTicks}");
        }

        private static void AssertDamageTraceExists(
            MobaSkillConfigTestHarness harness,
            long rootId,
            int reasonParam,
            string message)
        {
            var summary = string.Empty;
            var count = 0;
            foreach (var node in harness.Trace.GetNodesByKind((int)MobaExecutionKind.DamageApply))
            {
                if (node.Metadata?.ConfigId != reasonParam) continue;
                count++;
                summary += $"[{node.RootId}:{node.Metadata?.ConfigId}:{node.Metadata?.SourceActorId}->{node.Metadata?.TargetActorId}]";
            }

            Assert.Greater(count, 0, message + $" castRoot={rootId}, damageTrace={summary}");
        }

        private static int CountDamageTraces(MobaSkillConfigTestHarness harness, int reasonParam)
        {
            var count = 0;
            foreach (var node in harness.Trace.GetNodesByKind((int)MobaExecutionKind.DamageApply))
            {
                if (node.Metadata?.ConfigId == reasonParam) count++;
            }

            return count;
        }

        private static void TickUntilSummonDespawned(
            MobaSkillConfigTestHarness harness,
            MobaSummonService summons,
            int rootOwnerActorId,
            int summonId,
            int maxTicks)
        {
            for (var i = 0; i <= maxTicks; i++)
            {
                if (summons.GetAliveCount(rootOwnerActorId, summonId) == 0) return;
                if (i < maxTicks) harness.Tick(1);
            }

            Assert.Fail($"Summon did not despawn within {maxTicks} ticks. summonId={summonId}");
        }

        private static int CountTraceNodes(MobaSkillConfigTestHarness harness, MobaExecutionKind kind, int configId)
        {
            var count = 0;
            foreach (var node in harness.Trace.GetNodesByKind((int)kind))
            {
                if (node.Metadata?.ConfigId == configId) count++;
            }

            return count;
        }
    }
}

using System;
using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.Host;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class SunShangxiangSkillAcceptanceTests : MobaAcceptanceTestBase
    {
        private static readonly HeroSkillContract SunShangxiang = new HeroSkillContract(
            "SunShangxiang",
            heroId: 1007,
            attributeTemplateId: 1007,
            skillIds: new[] { 10070101, 10070201, 10070301 });

        private static readonly HeroSkillSlotContract Skill1 = new HeroSkillSlotContract(1, 10070101, 10070101, 10070101);
        private static readonly HeroSkillSlotContract Skill2 = new HeroSkillSlotContract(2, 10070201, 10070201, 10070201);
        private static readonly HeroSkillSlotContract Skill3 = new HeroSkillSlotContract(3, 10070301, 10070301, 10070301);

        [Test]
        public void SunShangxiangAndLubanSkills_ShouldExposeOfficialAimIndicators()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(SunShangxiang, "hero_skill_indicator_contract_world"))
            {
                AssertIndicator(harness, 10070101, 11, 6, 1.8f);
                AssertIndicator(harness, 10070201, 12, 2, 4f);
                AssertIndicator(harness, 10070301, 13, 7, 1.2f);
            }
        }

        private static void AssertIndicator(MobaSkillConfigTestHarness harness, int skillId, int templateId, int shape, float width)
        {
            Assert.IsTrue(harness.Config.TryGetSkill(skillId, out var skill));
            Assert.AreEqual(templateId, skill.SkillButtonTemplateId);
            Assert.IsTrue(harness.Config.TryGetSkillButtonTemplate(templateId, out var template));
            Assert.AreEqual(shape, template.IndicatorShape);
            Assert.AreEqual(width, template.IndicatorWorldWidth);
            Assert.IsTrue(template.EnableAim);
        }

        [Test]
        public void Passive10070000_ShouldReduceCooldownsOfAllThreeActiveSkillsAfterBasicAttackHit()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(SunShangxiang, "sun_shangxiang_passive_cooldown_reset_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "sun shangxiang passive cooldown reset runtime contract");
                var actorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 3f, z: 0f);
                Assert.IsTrue(harness.Config.TryGetPassiveSkill(10070000, out _), "Sun Shangxiang passive config should exist.");
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    10070002,
                    (int)TriggeringConstants.ResetCooldownId.Value);

                HeroSkillHeadlessContract.CastSlotAndAssertEffect(
                    harness,
                    Skill1,
                    "sun shangxiang passive cooldown reset runtime contract",
                    aimDir: Vec3.Right);
                harness.TickUntilSkillStops(
                    actorId,
                    Skill1.Slot,
                    maxTicks: 120,
                    message: "Sun Shangxiang skill 1 should finish before the passive reset is evaluated.");

                var actor = harness.AssertActorEntity(actorId);
                var skills = actor.skillLoadout.ActiveSkills;
                var skillOne = System.Array.Find(skills, runtime => runtime != null && runtime.SkillId == Skill1.SkillId);
                var skillTwo = System.Array.Find(skills, runtime => runtime != null && runtime.SkillId == Skill2.SkillId);
                var skillThree = System.Array.Find(skills, runtime => runtime != null && runtime.SkillId == Skill3.SkillId);
                Assert.IsNotNull(skillOne, "Sun Shangxiang skill 1 runtime should remain in the active loadout.");
                Assert.IsNotNull(skillTwo, "Sun Shangxiang skill 2 runtime should remain in the active loadout.");
                Assert.IsNotNull(skillThree, "Sun Shangxiang skill 3 runtime should remain in the active loadout.");
                Assert.Greater(skillOne.CooldownEndTimeMs, 0L, "Sun Shangxiang skill 1 should be on cooldown after casting.");

                var nowMs = (long)(harness.FrameTime.Time * 1000d);
                skillTwo.CooldownDurationMs = 5000;
                skillTwo.CooldownEndTimeMs = nowMs + 5000;
                skillThree.CooldownDurationMs = 8000;
                skillThree.CooldownEndTimeMs = nowMs + 8000;
                var expectedSkillOneEnd = skillOne.CooldownEndTimeMs - 500;
                var expectedSkillTwoEnd = skillTwo.CooldownEndTimeMs - 500;
                var expectedSkillThreeEnd = skillThree.CooldownEndTimeMs - 500;

                var passiveBaseline = harness.CaptureTraceBaseline();
                HeroSkillHeadlessContract.ExecuteBasicAttackDamage(harness, actorId, targetActorId, baseDamage: 10f);
                harness.TickUntilTraceNodeAfter(
                    passiveBaseline,
                    MobaExecutionKind.EffectExecution,
                    10070111,
                    maxTicks: 10,
                    message: "Sun Shangxiang basic attack should execute the armed enhanced hit trigger.");
                harness.Tick(1);
                Assert.AreEqual(expectedSkillOneEnd, skillOne.CooldownEndTimeMs, "A basic attack hit should reduce skill 1's remaining cooldown by 500 ms, not clear it.");
                Assert.AreEqual(expectedSkillTwoEnd, skillTwo.CooldownEndTimeMs, "A basic attack hit should reduce skill 2's remaining cooldown by 500 ms.");
                Assert.AreEqual(expectedSkillThreeEnd, skillThree.CooldownEndTimeMs, "A basic attack hit should reduce skill 3's remaining cooldown by 500 ms.");
                Assert.AreEqual(4500, skillTwo.CooldownDurationMs, "Cooldown reduction should reduce the remaining-time duration used by the skill HUD.");
            }
        }

        [Test]
        public void Passive10070000_ShouldClampShortCooldownAndPreserveReadySkill()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(SunShangxiang, "sun_shangxiang_passive_cooldown_boundary_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "sun shangxiang passive cooldown boundary contract");
                var actorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 3f, z: 0f);
                HeroSkillHeadlessContract.CastSlotAndAssertEffect(
                    harness,
                    Skill1,
                    "sun shangxiang passive cooldown boundary contract",
                    aimDir: Vec3.Right);
                harness.TickUntilSkillStops(
                    actorId,
                    Skill1.Slot,
                    maxTicks: 120,
                    message: "Sun Shangxiang skill 1 should finish before the passive cooldown boundary is evaluated.");

                var actor = harness.AssertActorEntity(actorId);
                var skills = actor.skillLoadout.ActiveSkills;
                var skillOne = System.Array.Find(skills, runtime => runtime != null && runtime.SkillId == Skill1.SkillId);
                var skillTwo = System.Array.Find(skills, runtime => runtime != null && runtime.SkillId == Skill2.SkillId);
                var skillThree = System.Array.Find(skills, runtime => runtime != null && runtime.SkillId == Skill3.SkillId);
                Assert.IsNotNull(skillOne);
                Assert.IsNotNull(skillTwo);
                Assert.IsNotNull(skillThree);

                var nowMs = (long)(harness.FrameTime.Time * 1000d);
                skillOne.CooldownDurationMs = 300;
                skillOne.CooldownEndTimeMs = nowMs + 300;
                skillTwo.CooldownDurationMs = 0;
                skillTwo.CooldownEndTimeMs = nowMs;
                skillThree.CooldownDurationMs = 5000;
                skillThree.CooldownEndTimeMs = nowMs + 5000;

                var passiveBaseline = harness.CaptureTraceBaseline();
                HeroSkillHeadlessContract.ExecuteBasicAttackDamage(harness, actorId, targetActorId, baseDamage: 10f);
                harness.TickUntilTraceNodeAfter(
                    passiveBaseline,
                    MobaExecutionKind.EffectExecution,
                    10070111,
                    maxTicks: 10,
                    message: "Sun Shangxiang's enhanced basic attack should evaluate cooldown reduction at the ready and underflow boundaries.");
                harness.Tick(1);

                var afterHitNowMs = (long)(harness.FrameTime.Time * 1000d);
                Assert.LessOrEqual(skillOne.CooldownEndTimeMs, afterHitNowMs, "A reduction larger than the remaining cooldown should clamp skill 1 to ready.");
                Assert.AreEqual(0, skillOne.CooldownDurationMs, "A clamped cooldown should expose zero remaining duration to the HUD.");
                Assert.LessOrEqual(skillTwo.CooldownEndTimeMs, afterHitNowMs, "A ready skill should remain ready after cooldown reduction.");
                Assert.AreEqual(0, skillTwo.CooldownDurationMs, "A ready skill should keep zero remaining duration after cooldown reduction.");
                Assert.AreEqual(nowMs + 4500, skillThree.CooldownEndTimeMs, "The third active skill should still receive the configured 500 ms reduction.");
                Assert.AreEqual(4500, skillThree.CooldownDurationMs, "The third skill's HUD duration should shrink with its remaining cooldown.");
            }
        }

        [Test]
        public void Skill10070101_ShouldDashAndArmEnhancedBasicAttack()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(SunShangxiang, "sun_shangxiang_skill_1_dash_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "sun shangxiang skill 1 movement-speed curve baseline");
                var actorId = harness.AssertPlayerActorBound();
                var baseMoveSpeed = harness.GetActorMoveSpeed(actorId);
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 0f, z: 3f);
                Assert.IsTrue(harness.Config.TryGetSkill(Skill1.SkillId, out _), "Sun Shangxiang skill 1 config should exist.");
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    Skill1.EffectId,
                    (int)TriggeringConstants.DashId.Value,
                    (int)TriggeringConstants.AddBuffId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    10070111,
                    (int)TriggeringConstants.GiveDamageId.Value,
                    (int)TriggeringConstants.AddBuffId.Value,
                    (int)TriggeringConstants.RemoveBuffId.Value);

                var effectTrace = HeroSkillHeadlessContract.CastSlotAndAssertEffect(
                    harness,
                    Skill1,
                    "sun shangxiang skill 1 movement-speed and enhanced basic attack contract",
                    aimDir: Vec3.Right);
                harness.AssertActionExecutedUnderEffect(effectTrace.RootId, (int)TriggeringConstants.DashId.Value, TriggeringConstants.Actions.Dash);
                harness.AssertActionExecutedUnderEffect(effectTrace.RootId, (int)TriggeringConstants.AddBuffId.Value, TriggeringConstants.Actions.AddBuff);
                HeroSkillHeadlessContract.AssertFreshBuff(harness, actorId, 10070103, 2.0f, "Sun Shangxiang roll should apply the movement-speed Buff.");
                AssertBuffPresentationCue(harness, actorId, 10070103,
                    "Sun Shangxiang's roll speed buff should publish an active HUD cue.");
                HeroSkillHeadlessContract.AssertFreshBuff(harness, actorId, 10070101, 2.0f, "Sun Shangxiang roll should arm the enhanced basic attack Buff.");

                var speedAtCurveStart = harness.GetActorMoveSpeed(actorId);
                Assert.Greater(speedAtCurveStart, baseMoveSpeed, "Sun Shangxiang roll should grant the conditional movement-speed bonus when an enemy is nearby.");
                harness.Tick(15);
                var speedAtCurveMid = harness.GetActorMoveSpeed(actorId);
                Assert.GreaterOrEqual(speedAtCurveStart, speedAtCurveMid, "Sun Shangxiang roll movement-speed modifier should follow a decaying curve.");
                harness.TickUntilSkillStops(actorId, Skill1.Slot, maxTicks: 120, message: "Sun Shangxiang skill 1 should finish before the enhanced basic attack is entered.");

                const int basicAttackSlot = 4;
                const int normalBasicAttackSkillId = 10070001;
                var candidates = harness.World.Services.Resolve<MobaSkillSlotCandidateService>();
                var loadout = harness.World.Services.Resolve<MobaSkillLoadoutService>();
                Assert.IsTrue(loadout.TryGetBaseSkillId(actorId, basicAttackSlot, out var baseSkillId));
                Assert.AreEqual(normalBasicAttackSkillId, baseSkillId);
                Assert.IsTrue(loadout.TryGetSkillId(actorId, basicAttackSlot, out var effectiveSkillId));
                Assert.AreEqual(10070112, effectiveSkillId, "Sun Shangxiang roll should replace the effective basic attack skill ID.");
                Assert.AreEqual(10070112, candidates.Resolve(actorId, basicAttackSlot, normalBasicAttackSkillId).EffectiveSkillId);

                harness.MoveScenarioActor(targetActorId, new MobaAcceptanceVector3Expectation { x = 4f, y = 0f, z = -6f });
                harness.Tick(1);
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastSkill(
                    actorId,
                    normalBasicAttackSkillId,
                    slot: basicAttackSlot,
                    aimPos: default,
                    aimDir: new Vec3(0.38f, 0f, -0.92f),
                    targetActorId: targetActorId);
                Assert.IsTrue(cast.Success, $"Sun Shangxiang normal basic attack should resolve to the enhanced skill. failReason={cast.FailReason}");
                harness.TickUntilTraceNode(
                    MobaExecutionKind.SkillCast,
                    10070112,
                    maxTicks: 10,
                    message: "Sun Shangxiang normal basic attack should cast enhanced skill 10070112 while the Buff is active.");
                var enhancedTrace = harness.TickUntilTraceNode(
                    MobaExecutionKind.EffectExecution,
                    10070112,
                    maxTicks: 30,
                    message: "Sun Shangxiang enhanced basic attack should execute its configured effect.");
                harness.AssertProjectileLaunchedUnderEffect(enhancedTrace.RootId, 31070001, 30070001);
                var projectileHit = false;
                var projectileExit = false;
                var projectileExitReason = 0;
                var projectileHitCollider = 0;
                for (var i = 0; i <= 30 && !projectileHit; i++)
                {
                    if (TryCollectProjectileLifecycle(harness, 30070001, out var hit, out var exit))
                    {
                        if (hit.Kind == (int)ProjectileEventKind.Hit)
                        {
                            projectileHit = true;
                            projectileHitCollider = hit.HitCollider;
                        }

                        if (exit.Kind == (int)ProjectileEventKind.Exit)
                        {
                            projectileExit = true;
                            projectileExitReason = exit.ExitReason;
                        }
                    }

                    if (!projectileHit && i < 30) harness.Tick(1);
                }
                Assert.IsTrue(
                    projectileHit,
                    $"Sun Shangxiang enhanced basic attack projectile should hit the target before its normal hit trigger executes. exit={projectileExit}, exitReason={projectileExitReason}, hitCollider={projectileHitCollider}, casterPosition={harness.AssertActorEntity(actorId).transform.Value.Position}, targetPosition={harness.AssertActorEntity(targetActorId).transform.Value.Position}");
                harness.TickUntilTraceNode(
                    MobaExecutionKind.EffectExecution,
                    10070011,
                    maxTicks: 30,
                    message: "Sun Shangxiang enhanced basic attack projectile should execute its normal hit trigger.");
                harness.Tick(30);
                Assert.IsFalse(harness.HasActorBuff(actorId, 10070101), "Sun Shangxiang enhanced basic attack should consume its armed Buff after the hit.");
                Assert.AreEqual(normalBasicAttackSkillId, candidates.Resolve(actorId, basicAttackSlot, normalBasicAttackSkillId).EffectiveSkillId, "Sun Shangxiang basic attack slot should recover after Buff consumption.");

                harness.Tick(60);
                Assert.IsFalse(harness.HasActorBuff(actorId, 10070103), "Sun Shangxiang movement-speed Buff should expire after its configured duration.");
                Assert.AreEqual(baseMoveSpeed, harness.GetActorMoveSpeed(actorId), 0.01f, "Sun Shangxiang movement speed should return to baseline after the curve Buff expires.");
            }
        }

        [Test]
        public void Skill10070101_ShouldNotGrantConditionalMoveSpeedWithoutNearbyEnemies()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(SunShangxiang, "sun_shangxiang_skill_1_no_nearby_enemy_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "sun shangxiang skill 1 should gate its movement-speed bonus on nearby enemies");
                var actorId = harness.AssertPlayerActorBound();
                var baseMoveSpeed = harness.GetActorMoveSpeed(actorId);
                var effectTrace = HeroSkillHeadlessContract.CastSlotAndAssertEffect(
                    harness,
                    Skill1,
                    "sun shangxiang skill 1 without nearby enemies",
                    aimDir: Vec3.Right);

                harness.AssertActionExecutedUnderEffect(effectTrace.RootId, (int)TriggeringConstants.DashId.Value, TriggeringConstants.Actions.Dash);
                HeroSkillHeadlessContract.AssertFreshBuff(harness, actorId, 10070101, 2.0f,
                    "Sun Shangxiang roll should always arm the enhanced basic attack.");
                Assert.IsFalse(harness.HasActorBuff(actorId, 10070103),
                    "Sun Shangxiang should not receive the conditional movement-speed Buff when no enemy is nearby.");
                Assert.AreEqual(baseMoveSpeed, harness.GetActorMoveSpeed(actorId), 0.001f,
                    "Sun Shangxiang movement speed should remain unchanged without nearby enemies.");
                var cues = harness.World.Services.Resolve<MobaPresentationCueSnapshotService>();
                var cuePrefix = $"buff:{actorId}:10070103:";
                foreach (var cueKey in cues.ActiveCues.Keys)
                    Assert.IsFalse(cueKey.StartsWith(cuePrefix, StringComparison.Ordinal),
                        "No movement-speed HUD cue should be published when the conditional Buff is absent.");
            }
        }

        [Test]
        public void Skill10070201_ShouldLaunchBombAndSpawnSlowArea()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(SunShangxiang, "sun_shangxiang_skill_2_area_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "sun shangxiang skill 2 projectile and area contract");
                var actorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 4f);
                var targetHp = harness.GetActorHp(targetActorId);
                harness.AssertProjectileConfigExists(31070202, 30070202);
                Assert.IsTrue(harness.Config.TryGetAoe(40070203, out _), "Sun Shangxiang skill 2 area config should exist.");
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    Skill2.EffectId,
                    (int)TriggeringConstants.ShootProjectileId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    10070211,
                    (int)TriggeringConstants.SpawnAreaId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    10070212,
                    (int)TriggeringConstants.GiveDamageId.Value,
                    (int)TriggeringConstants.AddBuffId.Value);
                Assert.IsTrue(harness.Config.TryGetBuff(10070202, out var armorBreak));
                Assert.AreEqual(1000027, armorBreak.Modifiers[0].DefinitionId,
                    "Sun Shangxiang skill 2 should use its own armor-break Buff contract.");

                var effectTrace = HeroSkillHeadlessContract.CastSlotAndAssertEffect(
                    harness,
                    Skill2,
                    "sun shangxiang skill 2 projectile and area contract",
                    aimDir: Vec3.Right);
                harness.AssertActionExecutedUnderEffect(effectTrace.RootId, (int)TriggeringConstants.ShootProjectileId.Value, TriggeringConstants.Actions.ShootProjectile);
                harness.AssertProjectileLaunchedUnderEffect(effectTrace.RootId, 31070202, 30070202);

                var spawn = TickUntilProjectileSpawnSnapshot(harness, 30070202, maxTicks: 20);
                Assert.Greater(spawn.ProjectileActorId, 0, "Sun Shangxiang skill 2 projectile should expose a scene actor for presentation.");
                var exit = TickUntilProjectileExitSnapshot(harness, spawn.ProjectileId, maxTicks: 30);
                Assert.AreEqual((int)ProjectileExitReason.Hit, exit.ExitReason, "Sun Shangxiang skill 2 bomb should resolve on the enemy before max range.");

                harness.TickUntilTraceNode(MobaExecutionKind.AreaSpawn, 40070203, maxTicks: 20,
                    message: "Sun Shangxiang skill 2 projectile exit should spawn its configured explosion area.");
                harness.TickUntilTraceNode(MobaExecutionKind.DamageApply, 10070201, maxTicks: 20,
                    message: "Sun Shangxiang skill 2 explosion area should apply its configured damage.");
                Assert.Less(harness.GetActorHp(targetActorId), targetHp, "Sun Shangxiang skill 2 explosion should damage the enemy.");
                HeroSkillHeadlessContract.AssertFreshBuff(harness, targetActorId, 10070201, 0.5f,
                    "Sun Shangxiang skill 2 explosion should apply its slow Buff to the enemy.");
                HeroSkillHeadlessContract.AssertFreshBuff(harness, targetActorId, 10070202, 0.5f,
                    "Sun Shangxiang skill 2 explosion should apply its armor-break mark to the enemy.");

                var areas = harness.World.Services.Resolve<MobaAreaRuntimeService>();
                var activeAreas = new List<MobaAreaRuntimeInfo>(2);
                Assert.IsTrue(areas.TryGetAreas(activeAreas, ownerActorId: actorId, templateId: 40070203),
                    "Sun Shangxiang skill 2 should keep the explosion area alive for its configured duration.");
                Assert.AreEqual(40070203, activeAreas[0].TemplateId);
                Assert.AreEqual(3.5f, activeAreas[0].Radius, 0.001f);

                harness.Tick(80);
                Assert.IsFalse(areas.TryGetAreas(activeAreas, ownerActorId: actorId, templateId: 40070203),
                    "Sun Shangxiang skill 2 explosion area should expire after its configured duration.");
            }
        }

        [Test]
        public void Skill10070301_ShouldLaunchConfiguredUltimateProjectile()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(SunShangxiang, "sun_shangxiang_skill_3_projectile_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "sun shangxiang skill 3 projectile damage contract");
                // Keep the target inside the deterministic headless arena's clear lane;
                // the projectile itself still validates the configured 18-unit range.
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 4f, z: -6f);
                var targetHp = harness.GetActorHp(targetActorId);
                var casterPosition = harness.AssertActorEntity(harness.AssertPlayerActorBound()).transform.Value.Position;
                var targetPosition = harness.AssertActorEntity(targetActorId).transform.Value.Position;
                var targetDirection = DeterministicMathBridge.Normalize(new Vec3(
                    targetPosition.X - casterPosition.X,
                    0f,
                    targetPosition.Z - casterPosition.Z));
                harness.AssertProjectileConfigExists(31070302, 30070302);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    Skill3.EffectId,
                    (int)TriggeringConstants.ShootProjectileId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    10070313,
                    (int)TriggeringConstants.GiveDamageId.Value,
                    (int)TriggeringConstants.SpawnAreaId.Value);
                Assert.IsTrue(harness.Config.TryGetAoe(40070301, out var ultimateAoe));
                CollectionAssert.Contains(ultimateAoe.OnDelayTriggerIds, 10070314);

                var effectTrace = HeroSkillHeadlessContract.CastSlotAndAssertEffect(
                    harness,
                    Skill3,
                    "sun shangxiang skill 3 projectile damage contract",
                    aimDir: targetDirection);
                harness.AssertActionExecutedUnderEffect(effectTrace.RootId, (int)TriggeringConstants.ShootProjectileId.Value, TriggeringConstants.Actions.ShootProjectile);
                harness.AssertProjectileLaunchedUnderEffect(effectTrace.RootId, 31070302, 30070302);
                var spawn = TickUntilProjectileSpawnSnapshot(harness, 30070302, maxTicks: 20);
                var exit = TickUntilProjectileExitSnapshot(harness, spawn.ProjectileId, maxTicks: 50);
                Assert.AreEqual((int)ProjectileExitReason.Hit, exit.ExitReason, "Sun Shangxiang ultimate should hit the enemy before max range.");
                var traceSummary = string.Empty;
                foreach (var node in harness.Trace.GetNodesByRoot(effectTrace.RootId))
                {
                    traceSummary += $"[{node.Kind}:{node.Metadata?.ConfigId}:{node.Metadata?.SourceActorId}->{node.Metadata?.TargetActorId}]";
                }
                harness.TickUntilTraceNode(
                    MobaExecutionKind.DamageApply,
                    10070301,
                    maxTicks: 20,
                    message: $"Sun Shangxiang ultimate hit should apply its configured damage trace. root={effectTrace.RootId} traces={traceSummary}");
                Assert.Less(harness.GetActorHp(targetActorId), targetHp, "Sun Shangxiang ultimate should damage the enemy on hit.");
                var hpAfterDirectHit = harness.GetActorHp(targetActorId);
                harness.TickUntilTraceNode(MobaExecutionKind.AreaSpawn, 40070301, 20,
                    "Sun Shangxiang ultimate should spawn an explosion area at the hit point.");
                harness.Tick(20);
                Assert.Less(harness.GetActorHp(targetActorId), hpAfterDirectHit,
                    "Sun Shangxiang ultimate explosion area should apply additional damage after the direct projectile hit.");
            }
        }

        private static MobaProjectileEventSnapshotEntry TickUntilProjectileSpawnSnapshot(MobaSkillConfigTestHarness harness, int templateId, int maxTicks)
        {
            for (var i = 0; i <= maxTicks; i++)
            {
                if (TryCollectProjectileSnapshot(harness, templateId, ProjectileEventKind.Spawn, out var entry)) return entry;
                if (i < maxTicks) harness.Tick(1);
            }

            Assert.Fail($"Projectile spawn snapshot missing for template {templateId} within {maxTicks} ticks.");
            return default;
        }

        private static MobaProjectileEventSnapshotEntry TickUntilProjectileExitSnapshot(MobaSkillConfigTestHarness harness, int projectileId, int maxTicks)
        {
            for (var i = 0; i <= maxTicks; i++)
            {
                if (TryCollectProjectileSnapshot(harness, projectileId, ProjectileEventKind.Exit, out var entry, byProjectileId: true)) return entry;
                if (i < maxTicks) harness.Tick(1);
            }

            Assert.Fail($"Projectile exit snapshot missing for projectile {projectileId} within {maxTicks} ticks.");
            return default;
        }

        private static bool TryCollectProjectileSnapshot(
            MobaSkillConfigTestHarness harness,
            int id,
            ProjectileEventKind kind,
            out MobaProjectileEventSnapshotEntry entry,
            bool byProjectileId = false)
        {
            entry = default;
            var provider = harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>();
            var snapshots = new List<WorldStateSnapshot>(16);
            provider.CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
            for (var i = 0; i < snapshots.Count; i++)
            {
                if (snapshots[i].OpCode != MobaOpCodes.Snapshot.ProjectileEvent) continue;
                var entries = MobaProjectileEventSnapshotCodec.Deserialize(snapshots[i].Payload);
                for (var j = 0; j < entries.Length; j++)
                {
                    if (entries[j].Kind != (int)kind) continue;
                    if (byProjectileId ? entries[j].ProjectileId != id : entries[j].TemplateId != id) continue;
                    entry = entries[j];
                    return true;
                }
            }

            return false;
        }

        private static bool TryCollectProjectileLifecycle(
            MobaSkillConfigTestHarness harness,
            int templateId,
            out MobaProjectileEventSnapshotEntry hit,
            out MobaProjectileEventSnapshotEntry exit)
        {
            hit = default;
            exit = default;
            var provider = harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>();
            var snapshots = new List<WorldStateSnapshot>(8);
            provider.CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
            var found = false;

            for (var i = 0; i < snapshots.Count; i++)
            {
                if (snapshots[i].OpCode != MobaOpCodes.Snapshot.ProjectileEvent) continue;
                var entries = MobaProjectileEventSnapshotCodec.Deserialize(snapshots[i].Payload);
                for (var j = 0; j < entries.Length; j++)
                {
                    var entry = entries[j];
                    if (entry.TemplateId != templateId) continue;
                    if (entry.Kind == (int)ProjectileEventKind.Hit)
                    {
                        hit = entry;
                        found = true;
                    }
                    else if (entry.Kind == (int)ProjectileEventKind.Exit)
                    {
                        exit = entry;
                        found = true;
                    }
                }
            }

            return found;
        }
    }
}

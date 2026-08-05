using System.Collections.Generic;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Trace;
using AbilityKit.Triggering.Runtime;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class ZhaoYunSkillAcceptanceTests : MobaAcceptanceTestBase
    {
        private static readonly HeroSkillContract ZhaoYun = new HeroSkillContract(
            "ZhaoYun",
            heroId: 1003,
            attributeTemplateId: 1003,
            skillIds: new[] { 10030101, 10030201, 10030301 });

        private static readonly HeroSkillSlotContract Skill1 = new HeroSkillSlotContract(1, 10030101, 10030101, 10030101);
        private static readonly HeroSkillSlotContract Skill2 = new HeroSkillSlotContract(2, 10030201, 10030201, 10030201);
        private static readonly HeroSkillSlotContract Skill3 = new HeroSkillSlotContract(3, 10030301, 10030301, 10030301);

        [Test]
        public void Passive10030000_ShouldScaleDamageReductionByMissingHealth()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(ZhaoYun, "zhaoyun_passive_missing_health_damage_reduction_contract_world"))
            {
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    10030000,
                    (int)TriggeringConstants.GetActionId(TriggeringConstants.Actions.AdjustDamageNumber).Value,
                    (int)TriggeringConstants.DebugLogId.Value);

                harness.EnterGameAndWarmup(reason: "zhaoyun passive missing health damage reduction contract");
                var actorId = harness.AssertPlayerActorBound();
                var maxHp = harness.GetActorAttribute(actorId, BattleAttributeType.MAX_HP);
                Assert.Greater(maxHp, 0f, "Zhao Yun must have positive maximum health for passive damage reduction.");

                harness.SetScenarioActorAttribute(actorId, "hp", maxHp);
                var fullHpDamage = HeroSkillHeadlessContract.ExecuteDamage(harness, 0, actorId, 100f, DamageReasonKind.Environment);
                Assert.Greater(fullHpDamage.Value, 0f, "Zhao Yun should receive positive incoming damage at full health.");

                harness.SetScenarioActorAttribute(actorId, "hp", maxHp * 0.5f);
                var halfHpDamage = HeroSkillHeadlessContract.ExecuteDamage(harness, 0, actorId, 100f, DamageReasonKind.Environment);
                Assert.AreEqual(fullHpDamage.Value * 0.835f, halfHpDamage.Value, 0.01f, "Zhao Yun should reduce final incoming damage by 16.5% at 50% missing health.");

                harness.SetScenarioActorAttribute(actorId, "hp", maxHp * 0.01f);
                var lowHpDamage = HeroSkillHeadlessContract.ExecuteDamage(harness, 0, actorId, 100f, DamageReasonKind.Environment);
                Assert.AreEqual(fullHpDamage.Value * 0.6733f, lowHpDamage.Value, 0.02f, "Zhao Yun passive reduction should cap at the configured 33% when health is nearly empty.");
            }
        }

        [Test]
        public void Skill10030101_ShouldGrantMoveSpeedAndConsumeEnhancedBasicAttack()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(ZhaoYun, "zhaoyun_skill_1_enhanced_basic_contract_world"))
            {
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    10030101,
                    (int)TriggeringConstants.AddBuffId.Value,
                    (int)TriggeringConstants.DebugLogId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    10030111,
                    (int)TriggeringConstants.GiveDamageId.Value,
                    (int)TriggeringConstants.AddBuffId.Value,
                    (int)TriggeringConstants.RemoveBuffId.Value,
                    (int)TriggeringConstants.DebugLogId.Value);

                harness.EnterGameAndWarmup(reason: "zhaoyun skill 1 movement-speed baseline");
                var actorId = harness.AssertPlayerActorBound();
                var baseMoveSpeed = harness.GetActorMoveSpeed(actorId);
                var effectTrace = HeroSkillHeadlessContract.CastSlotAndAssertEffect(harness, Skill1, "zhaoyun skill 1 enhanced basic attack contract");
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 3f);
                harness.AssertActionExecutedUnderEffect(effectTrace.RootId, (int)TriggeringConstants.AddBuffId.Value, TriggeringConstants.Actions.AddBuff);
                HeroSkillHeadlessContract.AssertFreshBuff(harness, actorId, 10030104, 2f, "Zhao Yun skill 1 should grant a short movement-speed state.");
                HeroSkillHeadlessContract.AssertFreshBuff(harness, actorId, 10030101, 4f, "Zhao Yun skill 1 should grant an enhanced basic attack state.");
                Assert.Greater(harness.GetActorMoveSpeed(actorId), baseMoveSpeed, "Zhao Yun movement-speed state must increase the actor's move speed above its pre-cast baseline.");
                harness.TickUntilSkillStops(actorId, Skill1.Slot, maxTicks: 120, message: "Zhao Yun skill 1 should finish while its temporary states remain active.");
                Assert.IsTrue(harness.HasActorBuff(actorId, 10030101), "Finishing Zhao Yun skill 1 should not consume the pending enhanced basic attack.");

                var subscriptions = harness.World.Services.Resolve<MobaTriggerPlanSubscriptionService>();
                var enhancedBasicOwnerKeys = new List<long>();
                subscriptions.CopyOwnerKeysForTrigger(10030111, enhancedBasicOwnerKeys);
                Assert.IsNotEmpty(enhancedBasicOwnerKeys, "Finishing Zhao Yun skill 1 should retain trigger 10030111's owner-bound subscriptions.");

                var enhancedBasicOwnerKey = 0L;
                for (int i = 0; i < enhancedBasicOwnerKeys.Count; i++)
                {
                    var ownerKey = enhancedBasicOwnerKeys[i];
                    Assert.Less(ownerKey, 0, "Zhao Yun enhanced basic trigger must not be duplicated by the passive skill owner.");
                    enhancedBasicOwnerKey = ownerKey;
                }

                Assert.AreEqual(1, enhancedBasicOwnerKeys.Count, "Zhao Yun enhanced basic trigger should have exactly one continuous Buff owner.");
                Assert.Less(enhancedBasicOwnerKey, 0, "Zhao Yun enhanced basic trigger should be owned by its continuous Buff runtime.");

                var gates = harness.World.Services.Resolve<MobaOwnerBoundTriggerGateService>();
                Assert.IsTrue(gates.HasGate(enhancedBasicOwnerKey, 10030111), "Zhao Yun enhanced basic subscription should retain its continuous runtime gate.");
                Assert.IsTrue(gates.CanExecute(enhancedBasicOwnerKey, 10030111), "Zhao Yun enhanced basic continuous runtime gate should remain executable after the cast pipeline finishes.");
                Assert.IsTrue(
                    gates.TryGetExecutionSource(enhancedBasicOwnerKey, 10030111, out var enhancedBasicSource),
                    "Zhao Yun enhanced basic subscription should retain a valid execution source.");
                Assert.AreEqual(actorId, enhancedBasicSource.SourceActorId, "Zhao Yun enhanced basic execution source should be the caster.");

                var enhancedTraceBaseline = harness.CaptureTraceBaseline();
                var basicAttackResult = HeroSkillHeadlessContract.ExecuteBasicAttackDamage(harness, actorId, targetActorId, baseDamage: 10f);
                Assert.AreEqual(actorId, basicAttackResult.AttackerActorId, "Zhao Yun basic attack result should retain the caster as its source actor.");
                Assert.AreEqual(DamageReasonKind.BasicAttack, basicAttackResult.ReasonKind, "Zhao Yun enhanced hit must be evaluated from a basic-attack damage result.");
                var enhancedTrace = harness.TickUntilTraceNodeAfter(enhancedTraceBaseline, MobaTraceKind.EffectExecution, 10030111, maxTicks: 10, message: "Zhao Yun enhanced basic attack should execute its post-hit trigger.");
                var enhancedDamageAction = harness.AssertActionExecutedUnderEffect(enhancedTrace.RootId, (int)TriggeringConstants.GiveDamageId.Value, TriggeringConstants.Actions.GiveDamage);
                harness.AssertTraceLifecycle(enhancedTrace, enhancedDamageAction, "Zhao Yun enhanced basic attack damage action should remain in its effect trace lifecycle.");
                harness.AssertActionExecutedUnderEffect(enhancedTrace.RootId, (int)TriggeringConstants.RemoveBuffId.Value, TriggeringConstants.Actions.RemoveBuff);
                HeroSkillHeadlessContract.AssertFreshBuff(harness, targetActorId, 10030102, 2f, "Zhao Yun enhanced basic attack should slow the hit target.");
                harness.Tick(1);
                Assert.IsFalse(harness.HasActorBuff(actorId, 10030101), "Zhao Yun enhanced basic attack state should be consumed by its first valid basic attack hit.");

                var recastBaseline = harness.CaptureTraceBaseline();
                harness.ResetSkillCooldown(actorId, Skill1.SkillId);
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var recast = skills.TryCastBySlot(actorId, Skill1.Slot, aimPos: default, aimDir: Vec3.Right, targetActorId: 0);
                Assert.IsTrue(recast.Success, $"Zhao Yun skill 1 should cast after its skill-specific cooldown reset. failReason={recast.FailReason}");
                var recastTrace = harness.TickUntilTraceNodeAfter(recastBaseline, MobaTraceKind.EffectExecution, Skill1.EffectId, maxTicks: 20, message: "Zhao Yun skill 1 recast should create a new effect trace after cooldown reset.");
                Assert.AreNotEqual(effectTrace.RootId, recastTrace.RootId, "Zhao Yun skill 1 recast should use a new root trace.");
            }
        }

        [Test]
        public void Skill10030201_ShouldCreateFourHitAreaAndHealCaster()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(ZhaoYun, "zhaoyun_skill_2_four_hit_heal_contract_world"))
            {
                HeroSkillHeadlessContract.AssertTriggerActions(harness, 10030201, (int)TriggeringConstants.SpawnAreaId.Value, (int)TriggeringConstants.DebugLogId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, 10030211, (int)TriggeringConstants.GiveDamageId.Value, (int)TriggeringConstants.HealId.Value, (int)TriggeringConstants.DebugLogId.Value);

                harness.EnterGameAndWarmup(reason: "zhaoyun skill 2 four hit heal contract");
                var actorId = harness.AssertPlayerActorBound();
                HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 0f, z: 3f);
                var maxHp = harness.GetActorAttribute(actorId, BattleAttributeType.MAX_HP);
                harness.SetScenarioActorAttribute(actorId, "hp", maxHp * 0.5f);
                var injuredHp = harness.GetActorHp(actorId);

                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(actorId, Skill2.Slot, aimPos: default, aimDir: Vec3.Right, targetActorId: 0);
                Assert.IsTrue(cast.Success, $"Zhao Yun skill 2 should cast. failReason={cast.FailReason}");
                var effectTrace = harness.TickUntilTraceNode(MobaTraceKind.EffectExecution, Skill2.EffectId, maxTicks: 20, message: "Zhao Yun skill 2 should execute its area effect.");
                harness.AssertActionExecutedUnderEffect(effectTrace.RootId, (int)TriggeringConstants.SpawnAreaId.Value, TriggeringConstants.Actions.SpawnArea);
                harness.TickUntilTraceNodeInRoot(effectTrace.RootId, MobaTraceKind.AreaSpawn, 40030201, maxTicks: 10, message: "Zhao Yun skill 2 should publish its deferred area spawn under the effect root.");

                harness.TickMilliseconds(900);
                Assert.GreaterOrEqual(harness.CountTraceNodesInRoot(effectTrace.RootId, MobaTraceKind.DamageApply, 10030201), 4, "Zhao Yun skill 2 should apply all four configured spear strikes to a target in the area.");
                Assert.Greater(harness.GetActorHp(actorId), injuredHp, "Zhao Yun skill 2 should heal the caster for successful spear strikes.");
                harness.TickUntilSkillStops(actorId, Skill2.Slot, maxTicks: 120, message: "Zhao Yun skill 2 should finish after its four delayed spear strikes.");
            }
        }

        [Test]
        public void Skill10030301_ShouldJumpToAimPointDamageKnockUpAndMarkTargets()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(ZhaoYun, "zhaoyun_skill_3_landing_control_mark_contract_world"))
            {
                HeroSkillHeadlessContract.AssertTriggerActions(harness, 10030301, (int)TriggeringConstants.DashId.Value, (int)TriggeringConstants.JumpId.Value, (int)TriggeringConstants.SpawnAreaId.Value, (int)TriggeringConstants.DebugLogId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, 10030311, (int)TriggeringConstants.GiveDamageId.Value, (int)TriggeringConstants.AddBuffId.Value, (int)TriggeringConstants.DebugLogId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, 10030321, (int)TriggeringConstants.PullId.Value, (int)TriggeringConstants.DebugLogId.Value);

                harness.EnterGameAndWarmup(reason: "zhaoyun skill 3 landing control mark contract");
                var actorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, x: 5f, z: 2f);
                var start = harness.AssertActorEntity(actorId).transform.Value.Position;
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(actorId, Skill3.Slot, aimPos: new Vec3(5f, 0f, 0f), aimDir: Vec3.Right, targetActorId: 0);
                Assert.IsTrue(cast.Success, $"Zhao Yun skill 3 should cast toward an aim position. failReason={cast.FailReason}");
                var effectTrace = harness.TickUntilTraceNode(MobaTraceKind.EffectExecution, Skill3.EffectId, maxTicks: 20, message: "Zhao Yun skill 3 should execute its jump and landing effect.");
                harness.AssertActionExecutedUnderEffect(effectTrace.RootId, (int)TriggeringConstants.DashId.Value, TriggeringConstants.Actions.Dash);
                harness.AssertActionExecutedUnderEffect(effectTrace.RootId, (int)TriggeringConstants.JumpId.Value, TriggeringConstants.Actions.Jump);
                harness.AssertActionExecutedUnderEffect(effectTrace.RootId, (int)TriggeringConstants.SpawnAreaId.Value, TriggeringConstants.Actions.SpawnArea);
                harness.TickUntilTraceNodeInRoot(effectTrace.RootId, MobaTraceKind.AreaSpawn, 40030301, maxTicks: 10, message: "Zhao Yun skill 3 should publish its deferred landing area spawn under the effect root.");

                harness.TickMilliseconds(700);
                var end = harness.AssertActorEntity(actorId).transform.Value.Position;
                Assert.Greater(end.X - start.X, 3f, $"Zhao Yun skill 3 should move the caster toward its aim position. start={start}, end={end}");
                Assert.GreaterOrEqual(harness.CountTraceNodesInRoot(effectTrace.RootId, MobaTraceKind.DamageApply, 10030301), 1, "Zhao Yun skill 3 landing should damage targets inside the landing area.");
                HeroSkillHeadlessContract.AssertFreshBuff(harness, targetActorId, 10030301, 4f, "Zhao Yun skill 3 should apply the persistent marked-target state after landing.");
                Assert.GreaterOrEqual(harness.CountTraceNodesInRoot(effectTrace.RootId, MobaTraceKind.EffectAction, (int)TriggeringConstants.PullId.Value), 1, "Zhao Yun skill 3 landing area should execute the configured knock-up pull action.");
                harness.TickUntilSkillStops(actorId, Skill3.Slot, maxTicks: 160, message: "Zhao Yun skill 3 should finish after the target-point leap and landing area resolve.");
            }
        }
    }
}

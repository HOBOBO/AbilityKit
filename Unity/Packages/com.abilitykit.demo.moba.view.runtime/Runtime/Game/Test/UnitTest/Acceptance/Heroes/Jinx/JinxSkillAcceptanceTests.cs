using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Core.Mathematics;
using AbilityKit.Ability.Host;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Projectile;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Demo.Moba.View.Config;
using AbilityKit.Deterministic;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Battle.View;
using AbilityKit.Game.Battle.View.Lib.Skill;
using AbilityKit.Modifiers;
using AbilityKit.Trace;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Game;
using UnityEditor.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AbilityKit.Game.Test.UnitTest
{
    /// <summary>
    /// Production-path acceptance for Jinx's hero-local weapon mode and Q toggle.
    /// </summary>
    public sealed class JinxSkillAcceptanceTests : MobaAcceptanceTestBase
    {
        private const int HeroId = 3002;
        private const int QSkillId = 30020101;
        private const int WSkillId = 30020201;
        private const int ESkillId = 30020301;
        private const int RSkillId = 30020401;
        private const int WLaunchTriggerId = 30020203;
        private const int WHitTriggerId = 30020213;
        private const int WLauncherId = 33020203;
        private const int WProjectileId = 32020203;
        private const int WPreviewVfxId = 90032023;
        private const int WHitVfxId = 90032024;
        private const int RLaunchTriggerId = 30020403;
        private const int RHitTriggerId = 30020413;
        private const int RLauncherId = 33020403;
        private const int RProjectileId = 32020403;
        private const int RTargetQueryId = 52020401;
        private const int RPreviewVfxId = 90032025;
        private const int RHitVfxId = 90032026;
        private const int PowPowBuffId = 30020011;
        private const int FishbonesBuffId = 30020012;
        private const int PowPowRemovesFishbonesTriggerId = 30020102;
        private const int FishbonesRemovesPowPowTriggerId = 30020103;
        private const int WeaponModeParameterId = 4;
        private const int BasicAttackSlot = 5;
        private const int BaseAttackSkillId = 30020001;
        private const int PowPowAttackSkillId = 30020002;
        private const int FishbonesAttackSkillId = 30020003;
        private const int PowPowLaunchTriggerId = 30020201;
        private const int FishbonesLaunchTriggerId = 30020202;
        private const int PowPowHitTriggerId = 30020211;
        private const int FishbonesHitTriggerId = 30020212;
        private const int PowPowLauncherId = 33020201;
        private const int FishbonesLauncherId = 33020202;
        private const int PowPowProjectileId = 32020201;
        private const int FishbonesProjectileId = 32020202;

        private static readonly HeroSkillContract Jinx = new HeroSkillContract(
            "Jinx", HeroId, HeroId, new[] { QSkillId, WSkillId, ESkillId, RSkillId });

        [Test]
        public void WeaponModeProfileIsHeroLocalAndSelectsWeaponActions()
        {
            var loader = new ResourcesTextAssetLoader();
            var definition = MobaCharacterHfsmProfile.ResolveDefinition(loader, HeroId);

            Assert.AreEqual("moba.character.3002", definition.DefinitionId);
            Assert.IsTrue(definition.Parameters.Exists(parameter => parameter.Id == WeaponModeParameterId &&
                parameter.Name == "WeaponMode" && parameter.DefaultValue == 0f));
            Assert.IsFalse(MobaCharacterHfsmTestFixture.CreateDefinition().Parameters.Exists(
                parameter => parameter.Id == WeaponModeParameterId),
                "Jinx's WeaponMode option must remain local to her HFSM profile.");

            const int actorId = 3002;
            const long qSourceId = QSkillId;
            var parameters = new MobaCharacterHfsmParameterService();
            parameters.RegisterActorProfile(actorId, definition);
            using (var runtime = new MobaCharacterHfsmRuntime(actorId, definition, 0, Fixed64.Zero,
                       registerForInspection: false, parameters: parameters))
            {
                runtime.ReevaluateCurrentFrame();
                Assert.AreEqual("life/alive/action/weapon.powpow", runtime.Action.Path);

                var fishbones = ModifierData.Override(ModifierKey.FromPacked(WeaponModeParameterId), 1f,
                    sourceId: unchecked((int)qSourceId));
                parameters.Set(actorId, qSourceId, WeaponModeParameterId, in fishbones);
                runtime.ReevaluateCurrentFrame();
                Assert.AreEqual("life/alive/action/weapon.fishbones", runtime.Action.Path);
            }
        }

        [Test]
        public void Q_CastAlternatesWeaponModeThroughMutuallyExclusiveBuffModifiers()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(
                Jinx, "jinx_q_weapon_mode_toggle_runtime_contract_world"))
            {
                Assert.IsTrue(harness.Config.TryGetSkill(QSkillId, out var qSkill),
                    "Jinx Q must load from published production skill data.");
                Assert.AreEqual(QSkillId, qSkill.CastFlowId,
                    "Jinx Q should execute its authored production cast flow.");
                Assert.IsTrue(harness.Config.TryGetBuff(PowPowBuffId, out var powPow),
                    "Pow-Pow mode must load from published Buff data.");
                Assert.IsTrue(harness.Config.TryGetBuff(FishbonesBuffId, out var fishbones),
                    "Fishbones mode must load from published Buff data.");
                CollectionAssert.Contains(powPow.OnAddEffects, PowPowRemovesFishbonesTriggerId,
                    "Adding Pow-Pow mode must immediately request removal of Fishbones mode.");
                CollectionAssert.Contains(fishbones.OnAddEffects, FishbonesRemovesPowPowTriggerId,
                    "Adding Fishbones mode must immediately request removal of Pow-Pow mode.");
                HeroSkillHeadlessContract.AssertTriggerActions(harness, PowPowRemovesFishbonesTriggerId,
                    (int)TriggeringConstants.RemoveBuffId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, FishbonesRemovesPowPowTriggerId,
                    (int)TriggeringConstants.RemoveBuffId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, QSkillId,
                    (int)TriggeringConstants.AddBuffId.Value);

                harness.EnterGameAndWarmup(reason: "Jinx Q switches weapon mode through Buff modifiers");
                var actorId = harness.AssertPlayerActorBound();
                var actor = harness.AssertActorEntity(actorId);
                var parameters = harness.World.Services.Resolve<MobaCharacterHfsmParameterService>();
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();

                Assert.AreEqual(0f, parameters.Resolve(actorId, WeaponModeParameterId),
                    "Jinx should start in Pow-Pow mode from the profile default.");
                Assert.AreEqual("life/alive/action/weapon.powpow", actor.characterHfsm.Runtime.Action.Path,
                    "Jinx's default WeaponMode parameter should select the Pow-Pow HFSM action.");
                Assert.IsFalse(harness.HasActorBuff(actorId, PowPowBuffId));
                Assert.IsFalse(harness.HasActorBuff(actorId, FishbonesBuffId));

                var firstCast = skills.TryCastBySlot(actorId, slot: 1,
                    aimPos: Vec3.Zero, aimDir: Vec3.Right, targetActorId: 0);
                Assert.IsTrue(firstCast.Success,
                    "Jinx Q should release through the production skill slot. " + DescribeCastFailure(firstCast));
                harness.TickUntilTraceNode(MobaExecutionKind.SkillCast, QSkillId, maxTicks: 10,
                    message: "The first Jinx Q press should produce the production skill-cast trace.");
                harness.TickUntilSkillStops(actorId, 1, maxTicks: 30,
                    message: "The first Jinx Q cast flow should complete.");
                harness.Tick(1);

                Assert.IsTrue(harness.HasActorBuff(actorId, FishbonesBuffId),
                    "The first Q cast should enable Fishbones mode.");
                Assert.IsFalse(harness.HasActorBuff(actorId, PowPowBuffId),
                    "Only one Jinx weapon-mode Buff may remain after the first Q cast.");
                Assert.AreEqual(1f, parameters.Resolve(actorId, WeaponModeParameterId),
                    "Fishbones Buff's Modifier should drive WeaponMode to 1.");
                Assert.AreEqual("life/alive/action/weapon.fishbones", actor.characterHfsm.Runtime.Action.Path,
                    "The HFSM should select Fishbones from the Buff-derived parameter.");

                harness.Tick(30);
                var secondCastTraceBaseline = harness.CaptureTraceBaseline();
                var secondCast = skills.TryCastBySlot(actorId, slot: 1,
                    aimPos: Vec3.Zero, aimDir: Vec3.Right, targetActorId: 0);
                Assert.IsTrue(secondCast.Success,
                    "Jinx Q should be castable again after its configured cooldown. " + DescribeCastFailure(secondCast));
                harness.TickUntilTraceNodeAfter(secondCastTraceBaseline, MobaExecutionKind.SkillCast, QSkillId,
                    maxTicks: 10,
                    message: "The second Jinx Q press should produce a new production skill-cast trace.");
                var secondModeEffect = harness.TickUntilTraceNodeAfter(secondCastTraceBaseline,
                    MobaExecutionKind.EffectExecution, QSkillId, maxTicks: 30,
                    message: "The second Jinx Q cast should execute its production weapon-mode Trigger.");
                harness.AssertActionExecutedUnderEffect(secondModeEffect.RootId,
                    (int)TriggeringConstants.AddBuffId.Value);
                harness.TickUntilSkillStops(actorId, 1, maxTicks: 30,
                    message: "The second Jinx Q cast flow should complete.");
                harness.Tick(1);

                Assert.IsTrue(harness.HasActorBuff(actorId, PowPowBuffId),
                    "The second Q cast should return to Pow-Pow mode.");
                Assert.IsFalse(harness.HasActorBuff(actorId, FishbonesBuffId),
                    "Fishbones Buff should be removed as Pow-Pow mode is added.");
                Assert.AreEqual(0f, parameters.Resolve(actorId, WeaponModeParameterId),
                    "Pow-Pow mode should restore WeaponMode to its profile default.");
                Assert.AreEqual("life/alive/action/weapon.powpow", actor.characterHfsm.Runtime.Action.Path,
                    "The HFSM should return to Pow-Pow from the Buff-derived parameter.");
            }
        }

        [Test]
        public void BasicAttack_ResolvesAndCompletesProjectileDamageLoopForBothWeaponModes()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(
                Jinx, "jinx_basic_attack_weapon_mode_projectile_damage_contract_world"))
            {
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness, PowPowLaunchTriggerId,
                    (int)TriggeringConstants.ShootProjectileId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness, FishbonesLaunchTriggerId,
                    (int)TriggeringConstants.ShootProjectileId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness, PowPowHitTriggerId,
                    (int)TriggeringConstants.GiveDamageId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness, FishbonesHitTriggerId,
                    (int)TriggeringConstants.GiveDamageId.Value);
                harness.AssertProjectileConfigExists(PowPowLauncherId, PowPowProjectileId);
                harness.AssertProjectileConfigExists(FishbonesLauncherId, FishbonesProjectileId);

                harness.EnterGameAndWarmup(reason: "Jinx basic attack projectile and damage closure");
                var actorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "jinx_basic_attack_target", 9302, x: 0f, z: 4f);
                var loadout = harness.World.Services.Resolve<MobaSkillLoadoutService>();
                var parameters = harness.World.Services.Resolve<MobaCharacterHfsmParameterService>();
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();

                Assert.IsTrue(loadout.TryResolveSlot(actorId, BasicAttackSlot, out var initialSlot),
                    "Jinx's configured basic-attack slot should resolve after loadout initialization.");
                Assert.AreEqual(BaseAttackSkillId, initialSlot.BaseSkillId,
                    "The mode-specific candidates must project from Jinx's base basic attack skill.");
                Assert.AreEqual(PowPowAttackSkillId, initialSlot.EffectiveSkillId,
                    "Jinx should begin with the Pow-Pow candidate at WeaponMode=0.");
                Assert.AreEqual(0f, parameters.Resolve(actorId, WeaponModeParameterId));

                var powPowHp = harness.GetActorHp(targetActorId);
                var powPowBaseline = harness.CaptureTraceBaseline();
                var powPowCast = skills.TryCastBySlot(actorId, BasicAttackSlot,
                    aimPos: Vec3.Zero, aimDir: Vec3.Forward, targetActorId: targetActorId);
                Assert.IsTrue(powPowCast.Success,
                    "Jinx Pow-Pow basic attack should release through the resolved slot. " + DescribeCastFailure(powPowCast));
                var powPowEffect = harness.TickUntilTraceNodeAfter(
                    powPowBaseline, MobaExecutionKind.EffectExecution, PowPowLaunchTriggerId,
                    maxTicks: 30,
                    message: "Pow-Pow basic attack should execute its authored launch Trigger.");
                harness.AssertProjectileLaunchedUnderEffect(
                    powPowEffect.RootId, PowPowLauncherId, PowPowProjectileId);
                AssertTraceNodeAfterWithDiagnostics(
                    harness, powPowBaseline, "DamageApply", PowPowAttackSkillId,
                    maxTicks: 60,
                    message: "Pow-Pow projectile hit should apply the configured physical basic-attack damage.");
                Assert.Less(harness.GetActorHp(targetActorId), powPowHp,
                    "Pow-Pow projectile hit should reduce the selected target's HP.");
                harness.TickUntilSkillStops(actorId, BasicAttackSlot, maxTicks: 30,
                    message: "Pow-Pow basic attack should finish its release.");

                harness.Tick(30);
                var switchToFishbonesBaseline = harness.CaptureTraceBaseline();
                var switchToFishbones = skills.TryCastBySlot(actorId, 1,
                    aimPos: Vec3.Zero, aimDir: Vec3.Right, targetActorId: 0);
                Assert.IsTrue(switchToFishbones.Success,
                    "Jinx Q should switch to Fishbones before the second basic-attack closure. " + DescribeCastFailure(switchToFishbones));
                harness.TickUntilTraceNodeAfter(
                    switchToFishbonesBaseline, MobaExecutionKind.EffectExecution, QSkillId,
                    maxTicks: 30,
                    message: "Jinx Q should commit the Fishbones mode before the second basic attack.");
                harness.TickUntilSkillStops(actorId, 1, maxTicks: 30,
                    message: "Jinx Q should finish before resolving the Fishbones attack slot.");
                harness.Tick(1);

                Assert.AreEqual(1f, parameters.Resolve(actorId, WeaponModeParameterId));
                Assert.IsTrue(loadout.TryResolveSlot(actorId, BasicAttackSlot, out var fishbonesSlot));
                Assert.AreEqual(FishbonesAttackSkillId, fishbonesSlot.EffectiveSkillId,
                    "Fishbones mode must select the Fishbones candidate from the same HFSM parameter.");

                var fishbonesHp = harness.GetActorHp(targetActorId);
                var fishbonesBaseline = harness.CaptureTraceBaseline();
                var fishbonesCast = skills.TryCastBySlot(actorId, BasicAttackSlot,
                    aimPos: Vec3.Zero, aimDir: Vec3.Forward, targetActorId: targetActorId);
                Assert.IsTrue(fishbonesCast.Success,
                    "Jinx Fishbones basic attack should release through the resolved slot. " + DescribeCastFailure(fishbonesCast));
                var fishbonesEffect = harness.TickUntilTraceNodeAfter(
                    fishbonesBaseline, MobaExecutionKind.EffectExecution, FishbonesLaunchTriggerId,
                    maxTicks: 30,
                    message: "Fishbones basic attack should execute its authored launch Trigger.");
                harness.AssertProjectileLaunchedUnderEffect(
                    fishbonesEffect.RootId, FishbonesLauncherId, FishbonesProjectileId);
                AssertTraceNodeAfterWithDiagnostics(
                    harness, fishbonesBaseline, "DamageApply", FishbonesAttackSkillId,
                    maxTicks: 60,
                    message: "Fishbones projectile hit should apply the configured physical basic-attack damage.");
                Assert.Less(harness.GetActorHp(targetActorId), fishbonesHp,
                    "Fishbones projectile hit should reduce the selected target's HP.");

                harness.TickUntilSkillStops(actorId, BasicAttackSlot, maxTicks: 30,
                    message: "Fishbones basic attack should finish its release.");
                harness.Tick(30);
                var switchBackBaseline = harness.CaptureTraceBaseline();
                var switchBack = skills.TryCastBySlot(actorId, 1,
                    aimPos: Vec3.Zero, aimDir: Vec3.Right, targetActorId: 0);
                Assert.IsTrue(switchBack.Success,
                    "Jinx Q should switch back to Pow-Pow after the Fishbones attack closure. " + DescribeCastFailure(switchBack));
                harness.TickUntilTraceNodeAfter(
                    switchBackBaseline, MobaExecutionKind.EffectExecution, QSkillId,
                    maxTicks: 30,
                    message: "The second Q press should commit the return to Pow-Pow.");
                harness.TickUntilSkillStops(actorId, 1, maxTicks: 30,
                    message: "The second Q cast should finish before the final slot assertion.");
                harness.Tick(1);

                Assert.AreEqual(0f, parameters.Resolve(actorId, WeaponModeParameterId));
                Assert.IsTrue(loadout.TryResolveSlot(actorId, BasicAttackSlot, out var restoredSlot));
                Assert.AreEqual(PowPowAttackSkillId, restoredSlot.EffectiveSkillId,
                    "Returning to Pow-Pow must restore the original candidate on the same basic-attack slot.");
            }
        }

        [Test]
        public void W_FiresDirectionalProjectileDealsRankDamageOnceAndStopsAtFirstEnemy()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(
                Jinx, "jinx_w_directional_projectile_damage_contract_world"))
            {
                Assert.IsTrue(harness.Config.TryGetSkill(WSkillId, out var wSkill),
                    "Jinx W must load from published production skill data.");
                Assert.AreEqual(WSkillId, wSkill.CastFlowId);
                Assert.AreEqual(15f, wSkill.Range, 0.001f);
                Assert.AreEqual(0, wSkill.RequiredTargetQueryId,
                    "Jinx W is direction-aimed and must not require a target query to cast.");
                Assert.AreEqual(WPreviewVfxId, wSkill.PreviewVfxId,
                    "Jinx W should use its authored Zap range resource for the directional preview.");
                Assert.AreEqual(2, wSkill.SkillButtonTemplateId,
                    "Jinx W should use the shared directional-line aim template.");
                Assert.IsTrue(wSkill.AllowCastWithoutTarget,
                    "Jinx W aims by direction and must not require an explicit unit target.");
                Assert.IsTrue(harness.Config.TryGetSkillButtonTemplate(wSkill.SkillButtonTemplateId, out var button),
                    "Jinx W directional-line button template must be published.");
                Assert.IsTrue(button.EnableAim);
                Assert.AreEqual((int)SkillAimMode.Direction, button.AimMode);
                Assert.AreEqual((int)SkillAimIndicatorShape.DirectionLine, button.IndicatorShape);
                Assert.AreEqual((int)SkillUsePointMode.Aim, button.UsePointMode);

                Assert.IsTrue(harness.Config.TryGetSkillLevelTable(WSkillId, out var levelTable),
                    "Jinx W skill-rank parameter table must be published.");
                CollectionAssert.AreEqual(
                    new[] { "damage", "physicalAttackRatio", "range", "speed" }, levelTable.ParamNames);
                Assert.AreEqual(5, levelTable.Levels.Count);
                CollectionAssert.AreEqual(new[] { 10f, 1.4f, 14.5f, 33f }, levelTable.Levels[0].Params);
                CollectionAssert.AreEqual(new[] { 210f, 1.4f, 14.5f, 33f }, levelTable.Levels[4].Params);

                harness.AssertSlotSkill(2, WSkillId);
                harness.AssertSkillUsesCastFlow(WSkillId, WSkillId);
                harness.AssertCastFlowContainsTimelineEffect(WSkillId, WLaunchTriggerId);
                harness.AssertProjectileConfigExists(WLauncherId, WProjectileId);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness, WLaunchTriggerId, (int)TriggeringConstants.ShootProjectileId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness, WHitTriggerId, (int)TriggeringConstants.GiveDamageId.Value);

                Assert.IsTrue(harness.Config.TryGetProjectile(WProjectileId, out var projectile));
                Assert.AreEqual(ProjectileHitPolicyKind.ExitOnHit, projectile.HitPolicyKind);
                Assert.AreEqual(1, projectile.HitsRemaining);
                Assert.AreEqual(WHitTriggerId, projectile.OnHitEffectId);
                Assert.AreEqual(WHitVfxId, projectile.OnHitVfxId,
                    "Jinx W should play its authored hit resource at the first valid target.");

                harness.EnterGameAndWarmup(reason: "Jinx W directional projectile hit closure");
                var casterActorId = harness.AssertPlayerActorBound();
                harness.AssertSlotSkill(2, WSkillId);
                var baseline = harness.CaptureTraceBaseline();
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(casterActorId, slot: 2,
                    aimPos: Vec3.Zero, aimDir: Vec3.Forward, targetActorId: 0);
                Assert.IsTrue(cast.Success,
                    "Jinx W should release from slot 2 with a directional aim. " + DescribeCastFailure(cast));
                harness.TickUntilTraceNodeAfter(
                    baseline, MobaExecutionKind.SkillCast, WSkillId, maxTicks: 10,
                    message: "Jinx W should start its own cast before the projectile closure is checked.");

                TraceSnapshot<MobaTraceMetadata> effect;
                try
                {
                    effect = harness.TickUntilTraceNodeAfter(
                        baseline, MobaExecutionKind.EffectExecution, WLaunchTriggerId, maxTicks: 40,
                        message: "Jinx W's timeline should execute its launch Trigger.");
                }
                catch (AssertionException exception)
                {
                    var diagnosticRecords = MobaAcceptanceTraceExporter.CaptureTraceRecords(
                        harness, "jinx_w_directional_projectile_damage_contract_world");
                    var trace = string.Empty;
                    for (var i = 0; i < diagnosticRecords.Length; i++)
                    {
                        var record = diagnosticRecords[i];
                        if (record.nodeId <= baseline) continue;
                        if (trace.Length > 0) trace += "; ";
                        trace += record.kind + ":" + record.configId +
                                 ":root=" + record.rootId + ":parent=" + record.parentId;
                    }

                    Assert.Fail(exception.Message + " " +
                                harness.DescribeSkillRuntimeState(casterActorId, slot: 2) +
                                " castTrace=" + (trace.Length > 0 ? trace : "<empty>"));
                    return;
                }
                harness.AssertProjectileLaunchedUnderEffect(effect.RootId, WLauncherId, WProjectileId);

                var firstTarget = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "jinx_w_first_target", 9303, x: 0f, z: 5f);
                var secondTarget = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "jinx_w_second_target", 9304, x: 0f, z: 9f);
                var firstHp = harness.GetActorHp(firstTarget);
                var secondHp = harness.GetActorHp(secondTarget);
                harness.Tick(100);

                var records = MobaAcceptanceTraceExporter.CaptureTraceRecords(
                    harness, "jinx_w_directional_projectile_damage_contract_world");
                var hits = 0;
                var damages = 0;
                var firstDamaged = false;
                for (var i = 0; i < records.Length; i++)
                {
                    var record = records[i];
                    if (record.nodeId <= baseline) continue;
                    if (record.kind == "ProjectileHit" && record.configId == WProjectileId) hits++;
                    if (record.kind == "DamageApply" && record.configId == WSkillId)
                    {
                        damages++;
                        firstDamaged |= record.targetActorId == firstTarget;
                    }
                }

                Assert.AreEqual(1, hits, "Jinx W must stop after its first valid enemy hit.");
                Assert.AreEqual(1, damages, "Jinx W should apply one damage event for that hit.");
                Assert.IsTrue(firstDamaged, "The first enemy on W's aim line should receive the hit effect.");
                Assert.Less(harness.GetActorHp(firstTarget), firstHp,
                    "Jinx W rank damage plus the physical-attack ratio should reduce the first target's HP.");
                Assert.AreEqual(secondHp, harness.GetActorHp(secondTarget), 0.001f,
                    "The first-hit projectile should terminate before reaching the second enemy.");
            }
        }

        [Test]
        public void R_SkipsNonHeroesAndExplodesOnceOnFirstEnemyHeroUsingHitSnapshot()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(
                Jinx, "jinx_r_first_enemy_hero_explosion_contract_world"))
            {
                Assert.IsTrue(harness.Config.TryGetSkill(RSkillId, out var skill),
                    "Jinx R must load from published production skill data.");
                Assert.AreEqual(RSkillId, skill.CastFlowId);
                Assert.AreEqual(SkillType.Active, skill.SkillType,
                    "Jinx R is a directional active skill and must not enter normal-attack target acquisition.");
                Assert.AreEqual(RPreviewVfxId, skill.PreviewVfxId,
                    "The R directional preview must reference its registered projectile VFX.");
                Assert.AreEqual(0, skill.RequiredTargetQueryId,
                    "Jinx R is direction-aimed and should not require an explicit unit target.");
                Assert.IsTrue(skill.AllowCastWithoutTarget);
                Assert.AreEqual(2, skill.SkillButtonTemplateId,
                    "Jinx R should use the shared direction-line input template.");
                Assert.IsTrue(harness.Config.TryGetSkillButtonTemplate(skill.SkillButtonTemplateId, out var button));
                Assert.IsTrue(button.EnableAim);
                Assert.AreEqual((int)SkillAimMode.Direction, button.AimMode);
                Assert.AreEqual((int)SkillAimIndicatorShape.DirectionLine, button.IndicatorShape);
                Assert.AreEqual((int)SkillUsePointMode.Aim, button.UsePointMode);

                Assert.IsTrue(harness.Config.TryGetSkillLevelTable(RSkillId, out var levels),
                    "Jinx R must publish replaceable rank parameters.");
                CollectionAssert.AreEqual(
                    new[] { "damage", "flightBonusPerSecond", "missingHpRatioCoefficient", "secondaryDamageScale" },
                    levels.ParamNames);
                Assert.AreEqual(3, levels.Levels.Count);
                CollectionAssert.AreEqual(new[] { 100f, 10f, 0.1f, 0.5f }, levels.Levels[0].Params);

                harness.AssertSkillUsesCastFlow(RSkillId, RSkillId);
                harness.AssertCastFlowContainsTimelineEffect(RSkillId, RLaunchTriggerId);
                harness.AssertProjectileConfigExists(RLauncherId, RProjectileId);
                Assert.IsTrue(harness.Config.TryGetProjectile(RProjectileId, out var projectile));
                Assert.AreEqual(ProjectileHitPolicyKind.ExitOnHit, projectile.HitPolicyKind);
                Assert.AreEqual(1, projectile.HitsRemaining);
                Assert.AreEqual((int)UnitSubType.Hero, projectile.HitUnitSubType,
                    "Only enemy Heroes should be accepted as projectile hits; minions and monsters must be passed through.");
                Assert.AreEqual(RHitTriggerId, projectile.OnHitEffectId);
                Assert.AreEqual(RPreviewVfxId, projectile.VfxId);
                Assert.AreEqual(RHitVfxId, projectile.OnHitVfxId);

                Assert.IsTrue(harness.Config.TryGetSearchQueryTemplate(RTargetQueryId, out var explosionQuery),
                    "R's secondary-target query must be published.");
                Assert.AreEqual((int)SearchQueryExplicitTargetPolicy.IgnoreExplicitTarget,
                    explosionQuery.ExplicitTargetPolicy);
                Assert.AreEqual((int)SearchTargetProviderKind.EnemyTeam, explosionQuery.Provider.Kind);
                Assert.AreEqual(10, explosionQuery.MaxCount);
                Assert.AreEqual(3, explosionQuery.Rules.Length);
                Assert.AreEqual((int)SearchTargetRuleKind.CircleShape, explosionQuery.Rules[0].Kind);
                Assert.AreEqual((int)SearchTargetPointKind.ProjectileHit, explosionQuery.Rules[0].Center);
                Assert.AreEqual(4.5f, explosionQuery.Rules[0].Radius, 0.001f);
                Assert.AreEqual((int)SearchTargetRuleKind.RequireUnitSubType, explosionQuery.Rules[1].Kind);
                Assert.AreEqual((int)UnitSubType.Hero, explosionQuery.Rules[1].UnitSubType);
                Assert.AreEqual((int)SearchTargetRuleKind.ExcludeExplicitTarget, explosionQuery.Rules[2].Kind);

                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness, RLaunchTriggerId, (int)TriggeringConstants.ShootProjectileId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness, RHitTriggerId, (int)TriggeringConstants.GiveDamageId.Value);

                harness.EnterGameAndWarmup(reason: "Jinx R first eligible enemy Hero impact and explosion closure");
                var casterActorId = harness.AssertPlayerActorBound();
                harness.AssertSlotSkill(4, RSkillId);
                var casterPosition = harness.AssertActorEntity(casterActorId).transform.Value.Position;
                MobaAcceptanceVector3Expectation Position(float xOffset, float zOffset) =>
                    new MobaAcceptanceVector3Expectation
                    {
                        x = casterPosition.X + xOffset,
                        y = casterPosition.Y,
                        z = casterPosition.Z + zOffset
                    };

                int Spawn(string alias, int actorId, string kind, int subtype, float xOffset, float zOffset,
                    string sourceKind, int sourceId)
                {
                    return harness.SpawnScenarioActor(
                        alias, actorId, kind, teamId: 2, heroId: 1002, attributeTemplateId: 1002,
                        level: 1, unitSubType: subtype, mainType: (int)EntityMainType.Unit,
                        ownerPlayerId: "jinx_r_enemy", ownerActorId: 0,
                        sourceKind: sourceKind, sourceId: sourceId,
                        position: Position(xOffset, zOffset));
                }

                var minion = Spawn("r_minion_before_hero", 9401, "Minion",
                    (int)UnitSubType.Minion, 0f, 3f, "Unknown", 0);
                var monster = Spawn("r_monster_before_hero", 9402, "Monster",
                    (int)UnitSubType.Neutral, 0f, 5f, "Unknown", 0);
                var primary = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "r_primary_hero", 9403, x: casterPosition.X, z: casterPosition.Z + 8f);
                var secondary = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "r_secondary_hero", 9404, x: casterPosition.X + 3.2f, z: casterPosition.Z + 8f);
                var rearHero = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "r_rear_splash_hero", 9405, x: casterPosition.X, z: casterPosition.Z + 11f);
                var outsideBlast = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "r_outside_blast_hero", 9406, x: casterPosition.X + 7f, z: casterPosition.Z + 8f);
                var heroAfterPrimary = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "r_hero_after_primary", 9407, x: casterPosition.X, z: casterPosition.Z + 14f);

                foreach (var actorId in new[] { primary, secondary, rearHero, outsideBlast, heroAfterPrimary, minion, monster })
                {
                    harness.SetScenarioActorAttribute(actorId, "max_hp", 10000f);
                    harness.SetScenarioActorAttribute(actorId, "hp", actorId == primary ? 5000f : 10000f);
                }

                var baseline = harness.CaptureTraceBaseline();
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var aimPosition = new Vec3(casterPosition.X, casterPosition.Y, casterPosition.Z + 20f);
                var cast = skills.TryCastBySlot(casterActorId, 4,
                    aimPos: aimPosition, aimDir: Vec3.Forward, targetActorId: 0);
                Assert.IsTrue(cast.Success,
                    "Jinx R should release directionally with no explicit target. " + DescribeCastFailure(cast));

                var records = System.Array.Empty<MobaAcceptanceTraceRecord>();
                var completed = false;
                var projectileLinks = harness.World.Services.Resolve<MobaProjectileLinkService>();
                var projectileService = harness.World.Services.Resolve<IProjectileService>();
                var activeProjectiles = new List<MobaProjectileLinkDiagnostics>(4);
                var lastProjectileState = "<none>";
                for (var tick = 0; tick < 600; tick++)
                {
                    harness.Tick(1);
                    activeProjectiles.Clear();
                    projectileLinks.CopyDiagnosticsTo(activeProjectiles);
                    for (var projectileIndex = 0; projectileIndex < activeProjectiles.Count; projectileIndex++)
                    {
                        var link = activeProjectiles[projectileIndex];
                        if (!projectileService.TryGetRuntimeState(link.ProjectileId, out var state)) continue;
                        lastProjectileState = "frame=" + harness.FrameTime.Frame.Value +
                            " id=" + link.ProjectileId.Value +
                            " actor=" + link.ActorId +
                            " pos=(" + state.Position.X + "," + state.Position.Y + "," + state.Position.Z + ")" +
                            " dir=(" + state.Direction.X + "," + state.Direction.Y + "," + state.Direction.Z + ")" +
                            " distance=" + state.DistanceTravelled;
                    }
                    records = MobaAcceptanceTraceExporter.CaptureTraceRecords(
                        harness, "jinx_r_first_enemy_hero_explosion_contract_world");
                    var primaryDamages = 0;
                    var secondaryDamages = 0;
                    var rearDamages = 0;
                    for (var i = 0; i < records.Length; i++)
                    {
                        var record = records[i];
                        if (record.nodeId <= baseline || record.kind != "DamageApply" || record.configId != RSkillId)
                            continue;
                        if (record.targetActorId == primary) primaryDamages++;
                        if (record.targetActorId == secondary) secondaryDamages++;
                        if (record.targetActorId == rearHero) rearDamages++;
                    }
                    if (primaryDamages == 1 && secondaryDamages == 1 && rearDamages == 1)
                    {
                        completed = true;
                        break;
                    }
                    if (activeProjectiles.Count == 0) break;
                }

                var traceSummary = new System.Text.StringBuilder();
                var traceSummaryCount = 0;
                for (var i = 0; i < records.Length; i++)
                {
                    var record = records[i];
                    if (record.nodeId <= baseline) continue;
                    if (traceSummaryCount >= 40) break;
                    if (traceSummaryCount > 0) traceSummary.Append("; ");
                    traceSummary.Append(record.kind).Append('#').Append(record.configId)
                        .Append('[').Append(record.sourceActorId).Append("->").Append(record.targetActorId)
                        .Append("]@f").Append(record.frame);
                    traceSummaryCount++;
                }
                Assert.IsTrue(completed,
                    "R should resolve one primary and one damage event per valid secondary hero inside the impact circle. " +
                    "Trace events=" + traceSummaryCount + ": " +
                    (traceSummaryCount == 0 ? "<none>" : traceSummary.ToString()) +
                    " Last projectile state: " + lastProjectileState);
                var projectileHits = 0;
                var primaryHitEvents = 0;
                var damageCounts = new Dictionary<int, int>();
                for (var i = 0; i < records.Length; i++)
                {
                    var record = records[i];
                    if (record.nodeId <= baseline) continue;
                    if (record.kind == "ProjectileHit" && record.configId == RProjectileId)
                    {
                        projectileHits++;
                        if (record.targetActorId == primary) primaryHitEvents++;
                    }
                    if (record.kind == "DamageApply" && record.configId == RSkillId)
                    {
                        var targetId = (int)record.targetActorId;
                        damageCounts[targetId] = damageCounts.TryGetValue(targetId, out var count) ? count + 1 : 1;
                    }
                }

                Assert.AreEqual(1, projectileHits,
                    "Non-Hero collisions must continue the projectile; the first enemy Hero must terminate it.");
                Assert.AreEqual(1, primaryHitEvents,
                    "The primary target is the first enemy Hero accepted by the projectile.");
                Assert.AreEqual(1, damageCounts[primary],
                    "The direct hit Hero must not be selected again by the explosion query.");
                Assert.AreEqual(1, damageCounts[secondary]);
                Assert.AreEqual(1, damageCounts[rearHero]);
                Assert.IsFalse(damageCounts.ContainsKey(outsideBlast),
                    "Heroes outside the impact-point circle must not be damaged.");
                Assert.IsFalse(damageCounts.ContainsKey(heroAfterPrimary),
                    "The projectile must stop at the first enemy Hero instead of reaching a later Hero.");
                Assert.IsFalse(damageCounts.ContainsKey(minion), "R's impact query must exclude minions.");
                Assert.IsFalse(damageCounts.ContainsKey(monster), "R's impact query must exclude monsters.");
                Assert.Less(harness.GetActorHp(primary), 5000f,
                    "R's primary hit should consume the configured damage and impact snapshot.");
                Assert.Less(harness.GetActorHp(secondary), 10000f);
                Assert.Less(harness.GetActorHp(rearHero), 10000f);
                Assert.AreEqual(10000f, harness.GetActorHp(outsideBlast), 0.001f);
                Assert.AreEqual(10000f, harness.GetActorHp(heroAfterPrimary), 0.001f);
                Assert.AreEqual(10000f, harness.GetActorHp(minion), 0.001f);
                Assert.AreEqual(10000f, harness.GetActorHp(monster), 0.001f);
            }
        }

        [UnityTest]
        public IEnumerator QAndW_LocalHudInputFeatureCommitsQAndDealsWLineDamage()
        {
            JinxHudInputFixture hud = null;
            try
            {
                var scene = DemoGameplayTestLauncher.OpenMobaLocalAndCompose();
                Assert.IsTrue(scene.IsValid(), "Unified gameplay scene should load for the local MOBA profile.");

                var entry = UnityEngine.Object.FindObjectOfType<GameEntry>();
                Assert.IsNotNull(entry, "Demo scene must contain a GameEntry.");
                InvokePrivate(entry, "Awake");
                var flow = entry.Get<GameFlowDomain>();
                Assert.IsNotNull(flow, "GameEntry must expose GameFlowDomain after Awake.");

                flow.StartWithPersistentSettingsSync();
                FlowTick(flow, 2);
                if (flow.CurrentPhase != MobaRootState.Battle &&
                    flow.BattleEntryState != MobaBattleEntryState.Pending)
                {
                    flow.EnterBattle(new TestBattleBootstrapper());
                }

                yield return TickUntil(flow,
                    () => flow.CurrentBattlePhase == MobaBattleState.InMatch,
                    6000,
                    "The local MOBA battle did not reach InMatch.");
                Assert.IsTrue(entry.TryGet(out BattleContext ctx),
                    "BattleContext must be attached after entering battle.");

                var plan = ctx.Plan;
                var playerId = BattleInputSessionIdentity.ResolvePlayerId(
                    (IBattleInputSessionIdentityPort)ctx);
                var worldId = BattleInputSessionIdentity.ResolveWorldId(in plan);
                var replaceHeroFrame = SessionSimRuntimeTuning.ResolveInputSubmitFrame(ctx.LastFrame, in plan);
                var replaceHeroCommand = BattleInputCommandFactory.CreateDebugReplaceHero(
                    replaceHeroFrame, playerId, HeroId);
                var replacementSubmitted = new BattleInputSubmitter(ctx.InputRuntime, playerId, worldId)
                    .Submit(in replaceHeroCommand);
                Assert.IsTrue(replacementSubmitted,
                    "The local Jinx acceptance must submit hero replacement through the production input port.");

                yield return TickUntil(flow,
                    () => IsPlayerUsingHero(ctx, "p1", HeroId) &&
                          TryGetPlayerActor(ctx, "p1", out _, out var actor) &&
                          actor.hasSkillLoadout && actor.hasTransform,
                    240,
                    "The local player did not finish replacing its loadout with Jinx.");

                Assert.IsTrue(ctx.Session.TryGetWorld(out var world),
                    "The local battle session should expose its logic world.");
                var playerActors = world.Services.Resolve<MobaPlayerActorMapService>();
                Assert.IsTrue(playerActors.TryGetActorId(new PlayerId("p2"), out var enemyActorId),
                    "The local training opponent should have an actor mapping.");
                var actors = world.Services.Resolve<MobaActorLookupService>();
                Assert.IsTrue(actors.TryGetActorEntity(enemyActorId, out var enemy) && enemy != null &&
                              enemy.hasAttributeGroup && enemy.hasTransform,
                    "The training opponent must expose combat attributes and a transform.");
                var enemyTransform = enemy.transform.Value;
                enemy.ReplaceTransform(new Transform3(new Vec3(0f, enemyTransform.Position.Y, 5f),
                    enemyTransform.Rotation, enemyTransform.Scale));
                FlowTick(flow, 1);

                var config = world.Services.Resolve<MobaConfigDatabase>();
                Assert.IsNotNull(config, "The local Jinx battle should resolve the published MOBA config database.");
                Assert.IsTrue(config.TryGetSkill(WSkillId, out var wSkill));
                Assert.IsTrue(config.TryGetSkillButtonTemplate(wSkill.SkillButtonTemplateId, out var wTemplate));
                var wSpec = new BattleHudSkillPresentationSpecResolver().Resolve(wSkill, wTemplate);
                Assert.AreEqual(BattleHudSkillPreviewShape.DirectionLine, wSpec.PreviewShape,
                    "The production W template must resolve to a directional-line preview.");
                Assert.AreEqual(SkillAimIndicatorShape.DirectionLine, wSpec.IndicatorShape);
                Assert.AreEqual(SkillAimMode.Direction, wSpec.AimMode);
                Assert.AreEqual(SkillUsePointMode.Aim, wSpec.UsePointMode);
                Assert.AreEqual(wSkill.Range, wSpec.Range, 0.001f,
                    "The indicator line length must use the same authored range as W.");
                Assert.AreEqual(wTemplate.IndicatorWorldWidth > 0f ? wTemplate.IndicatorWorldWidth : 1.5f,
                    wSpec.Width, 0.001f,
                    "The HUD line width must resolve from the shared button template.");
                Assert.AreEqual(WPreviewVfxId, wSpec.PreviewVfxId,
                    "The directional preview must retain W's configured preview resource.");

                var inputFeature = entry.Get<BattleInputFeature>();
                Assert.IsNotNull(inputFeature, "The composed local battle must register BattleInputFeature.");
                var weaponParameters = world.Services.Resolve<MobaCharacterHfsmParameterService>();
                Assert.AreEqual(0f, weaponParameters.Resolve(ctx.LocalActorId, WeaponModeParameterId),
                    "Jinx should begin in Pow-Pow mode before the HUD Q click.");

                hud = JinxHudInputFixture.Create(ctx, config, slot: 1, skillId: QSkillId);
                var qButtonCenter = hud.ButtonScreenPosition;
                var qSubmitsBefore = inputFeature.SkillSubmitSuccessCount;
                hud.PointerDown(qButtonCenter);
                hud.PointerUp(qButtonCenter);
                yield return TickUntil(flow,
                    () => inputFeature.SkillSubmitSuccessCount > qSubmitsBefore,
                    12,
                    "The Q button click did not reach BattleInputFeature.");
                Assert.AreEqual(qSubmitsBefore + 1, inputFeature.SkillSubmitSuccessCount,
                    "One Q HUD click must submit exactly one skill command.");
                yield return TickUntil(flow,
                    () => weaponParameters.Resolve(ctx.LocalActorId, WeaponModeParameterId) == 1f,
                    90,
                    "The HUD-submitted Q did not commit Fishbones mode through its Buff Modifier.");
                hud.Dispose();
                hud = null;

                var enemyHpBefore = enemy.GetMobaAttrs().Hp;
                hud = JinxHudInputFixture.Create(ctx, config, slot: 2, skillId: WSkillId);
                Assert.AreEqual(SkillAimIndicatorShape.DirectionLine, hud.Button.Config.IndicatorShape,
                    "The W button itself must receive the resolved DirectionLine configuration.");
                var wButtonCenter = hud.ButtonScreenPosition;
                var wSubmitsBefore = inputFeature.SkillSubmitSuccessCount;
                const string missingWPresentation =
                    "Missing MOBA presentation timeline: moba/action_timeline/skill_30020201.moba.presentation";
                LogAssert.Expect(LogType.Error, missingWPresentation);
                hud.PointerDown(wButtonCenter);
                hud.Drag(wButtonCenter + new Vector2(0f, 120f));
                hud.PointerUp(wButtonCenter + new Vector2(0f, 200f));

                yield return TickUntil(flow,
                    () => inputFeature.SkillSubmitSuccessCount > wSubmitsBefore,
                    12,
                    "The directional W release did not reach BattleInputFeature.");
                Assert.AreEqual(wSubmitsBefore + 1, inputFeature.SkillSubmitSuccessCount,
                    "One W HUD release must submit exactly one skill command.");
                Assert.IsFalse(BattleHudInputSource.TryConsumeSkillAimSubmit(ctx, out _),
                    "BattleInputFeature should consume the W aim snapshot exactly once.");

                for (var tick = 0; tick < 180 && enemy.GetMobaAttrs().Hp >= enemyHpBefore; tick++)
                {
                    FlowTick(flow, 1);
                    if (enemy.GetMobaAttrs().Hp < enemyHpBefore) break;
                    yield return null;
                }

                Assert.Less(enemy.GetMobaAttrs().Hp, enemyHpBefore,
                    "The production W projectile should damage the enemy on the released DirectionLine. " +
                    $"hpBefore={enemyHpBefore:F2}, hpAfter={enemy.GetMobaAttrs().Hp:F2}, " +
                    $"submits={inputFeature.SkillSubmitSuccessCount - wSubmitsBefore}, " +
                    $"enemyPosition={enemy.transform.Value.Position}");
            }
            finally
            {
                hud?.Dispose();
                if (GameEntry.IsInitialized && GameEntry.Instance != null)
                    UnityEngine.Object.DestroyImmediate(GameEntry.Instance.gameObject);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                DemoGameplayTestLauncher.Clear();
            }
        }

        private static string DescribeCastFailure(AbilityKit.Demo.Moba.Services.MobaSkillCastResult cast)
        {
            var failure = cast.Failure.HasValue ? cast.Failure.ToString() : "<none>";
            return $"failReason={cast.FailReason}, failure={failure}";
        }

        private static void AssertTraceNodeAfterWithDiagnostics(
            MobaSkillConfigTestHarness harness,
            long baseline,
            string kind,
            int configId,
            int maxTicks,
            string message)
        {
            for (var i = 0; i <= maxTicks; i++)
            {
                var records = MobaAcceptanceTraceExporter.CaptureTraceRecords(
                    harness, "jinx_basic_attack_diagnostics");
                for (var j = 0; j < records.Length; j++)
                {
                    var record = records[j];
                    if (record.nodeId > baseline && record.kind == kind && record.configId == configId)
                        return;
                }

                if (i < maxTicks) harness.Tick(1);
            }

            var finalRecords = MobaAcceptanceTraceExporter.CaptureTraceRecords(
                harness, "jinx_basic_attack_diagnostics");
            var trace = string.Empty;
            for (var i = 0; i < finalRecords.Length; i++)
            {
                var record = finalRecords[i];
                if (record.nodeId <= baseline) continue;
                if (trace.Length > 0) trace += "; ";
                trace += record.kind + ":" + record.configId +
                         ":source=" + record.sourceActorId +
                         ":target=" + record.targetActorId +
                         ":root=" + record.rootId +
                         ":parent=" + record.parentId;
            }

            Assert.Fail(message + " trace=" + (trace.Length > 0 ? trace : "<empty>") +
                        " projectile=" + CaptureProjectileLifecycleSummary(harness) +
                        " actors=" + CaptureActorSummary(harness));
        }

        private static string CaptureProjectileLifecycleSummary(MobaSkillConfigTestHarness harness)
        {
            var provider = harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>();
            var snapshots = new List<WorldStateSnapshot>(8);
            provider.CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
            var summary = string.Empty;
            for (var i = 0; i < snapshots.Count; i++)
            {
                if (snapshots[i].OpCode != MobaOpCodes.Snapshot.ProjectileEvent) continue;
                var entries = MobaProjectileEventSnapshotCodec.Deserialize(snapshots[i].Payload);
                for (var j = 0; j < entries.Length; j++)
                {
                    if (entries[j].TemplateId != PowPowProjectileId && entries[j].TemplateId != FishbonesProjectileId) continue;
                    if (summary.Length > 0) summary += ";";
                    summary += ((ProjectileEventKind)entries[j].Kind).ToString() +
                               ":template=" + entries[j].TemplateId +
                               ":projectile=" + entries[j].ProjectileId +
                               ":exit=" + entries[j].ExitReason +
                               ":collider=" + entries[j].HitCollider +
                               ":pos=(" + entries[j].X + "," + entries[j].Y + "," + entries[j].Z + ")";
                }
            }

            return summary.Length > 0 ? summary : "<empty>";
        }

        private static string CaptureActorSummary(MobaSkillConfigTestHarness harness)
        {
            var registry = harness.World.Services.Resolve<MobaActorRegistry>();
            var summary = string.Empty;
            foreach (var pair in registry.Entries)
            {
                var actor = pair.Value;
                if (actor == null || !actor.hasActorId || !actor.hasTransform) continue;
                if (summary.Length > 0) summary += ";";
                summary += "actor=" + actor.actorId.Value +
                           ":team=" + (actor.hasTeam ? actor.team.Value.ToString() : "none") +
                           ":collider=" + (actor.hasCollisionId ? actor.collisionId.Value.Value.ToString() : "none") +
                           ":pos=(" + actor.transform.Value.Position.X + "," +
                           actor.transform.Value.Position.Y + "," +
                           actor.transform.Value.Position.Z + ")";
            }

            return summary.Length > 0 ? summary : "<empty>";
        }

        private sealed class JinxHudInputFixture : System.IDisposable
        {
            private readonly BattleHudInputUi _ui;
            private readonly BattleHudInputEventBridge _bridge;
            private readonly SkillButtonView _button;

            private JinxHudInputFixture(
                BattleHudInputUi ui,
                BattleHudInputEventBridge bridge,
                SkillButtonView button)
            {
                _ui = ui;
                _bridge = bridge;
                _button = button;
            }

            public SkillButtonView Button => _button;
            public Vector2 ButtonScreenPosition =>
                RectTransformUtility.WorldToScreenPoint(null, _button.transform.position);

            public static JinxHudInputFixture Create(
                BattleContext context,
                MobaConfigDatabase configs,
                int slot,
                int skillId)
            {
                Assert.IsTrue(configs.TryGetSkill(skillId, out var skill),
                    "The HUD fixture skill must be present in production config.");
                Assert.IsTrue(configs.TryGetSkillButtonTemplate(skill.SkillButtonTemplateId, out var template),
                    "The HUD fixture button template must be present in production config.");

                var spec = new BattleHudSkillPresentationSpecResolver().Resolve(skill, template);
                var root = new GameObject("JinxHudInputFixture.Root", typeof(RectTransform));
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = new Vector2(1200f, 900f);
                var buttons = new SkillButtonView[4];
                SkillButtonView targetButton = null;
                for (var i = 0; i < buttons.Length; i++)
                {
                    var buttonObject = new GameObject("Skill" + (i + 1), typeof(RectTransform));
                    buttonObject.transform.SetParent(root.transform, false);
                    var buttonRect = (RectTransform)buttonObject.transform;
                    buttonRect.anchoredPosition = new Vector2(-400f + i * 100f, -300f);
                    buttonRect.sizeDelta = new Vector2(80f, 80f);
                    var button = buttonObject.AddComponent<SkillButtonView>();
                    button.Initialize(buttonRect, rootRect, null, SkillButtonConfig.Default);
                    if (i + 1 == slot)
                    {
                        new BattleHudSkillButtonTemplateApplier().Apply(button, template, spec);
                        targetButton = button;
                    }

                    buttons[i] = button;
                }

                var inputView = root.AddComponent<BattleHudInputView>();
                inputView.Initialize(null, buttons);
                var mapper = new BattleHudSkillAimInputMapper();
                mapper.Initialize(inputView, null);
                var ui = new BattleHudInputUi(root, inputView, null, null, mapper, buttons, null);
                var bridge = new BattleHudInputEventBridge(context);
                bridge.SetSkillSpecs(new Dictionary<int, BattleHudSkillPresentationSpec>
                {
                    [slot] = spec
                });
                bridge.Bind(ui);
                return new JinxHudInputFixture(ui, bridge, targetButton);
            }

            public void PointerDown(Vector2 position) => _button.OnPointerDown(CreatePointer(position));
            public void Drag(Vector2 position) => _button.OnDrag(CreatePointer(position));
            public void PointerUp(Vector2 position) => _button.OnPointerUp(CreatePointer(position));

            public void Dispose()
            {
                _bridge.Dispose();
                _ui.Destroy();
            }

            private static PointerEventData CreatePointer(Vector2 position) =>
                new PointerEventData(EventSystem.current) { pointerId = 1, position = position };
        }

        private static IEnumerator TickUntil(
            GameFlowDomain flow,
            System.Func<bool> predicate,
            int maxTicks,
            string failureMessage)
        {
            for (var tick = 0; tick < maxTicks; tick++)
            {
                FlowTick(flow, 1);
                if (predicate()) yield break;
                yield return null;
            }

            Assert.Fail(failureMessage);
        }

        private static void FlowTick(GameFlowDomain flow, int ticks)
        {
            const float fixedDeltaTime = 1f / 30f;
            for (var tick = 0; tick < ticks; tick++) flow.Tick(fixedDeltaTime);
        }

        private static bool IsPlayerUsingHero(BattleContext context, string playerId, int heroId)
        {
            var loadouts = context.BuildEffectivePlayerLoadouts();
            if (loadouts == null) return false;
            for (var i = 0; i < loadouts.Length; i++)
            {
                if (string.Equals(loadouts[i].PlayerId.Value, playerId, System.StringComparison.OrdinalIgnoreCase))
                    return loadouts[i].HeroId == heroId;
            }

            return false;
        }

        private static bool TryGetPlayerActor(
            BattleContext context,
            string playerId,
            out int actorId,
            out ActorEntity actor)
        {
            actorId = 0;
            actor = null;
            if (context?.Session == null || !context.Session.TryGetWorld(out var world) || world?.Services == null ||
                !world.Services.TryResolve<MobaPlayerActorMapService>(out var playerActors) || playerActors == null ||
                !playerActors.TryGetActorId(new PlayerId(playerId), out actorId) || actorId <= 0 ||
                !world.Services.TryResolve<MobaActorLookupService>(out var actors) || actors == null ||
                !actors.TryGetActorEntity(actorId, out actor) || actor == null)
            {
                return false;
            }

            return true;
        }

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            var methods = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);
            for (var i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                if (method.Name != methodName || method.GetParameters().Length != arguments.Length) continue;
                method.Invoke(target, arguments);
                return;
            }

            Assert.Fail($"Missing private method: {methodName} with {arguments.Length} arguments.");
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Core.Mathematics;
using AbilityKit.Ability.Host;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Area;
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
using AbilityKit.Game.Battle.Vfx;
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

        [TestCase(0f, 1f)]
        [TestCase(1f, 0f)]
        [TestCase(0.6f, 0.8f)]
        public void E_ReleasesPointAimAndDeploysThreeDirectionalTrapAreas(float directionX, float directionZ)
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(
                Jinx, "jinx_e_hud_release_and_three_area_deployment_contract"))
            {
                Assert.IsTrue(harness.Config.TryGetSkill(ESkillId, out var skill));
                Assert.AreEqual(ESkillId, skill.CastFlowId);
                Assert.AreEqual(3, skill.SkillButtonTemplateId);
                Assert.IsTrue(skill.AllowCastWithoutTarget);
                Assert.IsTrue(harness.Config.TryGetSkillButtonTemplate(skill.SkillButtonTemplateId, out var button));
                Assert.IsTrue(button.EnableAim);
                Assert.AreEqual((int)SkillAimMode.Point, button.AimMode);
                Assert.AreEqual((int)SkillAimIndicatorShape.TargetCircle, button.IndicatorShape);
                Assert.AreEqual((int)SkillUsePointMode.TargetPoint, button.UsePointMode);
                Assert.IsTrue(harness.Config.TryGetAoe(42020301, out var trapArea));
                Assert.AreEqual(5000, trapArea.DurationMs);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness, 30020303, (int)TriggeringConstants.SpawnAreaId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness, 30020313,
                    (int)TriggeringConstants.AddBuffId.Value,
                    (int)TriggeringConstants.GiveDamageId.Value);

                harness.EnterGameAndWarmup(reason: "Jinx E point aim and trap deployment");
                var casterActorId = harness.AssertPlayerActorBound();
                var aimPosition = new Vec3(0f, 0f, 8f);
                var forward = new Vec3(directionX, 0f, directionZ);
                var right = new Vec3(directionZ, 0f, -directionX);
                var baseline = harness.CaptureTraceBaseline();
                var cast = harness.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(
                    casterActorId, slot: 3, aimPos: aimPosition, aimDir: forward, targetActorId: 0);
                Assert.IsTrue(cast.Success, "Jinx E should release from its point-target slot. " + DescribeCastFailure(cast));
                harness.TickUntilTraceNodeAfter(baseline, MobaExecutionKind.SkillCast, ESkillId, maxTicks: 10,
                    message: "Jinx E must enter its production cast flow.");
                var deploy = harness.TickUntilTraceNodeAfter(baseline, MobaExecutionKind.EffectExecution,
                    30020303, maxTicks: 40, message: "Jinx E must execute its area deployment trigger.");
                harness.AssertActionExecutedUnderEffect(deploy.RootId,
                    (int)TriggeringConstants.SpawnAreaId.Value, TriggeringConstants.Actions.SpawnArea);
                harness.Tick(1);

                var areaService = harness.World.Services.Resolve<MobaAreaRuntimeService>();
                var areas = new List<MobaAreaRuntimeInfo>();
                Assert.IsTrue(areaService.TryGetAreas(areas, ownerActorId: casterActorId, templateId: 42020301));
                Assert.AreEqual(3, areas.Count, "E must deploy all three trap areas in the same cast.");
                var expectedOffsets = new[] { -2f, 0f, 2f };
                for (var i = 0; i < expectedOffsets.Length; i++)
                {
                    var found = false;
                    for (var j = 0; j < areas.Count; j++)
                    {
                        var expected = aimPosition + right * expectedOffsets[i];
                        if ((areas[j].Center - expected).SqrMagnitude > 0.0025f) continue;
                        Assert.AreEqual(1, areas[j].GroupKey,
                            "The three traps share the configured group key so each target is claimed once.");
                        Assert.IsTrue(areas[j].ClaimTargetsWithinGroup);
                        found = true;
                        break;
                    }

                    Assert.IsTrue(found, "Missing E trap at configured lateral offset " + expectedOffsets[i]);
                }
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
        public IEnumerator Q_LocalHudSwitchesRealModelAndBothAttackProjectileVisuals() => LocalHudWeaponClosure();

        [UnityTest]
        public IEnumerator PlayMode_Q_LocalHudRendersBothWeaponsAndDamagesThroughTerrain()
        {
            DemoGameplayTestLauncher.OpenMobaLocalForPlay();
            yield return new EnterPlayMode();
            Time.captureDeltaTime = 1f / 60f;
            // Create captured gameplay locals after domain reload, not before it.
            yield return LocalHudWeaponClosure();
        }

        [UnityTearDown]
        public IEnumerator ExitLiveAcceptance()
        {
            Time.captureDeltaTime = 0f;
            if (Application.isPlaying) yield return new ExitPlayMode();
            DemoGameplayTestLauncher.Clear();
        }

        private IEnumerator LocalHudWeaponClosure()
        {
            var playMode = Application.isPlaying;
            JinxHudInputFixture hud = null;
            try
            {
                if (!playMode) DemoGameplayTestLauncher.OpenMobaLocalAndCompose();
                if (Application.isPlaying)
                    for (var frame = 0; frame < 180 && !GameEntry.IsInitialized; frame++) yield return null;
                var entry = UnityEngine.Object.FindObjectOfType<GameEntry>();
                if (Application.isPlaying && GameEntry.IsInitialized) entry = GameEntry.Instance;
                Assert.IsNotNull(entry);
                if (!playMode) InvokePrivate(entry, "Awake");
                var flow = entry.Get<GameFlowDomain>();
                if (!playMode) flow.StartWithPersistentSettingsSync();
                FlowTick(flow, 2);
                if (!playMode && flow.CurrentPhase != MobaRootState.Battle && flow.BattleEntryState != MobaBattleEntryState.Pending)
                    flow.EnterBattle(new TestBattleBootstrapper());
                yield return TickUntil(flow, () => flow.CurrentBattlePhase == MobaBattleState.InMatch,
                    18000, () => "Local battle failed: " + flow.BattleEntryRejection);
                Assert.IsTrue(entry.TryGet(out BattleContext ctx));
                var debug = new BattleLocalDebugController(() => ctx, () => null);
                Assert.IsTrue(System.Array.Exists(debug.CaptureSnapshot().HeroOptions, x => x.HeroId == HeroId),
                    "The real local hero selector must contain Jinx.");
                var plan = ctx.Plan;
                var player = BattleInputSessionIdentity.ResolvePlayerId((IBattleInputSessionIdentityPort)ctx);
                var command = BattleInputCommandFactory.CreateDebugReplaceHero(
                    SessionSimRuntimeTuning.ResolveInputSubmitFrame(ctx.LastFrame, in plan), player, HeroId);
                Assert.IsTrue(new BattleInputSubmitter(ctx.InputRuntime, player,
                    BattleInputSessionIdentity.ResolveWorldId(in plan)).Submit(in command));
                yield return TickUntil(flow, () => IsPlayerUsingHero(ctx, "p1", HeroId) &&
                    TryGetPlayerActor(ctx, "p1", out _, out var a) && a.hasSkillLoadout,
                    240, "Jinx replacement did not complete.");
                Assert.IsTrue(ctx.Session.TryGetWorld(out var world));
                Assert.IsTrue(TryGetPlayerActor(ctx, "p1", out var actorId, out var actor));
                Assert.IsTrue(TryGetPlayerActor(ctx, "p2", out _, out var enemy));
                var oldTransform = actor.transform.Value;
                var origin = new Vec3(7f, oldTransform.Position.Y, -4f);
                actor.ReplaceTransform(new Transform3(origin, oldTransform.Rotation, oldTransform.Scale));
                var enemyTransform = enemy.transform.Value;
                enemy.ReplaceTransform(new Transform3(origin + Vec3.Forward * 6f,
                    enemyTransform.Rotation, enemyTransform.Scale));
                var config = world.Services.Resolve<MobaConfigDatabase>();
                var parameters = world.Services.Resolve<MobaCharacterHfsmParameterService>();
                var loadout = world.Services.Resolve<MobaSkillLoadoutService>();
                var input = entry.Get<BattleInputFeature>();
                Animator model = null;
                yield return TickUntil(flow, () => (model = FindJinxModel()) != null,
                    120, "Local hero replacement did not instantiate the Jinx model.");
                // EditMode manually renders the camera instead of running Unity's
                // normal visibility loop. Keep transform evaluation enabled while
                // exercising the production snapshot/Animator bridge.
                model.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                yield return TickUntil(flow, () => IsAnimatorState(model, "Idle_Minigun"),
                    60, "Default WeaponMode did not reach the actual minigun idle animation.");
                var bones = model.GetComponentsInChildren<Transform>(true);
                Quaternion[] minigunPose = null;

                // Attack, switch, attack, switch back, attack: use pointer events
                // throughout, rather than bypassing the HUD via TryCastBySlot.
                for (var modeIndex = 0; modeIndex < 3; modeIndex++)
                {
                    var rocket = modeIndex == 1;
                    if (modeIndex > 0)
                    {
                        hud = JinxHudInputFixture.Create(ctx, config, 1, QSkillId);
                        using var switchPlayback = new WeaponPlaybackProbe(ctx, actor, model, parameters,
                            actorId, (playMode ? "play-" : "edit-") + "switch-" + modeIndex);
                        var before = input.SkillSubmitSuccessCount;
                        hud.PointerDown(hud.ButtonScreenPosition);
                        hud.PointerUp(hud.ButtonScreenPosition);
                        var sawSwitch = false;
                        for (var tick = 0; tick < switchPlayback.ObservationFrameBudget(QSkillId); tick++)
                        {
                            FlowTick(flow, 1);
                            switchPlayback.Sample();
                            sawSwitch |= IsAnimatorState(model,
                                rocket ? "Spell_Q_MiniToRocket" : "Spell_Q_RocketToMini");
                            if (sawSwitch && switchPlayback.HasObservedNormalCompletion) break;
                            yield return null;
                        }
                        Assert.AreEqual(rocket ? 1f : 0f, parameters.Resolve(actorId, WeaponModeParameterId),
                            "HUD Q did not commit its Buff-derived weapon parameter.");
                        Assert.AreEqual(before + 1, input.SkillSubmitSuccessCount);
                        Assert.IsTrue(sawSwitch, "Q did not play the matching switch animation on the actual model.");
                        switchPlayback.AssertSinglePlayback(QSkillId,
                            rocket ? "Spell_Q_MiniToRocket" : "Spell_Q_RocketToMini");
                        hud.Dispose();
                        hud = null;
                        yield return TickUntil(flow, () => IsAnimatorState(model,
                            rocket ? "Idle_Rocket" : "Idle_Minigun"),
                            90, "Q did not restore the matching weapon idle pose.");
                    }

                    Assert.IsTrue(loadout.TryResolveSlot(actorId, BasicAttackSlot, out var slot));
                    Assert.AreEqual(rocket ? FishbonesAttackSkillId : PowPowAttackSkillId, slot.EffectiveSkillId);
                    if (modeIndex == 0) minigunPose = System.Array.ConvertAll(bones, b => b.localRotation);
                    if (rocket)
                    {
                        var changedBones = 0;
                        for (var i = 0; i < bones.Length; i++)
                            if (Quaternion.Angle(minigunPose[i], bones[i].localRotation) > 5f) changedBones++;
                        TestContext.WriteLine("Jinx weapon switch changed bones=" + changedBones);
                        Assert.Greater(changedBones, 2, "Q must change the actual skeleton pose, not just Animator's state hash.");
                    }
                    CaptureJinxLocalFrame(model, "idle-" + modeIndex);
                    var hp = enemy.GetMobaAttrs().Hp;
                    hud = JinxHudInputFixture.Create(ctx, config, BasicAttackSlot, BaseAttackSkillId);
                    using var flight = new ProjectileActorPresentationProbe(ctx,
                        world.Services.Resolve<MobaActorRegistry>(), world.Services.Resolve<IProjectileService>(),
                        rocket ? FishbonesProjectileId : PowPowProjectileId, rocket ? 90032005 : 90032002,
                        (playMode ? "play-q-" : "edit-q-") + modeIndex,
                        ((IViewFeatureRuntime)entry.Get<BattleViewFeature>()).Binder);
                    using var attackPlayback = new WeaponPlaybackProbe(ctx, actor, model, parameters,
                        actorId, (playMode ? "play-" : "edit-") + "attack-" + modeIndex);
                    hud.PointerDown(hud.ButtonScreenPosition);
                    hud.PointerUp(hud.ButtonScreenPosition);
                    var sawAnimation = false;
                    var sawProjectile = false;
                    var capturedAttack = false;
                    var observedActions = new System.Text.StringBuilder();
                    var visual = rocket ? "Jinx_Arcanist_AA_Rocket_Projectile" : "Jinx_Arcanist_AA_Minigun_Projectile";
                    for (var tick = 0; tick < attackPlayback.ObservationFrameBudget(
                             rocket ? FishbonesAttackSkillId : PowPowAttackSkillId); tick++)
                    {
                        FlowTick(flow, 1);
                        flight.Sample();
                        attackPlayback.Sample();
                        if (tick < 20)
                        {
                            var action = actor.characterHfsm.Runtime.Action;
                            var hasViewAction = ((IBattleCharacterActionReadPort)ctx).TryGetCharacterAction(actorId, out var viewAction);
                            observedActions.Append($"[{tick}:{action.Path},skill={action.SkillId},cast={action.CastInstanceId}," +
                                $"model={model.GetCurrentAnimatorStateInfo(0).shortNameHash},version={ctx.GetActorEntityVersion(actorId)}," +
                                $"viewAction={hasViewAction}:{viewAction.Path}/{viewAction.SnapshotFrame}," +
                                $"mono={model.GetComponentInParent<MonoCharacterHfsmView>() != null}]");
                        }
                        sawAnimation |= IsAnimatorState(model, rocket ? "AA_Rocket" : "AA_Minigun");
                        foreach (var tag in UnityEngine.Object.FindObjectsOfType<BattleVfxPoolableTag>())
                            if (tag.gameObject.activeInHierarchy && tag.VfxId == (rocket ? 90032005 : 90032002))
                            {
                                Assert.IsNotNull(tag.GetComponentInChildren<ParticleSystem>(true),
                                    "The actual projectile visual must contain the extracted particle effect.");
                                sawProjectile |= CaptureLiveVfx(tag, "q-" + modeIndex) > 20;
                            }
                        if (!capturedAttack && sawAnimation && sawProjectile && flight.HasFollowedFlight && enemy.GetMobaAttrs().Hp < hp)
                        {
                            CaptureJinxLocalFrame(model, "attack-" + modeIndex);
                            capturedAttack = true;
                        }
                        if (capturedAttack && attackPlayback.HasObservedNormalCompletion) break;
                        yield return null;
                    }
                    Assert.IsTrue(sawAnimation, "HUD attack did not play the weapon-specific model animation. " +
                        $"mode={modeIndex},visual={sawProjectile},hp={hp}->{enemy.GetMobaAttrs().Hp},actions={observedActions}");
                    Assert.IsTrue(sawProjectile, "HUD attack did not instantiate the weapon-specific visible projectile: " + visual);
                    Assert.Less(enemy.GetMobaAttrs().Hp, hp, "The same attack must also apply real damage.");
                    flight.AssertFollowedFlight();
                    attackPlayback.AssertSinglePlayback(rocket ? FishbonesAttackSkillId : PowPowAttackSkillId,
                        rocket ? "AA_Rocket" : "AA_Minigun");
                    hud.Dispose();
                    hud = null;
                    for (var tick = 0; tick < 35; tick++) { FlowTick(flow, 1); yield return null; }
                }
            }
            finally
            {
                hud?.Dispose();
                if (GameEntry.IsInitialized && GameEntry.Instance != null)
                    UnityEngine.Object.DestroyImmediate(GameEntry.Instance.gameObject);
                if (!Application.isPlaying) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                DemoGameplayTestLauncher.Clear();
            }
        }

        // Acceptance-only observation: never seeks the Animator or changes battle state.
        private sealed class WeaponPlaybackProbe : System.IDisposable
        {
            [System.Serializable]
            private sealed class Row
            {
                public int frame, renderFrame, sessionFrame, tickRate, skillId, localFrame, startFrame, stateHash, fullPathHash;
                public int viewFrame, viewLocalFrame, viewSkillId;
                public long actionId, castId, viewActionId, factCastId;
                public string path, viewPath, clipName, castStage;
                public long observedCastId;
                public int castElapsedMs, castNextEventIndex;
                public float weaponMode, normalizedTime, length, speed, speedMultiplier, maxBoneStep;
                public float clipLength, clipWeight, animatorSpeed;
                public bool casting, transition, hasView, castEntityPresent, castRunning;
            }

            private readonly BattleContext _context;
            private readonly ActorEntity _actor;
            private readonly Animator _model;
            private readonly MobaCharacterHfsmParameterService _parameters;
            private readonly int _actorId;
            private readonly string _label;
            private readonly List<Row> _rows = new List<Row>();
            private readonly Entitas.IGroup<ActorEntity> _casts;
            private readonly MobaConfigDatabase _config;
            private long _observedCastId;
            private readonly Transform[] _bones;
            private readonly Quaternion[] _previousPose;

            public WeaponPlaybackProbe(BattleContext context, ActorEntity actor, Animator model,
                MobaCharacterHfsmParameterService parameters, int actorId, string label)
            {
                _context = context;
                _actor = actor;
                _model = model;
                _parameters = parameters;
                _actorId = actorId;
                _label = label;
                Assert.IsTrue(context.Session.TryGetWorld(out var world));
                var contexts = (global::Contexts)world.Services.Resolve<Entitas.IContexts>();
                _casts = contexts.actor.GetGroup(ActorMatcher.AllOf(
                    ActorComponentsLookup.SkillCastOwnerActorId, ActorComponentsLookup.SkillCastInstanceId));
                _config = world.Services.Resolve<MobaConfigDatabase>();
                _bones = model.GetComponentsInChildren<Transform>(true);
                _previousPose = System.Array.ConvertAll(_bones, b => b.localRotation);
                Sample();
            }

            public void Sample()
            {
                var runtime = _actor.characterHfsm.Runtime;
                var action = runtime.Action;
                if (action.CastInstanceId > 0) _observedCastId = action.CastInstanceId;
                // Observe the ECS projection, including its retained terminal row.
                // FillEndedSnapshots drains the coordinator queue and must not be used by a probe.
                ActorEntity castEntity = null;
                foreach (var candidate in _casts.GetEntities())
                    if (candidate.skillCastOwnerActorId.Value == _actorId &&
                        candidate.skillCastInstanceId.Value == _observedCastId) castEntity = candidate;
                var state = _model.GetCurrentAnimatorStateInfo(0);
                var clips = _model.GetCurrentAnimatorClipInfo(0);
                var hasView = ((IBattleCharacterActionReadPort)_context).TryGetCharacterAction(_actorId, out var view);
                var maxStep = 0f;
                for (var i = 0; i < _bones.Length; i++)
                {
                    maxStep = Mathf.Max(maxStep, Quaternion.Angle(_previousPose[i], _bones[i].localRotation));
                    _previousPose[i] = _bones[i].localRotation;
                }
                _rows.Add(new Row {
                    frame = runtime.Frame, renderFrame = Time.frameCount, sessionFrame = _context.LastFrame,
                    tickRate = _context.Plan.World.TickRate, path = action.Path,
                    actionId = action.InstanceId, castId = action.CastInstanceId, skillId = action.SkillId,
                    observedCastId = _observedCastId, castEntityPresent = castEntity != null,
                    castStage = castEntity != null && castEntity.hasSkillCastStage
                        ? castEntity.skillCastStage.Value.ToString() : null,
                    castRunning = castEntity != null && castEntity.isSkillCastRunningTag,
                    castElapsedMs = castEntity != null && castEntity.hasSkillCastTimelineRuntime
                        ? castEntity.skillCastTimelineRuntime.ElapsedMs : -1,
                    castNextEventIndex = castEntity != null && castEntity.hasSkillCastTimelineRuntime
                        ? castEntity.skillCastTimelineRuntime.NextEventIndex : -1,
                    startFrame = action.StartFrame, localFrame = action.LocalFrame,
                    casting = runtime.FactIsCasting, factCastId = runtime.FactCastInstanceId,
                    weaponMode = _parameters.Resolve(_actorId, WeaponModeParameterId),
                    stateHash = state.shortNameHash, fullPathHash = state.fullPathHash,
                    normalizedTime = state.normalizedTime, length = state.length,
                    speed = state.speed, speedMultiplier = state.speedMultiplier,
                    clipName = clips.Length == 1 ? clips[0].clip.name : "clip-count:" + clips.Length,
                    clipLength = clips.Length == 1 ? clips[0].clip.length : 0f,
                    clipWeight = clips.Length == 1 ? clips[0].weight : 0f, animatorSpeed = _model.speed,
                    transition = _model.IsInTransition(0), maxBoneStep = maxStep,
                    hasView = hasView, viewFrame = hasView ? view.SnapshotFrame : -1,
                    viewPath = hasView ? view.Path : null, viewActionId = hasView ? view.InstanceId : 0,
                    viewLocalFrame = hasView ? view.LocalFrame : -1, viewSkillId = hasView ? view.SkillId : 0
                });
            }

            public bool HasObservedNormalCompletion => _rows.Exists(row =>
                row.castStage == SkillCastStage.Completed.ToString() && !row.casting && !row.castRunning);

            public int ObservationFrameBudget(int skillId)
            {
                Assert.IsTrue(_config.TryGetSkill(skillId, out var skill));
                Assert.IsTrue(_config.TryGetSkillFlow(skill.CastFlowId, out var flow));
                var durationMs = 0;
                foreach (var phase in flow.Phases)
                    if (phase.Type == (int)SkillPhaseType.Timeline) durationMs += phase.Timeline.DurationMs;
                // This fixture yields render frames while the session runs at its
                // own tick rate. Bound the observation from authored time, then
                // stop only after observing the actual terminal Cast projection.
                return Mathf.CeilToInt(durationMs * 0.001f * Mathf.Max(120, _context.Plan.World.TickRate * 4)) + 120;
            }

            public void AssertSinglePlayback(int skillId, string animatorState)
            {
                var actionIds = new HashSet<long>();
                var castIds = new HashSet<long>();
                var samples = 0;
                var firstFrame = -1;
                var lastFrame = -1;
                var maxTime = 0f;
                Row previous = null;
                foreach (var row in _rows)
                {
                    if (row.skillId != skillId) continue;
                    actionIds.Add(row.actionId);
                    castIds.Add(row.castId);
                    if (row.stateHash == Animator.StringToHash(animatorState))
                    {
                        Assert.That(row.length * row.speed * row.speedMultiplier,
                            Is.EqualTo(row.clipLength).Within(.001f),
                            "The actual Animator playback range must match the formal clip: " + _label);
                        samples++;
                        if (firstFrame < 0) firstFrame = row.frame;
                        lastFrame = row.frame;
                        maxTime = Mathf.Max(maxTime, row.normalizedTime);
                    }
                    if (previous != null && previous.actionId == row.actionId)
                        Assert.GreaterOrEqual(row.localFrame, previous.localFrame,
                            "An uninterrupted action cursor must not rewind: " + _label);
                    previous = row;
                }
                Assert.Greater(samples, 1, "Expected continuous actual Animator samples: " + _label);
                Assert.GreaterOrEqual(maxTime, 0.95f,
                    "An uninterrupted HUD action must sample the end of its formal animation, not just enter its state: " + _label);
                Assert.AreEqual(1, castIds.Count, "One HUD press must own one cast: " + _label);
                Assert.AreEqual(1, actionIds.Count, "Weapon change must not restart this committed action: " + _label);
                Assert.AreNotEqual(skillId, _rows[_rows.Count - 1].skillId,
                    "Observation must include the action exit: " + _label);
                AssertTimelineCompletion(skillId, castIds);
                TestContext.WriteLine($"Playback {_label}: frames={firstFrame}..{lastFrame}, " +
                    $"samples={samples}, maxNormalized={maxTime:F6}, actions={actionIds.Count}");
            }

            private void AssertTimelineCompletion(int skillId, HashSet<long> castIds)
            {
                Assert.IsTrue(_config.TryGetSkill(skillId, out var skill));
                Assert.IsTrue(_config.TryGetSkillFlow(skill.CastFlowId, out var flow));
                var durationMs = 0;
                foreach (var phase in flow.Phases)
                    if (phase.Type == (int)SkillPhaseType.Timeline) durationMs += phase.Timeline.DurationMs;
                Assert.Greater(durationMs, 0, "This diagnostic expects the configured finite Timeline.");
                Row completed = null;
                foreach (var row in _rows)
                {
                    if (!castIds.Contains(row.observedCastId) || !row.castEntityPresent) continue;
                    Assert.That(row.castStage, Is.Not.EqualTo(SkillCastStage.Cancelled.ToString())
                        .And.Not.EqualTo(SkillCastStage.Failed.ToString()),
                        "Uninterrupted HUD action must complete normally: " + _label);
                    if (row.castStage != SkillCastStage.Completed.ToString()) continue;
                    completed = row;
                    break;
                }
                Assert.IsNotNull(completed, "Observe the terminal cast, not just the Animator exit: " + _label);
                Assert.IsFalse(completed.castRunning, "Completed cast must leave the active cast group.");
                Assert.That(completed.castElapsedMs, Is.GreaterThanOrEqualTo(durationMs)
                    .And.LessThan(durationMs + Mathf.CeilToInt(1000f / completed.tickRate)));
                Assert.IsFalse(completed.casting, "The same logic frame must clear the casting fact.");
                Assert.AreNotEqual(skillId, completed.skillId,
                    "Current-context casting must exit when its Timeline completes.");
                TestContext.WriteLine($"Cast completion {_label}: frame={completed.frame}, " +
                    $"cast={completed.observedCastId}, stage={completed.castStage}, " +
                    $"elapsedMs={completed.castElapsedMs}, configuredTimelineMs={durationMs}, " +
                    $"running={completed.castRunning}, path={completed.path}");
            }

            public void Dispose()
            {
                var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,
                    "../../local/Logs/jinx-playback-" + _label + ".jsonl"));
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                System.IO.File.WriteAllLines(path, _rows.ConvertAll(row => JsonUtility.ToJson(row)));
                TestContext.WriteLine("Continuous playback evidence: " + path);
            }
        }

        private static Animator FindJinxModel()
        {
            foreach (var animator in UnityEngine.Object.FindObjectsOfType<Animator>())
                if (animator.runtimeAnimatorController != null &&
                    animator.runtimeAnimatorController.name == "Jinx_Arcanist_Model") return animator;
            return null;
        }

        private static bool IsAnimatorState(Animator animator, string state) =>
            animator != null && animator.GetCurrentAnimatorStateInfo(0).shortNameHash == Animator.StringToHash(state);

        private static void CaptureJinxLocalFrame(Animator model, string label)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var camera = Camera.main;
            Assert.IsNotNull(camera, "The real local battle must expose its gameplay camera.");
            var target = RenderTexture.GetTemporary(1280, 720, 24);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var enabled = System.Array.ConvertAll(renderers, r => r.enabled);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                var pixels = texture.GetPixels32();
                var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,
                    "../../local/Logs/jinx-q-" + label + ".png"));
                System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
                foreach (var renderer in renderers) renderer.enabled = false;
                camera.Render();
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                var hidden = texture.GetPixels32();
                var changed = 0;
                for (var i = 0; i < pixels.Length; i++) if (!pixels[i].Equals(hidden[i])) changed++;
                TestContext.WriteLine($"Jinx local frame {label}: {path}; model pixels={changed}");
                Assert.Greater(changed, 32, "Jinx must actually render in the production camera.");
            }
            finally
            {
                for (var i = 0; i < renderers.Length; i++) renderers[i].enabled = enabled[i];
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [UnityTest]
        public IEnumerator QAndWAndR_LocalHudInputCommitsSwitchAndHitsReleasedRays() =>
            LocalHudActiveSkillClosure(true);

        [UnityTest]
        public IEnumerator R_LocalHudClickWorksAfterQWEWithoutRefillingMana() =>
            LocalHudActiveSkillClosure(false);

        [UnityTest]
        public IEnumerator PlayMode_QWER_LocalHudShowsProjectilesAndAppliesDamageAndControl()
        {
            DemoGameplayTestLauncher.OpenMobaLocalForPlay();
            yield return new EnterPlayMode();
            Time.captureDeltaTime = 1f / 60f;
            yield return LocalHudActiveSkillClosure(true);
        }

        private IEnumerator LocalHudActiveSkillClosure(bool aimedR)
        {
            var playMode = Application.isPlaying;
            JinxHudInputFixture hud = null;
            try
            {
                var scene = playMode ? SceneManager.GetActiveScene() :
                    DemoGameplayTestLauncher.OpenMobaLocalAndCompose();
                Assert.IsTrue(scene.IsValid(), "Unified gameplay scene should load for the local MOBA profile.");

                if (Application.isPlaying)
                    for (var frame = 0; frame < 180 && !GameEntry.IsInitialized; frame++) yield return null;
                var entry = UnityEngine.Object.FindObjectOfType<GameEntry>();
                if (Application.isPlaying && GameEntry.IsInitialized) entry = GameEntry.Instance;
                Assert.IsNotNull(entry, "Demo scene must contain a GameEntry.");
                if (!playMode) InvokePrivate(entry, "Awake");
                var flow = entry.Get<GameFlowDomain>();
                Assert.IsNotNull(flow, "GameEntry must expose GameFlowDomain after Awake.");

                if (!playMode) flow.StartWithPersistentSettingsSync();
                FlowTick(flow, 2);
                if (!playMode && flow.CurrentPhase != MobaRootState.Battle &&
                    flow.BattleEntryState != MobaBattleEntryState.Pending)
                {
                    flow.EnterBattle(new TestBattleBootstrapper());
                }

                yield return TickUntil(flow,
                    () => flow.CurrentBattlePhase == MobaBattleState.InMatch,
                    18000,
                    () => "The local MOBA battle did not reach InMatch. root=" + flow.CurrentPhase +
                          ", entry=" + flow.BattleEntryState + ", battle=" + flow.CurrentBattlePhase +
                          ", rejection=" + flow.BattleEntryRejection + ".");
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
                Assert.IsTrue(actors.TryGetActorEntity(ctx.LocalActorId, out var localActor));
                var localTransform = localActor.transform.Value;
                localActor.ReplaceTransform(new Transform3(new Vec3(7f, localTransform.Position.Y, -4f),
                    localTransform.Rotation, localTransform.Scale));
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
                yield return TickUntil(flow,
                    () => TryGetPlayerActor(ctx, "p1", out _, out var caster) &&
                          caster.characterHfsm.Runtime.Action.CastInstanceId == 0,
                    60, "Q must finish its switch action before the next active skill.");

                var enemyHpBefore = enemy.GetMobaAttrs().Hp;
                var speedBeforeW = enemy.GetMobaAttrs().MoveSpeed;
                hud = JinxHudInputFixture.Create(ctx, config, slot: 2, skillId: WSkillId);
                Assert.AreEqual(SkillAimIndicatorShape.DirectionLine, hud.Button.Config.IndicatorShape,
                    "The W button itself must receive the resolved DirectionLine configuration.");
                var wButtonCenter = hud.ButtonScreenPosition;
                var wSubmitsBefore = inputFeature.SkillSubmitSuccessCount;
                hud.PointerDown(wButtonCenter);
                hud.Drag(wButtonCenter + new Vector2(0f, 120f));
                Assert.IsTrue(new BattleHudAimPreviewPositionResolver().TryResolve(ctx, out var preview));
                var onRay = preview.CasterPosition + preview.AimDirection * 6f;
                enemy.ReplaceTransform(new Transform3(new Vec3(onRay.x, onRay.y, onRay.z),
                    enemyTransform.Rotation, enemyTransform.Scale));
                FlowTick(flow, 1);
                hud.PointerUp(wButtonCenter + new Vector2(0f, 120f));

                yield return TickUntil(flow,
                    () => inputFeature.SkillSubmitSuccessCount > wSubmitsBefore,
                    12,
                    "The directional W release did not reach BattleInputFeature.");
                Assert.AreEqual(wSubmitsBefore + 1, inputFeature.SkillSubmitSuccessCount,
                    "One W HUD release must submit exactly one skill command.");
                Assert.IsFalse(BattleHudInputSource.TryConsumeSkillAimSubmit(ctx, out _),
                    "BattleInputFeature should consume the W aim snapshot exactly once.");

                var rayDiagnostics = new System.Text.StringBuilder();
                var links = world.Services.Resolve<MobaProjectileLinkService>();
                var projectileService = world.Services.Resolve<IProjectileService>();
                var projectiles = new List<MobaProjectileLinkDiagnostics>();
                var wVisiblePixels = 0;
                for (var tick = 0; tick < 180 && enemy.GetMobaAttrs().Hp >= enemyHpBefore; tick++)
                {
                    FlowTick(flow, 1);
                    wVisiblePixels = Mathf.Max(wVisiblePixels, CaptureLiveVfx(90032027, "w-travel"));
                    projectiles.Clear();
                    links.CopyDiagnosticsTo(projectiles);
                    foreach (var link in projectiles)
                        if (projectileService.TryGetRuntimeState(link.ProjectileId, out var shot))
                            rayDiagnostics.Append($"[{tick}:pos={shot.Position},dir={shot.Direction},distance={shot.DistanceTravelled}]");
                    if (enemy.GetMobaAttrs().Hp < enemyHpBefore) break;
                    yield return null;
                }

                Assert.Less(enemy.GetMobaAttrs().Hp, enemyHpBefore,
                    "The production W projectile should damage the enemy on the released DirectionLine. " +
                    $"hpBefore={enemyHpBefore:F2}, hpAfter={enemy.GetMobaAttrs().Hp:F2}, " +
                    $"submits={inputFeature.SkillSubmitSuccessCount - wSubmitsBefore}, " +
                    $"enemyPosition={enemy.transform.Value.Position}, preview={preview.AimDirection}, trajectory={rayDiagnostics}");
                Assert.Less(enemy.GetMobaAttrs().MoveSpeed, speedBeforeW, "The W hit must slow its actual victim.");
                hud.Dispose();
                hud = null;
                yield return TickUntil(flow,
                    () => localActor.characterHfsm.Runtime.Action.CastInstanceId == 0,
                    90, "W must finish before releasing R.");
                hud = JinxHudInputFixture.Create(ctx, config, slot: 3, skillId: ESkillId);
                var eCenter = hud.ButtonScreenPosition;
                hud.PointerDown(eCenter);
                hud.Drag(eCenter + new Vector2(-60f, 100f));
                Assert.IsTrue(new BattleHudAimPreviewPositionResolver().TryResolve(ctx, out var ePreview));
                var ePoint = ePreview.CasterPosition + ePreview.AimDirection * ePreview.AimDistance;
                var eLanding = new Vec3(ePoint.x, ePoint.y, ePoint.z);
                enemy.ReplaceTransform(new Transform3(eLanding, enemyTransform.Rotation, enemyTransform.Scale));
                FlowTick(flow, 1);
                var hpBeforeE = enemy.GetMobaAttrs().Hp;
                hud.PointerUp(eCenter + new Vector2(-60f, 100f));
                yield return TickUntil(flow, () =>
                {
                    var count = 0;
                    foreach (var tag in UnityEngine.Object.FindObjectsOfType<BattleVfxPoolableTag>())
                        if (tag.VfxId == 90032007 && tag.gameObject.activeInHierarchy &&
                            tag.GetComponentInChildren<ParticleSystem>() != null) count++;
                    return count == 3;
                }, 90, "E must create three independent formal trap visuals through the real HUD.");
                Assert.AreEqual(hpBeforeE, enemy.GetMobaAttrs().Hp, 0.001f,
                    "E must not apply contact damage before the configured arm delay.");
                var eVisiblePixels = 0;
                yield return TickUntil(flow, () =>
                {
                    eVisiblePixels = Mathf.Max(eVisiblePixels, CaptureLiveVfx(90032007, "e-traps"));
                    return enemy.GetMobaAttrs().Hp < hpBeforeE;
                }, 90, "The armed E trap must damage the enemy at the released indicator landing.");
                Assert.Greater(eVisiblePixels, 20, "E's real area visuals must render in the gameplay camera.");
                Assert.IsTrue(HasLiveBuff(enemy, 32020301), "E must apply its root Buff to the same damaged victim.");
                hud.Dispose();
                hud = null;
                yield return TickUntil(flow,
                    () => localActor.characterHfsm.Runtime.Action.CastInstanceId == 0,
                    90, "E must finish before releasing R.");
                Assert.GreaterOrEqual(localActor.GetMobaAttrs().Mana, 100f,
                    "The official R cost must remain affordable after W and E from a full initial mana pool.");
                var hpBeforeR = enemy.GetMobaAttrs().Hp;
                hud = JinxHudInputFixture.Create(ctx, config, slot: 4, skillId: RSkillId);
                var rCenter = hud.ButtonScreenPosition;
                hud.PointerDown(rCenter);
                var rRelease = rCenter;
                var rOrigin = localActor.transform.Value.Position;
                var rDirection = localActor.transform.Value.Rotation.Rotate(Vec3.Forward);
                if (aimedR)
                {
                    rRelease += new Vector2(80f, 100f);
                    hud.Drag(rRelease);
                    Assert.IsTrue(new BattleHudAimPreviewPositionResolver().TryResolve(ctx, out var rPreview));
                    rDirection = new Vec3(rPreview.AimDirection.x, 0f, rPreview.AimDirection.z);
                }
                var onRRay = rOrigin + rDirection * 7f;
                enemy.ReplaceTransform(new Transform3(onRRay,
                    enemyTransform.Rotation, enemyTransform.Scale));
                FlowTick(flow, 1);
                hud.PointerUp(rRelease);
                var rVisiblePixels = 0;
                yield return TickUntil(flow, () =>
                {
                    rVisiblePixels = Mathf.Max(rVisiblePixels, CaptureLiveVfx(90032025, "r-travel"));
                    return enemy.GetMobaAttrs().Hp < hpBeforeR;
                }, 120,
                    () => $"R after Q/W must damage the enemy on its released ray; mana={localActor.GetMobaAttrs().Mana}, hp={enemy.GetMobaAttrs().Hp}");
                Assert.Greater(rVisiblePixels, 20, "R must render its formal projectile before this same cast impacts.");
                Assert.Greater(wVisiblePixels, 20, "W must render its formal flying resource in this same cast.");
                yield return TickUntil(flow, () => !HasLiveBuff(enemy, 30020211) && !HasLiveBuff(enemy, 32020301),
                    180, "W slow and E root must expire through the normal Buff lifecycle.");
                Assert.AreEqual(speedBeforeW, enemy.GetMobaAttrs().MoveSpeed, 0.001f,
                    "W's movement modifier must be removed when its slow expires.");

            }
            finally
            {
                hud?.Dispose();
                if (GameEntry.IsInitialized && GameEntry.Instance != null)
                    UnityEngine.Object.DestroyImmediate(GameEntry.Instance.gameObject);
                if (!Application.isPlaying) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
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
                var buttons = new SkillButtonView[System.Math.Max(4, slot)];
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

        private static IEnumerator TickUntil(
            GameFlowDomain flow,
            System.Func<bool> predicate,
            int maxTicks,
            System.Func<string> failureMessage)
        {
            for (var tick = 0; tick < maxTicks; tick++)
            {
                FlowTick(flow, 1);
                if (predicate()) yield break;
                yield return null;
            }

            Assert.Fail(failureMessage == null ? string.Empty : failureMessage());
        }

        private static bool HasLiveBuff(ActorEntity actor, int id)
        {
            if (!actor.hasBuffs || actor.buffs.Active == null) return false;
            foreach (var buff in actor.buffs.Active)
                if (buff != null && buff.BuffId == id) return true;
            return false;
        }

        private static int CaptureLiveVfx(int id, string label)
        {
            var pixels = 0;
            foreach (var tag in UnityEngine.Object.FindObjectsOfType<BattleVfxPoolableTag>())
                if (tag.VfxId == id && tag.gameObject.activeInHierarchy)
                    pixels = Mathf.Max(pixels, CaptureLiveVfx(tag, label));
            return pixels;
        }

        private static readonly System.Collections.Generic.HashSet<string> InspectedVfx = new();
        private static readonly Dictionary<string, int> BestVfxPixels = new();

        private static int CaptureLiveVfx(BattleVfxPoolableTag tag, string label)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Same-cast visual acceptance requires a graphics device.");
            var camera = Camera.main;
            Assert.IsNotNull(camera);
            var renderers = tag.GetComponentsInChildren<Renderer>();
            var states = System.Array.ConvertAll(renderers, r => r.enabled);
            var target = RenderTexture.GetTemporary(960, 540, 24);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var texture = new Texture2D(960, 540, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                texture.Apply();
                var visible = texture.GetPixels32();
                var png = texture.EncodeToPNG();
                foreach (var renderer in renderers) renderer.enabled = false;
                camera.Render();
                texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                texture.Apply();
                var hidden = texture.GetPixels32();
                for (var index = 0; index < renderers.Length; index++) renderers[index].enabled = states[index];
                var min = new Vector2(1f, 1f);
                var max = Vector2.zero;
                // Ignore stray world-space particles at prefab origin. Only pixels
                // near this actual projectile/area anchor count as its visual.
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = tag.transform.position + Vector3.up * 0.8f + new Vector3(
                        (corner & 1) == 0 ? -2f : 2f,
                        (corner & 2) == 0 ? -2f : 2f,
                        (corner & 4) == 0 ? -2f : 2f);
                    var projected = camera.WorldToViewportPoint(point);
                    min = Vector2.Min(min, new Vector2(projected.x, projected.y));
                    max = Vector2.Max(max, new Vector2(projected.x, projected.y));
                }
                var changed = 0;
                for (var i = 0; i < visible.Length; i++)
                {
                    // Repeated game-camera renders can differ by a few quantization
                    // values. Count a visible contribution, not that background noise.
                    var delta = System.Math.Abs(visible[i].r - hidden[i].r) +
                        System.Math.Abs(visible[i].g - hidden[i].g) +
                        System.Math.Abs(visible[i].b - hidden[i].b);
                    var x = (i % 960) / 960f;
                    var y = (i / 960) / 540f;
                    if (delta > 24 && x >= min.x && x <= max.x && y >= min.y && y <= max.y) changed++;
                }
                var firstInspection = InspectedVfx.Add(label + ":" + tag.GetInstanceID());
                if (firstInspection)
                {
                    TestContext.WriteLine($"VFX diagnostics {label}: id={tag.VfxId}, pixels={changed}, position={tag.transform.position}, scale={tag.transform.lossyScale}, camera={camera.name}/{camera.depthTextureMode}, renderers={renderers.Length}");
                    foreach (var system in tag.GetComponentsInChildren<ParticleSystem>(true))
                        TestContext.WriteLine($"  {system.name}: active={system.gameObject.activeInHierarchy}, playing={system.isPlaying}, particles={system.particleCount}, time={system.time}, scale={system.transform.lossyScale}, bounds={system.GetComponent<ParticleSystemRenderer>().bounds}");
                    foreach (var renderer in renderers)
                        TestContext.WriteLine($"  Renderer {renderer.name}: enabled={renderer.enabled}, shader={renderer.sharedMaterial?.shader.name}, bounds={renderer.bounds}");
                }
                var captureKey = (Application.isPlaying ? "play-" : "edit-") + label;
                if (!BestVfxPixels.TryGetValue(captureKey, out var previousBest) || changed > previousBest)
                {
                    var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,
                        "../../local/Logs/jinx-live-" + (Application.isPlaying ? "play-" : "edit-") + label + ".png"));
                    BestVfxPixels[captureKey] = changed;
                    System.IO.File.WriteAllBytes(path, png);
                    TestContext.WriteLine($"Live VFX {tag.VfxId} pixels={changed}; pos={tag.transform.position}; capture={path}");
                }
                return changed;
            }
            finally
            {
                for (var i = 0; i < renderers.Length; i++) renderers[i].enabled = states[i];
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static void FlowTick(GameFlowDomain flow, int ticks)
        {
            // In Play Mode GameEntry.Update owns the production frame loop.
            if (Application.isPlaying) return;
            const float fixedDeltaTime = 1f / 30f;
            for (var tick = 0; tick < ticks; tick++)
            {
                flow.Tick(fixedDeltaTime);
                foreach (var tag in UnityEngine.Object.FindObjectsOfType<BattleVfxPoolableTag>())
                    foreach (var system in tag.GetComponentsInChildren<ParticleSystem>())
                        system.Simulate(fixedDeltaTime, false, false, true);
            }
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

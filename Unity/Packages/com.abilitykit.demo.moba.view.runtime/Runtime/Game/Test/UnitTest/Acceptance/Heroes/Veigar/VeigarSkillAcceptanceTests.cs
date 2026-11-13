using System.Collections.Generic;
using System.Linq;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Trace;
using NUnit.Framework;

namespace AbilityKit.Game.Test.UnitTest
{
    /// <summary>
    /// Production-path contract for Veigar's formal Battle Scenario slice.
    /// The assertions intentionally stay on published config and shared
    /// runtime services so a future presentation change cannot hide a logic
    /// or targeting regression.
    /// </summary>
    public sealed class VeigarSkillAcceptanceTests : MobaAcceptanceTestBase
    {
        private const int HeroId = 3004;
        private const int BasicSkillId = 30040101;
        private const int QSkillId = 30040201;
        private const int WSkillId = 30040301;
        private const int ESkillId = 30040401;
        private const int RSkillId = 30040501;
        private const int PassiveId = 30040000;
        private const int PassiveHitTriggerId = 30040001;
        private const int PassiveKillTriggerId = 30040002;
        private const int GrowthBuffId = 30040000;
        private const int QLauncherId = 33040101;
        private const int QProjectileId = 32040101;
        private const int RLauncherId = 33040501;
        private const int RProjectileId = 32040501;
        private const int WAreaId = 42040301;
        private const int EAreaId = 42040401;
        private const int EBuffId = 32040401;
        private const int WDelayedTriggerId = 30040311;
        private const int EEnterTriggerId = 30040413;

        private static readonly HeroSkillContract Veigar = new HeroSkillContract(
            "Veigar", HeroId, HeroId, new[] { BasicSkillId, QSkillId, WSkillId, ESkillId, RSkillId });

        [Test]
        public void PublishedSkillGraph_ContainsAllSlotsAndSharedExecutionContracts()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Veigar,
                       "veigar_published_skill_graph_contract_world"))
            {
                AssertSkill(harness, 1, QSkillId, QSkillId);
                AssertSkill(harness, 2, WSkillId, WSkillId);
                AssertSkill(harness, 3, ESkillId, ESkillId);
                AssertSkill(harness, 4, RSkillId, RSkillId);

                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness, QSkillId, (int)TriggeringConstants.ShootProjectileId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness, QSkillId + 10, (int)TriggeringConstants.GiveDamageId.Value);
                harness.AssertProjectileConfigExists(QLauncherId, QProjectileId);

                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness, WSkillId, (int)TriggeringConstants.SpawnAreaId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness, WDelayedTriggerId, (int)TriggeringConstants.GiveDamageId.Value);

                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness, ESkillId, (int)TriggeringConstants.SpawnAreaPatternId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness, EEnterTriggerId, (int)TriggeringConstants.AddBuffId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness, ESkillId, (int)TriggeringConstants.SpawnAreaId.Value);

                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness, RSkillId, (int)TriggeringConstants.ShootProjectileId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness, RSkillId + 10, (int)TriggeringConstants.GiveDamageId.Value);
                harness.AssertProjectileConfigExists(RLauncherId, RProjectileId);
            }
        }

        [Test]
        public void GroundTargetSkills_UsePublishedTargetPointButtonContract()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Veigar,
                       "veigar_ground_target_button_contract_world"))
            {
                foreach (var skillId in new[] { WSkillId, ESkillId })
                {
                    Assert.IsTrue(harness.Config.TryGetSkill(skillId, out var skill));
                    Assert.AreEqual(3, skill.SkillButtonTemplateId,
                        "Veigar ground skill must use the shared target-point button template.");
                    Assert.IsTrue(skill.AllowCastWithoutTarget);
                    Assert.IsTrue(harness.Config.TryGetSkillButtonTemplate(
                        skill.SkillButtonTemplateId, out var button));
                    Assert.IsTrue(button.EnableAim);
                    Assert.AreEqual(1, button.AimMode,
                        "Veigar ground skill must expose point aiming.");
                    Assert.AreEqual(2, button.IndicatorShape);
                    Assert.AreEqual(2, button.UsePointMode,
                        "Veigar ground skill must consume the released target point.");
                }
            }
        }

        [Test]
        public void PublishedAreaAndPassiveConfigs_MatchApprovedVeigarRules()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Veigar,
                       "veigar_area_passive_contract_world"))
            {
                Assert.IsTrue(harness.Config.TryGetAoe(WAreaId, out var wArea));
                Assert.AreEqual(450, wArea.DelayMs);
                Assert.AreEqual(700, wArea.DurationMs);
                Assert.AreEqual(4.5f, wArea.Radius, 0.001f);
                CollectionAssert.Contains(wArea.OnDelayTriggerIds, WDelayedTriggerId);

                Assert.IsTrue(harness.Config.TryGetAoe(EAreaId, out var eArea));
                Assert.AreEqual(450, eArea.DelayMs);
                Assert.AreEqual(0.875f, eArea.Radius, 0.001f);
                Assert.AreEqual(0f, eArea.InnerRadius, 0.001f);
                Assert.AreEqual(3500, eArea.DurationMs);
                CollectionAssert.Contains(eArea.OnEnterTriggerIds, EEnterTriggerId);
                CollectionAssert.Contains(eArea.OnCrossTriggerIds, EEnterTriggerId);
                Assert.IsEmpty(eArea.OnExitTriggerIds,
                    "Leaving an edge must not cancel the stun Buff's own duration.");
                Assert.AreEqual(0, eArea.VfxId);
                Assert.IsTrue(harness.Config.TryGetAoe(42040402, out var cage));
                Assert.AreEqual(90034017, cage.VfxId);
                Assert.AreEqual(eArea.DelayMs, cage.DelayMs);
                Assert.AreEqual(eArea.DurationMs, cage.DurationMs);
                Assert.IsEmpty(cage.OnEnterTriggerIds);
                Assert.IsEmpty(cage.OnCrossTriggerIds);
                Assert.IsFalse(eArea.ConsumeOnFirstEnter,
                    "E must use the shared per-target group claim lifecycle.");

                Assert.IsTrue(harness.Config.TryGetBuff(EBuffId, out var eBuff));
                Assert.AreEqual(1000, eBuff.DurationMs);
                Assert.AreEqual(BuffStackingPolicy.None, eBuff.StackingPolicy);

                Assert.IsTrue(harness.Config.TryGetPassiveSkill(PassiveId, out var passive));
                CollectionAssert.Contains(passive.TriggerIds, PassiveHitTriggerId);
                CollectionAssert.Contains(passive.TriggerIds, PassiveKillTriggerId);
                Assert.IsTrue(harness.Config.TryGetBuff(GrowthBuffId, out var growth));
                Assert.AreEqual(BuffStackingPolicy.AddStack, growth.StackingPolicy);
                Assert.Greater(growth.MaxStacks, 1);
                Assert.IsNotEmpty(growth.Modifiers,
                    "Veigar growth must project its stacks through the authored Modifier.");
            }
        }

        [Test]
        public void QCast_UsesPublishedProjectileAndAppliesHitDamage()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Veigar,
                       "veigar_q_projectile_damage_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "Veigar Q projectile closure");
                var actorId = harness.AssertPlayerActorBound();
                var targetId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "veigar_q_target", 9404, x: 0f, z: 4f);
                var hpBefore = harness.GetActorHp(targetId);
                var baseline = harness.CaptureTraceBaseline();
                var cast = harness.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(
                    actorId, 1, aimPos: Vec3.Zero, aimDir: Vec3.Forward, targetActorId: targetId);
                Assert.IsTrue(cast.Success, "Veigar Q must cast through its authored slot.");
                var launch = harness.TickUntilTraceNodeAfter(baseline,
                    MobaExecutionKind.EffectExecution, QSkillId, 40,
                    "Veigar Q launch trigger did not execute.");
                harness.AssertProjectileLaunchedUnderEffect(launch.RootId, QLauncherId, QProjectileId);
                harness.TickUntilTraceNodeAfter(baseline, MobaExecutionKind.DamageApply, QSkillId,
                    80, "Veigar Q projectile did not apply its authored hit damage.");
                Assert.Less(harness.GetActorHp(targetId), hpBefore);
            }
        }

        [Test]
        public void PassiveGrowth_AddsStackForOwnedSkillDamage()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Veigar,
                       "veigar_passive_skill_damage_growth_runtime_world"))
            {
                harness.EnterGameAndWarmup(reason: "Veigar passive skill damage growth");
                var actorId = harness.AssertPlayerActorBound();
                var targetId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "veigar_passive_damage_target", 9404, x: 0f, z: 4f);
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(actorId, 1, Vec3.Zero, Vec3.Forward, targetId);
                Assert.IsTrue(cast.Success, "Veigar Q should release for the passive damage test: " + cast.FailReason);
                harness.TickUntilTraceNode(MobaExecutionKind.DamageApply, QSkillId,
                    maxTicks: 80, message: "Veigar Q must publish its owned skill damage result.");
                Assert.IsTrue(harness.TryGetActorBuffStackCount(actorId, GrowthBuffId, out var stacks),
                    "Veigar passive growth Buff must be applied after owned skill damage.");
                Assert.AreEqual(1, stacks,
                    "One positive skill damage result must add exactly one Veigar growth stack.");
            }
        }

        [Test]
        public void PassiveGrowth_AddsStackForOwnedFinalKill()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Veigar,
                       "veigar_passive_final_kill_growth_runtime_world"))
            {
                harness.EnterGameAndWarmup(reason: "Veigar passive final kill growth");
                var actorId = harness.AssertPlayerActorBound();
                var targetId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "veigar_passive_kill_target", 9404, x: 0f, z: 4f);
                harness.SetScenarioActorAttribute(targetId, "hp", 1f);
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(actorId, 1, Vec3.Zero, Vec3.Forward, targetId);
                Assert.IsTrue(cast.Success, "Veigar Q should release for the passive kill test: " + cast.FailReason);
                harness.TickUntilTraceNode(MobaExecutionKind.DamageApply, QSkillId,
                    maxTicks: 80, message: "Veigar Q must publish the killing damage result.");
                Assert.LessOrEqual(harness.GetActorHp(targetId), 0f,
                    "The configured low-health target must be killed by Veigar Q.");
                Assert.IsTrue(harness.TryGetActorBuffStackCount(actorId, GrowthBuffId, out var stacks),
                    "Veigar passive growth Buff must be applied after the owned final kill.");
                Assert.AreEqual(2, stacks,
                    "The kill's positive skill damage and final unit.die awards must each add one stack.");
            }
        }

        [Test]
        public void WAndECasts_CreateConfiguredAreasWithLifecycleGeometry()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Veigar,
                       "veigar_area_lifecycle_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "Veigar W/E area lifecycle");
                var actorId = harness.AssertPlayerActorBound();
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var areas = harness.World.Services.Resolve<MobaAreaRuntimeService>();
                var active = new List<MobaAreaRuntimeInfo>();
                var aimPosition = new Vec3(0f, 0f, 6f);

                var wBaseline = harness.CaptureTraceBaseline();
                Assert.IsTrue(skills.TryCastBySlot(actorId, 2, aimPosition, Vec3.Forward, 0).Success);
                harness.TickUntilTraceNodeAfter(wBaseline, MobaExecutionKind.EffectExecution,
                    WSkillId, 40, "Veigar W area trigger did not execute.");
                // W's area becomes active after its authored 450 ms delay and
                // remains queryable for the configured 700 ms lifetime.
                harness.Tick(15);
                Assert.IsTrue(areas.TryGetAreas(active, actorId, WAreaId));
                Assert.AreEqual(4.5f, active[0].Radius, 0.001f);
                harness.Tick(25);
                Assert.IsFalse(areas.TryGetAreas(active, actorId, WAreaId),
                    "The delayed W impact area must expire through the configured lifecycle.");

                harness.TickUntilSkillStops(actorId, 2, maxTicks: 80,
                    message: "Veigar W pipeline must finish before the next ground-target cast.");
                var eBaseline = harness.CaptureTraceBaseline();
                var eCast = skills.TryCastBySlot(actorId, 3, aimPosition, Vec3.Forward, 0);
                var actor = harness.AssertActorEntity(actorId);
                var currentMana = new AbilityKit.Demo.Moba.Attributes.MobaAttrs(actor).Mana;
                var skillRuntimeSummary = actor.hasSkillLoadout && actor.skillLoadout.ActiveSkills != null
                    ? string.Join(",", System.Array.ConvertAll(actor.skillLoadout.ActiveSkills,
                        runtime => runtime == null ? "null" : $"{runtime.SkillId}:{runtime.CooldownEndTimeMs}/{runtime.CooldownDurationMs}"))
                    : "missing";
                Assert.IsTrue(eCast.Success,
                    "Veigar E point cast failed: " + eCast.FailReason +
                    $" failure={eCast.Failure.Source}/{eCast.Failure.Stage}/{eCast.Failure.Code}/{eCast.Failure.Message} " +
                    $" mana={currentMana} skills={skillRuntimeSummary} " +
                    harness.DescribeSkillRuntimeState(actorId, 3));
                harness.TickUntilTraceNodeAfter(eBaseline, MobaExecutionKind.EffectExecution,
                    ESkillId, 40, "Veigar E batch trigger did not execute.");
                Assert.IsTrue(areas.TryGetAreas(active, actorId, EAreaId));
                Assert.AreEqual(6, active.Count);
                var expectedEdgeLength = 2f * 0.75f * (float)System.Math.Tan(System.Math.PI / 6d);
                for (var i = 0; i < 6; i++)
                {
                    var angle = i * (float)(System.Math.PI / 3d);
                    var normal = new Vec3((float)System.Math.Cos(angle), 0f, (float)System.Math.Sin(angle));
                    var tangent = new Vec3(-normal.Z, 0f, normal.X);
                        var expectedCenter = aimPosition + normal * 0.75f;
                    var found = false;
                    for (var j = 0; j < active.Count; j++)
                    {
                        if ((active[j].Center - expectedCenter).SqrMagnitude > 0.0025f) continue;
                        Assert.AreEqual(2, active[j].ShapeKind);
                        Assert.AreEqual(0.25f, active[j].Width, 0.001f);
                        Assert.AreEqual(expectedEdgeLength, active[j].Length, 0.001f);
                        Assert.Greater(Vec3.Dot(active[j].Forward, tangent), 0.999f);
                        Assert.AreEqual(40401, active[j].GroupKey);
                        Assert.IsTrue(active[j].ClaimTargetsWithinGroup);
                        Assert.AreEqual(active[0].RootContextId, active[j].RootContextId);
                        found = true;
                        break;
                    }
                    Assert.IsTrue(found, "Missing Veigar E boundary edge at index " + i);
                }
            }
        }

        [Test]
        public void EBoundary_ArmsAfterDelayAndClaimsEnemyOnceAcrossEdges()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Veigar,
                       "veigar_e_delayed_hex_boundary_runtime_world"))
            {
                harness.EnterGameAndWarmup(reason: "Veigar E delayed hex boundary");
                var actorId = harness.AssertPlayerActorBound();
                var targetId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "veigar_e_boundary_target", 9404, x: 0.75f, z: 6f);
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var areas = harness.World.Services.Resolve<MobaAreaRuntimeService>();
                var active = new List<MobaAreaRuntimeInfo>();
                var cast = skills.TryCastBySlot(actorId, 3, new Vec3(0f, 0f, 6f), Vec3.Forward, 0);
                Assert.IsTrue(cast.Success, "Veigar E should cast: " + cast.FailReason);
                harness.TickUntilTraceNode(MobaExecutionKind.EffectExecution, ESkillId,
                    maxTicks: 40, message: "Veigar E batch trigger did not execute.");
                Assert.IsTrue(areas.TryGetAreas(active, actorId, EAreaId));
                Assert.AreEqual(6, active.Count);
                Assert.IsFalse(harness.TryGetActorBuffStackCount(targetId, EBuffId, out _),
                    "A target inside the delayed boundary must not be stunned before arming.");
                harness.Tick(30);
                Assert.IsTrue(harness.TryGetActorBuffStackCount(targetId, EBuffId, out var stacks));
                Assert.AreEqual(1, stacks,
                    "Group claim must prevent the same target from receiving six boundary Enter effects.");
            }
        }

        [Test]
        public void ECage_HasOneCentralCueAndOutlivesItsCastPipeline()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Veigar,
                       "veigar_e_cage_cast_and_cue_lifetime_world"))
            {
                harness.EnterGameAndWarmup(reason: "Veigar E independent area and cast lifetimes");
                var actorId = harness.AssertPlayerActorBound();
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var areas = harness.World.Services.Resolve<MobaAreaRuntimeService>();
                var cues = harness.World.Services.Resolve<MobaPresentationCueSnapshotService>();
                var point = new Vec3(0f, 0f, 6f);
                Assert.IsTrue(skills.TryCastBySlot(actorId, 3, point, Vec3.Forward, 0).Success);
                harness.TickUntilTraceNode(MobaExecutionKind.EffectExecution, ESkillId, 40,
                    "Veigar E must submit its areas.");
                var cage = cues.ActiveCues.Values.Where(c => c.Entry.SourceActorId == actorId && c.Entry.VfxId == 90034017).ToArray();
                Assert.AreEqual(1, cage.Length, "Six collision edges must produce one full cage cue.");
                Assert.AreEqual(42040402, cage[0].Entry.TemplateId);
                CollectionAssert.AreEqual(new[] { point.X, point.Y, point.Z }, cage[0].Entry.Positions);
                var key = cage[0].Key;
                harness.TickUntilSkillStops(actorId, 3, maxTicks: 80,
                    message: "E cast must finish using its action duration, independently of the cage.");
                var active = new List<MobaAreaRuntimeInfo>();
                Assert.IsTrue(areas.TryGetAreas(active, actorId, EAreaId));
                Assert.AreEqual(6, active.Count);
                Assert.IsTrue(cues.ActiveCues.ContainsKey(key));
                var castQ = skills.TryCastBySlot(actorId, 1, point, Vec3.Forward, 0);
                Assert.IsTrue(castQ.Success, "Q must be available while the E cage persists: " + castQ.FailReason);
                harness.Tick(120);
                Assert.IsFalse(areas.TryGetAreas(active, actorId, EAreaId));
                Assert.IsFalse(areas.TryGetAreas(active, actorId, 42040402));
                Assert.IsFalse(cues.ActiveCues.ContainsKey(key), "Authoritative Area expiry must clear its cue.");
            }
        }

        [Test]
        public void RCast_UsesFrozenTargetProjectileAndPublishedHitFormula()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(Veigar,
                       "veigar_r_target_formula_contract_world"))
            {
                Assert.IsTrue(harness.Config.TryGetProjectile(RProjectileId, out var projectile));
                Assert.AreEqual(RSkillId + 10, projectile.OnHitEffectId);
                Assert.IsTrue(harness.Config.TryGetSkillLevelTable(RSkillId, out var levels));
                Assert.IsTrue(levels.TryGetParameter(1, "damage", out var baseDamage));
                Assert.Greater(baseDamage, 0f);

                using (var source = System.IO.File.OpenText(
                           System.IO.Path.Combine(UnityEngine.Application.dataPath,
                               "../Packages/com.abilitykit.demo.moba.view.runtime/Configs/MobaTriggerAuthoring/Sources/moba-skills.trigger.json")))
                {
                    var json = source.ReadToEnd();
                    var index = json.IndexOf("\"id\": 30040511", System.StringComparison.Ordinal);
                    Assert.GreaterOrEqual(index, 0);
                    var end = json.IndexOf("\"id\":", index + 10, System.StringComparison.Ordinal);
                    var block = end > index ? json.Substring(index, end - index) : json.Substring(index);
                    StringAssert.Contains("target_missing_hp_ratio_coefficient", block);
                    StringAssert.Contains("target_missing_hp_bonus_max", block);
                    StringAssert.Contains("damage_max", block);
                }
            }
        }

        private static void AssertSkill(MobaSkillConfigTestHarness harness, int slot,
            int skillId, int effectId)
        {
            harness.AssertSlotSkill(slot, skillId);
            harness.AssertSkillUsesCastFlow(skillId, skillId);
            harness.AssertCastFlowContainsTimelineEffect(skillId, effectId);
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Demo.Moba.Services.Triggering.Cue;
using AbilityKit.Demo.Moba.Triggering;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.Share.Config;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Combat.Projectile;
using System;
using AbilityKit.Trace;
using AbilityKit.Continuous;
using AbilityKit.GameplayTags;
using NUnit.Framework;
using AbilityKit.ActionSchema;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Battle.Presentation.Features.Loading;
using AbilityKit.Game.Flow.Battle.Presentation.Timeline;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Game.Battle.View;
using AbilityKit.Game.Battle.View.Lib.Skill;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AbilityKit.Game.Test.UnitTest
{
    /// <summary>
    /// C3 runtime contract for Jhin Q.  The test intentionally exercises the
    /// authored skill flow and projectile hit trigger instead of calling the
    /// projectile world directly, so target locking and hit ordering remain
    /// production-path assertions.
    /// </summary>
    public sealed class JhinSkillAcceptanceTests : MobaAcceptanceTestBase
    {
        private const int HeroId = 3001;
        private const int SkillId = 30010101;
        private const int QInitialTargetQueryId = 52010102;
        private const int QBounceTargetQueryId = 52010101;
        private const int HitEffectId = 30010111;
        private const int ProjectileId = 32010101;
        private const int LauncherId = 33010101;
        private const int MaxHits = 4;
        private const int WSkillId = 30010201;
        private const int WHitEffectId = 30010212;
        private const int WProjectileId = 32010201;
        private const int WLauncherId = 33010201;
        private const int WMarkBuffId = 30010221;
        private const int WRootBuffId = 30010222;
        private const int PassiveCounterKeyId = 30010001;
        private const int GenericSuperArmorBuffId = 10010000;
        private const int ESkillId = 30010301;
        private const int ELaunchTriggerId = 30010301;
        private const int ELandingTriggerId = 30010312;
        private const int EProjectileId = 32010301;
        private const int ELauncherId = 33010301;
        private const int EEnterTriggerId = 30010311;
        private const int EAreaId = 42010301;
        private const int ESlowBuffId = 32010301;
        private const int EHitPresentationTemplateId = 90013018;
        private const int RSkillId = 30010401;
        private const int RRegularLaunchTriggerId = 30010401;
        private const int RRegularHitEffectId = 30010402;
        private const int RFinalLaunchTriggerId = 30010411;
        private const int RFinalHitEffectId = 30010412;
        private const int RProjectileId = 32010401;
        private const int RFinalProjectileId = 32010402;
        private const int RLauncherId = 33010401;
        private const int RSlowBuffId = 32010403;
        private const int RModeBuffId = 32010404;
        private const int RFinalCandidateBuffId = 32010405;
        private const int RShootingPhaseBuffId = 32010406;
        private const int RFinalPhaseBuffId = 32010407;
        private const int RTargetQueryId = 52010401;
        private const int BasicAttack1SkillId = 30010601;
        private const int BasicAttack2SkillId = 30010602;
        private const int BasicAttack3SkillId = 30010603;
        private const int BasicAttack4SkillId = 30010604;
        private const int BasicAttackLauncherId = 33010601;
        private const int BasicAttackProjectileId = 32010601;
        private const int BasicAttackFinalProjectileId = 32010602;
        private const int BasicAttackLaunchCounterKeyId = 30010004;
        private const int ReloadPresentationTemplateId = 90013015;

        [Test]
        public void CurtainCallPresentation_ResolvesPublishedTemplateFromExportedTimeline()
        {
            var presentation = Resources.Load<TextAsset>("moba/action_timeline/skill_30010401.moba.presentation");
            var logic = Resources.Load<TextAsset>("moba/action_timeline/skill_30010401.moba.logic");
            Assert.IsNotNull(presentation, "Jhin R presentation timeline should be exported.");
            Assert.IsNotNull(logic, "Jhin R logic timeline should be exported separately.");
            Assert.IsFalse(logic.text.Contains("presentationTemplateId"), "Presentation template IDs must not leak into the logic export.");

            var sink = new PresentationRequestRecordingSink();
            var runtime = new MobaSkillPresentationTimelineRuntime(
                4294967297L, presentation.text, sink, new MobaPresentationTemplateResolver());
            runtime.StartAt(0f);

            Assert.AreEqual("90013004", sink.Clip.args["presentationTemplateId"]);
            Assert.IsFalse(sink.Clip.args.ContainsKey("resourceKey"), "The exported clip should reference the template, not a prefab path.");
            Assert.AreEqual(90013004, sink.Request.AssetId);
            Assert.AreEqual("ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_R_Cast", sink.Request.ResourceKey);
            Assert.AreEqual(1800, sink.Request.DurationMs);
            Assert.AreEqual(1f, sink.Request.Scale, 0.0001f);
            Assert.AreEqual(PresentationAttachmentKind.Actor, sink.Request.Attachment.Kind);
            Assert.AreEqual(0, sink.Request.Attachment.ActorId);
            Assert.AreEqual(0f, sink.Request.Attachment.Offset.X, 0.0001f);
            Assert.AreEqual(0.15f, sink.Request.Attachment.Offset.Y, 0.0001f);
            Assert.AreEqual(0f, sink.Request.Attachment.Offset.Z, 0.0001f);
            Assert.AreEqual(0f, sink.Request.Position.X, 0.0001f);
            Assert.AreEqual(0.15f, sink.Request.Position.Y, 0.0001f);
            Assert.AreEqual(0f, sink.Request.Position.Z, 0.0001f);
        }

        [Test]
        public void BasicSpellsPresentation_ResolvePublishedTemplatesFromExportedTimelines()
        {
            var skillIds = new[] { 30010101, 30010201, 30010301 };
            var templateIds = new[] { 90013001, 90013002, 90013003 };
            var durations = new[] { 700, 700, 1600 };
            var resources = new[]
            {
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_Q",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_W",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_E"
            };

            for (var i = 0; i < skillIds.Length; i++)
            {
                var presentation = Resources.Load<TextAsset>("moba/action_timeline/skill_" + skillIds[i] + ".moba.presentation");
                var logic = Resources.Load<TextAsset>("moba/action_timeline/skill_" + skillIds[i] + ".moba.logic");
                Assert.IsNotNull(presentation, "Jhin skill presentation timeline should be exported: " + skillIds[i]);
                Assert.IsNotNull(logic, "Jhin skill logic timeline should be exported separately: " + skillIds[i]);
                Assert.IsFalse(logic.text.Contains("presentationTemplateId"),
                    "Presentation template IDs must not leak into the logic export: " + skillIds[i]);

                var sink = new PresentationRequestRecordingSink();
                var runtime = new MobaSkillPresentationTimelineRuntime(
                    4294967297L + i, presentation.text, sink, new MobaPresentationTemplateResolver());
                runtime.StartAt(0f);

                Assert.IsNotNull(sink.Clip, "The exported timeline should dispatch a presentation clip: " + skillIds[i]);
                Assert.AreEqual(templateIds[i].ToString(), sink.Clip.args["presentationTemplateId"]);
                Assert.AreEqual(templateIds[i], sink.Request.AssetId);
                Assert.AreEqual(resources[i], sink.Request.ResourceKey);
                Assert.AreEqual(durations[i], sink.Request.DurationMs);
                Assert.AreEqual(PresentationAttachmentKind.Actor, sink.Request.Attachment.Kind);
                Assert.AreEqual(0.15f, sink.Request.Attachment.Offset.Y, 0.0001f);
            }
        }

        [Test]
        public void Skills_UseAimIndicatorsThatMatchTheirCastGeometry()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { SkillId, WSkillId, ESkillId, RSkillId });
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_skill_indicator_contract_world"))
            {
                AssertIndicator(harness, SkillId, 24, 3, 0f, 0f);
                AssertIndicator(harness, WSkillId, 25, 1, 0.8f, 0f);
                AssertIndicator(harness, ESkillId, 26, 2, 2.5f, 0f);
                AssertIndicator(harness, RSkillId, 27, 4, 30f, 10f);
            }
        }

        [Test]
        public void SkillTags_UsePipelineForWAndRModeBuffForPersistentMovementLock()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId,
                new[] { SkillId, WSkillId, ESkillId, RSkillId, 30010501, 30010502 });
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_skill_tag_matrix_contract_world"))
            {
                AssertPipelineTag(harness, SkillId, 0);
                AssertPipelineTag(harness, WSkillId, 3015);
                AssertPipelineTag(harness, ESkillId, 0);
                AssertPipelineTag(harness, RSkillId, 3015);
                AssertPipelineTag(harness, 30010501, 0);
                AssertPipelineTag(harness, 30010502, 0);

                Assert.IsTrue(harness.Config.TryGetBuff(RModeBuffId, out var rModeBuff),
                    "Jhin R mode Buff config should be published.");
                Assert.AreEqual(3015, rModeBuff.ContinuousTagTemplateId,
                    "The persistent R mode Buff must own the movement gate between separate shot pipelines.");

                var tagTemplates = new MobaContinuousTagTemplateRegistry(harness.Config);
                Assert.IsTrue(tagTemplates.TryGet(3015, out var movementGate),
                    "The shared autonomous-movement gate template should be published.");
                Assert.AreEqual((ulong)MobaTagResultFlags.CannotMove,
                    movementGate.ExecutionBlockedResultMask,
                    "The movement gate should block movement while leaving rotation and other execution results untouched.");
                Assert.IsTrue(movementGate.ApplicationTags.IsEmpty,
                    "The movement gate should not masquerade as a gameplay state or facing tag.");
            }
        }

        [Test]
        public void W_PipelineMovementGateEndsWithTheSingleCastFlow()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { WSkillId });
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_w_pipeline_movement_gate_world"))
            {
                harness.EnterGameAndWarmup(reason: "jhin w pipeline movement gate");
                var actorId = harness.AssertPlayerActorBound();
                var combatRules = harness.World.Services.Resolve<MobaCombatRulesService>();
                Assert.IsTrue(combatRules.CanMove(actorId), "Jhin should be movable before W starts.");

                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(actorId, 2, default, Vec3.Forward, 0);
                Assert.IsTrue(cast.Success, "Jhin W should start its directional cast. failReason=" + cast.FailReason);
                harness.Tick(1);

                Assert.IsTrue(harness.TryGetRunningSkillSnapshot(actorId, 2, out _),
                    harness.DescribeSkillRuntimeState(actorId, 2));
                Assert.IsFalse(combatRules.CanMove(actorId),
                    "W's 3015 Pipeline tag should block autonomous movement while its single cast flow runs.");

                harness.TickUntilSkillStops(actorId, 2, maxTicks: 120,
                    message: "Jhin W should release its pipeline movement gate when the one-shot cast flow completes.");
                Assert.IsTrue(combatRules.CanMove(actorId),
                    "W's Pipeline tag must be removed as part of cast completion.");
            }
        }

        private static void AssertPipelineTag(MobaSkillConfigTestHarness harness, int skillId, int expectedTemplateId)
        {
            Assert.IsTrue(harness.Config.TryGetSkillFlow(skillId, out var flow),
                "Jhin skill flow should be published: " + skillId);
            Assert.AreEqual(expectedTemplateId, flow.PipelineContinuousTagTemplateId,
                "Pipeline tag ownership should match the Jhin tag matrix for skill " + skillId + ".");
        }

        private static void AssertIndicator(
            MobaSkillConfigTestHarness harness,
            int skillId,
            int expectedTemplateId,
            int expectedShape,
            float expectedWidth,
            float expectedSectorAngle)
        {
            Assert.IsTrue(harness.Config.TryGetSkill(skillId, out var skill), "Jhin skill config should exist: " + skillId);
            Assert.AreEqual(expectedTemplateId, skill.SkillButtonTemplateId, "Skill button template should be mapped to the intended cast geometry.");
            Assert.IsTrue(harness.Config.TryGetSkillButtonTemplate(expectedTemplateId, out var template), "Aim template should be published: " + expectedTemplateId);
            Assert.IsTrue(template.EnableAim, "Jhin aimed skills should expose an input indicator.");
            Assert.AreEqual(expectedShape, template.IndicatorShape, "Indicator shape should match the skill's targeting geometry.");
            Assert.AreEqual(expectedWidth, template.IndicatorWorldWidth, 0.001f, "Indicator width/radius should match the skill geometry.");
            Assert.AreEqual(expectedSectorAngle, template.SectorAngleDegrees, 0.001f, "Sector angle should match the released R query.");
            if (skillId == SkillId)
            {
                Assert.AreEqual(12f, template.SelfRadius, 0.001f,
                    "Jhin Q indicator radius must match the caster-centered acquisition query.");
                Assert.AreEqual((int)SkillUsePointMode.None, template.UsePointMode,
                    "Jhin Q must release against a locked target rather than a ground point.");
            }
        }

        [Test]
        public void CurtainCallShotPresentation_ResolvesRegularAndFinalProjectileTemplates()
        {
            var skillIds = new[] { 30010501, 30010502 };
            var templateIds = new[] { 90013005, 90013006 };
            var resources = new[]
            {
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_R_Projectile",
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_R_Last_Projectile"
            };

            for (var i = 0; i < skillIds.Length; i++)
            {
                var presentation = Resources.Load<TextAsset>("moba/action_timeline/skill_" + skillIds[i] + ".moba.presentation");
                var logic = Resources.Load<TextAsset>("moba/action_timeline/skill_" + skillIds[i] + ".moba.logic");
                Assert.IsNotNull(presentation, "Jhin Curtain Call shot presentation timeline should be exported: " + skillIds[i]);
                Assert.IsNotNull(logic, "Jhin Curtain Call shot logic timeline should be exported separately: " + skillIds[i]);
                Assert.IsFalse(logic.text.Contains("presentationTemplateId"),
                    "Presentation template IDs must not leak into the shot logic export: " + skillIds[i]);

                var sink = new PresentationRequestRecordingSink();
                var runtime = new MobaSkillPresentationTimelineRuntime(
                    4294967301L + i, presentation.text, sink, new MobaPresentationTemplateResolver());
                runtime.StartAt(0f);

                Assert.IsNotNull(sink.Clip, "The shot timeline should dispatch a presentation clip: " + skillIds[i]);
                Assert.AreEqual(templateIds[i].ToString(), sink.Clip.args["presentationTemplateId"]);
                Assert.AreEqual(templateIds[i], sink.Request.AssetId);
                Assert.AreEqual(resources[i], sink.Request.ResourceKey);
                Assert.AreEqual(PresentationAttachmentKind.Actor, sink.Request.Attachment.Kind);
                Assert.AreEqual(0.15f, sink.Request.Attachment.Offset.Y, 0.0001f);
            }
        }

        [Test]
        public void BasicAttackPresentation_ResolvesPerSlotTemplatesAndKeepsLogicChannelPure()
        {
            var skillIds = new[] { 30010601, 30010602, 30010603, 30010604 };
            var templateIds = new[] { 90013009, 90013009, 90013009, 90013011 };

            for (var i = 0; i < skillIds.Length; i++)
            {
                var presentation = Resources.Load<TextAsset>("moba/action_timeline/skill_" + skillIds[i] + ".moba.presentation");
                var logic = Resources.Load<TextAsset>("moba/action_timeline/skill_" + skillIds[i] + ".moba.logic");
                Assert.IsNotNull(presentation, "Jhin basic attack presentation timeline should be exported: " + skillIds[i]);
                Assert.IsNotNull(logic, "Jhin basic attack logic timeline should be exported separately: " + skillIds[i]);
                Assert.IsFalse(logic.text.Contains("presentationTemplateId"),
                    "Presentation template IDs must not leak into the basic attack logic export: " + skillIds[i]);

                var sink = new PresentationRequestRecordingSink();
                var runtime = new MobaSkillPresentationTimelineRuntime(
                    4294967310L + i, presentation.text, sink, new MobaPresentationTemplateResolver());
                runtime.StartAt(0f);

                Assert.IsNotNull(sink.Clip, "The basic attack timeline should dispatch a presentation clip: " + skillIds[i]);
                Assert.AreEqual(templateIds[i].ToString(), sink.Clip.args["presentationTemplateId"]);
                Assert.AreEqual(templateIds[i], sink.Request.AssetId);
                Assert.AreEqual(PresentationAttachmentKind.Actor, sink.Request.Attachment.Kind);
            }
        }

        [Test]
        public void ReloadState_UsesLogicalEntryTriggerAndPublishedPresentationTemplate()
        {
            var hfsm = Resources.Load<TextAsset>("moba/character_hfsm_3001");
            Assert.IsNotNull(hfsm, "Published character HFSM should be available to the runtime.");
            StringAssert.Contains("attack.slot.5", hfsm.text);
            StringAssert.Contains("trigger.enter.30010006", hfsm.text);
            StringAssert.Contains("trigger.enter.30010014", hfsm.text);

            var plans = Resources.Load<TextAsset>("ability/ability_trigger_plans");
            Assert.IsNotNull(plans, "Published trigger plans should be available to the runtime.");
            StringAssert.Contains("30010014", plans.text);
            StringAssert.Contains("template_id", plans.text);
            StringAssert.Contains("90013015", plans.text);
            StringAssert.Contains("target_mode", plans.text);
            StringAssert.Contains("duration_ms", plans.text);

            var resolver = new MobaPresentationTemplateResolver();
            Assert.IsTrue(resolver.TryCreateRequest(
                90013015, "jhin.reload", null, 2500, 0f, out var request),
                "Reload entry trigger should resolve its published presentation template and VFX.");
            Assert.AreEqual(90013015, request.AssetId);
            Assert.AreEqual(2500, request.DurationMs);
            Assert.AreEqual(PresentationAttachmentKind.Actor, request.Attachment.Kind);
            Assert.AreEqual(0.15f, request.Attachment.Offset.Y, 0.0001f);
            Assert.AreEqual(
                "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_Passive_Reload",
                request.ResourceKey);
        }

        [Test]
        public void PersistentPresentationRequest_StartsAndStopsByRequestKey()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { SkillId });
            using (var harness = HeroSkillHeadlessContract.CreateHarness(
                       hero,
                       "jhin_persistent_presentation_request_runtime_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "jhin persistent presentation request runtime contract");
                var actorId = harness.AssertPlayerActorBound();
                var cues = harness.World.Services.Resolve<MobaPresentationCueSnapshotService>();
                var payload = new AbilityKit.Demo.Moba.Triggering.PresentationEventArgs
                {
                    EventId = MobaPresentationTriggering.Events.Play,
                    TemplateId = ReloadPresentationTemplateId,
                    RequestKey = "jhin.reload.persistent.test",
                    DurationMsOverride = 2500,
                    SourceActorId = actorId,
                    Targets = new[] { actorId },
                    ExecutionKind = MobaExecutionKind.PresentationPlay,
                };

                MobaPresentationRequestReporter.ReportPlay(cues, harness.Config, payload);

                Assert.IsTrue(cues.TryGetActiveCue(payload.RequestKey, out var active),
                    "A keyed presentation request should enter the active presentation cue store.");
                Assert.AreEqual((int)MobaPresentationCueStage.Started, active.Entry.Stage);
                Assert.AreEqual((int)PresentationCueLifetime.PersistentState, active.Entry.Lifetime);
                Assert.AreEqual(ReloadPresentationTemplateId, active.Entry.TemplateId);
                Assert.AreEqual(ReloadPresentationTemplateId, active.Entry.VfxId);
                Assert.AreEqual(2500, active.Entry.DurationMsOverride);
                CollectionAssert.Contains(active.Entry.Targets, actorId);

                payload.EventId = MobaPresentationTriggering.Events.Stop;
                payload.ExecutionKind = MobaExecutionKind.PresentationStop;
                MobaPresentationRequestReporter.ReportStop(cues, harness.Config, payload);

                Assert.IsFalse(cues.TryGetActiveCue(payload.RequestKey, out _),
                    "A keyed presentation stop should remove the matching active cue.");
            }
        }

        private sealed class PresentationRequestRecordingSink : IMobaSkillPresentationTimelineSink, IMobaSkillPresentationRequestSink
        {
            public PresentationRequest Request;
            public ClipDto Clip;

            public void OnPresentationRequest(in PresentationRequest request, GroupDto group, ClipDto clip, float offsetSeconds)
            {
                Request = request;
                Clip = clip;
            }

            public void OnClipStart(long skillInstanceId, GroupDto group, ClipDto clip, float offsetSeconds) => Clip = clip;
            public void OnTimelineStop(long skillInstanceId) { }
        }

        [Test]
        public void Passive_CountsOnlyPositiveBasicAttackDamageAndEmpowersEveryFourthShot()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { SkillId });
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_passive_fourth_shot_runtime_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "jhin passive fourth shot runtime contract");
                var actorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, "passive_target", 9491, x: 0f, z: 4f);
                harness.Tick(1);

                var variables = harness.World.Services.Resolve<AbilityKit.Demo.Moba.Gameplay.MobaGameplayVariableService>();
                var counterKey = unchecked(PassiveCounterKeyId * 100000 + actorId);
                var firstThreeDamage = new float[3];
                for (var i = 0; i < firstThreeDamage.Length; i++)
                {
                    var result = HeroSkillHeadlessContract.ExecuteBasicAttackDamage(harness, actorId, targetActorId, baseDamage: 10f);
                    Assert.Greater(result.Value, 0f, "A successful positive basic attack should resolve damage.");
                    firstThreeDamage[i] = result.Value;
                    Assert.AreEqual(i + 1, variables.Get(counterKey), "Only successful basic attacks should advance Jhin's per-attacker counter.");
                }

                var skillDamage = HeroSkillHeadlessContract.ExecuteDamage(
                    harness, actorId, targetActorId, baseDamage: 10f, DamageReasonKind.Skill, reasonParam: SkillId);
                Assert.AreEqual(firstThreeDamage[0], skillDamage.Value, 0.001f,
                    "Skill damage must not receive Jhin's basic-attack fourth-shot or missing-health modifiers.");
                Assert.AreEqual(3d, variables.Get(counterKey), "Skill damage must not advance the basic attack counter.");

                var zeroDamage = HeroSkillHeadlessContract.ExecuteBasicAttackDamage(harness, actorId, targetActorId, baseDamage: 0f);
                Assert.AreEqual(0f, zeroDamage.Value, "The zero-damage attack should not deal damage.");
                Assert.AreEqual(3d, variables.Get(counterKey), "A zero-damage attack must not advance the counter.");

                var fourthDamage = HeroSkillHeadlessContract.ExecuteBasicAttackDamage(harness, actorId, targetActorId, baseDamage: 10f);
                Assert.Greater(fourthDamage.Value, firstThreeDamage[0] * 1.9f,
                    "The fourth positive basic attack should receive the authored 100% damage increase.");
                Assert.AreEqual(0d, variables.Get(counterKey), "The empowered fourth hit should consume and reset the counter.");

                var nextDamage = HeroSkillHeadlessContract.ExecuteBasicAttackDamage(harness, actorId, targetActorId, baseDamage: 10f);
                Assert.AreEqual(firstThreeDamage[0], nextDamage.Value, 0.001f,
                    "The shot after the empowered fourth hit should return to normal damage.");
                Assert.AreEqual(1d, variables.Get(counterKey), "The next cycle should begin at one after a successful basic attack.");

                var hpBonusTarget = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "passive_fourth_slot_target", 9492, x: 2f, z: 4f);
                harness.SetScenarioActorAttribute(hpBonusTarget, "max_hp", 1000f);
                harness.SetScenarioActorAttribute(hpBonusTarget, "hp", 500f);
                var regularSlotDamage = HeroSkillHeadlessContract.ExecuteDamage(
                    harness, actorId, hpBonusTarget, baseDamage: 10f, DamageReasonKind.BasicAttack,
                    reasonParam: BasicAttack1SkillId);
                var fourthSlotDamage = HeroSkillHeadlessContract.ExecuteDamage(
                    harness, actorId, hpBonusTarget, baseDamage: 10f, DamageReasonKind.BasicAttack,
                    reasonParam: BasicAttack4SkillId);
                Assert.Greater(fourthSlotDamage.Value, regularSlotDamage.Value,
                    "The missing-health bonus should apply to attack slot 4 and not the other basic-attack skills.");
            }
        }

        [Test]
        public void BasicAttack_EmptyLaunchesAdvanceMagazineAndEnterReloadSlot()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { SkillId });
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_basic_attack_empty_launch_runtime_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "jhin basic attack empty launch runtime contract");
                var actorId = harness.AssertPlayerActorBound();
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var variables = harness.World.Services.Resolve<MobaGameplayVariableService>();
                var counterKey = unchecked(BasicAttackLaunchCounterKeyId * 100000 + actorId);
                Assert.IsTrue(harness.Config.TryGetPassiveSkill(30010000, out var jhinPassive),
                    "Jhin's passive skill config should be present in the runtime config database.");
                CollectionAssert.Contains(jhinPassive.TriggerIds, 30010004,
                    "Jhin's passive must subscribe to the committed basic-attack launch event.");
                var actors = harness.World.Services.Resolve<MobaActorLookupService>();
                Assert.IsTrue(actors.TryGetActorEntity(actorId, out var actor) && actor != null && actor.hasSkillLoadout,
                    "Jhin actor should have its passive skill loadout initialized.");
                Assert.IsTrue(Array.Exists(actor.skillLoadout.PassiveSkills,
                        passive => passive != null && passive.PassiveSkillId == 30010000),
                    "Jhin actor loadout should contain passive 30010000.");
                Assert.IsTrue(actor.hasOngoingTriggerPlans,
                    "Jhin passive should publish an ongoing owner-bound trigger plan after warmup.");
                var launchTriggerBound = false;
                foreach (var plan in actor.ongoingTriggerPlans.Active)
                {
                    if (plan == null || plan.TriggerIds == null) continue;
                    for (var i = 0; i < plan.TriggerIds.Length; i++)
                        if (plan.TriggerIds[i] == 30010004) launchTriggerBound = true;
                }
                Assert.IsTrue(launchTriggerBound,
                    "Jhin's actor-owned trigger plan should include trigger 30010004 before the first attack.");
                var subscriptions = harness.World.Services.Resolve<MobaTriggerPlanSubscriptionService>();
                var launchOwnerKeys = new List<long>();
                subscriptions.CopyOwnerKeysForTrigger(BasicAttackLaunchCounterKeyId, launchOwnerKeys);
                Assert.AreEqual(1, launchOwnerKeys.Count,
                    "Jhin should have exactly one owner-bound basic-attack launch listener.");
                var expectedSkills = new[]
                {
                    BasicAttack1SkillId,
                    BasicAttack2SkillId,
                    BasicAttack3SkillId,
                    BasicAttack4SkillId,
                };

                harness.AssertSlotSkill(5, BasicAttack1SkillId);
                var baseline = harness.CaptureTraceBaseline();
                for (var i = 0; i < expectedSkills.Length; i++)
                {
                    harness.AssertSlotSkill(5, expectedSkills[i]);
                    var cast = skills.TryCastBySlot(actorId, 5, aimPos: default, aimDir: Vec3.Forward, targetActorId: 0);
                    Assert.IsTrue(cast.Success, $"Jhin empty basic attack {i + 1} should cast. failReason={cast.FailReason}");
                    var effect = harness.TickUntilTraceNodeAfter(
                        baseline,
                        MobaExecutionKind.EffectExecution,
                        expectedSkills[i],
                        maxTicks: 30,
                        message: $"Jhin empty basic attack {i + 1} should execute its launch trigger.");
                    // The launch trigger commits the next-slot parameter, while
                    // the current attack state remains active until its short
                    // authored pipeline completes. Wait for that boundary
                    // before asserting which skill the next input resolves to.
                    harness.TickUntilSkillStops(actorId, 5, maxTicks: 30,
                        message: $"Jhin basic attack {i + 1} should finish its independent short flow before the next slot is selected.");
                    // The runtime advances the character HFSM on the next
                    // world tick after the cast runner retires.
                    harness.Tick(1);
                    var expectedProjectileId = i == expectedSkills.Length - 1
                        ? BasicAttackFinalProjectileId
                        : BasicAttackProjectileId;
                    harness.AssertProjectileLaunchedUnderEffect(effect.RootId, BasicAttackLauncherId, expectedProjectileId);
                    Assert.IsTrue(harness.Config.TryGetProjectile(expectedProjectileId, out var projectile),
                        $"Jhin basic attack projectile config should exist: {expectedProjectileId}");
                    Assert.AreEqual(i == expectedSkills.Length - 1 ? 90013012 : 90013010, projectile.VfxId,
                        $"Jhin basic attack projectile VFX should match slot {i + 1}.");
                    Assert.AreEqual(i == expectedSkills.Length - 1 ? 90013014 : 90013013, projectile.OnHitVfxId,
                        $"Jhin basic attack hit VFX should match slot {i + 1}.");
                    Assert.AreEqual(30010611, projectile.OnHitEffectId,
                        $"Jhin basic attack hit effect should match slot {i + 1}.");
                    baseline = harness.CaptureTraceBaseline();
                    var expectedCounter = i == expectedSkills.Length - 1 ? 0d : i + 1d;
                    for (var tick = 0; tick < 10 && variables.Get(counterKey) != expectedCounter; tick++) harness.Tick(1);
                    Assert.AreEqual(expectedCounter, variables.Get(counterKey),
                        $"Jhin empty basic attack {i + 1} should advance the launch counter independently of projectile hits.");
                    Assert.IsTrue(subscriptions.TryGetLastEvaluation(launchOwnerKeys[0], BasicAttackLaunchCounterKeyId, out var evaluation),
                        $"Jhin launch trigger should be evaluated for empty basic attack {i + 1}.");
                    Assert.IsTrue(evaluation.GatePassed && evaluation.SourceResolved && evaluation.PlanPassed,
                        $"Jhin launch trigger should pass its owner gate and plan for empty basic attack {i + 1}. " +
                        $"gate={evaluation.GatePassed} source={evaluation.SourceResolved} plan={evaluation.PlanPassed}");
                }

                var parameters = harness.World.Services.Resolve<MobaCharacterHfsmParameterService>();
                for (var tick = 0; tick < 30 && parameters.Resolve(actorId, MobaCharacterHfsmTestFixture.AttackSlot) != 5f; tick++)
                    harness.Tick(1);
                Assert.AreEqual(5f, parameters.Resolve(actorId, MobaCharacterHfsmTestFixture.AttackSlot),
                    "The fourth launched attack should drive the HFSM into the reload slot.");
                Assert.IsTrue(actor != null && actor.hasCharacterHfsm,
                    "Jhin actor should have a runtime character HFSM.");
                var reloadPath = actor.characterHfsm.Runtime.Action.Path;
                StringAssert.EndsWith("action/attack.slot.5", reloadPath,
                    "The fourth launched attack should enter the authored reload HFSM state.");

                var reloadCue = CapturePresentationCue(harness, ReloadPresentationTemplateId);
                Assert.AreEqual((int)MobaPresentationCueStage.Executed, reloadCue.Stage,
                    "Reload entry should publish a transient presentation cue at logical execution time.");
                Assert.AreEqual(ReloadPresentationTemplateId, reloadCue.TemplateId);
                Assert.AreEqual(ReloadPresentationTemplateId, reloadCue.VfxId,
                    "Reload presentation should carry the configured template asset into the snapshot.");
                Assert.AreEqual(2500, reloadCue.DurationMsOverride);
                Assert.AreEqual((int)PresentationCueDomain.Effect, reloadCue.Domain);
                Assert.AreEqual(actorId, reloadCue.SourceActorId);
                CollectionAssert.Contains(reloadCue.Targets, actorId,
                    "Self-targeted reload presentation should be attached to Jhin's actor.");
                Assert.AreEqual(0.15f, reloadCue.OffsetY, 0.0001f);

                // Reload ownership is parameter-driven. Removing the source contribution
                // must leave the authored state without relying on animation completion.
                parameters.ClearSource(actorId, BasicAttackLaunchCounterKeyId);
                harness.Tick(1);
                Assert.AreEqual(0f, parameters.Resolve(actorId, MobaCharacterHfsmTestFixture.AttackSlot),
                    "Clearing the reload parameter contribution should restore the default attack slot.");
                StringAssert.DoesNotEndWith("action/attack.slot.5", actor.characterHfsm.Runtime.Action.Path,
                    "Reload exit must be driven by the HFSM parameter condition, not the model animation callback.");

                Assert.AreEqual(0d, variables.Get(counterKey),
                    "The fourth launched attack should consume the shot counter before the reload state takes over.");
            }
        }

        [Test]
        public void Q_UsesExplicitInitialTargetThenBouncesByHitPositionAndStopsAtFourUniqueTargets()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { SkillId });
            var skill = new HeroSkillSlotContract(1, SkillId, SkillId, SkillId);
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_q_bounce_runtime_contract_world"))
            {
                harness.AssertSlotSkill(skill.Slot, SkillId);
                harness.AssertSkillUsesCastFlow(SkillId, SkillId);
                harness.AssertCastFlowContainsTimelineEffect(SkillId, SkillId);
                harness.AssertProjectileConfigExists(LauncherId, ProjectileId);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    HitEffectId,
                    (int)TriggeringConstants.GiveDamageId.Value);

                harness.EnterGameAndWarmup(reason: "jhin q bounce runtime contract");
                var casterActorId = harness.AssertPlayerActorBound();

                // Keep the bounce route clear of the arena's center blocker.
                // B is closer to the caster than A, but is off the initial ray.
                var initialTarget = HeroSkillHeadlessContract.SpawnEnemyHero(harness, "initial", 9101, x: 0f, z: 4f);
                var closerOffRay = HeroSkillHeadlessContract.SpawnEnemyHero(harness, "closer", 9102, x: -2f, z: 3f);
                var third = HeroSkillHeadlessContract.SpawnEnemyHero(harness, "third", 9103, x: -2f, z: 7f);
                var fourth = HeroSkillHeadlessContract.SpawnEnemyHero(harness, "fourth", 9104, x: -2f, z: 10f);
                var fifth = HeroSkillHeadlessContract.SpawnEnemyHero(harness, "fifth", 9105, x: -4f, z: 11f);
                harness.Tick(1);

                var initialHp = new Dictionary<int, float>
                {
                    [initialTarget] = harness.GetActorHp(initialTarget),
                    [closerOffRay] = harness.GetActorHp(closerOffRay),
                    [third] = harness.GetActorHp(third),
                    [fourth] = harness.GetActorHp(fourth),
                    [fifth] = harness.GetActorHp(fifth),
                };

                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(
                    casterActorId,
                    skill.Slot,
                    aimPos: default,
                    aimDir: Vec3.Forward,
                    targetActorId: initialTarget);
                Assert.IsTrue(cast.Success, "Jhin Q should accept an explicit enemy target. failReason=" + cast.FailReason);

                var effectTrace = harness.TickUntilTraceNode(
                    MobaExecutionKind.EffectExecution,
                    SkillId,
                    maxTicks: harness.CalculateWaitTicksForSkillEffect(SkillId, SkillId, safetyFrames: 5) + 30,
                    message: "Jhin Q should execute its authored effect before launching the projectile.");
                harness.AssertProjectileLaunchedUnderEffect(effectTrace.RootId, LauncherId, ProjectileId);
                harness.Tick(150);

                var records = MobaAcceptanceTraceExporter.CaptureTraceRecords(harness, "jhin_q_bounce_runtime_contract_world");
                var damage = new List<MobaAcceptanceTraceRecord>();
                var hits = new List<MobaAcceptanceTraceRecord>();
                for (var i = 0; i < records.Length; i++)
                {
                    var record = records[i];
                    if (record.kind == "ProjectileHit" && record.configId == ProjectileId)
                        hits.Add(record);
                    if (record.kind == "DamageApply" && record.configId == SkillId)
                        damage.Add(record);
                }

                var traceSummary = string.Empty;
                for (var i = 0; i < records.Length; i++)
                {
                    if (i > 0) traceSummary += "; ";
                    traceSummary += records[i].kind + ":" + records[i].configId + ":target=" + records[i].targetActorId;
                }
                var lifecycleSummary = CaptureProjectileLifecycleSummary(harness);
                var collisionSummary = CaptureCollisionSummary(harness);
                Assert.AreEqual(MaxHits, hits.Count, "Jhin Q must report four projectile hits before the hit trigger applies damage. trace=" + traceSummary + " lifecycle=" + lifecycleSummary + " collisions=" + collisionSummary);
                Assert.AreEqual(MaxHits, damage.Count, "Jhin Q must apply exactly four hit effects including the initial target. trace=" + traceSummary);
                Assert.AreEqual(initialTarget, damage[0].targetActorId, "The explicit initial target must be hit first even when another enemy is closer to the caster.");

                var hitIds = new HashSet<long>();
                for (var i = 0; i < damage.Count; i++)
                    Assert.IsTrue(hitIds.Add(damage[i].targetActorId), "Jhin Q bounce must not damage an actor more than once.");

                Assert.Less(harness.GetActorHp(initialTarget), initialHp[initialTarget], "Initial target should take Jhin Q damage.");
                Assert.Less(harness.GetActorHp(closerOffRay), initialHp[closerOffRay], "The nearest eligible off-ray target should be selected by the first bounce.");
                Assert.Less(harness.GetActorHp(third), initialHp[third], "The second bounce target should take damage.");
                Assert.Less(harness.GetActorHp(fourth), initialHp[fourth], "The third bounce target should take damage.");
                Assert.AreEqual(initialHp[fifth], harness.GetActorHp(fifth), 0.001f, "The fifth eligible target must remain untouched after the four-hit budget is exhausted.");
            }
        }

        [Test]
        public void Q_RequiresAnEnemyInsideTheCasterCenteredRangeBeforeRelease()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { SkillId });
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_q_aim_release_target_acquisition_world"))
            {
                harness.EnterGameAndWarmup(reason: "jhin q directional aim release target acquisition");
                var casterActorId = harness.AssertPlayerActorBound();
                harness.Tick(1);

                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var noTargetCast = skills.TryCastBySlot(
                    casterActorId,
                    slot: 1,
                    aimPos: Vec3.Zero,
                    aimDir: Vec3.Forward,
                    targetActorId: 0);
                Assert.IsFalse(noTargetCast.Success,
                    "Jhin Q must reject release when no enemy exists inside its caster-centered range.");

                var inRangeTarget = HeroSkillHeadlessContract.SpawnEnemyHero(harness, "in_range_target", 9106, x: 0f, z: 4f);
                harness.Tick(1);
                var initialHp = harness.GetActorHp(inRangeTarget);

                var acquiredCast = skills.TryCastBySlot(
                    casterActorId,
                    slot: 1,
                    aimPos: Vec3.Zero,
                    aimDir: Vec3.Forward,
                    targetActorId: 0);
                Assert.IsTrue(acquiredCast.Success,
                    "Jhin Q should auto-lock an enemy inside its caster-centered range. failReason=" + acquiredCast.FailReason);

                var effectTrace = harness.TickUntilTraceNode(
                    MobaExecutionKind.EffectExecution,
                    SkillId,
                    maxTicks: harness.CalculateWaitTicksForSkillEffect(SkillId, SkillId, safetyFrames: 5) + 30,
                    message: "Jhin Q should launch its projectile after automatic target acquisition.");
                harness.AssertProjectileLaunchedUnderEffect(effectTrace.RootId, LauncherId, ProjectileId);
                harness.Tick(120);

                var records = MobaAcceptanceTraceExporter.CaptureTraceRecords(
                    harness, "jhin_q_aim_release_target_acquisition_world");
                var hit = false;
                for (var i = 0; i < records.Length; i++)
                {
                    if (records[i].kind == "ProjectileHit" &&
                        records[i].configId == ProjectileId &&
                        records[i].targetActorId == inRangeTarget)
                    {
                        hit = true;
                        break;
                    }
                }
                Assert.IsTrue(hit, "Auto-acquired Q target must receive the initial projectile hit.");
                Assert.Less(harness.GetActorHp(inRangeTarget), initialHp,
                    "Auto-acquired Q target should take damage from the projectile.");

                Assert.IsTrue(harness.Config.TryGetSkill(SkillId, out var qSkill));
                Assert.AreEqual(QInitialTargetQueryId, qSkill.RequiredTargetQueryId);
                Assert.IsTrue(harness.Config.TryGetSearchQueryTemplate(QInitialTargetQueryId, out var initialQuery));
                Assert.AreEqual(1, initialQuery.MaxCount);
                Assert.AreEqual((int)SearchTargetPointKind.Caster, initialQuery.Rules[0].Center);
                Assert.AreEqual(12f, initialQuery.Rules[0].Radius, 0.001f);
                Assert.IsTrue(harness.Config.TryGetSearchQueryTemplate(QBounceTargetQueryId, out var bounceQuery));
                Assert.AreEqual((int)SearchTargetPointKind.AimPosition, bounceQuery.Rules[0].Center);
            }
        }

        [Test]
        public void Q_LockedCastFacesTargetAndResolvesConfiguredMovingProjectileVfx()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { SkillId });
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_q_facing_and_projectile_vfx_world"))
            {
                Assert.IsTrue(harness.Config.TryGetProjectile(ProjectileId, out var projectile));
                Assert.IsNotNull(projectile);
                Assert.AreEqual(90013001, projectile.VfxId, "Jhin Q projectile must reference its authored moving VFX.");
                Assert.AreEqual(90013001, new BattleProjectileVfxResolver().ResolveSnapshotVfxId(
                    ProjectileId, ProjectilePresentationEventKind.Spawn),
                    "The actual projectile spawn event must resolve to the configured Jhin Q VFX.");

                harness.EnterGameAndWarmup(reason: "jhin q locked target facing and projectile vfx");
                var casterActorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "facing_target", 9110, x: 4f, z: 0f);
                harness.Tick(1);

                var cast = harness.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(
                    casterActorId, slot: 1, aimPos: Vec3.Zero, aimDir: Vec3.Forward, targetActorId: targetActorId);
                Assert.IsTrue(cast.Success, "Jhin Q should accept a valid locked target. failReason=" + cast.FailReason);

                var caster = harness.AssertActorEntity(casterActorId);
                Assert.Greater(caster.transform.Value.Forward.X, 0.9f,
                    "A locked cast with FaceToAim enabled must update authoritative facing toward its target.");
            }
        }

        [Test]
        public void W_DirectionalCastFacesSubmittedAimDir()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { WSkillId });
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_w_directional_cast_facing_world"))
            {
                harness.EnterGameAndWarmup(reason: "jhin w directional cast facing");
                var actorId = harness.AssertPlayerActorBound();
                var cast = harness.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(
                    actorId, slot: 2, aimPos: Vec3.Zero, aimDir: Vec3.Right, targetActorId: 0);
                Assert.IsTrue(cast.Success, "Jhin W should accept a valid directional cast. failReason=" + cast.FailReason);
                var actor = harness.AssertActorEntity(actorId);
                Assert.Greater(actor.transform.Value.Forward.X, 0.9f,
                    "A successful directional cast must face its submitted AimDir.");
            }
        }

        [Test]
        public void W_DirectionalCastFreezesAimDirectionIntoProjectileSpawnSnapshot()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { WSkillId });
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_w_direction_snapshot_world"))
            {
                harness.EnterGameAndWarmup(reason: "jhin w projectile direction snapshot");
                var actorId = harness.AssertPlayerActorBound();
                harness.Tick(1);

                var cast = harness.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(
                    actorId, slot: 2, aimPos: Vec3.Zero, aimDir: Vec3.Right, targetActorId: 0);
                Assert.IsTrue(cast.Success, "Jhin W should accept the frozen rightward direction. failReason=" + cast.FailReason);

                var effectTrace = harness.TickUntilTraceNode(
                    MobaExecutionKind.EffectExecution,
                    WSkillId,
                    maxTicks: harness.CalculateWaitTicksForSkillEffect(WSkillId, WSkillId, safetyFrames: 5) + 30,
                    message: "Jhin W should execute before its direction is checked.");
                harness.AssertProjectileLaunchedUnderEffect(effectTrace.RootId, WLauncherId, WProjectileId);

                MobaProjectileEventSnapshotEntry spawn = default;
                var found = false;
                for (var tick = 0; tick < 30 && !found; tick++)
                {
                    harness.Tick(1);
                    var snapshots = new List<WorldStateSnapshot>();
                    harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>()
                        .CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
                    for (var snapshotIndex = 0; snapshotIndex < snapshots.Count && !found; snapshotIndex++)
                    {
                        if (snapshots[snapshotIndex].OpCode != MobaOpCodes.Snapshot.ProjectileEvent) continue;
                        var entries = MobaProjectileEventSnapshotCodec.Deserialize(snapshots[snapshotIndex].Payload);
                        for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                        {
                            if (entries[entryIndex].TemplateId == WProjectileId &&
                                entries[entryIndex].Kind == (int)ProjectileEventKind.Spawn)
                            {
                                spawn = entries[entryIndex];
                                found = true;
                                break;
                            }
                        }
                    }
                }

                Assert.IsTrue(found, "Jhin W should publish a projectile spawn snapshot.");
                Assert.Greater(spawn.ForwardX, 0.9f,
                    "W projectile spawn must preserve the frozen HUD aim direction for VFX orientation.");
                Assert.AreEqual(0f, spawn.ForwardZ, 0.0001f,
                    "W projectile spawn must not fall back to the caster's previous forward direction.");

                var actor = harness.AssertActorEntity(actorId);
                Assert.Greater(actor.transform.Value.Forward.X, 0.9f,
                    "The caster facing and projectile direction must use the same frozen W aim snapshot.");
            }
        }

        [Test]
        public void W_HudAimReleaseSnapshotDrivesRealProjectileHitAndStopsAtFirstEnemy()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { WSkillId });
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_w_hud_release_hit_world"))
            {
                var hudContext = BattleContext.Rent();
                try
                {
                harness.EnterGameAndWarmup(reason: "jhin w hud release snapshot to hit");
                var casterActorId = harness.AssertPlayerActorBound();
                var firstTarget = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "hud_first", 9220, x: 0f, z: 5f);
                var secondTarget = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "hud_second", 9221, x: 0f, z: 9f);
                harness.Tick(1);
                var firstHp = harness.GetActorHp(firstTarget);
                var secondHp = harness.GetActorHp(secondTarget);

                using var hud = JhinHudAimFixture.Create(
                    hudContext,
                    2,
                    WSkillId,
                    SkillAimMode.Direction,
                    SkillUsePointMode.Aim,
                    range: 26f);
                // Drive the production pointer lifecycle through SkillButtonView, input view,
                // aim mapper, bridge and dispatcher. The release position is the submit source.
                var buttonCenter = hud.ButtonScreenPosition;
                hud.PointerDown(buttonCenter);
                hud.Drag(buttonCenter + new Vector2(0f, 120f));
                hud.PointerUp(buttonCenter + new Vector2(0f, 200f));
                Assert.IsTrue(BattleHudInputSource.TryConsumeSkillAimSubmit(hudContext, out var submitted),
                    "A real skill button release must produce a consumable W aim snapshot.");
                Assert.AreEqual(2, submitted.Slot);
                Assert.AreEqual(0f, submitted.AimDirX, 0.0001f);
                Assert.AreEqual(1f, submitted.AimDirZ, 0.0001f);

                SubmitHudReleaseToBattleWorld(
                    harness,
                    submitted.Slot,
                    aimPos: Vec3.Zero,
                    aimDir: new Vec3(submitted.AimDirX, submitted.AimDirY, submitted.AimDirZ),
                    skillName: "W");

                harness.TickUntilTraceNode(
                    MobaExecutionKind.EffectExecution,
                    WSkillId,
                    maxTicks: harness.CalculateWaitTicksForSkillEffect(WSkillId, WSkillId, safetyFrames: 5) + 30,
                    message: "W should launch from the HUD-submitted direction.");
                harness.Tick(100);

                Assert.Less(harness.GetActorHp(firstTarget), firstHp,
                    "The first enemy on the final HUD aim line should take W damage.");
                    Assert.AreEqual(secondHp, harness.GetActorHp(secondTarget), 0.001f,
                        "The W projectile should stop at the first enemy on the submitted line.");
                }
                finally
                {
                    BattleContext.Return(hudContext);
                }
            }
        }

        [UnityTest]
        public IEnumerator W_LocalBattleHudReleaseFlowsThroughInputFeatureAndDamagesEnemy()
        {
            JhinHudAimFixture hud = null;
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
                    () => $"The local demo flow did not reach InMatch. root={flow.CurrentPhase}, battle={flow.CurrentBattlePhase}, " +
                          $"entry={flow.BattleEntryState}, rejection={flow.BattleEntryRejection}.{DescribeAssetLoadState(entry)}");

                Assert.IsTrue(entry.TryGet(out BattleContext ctx), "BattleContext must be attached after entering battle.");
                yield return TickUntil(flow,
                    () => TryGetLocalActor(ctx, out _, out _),
                    240,
                    "Local Jhin and its actor mapping were not ready.");
                Assert.IsTrue(TryGetLocalActor(ctx, out var localActorId, out var localActor),
                    "The local actor entity should be available before releasing W.");

                Assert.IsTrue(ctx.Session.TryGetWorld(out var world), "The local battle session should expose its logic world.");
                var playerActors = world.Services.Resolve<MobaPlayerActorMapService>();
                Assert.IsTrue(playerActors.TryGetActorId(new PlayerId("p2"), out var enemyActorId),
                    "The local training opponent should have an actor mapping.");
                var actors = world.Services.Resolve<MobaActorLookupService>();
                Assert.IsTrue(actors.TryGetActorEntity(enemyActorId, out var enemy) && enemy != null &&
                              enemy.hasAttributeGroup && enemy.hasTransform,
                    "The training opponent must expose combat attributes and a transform.");
                var casterPosition = localActor.transform.Value.Position;
                // Keep the map's projectile-blocking center obstacle intact while placing
                // the training opponent on the clear segment before it.
                var enemyTransform = enemy.transform.Value;
                enemy.ReplaceTransform(new Transform3(new Vec3(0f, enemyTransform.Position.Y, 5f),
                    enemyTransform.Rotation, enemyTransform.Scale));
                FlowTick(flow, 1);
                var enemyPosition = enemy.transform.Value.Position;
                Assert.Greater(enemyPosition.Z, casterPosition.Z,
                    "The local opponent should remain in front of Jhin for the W firing line.");
                var collisionWorld = world.Services.Resolve<AbilityKit.Combat.Collision.ICollisionService>().World;
                var firingRay = new Ray3(new Vec3(0f, 0f, 1f), Vec3.Forward);
                var firingLayers = new AbilityKit.Combat.Collision.LayerFilter(
                    MobaCollisionLayers.UnitMask | MobaCollisionLayers.WorldMask);
                Assert.IsTrue(collisionWorld.Raycast(in firingRay, 25f, in firingLayers, out var firstRayHit),
                    "The local W acceptance lane must contain the configured enemy target.");
                Assert.AreEqual(enemy.collisionId.Value, firstRayHit.Collider,
                    $"The local W acceptance lane must hit the enemy before map geometry. firstCollider={firstRayHit.Collider}");
                var enemyHpBefore = enemy.GetMobaAttrs().Hp;
                var projectileSnapshots = world.Services.Resolve<IWorldStateSnapshotBatchProvider>();
                var frameTime = world.Services.Resolve<IFrameTime>();
                var projectileLifecycle = world.Services.Resolve<IMobaTemporaryEntityLifecycleHealthProvider>();
                var projectileHealthBefore = projectileLifecycle.GetHealth(MobaTemporaryEntityKind.Projectile);
                var inputFeature = entry.Get<BattleInputFeature>();
                Assert.IsNotNull(inputFeature, "The composed local battle must register BattleInputFeature.");
                var successfulSkillSubmitsBefore = inputFeature.SkillSubmitSuccessCount;

                hud = JhinHudAimFixture.Create(
                    ctx,
                    slot: 2,
                    WSkillId,
                    SkillAimMode.Direction,
                    SkillUsePointMode.Aim,
                    range: 26f);
                var buttonCenter = hud.ButtonScreenPosition;
                hud.PointerDown(buttonCenter);
                hud.Drag(buttonCenter + new Vector2(0f, 120f));
                hud.PointerUp(buttonCenter + new Vector2(0f, 200f));

                yield return TickUntil(flow,
                    () => inputFeature.SkillSubmitSuccessCount > successfulSkillSubmitsBefore,
                    12,
                    "BattleInputFeature did not submit the W release to the active battle session.");

                var snapshots = new List<WorldStateSnapshot>(16);
                var projectileEvents = new List<string>(8);
                var snapshotOps = new List<string>(8);
                for (var tick = 0; tick < 180 && enemy.GetMobaAttrs().Hp >= enemyHpBefore; tick++)
                {
                    FlowTick(flow, 1);
                    snapshots.Clear();
                    projectileSnapshots.CollectSnapshots(frameTime.Frame, snapshots, 32);
                    for (var snapshotIndex = 0; snapshotIndex < snapshots.Count; snapshotIndex++)
                    {
                        var snapshot = snapshots[snapshotIndex];
                        if (snapshotOps.Count < 16) snapshotOps.Add(snapshot.OpCode.ToString());
                        if (snapshot.OpCode != MobaOpCodes.Snapshot.ProjectileEvent) continue;
                        var entries = MobaProjectileEventSnapshotCodec.Deserialize(snapshot.Payload);
                        for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                        {
                            var projectileEvent = entries[entryIndex];
                            if (projectileEvents.Count >= 32) break;
                            projectileEvents.Add(
                                $"kind={(ProjectileEventKind)projectileEvent.Kind} template={projectileEvent.TemplateId} projectile={projectileEvent.ProjectileId} " +
                                $"pos=({projectileEvent.X:F2},{projectileEvent.Y:F2},{projectileEvent.Z:F2}) " +
                                $"dir=({projectileEvent.ForwardX:F2},{projectileEvent.ForwardY:F2},{projectileEvent.ForwardZ:F2}) " +
                                $"collider={projectileEvent.HitCollider} exit={projectileEvent.ExitReason}");
                        }
                    }

                    if (enemy.GetMobaAttrs().Hp < enemyHpBefore) break;
                    yield return null;
                }

                var projectileHealthAfter = projectileLifecycle.GetHealth(MobaTemporaryEntityKind.Projectile);
                Assert.Less(enemy.GetMobaAttrs().Hp, enemyHpBefore,
                    $"The local W release did not damage the enemy. hpBefore={enemyHpBefore:F2}, hpAfter={enemy.GetMobaAttrs().Hp:F2}, " +
                    $"casterStart=({casterPosition.X:F2},{casterPosition.Y:F2},{casterPosition.Z:F2}), " +
                    $"casterEnd=({localActor.transform.Value.Position.X:F2},{localActor.transform.Value.Position.Y:F2},{localActor.transform.Value.Position.Z:F2}), " +
                    $"enemyStart=({enemyPosition.X:F2},{enemyPosition.Y:F2},{enemyPosition.Z:F2}), " +
                    $"enemyEnd=({enemy.transform.Value.Position.X:F2},{enemy.transform.Value.Position.Y:F2},{enemy.transform.Value.Position.Z:F2}), " +
                    $"projectileHealthBefore=[{projectileHealthBefore}], projectileHealthAfter=[{projectileHealthAfter}], " +
                    $"projectileEvents=[{string.Join("; ", projectileEvents)}], snapshotOps=[{string.Join(",", snapshotOps)}], " +
                    $"snapshotFrame={frameTime.Frame.Value}, collisions=[{CaptureCollisionSummary(world)}], " +
                    $"localActorId={localActorId}, lastFrame={ctx.LastFrame}, logicTime={ctx.LogicTimeSeconds:F3}");

                Assert.AreEqual(successfulSkillSubmitsBefore + 1, inputFeature.SkillSubmitSuccessCount,
                    "One HUD release should submit exactly one skill command to the active session.");
                Assert.IsFalse(BattleHudInputSource.TryConsumeSkillAimSubmit(ctx, out _),
                    "BattleInputFeature should consume the released aim snapshot exactly once.");
                Assert.Less(enemy.GetMobaAttrs().Hp, enemyHpBefore,
                    "The command handled by the local session should produce real W damage.");
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

        internal sealed class JhinHudAimFixture : System.IDisposable
        {
            private readonly BattleHudInputUi _ui;
            private readonly BattleHudInputEventBridge _bridge;
            private readonly SkillButtonView _button;
            private readonly bool _ownsContext;

            private JhinHudAimFixture(BattleHudInputUi ui, BattleHudInputEventBridge bridge, SkillButtonView button)
            {
                _ui = ui;
                _bridge = bridge;
                _button = button;
            }

            private JhinHudAimFixture(BattleHudInputUi ui, BattleHudInputEventBridge bridge, SkillButtonView button, BattleContext context, bool ownsContext)
                : this(ui, bridge, button)
            {
                Context = context;
                _ownsContext = ownsContext;
            }

            public BattleContext Context { get; }

            public static JhinHudAimFixture Create(
                BattleContext context,
                int slot,
                int skillId,
                SkillAimMode aimMode,
                SkillUsePointMode usePointMode,
                float range)
            {
                var ownsContext = context == null;
                context ??= BattleContext.Rent();
                var root = new GameObject("JhinHudAimFixture.Root", typeof(RectTransform));
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = new Vector2(1200f, 900f);
                var buttons = new SkillButtonView[3];
                SkillButtonView targetButton = null;
                for (var i = 0; i < buttons.Length; i++)
                {
                    var buttonObject = new GameObject("Skill" + (i + 1), typeof(RectTransform));
                    buttonObject.transform.SetParent(root.transform, false);
                    var buttonRect = (RectTransform)buttonObject.transform;
                    buttonRect.anchoredPosition = new Vector2(-400f + i * 100f, -300f);
                    buttonRect.sizeDelta = new Vector2(80f, 80f);
                    var button = buttonObject.AddComponent<SkillButtonView>();
                    var config = SkillButtonConfig.Default;
                    config.EnableAim = i + 1 == slot;
                    config.AimMode = aimMode;
                    config.SelectRange = range;
                    config.FaceToAim = true;
                    config.AimMaxRadius = 400f;
                    config.IndicatorShape = config.EnableAim
                        ? (aimMode == SkillAimMode.Point ? SkillAimIndicatorShape.TargetCircle : SkillAimIndicatorShape.DirectionLine)
                        : SkillAimIndicatorShape.Hidden;
                    button.Initialize(buttonRect, rootRect, null, config);
                    buttons[i] = button;
                    if (i + 1 == slot) targetButton = button;
                }

                var inputView = root.AddComponent<BattleHudInputView>();
                inputView.Initialize(null, buttons);
                var mapper = new BattleHudSkillAimInputMapper();
                mapper.Initialize(inputView, null);
                var ui = new BattleHudInputUi(root, inputView, null, null, mapper, buttons, null);
                var bridge = new BattleHudInputEventBridge(context);
                bridge.SetSkillSpecs(new Dictionary<int, BattleHudSkillPresentationSpec>
                {
                    [slot] = new BattleHudSkillPresentationSpec(
                        skillId,
                        "Jhin skill",
                        aimMode == SkillAimMode.Point ? BattleHudSkillPreviewShape.TargetCircle : BattleHudSkillPreviewShape.DirectionLine,
                        aimMode == SkillAimMode.Point ? SkillAimIndicatorShape.TargetCircle : SkillAimIndicatorShape.DirectionLine,
                        range,
                        0.8f,
                        0f,
                        Color.white,
                        aimMode: aimMode,
                        usePointMode: usePointMode,
                        faceToAim: true)
                });
                bridge.Bind(ui);
                return new JhinHudAimFixture(ui, bridge, targetButton, context, ownsContext);
            }

            public void PointerDown(Vector2 position) => _button.OnPointerDown(CreatePointer(position));
            public void Drag(Vector2 position) => _button.OnDrag(CreatePointer(position));
            public void PointerUp(Vector2 position) => _button.OnPointerUp(CreatePointer(position));
            public Vector2 ButtonScreenPosition => RectTransformUtility.WorldToScreenPoint(null, _button.transform.position);

            public void Dispose()
            {
                _bridge.Dispose();
                _ui.Destroy();
                if (_ownsContext) BattleContext.Return(Context);
            }

            private static PointerEventData CreatePointer(Vector2 position)
            {
                return new PointerEventData(EventSystem.current) { pointerId = 1, position = position };
            }
        }

        private static void SubmitHudReleaseToBattleWorld(
            MobaSkillConfigTestHarness harness,
            int slot,
            Vec3 aimPos,
            Vec3 aimDir,
            string skillName)
        {
            var result = harness.SubmitSkillInputAndGetResult(
                harness.PlayerId,
                slot,
                SkillInputPhase.Release,
                out var frame,
                targetActorId: 0,
                aimPos: aimPos,
                aimDir: aimDir);
            Assert.IsTrue(result.Succeeded,
                $"Jhin {skillName} HUD release should be accepted by the battle input coordinator at frame {frame.Value}. {result}");
            Assert.AreEqual(1, result.AcceptedCount,
                $"Jhin {skillName} HUD release should submit exactly one frame command.");
            Assert.AreEqual(1, result.HandledCount,
                $"Jhin {skillName} HUD release must be handled by the registered skill input command handler, not a direct-cast fallback.");
        }

        private static IEnumerator TickUntil(GameFlowDomain flow, System.Func<bool> predicate, int maxTicks, string failureMessage)
        {
            return TickUntil(flow, predicate, maxTicks, () => failureMessage);
        }

        private static IEnumerator TickUntil(
            GameFlowDomain flow,
            System.Func<bool> predicate,
            int maxTicks,
            System.Func<string> failureMessage)
        {
            for (var i = 0; i < maxTicks; i++)
            {
                FlowTick(flow, 1);
                if (predicate()) yield break;
                yield return null;
            }

            Assert.Fail(failureMessage == null ? string.Empty : failureMessage());
        }

        private static void FlowTick(GameFlowDomain flow, int ticks)
        {
            const float fixedDeltaTime = 1f / 30f;
            for (var i = 0; i < ticks; i++) flow.Tick(fixedDeltaTime);
        }

        private static string DescribeAssetLoadState(GameEntry entry)
        {
            if (entry == null || !entry.TryGet(out BattleLoadingScreenFeature loading) || loading == null)
                return " BattleLoadingScreenFeature is not registered.";

            var snapshot = loading.CurrentSnapshot;
            var errors = snapshot.Errors == null || snapshot.Errors.Count == 0
                ? "none"
                : string.Join("; ", snapshot.Errors);
            return $" AssetLoad: completed={snapshot.Completed}, success={snapshot.Success}, " +
                   $"progress={snapshot.LoadedCount}/{snapshot.TotalCount}, current={snapshot.CurrentAssetKey}, " +
                   $"error={snapshot.ErrorMessage}, errors={errors}.";
        }

        private static bool TryGetLocalActor(BattleContext context, out int actorId, out ActorEntity actor)
        {
            actorId = 0;
            actor = null;
            if (context?.Session == null || context.EntityQuery == null ||
                !context.Session.TryGetWorld(out var world) || world?.Services == null ||
                !world.Services.TryResolve<MobaPlayerActorMapService>(out var playerActors) || playerActors == null)
                return false;

            var playerIdText = context.Plan.World.PlayerId;
            var playerId = new PlayerId(string.IsNullOrEmpty(playerIdText) ? "p1" : playerIdText);
            if (!playerActors.TryGetActorId(playerId, out actorId) || actorId <= 0 ||
                !world.Services.TryResolve<MobaActorLookupService>(out var actors) || actors == null ||
                !actors.TryGetActorEntity(actorId, out actor) || actor == null || !actor.hasTransform)
                return false;

            return true;
        }

        private static void InvokePrivate(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing private method: {methodName}");
            method.Invoke(target, null);
        }

        [Test]
        public void W_FiresStraightProjectileAppliesLevelDamageOnceAndStopsAtFirstEnemy()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { WSkillId });
            var skill = new HeroSkillSlotContract(2, WSkillId, WSkillId, WSkillId);
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_w_linear_runtime_contract_world"))
            {
                harness.AssertSlotSkill(skill.Slot, WSkillId);
                harness.AssertSkillUsesCastFlow(WSkillId, WSkillId);
                harness.AssertCastFlowContainsTimelineEffect(WSkillId, WSkillId);
                harness.AssertProjectileConfigExists(WLauncherId, WProjectileId);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    WHitEffectId,
                    (int)TriggeringConstants.GiveDamageId.Value);

                harness.EnterGameAndWarmup(reason: "jhin w straight projectile runtime contract");
                var casterActorId = harness.AssertPlayerActorBound();
                var firstTarget = HeroSkillHeadlessContract.SpawnEnemyHero(harness, "first", 9201, x: 0f, z: 5f);
                var secondTarget = HeroSkillHeadlessContract.SpawnEnemyHero(harness, "second", 9202, x: 0f, z: 9f);
                harness.Tick(1);
                var firstHp = harness.GetActorHp(firstTarget);
                var secondHp = harness.GetActorHp(secondTarget);

                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(
                    casterActorId,
                    skill.Slot,
                    aimPos: default,
                    aimDir: Vec3.Forward,
                    targetActorId: 0);
                Assert.IsTrue(cast.Success, "Jhin W should accept a directional cast without an explicit target. failReason=" + cast.FailReason);

                var effectTrace = harness.TickUntilTraceNode(
                    MobaExecutionKind.EffectExecution,
                    WSkillId,
                    maxTicks: harness.CalculateWaitTicksForSkillEffect(WSkillId, WSkillId, safetyFrames: 5) + 30,
                    message: "Jhin W should execute its authored effect before launching the projectile.");
                harness.AssertProjectileLaunchedUnderEffect(effectTrace.RootId, WLauncherId, WProjectileId);
                harness.Tick(100);

                var records = MobaAcceptanceTraceExporter.CaptureTraceRecords(harness, "jhin_w_linear_runtime_contract_world");
                var hits = 0;
                var damages = 0;
                var firstDamaged = false;
                for (var i = 0; i < records.Length; i++)
                {
                    var record = records[i];
                    if (record.kind == "ProjectileHit" && record.configId == WProjectileId) hits++;
                    if (record.kind == "DamageApply" && record.configId == WSkillId)
                    {
                        damages++;
                        firstDamaged |= record.targetActorId == firstTarget;
                    }
                }

                Assert.AreEqual(1, hits, "Jhin W should stop on its first valid enemy hit.");
                Assert.AreEqual(1, damages, "Jhin W should apply one damage effect for the first hit.");
                Assert.IsTrue(firstDamaged, "The first enemy on W's line should receive the hit effect.");
                Assert.Less(harness.GetActorHp(firstTarget), firstHp, "First enemy should take level-table-driven W damage.");
                Assert.AreEqual(secondHp, harness.GetActorHp(secondTarget), 0.001f, "The projectile should terminate before reaching the second enemy.");
                Assert.IsTrue(harness.HasActorBuff(firstTarget, WMarkBuffId), "Positive W damage should apply the short-lived Jhin mark after the hit.");
                Assert.IsFalse(harness.HasActorBuff(firstTarget, WRootBuffId), "An unmarked target should not be rooted by the same W hit that first marks it.");
                Assert.IsTrue(harness.TryGetActorBuffRemainingSeconds(firstTarget, WMarkBuffId, out var markRemaining), "The damage event should create a timed W mark.");
                Assert.Greater(markRemaining, 0f, "The mark should have a live duration after W damage.");
                harness.Tick(260);
                Assert.IsFalse(harness.HasActorBuff(firstTarget, WMarkBuffId), "The W mark should expire naturally after its configured four-second lifetime.");
            }
        }

        [Test]
        public void WMarkTrigger_IgnoresPositiveDamageFromUnrelatedReasonParameter()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { WSkillId });
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_w_unrelated_damage_mark_filter_world"))
            {
                harness.EnterGameAndWarmup(reason: "jhin w mark source filter contract");
                var target = HeroSkillHeadlessContract.SpawnEnemyHero(harness, "unrelated_damage_target", 9204, x: 3f, z: 0f);
                harness.Tick(1);

                var result = HeroSkillHeadlessContract.ExecuteDamage(
                    harness,
                    attackerActorId: harness.AssertPlayerActorBound(),
                    targetActorId: target,
                    baseDamage: 10f,
                    reasonKind: DamageReasonKind.Skill,
                    reasonParam: 987654);
                Assert.Greater(result.Value, 0f, "The unrelated damage must be positive so the test exercises reason filtering rather than damage filtering.");
                harness.Tick(1);

                Assert.IsFalse(harness.HasActorBuff(target, WMarkBuffId), "Positive damage with a reason other than Jhin Q/W must not grant the Jhin mark.");
            }
        }

        [Test]
        public void QMark_IsGrantedByRealProjectileDamageThenRefreshedByWAndExpires()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { SkillId, WSkillId });
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_q_to_w_mark_refresh_runtime_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "jhin q to w mark refresh contract");
                var casterActorId = harness.AssertPlayerActorBound();
                var target = HeroSkillHeadlessContract.SpawnEnemyHero(harness, "refresh_target", 9206, x: 0f, z: 5f);
                harness.Tick(1);

                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var qCast = skills.TryCastBySlot(casterActorId, 1, aimPos: default, aimDir: Vec3.Forward, targetActorId: target);
                Assert.IsTrue(qCast.Success, "Jhin Q should cast at the target to create the first mark through the real projectile damage event.");
                harness.TickUntilTraceNode(
                    MobaExecutionKind.EffectExecution,
                    SkillId,
                    maxTicks: harness.CalculateWaitTicksForSkillEffect(SkillId, SkillId, safetyFrames: 5) + 30,
                    message: "Jhin Q should execute before its projectile can mark the target.");
                TickUntilActorBuff(harness, target, WMarkBuffId, maxTicks: 120, message: "Positive Q projectile damage should grant the Jhin mark through damage.apply.after.");
                Assert.IsTrue(harness.TryGetActorBuffRemainingSeconds(target, WMarkBuffId, out var firstRemaining), "Q damage should create a timed mark.");
                Assert.Greater(firstRemaining, 3f, "The Q mark should have most of its configured four-second lifetime remaining.");

                harness.TickUntilSkillStops(casterActorId, 1, maxTicks: 180, message: "Jhin Q must finish before W can be submitted by the same skill runner.");
                harness.Tick(30);
                Assert.IsTrue(harness.TryGetActorBuffRemainingSeconds(target, WMarkBuffId, out var beforeRefresh), "The Q mark should remain active before W arrives.");
                Assert.Less(beforeRefresh, firstRemaining, "The mark should lose time before the W refresh.");

                var wCast = skills.TryCastBySlot(casterActorId, 2, aimPos: default, aimDir: Vec3.Forward, targetActorId: 0);
                Assert.IsTrue(wCast.Success, "Jhin W should cast after Q has completed.");
                TickUntilActorBuff(harness, target, WRootBuffId, maxTicks: 180, message: "W should root the target marked by the preceding real Q hit.");
                Assert.IsTrue(harness.TryGetActorBuffRemainingSeconds(target, WMarkBuffId, out var afterRefresh), "W damage should refresh the existing mark.");
                Assert.Greater(afterRefresh, 3f, "W damage should refresh the mark close to four seconds.");

                harness.Tick(250);
                Assert.IsFalse(harness.HasActorBuff(target, WMarkBuffId), "The refreshed Q/W mark should expire naturally after four seconds.");
            }
        }

        [Test]
        public void W_DamagesPreviouslyMarkedSuperArmorTargetButGenericControlGateRejectsRoot()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { WSkillId });
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_w_superarmor_root_gate_world"))
            {
                harness.EnterGameAndWarmup(reason: "jhin w super armor control gate contract");
                var casterActorId = harness.AssertPlayerActorBound();
                var target = HeroSkillHeadlessContract.SpawnEnemyHero(harness, "superarmor_marked", 9205, x: 0f, z: 5f);
                harness.Tick(1);
                harness.AddScenarioBuff(target, WMarkBuffId, casterActorId, durationOverrideMs: 4000);
                harness.AddScenarioBuff(target, GenericSuperArmorBuffId, target, durationOverrideMs: 4000);
                var hpBeforeW = harness.GetActorHp(target);

                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(casterActorId, 2, aimPos: default, aimDir: Vec3.Forward, targetActorId: 0);
                Assert.IsTrue(cast.Success, "Jhin W should still cast at a marked super-armor target. failReason=" + cast.FailReason);
                harness.TickUntilTraceNode(
                    MobaExecutionKind.EffectExecution,
                    WSkillId,
                    maxTicks: harness.CalculateWaitTicksForSkillEffect(WSkillId, WSkillId, safetyFrames: 5) + 30,
                    message: "Jhin W should launch while the marked target has super armor.");
                TickUntilActorBuff(harness, target, WMarkBuffId, maxTicks: 120, message: "The existing mark should remain until W's damage and follow-up mark refresh resolve.");
                harness.Tick(20);

                Assert.Less(harness.GetActorHp(target), hpBeforeW, "Super armor should block the root without blocking W damage.");
                Assert.IsTrue(harness.HasActorBuff(target, GenericSuperArmorBuffId), "The scenario super-armor source should remain active during the hit.");
                Assert.IsFalse(harness.HasActorBuff(target, WRootBuffId), "The shared control template must reject root application to a super-armor target.");
            }
        }

        [Test]
        public void W_DamagesPreviouslyMarkedControlImmuneTargetButDoesNotRoot()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { WSkillId });
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_w_control_immune_root_gate_world"))
            {
                harness.EnterGameAndWarmup(reason: "jhin w control immune contract");
                var casterActorId = harness.AssertPlayerActorBound();
                var target = HeroSkillHeadlessContract.SpawnEnemyHero(harness, "control_immune_marked", 9210, x: 0f, z: 5f);
                harness.Tick(1);
                harness.AddScenarioBuff(target, WMarkBuffId, casterActorId, durationOverrideMs: 4000);

                Assert.IsTrue(MobaGameplayTagCatalog.TryGet("State.ControlImmune", out var controlImmuneTag),
                    "The published MOBA tag catalog should contain State.ControlImmune.");
                var immunity = new ScenarioTagContinuous(target, controlImmuneTag);
                var continuousManager = harness.World.Services.Resolve<IContinuousManager>();
                Assert.IsTrue(continuousManager.Register(immunity), "The scenario tag source should register with the production continuous manager.");
                Assert.IsTrue(continuousManager.TryActivate(immunity), "The scenario tag source should activate through the production lifecycle.");
                var effectiveTags = harness.World.Services.Resolve<IMobaEffectiveTagQueryService>();
                Assert.IsTrue(effectiveTags.HasTag(target, controlImmuneTag), "The target should have effective ControlImmune before W arrives.");

                var hpBeforeW = harness.GetActorHp(target);
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(casterActorId, 2, aimPos: default, aimDir: Vec3.Forward, targetActorId: 0);
                Assert.IsTrue(cast.Success, "Jhin W should still cast at a marked control-immune target. failReason=" + cast.FailReason);
                harness.TickUntilTraceNode(
                    MobaExecutionKind.EffectExecution,
                    WSkillId,
                    maxTicks: harness.CalculateWaitTicksForSkillEffect(WSkillId, WSkillId, safetyFrames: 5) + 30,
                    message: "Jhin W should launch while the marked target has ControlImmune.");
                harness.Tick(100);

                Assert.Less(harness.GetActorHp(target), hpBeforeW, "ControlImmune should block the root without blocking W damage.");
                Assert.IsTrue(effectiveTags.HasTag(target, controlImmuneTag), "ControlImmune should remain effective through the W hit.");
                Assert.IsFalse(harness.HasActorBuff(target, WRootBuffId), "The shared control gate must reject root application to a ControlImmune target.");
                continuousManager.TryEnd(immunity, ContinuousEndReason.CleanedUp);
            }
        }

        [Test]
        public void W_RootsPreviouslyMarkedTargetBeforeApplyingItsDamage()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { WSkillId });
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_w_marked_root_runtime_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "jhin w marked target root contract");
                var casterActorId = harness.AssertPlayerActorBound();
                var target = HeroSkillHeadlessContract.SpawnEnemyHero(harness, "marked", 9203, x: 0f, z: 5f);
                harness.Tick(1);

                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                harness.AddScenarioBuff(target, WMarkBuffId, casterActorId, durationOverrideMs: 4000);

                var wCast = skills.TryCastBySlot(casterActorId, 2, aimPos: default, aimDir: Vec3.Forward, targetActorId: 0);
                Assert.IsTrue(wCast.Success, "Jhin W should cast at a previously marked target. failReason=" + wCast.FailReason);
                var hpBeforeW = harness.GetActorHp(target);
                TickUntilActorBuff(harness, target, WRootBuffId, maxTicks: 180, message: "W should root a target marked by a prior positive Q hit.");

                Assert.Less(harness.GetActorHp(target), hpBeforeW, "Marked target should still take W damage.");
                Assert.IsTrue(harness.TryGetActorBuffRemainingSeconds(target, WRootBuffId, out var remaining), "W root should remain active after the hit.");
                Assert.Greater(remaining, 0f, "W root duration should be positive.");
            }
        }

        [Test]
        public void E_ArmsAfterDelayHitsOneEnemyAppliesDamageAndSlowThenRemovesArea()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { ESkillId });
            var skill = new HeroSkillSlotContract(3, ESkillId, ESkillId, ESkillId);
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_e_area_runtime_contract_world"))
            {
                harness.AssertSlotSkill(skill.Slot, ESkillId);
                harness.AssertSkillUsesCastFlow(ESkillId, ESkillId);
                harness.AssertCastFlowContainsTimelineEffect(ESkillId, ESkillId);
                Assert.IsTrue(harness.Config.TryGetAoe(EAreaId, out var area), "Jhin E area config should be published.");
                Assert.AreEqual(500, area.DelayMs, "Jhin E trap should remain unarmed for its configured delay.");
                Assert.IsTrue(area.ConsumeOnFirstEnter, "Jhin E trap should consume its first accepted enemy enter.");
                harness.AssertProjectileConfigExists(ELauncherId, EProjectileId);
                Assert.IsTrue(harness.Config.TryGetProjectile(EProjectileId, out var eProjectile),
                    "Jhin E should publish a dedicated flying trap projectile.");
                Assert.AreEqual(ProjectileHitPolicyKind.Pierce, eProjectile.HitPolicyKind,
                    "The E trap must pass through units and land at the selected point.");
                Assert.AreEqual(90013003, eProjectile.VfxId,
                    "The thrown trap should reuse Jhin's authored E trap model and effects during flight.");
                CollectionAssert.Contains(eProjectile.OnExitTriggerIds, ELandingTriggerId,
                    "The landing trigger should be dispatched only after the E projectile exits.");
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    ELaunchTriggerId,
                    (int)TriggeringConstants.ShootProjectileId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    ELandingTriggerId,
                    (int)TriggeringConstants.SpawnAreaId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    EEnterTriggerId,
                    (int)TriggeringConstants.GiveDamageId.Value,
                    (int)TriggeringConstants.AddBuffId.Value,
                    (int)TriggeringConstants.RemoveAreaId.Value);

                harness.EnterGameAndWarmup(reason: "jhin e delayed area runtime contract");
                var casterActorId = harness.AssertPlayerActorBound();
                var enemy = HeroSkillHeadlessContract.SpawnEnemyHero(harness, "enemy", 9301, x: 0f, z: 2f);
                harness.Tick(1);
                var enemyHp = harness.GetActorHp(enemy);

                using var hud = JhinHudAimFixture.Create(
                    null,
                    skill.Slot,
                    ESkillId,
                    SkillAimMode.Point,
                    SkillUsePointMode.Aim,
                    range: 26f);
                var buttonCenter = hud.ButtonScreenPosition;
                hud.PointerDown(buttonCenter);
                hud.Drag(buttonCenter + new Vector2(0f, 20f));
                hud.PointerUp(buttonCenter + new Vector2(0f, 30.76923f));
                Assert.IsTrue(BattleHudInputSource.TryConsumeSkillAimSubmit(hud.Context, out var submitted),
                    "A real skill button release must produce a consumable E ground-target snapshot.");
                Assert.AreEqual(skill.Slot, submitted.Slot);
                Assert.AreEqual(0f, submitted.AimPosX, 0.01f);
                Assert.AreEqual(2f, submitted.AimPosZ, 0.01f,
                    "The HUD's range projection must preserve the selected 2-unit trap landing point.");
                SubmitHudReleaseToBattleWorld(
                    harness,
                    submitted.Slot,
                    aimPos: new Vec3(submitted.AimPosX, submitted.AimPosY, submitted.AimPosZ),
                    aimDir: new Vec3(submitted.AimDirX, submitted.AimDirY, submitted.AimDirZ),
                    skillName: "E");

                var effectTrace = harness.TickUntilTraceNode(
                    MobaExecutionKind.EffectExecution,
                    ESkillId,
                    maxTicks: harness.CalculateWaitTicksForSkillEffect(ESkillId, ESkillId, safetyFrames: 5) + 30,
                    message: "Jhin E should execute its authored Flow and launch the trap projectile.");
                harness.AssertActionExecutedUnderEffect(
                    effectTrace.RootId,
                    (int)TriggeringConstants.ShootProjectileId.Value,
                    TriggeringConstants.Actions.ShootProjectile);
                var eSpawn = TickUntilProjectileSpawnSnapshot(harness, EProjectileId, maxTicks: 10);
                Assert.AreEqual((int)ProjectileEventKind.Spawn, eSpawn.Kind);
                Assert.Greater(eSpawn.ProjectileActorId, 0,
                    "The flying E trap must expose a scene actor for its travel presentation.");
                var casterPosition = harness.AssertActorEntity(casterActorId).transform.Value.Position;
                var projectileActor = harness.AssertActorEntity(eSpawn.ProjectileActorId);
                Assert.IsTrue(projectileActor.hasTransform,
                    "The flying E trap actor must expose its authoritative transform for movement and presentation follow.");
                var launchPosition = projectileActor.transform.Value.Position;
                var launchOffsetX = launchPosition.X - casterPosition.X;
                var launchOffsetZ = launchPosition.Z - casterPosition.Z;
                Assert.Less(launchOffsetX * launchOffsetX + launchOffsetZ * launchOffsetZ, 2.25f,
                    $"The E trap should launch from Jhin's vicinity, not appear at its destination. caster=({casterPosition.X:F2},{casterPosition.Z:F2}), projectile=({launchPosition.X:F2},{launchPosition.Z:F2})");
                var areas = harness.World.Services.Resolve<MobaAreaRuntimeService>();
                Assert.AreEqual(0, areas.ActiveCount,
                    "The trap area must not exist while the thrown trap is still travelling.");
                var movedDuringFlight = false;
                for (var i = 0; i < 10 && !movedDuringFlight; i++)
                {
                    harness.Tick(1);
                    projectileActor = harness.AssertActorEntity(eSpawn.ProjectileActorId);
                    var currentPosition = projectileActor.transform.Value.Position;
                    var dx = currentPosition.X - launchPosition.X;
                    var dz = currentPosition.Z - launchPosition.Z;
                    movedDuringFlight = dx * dx + dz * dz > 0.0025f;
                }
                Assert.IsTrue(movedDuringFlight,
                    "The E trap projectile actor should advance along its flight path before landing.");
                Assert.AreEqual(0, areas.ActiveCount,
                    "The E trap area must remain absent throughout projectile flight.");
                var eExit = TickUntilProjectileExitSnapshot(harness, eSpawn.ProjectileId, maxTicks: 120);
                Assert.AreEqual((int)ProjectileExitReason.MaxDistance, eExit.ExitReason,
                    "The trap should reach the selected ground point instead of stopping on unit collision.");
                Assert.AreEqual(2f, eExit.Z, 0.15f, "The E trap should land at the submitted ground point.");
                harness.TickUntilTraceNodeInRoot(
                    effectTrace.RootId,
                    MobaExecutionKind.AreaSpawn,
                    EAreaId,
                    maxTicks: 10,
                    message: "Jhin E should spawn its area at the projectile's landing point.");
                Assert.AreEqual(1, areas.ActiveCount, "The trap should remain active before a valid armed enter.");

                harness.Tick(5);
                Assert.AreEqual(enemyHp, harness.GetActorHp(enemy), 0.001f, "The trap must not damage targets before the 500 ms arming delay.");

                for (var i = 0; i < 60 && areas.ActiveCount > 0; i++) harness.Tick(1);

                var diagnostics = MobaAcceptanceTraceExporter.CaptureTraceRecords(harness, "jhin_e_area_runtime_contract_world");
                var areaEntersBeforeDamage = 0;
                for (var i = 0; i < diagnostics.Length; i++)
                {
                    if (diagnostics[i].kind == "AreaEnter" && diagnostics[i].configId == EAreaId)
                        areaEntersBeforeDamage++;
                }
                Assert.Less(harness.GetActorHp(enemy), enemyHp,
                    $"The armed trap should damage an enemy already inside its area. activeAreas={areas.ActiveCount} areaEnters={areaEntersBeforeDamage}");
                var eTraceSummary = string.Empty;
                for (var i = 0; i < diagnostics.Length; i++)
                {
                    if (i > 0) eTraceSummary += "; ";
                    eTraceSummary += diagnostics[i].kind + ":" + diagnostics[i].configId + ":target=" + diagnostics[i].targetActorId;
                }
                Assert.IsTrue(harness.HasActorBuff(enemy, ESlowBuffId), "The first accepted enemy enter should apply Jhin E slow. trace=" + eTraceSummary);
                Assert.AreEqual(0, areas.ActiveCount, "The first accepted enemy enter should remove the actual runtime area.");

                var eHitCue = CapturePresentationCue(harness, EHitPresentationTemplateId);
                Assert.AreEqual((int)MobaPresentationCueStage.Executed, eHitCue.Stage,
                    "The armed E enter must publish its hit presentation at logical execution time.");
                Assert.AreEqual(EHitPresentationTemplateId, eHitCue.TemplateId,
                    "E hit presentation must resolve through the configured presentation template.");
                Assert.AreEqual(EHitPresentationTemplateId, eHitCue.VfxId,
                    "E hit presentation must carry the configured VFX template into the cue snapshot.");
                Assert.AreEqual(casterActorId, eHitCue.SourceActorId);
                Assert.AreEqual(enemy, eHitCue.TargetActorId);
                CollectionAssert.Contains(eHitCue.Targets, enemy,
                    "E hit presentation must be targeted at the enemy that entered the armed trap.");

                var records = diagnostics;
                var damages = 0;
                var areaEnters = 0;
                for (var i = 0; i < records.Length; i++)
                {
                    if (records[i].kind == "DamageApply" && records[i].configId == ESkillId && records[i].targetActorId == enemy)
                        damages++;
                    if (records[i].kind == "AreaEnter" && records[i].configId == EAreaId)
                        areaEnters++;
                }
                Assert.AreEqual(1, damages, "The one-shot trap should apply exactly one E damage event.");
                Assert.AreEqual(1, areaEnters, "The one-shot trap should dispatch exactly one accepted AreaEnter.");
            }
        }

        [Test]
        public void R_LogicHfsmOwnsAimShootFinalAndExitTransitions()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { RSkillId });
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_r_logic_hfsm_state_chain_world"))
            {
                harness.EnterGameAndWarmup(reason: "jhin r logic hfsm state chain");
                var actorId = harness.AssertPlayerActorBound();
                var combatRules = harness.World.Services.Resolve<MobaCombatRulesService>();
                var targetId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "r_hfsm_target", 9461, x: 0f, z: 5f);
                harness.SetScenarioActorAttribute(targetId, "max_hp", 100000f);
                harness.SetScenarioActorAttribute(targetId, "hp", 100000f);
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();

                Assert.IsTrue(skills.TryCastBySlot(actorId, 4, default, Vec3.Forward, 0).Success,
                    "R entry cast should succeed.");
                AssertStatePath(harness, actorId, "action/r.aiming", 30,
                    "R mode must be represented by the logic HFSM aiming state.");
                Assert.IsFalse(combatRules.CanMove(actorId),
                    "R mode's persistent Buff should hold the movement gate after the entry Pipeline ends.");

                for (var shot = 0; shot < 3; shot++)
                {
                    var baseline = harness.CaptureTraceBaseline();
                    Assert.IsTrue(skills.TryCastBySlot(actorId, 5, default, Vec3.Forward, targetId).Success,
                        "R regular shot " + (shot + 1) + " should be an independent cast.");
                    AssertStatePath(harness, actorId, "action/r.shooting", 30,
                        "Each regular R cast must enter the logic HFSM shooting state.");
                    harness.TickUntilTraceNodeAfter(baseline, MobaExecutionKind.DamageApply, RSkillId, 120,
                        "Each regular R cast must commit its projectile hit before the next HFSM phase is checked.");
                    harness.TickUntilSkillStops(actorId, 5, maxTicks: 120,
                        message: "R regular shot must complete as a short pipeline.");
                    Assert.IsFalse(combatRules.CanMove(actorId),
                        "R mode should keep movement blocked between independent shot Pipelines.");
                    if (shot < 2)
                        AssertStatePath(harness, actorId, "action/r.aiming", 30,
                            "Completing a regular R pipeline must return to persistent aiming.");
                }

                for (var i = 0; i < 60 && !harness.HasActorBuff(actorId, RFinalCandidateBuffId); i++)
                    harness.Tick(1);
                Assert.IsTrue(harness.HasActorBuff(actorId, RFinalCandidateBuffId),
                    "The third regular R shot must publish the final-shot modifier before the HFSM transition.");
                AssertStatePath(harness, actorId, "action/r.aiming", 30,
                    "The third regular shot must return to aiming while the final-shot slot is promoted.");
                Assert.IsTrue(skills.TryCastBySlot(actorId, 5, default, Vec3.Forward, targetId).Success,
                    "The promoted final R slot should cast successfully.");
                AssertStatePath(harness, actorId, "action/r.final_shot", 30,
                    "The final R cast must remain owned by the final-shot HFSM state.");
                harness.TickUntilSkillStops(actorId, 5, maxTicks: 120,
                    message: "R final shot must complete as an independent short pipeline.");
                for (var i = 0; i < 120 && harness.HasActorBuff(actorId, RModeBuffId); i++)
                    harness.Tick(1);
                Assert.IsFalse(harness.HasActorBuff(actorId, RModeBuffId),
                    "Final projectile exit must clear the persistent R mode.");
                Assert.IsTrue(combatRules.CanMove(actorId),
                    "R mode cleanup must remove its movement gate and restore movement.");
                AssertStatePath(harness, actorId, "action/idle", 30,
                    "Clearing R mode must return the logic HFSM to idle.");
            }
        }

        private static void AssertStatePath(
            MobaSkillConfigTestHarness harness,
            int actorId,
            string suffix,
            int maxTicks,
            string message)
        {
            for (var i = 0; i < maxTicks; i++)
            {
                if (harness.TryGetActorEntity(actorId, out var actor) && actor.hasCharacterHfsm &&
                    actor.characterHfsm.Runtime != null &&
                    actor.characterHfsm.Runtime.Action.Path.EndsWith(suffix, StringComparison.Ordinal))
                    return;
                harness.Tick(1);
            }

            var actual = "<missing>";
            if (harness.TryGetActorEntity(actorId, out var current) && current.hasCharacterHfsm &&
                current.characterHfsm.Runtime != null)
                actual = current.characterHfsm.Runtime.Action.Path;
            Assert.Fail(message + " actual=" + actual);
        }

        [Test]
        public void R_UsesThreeRegularShotsThenFinalShotWithFixedDirectionAndFinalDamageParameter()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { RSkillId });
            var skill = new HeroSkillSlotContract(4, RSkillId, RSkillId, RRegularLaunchTriggerId);
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_r_curtain_call_runtime_contract_world"))
            {
                harness.AssertSlotSkill(skill.Slot, RSkillId);
                harness.AssertSkillUsesCastFlow(RSkillId, RSkillId);
                Assert.IsTrue(harness.Config.TryGetSkill(RSkillId, out var rSkill), "Jhin R skill config should be published.");
                Assert.IsTrue(rSkill.AllowCastWithoutTarget, "Jhin R should accept a targetless directional cast.");
                Assert.AreEqual(RTargetQueryId, rSkill.RequiredTargetQueryId,
                    "Jhin R must use the authored target-sector query instead of an implicit cast-range fallback.");
                Assert.IsTrue(harness.Config.TryGetSearchQueryTemplate(RTargetQueryId, out var targetQuery),
                    "Jhin R target-sector query should be published.");
                Assert.AreEqual((int)SearchQueryExplicitTargetPolicy.PreferExplicitTarget, targetQuery.ExplicitTargetPolicy);
                Assert.AreEqual((int)SearchTargetProviderKind.EnemyTeam, targetQuery.Provider.Kind);
                Assert.AreEqual(1, targetQuery.MaxCount);
                Assert.AreEqual(1, targetQuery.Rules.Length);
                var sectorRule = targetQuery.Rules[0];
                Assert.AreEqual((int)SearchTargetRuleKind.SectorShape, sectorRule.Kind);
                Assert.AreEqual((int)SearchTargetPointKind.Caster, sectorRule.Center);
                Assert.AreEqual((int)SearchTargetPointKind.AimPosition, sectorRule.Forward);
                Assert.AreEqual(30f, sectorRule.Radius, 0.001f);
                Assert.AreEqual(5f, sectorRule.HalfAngleDeg, 0.001f);
                Assert.IsTrue(harness.Config.TryGetSkillLevelTable(30010502, out var finalShotLevelTable),
                    "Jhin R final-shot skill level table should be published.");
                Assert.AreEqual("finalDamage", finalShotLevelTable.ParamNames[4],
                    "Jhin R final shot must expose finalDamage as a configurable level parameter.");
                Assert.AreEqual(800f, finalShotLevelTable.Levels[0].Params[4], 0.001f,
                    "Jhin R finalDamage should come from the final-shot skill's level table.");

                Assert.IsTrue(harness.Config.TryGetProjectileLauncher(RLauncherId, out var launcher));
                Assert.AreEqual(1, launcher.CountPerShot, "Each Jhin R trigger should emit one projectile.");
                Assert.IsTrue(harness.Config.TryGetProjectile(RProjectileId, out var regularProjectile));
                Assert.IsTrue(harness.Config.TryGetProjectile(RFinalProjectileId, out var finalProjectile));
                Assert.AreEqual(ProjectileHitPolicyKind.Pierce, regularProjectile.HitPolicyKind);
                Assert.AreEqual(ProjectileHitPolicyKind.Pierce, finalProjectile.HitPolicyKind);
                Assert.AreEqual(4, regularProjectile.HitsRemaining, "R regular projectile should retain the configured finite pierce budget.");
                Assert.AreEqual(4, finalProjectile.HitsRemaining, "R final projectile should retain the configured finite pierce budget.");
                Assert.AreEqual(RRegularHitEffectId, regularProjectile.OnHitEffectId);
                Assert.AreEqual(RFinalHitEffectId, finalProjectile.OnHitEffectId);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, RRegularLaunchTriggerId, (int)TriggeringConstants.ShootProjectileId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, RRegularHitEffectId, (int)TriggeringConstants.GiveDamageId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, RFinalLaunchTriggerId, (int)TriggeringConstants.ShootProjectileId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(harness, RFinalHitEffectId, (int)TriggeringConstants.GiveDamageId.Value);

                Assert.IsTrue(harness.Config.TryGetSkillFlow(RSkillId, out var flow), "Jhin R aiming flow should be published.");
                Assert.AreEqual(2, flow.Phases.Count, "The R cast flow only enters the mode; it must not own repeated shots.");
                Assert.IsFalse(TryFindRepeatPhase(flow.Phases, out _), "R shot repetition must be driven by the HFSM and slot candidate, not by a Repeat phase.");
                Assert.IsTrue(harness.Config.TryGetSkillFlow(30010501, out var regularFlow), "Jhin regular R single-shot flow should be published.");
                Assert.IsTrue(ContainsTimelineEffect(regularFlow.Phases, RRegularLaunchTriggerId));
                Assert.IsTrue(harness.Config.TryGetSkillFlow(30010502, out var finalFlow), "Jhin final R single-shot flow should be published.");
                Assert.IsTrue(ContainsTimelineEffect(finalFlow.Phases, RFinalLaunchTriggerId));

                harness.EnterGameAndWarmup(reason: "jhin r curtain call runtime contract");
                var casterActorId = harness.AssertPlayerActorBound();
                var lineTarget = HeroSkillHeadlessContract.SpawnEnemyHero(harness, "line_target", 9401, x: 0f, z: 5f);
                var offAxisTarget = HeroSkillHeadlessContract.SpawnEnemyHero(harness, "off_axis_target", 9402, x: 3f, z: 5f);
                harness.SetScenarioActorAttribute(lineTarget, "hp", 5000f);
                harness.SetScenarioActorAttribute(offAxisTarget, "hp", 5000f);
                harness.Tick(1);
                var lineHp = harness.GetActorHp(lineTarget);
                var offAxisHp = harness.GetActorHp(offAxisTarget);

                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(casterActorId, skill.Slot, aimPos: default, aimDir: Vec3.Forward, targetActorId: 0);
                Assert.IsTrue(cast.Success, "Jhin R should cast without an explicit target. failReason=" + cast.FailReason);

                for (var i = 0; i < 30 && !harness.HasActorBuff(casterActorId, RModeBuffId); i++) harness.Tick(1);
                Assert.IsTrue(harness.HasActorBuff(casterActorId, RModeBuffId),
                    "Jhin R aiming flow should leave the persistent R mode buff behind.");
                harness.AssertSlotSkill(5, 30010501);

                var variables = harness.World.Services.Resolve<MobaGameplayVariableService>();
                var counterKey = unchecked(RRegularLaunchTriggerId * 100000 + casterActorId);
                var counterCues = harness.World.Services.Resolve<MobaPresentationCueSnapshotService>();
                var regularDamage = new float[3];
                var previousBaseline = harness.CaptureTraceBaseline();
                for (var i = 0; i < regularDamage.Length; i++)
                {
                    var shot = skills.TryCastBySlot(casterActorId, 5, aimPos: default, aimDir: Vec3.Forward, targetActorId: 0);
                    Assert.IsTrue(shot.Success, "Each R regular shot should be a separate slot-5 cast. failReason=" + shot.FailReason);
                    harness.TickUntilTraceNodeAfter(previousBaseline, MobaExecutionKind.DamageApply, RSkillId, 120,
                        "Jhin R regular single-shot flow should apply damage through its projectile hit trigger.");
                    var expectedCounter = i == regularDamage.Length - 1 ? 0d : i + 1d;
                    Assert.AreEqual(expectedCounter, variables.Get(counterKey),
                        $"Jhin R regular shot {i + 1} should advance the per-caster counter and consume it on the threshold shot.");
                    var hasCounterCue = counterCues.TryGetActiveCue(
                        $"gameplay-counter:{casterActorId}:{RRegularLaunchTriggerId}", out var counterCue);
                    if (expectedCounter > 0d)
                    {
                        Assert.IsTrue(hasCounterCue, $"Jhin R counter cue should be active after regular shot {i + 1}.");
                        Assert.AreEqual((int)expectedCounter, counterCue.Entry.StackCount,
                            $"Jhin R counter cue should display {expectedCounter}/3 after regular shot {i + 1}.");
                    }
                    else
                    {
                        Assert.IsFalse(hasCounterCue, "Jhin R counter cue should be removed when the final candidate is promoted.");
                    }
                    regularDamage[i] = lineHp - harness.GetActorHp(lineTarget);
                    lineHp = harness.GetActorHp(lineTarget);
                    previousBaseline = harness.CaptureTraceBaseline();
                }

                for (var i = 0; i < 30 && !harness.HasActorBuff(casterActorId, RFinalCandidateBuffId); i++) harness.Tick(1);
                Assert.IsTrue(harness.HasActorBuff(casterActorId, RFinalCandidateBuffId),
                    "The third R regular shot should promote the final slot candidate.");
                harness.AssertSlotSkill(5, 30010502);
                var finalCast = skills.TryCastBySlot(casterActorId, 5, aimPos: default, aimDir: Vec3.Forward, targetActorId: 0);
                Assert.IsTrue(finalCast.Success, "The promoted R final candidate should cast from slot 5. failReason=" + finalCast.FailReason);
                var finalProjectileEvents = string.Empty;
                var finalDamageObserved = false;
                for (var tick = 0; tick < 120; tick++)
                {
                    harness.Tick(1);
                    var projectileEvents = CaptureProjectileLifecycleSummary(harness, RFinalProjectileId);
                    if (!string.IsNullOrEmpty(projectileEvents))
                        finalProjectileEvents += (finalProjectileEvents.Length == 0 ? string.Empty : ";") + projectileEvents;

                    var trace = MobaAcceptanceTraceExporter.CaptureTraceRecords(
                        harness, "jhin_r_curtain_call_runtime_contract_world");
                    for (var i = 0; i < trace.Length; i++)
                    {
                        if (trace[i].nodeId > previousBaseline && trace[i].kind == "DamageApply" &&
                            trace[i].configId == RSkillId &&
                            trace[i].targetActorId == lineTarget)
                        {
                            finalDamageObserved = true;
                            break;
                        }
                    }
                    if (finalDamageObserved) break;
                }
                if (!finalDamageObserved)
                {
                    var trace = MobaAcceptanceTraceExporter.CaptureTraceRecords(
                        harness, "jhin_r_curtain_call_runtime_contract_world");
                    var finalProjectileHits = 0;
                    var finalHitExecutions = 0;
                    var finalLaunchesObserved = 0;
                    var recentTrace = string.Empty;
                    for (var i = 0; i < trace.Length; i++)
                    {
                        if (trace[i].nodeId > previousBaseline && trace[i].kind == "ProjectileHit" &&
                            trace[i].configId == RFinalProjectileId)
                            finalProjectileHits++;
                        if (trace[i].nodeId > previousBaseline && trace[i].kind == "EffectExecution" &&
                            trace[i].configId == RFinalHitEffectId)
                            finalHitExecutions++;
                        if (trace[i].nodeId > previousBaseline && trace[i].kind == "EffectExecution" &&
                            trace[i].configId == RFinalLaunchTriggerId)
                            finalLaunchesObserved++;
                        if (i >= trace.Length - 12)
                            recentTrace += (recentTrace.Length == 0 ? string.Empty : ";") +
                                trace[i].kind + ":" + trace[i].configId + "->" + trace[i].targetActorId;
                    }
                    Assert.Fail("Jhin R final shot did not apply damage. " +
                        $"castSuccess={finalCast.Success}, launchEffects={finalLaunchesObserved}, " +
                        $"finalProjectileHits={finalProjectileHits}, hitEffects={finalHitExecutions}, " +
                        $"projectileEvents={finalProjectileEvents}, rMode={harness.HasActorBuff(casterActorId, RModeBuffId)}, " +
                        $"finalCandidate={harness.HasActorBuff(casterActorId, RFinalCandidateBuffId)}, " +
                        $"finalPhase={harness.HasActorBuff(casterActorId, RFinalPhaseBuffId)}, recentTrace={recentTrace}");
                }

                Assert.IsTrue(harness.HasActorBuff(casterActorId, RModeBuffId),
                    "Jhin R mode must remain active after the final hit while its piercing projectile is still in flight.");
                for (var tick = 0; tick < 80 && harness.HasActorBuff(casterActorId, RModeBuffId); tick++)
                {
                    harness.Tick(1);
                    var projectileEvents = CaptureProjectileLifecycleSummary(harness, RFinalProjectileId);
                    if (!string.IsNullOrEmpty(projectileEvents))
                        finalProjectileEvents += (finalProjectileEvents.Length == 0 ? string.Empty : ";") + projectileEvents;
                }
                Assert.IsFalse(harness.HasActorBuff(casterActorId, RModeBuffId),
                    "Jhin R mode should end when the final projectile exits.");
                StringAssert.Contains("Exit:exit=1", finalProjectileEvents,
                    "The R cleanup must be driven by the final projectile exit event.");

                var finalDamage = lineHp - harness.GetActorHp(lineTarget);
                Assert.Greater(finalDamage, regularDamage[0], "Jhin R final shot must use the distinct finalDamage parameter.");
                Assert.AreEqual(offAxisHp, harness.GetActorHp(offAxisTarget), 0.001f,
                    "Jhin R must keep its authored fixed direction when no target is selected.");

                var records = MobaAcceptanceTraceExporter.CaptureTraceRecords(harness, "jhin_r_curtain_call_runtime_contract_world");
                var regularHits = 0;
                var finalHits = 0;
                var regularLaunches = 0;
                var finalLaunches = 0;
                var damageEvents = 0;
                for (var i = 0; i < records.Length; i++)
                {
                    var record = records[i];
                    if (record.kind == "ProjectileHit" && record.configId == RProjectileId) regularHits++;
                    if (record.kind == "ProjectileHit" && record.configId == RFinalProjectileId) finalHits++;
                    if (record.kind == "EffectExecution" && record.configId == RRegularLaunchTriggerId) regularLaunches++;
                    if (record.kind == "EffectExecution" && record.configId == RFinalLaunchTriggerId) finalLaunches++;
                    if (record.kind == "DamageApply" && record.configId == RSkillId) damageEvents++;
                }

                Assert.AreEqual(3, regularHits, "Jhin R should fire exactly three regular projectiles.");
                    Assert.AreEqual(1, finalHits, "Jhin R should fire exactly one final projectile.");
                Assert.AreEqual(3, regularLaunches, "The first three independent R shot flows should execute the regular launch trigger.");
                Assert.AreEqual(1, finalLaunches, "The fourth R shot should execute the final launch trigger.");
                Assert.AreEqual(4, damageEvents, "Each R projectile hit should resolve one skill damage event.");
                harness.TickUntilSkillStops(casterActorId, 5, maxTicks: 30,
                    message: "Jhin R final single-shot flow should complete independently.");
                for (var i = 0; i < 30 && harness.HasActorBuff(casterActorId, RModeBuffId); i++) harness.Tick(1);
                Assert.IsFalse(harness.HasActorBuff(casterActorId, RModeBuffId),
                    "Jhin R should clear the persistent mode after the final shot.");
                harness.AssertSlotSkill(5, BasicAttack1SkillId);
            }
        }

        [Test]
        public void R_ExplicitTargetInsideConfiguredSectorIsAccepted()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { RSkillId });
            var skill = new HeroSkillSlotContract(4, RSkillId, RSkillId, RRegularLaunchTriggerId);
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_r_explicit_target_inside_sector_world"))
            {
                harness.EnterGameAndWarmup(reason: "jhin r explicit target inside sector");
                var casterActorId = harness.AssertPlayerActorBound();
                // The authored query is a 30-unit sector with a 5-degree half angle.
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "inside_sector", 9451, x: 0.5f, z: 10f);
                harness.Tick(1);

                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(
                    casterActorId,
                    skill.Slot,
                    aimPos: default,
                    aimDir: Vec3.Forward,
                    targetActorId: targetActorId);

                Assert.IsTrue(cast.Success,
                    "An explicit target inside R's range and sector should be accepted. failReason=" + cast.FailReason);
            }
        }

        [Test]
        public void R_ExplicitTargetOutsideConfiguredSectorIsRejected()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { RSkillId });
            var skill = new HeroSkillSlotContract(4, RSkillId, RSkillId, RRegularLaunchTriggerId);
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_r_explicit_target_outside_sector_world"))
            {
                harness.EnterGameAndWarmup(reason: "jhin r explicit target outside sector");
                var casterActorId = harness.AssertPlayerActorBound();
                // About 11 degrees from Forward: inside the radius, outside the 5-degree sector.
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "outside_sector", 9452, x: 2f, z: 10f);
                harness.Tick(1);

                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(
                    casterActorId,
                    skill.Slot,
                    aimPos: default,
                    aimDir: Vec3.Forward,
                    targetActorId: targetActorId);

                Assert.IsFalse(cast.Success,
                    "An explicit target outside R's fixed sector must be rejected.");
                Assert.AreEqual("skill.cast.targetMissing", cast.Failure.Code,
                    "Sector rejection should be reported as an invalid required target.");
            }
        }

        [Test]
        public void R_ExplicitTargetOutsideConfiguredRangeIsRejected()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { RSkillId });
            var skill = new HeroSkillSlotContract(4, RSkillId, RSkillId, RRegularLaunchTriggerId);
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_r_explicit_target_outside_range_world"))
            {
                harness.EnterGameAndWarmup(reason: "jhin r explicit target outside range");
                var casterActorId = harness.AssertPlayerActorBound();
                // Keep the target on the frozen ray while exceeding the authored radius by 0.5.
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "outside_range", 9453, x: 0f, z: 30.5f);
                harness.Tick(1);

                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(
                    casterActorId,
                    skill.Slot,
                    aimPos: default,
                    aimDir: Vec3.Forward,
                    targetActorId: targetActorId);

                Assert.IsFalse(cast.Success,
                    "An explicit target beyond R's 30-unit range must be rejected.");
                Assert.AreEqual("skill.cast.targetMissing", cast.Failure.Code,
                    "Range rejection should be reported as an invalid required target.");
            }
        }

        [Test]
        public void R_ExplicitTargetSelectionUsesFrozenAimDirectionOverAimPosition()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { RSkillId });
            var skill = new HeroSkillSlotContract(4, RSkillId, RSkillId, RRegularLaunchTriggerId);
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_r_frozen_aim_direction_world"))
            {
                harness.EnterGameAndWarmup(reason: "jhin r frozen aim direction");
                var casterActorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "frozen_direction_target", 9454, x: 0f, z: 10f);
                harness.Tick(1);

                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                // aimPos deliberately points Right; the explicit aimDir is the authoritative cast direction.
                var cast = skills.TryCastBySlot(
                    casterActorId,
                    skill.Slot,
                    aimPos: new Vec3(10f, 0f, 0f),
                    aimDir: Vec3.Forward,
                    targetActorId: targetActorId);

                Assert.IsTrue(cast.Success,
                    "R target selection must use the frozen aimDir when aimPos and aimDir disagree. failReason=" + cast.FailReason);
            }
        }

        [Test]
        public void R_HitsApplyIndependentSlowForRegularAndFinalShotsWithRefreshAndExpiry()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { RSkillId });
            var skill = new HeroSkillSlotContract(4, RSkillId, RSkillId, RRegularLaunchTriggerId);
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_r_curtain_call_slow_runtime_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "jhin r slow runtime contract");
                var casterActorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, "slow_target", 9401, x: 0f, z: 5f);
                harness.Tick(1);

                // Keep the target alive through all four shots and seed a longer
                // instance so the first real hit proves RefreshDuration semantics.
                harness.SetScenarioActorAttribute(targetActorId, "max_hp", 100000f);
                var maxHp = harness.GetActorAttribute(targetActorId, BattleAttributeType.MAX_HP);
                harness.SetScenarioActorAttribute(targetActorId, "hp", maxHp);
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                EnterRMode(harness, skills, casterActorId, skill.Slot);

                var previousHp = harness.GetActorHp(targetActorId);
                var baseline = harness.CaptureTraceBaseline();
                var remainingAfterFirst = 0f;
                for (var shot = 0; shot < 4; shot++)
                {
                    CastRShot(harness, skills, casterActorId, baseline);
                    var currentHp = harness.GetActorHp(targetActorId);
                    Assert.Less(currentHp, previousHp, "Each R shot should still apply damage while validating the slow.");
                    previousHp = currentHp;
                    // DamageApply is traced at the first action in the trigger;
                    // the following add_buff action is committed by the trigger
                    // runner on a later simulation frame. Wait for that commit
                    // instead of assuming a fixed number of frames, since the
                    // final projectile uses a separate hit trigger.
                    TickUntilActorBuff(harness, targetActorId, RSlowBuffId, maxTicks: 30,
                        message: "Every regular and final R hit should apply the dedicated Curtain Call slow. shot=" + shot);

                    Assert.IsTrue(harness.HasActorBuff(targetActorId, RSlowBuffId),
                        "Every regular and final R hit should apply the dedicated Curtain Call slow. shot=" + shot);
                    Assert.IsTrue(harness.TryGetActorBuffStackCount(targetActorId, RSlowBuffId, out var stacks));
                    Assert.AreEqual(1, stacks, "Curtain Call slow must refresh as one instance instead of stacking.");
                    Assert.IsTrue(harness.TryGetActorBuffRemainingSeconds(targetActorId, RSlowBuffId, out var remaining));
                    Assert.Greater(remaining, 0.3f, "Curtain Call slow should be live after the hit.");
                    Assert.LessOrEqual(remaining, 0.55f, "Curtain Call slow should use its authored 500ms duration after refresh.");
                    if (shot == 0) remainingAfterFirst = remaining;

                    baseline = harness.CaptureTraceBaseline();
                    if (shot == 2)
                        TickUntilActorBuff(harness, casterActorId, RFinalCandidateBuffId, maxTicks: 30,
                            message: "The third independent R shot should promote the final candidate.");
                }

                Assert.Greater(remainingAfterFirst, 0f);
                var records = MobaAcceptanceTraceExporter.CaptureTraceRecords(harness, "jhin_r_curtain_call_slow_runtime_contract_world");
                var finalHitEffects = 0;
                for (var i = 0; i < records.Length; i++)
                    if (records[i].kind == "EffectExecution" && records[i].configId == RFinalHitEffectId)
                        finalHitEffects++;
                Assert.AreEqual(1, finalHitEffects, "The final R hit must execute its distinct damage-and-slow trigger.");

                harness.TickMilliseconds(600);
                Assert.IsFalse(harness.HasActorBuff(targetActorId, RSlowBuffId),
                    "Curtain Call slow should be removed by its 500ms lifetime after the final hit.");
            }
        }

        [Test]
        public void R_SlowRespectsConfigurableSlowImmunityWithoutBlockingDamage()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { RSkillId });
            var skill = new HeroSkillSlotContract(4, RSkillId, RSkillId, RRegularLaunchTriggerId);
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_r_slow_immunity_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "jhin r slow immunity contract");
                var casterActorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, "slow_immune_target", 9411, x: 0f, z: 5f);
                harness.Tick(1);

                harness.SetScenarioActorAttribute(targetActorId, "max_hp", 100000f);
                var maxHp = harness.GetActorAttribute(targetActorId, BattleAttributeType.MAX_HP);
                harness.SetScenarioActorAttribute(targetActorId, "hp", maxHp);

                Assert.IsTrue(MobaGameplayTagCatalog.TryGet("State.Immunity.Slow", out var slowImmunityTag),
                    "The published MOBA tag catalog should contain State.Immunity.Slow.");
                var immunity = new ScenarioTagContinuous(targetActorId, slowImmunityTag);
                var continuousManager = harness.World.Services.Resolve<IContinuousManager>();
                Assert.IsTrue(continuousManager.Register(immunity), "The slow-immunity source should register with the production continuous manager.");
                Assert.IsTrue(continuousManager.TryActivate(immunity), "The slow-immunity source should activate through the production lifecycle.");
                var effectiveTags = harness.World.Services.Resolve<IMobaEffectiveTagQueryService>();
                Assert.IsTrue(effectiveTags.HasTag(targetActorId, slowImmunityTag), "The target should have effective slow immunity before R arrives.");

                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                EnterRMode(harness, skills, casterActorId, skill.Slot);

                var hpBeforeFirst = harness.GetActorHp(targetActorId);
                CastRShot(harness, skills, casterActorId, harness.CaptureTraceBaseline());
                Assert.Less(harness.GetActorHp(targetActorId), hpBeforeFirst,
                    "Slow immunity must not block R damage.");
                harness.Tick(20);
                Assert.IsFalse(harness.HasActorBuff(targetActorId, RSlowBuffId),
                    "State.Immunity.Slow must block the R slow buff through the shared template gate.");

                // Removing the immunity must restore ordinary slow application for
                // the same generic R hit path, without a Jhin-specific branch.
                continuousManager.TryEnd(immunity, ContinuousEndReason.CleanedUp);
                Assert.IsFalse(effectiveTags.HasTag(targetActorId, slowImmunityTag),
                    "Slow immunity should be absent after its source ends.");
                var baseline = harness.CaptureTraceBaseline();
                CastRShot(harness, skills, casterActorId, baseline);
                TickUntilActorBuff(harness, targetActorId, RSlowBuffId, maxTicks: 30,
                    "The next R shot should apply its configured slow after immunity is removed.");
                Assert.IsTrue(harness.HasActorBuff(targetActorId, RSlowBuffId),
                    "Removing slow immunity should restore the shared slow application contract.");
            }
        }

        [Test]
        public void R_SlowImmunityBlocksRepeatedHitsWithoutBlockingDamage()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { RSkillId });
            var skill = new HeroSkillSlotContract(4, RSkillId, RSkillId, RRegularLaunchTriggerId);
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_r_slow_immunity_repeated_hits_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "jhin r repeated slow immunity contract");
                var casterActorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, "repeated_slow_immune_target", 9412, x: 0f, z: 5f);
                harness.Tick(1);

                harness.SetScenarioActorAttribute(targetActorId, "max_hp", 100000f);
                var maxHp = harness.GetActorAttribute(targetActorId, BattleAttributeType.MAX_HP);
                harness.SetScenarioActorAttribute(targetActorId, "hp", maxHp);

                Assert.IsTrue(MobaGameplayTagCatalog.TryGet("State.Immunity.Slow", out var slowImmunityTag),
                    "The published MOBA tag catalog should contain State.Immunity.Slow.");
                var immunity = new ScenarioTagContinuous(targetActorId, slowImmunityTag);
                var continuousManager = harness.World.Services.Resolve<IContinuousManager>();
                Assert.IsTrue(continuousManager.Register(immunity), "The slow-immunity source should register with the production continuous manager.");
                Assert.IsTrue(continuousManager.TryActivate(immunity), "The slow-immunity source should activate through the production lifecycle.");

                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                EnterRMode(harness, skills, casterActorId, skill.Slot);

                var hpBeforeR = harness.GetActorHp(targetActorId);
                var baseline = harness.CaptureTraceBaseline();
                for (var shot = 0; shot < 3; shot++)
                {
                    CastRShot(harness, skills, casterActorId, baseline);
                    baseline = harness.CaptureTraceBaseline();
                }
                TickUntilActorBuff(harness, casterActorId, RFinalCandidateBuffId, maxTicks: 30,
                    message: "The third independent R shot should promote the final candidate.");
                CastRShot(harness, skills, casterActorId, baseline);
                var hpAfterR = harness.GetActorHp(targetActorId);
                Assert.Less(hpAfterR, hpBeforeR,
                    "Slow immunity must not block the four-shot R damage sequence.");

                var records = MobaAcceptanceTraceExporter.CaptureTraceRecords(
                    harness, "jhin_r_slow_immunity_repeated_hits_contract_world");
                var damageEvents = 0;
                var slowApplications = 0;
                for (var i = 0; i < records.Length; i++)
                {
                    var record = records[i];
                    if (record.kind == "DamageApply" && record.configId == RSkillId && record.targetActorId == targetActorId)
                        damageEvents++;
                    if (record.kind == "BuffApply" && record.configId == RSlowBuffId && record.targetActorId == targetActorId)
                        slowApplications++;
                }

                Assert.AreEqual(4, damageEvents,
                    "All four R shots should damage the slow-immune target.");
                Assert.AreEqual(0, slowApplications,
                    "Repeated R hits must not create or refresh the slow while immunity is active.");
                Assert.IsFalse(harness.HasActorBuff(targetActorId, RSlowBuffId),
                    "No R slow instance should remain on the slow-immune target.");

                Assert.IsTrue(continuousManager.TryEnd(immunity, ContinuousEndReason.CleanedUp),
                    "The slow-immunity source should end cleanly after the repeated-hit contract.");
            }
        }

        [Test]
        public void R_ExistingSlowSurvivesImmunityActivationAndExpiresNaturally()
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { RSkillId });
            var skill = new HeroSkillSlotContract(4, RSkillId, RSkillId, RRegularLaunchTriggerId);
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, "jhin_r_existing_slow_immunity_transition_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "jhin r existing slow immunity transition contract");
                var casterActorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, "existing_slow_target", 9413, x: 0f, z: 5f);
                harness.Tick(1);

                harness.SetScenarioActorAttribute(targetActorId, "max_hp", 100000f);
                var maxHp = harness.GetActorAttribute(targetActorId, BattleAttributeType.MAX_HP);
                harness.SetScenarioActorAttribute(targetActorId, "hp", maxHp);

                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                EnterRMode(harness, skills, casterActorId, skill.Slot);
                var baseline = harness.CaptureTraceBaseline();
                CastRShot(harness, skills, casterActorId, baseline);
                TickUntilActorBuff(harness, targetActorId, RSlowBuffId, maxTicks: 30,
                    "The first R hit should create a real slow before immunity is enabled.");
                Assert.IsTrue(harness.TryGetActorBuffRemainingSeconds(targetActorId, RSlowBuffId, out var remainingBeforeImmunity),
                    "The existing R slow should expose its remaining lifetime.");
                Assert.Greater(remainingBeforeImmunity, 0.3f,
                    "The existing slow should still have meaningful lifetime when immunity is enabled.");

                Assert.IsTrue(MobaGameplayTagCatalog.TryGet("State.Immunity.Slow", out var slowImmunityTag),
                    "The published MOBA tag catalog should contain State.Immunity.Slow.");
                var immunity = new ScenarioTagContinuous(targetActorId, slowImmunityTag);
                var continuousManager = harness.World.Services.Resolve<IContinuousManager>();
                Assert.IsTrue(continuousManager.Register(immunity), "The slow-immunity source should register with the production continuous manager.");
                Assert.IsTrue(continuousManager.TryActivate(immunity), "The slow-immunity source should activate through the production lifecycle.");
                Assert.IsTrue(harness.HasActorBuff(targetActorId, RSlowBuffId),
                    "Enabling slow immunity must not remove an already active slow under the activation-only contract.");

                harness.Tick(5);
                Assert.IsTrue(harness.HasActorBuff(targetActorId, RSlowBuffId),
                    "The existing slow should remain active briefly after immunity is enabled.");
                Assert.IsTrue(harness.TryGetActorBuffRemainingSeconds(targetActorId, RSlowBuffId, out var remainingAfterImmunity),
                    "The existing slow should continue exposing its remaining lifetime.");
                Assert.Less(remainingAfterImmunity, remainingBeforeImmunity,
                    "The existing slow should continue counting down instead of being refreshed or replaced by immunity activation.");

                harness.TickMilliseconds(600);
                Assert.IsFalse(harness.HasActorBuff(targetActorId, RSlowBuffId),
                    "An existing slow should expire at its authored lifetime while immunity is active.");
                Assert.IsTrue(continuousManager.TryEnd(immunity, ContinuousEndReason.CleanedUp),
                    "The slow-immunity source should end cleanly after the lifecycle transition contract.");
            }
        }

        [Test]
        public void R_LowHealthBonusReadsHpBeforeDamageForRegularAndFinalShots()
        {
            var normal = CaptureRDamageAtHealthRatio(0.75f, "jhin_r_health_percent_normal_contract_world");
            var low = CaptureRDamageAtHealthRatio(0.40f, "jhin_r_health_percent_low_contract_world");

            Assert.AreEqual(4, normal.Length, "The normal-health R cast should resolve three regular shots and one final shot.");
            Assert.AreEqual(4, low.Length, "The low-health R cast should resolve three regular shots and one final shot.");
            Assert.AreEqual(normal[0], normal[1], 0.001f, "Targets at 75% health should not receive the low-health bonus on regular shots.");
            Assert.AreEqual(normal[1], normal[2], 0.001f, "Targets at 75% health should not receive the low-health bonus on regular shots.");
            Assert.Greater(low[0], normal[0], "A target below 50% health should receive the configured missing-health bonus on a regular R shot.");
            Assert.Greater(low[3], normal[3], "A target below 50% health should receive the configured missing-health bonus on the final R shot.");
        }

        private static float[] CaptureRDamageAtHealthRatio(float healthRatio, string worldName)
        {
            var hero = new HeroSkillContract("Jhin", HeroId, HeroId, new[] { RSkillId });
            var skill = new HeroSkillSlotContract(4, RSkillId, RSkillId, RRegularLaunchTriggerId);
            using (var harness = HeroSkillHeadlessContract.CreateHarness(hero, worldName))
            {
                harness.EnterGameAndWarmup(reason: worldName);
                var casterActorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, "health_target", 9450, x: 0f, z: 5f);
                harness.Tick(1);

                // Keep the target alive through all four shots so the test observes
                // the before-calc condition independently for regular and final R.
                harness.SetScenarioActorAttribute(targetActorId, "max_hp", 10000f);
                var maxHp = harness.GetActorAttribute(targetActorId, BattleAttributeType.MAX_HP);
                harness.SetScenarioActorAttribute(targetActorId, "hp", maxHp * healthRatio);
                var previousHp = harness.GetActorHp(targetActorId);

                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                EnterRMode(harness, skills, casterActorId, skill.Slot);

                var damage = new float[4];
                var baseline = harness.CaptureTraceBaseline();
                for (var i = 0; i < damage.Length; i++)
                {
                    CastRShot(harness, skills, casterActorId, baseline);
                    var currentHp = harness.GetActorHp(targetActorId);
                    damage[i] = previousHp - currentHp;
                    Assert.Greater(damage[i], 0f, "Each R shot should apply positive damage while the target remains alive.");
                    previousHp = currentHp;
                    baseline = harness.CaptureTraceBaseline();
                    if (i == 2)
                        TickUntilActorBuff(harness, casterActorId, RFinalCandidateBuffId, maxTicks: 30,
                            message: "The third independent R shot should promote the final candidate.");
                }

                return damage;
            }
        }

        private static void EnterRMode(MobaSkillConfigTestHarness harness, SkillCastCoordinator skills, int casterActorId, int rSlot)
        {
            var cast = skills.TryCastBySlot(casterActorId, rSlot, aimPos: default, aimDir: Vec3.Forward, targetActorId: 0);
            Assert.IsTrue(cast.Success, "Jhin R should enter aiming mode. failReason=" + cast.FailReason);
            for (var i = 0; i < 30 && !harness.HasActorBuff(casterActorId, RModeBuffId); i++) harness.Tick(1);
            Assert.IsTrue(harness.HasActorBuff(casterActorId, RModeBuffId), "Jhin R aiming mode should remain active between single-shot flows.");
        }

        private static void CastRShot(MobaSkillConfigTestHarness harness, SkillCastCoordinator skills, int casterActorId, long baseline)
        {
            var shot = skills.TryCastBySlot(casterActorId, 5, aimPos: default, aimDir: Vec3.Forward, targetActorId: 0);
            Assert.IsTrue(shot.Success, "Each R shot should be an independent slot-5 cast. failReason=" + shot.FailReason);
            harness.TickUntilTraceNodeAfter(baseline, MobaExecutionKind.DamageApply, RSkillId, 120,
                "The independent R shot should apply damage through its projectile hit trigger.");
        }

        private static bool TryFindRepeatPhase(IReadOnlyList<SkillPhaseDTO> phases, out SkillRepeatPhaseDTO repeat)
        {
            repeat = null;
            if (phases == null) return false;
            for (var i = 0; i < phases.Count; i++)
            {
                var phase = phases[i];
                if (phase == null) continue;
                if (phase.Repeat != null)
                {
                    repeat = phase.Repeat;
                    return true;
                }
                if (TryFindRepeatPhase(phase.Children, out repeat)) return true;
            }
            return false;
        }

        private static bool ContainsTimelineEffect(SkillPhaseDTO phase, int effectId)
        {
            if (phase == null) return false;
            if (phase.Timeline?.Events != null)
            {
                for (var i = 0; i < phase.Timeline.Events.Length; i++)
                    if (phase.Timeline.Events[i].EffectId == effectId) return true;
            }
            if (phase.Repeat?.Phase != null && ContainsTimelineEffect(phase.Repeat.Phase, effectId)) return true;
            return ContainsTimelineEffect(phase.Children, effectId);
        }

        private static bool ContainsTimelineEffect(IReadOnlyList<SkillPhaseDTO> phases, int effectId)
        {
            if (phases == null) return false;
            for (var i = 0; i < phases.Count; i++)
                if (ContainsTimelineEffect(phases[i], effectId)) return true;
            return false;
        }

        private sealed class ScenarioTagContinuous : MobaContinuousRuntimeBase, IMobaContinuousTagConfig
        {
            private readonly ScenarioTagConfig _config;

            public ScenarioTagContinuous(int ownerActorId, GameplayTag tag)
            {
                var tags = new GameplayTagContainer(tag);
                TagRequirements = new ContinuousTagRequirements { ApplicationTags = tags };
                _config = new ScenarioTagConfig(ownerActorId, TagRequirements);
            }

            public override IContinuousConfig Config => _config;
            public ContinuousTagRequirements TagRequirements { get; }
        }

        private sealed class ScenarioTagConfig : IContinuousConfig, IMobaContinuousTagConfig
        {
            public ScenarioTagConfig(int ownerActorId, ContinuousTagRequirements requirements)
            {
                OwnerId = ownerActorId;
                TagRequirements = requirements;
            }

            public string Id => "jhin_acceptance_control_immune";
            public long OwnerId { get; }
            public bool CanBeInterrupted => false;
            public ContinuousTagRequirements TagRequirements { get; }
        }

        private static string CaptureProjectileLifecycleSummary(MobaSkillConfigTestHarness harness, int projectileId = 0)
        {
            if (projectileId <= 0) projectileId = ProjectileId;
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
                    if (entries[j].TemplateId != projectileId) continue;
                    if (summary.Length > 0) summary += ";";
                    summary += ((ProjectileEventKind)entries[j].Kind).ToString() + ":exit=" + entries[j].ExitReason + ":collider=" + entries[j].HitCollider +
                               ":pos=(" + entries[j].X + "," + entries[j].Y + "," + entries[j].Z + ")";
                }
            }
            return summary;
        }

        private static MobaProjectileEventSnapshotEntry TickUntilProjectileSpawnSnapshot(
            MobaSkillConfigTestHarness harness, int templateId, int maxTicks)
        {
            for (var i = 0; i <= maxTicks; i++)
            {
                if (TryCollectProjectileSnapshot(harness, templateId, (int)ProjectileEventKind.Spawn, 0, out var entry)) return entry;
                if (i < maxTicks) harness.Tick(1);
            }

            Assert.Fail("Jhin E projectile spawn snapshot missing. templateId=" + templateId);
            return default;
        }

        private static MobaProjectileEventSnapshotEntry TickUntilProjectileExitSnapshot(
            MobaSkillConfigTestHarness harness, int projectileId, int maxTicks)
        {
            for (var i = 0; i <= maxTicks; i++)
            {
                if (TryCollectProjectileSnapshot(harness, 0, (int)ProjectileEventKind.Exit, projectileId, out var entry)) return entry;
                if (i < maxTicks) harness.Tick(1);
            }

            Assert.Fail("Jhin E projectile exit snapshot missing. projectileId=" + projectileId);
            return default;
        }

        private static bool TryCollectProjectileSnapshot(
            MobaSkillConfigTestHarness harness, int templateId, int kind, int projectileId,
            out MobaProjectileEventSnapshotEntry result)
        {
            result = default;
            var provider = harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>();
            var snapshots = new List<WorldStateSnapshot>(16);
            provider.CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
            for (var i = 0; i < snapshots.Count; i++)
            {
                if (snapshots[i].OpCode != MobaOpCodes.Snapshot.ProjectileEvent) continue;
                var entries = MobaProjectileEventSnapshotCodec.Deserialize(snapshots[i].Payload);
                for (var j = 0; j < entries.Length; j++)
                {
                    if (entries[j].Kind != kind) continue;
                    if (templateId > 0 && entries[j].TemplateId != templateId) continue;
                    if (projectileId > 0 && entries[j].ProjectileId != projectileId) continue;
                    result = entries[j];
                    return true;
                }
            }

            return false;
        }

        private static MobaPresentationCueSnapshotEntry CapturePresentationCue(
            MobaSkillConfigTestHarness harness,
            int templateId)
        {
            var provider = harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>();
            var snapshots = new List<WorldStateSnapshot>(8);
            provider.CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
            for (var i = 0; i < snapshots.Count; i++)
            {
                if (snapshots[i].OpCode != MobaOpCodes.Snapshot.PresentationCue) continue;
                var entries = MobaPresentationCueSnapshotCodec.Deserialize(snapshots[i].Payload);
                for (var j = 0; j < entries.Length; j++)
                    if (entries[j].TemplateId == templateId) return entries[j];
            }

            Assert.Fail($"Presentation cue snapshot missing. templateId={templateId}");
            return default;
        }

        private static void TickUntilActorBuff(MobaSkillConfigTestHarness harness, int actorId, int buffId, int maxTicks, string message)
        {
            for (var i = 0; i < maxTicks; i++)
            {
                if (harness.HasActorBuff(actorId, buffId)) return;
                harness.Tick(1);
            }

            Assert.Fail(message);
        }

        private static string CaptureCollisionSummary(MobaSkillConfigTestHarness harness)
        {
            return CaptureCollisionSummary(harness.World);
        }

        private static string CaptureCollisionSummary(AbilityKit.Ability.World.Abstractions.IWorld world)
        {
            var registry = world.Services.Resolve<MobaActorRegistry>();
            var summary = string.Empty;
            foreach (var pair in registry.Entries)
            {
                var actor = pair.Value;
                if (actor == null || !actor.hasActorId || !actor.hasTransform) continue;
                var position = actor.transform.Value.Position;
                if (summary.Length > 0) summary += ";";
                summary += "actor=" + actor.actorId.Value + ",team=" + (actor.hasTeam ? actor.team.Value.ToString() : "none") +
                           ",collider=" + (actor.hasCollisionId ? actor.collisionId.Value.Value.ToString() : "none") +
                           ",pos=(" + position.X + "," + position.Y + "," + position.Z + ")";
            }

            var collisionWorld = world.Services.Resolve<AbilityKit.Combat.Collision.ICollisionService>().World;
            if (collisionWorld is AbilityKit.Combat.Collision.ICollisionWorldDebugView debugView)
            {
                var shapes = new List<AbilityKit.Combat.Collision.CollisionWorldDebugShape>();
                debugView.CopyWorldShapes(shapes);
                for (var i = 0; i < shapes.Count; i++)
                {
                    var shape = shapes[i];
                    var center = shape.WorldShape.Type switch
                    {
                        AbilityKit.Core.Mathematics.ColliderShapeType.Sphere => shape.WorldShape.Sphere.Center,
                        AbilityKit.Core.Mathematics.ColliderShapeType.Aabb => shape.WorldShape.Aabb.Center,
                        AbilityKit.Core.Mathematics.ColliderShapeType.Capsule => shape.WorldShape.Capsule.Center,
                        AbilityKit.Core.Mathematics.ColliderShapeType.OBB => shape.WorldShape.Obb.Center,
                        _ => Vec3.Zero,
                    };
                    if (summary.Length > 0) summary += ";";
                    summary += "collision=" + shape.Id.Value + ",layer=" + shape.LayerId + ",type=" + shape.WorldShape.Type +
                               ",center=(" + center.X + "," + center.Y + "," + center.Z + ")";
                }
            }

            return summary;
        }
    }
}

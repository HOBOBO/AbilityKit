using AbilityKit.Core.Mathematics;
using AbilityKit.Ability.Host;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Battle.View;
using AbilityKit.Game.Battle.View.Lib.Skill;
using AbilityKit.Game.Battle.Presentation.Features.Loading;
using AbilityKit.Demo.Moba.View.Config;
using UnityEditor.SceneManagement;
using AbilityKit.Deterministic;
using AbilityKit.Demo.Moba;
using AbilityKit.Core.Eventing;
using AbilityKit.Demo.Moba.Events.Unit;
using AbilityKit.Demo.Moba.Events.Summon;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Behavior;
using AbilityKit.Demo.Moba.Gameplay;
using AbilityKit.Demo.Moba.Gameplay.Triggering;
using AbilityKit.Demo.Moba.Systems;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Protocol.Moba;
using AbilityKit.Trace;
using AbilityKit.Triggering.Eventing;
using NUnit.Framework;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class AnnieSkillAcceptanceTests : MobaAcceptanceTestBase
    {
        private const int HeroId = 3003;
        private const int PassiveId = 30030000;
        private const int BasicAttackSkillId = 30030101;
        private const int QSkillId = 30030201;
        private const int WSkillId = 30030301;
        private const int ESkillId = 30030401;
        private const int RSkillId = 30030501;
        private const int TibbersSummonId = 30030502;
        private const int EMoveSpeedBuffId = 30030402;
        private const int PassiveTriggerId = 30030002;
        private const int CounterKeyId = 30030002;
        private const int AnnieQPreviewVfxId = 90033001;
        private const int AnnieWPreviewVfxId = 90033002;
        private const int AnnieStunPresentationTemplateId = 90033003;
        private const int AnnieEPresentationTemplateId = 90033004;
        private const int AnnieRPreviewVfxId = 90033005;

        private static readonly HeroSkillContract Annie = new HeroSkillContract(
            "Annie", HeroId, HeroId, new[] { BasicAttackSkillId, QSkillId, WSkillId, ESkillId, RSkillId });

        [Test]
        public void PresentationMappings_ShouldUseAnnieOwnedVfxAndBuffTemplates()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(
                Annie, "annie_presentation_mapping_runtime_contract_world"))
            {
                Assert.AreEqual(AnnieQPreviewVfxId, GetSkillPreviewVfx(harness, QSkillId));
                Assert.AreEqual(AnnieWPreviewVfxId, GetSkillPreviewVfx(harness, WSkillId));
                Assert.AreEqual(AnnieEPresentationTemplateId, GetSkillPreviewVfx(harness, ESkillId));
                Assert.AreEqual(AnnieRPreviewVfxId, GetSkillPreviewVfx(harness, RSkillId));

                Assert.IsTrue(harness.Config.TryGetBuff(30030211, out var stun));
                Assert.AreEqual(AnnieStunPresentationTemplateId, stun.PresentationTemplateId,
                    "Annie's passive stun Buff must resolve the dedicated Annie hit presentation.");
                Assert.IsTrue(harness.Config.TryGetBuff(EMoveSpeedBuffId, out var shield));
                Assert.AreEqual(AnnieEPresentationTemplateId, shield.PresentationTemplateId,
                    "Annie E's shield/movement Buff must resolve the dedicated Annie shield presentation.");

                var templates = harness.Config.GetTable<PresentationTemplateMO>();
                foreach (var templateId in new[]
                         {
                             AnnieQPreviewVfxId, AnnieWPreviewVfxId,
                             AnnieStunPresentationTemplateId,
                             AnnieEPresentationTemplateId, AnnieRPreviewVfxId,
                         })
                {
                    Assert.IsTrue(templates.TryGet(templateId, out var template),
                        "Annie presentation template is missing: " + templateId);
                    Assert.AreEqual(templateId, template.AssetId,
                        "Annie presentation template must point at its same-id VFX asset.");
                }
            }
        }

        [UnityTest]
        public IEnumerator LocalBattleScenario_AllFourHudSlotsSubmitProductionSkillInput()
        {
            AnnieHudInputFixture[] huds = new AnnieHudInputFixture[4];
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
                    flow.EnterBattle(new TestBattleBootstrapper());

                yield return TickUntil(flow, () => flow.CurrentBattlePhase == MobaBattleState.InMatch, 18000,
                    () => "The local MOBA battle did not reach InMatch. root=" + flow.CurrentPhase +
                          ", entry=" + flow.BattleEntryState + ", battle=" + flow.CurrentBattlePhase + "." +
                          DescribeAssetLoadState(flow));
                Assert.IsTrue(entry.TryGet(out BattleContext ctx),
                    "BattleContext must be attached after entering battle.");

                var plan = ctx.Plan;
                var playerId = BattleInputSessionIdentity.ResolvePlayerId((IBattleInputSessionIdentityPort)ctx);
                var worldId = BattleInputSessionIdentity.ResolveWorldId(in plan);
                var frame = SessionSimRuntimeTuning.ResolveInputSubmitFrame(ctx.LastFrame, in plan);
                var replace = BattleInputCommandFactory.CreateDebugReplaceHero(frame, playerId, HeroId);
                Assert.IsTrue(new BattleInputSubmitter(ctx.InputRuntime, playerId, worldId).Submit(in replace),
                    "Annie selection must use the production local-player input port.");

                yield return TickUntil(flow,
                    () => IsPlayerUsingHero(ctx, "p1", HeroId) &&
                          TryGetPlayerActor(ctx, "p1", out _, out var actor) &&
                          actor.hasSkillLoadout && actor.hasTransform,
                    240, "The local player did not finish replacing its loadout with Annie.");
                Assert.IsTrue(ctx.Session.TryGetWorld(out var world), "Local battle should expose its logic world.");
                var config = world.Services.Resolve<MobaConfigDatabase>();
                Assert.IsNotNull(config, "The local Annie battle must use published MOBA configuration.");
                Assert.IsTrue(config.TryGetSkill(QSkillId, out var qSkill));
                Assert.IsTrue(config.TryGetSkill(WSkillId, out var wSkill));
                Assert.IsTrue(config.TryGetSkill(ESkillId, out var eSkill));
                Assert.IsTrue(config.TryGetSkill(RSkillId, out var rSkill));

                var specs = new[]
                {
                    ResolveHudSpec(config, qSkill, SkillAimIndicatorShape.SelfCircle),
                    ResolveHudSpec(config, wSkill, SkillAimIndicatorShape.FanArea),
                    ResolveHudSpec(config, eSkill, SkillAimIndicatorShape.Hidden),
                    ResolveHudSpec(config, rSkill, SkillAimIndicatorShape.TargetCircle),
                };
                var templates = new SkillButtonTemplateMO[4];
                var skills = new[] { qSkill, wSkill, eSkill, rSkill };
                for (var i = 0; i < skills.Length; i++)
                    Assert.IsTrue(config.TryGetSkillButtonTemplate(skills[i].SkillButtonTemplateId, out templates[i]));
                Assert.AreEqual(6f, specs[0].SelfRadius, 0.001f);
                Assert.AreEqual(90f, specs[1].FanAngleDegrees, 0.001f);
                Assert.AreEqual(5.5f, specs[1].FanRadius, 0.001f);
                Assert.IsFalse(specs[2].EnableAim, "Annie E is a self-cast shield and should use a normal click.");
                Assert.IsTrue(specs[3].UsesTargetPoint, "Annie R must submit a target-point aim.");
                Assert.AreEqual(4.5f, specs[3].Radius, 0.001f);

                var inputFeature = entry.Get<BattleInputFeature>();
                Assert.IsNotNull(inputFeature, "The composed local battle must register BattleInputFeature.");
                for (var i = 0; i < huds.Length; i++)
                    huds[i] = AnnieHudInputFixture.Create(ctx, specs, templates, i + 1);

                for (var slot = 1; slot <= 4; slot++)
                {
                    var before = inputFeature.SkillSubmitSuccessCount;
                    var button = huds[slot - 1];
                    var center = button.ButtonScreenPosition;
                    if (slot == 2 || slot == 4)
                    {
                        button.PointerDown(center);
                        button.Drag(center + new Vector2(0f, 120f));
                        button.PointerUp(center + new Vector2(0f, 200f));
                    }
                    else
                    {
                        button.PointerDown(center);
                        button.PointerUp(center);
                    }

                    yield return TickUntil(flow,
                        () => inputFeature.SkillSubmitSuccessCount > before,
                        12, "Annie HUD slot " + slot + " did not reach BattleInputFeature.");
                    Assert.AreEqual(before + 1, inputFeature.SkillSubmitSuccessCount,
                        "One Annie HUD gesture must submit exactly one skill command for slot " + slot + ".");
                    if (slot == 2 || slot == 4)
                        Assert.IsFalse(BattleHudInputSource.TryConsumeSkillAimSubmit(ctx, out _),
                            "BattleInputFeature must consume the aimed skill snapshot once for slot " + slot + ".");
                    yield return null;
                }
            }
            finally
            {
                foreach (var hud in huds) hud?.Dispose();
                if (GameEntry.IsInitialized && GameEntry.Instance != null)
                    UnityEngine.Object.DestroyImmediate(GameEntry.Instance.gameObject);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                DemoGameplayTestLauncher.Clear();
            }
        }

        private static BattleHudSkillPresentationSpec ResolveHudSpec(
            MobaConfigDatabase configs, SkillMO skill, SkillAimIndicatorShape expectedShape)
        {
            Assert.IsTrue(configs.TryGetSkillButtonTemplate(skill.SkillButtonTemplateId, out var template),
                "Annie skill button template is missing for skill " + skill.Id);
            var spec = new BattleHudSkillPresentationSpecResolver().Resolve(skill, template);
            Assert.AreEqual(expectedShape, spec.IndicatorShape, "Unexpected indicator for skill " + skill.Id);
            return spec;
        }

        private sealed class AnnieHudInputFixture : System.IDisposable
        {
            private readonly BattleHudInputUi _ui;
            private readonly BattleHudInputEventBridge _bridge;
            private readonly SkillButtonView _button;

            private AnnieHudInputFixture(BattleHudInputUi ui, BattleHudInputEventBridge bridge, SkillButtonView button)
            {
                _ui = ui;
                _bridge = bridge;
                _button = button;
            }

            public Vector2 ButtonScreenPosition =>
                RectTransformUtility.WorldToScreenPoint(null, _button.transform.position);

            public static AnnieHudInputFixture Create(
                BattleContext context, BattleHudSkillPresentationSpec[] specs,
                SkillButtonTemplateMO[] templates, int slot)
            {
                Assert.GreaterOrEqual(slot, 1);
                Assert.LessOrEqual(slot, specs.Length);
                var root = new GameObject("AnnieHudInputFixture.Root", typeof(RectTransform));
                var rootRect = (RectTransform)root.transform;
                rootRect.sizeDelta = new Vector2(1200f, 900f);
                var buttons = new SkillButtonView[specs.Length];
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
                        new BattleHudSkillButtonTemplateApplier().Apply(button, templates[i], specs[i]);
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
                    [slot] = specs[slot - 1]
                });
                bridge.Bind(ui);
                return new AnnieHudInputFixture(ui, bridge, targetButton);
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
            GameFlowDomain flow, System.Func<bool> predicate, int maxTicks, string failureMessage)
        {
            return TickUntil(flow, predicate, maxTicks, () => failureMessage);
        }

        private static IEnumerator TickUntil(
            GameFlowDomain flow, System.Func<bool> predicate, int maxTicks,
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

        private static void FlowTick(GameFlowDomain flow, int ticks)
        {
            const float fixedDeltaTime = 1f / 30f;
            for (var tick = 0; tick < ticks; tick++) flow.Tick(fixedDeltaTime);
        }

        private static string DescribeAssetLoadState(GameFlowDomain flow)
        {
            var core = GetPrivateField(flow, "_core");
            var scheduler = GetPrivateField(core, "_featureScheduler");
            var loading = FindLoadingFeature(scheduler, 0, new HashSet<object>());
            if (loading == null)
                return " BattleLoadingScreenFeature is not registered.";

            var snapshot = loading.CurrentSnapshot;
            var errorCount = snapshot.Errors == null ? 0 : snapshot.Errors.Count;
            return " AssetLoad: completed=" + snapshot.Completed + ", success=" + snapshot.Success +
                   ", progress=" + snapshot.LoadedCount + "/" + snapshot.TotalCount +
                   ", current=" + snapshot.CurrentAssetKey + ", error=" + snapshot.ErrorMessage +
                   ", errors=" + errorCount + ".";
        }

        private static BattleLoadingScreenFeature FindLoadingFeature(
            object value, int depth, HashSet<object> visited)
        {
            if (value == null || depth > 7 || !visited.Add(value)) return null;
            if (value is BattleLoadingScreenFeature loading) return loading;
            if (value is System.Collections.IEnumerable sequence && !(value is string))
            {
                foreach (var item in sequence)
                {
                    var found = FindLoadingFeature(item, depth + 1, visited);
                    if (found != null) return found;
                }
            }

            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(string)) return null;
            for (var current = type; current != null; current = current.BaseType)
            {
                var fields = current.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                               BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (var i = 0; i < fields.Length; i++)
                {
                    object nested;
                    try { nested = fields[i].GetValue(value); }
                    catch { continue; }
                    var found = FindLoadingFeature(nested, depth + 1, visited);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private static object GetPrivateField(object value, string fieldName)
        {
            if (value == null) return null;
            for (var type = value.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public |
                                                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(value);
            }
            return null;
        }

        private static bool IsPlayerUsingHero(BattleContext context, string playerId, int heroId)
        {
            var loadouts = context.BuildEffectivePlayerLoadouts();
            if (loadouts == null) return false;
            for (var i = 0; i < loadouts.Length; i++)
                if (string.Equals(loadouts[i].PlayerId.Value, playerId, System.StringComparison.OrdinalIgnoreCase))
                    return loadouts[i].HeroId == heroId;
            return false;
        }

        private static bool TryGetPlayerActor(
            BattleContext context, string playerId, out int actorId, out ActorEntity actor)
        {
            actorId = 0;
            actor = null;
            if (context?.Session == null || !context.Session.TryGetWorld(out var world) || world?.Services == null ||
                !world.Services.TryResolve<MobaPlayerActorMapService>(out var playerActors) || playerActors == null ||
                !playerActors.TryGetActorId(new PlayerId(playerId), out actorId) || actorId <= 0 ||
                !world.Services.TryResolve<MobaActorLookupService>(out var actors) || actors == null ||
                !actors.TryGetActorEntity(actorId, out actor) || actor == null)
                return false;
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

        private static int GetSkillPreviewVfx(MobaSkillConfigTestHarness harness, int skillId)
        {
            Assert.IsTrue(harness.Config.TryGetSkill(skillId, out var skill),
                "Annie skill config is missing: " + skillId);
            return skill.PreviewVfxId;
        }

        [Test]
        public void E_ShouldGrantConfiguredShieldAndTimedMoveSpeedBuff()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(
                Annie, "annie_e_shield_move_speed_runtime_contract_world"))
            {
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    ESkillId,
                    (int)TriggeringConstants.AddShieldId.Value,
                    (int)TriggeringConstants.AddBuffId.Value);
                Assert.IsTrue(harness.Config.TryGetBuff(EMoveSpeedBuffId, out _),
                    "Annie E's timed movement Buff must load from published runtime config.");

                harness.EnterGameAndWarmup(reason: "Annie E shield and movement Buff contract");
                var actorId = harness.AssertPlayerActorBound();
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(actorId, 3, aimPos: default, aimDir: Vec3.Right);
                Assert.IsTrue(cast.Success, "Annie E should release successfully: " + cast.FailReason);
                harness.TickUntilSkillStops(actorId, 3, maxTicks: 50,
                    message: "Annie E should finish its shield release.");

                var shields = harness.World.Services.Resolve<MobaShieldService>();
                Assert.AreEqual(100f, shields.GetTotalRemaining(actorId), 0.01f,
                    "Annie E rank one should grant the configured 100 point shield.");
                Assert.IsTrue(harness.HasActorBuff(actorId, EMoveSpeedBuffId),
                    "Annie E should apply its configured timed movement Buff to self.");
                AssertBuffPresentationCue(harness, actorId, EMoveSpeedBuffId,
                    "Annie E movement Buff should publish the shared Buff presentation cue.");

                var enemyActorId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "annie_e_attacker", 9306, x: 2f, z: 0f);
                var hpBeforeHit = harness.GetActorHp(actorId);
                var incomingAttack = new AttackInfo
                {
                    AttackerActorId = enemyActorId,
                    TargetActorId = actorId,
                    DamageType = DamageType.Magic,
                    CritType = CritType.None,
                    ReasonKind = DamageReasonKind.Skill,
                    ReasonParam = 930601,
                    FormulaKind = (int)DamageFormulaKind.Standard,
                };
                incomingAttack.BaseDamage.FixedBaseValue = Fixed64.FromSingle(20f);
                var damageResult = harness.World.Services.Resolve<MobaCombatEffectService>()
                    .DealDamage(incomingAttack);
                Assert.IsNotNull(damageResult,
                    "An enemy hit against Annie E should complete through the authoritative damage pipeline.");
                Assert.Greater(damageResult.ShieldAbsorb, 0f,
                    "Annie E shield should report the amount absorbed by the damage pipeline.");
                Assert.AreEqual(hpBeforeHit, harness.GetActorHp(actorId), 0.01f,
                    "A hit fully covered by Annie E should not reduce the protected actor's HP.");

                harness.Tick(160);
                Assert.AreEqual(0f, shields.GetTotalRemaining(actorId), 0.01f,
                    "Annie E shield should expire with its configured duration.");
                Assert.IsFalse(harness.HasActorBuff(actorId, EMoveSpeedBuffId),
                    "Annie E movement Buff should expire with the shield duration.");
            }
        }

        [Test]
        public void Passive_ShouldCountSuccessfulQAndWCastsButNotBasicAttacks()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(
                Annie, "annie_passive_spell_counter_runtime_contract_world"))
            {
                Assert.IsTrue(harness.Config.TryGetPassiveSkill(PassiveId, out var passive),
                    "Annie's passive skill must be loaded from the runtime config.");
                CollectionAssert.Contains(passive.TriggerIds, PassiveTriggerId,
                    "Annie's passive must subscribe to the shared authored spell counter trigger.");
                HeroSkillHeadlessContract.AssertTriggerActions(harness, PassiveTriggerId,
                    (int)TriggeringConstants.AdvanceGameplayCounterId.Value);

                harness.EnterGameAndWarmup(reason: "Annie passive Q/W cast counter contract");
                var actorId = harness.AssertPlayerActorBound();
                var variables = harness.World.Services.Resolve<MobaGameplayVariableService>();
                var counterKey = MobaGameplayVariableKeys.ResolveScopedKey(CounterKeyId, actorId);
                var target = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "annie_passive_target", 9303, x: 0f, z: 4f);
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();

                var qCast = skills.TryCastBySlot(actorId, 1, aimPos: default, aimDir: Vec3.Right,
                    targetActorId: target);
                Assert.IsTrue(qCast.Success, "Annie Q should release successfully.");
                harness.TickUntilSkillStops(actorId, 1, maxTicks: 50,
                    message: "Annie Q should finish its release before counting the cast.");
                harness.Tick(1);
                var subscriptions = harness.World.Services.Resolve<MobaTriggerPlanSubscriptionService>();
                var passiveOwnerKey = harness.AssertActorEntity(actorId).hasOngoingTriggerPlans
                    && harness.AssertActorEntity(actorId).ongoingTriggerPlans.Active != null
                    && harness.AssertActorEntity(actorId).ongoingTriggerPlans.Active.Count > 0
                    ? harness.AssertActorEntity(actorId).ongoingTriggerPlans.Active[0].OwnerKey
                    : 0L;
                Assert.IsTrue(passiveOwnerKey != 0, "Annie passive should expose an owner-bound trigger context.");
                Assert.IsTrue(subscriptions.TryGetLastEvaluation(passiveOwnerKey, PassiveTriggerId, out var evaluation),
                    "Annie passive trigger should be evaluated after Q cast complete.");
                Assert.IsTrue(evaluation.GatePassed && evaluation.SourceResolved && evaluation.PlanPassed,
                    $"Annie passive evaluation gate={evaluation.GatePassed} source={evaluation.SourceResolved} plan={evaluation.PlanPassed} count={evaluation.Count}");
                Assert.AreEqual(1d, variables.Get(counterKey),
                    "A completed Q cast should add exactly one passive charge.");

                var wCast = skills.TryCastBySlot(actorId, 2, aimPos: default, aimDir: Vec3.Right,
                    targetActorId: target);
                Assert.IsTrue(wCast.Success, "Annie W should release successfully.");
                harness.TickUntilSkillStops(actorId, 2, maxTicks: 50,
                    message: "Annie W should finish its release before counting the cast.");
                harness.Tick(1);
                Assert.AreEqual(2d, variables.Get(counterKey),
                    "A completed W cast should add one more passive charge.");

                var cues = harness.World.Services.Resolve<MobaPresentationCueSnapshotService>();
                Assert.IsTrue(cues.TryGetActiveCue($"gameplay-counter:{actorId}:{CounterKeyId}", out var cue),
                    "The passive counter should publish the shared gameplay-counter presentation cue.");
                Assert.AreEqual(2, cue.Entry.StackCount,
                    "The passive counter cue should display the current two spell charges.");

                var basicAttackSlot = harness.AssertActorEntity(actorId).skillLoadout.ActiveSkills.Length;
                var loadout = harness.World.Services.Resolve<MobaSkillLoadoutService>();
                Assert.IsTrue(loadout.TryGetSkillId(actorId, basicAttackSlot, out var basicAttackId));
                Assert.AreEqual(BasicAttackSkillId, basicAttackId,
                    "The basic attack follows Annie's configured active skills.");
                var basicCast = skills.TryCastBySlot(actorId, basicAttackSlot, aimPos: default, aimDir: Vec3.Right,
                    targetActorId: target);
                Assert.IsTrue(basicCast.Success, "Annie's basic attack should release successfully: " + basicCast.FailReason);
                harness.TickUntilSkillStops(actorId, basicAttackSlot, maxTicks: 30,
                    message: "Annie's basic attack should finish its release.");
                harness.Tick(1);
                Assert.AreEqual(2d, variables.Get(counterKey),
                    "Annie's basic attack must not advance the spell-cast passive.");
            }
        }

        [Test]
        public void Passive_ShouldCountECastAndRCastBeforeTheirDamageResolution()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(
                Annie, "annie_passive_e_r_cast_counter_runtime_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "Annie passive E/R cast counter contract");
                var actorId = harness.AssertPlayerActorBound();
                var variables = harness.World.Services.Resolve<MobaGameplayVariableService>();
                var counterKey = MobaGameplayVariableKeys.ResolveScopedKey(CounterKeyId, actorId);
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();

                var eCast = skills.TryCastBySlot(actorId, 3, aimPos: default, aimDir: Vec3.Right);
                Assert.IsTrue(eCast.Success, "Annie E should release successfully: " + eCast.FailReason);
                harness.TickUntilSkillStops(actorId, 3, maxTicks: 50,
                    message: "Annie E should finish before checking its passive charge.");
                harness.Tick(1);
                Assert.AreEqual(1d, variables.Get(counterKey),
                    "Annie E is an active spell and must add one passive charge.");

                // R uses the shared target-query contract and therefore needs a valid
                // in-range target to release. The counter remains below four, so its
                // impact damage cannot consume the charge in this test.
                var target = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "annie_passive_r_target", 9310, x: 3f, z: 0f);
                var rCast = skills.TryCastBySlot(actorId, 4,
                    aimPos: new Vec3(3f, 0f, 0f), aimDir: Vec3.Right,
                    targetActorId: target);
                Assert.IsTrue(rCast.Success, "Annie R should release successfully: " + rCast.FailReason);
                harness.TickUntilSkillStops(actorId, 4, maxTicks: 70,
                    message: "Annie R should finish before checking its passive charge.");
                harness.Tick(1);
                Assert.AreEqual(2d, variables.Get(counterKey),
                    "Annie R is an active spell and must add one passive charge even when it hits no enemy.");
            }
        }

        [Test]
        public void Passive_ShouldApplyStunOnFourthSpellDamageAndConsumeCounter()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(
                Annie, "annie_passive_fourth_spell_stun_runtime_contract_world"))
            {
                HeroSkillHeadlessContract.AssertTriggerActions(harness, 30030003,
                    (int)TriggeringConstants.AddBuffId.Value);
                Assert.IsTrue(harness.Config.TryGetBuff(30030211, out var stun),
                    "Annie's empowered spell must resolve the authored stun Buff.");
                Assert.AreEqual(1250, stun.DurationMs,
                    "The passive stun duration must remain data driven.");

                harness.EnterGameAndWarmup(reason: "Annie passive fourth spell stun contract");
                var actorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "annie_passive_stun_target", 9303, x: 3f, z: 0f);
                var variables = harness.World.Services.Resolve<MobaGameplayVariableService>();
                var counterKey = MobaGameplayVariableKeys.ResolveScopedKey(CounterKeyId, actorId);
                variables.Set(counterKey, 3d);

                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(actorId, 1, aimPos: default, aimDir: Vec3.Right,
                    targetActorId: targetActorId);
                Assert.IsTrue(cast.Success, "The fourth Annie spell should release successfully: " + cast.FailReason);
                harness.TickUntilTraceNode(
                    MobaExecutionKind.DamageApply,
                    QSkillId,
                    maxTicks: harness.CalculateWaitTicksForSkillEffect(
                        QSkillId, QSkillId, safetyFrames: 5) + 60,
                    message: "The fourth Annie spell should resolve damage before consuming the passive.");

                var subscriptions = harness.World.Services.Resolve<MobaTriggerPlanSubscriptionService>();
                var passiveOwnerKey = harness.AssertActorEntity(actorId).ongoingTriggerPlans.Active[0].OwnerKey;
                Assert.IsTrue(subscriptions.TryGetLastEvaluation(passiveOwnerKey, 30030006, out var stunEvaluation),
                    "Annie passive damage trigger should evaluate after the fourth Q hit.");
                Assert.IsTrue(stunEvaluation.GatePassed && stunEvaluation.SourceResolved && stunEvaluation.PlanPassed,
                    $"Annie passive damage evaluation gate={stunEvaluation.GatePassed} source={stunEvaluation.SourceResolved} plan={stunEvaluation.PlanPassed} count={stunEvaluation.Count}");
                Assert.AreEqual(0d, variables.Get(counterKey),
                    "Annie passive counter should be consumed by the damage trigger before its follow-up effect executes.");

                Assert.IsTrue(harness.HasActorBuff(targetActorId, 30030211),
                    "The first positive damage from the fourth spell must apply the shared stun Buff to its target.");
                Assert.AreEqual(0d, variables.Get(counterKey),
                    "The empowered spell must consume the four charge counter exactly once.");
            }
        }

        [Test]
        public void Q_ShouldLaunchConfiguredProjectileAndApplyConfiguredDamageOnHit()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(
                Annie, "annie_q_projectile_damage_runtime_contract_world"))
            {
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    QSkillId,
                    (int)TriggeringConstants.ShootProjectileId.Value);
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    30030211,
                    (int)TriggeringConstants.GiveDamageId.Value);
                harness.AssertProjectileConfigExists(33030101, 32030101);
                Assert.IsTrue(harness.Config.TryGetProjectile(32030101, out var projectile),
                    "Annie Q projectile config should be loaded from the published runtime data.");
                Assert.AreEqual(22f, projectile.Speed, 0.001f);
                Assert.AreEqual(6f, projectile.MaxDistance, 0.001f);
                Assert.AreEqual(1, projectile.HitsRemaining);
                CollectionAssert.Contains(projectile.OnExitTriggerIds, 30030211,
                    "Annie Q projectile exit must dispatch the authored hit trigger.");

                harness.EnterGameAndWarmup(reason: "Annie Q projectile and damage contract");
                var actorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "annie_q_target", 9303, x: 3f, z: 0f);
                var hpBefore = harness.GetActorHp(targetActorId);
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(actorId, 1, aimPos: default, aimDir: Vec3.Right,
                    targetActorId: targetActorId);
                Assert.IsTrue(cast.Success, "Annie Q should release successfully: " + cast.FailReason);

                var effectTrace = harness.TickUntilTraceNode(
                    MobaExecutionKind.EffectExecution,
                    QSkillId,
                    maxTicks: harness.CalculateWaitTicksForSkillEffect(
                        QSkillId, QSkillId, safetyFrames: 5) + 30,
                    message: "Annie Q should execute its configured effect.");
                harness.AssertProjectileLaunchedUnderEffect(effectTrace.RootId, 33030101, 32030101);

                var damageTrace = harness.TickUntilTraceNode(
                    MobaExecutionKind.DamageApply,
                    QSkillId,
                    maxTicks: 60,
                    message: "Annie Q projectile hit should dispatch the configured damage trigger.");
                Assert.AreEqual(effectTrace.RootId, damageTrace.RootId,
                    "Annie Q projectile damage must remain under the originating skill effect lineage.");
                Assert.Less(harness.GetActorHp(targetActorId), hpBefore,
                    "Annie Q projectile hit should reduce the locked enemy's HP.");
            }
        }

        [Test]
        public void Q_ShouldRefundTheActualCostWhenItsProjectileKillsTheTarget()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(
                Annie, "annie_q_kill_refund_runtime_contract_world"))
            {
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    30030212,
                    (int)TriggeringConstants.GetActionId(
                        TriggeringConstants.Actions.RefundSkillCostOnKill).Value);

                harness.EnterGameAndWarmup(reason: "Annie Q kill refund contract");
                var actorId = harness.AssertPlayerActorBound();
                var gameplayBinding = harness.World.Services.Resolve<MobaGameplayTriggerBindingService>();
                var gameplayConfig = harness.World.Services.Resolve<MobaGameplayConfigService>();
                Assert.IsTrue(gameplayConfig.TryGetGameplay(gameplayBinding.BoundGameplayId, out var gameplay),
                    "The active gameplay must resolve before checking its global kill-refund trigger.");
                CollectionAssert.Contains(gameplay.TriggerIds, 30030212,
                    "The active gameplay must bind Annie Q's generic kill-refund trigger.");
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "annie_q_kill_target", 9303, x: 3f, z: 0f);
                harness.SetScenarioActorAttribute(targetActorId, "hp", 1f);
                UnitDieEventPayload? observedDeath = null;
                var eventBus = harness.World.Services.Resolve<IEventBus>();
                var deathEventId = TriggeringIdUtil.GetEventEid(MobaUnitTriggering.Events.Die);
                using (eventBus.Subscribe(new EventKey<UnitDieEventPayload>(deathEventId), death => observedDeath = death))
                {
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var manaBefore = harness.GetActorAttribute(actorId, BattleAttributeType.MANA);
                var cast = skills.TryCastBySlot(actorId, 1, aimPos: default, aimDir: Vec3.Right,
                    targetActorId: targetActorId);
                Assert.IsTrue(cast.Success, "Annie Q should release successfully: " + cast.FailReason);

                var manaAfterCost = harness.GetActorAttribute(actorId, BattleAttributeType.MANA);
                Assert.Less(manaAfterCost, manaBefore,
                    "Annie Q should consume mana before its projectile resolves.");
                var actualCost = manaBefore - manaAfterCost;
                Assert.Greater(actualCost, 0f, "Annie Q actual cost should be positive.");

                harness.TickUntilTraceNode(
                    MobaExecutionKind.DamageApply,
                    QSkillId,
                    maxTicks: harness.CalculateWaitTicksForSkillEffect(
                        QSkillId, QSkillId, safetyFrames: 5) + 60,
                    message: "Annie Q should damage and kill the low-health target.");
                Assert.LessOrEqual(harness.GetActorHp(targetActorId), 0f,
                    "Annie Q should kill a target configured with one HP.");

                for (var i = 0; i < 30 && harness.GetActorAttribute(actorId, BattleAttributeType.MANA) < manaBefore; i++)
                    harness.Tick(1);
                Assert.IsTrue(observedDeath.HasValue,
                    "Annie Q projectile damage must publish a unit.die payload for the killed target.");
                var deathPayload = observedDeath.Value;
                Assert.AreEqual(actorId, deathPayload.KillerActorId);
                Assert.AreEqual((int)DamageReasonKind.Skill, deathPayload.ReasonKind);
                Assert.AreEqual(QSkillId, deathPayload.ReasonParam);
                Assert.IsTrue(deathPayload.TryGetOrigin(out var deathOrigin)
                    && deathOrigin.SkillRuntimeHandle.IsValid,
                    "The unit.die payload must retain the originating skill runtime handle.");
                Assert.AreEqual(manaBefore, harness.GetActorAttribute(actorId, BattleAttributeType.MANA), 0.001f,
                    "Annie Q kill should refund the actual mana cost through the generic unit.die trigger.");
                Assert.AreEqual(actualCost, manaBefore - manaAfterCost, 0.001f,
                    "The refund assertion must be based on the actual cost paid by this cast.");
                }
            }
        }

        [Test]
        public void W_ShouldApplyConfiguredSectorDamageOnlyToTargetsInsideTheCastDirection()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(
                Annie, "annie_w_sector_damage_runtime_contract_world"))
            {
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    WSkillId,
                    (int)TriggeringConstants.GiveDamageId.Value);

                harness.EnterGameAndWarmup(reason: "Annie W sector damage contract");
                var actorId = harness.AssertPlayerActorBound();
                var insideActorId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "annie_w_inside_target", 9304, x: 3f, z: 0f);
                var outsideActorId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "annie_w_outside_target", 9305, x: 3f, z: 4f);
                var insideHpBefore = harness.GetActorHp(insideActorId);
                var outsideHpBefore = harness.GetActorHp(outsideActorId);
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(actorId, 2, aimPos: new Vec3(3f, 0f, 0f), aimDir: Vec3.Right,
                    targetActorId: insideActorId);
                Assert.IsTrue(cast.Success, "Annie W should release successfully: " + cast.FailReason);

                harness.TickUntilTraceNode(
                    MobaExecutionKind.DamageApply,
                    WSkillId,
                    maxTicks: harness.CalculateWaitTicksForSkillEffect(
                        WSkillId, WSkillId, safetyFrames: 5) + 30,
                    message: "Annie W should execute its configured sector damage.");

                Assert.Less(harness.GetActorHp(insideActorId), insideHpBefore,
                    "Annie W should damage a target inside its forward sector.");
                Assert.AreEqual(outsideHpBefore, harness.GetActorHp(outsideActorId), 0.001f,
                    "Annie W should not damage a target outside its configured sector.");
            }
        }

        [Test]
        public void R_ShouldApplyConfiguredImpactDamageAndSpawnConfiguredTibbersSummon()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(
                Annie, "annie_r_tibbers_runtime_contract_world"))
            {
                Assert.IsTrue(harness.Config.TryGetSummon(TibbersSummonId, out var summon),
                    "Annie R must resolve the authored Tibbers summon configuration.");
                Assert.AreEqual(14000, summon.LifetimeMs,
                    "Tibbers must use the authored fourteen-second lifetime.");
                Assert.AreEqual(1, summon.MaxAlivePerOwner,
                    "Annie R must keep one active Tibbers summon per owner.");
                Assert.AreEqual(30030502, summon.ModelId,
                    "Tibbers must resolve the authored model configuration.");
                Assert.AreEqual(30030502, summon.AttributeTemplateId,
                    "Tibbers must resolve the authored attribute template.");
                Assert.AreEqual(30030502, summon.InheritAttributeConfigId,
                    "Tibbers must resolve the authored owner inheritance template.");
                Assert.AreEqual(30030501, summon.BrainId,
                    "Tibbers must use the authored summon Brain configuration.");
                HeroSkillHeadlessContract.AssertTriggerActions(
                    harness,
                    RSkillId,
                    (int)TriggeringConstants.GiveDamageId.Value,
                    (int)TriggeringConstants.SpawnSummonId.Value);

                harness.EnterGameAndWarmup(reason: "Annie R impact and Tibbers summon contract");
                var actorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "annie_r_impact_target", 9307, x: 3f, z: 0f);
                var hpBefore = harness.GetActorHp(targetActorId);
                var aimPos = new Vec3(3f, 0f, 0f);
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(actorId, 4, aimPos: aimPos, aimDir: Vec3.Right,
                    targetActorId: targetActorId);
                Assert.IsTrue(cast.Success, "Annie R should release successfully: " + cast.FailReason);

                var effectTrace = harness.TickUntilTraceNode(
                    MobaExecutionKind.EffectExecution,
                    RSkillId,
                    maxTicks: harness.CalculateWaitTicksForSkillEffect(
                        RSkillId, RSkillId, safetyFrames: 5) + 30,
                    message: "Annie R should execute its configured impact effect.");
                harness.AssertActionExecutedUnderEffect(
                    effectTrace.RootId,
                    (int)TriggeringConstants.GiveDamageId.Value,
                    TriggeringConstants.Actions.GiveDamage);
                harness.AssertActionExecutedUnderEffect(
                    effectTrace.RootId,
                    (int)TriggeringConstants.SpawnSummonId.Value,
                    TriggeringConstants.Actions.SpawnSummon);

                harness.TickUntilTraceNode(
                    MobaExecutionKind.DamageApply,
                    RSkillId,
                    maxTicks: 30,
                    message: "Annie R impact should apply configured damage at the aimed position.");
                Assert.Less(harness.GetActorHp(targetActorId), hpBefore,
                    "Annie R impact should reduce an enemy inside its configured query range.");

                harness.TickUntilTraceNode(
                    MobaExecutionKind.SummonSpawn,
                    TibbersSummonId,
                    maxTicks: 30,
                    message: "Annie R should spawn the configured Tibbers summon.");
                var summons = harness.World.Services.Resolve<MobaSummonService>();
                var summonActorIds = new List<int>(1);
                Assert.IsTrue(summons.TryGetSummons(actorId, summonActorIds, TibbersSummonId));
                Assert.AreEqual(1, summonActorIds.Count,
                    "Annie R should create exactly one Tibbers summon.");
                var tibbers = harness.AssertActorEntity(summonActorIds[0]);
                var tibbersActorId = tibbers.actorId.Value;
                var tibbersBehaviorId = tibbers.actorBrain.BehaviorInstanceId;
                Assert.IsTrue(tibbers.hasSummonMeta);
                Assert.AreEqual(TibbersSummonId, tibbers.summonMeta.SummonId);
                Assert.IsTrue(tibbers.hasOwnerLink);
                Assert.AreEqual(actorId, tibbers.ownerLink.OwnerActorId);
                Assert.AreEqual(actorId, tibbers.ownerLink.RootOwnerActorId);
                Assert.IsTrue(tibbers.hasActorBrain);
                Assert.AreEqual(summon.BrainId, tibbers.actorBrain.BrainId);
                Assert.IsTrue(tibbers.hasModelId);
                Assert.AreEqual(summon.ModelId, tibbers.modelId.Value,
                    "Tibbers runtime entity must carry the configured model id.");
                var owner = harness.AssertActorEntity(actorId);
                Assert.IsTrue(owner.hasAttributeGroup && owner.attributeGroup.Group != null);
                Assert.IsTrue(tibbers.hasAttributeGroup && tibbers.attributeGroup.Group != null);
                Assert.AreEqual(owner.GetMobaAttrs().MaxHp, tibbers.GetMobaAttrs().MaxHp, 0.001f,
                    "Tibbers MaxHP must inherit the owner's configured MaxHP at 1.0 ratio.");
                Assert.Less((tibbers.transform.Value.Position - aimPos).Magnitude, 0.5f,
                    "Tibbers must spawn at the R aimed position.");

                Assert.IsTrue(tibbers.hasSkillLoadout && tibbers.skillLoadout.ActiveSkills != null,
                    "Tibbers must initialize a runtime skill loadout from the summon SkillIds column.");
                Assert.IsTrue(ContainsActiveSkill(tibbers, BasicAttackSkillId),
                    "Tibbers SkillLoadout must expose the configured Annie basic attack skill.");
                var brains = harness.World.Services.Resolve<MobaBrainService>();
                Assert.IsTrue(brains.TryGetBehavior(tibbers.actorBrain.BehaviorInstanceId, out var tibbersBehavior),
                    "Tibbers must bind a runtime behavior instance when it is spawned.");
                Assert.AreEqual("MobaBTree", tibbersBehavior.Decision.DecisionType,
                    "Tibbers must execute the authored summon behavior tree through the generic brain runtime.");

                var tibbersAttackHpBefore = harness.GetActorHp(targetActorId);
                for (var i = 0; i < 180 && harness.GetActorHp(targetActorId) >= tibbersAttackHpBefore - 0.001f; i++)
                    harness.Tick(1);
                Assert.Less(harness.GetActorHp(targetActorId), tibbersAttackHpBefore,
                    "Tibbers behavior tree must select its loaded basic attack and damage the nearest enemy.");

                for (var i = 0; i < 500 && summons.GetAliveCount(actorId, TibbersSummonId) > 0; i++)
                    harness.Tick(1);
                Assert.AreEqual(0, summons.GetAliveCount(actorId, TibbersSummonId),
                    "Tibbers must be cleaned up after its authored lifetime.");
                var actorRegistry = harness.World.Services.Resolve<MobaActorRegistry>();
                Assert.IsFalse(actorRegistry.TryGet(tibbersActorId, out _),
                    "Timed-out Tibbers must be removed from the actor registry.");
                Assert.IsFalse(brains.TryGetBehavior(tibbersBehaviorId, out _),
                    "Timed-out Tibbers must release its behavior runtime.");
                Assert.IsFalse(summons.TryDespawn(tibbersActorId, SummonDespawnReason.Timeout),
                    "A completed timeout cleanup must not execute a second despawn.");
            }
        }

        [Test]
        public void Tibbers_DeathShouldReleaseSummonAndBehaviorRuntime()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(
                Annie, "annie_tibbers_death_cleanup_runtime_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "Annie Tibbers killed cleanup contract");
                var ownerActorId = harness.AssertPlayerActorBound();
                var enemyActorId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "annie_tibbers_death_target", 9321, x: 3f, z: 0f);
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(ownerActorId, 4, aimPos: new Vec3(2f, 0f, 0f),
                    aimDir: Vec3.Right);
                Assert.IsTrue(cast.Success, "Annie R should release successfully: " + cast.FailReason);
                harness.TickUntilTraceNode(MobaExecutionKind.SummonSpawn, TibbersSummonId, maxTicks: 80,
                    message: "Annie R should spawn Tibbers before its death cleanup check.");

                var summons = harness.World.Services.Resolve<MobaSummonService>();
                var actors = harness.World.Services.Resolve<MobaActorRegistry>();
                var summonIds = new List<int>(1);
                Assert.IsTrue(summons.TryGetSummons(ownerActorId, summonIds, TibbersSummonId));
                Assert.AreEqual(1, summonIds.Count);
                var tibbers = harness.AssertActorEntity(summonIds[0]);
                var tibbersActorId = tibbers.actorId.Value;
                var behaviorId = tibbers.actorBrain.BehaviorInstanceId;
                var death = harness.World.Services.Resolve<MobaDamageService>().CommitDamage(
                    enemyActorId, tibbersActorId, (int)DamageType.Physical, 1000000f,
                    (int)DamageReasonKind.BasicAttack);
                Assert.IsTrue(death.BecameDead,
                    "Lethal damage must publish the committed death event for Tibbers.");
                harness.Tick(1);
                Assert.AreEqual(0, summons.GetAliveCount(ownerActorId, TibbersSummonId),
                    "Killed Tibbers must be removed from the summon service.");
                Assert.IsFalse(actors.TryGet(tibbersActorId, out _),
                    "Killed Tibbers must be removed from the actor registry.");
                Assert.IsFalse(harness.World.Services.Resolve<MobaBrainService>().TryGetBehavior(behaviorId, out _),
                    "Killed Tibbers must release its behavior runtime.");
                Assert.IsFalse(summons.TryDespawn(tibbersActorId, SummonDespawnReason.Killed),
                    "A completed killed cleanup must not execute a second despawn.");
            }
        }

        [Test]
        public void Tibbers_OwnerDeathShouldDespawnWithoutKilledCleanup()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(
                Annie, "annie_tibbers_owner_death_cleanup_runtime_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "Annie Tibbers owner-death cleanup contract");
                var ownerActorId = harness.AssertPlayerActorBound();
                var enemyActorId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "annie_owner_death_target", 9322, x: 3f, z: 0f);
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(ownerActorId, 4, aimPos: new Vec3(2f, 0f, 0f),
                    aimDir: Vec3.Right);
                Assert.IsTrue(cast.Success, "Annie R should release successfully: " + cast.FailReason);
                harness.TickUntilTraceNode(MobaExecutionKind.SummonSpawn, TibbersSummonId, maxTicks: 80,
                    message: "Annie R should spawn Tibbers before its owner-death cleanup check.");

                var summons = harness.World.Services.Resolve<MobaSummonService>();
                var actors = harness.World.Services.Resolve<MobaActorRegistry>();
                var summonIds = new List<int>(1);
                Assert.IsTrue(summons.TryGetSummons(ownerActorId, summonIds, TibbersSummonId));
                Assert.AreEqual(1, summonIds.Count);
                var tibbersActorId = summonIds[0];
                var ownerDeath = harness.World.Services.Resolve<MobaDamageService>().CommitDamage(
                    enemyActorId, ownerActorId, (int)DamageType.Physical, 1000000f,
                    (int)DamageReasonKind.BasicAttack);
                Assert.IsTrue(ownerDeath.BecameDead,
                    "Lethal damage must commit Annie's owner death before summon cleanup.");
                harness.Tick(1);
                Assert.AreEqual(0, summons.GetAliveCount(ownerActorId, TibbersSummonId),
                    "DespawnOnOwnerDie must remove Tibbers after Annie dies.");
                Assert.IsFalse(actors.TryGet(tibbersActorId, out _),
                    "Owner-death cleanup must remove Tibbers from the actor registry.");
                Assert.IsFalse(summons.TryDespawn(tibbersActorId, SummonDespawnReason.OwnerDead),
                    "Owner-death cleanup must not be applied a second time.");
            }
        }

        [Test]
        public void Tibbers_BehaviorTreeShouldChaseWhenTargetLeavesAttackRange()
        {
            using (var harness = HeroSkillHeadlessContract.CreateHarness(
                Annie, "annie_tibbers_chase_recheck_runtime_contract_world"))
            {
                harness.EnterGameAndWarmup(reason: "Annie Tibbers chase and range recheck contract");
                var actorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "annie_tibbers_chase_target", 9311, x: 3f, z: 0f);
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(actorId, 4,
                    aimPos: new Vec3(3f, 0f, 0f), aimDir: Vec3.Right,
                    targetActorId: targetActorId);
                Assert.IsTrue(cast.Success, "Annie R should release successfully: " + cast.FailReason);
                harness.TickUntilTraceNode(
                    MobaExecutionKind.SummonSpawn,
                    TibbersSummonId,
                    maxTicks: 80,
                    message: "Annie R should spawn Tibbers before the chase check.");

                var summons = harness.World.Services.Resolve<MobaSummonService>();
                var summonActorIds = new List<int>(1);
                Assert.IsTrue(summons.TryGetSummons(actorId, summonActorIds, TibbersSummonId));
                Assert.AreEqual(1, summonActorIds.Count);
                var tibbers = harness.AssertActorEntity(summonActorIds[0]);
                var target = harness.AssertActorEntity(targetActorId);
                var targetTransform = target.transform.Value;
                target.ReplaceTransform(new Transform3(
                    new Vec3(20f, targetTransform.Position.Y, targetTransform.Position.Z),
                    targetTransform.Rotation,
                    targetTransform.Scale));

                var distanceBeforeChase = (tibbers.transform.Value.Position -
                    target.transform.Value.Position).Magnitude;
                var hpBeforeAttack = harness.GetActorHp(targetActorId);
                harness.Tick(180);

                var distanceAfterChase = (tibbers.transform.Value.Position -
                    target.transform.Value.Position).Magnitude;
                Assert.Less(distanceAfterChase, distanceBeforeChase - 0.25f,
                    "Tibbers must publish movement while the selected basic attack is out of range.");

                for (var i = 0; i < 240 && harness.GetActorHp(targetActorId) >= hpBeforeAttack - 0.01f; i++)
                    harness.Tick(1);
                Assert.Less(harness.GetActorHp(targetActorId), hpBeforeAttack,
                    "Tibbers must re-check attack range and resume its loaded basic attack after chasing.");
            }
        }

        private static bool ContainsActiveSkill(global::ActorEntity actor, int skillId)
        {
            if (actor == null || !actor.hasSkillLoadout || actor.skillLoadout.ActiveSkills == null)
                return false;
            for (var i = 0; i < actor.skillLoadout.ActiveSkills.Length; i++)
            {
                var runtime = actor.skillLoadout.ActiveSkills[i];
                if (runtime != null && runtime.SkillId == skillId) return true;
            }
            return false;
        }
    }
}

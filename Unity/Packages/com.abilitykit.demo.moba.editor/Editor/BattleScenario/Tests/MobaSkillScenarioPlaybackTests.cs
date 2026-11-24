#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Eventing;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Triggering;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.Triggering;
using AbilityKit.Game;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow;
using AbilityKit.Protocol.Moba;
using AbilityKit.Triggering.Eventing;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace AbilityKit.Demo.Moba.Editor.BattleScenario.Tests
{
    public sealed class MobaSkillScenarioPlaybackTests
    {
        private const int TibbersBasicAttackSkillId = 30030102;
        private const int TibbersHitVfxId = 90033007;

        [UnityTest]
        public IEnumerator LuxR_FromScenario_ConsumesLocalInput()
        {
            Prepare("lux-r");
            yield return new EnterPlayMode();
            yield return WaitForPlayback("lux-r");
            Assert.AreNotEqual("moba.skill_preview", GameEntry.Instance.Get<GameFlowDomain>().CurrentBattleProfileId);
            Assert.IsNotNull(GameObject.Find("BattleHudCanvas"));
            Assert.IsFalse(EditorApplication.isPaused);
        }

        [UnityTest]
        public IEnumerator PureSkillPreview_PlaysInFreshMatchWithoutHudAndPausesAtEnd()
        {
            Prepare("jinx-w", previewOnly: true);
            yield return new EnterPlayMode();
            yield return VerifyPurePreview();
        }

        private static IEnumerator VerifyPurePreview()
        {
            Time.captureDeltaTime = 1f / 60f;
            var pausedUpdates = 0;
            var firstPausedFrame = -1;
            var lastPausedFrame = -1;
            var hudPresent = false;
            void ObservePause()
            {
                if (!EditorApplication.isPaused || !GameEntry.IsInitialized ||
                    !GameEntry.Instance.TryGet(out BattleContext context)) return;
                if (pausedUpdates == 0) firstPausedFrame = context.LastFrame;
                lastPausedFrame = context.LastFrame;
                hudPresent |= GameObject.Find("BattleHudCanvas") != null;
                if (++pausedUpdates == 4) EditorApplication.isPaused = false;
            }

            EditorApplication.update += ObservePause;
            try
            {
                var deadline = Time.realtimeSinceStartup + 130f;
                while (pausedUpdates < 4 && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.AreEqual(4, pausedUpdates, "Pure preview did not pause at completion.");
                Assert.Greater(firstPausedFrame, 0);
                Assert.AreEqual(firstPausedFrame, lastPausedFrame);
                Assert.IsFalse(hudPresent);
                Assert.IsNotNull(Camera.main);
                var flow = GameEntry.Instance.Get<GameFlowDomain>();
                Assert.AreEqual("moba.skill_preview", flow.CurrentBattleProfileId);
                Assert.IsTrue(GameEntry.Instance.TryGet(out BattleContext context));
                Assert.IsTrue(context.CanSubmitGameplayInput);
                Assert.IsTrue(context.Session.TryGetWorld(out var world));
                var trace = world.Services.Resolve<MobaTraceRegistry>();
                Assert.AreEqual(1, trace.GetNodesByKind((int)MobaExecutionKind.SkillCast)
                    .Count(node => node.Metadata?.ConfigId == 30020201));
                Assert.IsTrue(trace.GetNodesByKind((int)MobaExecutionKind.ProjectileLaunch)
                    .Any(node => node.Metadata?.ConfigId == 32020203));
            }
            finally
            {
                EditorApplication.update -= ObservePause;
                EditorApplication.isPaused = false;
                Time.captureDeltaTime = 0f;
            }
        }

        [UnityTest]
        public IEnumerator JhinR_FromScenario_ConsumesAllFourShots()
        {
            Prepare("jhin-r");
            yield return new EnterPlayMode();
            yield return WaitForPlayback("jhin-r");
        }

        [UnityTest]
        public IEnumerator JhinQ_FromScenario_ConsumesAllFourHits()
        {
            const string caseId = "jhin-q-four-hits";
            Prepare(caseId);
            yield return new EnterPlayMode();
            yield return WaitForPlayback(caseId);
            Assert.IsTrue(GameEntry.Instance.TryGet(out BattleContext context));
            Assert.IsTrue(context.Session.TryGetWorld(out var world));
            var trace = world.Services.Resolve<MobaTraceRegistry>();
            Assert.AreEqual(4, trace.GetNodesByKind((int)MobaExecutionKind.ProjectileHit)
                .Count(node => node.Metadata?.ConfigId == 32010101));
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator JhinW_FromScenario_CapturesPresentation()
        {
            PrepareCapture("jhin-w");
            yield return new EnterPlayMode();
            yield return CapturePlayback("jhin-w", 30010201, 32010201, 90013017, 0, 5, 8, 9, 10, 11, 12, 13, 15, 19, 27, 45, 90);
        }

        [UnityTest]
        public IEnumerator JhinW_FromScenario_RootsHeroMarkedByQ()
        {
            const string caseId = "jhin-w-marked-hero";
            Prepare(caseId);
            yield return new EnterPlayMode();
            yield return WaitForPlayback(caseId);
            Assert.IsTrue(GameEntry.Instance.TryGet(out BattleContext context));
            Assert.IsTrue(context.Session.TryGetWorld(out var world));
            var trace = world.Services.Resolve<MobaTraceRegistry>();
            Assert.AreEqual(1, trace.GetNodesByKind((int)MobaExecutionKind.BuffApply)
                .Count(node => node.Metadata?.ConfigId == 30010222));
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator JhinE_FromScenario_CapturesPresentation()
        {
            PrepareCapture("jhin-e");
            yield return new EnterPlayMode();
            yield return CapturePlayback("jhin-e", 30010301, 32010301, 90013018,
                new CastPresentationProbe("Jhin_Model_SpellE", flightVfxId: 90013003, groundVfxId: 90013019, chargeVfxId: 90031011),
                0, 4, 8, 14, 19, 24, 30, 45, 68, 72, 76, 80, 84, 88, 92, 96, 100, 104, 120);
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator GragasQ_FromScenario_RendersOneBarrelAcrossAllStages()
        {
            PrepareCapture("gragas-q");
            yield return new EnterPlayMode();
            yield return CapturePlayback("gragas-q", 30050201, 32050201, 90035007,
                new CastPresentationProbe("Gragas_Model_SpellQ", flightVfxId: 90035009,
                    deployedClip: "Q_deployed", detonationClip: "Q_detonating"),
                0, 4, 8, 14, 20, 28, 40, 60, 67, 72, 78, 85, 95, 110, 135);
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator GragasR_FromScenario_RendersImpactAndShockwaveAtLanding()
        {
            PrepareCapture("gragas-r");
            yield return new EnterPlayMode();
            yield return CapturePlayback("gragas-r", 30050501, 32050501, 90035012,
                new CastPresentationProbe("Gragas_Model_SpellR", flightVfxId: 90035013,
                    impactBranch: "BeerShockwave_1", impactPosition: new Vector2(7f, 2f)),
                0, 5, 9, 12, 16, 20, 24, 28, 32, 37, 45, 60, 90);
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator GragasE_HitFromScenario_RendersDashAndImpact()
        {
            const string caseId = "gragas-e";
            PrepareCapture(caseId, previewOnly: true);
            yield return new EnterPlayMode();
            yield return CaptureGragasDash(caseId, expectHit: true);
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator GragasE_MissFromScenario_RendersDashWithoutImpact()
        {
            const string caseId = "gragas-e-miss";
            PrepareCapture(caseId, previewOnly: true);
            yield return new EnterPlayMode();
            yield return CaptureGragasDash(caseId, expectHit: false);
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator GragasW_FromScenario_RendersOnlyQualifiedAttackImpact()
        {
            const string caseId = "gragas-w";
            PrepareCapture(caseId, previewOnly: true);
            yield return new EnterPlayMode();
            var drink = new PreviewEffectSample("drink", "Gragas_StarGuardian_W_Cast", 90035016);
            var impact = new PreviewEffectSample("empowered-hit", "Gragas_StarGuardian_AA_Impact", 90035002);
            var result = new AttachedEffectPlayback(caseId, impact.AssetId);
            yield return result.Observe(drink, impact);
            drink.AssertPlayed(result.Directory);
            impact.AssertPlayed(result.Directory);
            Assert.That(result.RequestFrames, Has.Count.EqualTo(1), result.Directory);
            Assert.That(result.CastingClips, Does.Contain("Gragas_Model_SpellW"));
            Assert.That(result.CastingClips, Does.Contain("Gragas_Model_AutoAttack_2"));
            var empowered = result.FindFrame(MobaExecutionKind.SkillCast, 30050701);
            var contact = result.FindFrame(MobaExecutionKind.DamageApply, 30050701);
            var normal = result.FindFrame(MobaExecutionKind.SkillCast, 30050101);
            Assert.That(empowered, Is.GreaterThan(drink.FirstFrame));
            Assert.That(contact, Is.GreaterThan(empowered));
            Assert.That(normal, Is.GreaterThan(impact.FirstFrame));
            Assert.That(result.FindFrame(MobaExecutionKind.DamageApply, 30050101, afterFrame: normal), Is.GreaterThan(normal));
            Assert.That(impact.FirstFrame, Is.GreaterThanOrEqualTo(contact));
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator JinxE_FromScenario_RendersEachTrapAndHitAtItsOwnCenter()
        {
            const string caseId = "jinx-e";
            PrepareCapture(caseId, previewOnly: true);
            yield return new EnterPlayMode();
            yield return CaptureJinxTraps(caseId, expectHit: true);
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator JinxE_MissFromScenario_RendersTrapsWithoutExplosions()
        {
            const string caseId = "jinx-e-miss";
            PrepareCapture(caseId, previewOnly: true);
            yield return new EnterPlayMode();
            yield return CaptureJinxTraps(caseId, expectHit: false);
        }

        private static IEnumerator CaptureJinxTraps(string caseId, bool expectHit)
        {
            var sides = new[] { "left", "center", "right" };
            var trapIds = new[] { 42020302, 42020301, 42020303 };
            var hitIds = new[] { 42020305, 42020304, 42020306 };
            var trapVfx = new[] { 90032008, 90032007, 90032009 };
            var hitVfx = new[] { 90032011, 90032010, 90032012 };
            var deploy = sides.Select((side, index) => new PreviewEffectSample(side + "-deploy",
                index == 1 ? "Jinx_E_Trap" : "Jinx_E_Trap_" + side, trapVfx[index], maximumAgeFrames: 26)).ToArray();
            var armed = sides.Select((side, index) => new PreviewEffectSample(side + "-armed",
                index == 1 ? "Jinx_E_Trap" : "Jinx_E_Trap_" + side, trapVfx[index], minimumAgeFrames: 35)).ToArray();
            var hit = sides.Select((side, index) => new PreviewEffectSample(side + "-hit",
                "Jinx_E_Hit_" + side, hitVfx[index])).ToArray();
            var result = new AttachedEffectPlayback(caseId, 0);
            yield return result.Observe(deploy.Concat(armed).Concat(hit).ToArray());
            Assert.That(result.CastingClips, Does.Contain("Jinx_Model_SpellE"), result.Directory);
            for (var index = 0; index < sides.Length; index++)
            {
                var center = new Vector3(5f + index * 2f, 0f, 1f);
                var enter = result.FindFrame(MobaExecutionKind.AreaEnter, trapIds[index]);
                var impact = result.FindFrame(MobaExecutionKind.AreaSpawn, hitIds[index]);
                deploy[index].AssertPlayed(result.Directory);
                armed[index].AssertPlayed(result.Directory);
                Assert.That(Vector3.Distance(deploy[index].StartPosition, center), Is.LessThan(0.05f));
                Assert.That(result.FindFrame(MobaExecutionKind.AreaExpire, trapIds[index]), Is.GreaterThan(armed[index].SampleFrame));
                if (expectHit)
                {
                    hit[index].AssertPlayed(result.Directory);
                    Assert.That(enter - result.FindFrame(MobaExecutionKind.AreaSpawn, trapIds[index]), Is.GreaterThanOrEqualTo(14));
                    Assert.That(impact, Is.EqualTo(enter));
                    Assert.That(hit[index].FirstFrame, Is.GreaterThanOrEqualTo(impact));
                    Assert.That(Vector3.Distance(hit[index].StartPosition, center), Is.LessThan(0.05f));
                }
                else
                {
                    Assert.That(enter, Is.EqualTo(-1));
                    Assert.That(impact, Is.EqualTo(-1));
                    Assert.That(hit[index].FirstFrame, Is.EqualTo(-1));
                }
            }
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator JinxR_FromScenario_CapturesPresentation()
        {
            PrepareCapture("jinx-r");
            yield return new EnterPlayMode();
            yield return CapturePlayback("jinx-r", 30020401, 32020403, 90032026, 0, 6, 10, 13, 14, 15, 16, 17, 18, 20, 23, 33, 48, 90);
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator JinxW_FromScenario_RendersTravelBolt()
        {
            PrepareCapture("jinx-w");
            yield return new EnterPlayMode();
            yield return CaptureTravel("jinx-w", 90032027);
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator AnnieQ_FromScenario_RendersThrowMissileAndImpact()
        {
            const string caseId = "annie-q";
            PrepareCapture(caseId, previewOnly: true);
            yield return new EnterPlayMode();
            var capture = new AttachedEffectPlayback(caseId, 90033018);
            var release = new PreviewEffectSample("q-throw", "Void_Annie_Q_Cast", 90033010);
            var flight = new PreviewEffectSample("q-missile", "Void_Annie_Q_Projectile", 90033001);
            var impact = new PreviewEffectSample("q-impact", "Void_Annie_Q_Hit", 90033003);
            yield return capture.Observe(release, flight, impact);

            var launch = capture.FindFrame(MobaExecutionKind.ProjectileLaunch, 32030101);
            var hit = capture.FindFrame(MobaExecutionKind.DamageApply, 30030201);
            Assert.That(launch, Is.GreaterThanOrEqualTo(0), capture.Directory);
            Assert.That(hit, Is.GreaterThan(launch), capture.Directory);
            Assert.That(capture.CastingClips, Does.Contain("Void_Annie_Model_SpellQ"), capture.Directory);
            Assert.That(release.FirstFrame, Is.InRange(launch - 4, launch + 4), capture.Directory);
            Assert.That(Vector2.Distance(new Vector2(7f, -4f), new Vector2(release.StartPosition.x, release.StartPosition.z)),
                Is.LessThan(.25f), capture.Directory);
            release.AssertPlayed(capture.Directory);
            flight.AssertVisible(capture.Directory);
            Assert.That(flight.Moved, Is.True, capture.Directory);
            Assert.That(capture.RequestFrames, Has.Count.EqualTo(1), capture.Directory);
            Assert.That(impact.FirstFrame, Is.InRange(hit, hit + 4), capture.Directory);
            Assert.That(Vector2.Distance(new Vector2(7f, 0f), new Vector2(impact.StartPosition.x, impact.StartPosition.z)),
                Is.LessThan(.25f), capture.Directory);
            impact.AssertPlayed(capture.Directory);
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator AnnieE_FromScenario_RendersShieldUntilExpiry()
        {
            PrepareCapture("annie-e");
            yield return new EnterPlayMode();
            yield return CaptureShield("annie-e", "player_1", reapplyBaseline: true);
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator AnnieE_OnAllyFromScenario_RendersShieldUntilExpiry()
        {
            PrepareCapture("annie-e-ally");
            yield return new EnterPlayMode();
            yield return CaptureShield("annie-e-ally", "preview_ally");
        }

        private static IEnumerator CaptureShield(string caseId, string targetPlayerId, bool reapplyBaseline = false)
        {
            const int shieldVfxId = 90033004;
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../local/Logs/moba-presentation-scenarios",
                System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"), caseId));
            Directory.CreateDirectory(directory);
            var samples = new List<string>();
            var lastFrame = -1;
            var peakPixels = 0;
            var targetActorId = 0;
            MobaShieldService shields = null;
            System.IDisposable baselineSubscription = null;
            var baselineRequests = 0;
            var baselineDeliveries = 0;
            yield return WaitForPlayback(caseId, () =>
            {
                if (Camera.main == null || !GameEntry.IsInitialized ||
                    !GameEntry.Instance.TryGet(out BattleContext context) || context.LastFrame == lastFrame ||
                    context.Session == null || !context.Session.TryGetWorld(out var world)) return;
                lastFrame = context.LastFrame;
                var active = Object.FindObjectsOfType<BattleVfxPoolableTag>()
                    .Where(tag => tag.VfxId == shieldVfxId && tag.gameObject.activeInHierarchy).ToArray();
                if (active.Length == 0) return;
                Assert.AreEqual(1, active.Length);
                if (targetActorId == 0)
                {
                    var map = world.Services.Resolve<MobaPlayerActorMapService>();
                    Assert.IsTrue(map.TryGetActorId(new PlayerId(targetPlayerId), out targetActorId));
                    shields = world.Services.Resolve<MobaShieldService>();
                    Assert.IsTrue(shields.GetPresentationSnapshot(targetActorId).HasShield);
                    if (targetActorId != context.LocalActorId)
                        Assert.IsFalse(shields.GetPresentationSnapshot(context.LocalActorId).HasShield);
                }

                if (reapplyBaseline && baselineRequests < 2 && baselineRequests == baselineDeliveries)
                {
                    baselineSubscription ??= context.FrameSnapshots.Subscribe<PresentationCueData[]>(
                        MobaOpCodes.Snapshot.PresentationCue, (_, entries) =>
                        {
                            var matches = entries.Count(entry => entry.SnapshotKind == PresentationCueSnapshotKind.ActiveBaseline &&
                                entry.VfxId == shieldVfxId && entry.TargetActorId == targetActorId);
                            if (matches == 0) return;
                            Assert.AreEqual(1, matches, "The active shield must appear once in each baseline.");
                            baselineDeliveries++;
                            samples.Add($"frame={context.LastFrame} shield baseline={baselineDeliveries} target={targetActorId}");
                        });
                    var router = world.Services.Resolve<MobaSnapshotRouter>();
                    Assert.IsTrue(router.RequestActiveBaseline(typeof(MobaPresentationCueSnapshotService)));
                    baselineRequests++;
                }

                peakPixels = System.Math.Max(peakPixels, Capture(directory, "shield", lastFrame,
                    samples, active, previousBest: peakPixels));
            }, () => baselineSubscription?.Dispose());
            File.WriteAllLines(Path.Combine(directory, "samples.txt"), samples);
            Assert.Greater(peakPixels, 20, $"No shield pixels in the gameplay camera. See {directory}.");
            if (reapplyBaseline)
                Assert.AreEqual(2, baselineDeliveries, $"The production snapshot route did not replay both shield baselines. See {directory}.");
            Assert.IsFalse(shields.GetPresentationSnapshot(targetActorId).HasShield);
            Assert.IsFalse(Object.FindObjectsOfType<BattleVfxPoolableTag>()
                .Any(tag => tag.VfxId == shieldVfxId && tag.gameObject.activeInHierarchy));
            Debug.Log($"[SkillScenarioCapture] {caseId}: {directory}; shield={peakPixels} px");
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator AnnieR_FromScenario_RendersSummonAndFirstAttack()
        {
            PrepareCapture("annie-r", previewOnly: true);
            yield return new EnterPlayMode();
            yield return CaptureAnnieSummon("annie-r", expectAttack: true);
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator AnnieR_WithoutTarget_RendersSummonAndNaturalTimeout()
        {
            PrepareCapture("annie-r-tibbers-timeout", previewOnly: true);
            yield return new EnterPlayMode();
            yield return CaptureAnnieSummon("annie-r-tibbers-timeout", expectAttack: false);
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator AnnieBasicAttack_DoesNotPlayTibbersImpact()
        {
            const string caseId = "annie-basic-attack";
            PrepareCapture(caseId, previewOnly: true);
            yield return new EnterPlayMode();
            var capture = new AttachedEffectPlayback(caseId, TibbersHitVfxId);
            yield return capture.Observe();
            Assert.That(capture.FindFrame(MobaExecutionKind.DamageApply, 30030101), Is.GreaterThanOrEqualTo(0));
            Assert.That(capture.RequestFrames, Is.Empty);
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator AnnieBasicAttack_FromScenario_RendersOriginalImpact()
        {
            const string caseId = "annie-basic-attack";
            PrepareCapture(caseId, previewOnly: true);
            yield return new EnterPlayMode();
            var capture = new AttachedEffectPlayback(caseId, 90033017);
            var impact = new PreviewEffectSample("annie-impact", "Void_Annie_AA_Hit", 90033017);
            yield return capture.Observe(impact);

            var hit = capture.FindFrame(MobaExecutionKind.DamageApply, 30030101);
            Assert.That(hit, Is.GreaterThanOrEqualTo(0), capture.Directory);
            Assert.That(capture.RequestFrames, Has.Count.EqualTo(1), capture.Directory);
            Assert.That(impact.FirstFrame, Is.InRange(hit, hit + 4), capture.Directory);
            Assert.That(Vector2.Distance(new Vector2(7f, 0f), new Vector2(impact.StartPosition.x, impact.StartPosition.z)),
                Is.LessThan(.15f), capture.Directory);
            impact.AssertPlayed(capture.Directory);
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator VeigarW_FromScenario_RendersWarningFallAndImpact()
        {
            PrepareCapture("veigar-w", previewOnly: true);
            yield return new EnterPlayMode();
            yield return CaptureVeigarArea("veigar-w", 42040301, 90034016, "Warning", "Falling", "Impact");
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator VeigarE_FromScenario_RendersWarningAndCage()
        {
            PrepareCapture("veigar-e", previewOnly: true);
            yield return new EnterPlayMode();
            yield return CaptureVeigarArea("veigar-e", 42040402, 90034017, "Warning", "Boundary");
        }

        private static IEnumerator CaptureAnnieSummon(string caseId, bool expectAttack)
        {
            const int summonId = 30030502;
            var directory = CreatePreviewEvidenceDirectory(caseId);
            var samples = new List<string>();
            var summons = new List<int>();
            var landing = new PreviewEffectSample("landing", "Void_Annie_Tibbers_Spawn", 90033006);
            var impact = new PreviewEffectSample("tibbers-impact", "Void_Annie_Tibbers_Hit", TibbersHitVfxId);
            var impactRequests = new List<(int frame, int source, int target)>();
            var damageResults = new List<(int frame, int source, int target)>();
            var subscriptions = new List<System.IDisposable>();
            MobaTraceRegistry trace = null;
            var petId = 0;
            var petPixels = 0;
            var lastFrame = -1;
            var castPose = -1;
            var attackPose = -1;
            var despawnFrame = -1;
            yield return WaitForPlayback(caseId, () =>
            {
                if (!TryGetPreviewContext(lastFrame, out var context)) return;
                lastFrame = context.LastFrame;
                Assert.IsTrue(context.Session.TryGetWorld(out var world));
                trace ??= world.Services.Resolve<MobaTraceRegistry>();
                if (subscriptions.Count == 0)
                {
                    var bus = world.Services.Resolve<IEventBus>();
                    var play = new EventKey<PresentationEventArgs>(TriggeringIdUtil.GetEventEid(MobaPresentationTriggering.Events.Play));
                    var afterDamage = new EventKey<DamageResult>(TriggeringIdUtil.GetEventEid(DamagePipelineEvents.AfterApply));
                    subscriptions.Add(bus.Subscribe(play, request =>
                    {
                        if (request.TemplateId != TibbersHitVfxId) return;
                        Assert.That(request.Targets, Is.EqualTo(new[] { request.TargetActorId }), directory);
                        impactRequests.Add((context.LastFrame, request.SourceActorId, request.TargetActorId));
                    }));
                    subscriptions.Add(bus.Subscribe(afterDamage, result =>
                    {
                        if (result.ReasonParam == TibbersBasicAttackSkillId)
                            damageResults.Add((context.LastFrame, result.AttackerActorId, result.TargetActorId));
                    }));
                }
                var active = Object.FindObjectsOfType<Transform>();
                Assert.IsFalse(active.Any(node => node.name == "Void_Annie_R_Summon(Clone)" || node.name == "Vfx_90033005"),
                    "The showcase bear must not overlap the real summon.");
                landing.Observe(active, lastFrame, directory, samples);
                impact.Observe(active, lastFrame, directory, samples);
                if (castPose < 0 && Object.FindObjectsOfType<Animator>().Any(animator => animator.isInitialized &&
                    animator.GetCurrentAnimatorStateInfo(0).shortNameHash == Animator.StringToHash("Void_Annie_Model_SpellR")))
                {
                    castPose = lastFrame;
                    Capture(directory, "annie-cast", lastFrame, samples);
                }

                var alive = world.Services.Resolve<MobaSummonService>().TryGetSummons(context.LocalActorId, summons, summonId);
                if (alive)
                {
                    Assert.That(summons, Has.Count.EqualTo(1), directory);
                    Assert.That(petId, Is.EqualTo(0).Or.EqualTo(summons[0]), directory);
                    petId = summons[0];
                }
                if (petId == 0) return;
                var shell = active.FirstOrDefault(node => node.name == $"Shell_Summon_{summonId}_{petId}");
                if (!alive && shell == null && despawnFrame < 0) despawnFrame = lastFrame;
                if (shell == null) return;
                if (petPixels <= 20 && shell.GetComponentInChildren<SkinnedMeshRenderer>() != null)
                    petPixels = System.Math.Max(petPixels, Capture(directory, "tibbers-model", lastFrame,
                        samples, previousBest: petPixels, isolatedBranch: shell));
                var petAnimator = shell.GetComponentInChildren<Animator>();
                if (attackPose < 0 && petAnimator != null && petAnimator.isInitialized &&
                    petAnimator.GetCurrentAnimatorClipInfo(0).Any(clip => clip.clip.name.Contains("Attack")))
                {
                    attackPose = lastFrame;
                    Capture(directory, "tibbers-first-attack", lastFrame, samples);
                }
            }, () => subscriptions.ForEach(subscription => subscription.Dispose()), previewOnly: true);

            Assert.IsNotNull(trace, directory);
            var spawn = TraceFrames(trace, MobaExecutionKind.SummonSpawn, summonId);
            var hits = TraceFrames(trace, MobaExecutionKind.DamageApply, TibbersBasicAttackSkillId, petId);
            samples.Add($"castPose={castPose} spawn=[{string.Join(",", spawn)}] pet={petId} " +
                $"attackPose={attackPose} hits=[{string.Join(",", hits)}] despawn={despawnFrame} modelPixels={petPixels}");
            samples.Add(landing.Summary);
            samples.Add(impact.Summary);
            samples.Add($"impact requests=[{string.Join(",", impactRequests)}]");
            File.WriteAllLines(Path.Combine(directory, "samples.txt"), samples);
            Assert.That(TraceFrames(trace, MobaExecutionKind.SkillCast, 30030501), Has.Length.EqualTo(1), directory);
            Assert.That(spawn, Has.Length.EqualTo(1), directory);
            Assert.That(castPose, Is.InRange(0, spawn[0] + 2), directory);
            Assert.That(petId, Is.Positive, directory);
            Assert.That(petPixels, Is.GreaterThan(20), directory);
            landing.AssertPlayed(directory);
            Assert.That(Vector2.Distance(new Vector2(7f, 1f), new Vector2(landing.StartPosition.x, landing.StartPosition.z)),
                Is.LessThan(.15f), directory);
            Assert.That(landing.FirstFrame, Is.InRange(spawn[0], spawn[0] + 4), directory);
            Assert.That(impactRequests, Is.EqualTo(damageResults), directory);
            Assert.That(damageResults, Has.Count.EqualTo(hits.Length), directory);
            if (expectAttack)
            {
                Assert.That(attackPose, Is.GreaterThanOrEqualTo(spawn[0]), directory);
                Assert.That(hits.Length, Is.GreaterThanOrEqualTo(2), directory);
                Assert.That(hits[0], Is.InRange(spawn[0], attackPose + 24), directory);
                Assert.That(hits.Skip(1).Select((frame, index) => frame - hits[index]), Is.All.EqualTo(24), directory);
                impact.AssertVisible(directory);
                Assert.That(impact.FirstFrame, Is.InRange(hits[0], hits[0] + 4), directory);
                Assert.That(Vector2.Distance(new Vector2(7f, 1f), new Vector2(impact.StartPosition.x, impact.StartPosition.z)),
                    Is.LessThan(.15f), directory);
                Assert.That(despawnFrame, Is.EqualTo(-1), directory);
            }
            else
            {
                Assert.That(attackPose, Is.EqualTo(-1), directory);
                Assert.That(hits, Is.Empty, directory);
                Assert.That(impact.FirstFrame, Is.EqualTo(-1), directory);
                Assert.That(TraceFrames(trace, MobaExecutionKind.SummonDeath, summonId), Is.Empty, directory);
                Assert.That(despawnFrame - spawn[0], Is.InRange(420, 424), directory);
            }
        }

        private static IEnumerator CaptureVeigarArea(string caseId, int areaId, int assetId, params string[] stageNames)
        {
            var directory = CreatePreviewEvidenceDirectory(caseId);
            var samples = new List<string>();
            var stages = stageNames.Select(name => new AreaStageSample(name)).ToArray();
            MobaTraceRegistry trace = null;
            Transform area = null;
            var lastFrame = -1;
            var returned = -1;
            yield return WaitForPlayback(caseId, () =>
            {
                if (!TryGetPreviewContext(lastFrame, out var context)) return;
                lastFrame = context.LastFrame;
                Assert.IsTrue(context.Session.TryGetWorld(out var world));
                trace ??= world.Services.Resolve<MobaTraceRegistry>();
                var active = Object.FindObjectsOfType<Transform>().Where(node => node.name == $"Vfx_{assetId}").ToArray();
                if (active.Length == 0)
                {
                    if (area != null && !area.gameObject.activeInHierarchy && returned < 0) returned = lastFrame;
                    return;
                }
                if (area == null) area = active[0];
                Assert.That(active, Is.EqualTo(new[] { area }), directory);
                Assert.That(Vector2.Distance(new Vector2(7f, 1f), new Vector2(area.position.x, area.position.z)), Is.LessThan(.1f), directory);
                var envelope = area.Find("Boundary/Cage/CageEnveloppe");
                if (envelope != null && envelope.gameObject.activeInHierarchy)
                    Assert.That(envelope.GetComponent<MeshRenderer>().bounds.size.x, Is.InRange(6f, 8f), directory);
                foreach (var stage in stages) stage.Observe(area, lastFrame, directory, samples);
            }, previewOnly: true);

            Assert.IsNotNull(trace, directory);
            var spawn = TraceFrames(trace, MobaExecutionKind.AreaSpawn, areaId);
            var expiry = TraceFrames(trace, MobaExecutionKind.AreaExpire, areaId);
            var damage = TraceFrames(trace, MobaExecutionKind.DamageApply, 30040301);
            samples.Add($"spawn=[{string.Join(",", spawn)}] expiry=[{string.Join(",", expiry)}] damage=[{string.Join(",", damage)}] returned={returned}");
            samples.AddRange(stages.Select(stage => $"{stage.Name}: frames={stage.First}..{stage.Last} pixels={stage.Pixels}"));
            File.WriteAllLines(Path.Combine(directory, "samples.txt"), samples);
            Assert.That(spawn, Has.Length.EqualTo(1), directory);
            Assert.That(expiry, Has.Length.EqualTo(1), directory);
            Assert.That(stages[0].First, Is.InRange(spawn[0], spawn[0] + 4), directory);
            foreach (var stage in stages) Assert.That(stage.Pixels, Is.GreaterThan(20), $"{stage.Name}: {directory}");
            if (stages.Length == 3)
            {
                Assert.That(damage, Has.Length.EqualTo(1), directory);
                Assert.That(stages[1].First, Is.GreaterThan(stages[0].First), directory);
                Assert.That(stages[1].Last, Is.LessThan(stages[2].First), directory);
                Assert.That(stages[2].First, Is.InRange(damage[0] - 4, damage[0] + 4), directory);
            }
            else
            {
                Assert.That(stages[0].Last, Is.LessThan(stages[1].First), directory);
                Assert.That(stages[1].Last, Is.GreaterThanOrEqualTo(expiry[0] - 4), directory);
            }
            Assert.That(returned, Is.GreaterThanOrEqualTo(expiry[0]), directory);
        }

        private sealed class AreaStageSample
        {
            public AreaStageSample(string name) => Name = name;
            public string Name { get; }
            public int First { get; private set; } = -1;
            public int Last { get; private set; } = -1;
            public int Pixels { get; private set; }

            public void Observe(Transform area, int frame, string directory, List<string> samples)
            {
                var branch = area.Find(Name);
                Assert.NotNull(branch, $"Missing {Name}; no placeholder asset is accepted. See {directory}.");
                if (!branch.gameObject.activeInHierarchy) return;
                if (First < 0) First = frame;
                Last = frame;
                if (Pixels > 20 || !branch.GetComponentsInChildren<Renderer>().Any(renderer => renderer.enabled)) return;
                Pixels = System.Math.Max(Pixels, Capture(directory, Name, frame, samples,
                    previousBest: Pixels, isolatedBranch: branch));
            }
        }

        private static int[] TraceFrames(MobaTraceRegistry trace, MobaExecutionKind kind, int configId, int source = 0)
            => trace.GetNodesByKind((int)kind).Where(node => node.Metadata?.ConfigId == configId &&
                (source == 0 || node.Metadata.SourceActorId == source)).Select(node => node.CreatedFrame).OrderBy(frame => frame).ToArray();

        private static string CreatePreviewEvidenceDirectory(string caseId)
        {
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../local/Logs/moba-presentation-scenarios",
                System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"), caseId));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static bool TryGetPreviewContext(int lastFrame, out BattleContext context)
        {
            context = null;
            if (!GameEntry.IsInitialized || !GameEntry.Instance.TryGet(out context) || context.LocalActorId <= 0 ||
                Camera.main == null || context.Session == null || context.LastFrame == lastFrame) return false;
            Assert.IsNull(GameObject.Find("BattleHudCanvas"), "Pure preview must not open the battle HUD.");
            return true;
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator AnnieE_WithLivingTibbers_RendersTwoIndependentShields()
        {
            PrepareCapture("annie-e-tibbers");
            yield return new EnterPlayMode();
            yield return CaptureTibbersShields();
        }

        private static IEnumerator CaptureTibbersShields()
        {
            const string caseId = "annie-e-tibbers";
            const int shieldVfxId = 90033004;

            var directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../local/Logs/moba-presentation-scenarios",
                System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"), caseId));
            Directory.CreateDirectory(directory);
            var samples = new List<string>();
            var summons = new List<int>();
            var heroPixels = 0;
            var petPixels = 0;
            var lastFrame = -1;
            var petActorId = 0;
            var heroActorId = 0;
            var sawUnshieldedPet = false;
            MobaShieldService shields = null;
            yield return WaitForPlayback(caseId, () =>
            {
                if (Camera.main == null || !GameEntry.IsInitialized ||
                    !GameEntry.Instance.TryGet(out BattleContext context) || context.LastFrame == lastFrame ||
                    context.Session == null || !context.Session.TryGetWorld(out var world)) return;
                lastFrame = context.LastFrame;
                heroActorId = context.LocalActorId;
                if (!world.Services.Resolve<MobaSummonService>().TryGetSummons(heroActorId, summons, 30030502)) return;
                Assert.AreEqual(1, summons.Count);
                petActorId = summons[0];
                shields ??= world.Services.Resolve<MobaShieldService>();
                var heroShielded = shields.GetPresentationSnapshot(heroActorId).HasShield;
                sawUnshieldedPet |= !heroShielded && !shields.GetPresentationSnapshot(petActorId).HasShield;
                if (!heroShielded) return;
                Assert.IsTrue(shields.GetPresentationSnapshot(petActorId).HasShield);

                var active = Object.FindObjectsOfType<BattleVfxPoolableTag>()
                    .Where(tag => tag.VfxId == shieldVfxId && tag.gameObject.activeInHierarchy).ToArray();
                Assert.LessOrEqual(active.Length, 2);
                if (active.Length != 2) return;

                var registry = world.Services.Resolve<MobaActorRegistry>();
                Assert.IsTrue(registry.TryGet(petActorId, out var pet));
                var position = pet.transform.Value.Position;
                var petVisualPosition = new Vector3(position.X, position.Y, position.Z);
                var ordered = active.OrderBy(tag => (tag.transform.position - petVisualPosition).sqrMagnitude).ToArray();
                Assert.Less(Vector3.Distance(ordered[0].transform.position, petVisualPosition), 2f);
                heroPixels = System.Math.Max(heroPixels, Capture(directory, "hero-shield", lastFrame,
                    samples, new[] { ordered[1] }, previousBest: heroPixels));
                petPixels = System.Math.Max(petPixels, Capture(directory, "tibbers-shield", lastFrame,
                    samples, new[] { ordered[0] }, previousBest: petPixels));
            });

            File.WriteAllLines(Path.Combine(directory, "samples.txt"), samples);
            Assert.IsTrue(sawUnshieldedPet, "Expected to observe the summon before casting E.");
            Assert.Greater(heroPixels, 20, $"No hero shield pixels. See {directory}.");
            Assert.Greater(petPixels, 20, $"No Tibbers shield pixels. See {directory}.");
            Assert.IsFalse(shields.GetPresentationSnapshot(heroActorId).HasShield);
            Assert.IsFalse(shields.GetPresentationSnapshot(petActorId).HasShield);
            Assert.IsFalse(Object.FindObjectsOfType<BattleVfxPoolableTag>()
                .Any(tag => tag.VfxId == shieldVfxId && tag.gameObject.activeInHierarchy));
            Debug.Log($"[SkillScenarioCapture] {caseId}: {directory}; hero={heroPixels} px; Tibbers={petPixels} px");
        }

        /// <summary>
        /// 使用本地战斗的移动管线穿墙，分别验证墙体和减益的生命周期。
        /// </summary>
        [UnityTest]
        public IEnumerator KarthusW_FromScenario_AllowsCrossingAndKeepsDebuffAfterWallExpires()
        {
            Prepare("karthus-w");
            yield return new EnterPlayMode();
            yield return VerifyWallCrossing();
        }

        private static IEnumerator VerifyWallCrossing()
        {
            const int wallTemplate = 42060301;
            const int wallDebuff = 30060391;
            const int wallVfxId = 90036015;
            const float wallZ = -6f;
            ActorEntity enemy = null;
            ActorEntity ally = null;
            MobaTraceRegistry trace = null;
            var baseSpeed = 0f;
            var baseMagicDefense = 0f;
            var allySpeed = 0f;
            var allyDefense = 0f;
            var slowedSpeed = 0f;
            var phase = 0;
            var sawWallVfx = false;
            var debuffOutlivedWall = false;
            yield return WaitForPlayback("karthus-w", () =>
            {
                if (!GameEntry.IsInitialized || !GameEntry.Instance.TryGet(out BattleContext context) ||
                    !context.CanSubmitGameplayInput || context.Session == null || !context.Session.TryGetWorld(out var world)) return;
                trace ??= world.Services.Resolve<MobaTraceRegistry>();
                var wall = trace.GetNodesByKind((int)MobaExecutionKind.AreaSpawn)
                    .FirstOrDefault(node => node.Metadata?.ConfigId == wallTemplate);
                if (!wall.IsValid) return;
                sawWallVfx |= IsWallVfxActive();
                if (enemy == null)
                {
                    var map = world.Services.Resolve<MobaPlayerActorMapService>();
                    var registry = world.Services.Resolve<MobaActorRegistry>();
                    Assert.IsTrue(map.TryGetActorId(new PlayerId("preview_target"), out var enemyId) && registry.TryGet(enemyId, out enemy));
                    Assert.IsTrue(map.TryGetActorId(new PlayerId("preview_ally"), out var allyId) && registry.TryGet(allyId, out ally));
                    baseSpeed = new MobaAttrs(enemy).MoveSpeed;
                    baseMagicDefense = new MobaAttrs(enemy).MagicDefense;
                    allySpeed = new MobaAttrs(ally).MoveSpeed;
                    allyDefense = new MobaAttrs(ally).MagicDefense;
                    Assert.Greater(baseMagicDefense, 0f);
                    Assert.IsFalse(HasDebuff(enemy));
                }

                if (phase == 0 && context.LastFrame >= wall.CreatedFrame + 60)
                {
                    enemy.ReplaceMoveInput(0f, 1f);
                    ally.ReplaceMoveInput(0f, 1f);
                    phase = 1;
                }
                else if (phase == 1 && enemy.transform.Value.Position.Z > wallZ + 0.8f)
                {
                    Assert.Greater(ally.transform.Value.Position.Z, wallZ + 0.8f);
                    Assert.IsTrue(HasDebuff(enemy));
                    Assert.IsFalse(HasDebuff(ally));
                    Assert.AreEqual(allySpeed, new MobaAttrs(ally).MoveSpeed, .01f);
                    Assert.AreEqual(allyDefense, new MobaAttrs(ally).MagicDefense, .01f);
                    slowedSpeed = new MobaAttrs(enemy).MoveSpeed;
                    Assert.Less(slowedSpeed, baseSpeed);
                    Assert.AreEqual(baseMagicDefense * .75f, new MobaAttrs(enemy).MagicDefense, .01f);
                    enemy.ReplaceMoveInput(0f, -1f);
                    ally.ReplaceMoveInput(0f, 0f);
                    phase = 2;
                }
                else if (phase == 2 && enemy.transform.Value.Position.Z < wallZ - 0.8f)
                {
                    enemy.ReplaceMoveInput(0f, 0f);
                    Assert.IsTrue(HasDebuff(enemy));
                    phase = 3;
                }

                if (!debuffOutlivedWall && trace.GetNodesByKind((int)MobaExecutionKind.AreaExpire)
                        .Any(node => node.Metadata?.ConfigId == wallTemplate))
                {
                    Assert.IsTrue(HasDebuff(enemy), "The wall expires before this late contact's debuff.");
                    Assert.AreEqual(baseMagicDefense * .75f, new MobaAttrs(enemy).MagicDefense, .01f);
                    Assert.Greater(new MobaAttrs(enemy).MoveSpeed, slowedSpeed, "The slow must decay while active.");
                    Assert.Less(new MobaAttrs(enemy).MoveSpeed, baseSpeed);
                    debuffOutlivedWall = true;
                }
            });

            Assert.AreEqual(3, phase, "The enemy must walk across the wall and back without being blocked.");
            Assert.IsTrue(sawWallVfx);
            Assert.IsFalse(IsWallVfxActive());
            Assert.IsTrue(debuffOutlivedWall);
            Assert.AreEqual(1, trace.GetNodesByKind((int)MobaExecutionKind.BuffApply)
                .Count(node => node.Metadata?.ConfigId == wallDebuff));
            Assert.IsFalse(HasDebuff(enemy));
            Assert.AreEqual(baseSpeed, new MobaAttrs(enemy).MoveSpeed, .01f);
            Assert.AreEqual(baseMagicDefense, new MobaAttrs(enemy).MagicDefense, .01f);

            static bool HasDebuff(ActorEntity actor) => actor.hasBuffs && actor.buffs.Active != null &&
                actor.buffs.Active.Any(buff => buff != null && buff.BuffId == wallDebuff);
            static bool IsWallVfxActive() => Object.FindObjectsOfType<BattleVfxPoolableTag>()
                .Any(tag => tag.VfxId == wallVfxId && tag.gameObject.activeInHierarchy);
        }
        private static IEnumerator CaptureTravel(string caseId, int flightVfxId)
        {
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../local/Logs/moba-presentation-scenarios",
                System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"), caseId));
            Directory.CreateDirectory(directory);
            var samples = new List<string>();
            var lastFrame = -1;
            var peakPixels = 0;
            var firstPosition = (Vector3?)null;
            var traveled = false;
            yield return WaitForPlayback(caseId, () =>
            {
                if (Camera.main == null || !GameEntry.IsInitialized ||
                    !GameEntry.Instance.TryGet(out BattleContext context) || context.LastFrame == lastFrame) return;
                lastFrame = context.LastFrame;
                var flight = Object.FindObjectsOfType<BattleVfxPoolableTag>()
                    .FirstOrDefault(tag => tag.VfxId == flightVfxId && tag.gameObject.activeInHierarchy);
                if (flight == null) return;

                firstPosition ??= flight.transform.position;
                traveled |= (flight.transform.position - firstPosition.Value).sqrMagnitude > 1f;
                peakPixels = System.Math.Max(peakPixels, Capture(directory, "w-travel", lastFrame,
                    samples, new[] { flight }, previousBest: peakPixels));
            });
            File.WriteAllLines(Path.Combine(directory, "samples.txt"), samples);
            Assert.IsTrue(traveled, $"The configured bolt did not travel. See {directory}.");
            Assert.Greater(peakPixels, 20, $"The bolt rendered no visible pixels. See {directory}.");
            Debug.Log($"[SkillScenarioCapture] {caseId}: {directory}; travel={peakPixels} px");
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator LuxR_FromScenario_CapturesPresentation()
        {
            PrepareCapture("lux-r");
            yield return new EnterPlayMode();
            yield return CapturePlayback("lux-r", 30070501, 42070501, 90037015, 0, 5, 15, 24, 29, 30, 31, 33, 38, 45, 60, 75, 90);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.captureDeltaTime = 0f;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        private static void Prepare(string caseId, bool previewOnly = false)
        {
            var file = Path.GetFullPath(Path.Combine(Application.dataPath, "../../src",
                "AbilityKit.Demo.Moba.BattleScenario.Runner/Flows/SkillPresentation", caseId + ".battlescenario"));
            Assert.IsTrue(MobaBattleScenarioPreview.PreparePreview(file, previewOnly));
        }

        private static void PrepareCapture(string caseId, bool previewOnly = false)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Presentation evidence requires Unity with a graphics device.");
            Prepare(caseId, previewOnly);
        }

        private static IEnumerator CaptureGragasDash(string caseId, bool expectHit)
        {
            var dash = new PreviewEffectSample("dash", "E_Dash", 90035003);
            var impact = new PreviewEffectSample("impact", "Gragas_StarGuardian_E_Impact", 90035004);
            var result = new AttachedEffectPlayback(caseId, impact.AssetId);
            yield return result.Observe(dash, impact);
            dash.AssertPlayed(result.Directory);
            Assert.That(dash.Moved, Is.True, "Dash effect did not move with the caster.");
            Assert.That(result.CastingClips, Does.Contain("Gragas_Model_SpellE"));
            Assert.That(result.RequestFrames, Has.Count.EqualTo(expectHit ? 1 : 0), result.Directory);
            var hit = result.FindFrame(MobaExecutionKind.DamageApply, 30050401);
            if (expectHit)
            {
                impact.AssertPlayed(result.Directory);
                Assert.That(hit, Is.GreaterThanOrEqualTo(dash.FirstFrame));
                Assert.That(impact.FirstFrame, Is.GreaterThanOrEqualTo(hit));
            }
            else
            {
                Assert.That(hit, Is.EqualTo(-1));
                Assert.That(impact.FirstFrame, Is.EqualTo(-1));
            }
        }

        private sealed class AttachedEffectPlayback
        {
            private readonly string _caseId;
            private readonly int _contactAssetId;
            private MobaTraceRegistry _trace;

            public AttachedEffectPlayback(string caseId, int contactAssetId)
            {
                _caseId = caseId;
                _contactAssetId = contactAssetId;
                Directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../local/Logs/moba-presentation-scenarios",
                    System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"), caseId));
            }

            public string Directory { get; }
            public List<int> RequestFrames { get; } = new List<int>();
            public HashSet<string> CastingClips { get; } = new HashSet<string>();

            public int FindFrame(MobaExecutionKind kind, int configId, int afterFrame = -1) => _trace.GetNodesByKind((int)kind)
                .Where(node => node.Metadata?.ConfigId == configId && node.CreatedFrame > afterFrame)
                .Select(node => node.CreatedFrame).DefaultIfEmpty(-1).Min();

            public IEnumerator Observe(params PreviewEffectSample[] effects)
            {
                System.IO.Directory.CreateDirectory(Directory);
                var samples = new List<string>();
                System.IDisposable subscription = null;
                yield return WaitForPlayback(_caseId, () =>
                {
                    if (!GameEntry.IsInitialized || !GameEntry.Instance.TryGet(out BattleContext context) ||
                        context.LocalActorId <= 0 || Camera.main == null || context.Session == null ||
                        !context.Session.TryGetWorld(out var world)) return;
                    _trace ??= world.Services.Resolve<MobaTraceRegistry>();
                    if (subscription == null)
                    {
                        var play = new EventKey<PresentationEventArgs>(TriggeringIdUtil.GetEventEid(MobaPresentationTriggering.Events.Play));
                        subscription = world.Services.Resolve<IEventBus>().Subscribe(play, request =>
                        {
                            if (request.TemplateId == _contactAssetId) RequestFrames.Add(context.LastFrame);
                        });
                    }

                    foreach (var animator in Object.FindObjectsOfType<Animator>().Where(candidate => candidate.isInitialized &&
                        candidate.GetComponentInChildren<SkinnedMeshRenderer>() != null))
                    foreach (var clip in animator.GetCurrentAnimatorClipInfo(0))
                        CastingClips.Add(clip.clip.name);
                    var active = Object.FindObjectsOfType<Transform>();
                    foreach (var effect in effects) effect.Observe(active, context.LastFrame, Directory, samples);
                }, () => subscription?.Dispose(), previewOnly: true);
                samples.Add($"requests=[{string.Join(",", RequestFrames)}] clips=[{string.Join(",", CastingClips)}]");
                samples.AddRange(effects.Select(effect => effect.Summary));
                File.WriteAllLines(Path.Combine(Directory, "samples.txt"), samples);
                Assert.IsNotNull(_trace, Directory);
            }
        }

        private sealed class PreviewEffectSample
        {
            private readonly string _label;
            private readonly string _prefabName;
            private readonly int _minimumAgeFrames;
            private readonly int _maximumAgeFrames;
            private int _sampleFrame = -1;
            private int _pixels;
            private int _peakInstances;
            private int _activeCount;
            private Vector3 _start;

            public PreviewEffectSample(string label, string prefabName, int assetId,
                int minimumAgeFrames = 0, int maximumAgeFrames = int.MaxValue)
            {
                _label = label;
                _prefabName = prefabName + "(Clone)";
                AssetId = assetId;
                _minimumAgeFrames = minimumAgeFrames;
                _maximumAgeFrames = maximumAgeFrames;
            }

            public int AssetId { get; }
            public int FirstFrame { get; private set; } = -1;
            public int SampleFrame => _sampleFrame;
            public Vector3 StartPosition => _start;
            public bool Moved { get; private set; }
            public string Summary => $"{_label} first={FirstFrame} pixels={_pixels} moved={Moved} peak={_peakInstances} activeAtEnd={_activeCount}";

            public void Observe(Transform[] active, int frame, string directory, List<string> samples)
            {
                var matches = active.Where(node => node.name == _prefabName || node.name == "Vfx_" + AssetId).ToArray();
                _activeCount = matches.Length;
                _peakInstances = System.Math.Max(_peakInstances, _activeCount);
                if (_activeCount == 0) return;
                var instance = matches[0];
                if (FirstFrame < 0)
                {
                    FirstFrame = frame;
                    _start = instance.position;
                }
                Moved |= (instance.position - _start).sqrMagnitude > 0.04f;
                var age = frame - FirstFrame;
                if (age < _minimumAgeFrames || age > _maximumAgeFrames ||
                    _pixels > 20 || frame < _sampleFrame + 2 || !instance.GetComponentsInChildren<ParticleSystem>()
                    .Any(system => system.particleCount > 0)) return;
                _sampleFrame = frame;
                _pixels = System.Math.Max(_pixels, Capture(directory, _label, frame, samples,
                    previousBest: _pixels, isolatedBranch: instance));
            }

            public void AssertVisible(string directory)
            {
                Assert.That(FirstFrame, Is.GreaterThanOrEqualTo(0), $"{Summary}. See {directory}.");
                Assert.That(_pixels, Is.GreaterThan(20), $"{Summary}. See {directory}.");
            }

            public void AssertPlayed(string directory)
            {
                AssertVisible(directory);
                Assert.That(_peakInstances, Is.EqualTo(1), $"{Summary}. See {directory}.");
                Assert.That(_activeCount, Is.Zero, $"{Summary}. See {directory}.");
            }
        }

        private static IEnumerator CapturePlayback(string caseId, int skillId, int childId, int hitVfxId, params int[] offsets)
            => CapturePlayback(caseId, skillId, childId, hitVfxId, null, offsets);

        private static IEnumerator CapturePlayback(string caseId, int skillId, int childId, int hitVfxId,
            CastPresentationProbe castProbe, params int[] offsets)
        {
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../local/Logs/moba-presentation-scenarios",
                System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"), caseId));
            Directory.CreateDirectory(directory);
            var captures = new List<string>();
            var impact = new List<string>();
            var subscriptions = new List<System.IDisposable>();
            BattleVfxManager vfx = null;
            MobaTraceRegistry trace = null;
            var castFrame = -1;
            var sample = 0;
            var readyCaptured = false;
            var sawEmittingImpact = false;
            var peakActiveImpacts = 0;
            var impactPixels = 0;
            var finalImpactReturned = false;
            yield return WaitForPlayback(caseId, () =>
            {
                if (!GameEntry.IsInitialized || !GameEntry.Instance.TryGet(out BattleContext context) ||
                    context.LocalActorId <= 0 || !context.CanSubmitGameplayInput || Camera.main == null ||
                    context.Session == null || !context.Session.TryGetWorld(out var world)) return;

                trace ??= world.Services.Resolve<MobaTraceRegistry>();
                vfx ??= context.ViewVfxManager;
                if (subscriptions.Count == 0 && context.FrameSnapshots != null)
                    ObserveImpact(context, world.Services.Resolve<IEventBus>(), childId, hitVfxId, impact, subscriptions);
                if (castFrame < 0)
                {
                    var cast = trace.GetNodesByKind((int)MobaExecutionKind.SkillCast)
                        .FirstOrDefault(node => node.Metadata?.ConfigId == skillId);
                    if (cast.IsValid) castFrame = cast.CreatedFrame;
                }

                if (!readyCaptured && castFrame < 0 && Object.FindObjectsOfType<SkinnedMeshRenderer>().Length > 0)
                {
                    Capture(directory, "ready", context.LastFrame, captures);
                    readyCaptured = true;
                }
                if (castFrame >= 0) castProbe?.Observe(context.LastFrame, hitVfxId, directory, captures);
                if (castFrame < 0 || sample >= offsets.Length || context.LastFrame < castFrame + offsets[sample]) return;

                var frame = context.LastFrame;
                var instances = Object.FindObjectsOfType<BattleVfxPoolableTag>(true)
                    .Where(tag => tag.VfxId == hitVfxId).ToArray();
                var active = instances.Where(tag => tag.gameObject.activeInHierarchy).ToArray();
                peakActiveImpacts = System.Math.Max(peakActiveImpacts, active.Length);
                sawEmittingImpact |= active.Any(tag => tag.GetComponentsInChildren<ParticleSystem>()
                    .Any(system => system.particleCount > 0 && system.TryGetComponent<ParticleSystemRenderer>(out var renderer)
                        && renderer.enabled));
                finalImpactReturned = active.Length == 0 && instances.Length > 0;
                Capture(directory, $"cast-plus-{frame - castFrame:D3}", frame, captures, instances);
                if (castProbe != null && active.Length > 0 && impactPixels <= 20)
                    impactPixels = System.Math.Max(impactPixels, Capture(directory, "impact-visible", frame,
                        captures, active, previousBest: impactPixels));
                while (sample < offsets.Length && castFrame + offsets[sample] <= frame) sample++;
            }, () =>
            {
                foreach (var subscription in subscriptions) subscription.Dispose();
            });

            Assert.IsNotNull(trace);
            Assert.GreaterOrEqual(castFrame, 0, "No matching commit in the preview world.");
            Assert.AreEqual(offsets.Length, sample, "Preview ended before the last capture.");
            var kinds = new[] { MobaExecutionKind.SkillCast, MobaExecutionKind.ProjectileLaunch,
                MobaExecutionKind.ProjectileHit, MobaExecutionKind.AreaSpawn, MobaExecutionKind.AreaEnter,
                MobaExecutionKind.AreaExpire, MobaExecutionKind.DamageApply, MobaExecutionKind.PresentationPlay };
            var events = kinds.SelectMany(kind => trace.GetNodesByKind((int)kind))
                .Where(node => node.Metadata?.ConfigId == skillId || node.Metadata?.ConfigId == childId ||
                    node.Kind == (int)MobaExecutionKind.AreaSpawn || node.Kind == (int)MobaExecutionKind.AreaEnter ||
                    node.Kind == (int)MobaExecutionKind.AreaExpire || node.Kind == (int)MobaExecutionKind.PresentationPlay)
                .OrderBy(node => node.CreatedFrame)
                .Select(node => $"frame={node.CreatedFrame} {(MobaExecutionKind)node.Kind} {node.Metadata.ConfigId}");
            if (vfx != null && vfx.TryCreatePresentationRequest(hitVfxId, "impact-probe", default, out var request))
            {
                var prefab = Resources.Load<GameObject>(request.ResourceKey);
                impact.Add($"resource vfx={hitVfxId} key={request.ResourceKey} durationMs={request.DurationMs} " +
                    $"prefab={(prefab == null ? "missing" : prefab.name)} " +
                    $"particleSystems={(prefab == null ? 0 : prefab.GetComponentsInChildren<ParticleSystem>(true).Length)}");
            }
            else
            {
                impact.Add($"resource vfx={hitVfxId} not resolved by live VFX catalog");
            }
            if (castProbe != null) impact.Add(castProbe.Summarize(impactPixels));
            File.WriteAllLines(Path.Combine(directory, "samples.txt"), events.Concat(impact.Distinct()).Concat(captures));
            Assert.IsTrue(readyCaptured, "No pre-cast scene with a loaded character model was captured.");
            Assert.IsTrue(trace.GetNodesByKind((int)MobaExecutionKind.DamageApply)
                .Any(node => node.Metadata?.ConfigId == skillId), "No matching damage in the preview world.");
            Assert.IsTrue(sawEmittingImpact, $"VFX {hitVfxId} never emitted particles from an active pooled prefab. See {directory}.");
            Assert.AreEqual(1, peakActiveImpacts, $"Expected only one impact per cast. See {directory}.");
            Assert.IsTrue(finalImpactReturned, $"VFX {hitVfxId} did not return to the pool by the final sample. See {directory}.");
            if (castProbe != null)
            {
                castProbe.AssertStages(directory);
                Assert.Greater(impactPixels, 20, $"Impact renderer contributed no visible pixels. See {directory}.");
            }
            Debug.Log($"[SkillScenarioCapture] {caseId}: {directory} ({captures.Count} images; review pixels separately)");
        }

        /// <summary>
        /// 检查带弹道和落地阶段的技能在渲染场景中的播放顺序；不以源展示脚本驱动战斗。
        /// </summary>
        private sealed class CastPresentationProbe
        {
            private readonly string _castClip;
            private readonly int _flightVfxId;
            private readonly int _groundVfxId;
            private readonly int _chargeVfxId;
            private readonly string _deployedClip;
            private readonly string _detonationClip;
            private readonly string _impactBranch;
            private readonly Vector2? _impactPosition;
            private int _cast = -1;
            private int _flight = -1;
            private int _ground = -1;
            private int _impact = -1;
            private int _peakGroundInstances;
            private int _charge = -1;
            private int _chargePixels;
            private bool _groundRetiredDuringCharge;
            private bool _chargeReturnedByEnd;
            private Vector3 _flightStart;
            private bool _flightMoved;
            private int _firstFlightInstance;
            private bool _sameFlightInstance = true;
            private bool _flightReturnedByEnd;
            private Vector3 _deployedPosition;
            private int _deployed = -1;
            private int _detonation = -1;
            private int _impactBranchPixels;
            private int _lastImpactBranchSampleFrame = -1;
            private float _impactPositionError;

            public CastPresentationProbe(string castClip, int flightVfxId, int groundVfxId = 0, int chargeVfxId = 0,
                string deployedClip = null, string detonationClip = null, string impactBranch = null, Vector2? impactPosition = null)
            {
                _castClip = castClip;
                _flightVfxId = flightVfxId;
                _groundVfxId = groundVfxId;
                _chargeVfxId = chargeVfxId;
                _deployedClip = deployedClip;
                _detonationClip = detonationClip;
                _impactBranch = impactBranch;
                _impactPosition = impactPosition;
            }

            public void Observe(int frame, int impactVfxId, string directory, List<string> captures)
            {
                if (_cast < 0 && Object.FindObjectsOfType<Animator>().Any(animator => animator.isInitialized &&
                        animator.GetComponentInChildren<SkinnedMeshRenderer>() != null &&
                        animator.GetCurrentAnimatorClipInfo(0).Any(info => info.clip.name == _castClip)))
                    _cast = frame;

                var groundInstances = 0;
                var flightInstances = 0;
                var chargeInstances = new List<BattleVfxPoolableTag>();
                foreach (var tag in Object.FindObjectsOfType<BattleVfxPoolableTag>())
                {
                    if (tag.VfxId != _flightVfxId && tag.VfxId != _groundVfxId && tag.VfxId != _chargeVfxId && tag.VfxId != impactVfxId) continue;
                    if (!tag.GetComponentsInChildren<Renderer>().Any(renderer => renderer.enabled)) continue;

                    if (tag.VfxId == _flightVfxId)
                    {
                        flightInstances++;
                        if (_flight < 0)
                        {
                            _flight = frame;
                            _flightStart = tag.transform.position;
                            _firstFlightInstance = tag.GetInstanceID();
                        }
                        _flightMoved |= (tag.transform.position - _flightStart).sqrMagnitude > 0.01f;
                        if (_deployedClip != null)
                        {
                            _sameFlightInstance &= _firstFlightInstance == tag.GetInstanceID();
                            foreach (var animator in tag.GetComponentsInChildren<Animator>().Where(candidate => candidate.isInitialized))
                            foreach (var clip in animator.GetCurrentAnimatorClipInfo(0))
                            {
                                if (_deployed < 0 && clip.clip.name == _deployedClip)
                                {
                                    _deployed = frame;
                                    _deployedPosition = tag.transform.position;
                                }
                                if (_detonation < 0 && clip.clip.name == _detonationClip)
                                    _detonation = frame;
                            }
                        }
                    }
                    else if (tag.VfxId == _groundVfxId)
                    {
                        if (_ground < 0) _ground = frame;
                        groundInstances++;
                    }
                    else if (tag.VfxId == _chargeVfxId)
                    {
                        if (_charge < 0) _charge = frame;
                        chargeInstances.Add(tag);
                    }
                    else if (tag.VfxId == impactVfxId)
                    {
                        if (_impact < 0)
                        {
                            _impact = frame;
                            var position = tag.transform.position;
                            if (_impactPosition.HasValue)
                                _impactPositionError = Vector2.Distance(_impactPosition.Value, new Vector2(position.x, position.z));
                            else if (_deployed >= 0)
                                _impactPositionError = Vector2.Distance(new Vector2(_deployedPosition.x, _deployedPosition.z),
                                    new Vector2(position.x, position.z));
                        }
                        if (_impactBranch != null && frame >= _lastImpactBranchSampleFrame + 2)
                        {
                            var branch = tag.GetComponentsInChildren<Transform>().SingleOrDefault(node => node.name == _impactBranch);
                            if (branch != null)
                            {
                                _lastImpactBranchSampleFrame = frame;
                                _impactBranchPixels = System.Math.Max(_impactBranchPixels, Capture(directory, "impact-branch-visible", frame,
                                    captures, new[] { tag }, previousBest: _impactBranchPixels, isolatedBranch: branch));
                            }
                        }
                    }
                }
                _flightReturnedByEnd = _flight >= 0 && flightInstances == 0;
                _peakGroundInstances = System.Math.Max(_peakGroundInstances, groundInstances);
                _groundRetiredDuringCharge |= chargeInstances.Count > 0 && groundInstances == 0;
                _chargeReturnedByEnd = _charge >= 0 && chargeInstances.Count == 0;
                if (_chargePixels <= 20 && chargeInstances.Any(tag => tag.GetComponentsInChildren<ParticleSystem>()
                    .Any(system => system.particleCount > 0)))
                    _chargePixels = System.Math.Max(_chargePixels, Capture(directory, "charge-visible", frame,
                        captures, chargeInstances.ToArray(), previousBest: _chargePixels));
            }

            public string Summarize(int impactPixels) =>
                $"stages clip={_castClip} cast={_cast} flight={_flight} flightMoved={_flightMoved} " +
                $"ground={_ground} peakGroundInstances={_peakGroundInstances} charge={_charge} chargePixels={_chargePixels} " +
                $"groundRetiredDuringCharge={_groundRetiredDuringCharge} chargeReturnedByEnd={_chargeReturnedByEnd} " +
                $"deployed={_deployed} detonation={_detonation} sameFlightInstance={_sameFlightInstance} flightReturned={_flightReturnedByEnd} " +
                $"impact={_impact} impactPixels={impactPixels} impactBranchPixels={_impactBranchPixels} impactPositionError={_impactPositionError:F3}";

            public void AssertStages(string directory)
            {
                Assert.GreaterOrEqual(_cast, 0, $"Model never played {_castClip}. See {directory}.");
                Assert.IsTrue(_flight >= _cast && _flightMoved, $"No moving throw VFX after cast. See {directory}.");
                Assert.Greater(_impact, _flight, $"No impact VFX after flight. See {directory}.");
                Assert.IsTrue(_flightReturnedByEnd, $"Projectile VFX did not return to the pool. See {directory}.");
                if (_deployedClip != null)
                {
                    Assert.Greater(_deployed, _flight, $"Barrel never played {_deployedClip} after flight. See {directory}.");
                    Assert.Greater(_detonation, _deployed, $"Barrel never played {_detonationClip} after deployment. See {directory}.");
                    Assert.IsTrue(_sameFlightInstance, $"Barrel changed instances across phases. See {directory}.");
                }
                if (_impactPosition.HasValue || _deployedClip != null)
                    Assert.Less(_impactPositionError, 0.15f, $"Impact did not play at the landing position. See {directory}.");
                if (_impactBranch != null)
                    Assert.Greater(_impactBranchPixels, 20, $"Impact branch {_impactBranch} contributed no visible pixels. See {directory}.");
                if (_groundVfxId == 0) return;
                Assert.Greater(_ground, _flight, $"No grounded VFX after flight. See {directory}.");
                Assert.Greater(_charge, _ground, $"No independent charge VFX after grounding. See {directory}.");
                Assert.AreEqual(1, _peakGroundInstances, $"Ground trap was duplicated. See {directory}.");
                Assert.IsTrue(_groundRetiredDuringCharge, $"Ground trap remained throughout the charge. See {directory}.");
                Assert.Greater(_chargePixels, 20, $"Charge renderer contributed no visible pixels. See {directory}.");
                Assert.Greater(_impact, _charge, $"No impact VFX after charging. See {directory}.");
                Assert.IsTrue(_chargeReturnedByEnd, $"Charge VFX did not return to the pool. See {directory}.");
            }
        }

        private static void ObserveImpact(BattleContext context, IEventBus bus, int childId, int vfxId,
            List<string> records, List<System.IDisposable> subscriptions)
        {
            records.Add($"impact observer attached frame={context.LastFrame} vfx={vfxId}");
            var play = new EventKey<PresentationEventArgs>(TriggeringIdUtil.GetEventEid(MobaPresentationTriggering.Events.Play));
            subscriptions.Add(bus.Subscribe(play, request =>
            {
                if (request.TemplateId == vfxId)
                    records.Add($"frame={context.LastFrame} PlayRequest template={vfxId} source={request.SourceActorId} " +
                        $"target={request.TargetActorId} targets={string.Join(",", request.Targets ?? System.Array.Empty<int>())} " +
                        $"positions={request.Positions?.Length ?? 0} durationMs={request.DurationMsOverride}");
            }));
            subscriptions.Add(context.FrameSnapshots.Subscribe<PresentationCueData[]>(MobaOpCodes.Snapshot.PresentationCue, (_, entries) =>
            {
                foreach (var entry in entries)
                    if (entry.VfxId == vfxId || entry.TemplateId == vfxId)
                        records.Add($"frame={context.LastFrame} Cue {entry.CueKind}/{entry.Stage} sourceFrame={entry.SourceFrame} " +
                            $"template={entry.TemplateId} vfx={entry.VfxId} source={entry.SourceActorId} target={entry.TargetActorId} " +
                            $"targets={entry.Targets?.Count ?? 0} positions={entry.Positions?.Count ?? 0}");
            }));
            subscriptions.Add(context.FrameSnapshots.Subscribe<ProjectileEventData[]>(MobaOpCodes.Snapshot.ProjectileEvent, (_, entries) =>
            {
                foreach (var entry in entries)
                    if (entry.TemplateId == childId)
                        records.Add($"frame={context.LastFrame} ProjectileEvent {entry.Kind} sourceFrame={entry.SnapshotFrame} " +
                            $"template={entry.TemplateId} actor={entry.ProjectileActorId} hit={entry.HitCollider} " +
                            $"position=({entry.X:F2},{entry.Y:F2},{entry.Z:F2})");
            }));
        }

        private static int Capture(string directory, string name, int frame, List<string> samples,
            BattleVfxPoolableTag[] instances = null, int? previousBest = null, Transform isolatedBranch = null)
        {
            var camera = Camera.main;
            var render = RenderTexture.GetTemporary(1280, 720, 24);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var texture = new Texture2D(render.width, render.height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = render;
                camera.Render();
                RenderTexture.active = render;
                texture.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0);
                texture.Apply();
                var changed = 0;
                if (previousBest.HasValue)
                {
                    var pixels = texture.GetPixels32();
                    var candidates = isolatedBranch == null ? instances.SelectMany(tag => tag.GetComponentsInChildren<Renderer>())
                        : isolatedBranch.GetComponentsInChildren<Renderer>();
                    var renderers = candidates
                        .Where(renderer => renderer.enabled).Distinct().ToArray();
                    try
                    {
                        foreach (var renderer in renderers) renderer.enabled = false;
                        camera.Render();
                        RenderTexture.active = render;
                        texture.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0);
                        var hidden = texture.GetPixels32();
                        for (var pixel = 0; pixel < pixels.Length; pixel++)
                        {
                            var before = pixels[pixel];
                            var after = hidden[pixel];
                            if (System.Math.Abs(before.r - after.r) + System.Math.Abs(before.g - after.g) +
                                System.Math.Abs(before.b - after.b) > 24) changed++;
                        }
                    }
                    finally
                    {
                        foreach (var renderer in renderers) renderer.enabled = true;
                    }
                    if (changed > previousBest.Value)
                    {
                        texture.SetPixels32(pixels);
                        texture.Apply();
                    }
                }
                if (!previousBest.HasValue || changed > previousBest.Value)
                    File.WriteAllBytes(Path.Combine(directory, name + ".png"), texture.EncodeToPNG());
                var animators = Object.FindObjectsOfType<Animator>().Where(animator => animator.isInitialized)
                    .Select(animator => $"{animator.name}:" + string.Join("+",
                        animator.GetCurrentAnimatorClipInfo(0).Select(info => info.clip.name)));
                var effects = Object.FindObjectsOfType<Transform>()
                    .Where(transform => transform.name.StartsWith("Vfx_", System.StringComparison.Ordinal) ||
                        transform.name.StartsWith("AreaVfx_", System.StringComparison.Ordinal) ||
                        transform.name.StartsWith("VfxPlaceholder_", System.StringComparison.Ordinal))
                    .OrderBy(transform => transform.name)
                    .Select(transform => $"{transform.name}@{transform.position}");
                var impacts = (instances ?? System.Array.Empty<BattleVfxPoolableTag>())
                    .Select(tag => $"{tag.name}@{tag.transform.position} " +
                        $"active={tag.gameObject.activeInHierarchy} parent={tag.transform.parent?.name} " +
                        $"particles={tag.GetComponentsInChildren<ParticleSystem>().Sum(system => system.particleCount)} " +
                        $"renderers={tag.GetComponentsInChildren<Renderer>().Count(renderer => renderer.enabled)}");
                samples.Add($"{name} frame={frame} camera={camera.transform.position} fov={camera.fieldOfView:F1} " +
                    $"animations=[{string.Join(", ", animators)}] vfx=[{string.Join(", ", effects)}] " +
                    $"instances=[{string.Join(", ", impacts)}]" + (previousBest.HasValue ? $" changedPixels={changed}" : ""));
                return changed;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(texture);
                RenderTexture.ReleaseTemporary(render);
            }
        }

        private static IEnumerator WaitForPlayback(string caseId, System.Action observeFrame = null, System.Action cleanup = null,
            bool previewOnly = false)
        {
            Time.captureDeltaTime = 1f / 60f;
            var finished = false;
            var finishMessage = previewOnly
                ? $"[SkillScenarioPreview] 纯预览完成：{caseId}。已暂停；停止 Play Mode 后可重新播放。"
                : $"[SkillScenarioPreview] 播放完成：{caseId}。画面需在 Game 视图复核。";
            Application.LogCallback onLog = (message, _, __) => finished |= message == finishMessage;
            Application.logMessageReceived += onLog;
            try
            {
                var playbackDeadline = Time.realtimeSinceStartup + 130f;
                while (!finished && Time.realtimeSinceStartup < playbackDeadline)
                {
                    observeFrame?.Invoke();
                    yield return null;
                }
                Assert.IsTrue(finished, $"Preview did not complete: {caseId}.");
            }
            finally
            {
                Application.logMessageReceived -= onLog;
                cleanup?.Invoke();
            }
        }
    }
}
#endif

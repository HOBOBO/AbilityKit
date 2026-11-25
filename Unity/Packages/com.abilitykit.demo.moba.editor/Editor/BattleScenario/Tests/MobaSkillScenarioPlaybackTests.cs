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
using AbilityKit.Game.Battle.Moba.Config;
using AbilityKit.Game.Battle.Testing;
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

        [Test]
        public void ActorReplay_UsesFormalModelWithIndependentMaterialsAndObservedPoses()
        {
            const int modelId = 3005;
            var prefab = new BattleViewModelPrefabResolver().Resolve(MobaConfigLoader.LoadDefault(), modelId).Prefab;
            using var preview = BattleActorReplayPreview.Create(modelId);
            Assert.IsNotNull(preview, "The recorded hero must resolve to its formal model.");
            var authority = new Pose(new Vector3(7f, 0f, -2f), Quaternion.Euler(0f, 45f, 0f));
            var prediction = new Pose(new Vector3(7f, 0f, 1.354f), Quaternion.identity);
            preview.ApplyPoses(null, prediction);
            Assert.IsFalse(preview.AuthorityModel.activeSelf);
            Assert.AreEqual(prediction.position, preview.PredictionModel.transform.position);

            preview.ApplyPoses(authority, prediction);
            Assert.AreEqual(authority.position, preview.AuthorityModel.transform.position);
            Assert.That(Quaternion.Angle(authority.rotation, preview.AuthorityModel.transform.rotation), Is.LessThan(0.01f));
            static Material[] MeshSlots(GameObject actor) => actor.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                .SelectMany(renderer => renderer.sharedMaterials).ToArray();

            var predictionSlots = MeshSlots(preview.PredictionModel);
            CollectionAssert.AreEqual(MeshSlots(prefab), predictionSlots);
            var authoritySlots = MeshSlots(preview.AuthorityModel);
            Assert.AreEqual(predictionSlots.Length, authoritySlots.Length);
            var authorityMaterial = authoritySlots.First(material => material != null);
            CollectionAssert.DoesNotContain(predictionSlots, authorityMaterial);
            for (var slot = 0; slot < authoritySlots.Length; slot++)
                Assert.AreSame(predictionSlots[slot] == null ? null : authorityMaterial, authoritySlots[slot]);
            Assert.AreEqual("Standard", authorityMaterial.shader.name);
            Assert.That(Vector4.Distance(new Color(0.28f, 0.30f, 0.33f), authorityMaterial.color), Is.LessThan(0.0001f));
            Assert.IsNull(authorityMaterial.mainTexture);
            Assert.AreEqual(0f, authorityMaterial.GetFloat("_Glossiness"));

            preview.ApplyPoses(authority, null);
            Assert.IsTrue(preview.AuthorityModel.activeSelf);
            Assert.IsFalse(preview.PredictionModel.activeSelf);
        }

        [UnityTest]
        public IEnumerator LuxR_FromScenario_ConsumesLocalInput()
        {
            Prepare("lux-r");
            yield return new EnterPlayMode();
            yield return WaitForPlayback("lux-r");
            Assert.AreNotEqual("moba.skill_preview", GameEntry.Instance.Get<GameFlowDomain>().CurrentBattleProfileId);
            Assert.IsNotNull(GameObject.Find("BattleHudCanvas"));
            AssertLocalDebugPanelVisibility(true);
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
                AssertLocalDebugPanelVisibility(false);
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

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator JhinR_FourShots_RenderDistinctMuzzlesAndProjectiles()
        {
            const string caseId = "jhin-r";
            PrepareCapture(caseId, previewOnly: true);
            yield return new EnterPlayMode();
            yield return CaptureWeaponStages(caseId, new[] { 90013004, 90031010 },
                new[] { 90013005, 90013006 }, new[] { 90013007, 90013008 },
                new[] { 32010401, 32010402 }, new[] { 3, 1 },
                new[] { "Jhin_R_Cast", "Jhin_R_Last_Cast" });
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator JinxQ_RangeEdge_RendersBothMuzzlesAndProjectiles()
        {
            const string caseId = "jinx-q-range-edge";
            PrepareCapture(caseId, previewOnly: true);
            yield return new EnterPlayMode();
            yield return CaptureWeaponStages(caseId, new[] { 90032001, 90032003 },
                new[] { 90032002, 90032005 }, new[] { 90032004 },
                new[] { 32020201, 32020202 }, new[] { 1, 1 },
                new[] { "Jinx_AA_Minigun_Cast", "Jinx_AA_Rocket_Cast" });
        }

        private static IEnumerator CaptureWeaponStages(string caseId, int[] muzzleIds, int[] flightIds,
            int[] impactIds, int[] projectileIds, int[] expectedShots, string[] muzzlePrefabs)
        {
            var directory = CreatePreviewEvidenceDirectory(caseId);
            var ids = muzzleIds.Concat(flightIds).Concat(impactIds).ToArray();
            var effects = ids.Select((id, index) => new PreviewEffectSample(id.ToString(),
                index < muzzlePrefabs.Length ? muzzlePrefabs[index] : string.Empty, id)).ToArray();
            var starts = ids.ToDictionary(id => id, _ => new List<int>());
            var previouslyActive = new HashSet<int>();
            var samples = new List<string>();
            var lastFrame = -1;
            MobaTraceRegistry trace = null;
            yield return WaitForPlayback(caseId, () =>
            {
                if (!TryGetPreviewContext(lastFrame, out var context) || !context.Session.TryGetWorld(out var world)) return;
                lastFrame = context.LastFrame;
                trace ??= world.Services.Resolve<MobaTraceRegistry>();
                var active = Object.FindObjectsOfType<Transform>();
                for (var index = 0; index < ids.Length; index++)
                {
                    var id = ids[index];
                    var count = active.Count(effects[index].Matches);
                    Assert.That(count, Is.LessThanOrEqualTo(1), $"Duplicate VFX {id} at {lastFrame}. {directory}");
                    if (count == 0) previouslyActive.Remove(id);
                    else if (previouslyActive.Add(id)) starts[id].Add(lastFrame);
                    effects[index].Observe(active, lastFrame, directory, samples);
                }
            }, previewOnly: true);

            for (var index = 0; index < ids.Length; index++)
                samples.Add($"{effects[index].Summary} starts=[{string.Join(",", starts[ids[index]])}]");
            File.WriteAllLines(Path.Combine(directory, "samples.txt"), samples);
            Assert.IsNotNull(trace, directory);
            for (var mode = 0; mode < projectileIds.Length; mode++)
            {
                var launch = TraceFrames(trace, MobaExecutionKind.ProjectileLaunch, projectileIds[mode]);
                var hit = TraceFrames(trace, MobaExecutionKind.ProjectileHit, projectileIds[mode]);
                Assert.That(hit, Has.Length.EqualTo(expectedShots[mode]), directory);
                Assert.That(starts[muzzleIds[mode]], Has.Count.EqualTo(hit.Length), directory);
                Assert.That(starts[flightIds[mode]], Has.Count.EqualTo(hit.Length), directory);
                for (var shot = 0; shot < hit.Length; shot++)
                {
                    var muzzle = starts[muzzleIds[mode]][shot];
                    var flight = starts[flightIds[mode]][shot];
                    Assert.IsTrue(launch.Any(frame => Mathf.Abs(frame - flight) <= 3), directory);
                    Assert.That(muzzle, Is.InRange(flight - 3, hit[shot]), directory);
                    Assert.That(flight, Is.LessThan(hit[shot]), directory);
                }
                Assert.IsTrue(effects[muzzleIds.Length + mode].Moved, directory);
            }
            foreach (var effect in effects) effect.AssertPlayed(directory);
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

        [UnityTest]
        public IEnumerator JhinQ_FourImpacts_RenderInOrder()
        {
            const string caseId = "jhin-q-four-hits";
            PrepareCapture(caseId, previewOnly: true);
            yield return new EnterPlayMode();
            yield return CaptureJhinQImpacts(caseId, 4);
        }

        [UnityTest]
        public IEnumerator JhinQ_ShortChain_DoesNotRenderLaterImpacts()
        {
            const string caseId = "jhin-q-short-chain";
            PrepareCapture(caseId, previewOnly: true);
            yield return new EnterPlayMode();
            yield return CaptureJhinQImpacts(caseId, 1);
        }

        private static IEnumerator CaptureJhinQImpacts(string caseId, int expectedHits)
        {
            var ids = new[] { 90013016, 90013022, 90013023, 90013024 };
            var impacts = new List<PresentationCueData>();
            var matched = new HashSet<int>();
            yield return CaptureScenarioVfx(caseId, ids.Take(expectedHits).ToArray(), (_, effects) =>
            {
                for (var index = 0; index < ids.Length; index++)
                {
                    var instances = effects.Where(effect => effect.VfxId == ids[index]).ToArray();
                    Assert.That(instances, Has.Length.LessThanOrEqualTo(1), "A hit visual must not spawn twice.");
                    if (instances.Length == 0) continue;
                    Assert.Less(index, expectedHits);
                    var impact = impacts.FirstOrDefault(cue => cue.VfxId == ids[index]);
                    if (impact.Positions == null || impact.Positions.Count != 1) continue;
                    var point = impact.Positions[0];
                    var actual = instances[0].transform.position;
                    Assert.That(Vector2.Distance(new Vector2(actual.x, actual.z), new Vector2(point.X, point.Z)),
                        Is.LessThan(0.15f));
                    matched.Add(ids[index]);
                }
            }, cues => impacts.AddRange(cues.Where(cue => cue.Domain == PresentationCueDomain.Projectile &&
                cue.Stage == PresentationCueStage.Executed && cue.TemplateId == 32010101)),
                forbiddenIds: ids.Skip(expectedHits).ToArray());
            impacts = impacts.GroupBy(cue => (cue.InstanceId, cue.SourceFrame, cue.LifecycleReason, cue.VfxId, cue.TargetActorId))
                .Select(group => group.First()).ToList();
            CollectionAssert.AreEqual(ids.Take(expectedHits).ToArray(), impacts.Select(cue => cue.VfxId).ToArray());
            CollectionAssert.AreEqual(Enumerable.Range(1, expectedHits), impacts.Select(cue => cue.LifecycleReason));
            Assert.AreEqual(1, impacts.Select(cue => cue.InstanceId).Distinct().Count());
            Assert.AreEqual(expectedHits, impacts.Select(cue => cue.TargetActorId).Distinct().Count());
            CollectionAssert.AreEquivalent(ids.Take(expectedHits), matched);
        }

        [UnityTest]
        public IEnumerator LuxW_RoundTrip_RendersShieldsOnTheCorrectActors()
        {
            const string caseId = "lux-w";
            PrepareCapture(caseId, previewOnly: true);
            yield return new EnterPlayMode();
            yield return CaptureLuxWRoundTrip(caseId);
        }

        private static IEnumerator CaptureLuxWRoundTrip(string caseId)
        {
            var ally = 0;
            var amounts = new List<float>();
            var cues = new List<PresentationCueData>();
            var casterEffects = new HashSet<int>();
            var allyShieldVisible = false;
            yield return CaptureScenarioVfx(caseId, new[] { 90037017, 90037018, 90037019 }, (context, effects) =>
            {
                if (context.Session == null || !context.Session.TryGetWorld(out var world)) return;
                if (!world.Services.Resolve<MobaPlayerActorMapService>().TryGetActorId(new PlayerId("preview_target"), out ally)) return;
                var shields = world.Services.Resolve<MobaShieldService>();
                var remaining = shields.GetTotalRemaining(ally);
                if (remaining > 0 && !amounts.Contains(remaining)) amounts.Add(remaining);
                foreach (var effect in effects.Where(effect => effect.VfxId == 90037018 || effect.VfxId == 90037019))
                {
                    var position = effect.transform.position;
                    var center = new Vector2(position.x, position.z);
                    if (Vector2.Distance(center, new Vector2(7, -4)) < 0.15f &&
                        shields.GetTotalRemaining(context.LocalActorId) > 0f)
                        casterEffects.Add(effect.VfxId);
                    if (effect.VfxId == 90037019 && remaining > 0f &&
                        Vector2.Distance(center, new Vector2(7, 1)) < 0.15f)
                        allyShieldVisible = true;
                }
            }, entries => cues.AddRange(entries.Where(cue => cue.Stage == PresentationCueStage.Executed &&
                (cue.VfxId == 90037018 || cue.VfxId == 90037019))));
            cues = cues.GroupBy(cue => cue.Sequence).Select(group => group.First()).ToList();
            Assert.That(amounts, Has.Count.EqualTo(2));
            Assert.That(amounts[1], Is.EqualTo(amounts[0] * 2).Within(0.01f));
            Assert.AreEqual(2, cues.Count(cue => cue.VfxId == 90037019 && cue.TargetActorId == ally));
            CollectionAssert.AreEquivalent(new[] { 90037018, 90037019 }, casterEffects);
            Assert.IsTrue(allyShieldVisible, "ShieldUp must also render at the ally contact target.");
        }

        [UnityTest]
        public IEnumerator KarthusR_MultiTarget_RendersWarningAndImpact()
        {
            const string caseId = "karthus-r-multi-target";
            PrepareCapture(caseId, previewOnly: true);
            yield return new EnterPlayMode();
            yield return CaptureKarthusR(caseId, interrupted: false);
        }

        [UnityTest]
        public IEnumerator KarthusR_Interrupted_ReleasesWarningsWithoutImpact()
        {
            const string caseId = "karthus-r-interrupted";
            PrepareCapture(caseId, previewOnly: true);
            yield return new EnterPlayMode();
            yield return CaptureKarthusR(caseId, interrupted: true);
        }

        private static IEnumerator CaptureKarthusR(string caseId, bool interrupted)
        {
            var warnings = new HashSet<int>();
            var released = new HashSet<int>();
            var impactCenters = new HashSet<Vector2>();
            var ids = interrupted ? new[] { 90036013 } : new[] { 90036013, 90036012 };
            yield return CaptureScenarioVfx(caseId, ids, (_, effects) =>
            {
                foreach (var impact in effects.Where(effect => effect.VfxId == 90036012))
                {
                    var position = impact.transform.position;
                    impactCenters.Add(new Vector2(position.x, position.z));
                }
            }, entries =>
            {
                foreach (var cue in entries.Where(cue => cue.VfxId == 90036013 || cue.TemplateId == 90036013))
                {
                    if (cue.Stage == PresentationCueStage.Started) warnings.Add(cue.TargetActorId);
                    if (cue.Stage == PresentationCueStage.Removed || cue.Stage == PresentationCueStage.Expired)
                        released.Add(cue.TargetActorId);
                }
            }, forbiddenIds: interrupted ? new[] { 90036012 } : null);
            Assert.That(warnings, Has.Count.EqualTo(2));
            CollectionAssert.AreEquivalent(warnings, released);
            if (interrupted) Assert.IsEmpty(impactCenters);
            else
            {
                Assert.AreEqual(2, impactCenters.Count);
                Assert.IsTrue(impactCenters.Any(point => Vector2.Distance(point, new Vector2(7, 5)) < 0.15f));
                Assert.IsTrue(impactCenters.Any(point => Vector2.Distance(point, new Vector2(11, 2)) < 0.15f));
            }
        }

        private static IEnumerator CaptureScenarioVfx(string caseId, int[] ids,
            System.Action<BattleContext, BattleVfxPoolableTag[]> observe,
            System.Action<PresentationCueData[]> observeCues, int[] forbiddenIds = null)
        {
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../local/Logs/moba-presentation-scenarios",
                System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"), caseId));
            Directory.CreateDirectory(directory);
            var samples = new List<string>();
            var pixels = ids.ToDictionary(id => id, _ => 0);
            var forbidden = new HashSet<int>(forbiddenIds ?? System.Array.Empty<int>());
            System.IDisposable subscription = null;
            var lastFrame = -1;
            yield return WaitForPlayback(caseId, () =>
            {
                if (!GameEntry.IsInitialized || !GameEntry.Instance.TryGet(out BattleContext context)) return;
                subscription ??= context.FrameSnapshots.Subscribe<PresentationCueData[]>(MobaOpCodes.Snapshot.PresentationCue,
                    (_, entries) => observeCues(entries));
                if (context.LastFrame == lastFrame || Camera.main == null) return;
                lastFrame = context.LastFrame;
                var active = Object.FindObjectsOfType<BattleVfxPoolableTag>()
                    .Where(tag => tag.gameObject.activeInHierarchy && (pixels.ContainsKey(tag.VfxId) ||
                        forbidden.Contains(tag.VfxId))).ToArray();
                Assert.IsFalse(active.Any(tag => forbidden.Contains(tag.VfxId)));
                observe(context, active);
                foreach (var group in active.GroupBy(tag => tag.VfxId))
                {
                    if (pixels[group.Key] > 20) continue;
                    pixels[group.Key] = System.Math.Max(pixels[group.Key], Capture(directory,
                        group.Key.ToString(), lastFrame, samples, group.ToArray(), pixels[group.Key]));
                }
            }, () => subscription?.Dispose(), previewOnly: true);
            File.WriteAllLines(Path.Combine(directory, "samples.txt"), samples);
            foreach (var id in ids) Assert.Greater(pixels[id], 20, $"VFX {id} had no visible pixels. See {directory}.");
            Assert.IsFalse(Object.FindObjectsOfType<BattleVfxPoolableTag>()
                .Any(tag => tag.gameObject.activeInHierarchy && pixels.ContainsKey(tag.VfxId)), directory);
        }

        [UnityTest]
        public IEnumerator JhinAttacks_FromScenario_WalkBetweenShotsAndResumeAfterReload()
        {
            const string caseId = "jhin-basic-attack-locomotion";
            Prepare(caseId);
            yield return new EnterPlayMode();
            yield return VerifyJhinAttackLocomotion(caseId);
        }

        private static IEnumerator VerifyJhinAttackLocomotion(string caseId)
        {
            ActorEntity caster = null;
            MobaTraceRegistry trace = null;
            var walked = new bool[4];
            var stopped = new bool[4];
            var movingAfterShot = -1;
            var lastFrame = -1;
            var sawReload = false;
            var movedDuringReload = false;
            var reloadCast = 0L;
            var positionBeforeMove = 0f;
            var walkHash = Animator.StringToHash("Walk");
            yield return WaitForPlayback(caseId, () =>
            {
                if (!GameEntry.IsInitialized || !GameEntry.Instance.TryGet(out BattleContext context) ||
                    context.LocalActorId <= 0 || context.Session == null ||
                    !context.Session.TryGetWorld(out var world) || context.LastFrame == lastFrame) return;
                lastFrame = context.LastFrame;
                if (caster == null && !world.Services.Resolve<MobaActorRegistry>().TryGet(context.LocalActorId, out caster)) return;
                if (!caster.hasCharacterHfsm) return;
                trace ??= world.Services.Resolve<MobaTraceRegistry>();
                var runtime = caster.characterHfsm.Runtime;
                var action = runtime.Action;
                var next = (int)runtime.ResolveParameter(10);
                var currentX = caster.transform.Value.Position.X;
                if (action.SkillId == 30010605 && action.CastInstanceId > 0)
                {
                    if (!sawReload)
                    {
                        sawReload = true;
                        reloadCast = action.CastInstanceId;
                        positionBeforeMove = currentX;
                        caster.ReplaceMoveInput(-1f, 0f);
                        Assert.IsFalse(world.Services.Resolve<MobaCombatRulesService>().CanAttack(context.LocalActorId));
                    }
                    else
                    {
                        Assert.AreEqual(reloadCast, action.CastInstanceId, "Movement must not restart reload.");
                        if (Mathf.Abs(currentX - positionBeforeMove) > .2f)
                        {
                            movedDuringReload = true;
                            caster.ReplaceMoveInput(0f, 0f);
                        }
                    }
                    return;
                }
                var shot = next >= 2 && next <= 4 ? next - 2 : sawReload && next == 0 ? 3 : -1;
                if (shot < 0 || stopped[shot] || runtime.FactIsCasting) return;
                if (movingAfterShot != shot)
                {
                    if (!action.Path.EndsWith("/idle", System.StringComparison.Ordinal)) return;
                    var expectedSkill = shot == 3 ? 30010601 : 30010602 + shot;
                    var loadout = world.Services.Resolve<MobaSkillLoadoutService>();
                    Assert.IsTrue(loadout.TryGetSkillId(context.LocalActorId, 5, out var effective));
                    Assert.AreEqual(expectedSkill, effective);
                    positionBeforeMove = currentX;
                    movingAfterShot = shot;
                    caster.ReplaceMoveInput(shot % 2 == 0 ? 1f : -1f, 0f);
                }
                else if (!walked[shot])
                {
                    if (Mathf.Abs(currentX - positionBeforeMove) <= .1f ||
                        !action.Path.EndsWith("/moving", System.StringComparison.Ordinal)) return;
                    if (!Object.FindObjectsOfType<Animator>().Any(animator => animator.isInitialized &&
                        animator.GetCurrentAnimatorStateInfo(0).shortNameHash == walkHash)) return;
                    Assert.AreEqual(0, action.CastInstanceId);
                    walked[shot] = true;
                    caster.ReplaceMoveInput(0f, 0f);
                }
                else if (action.Path.EndsWith("/idle", System.StringComparison.Ordinal))
                {
                    stopped[shot] = true;
                }
            }, () =>
            {
                if (caster != null && caster.hasMoveInput) caster.ReplaceMoveInput(0f, 0f);
            });
            Assert.IsTrue(walked.All(value => value), "Expected real displacement and Walk after every shot/reload.");
            Assert.IsTrue(stopped.All(value => value), "Stopping should return to Idle without losing the next round.");
            Assert.IsTrue(movedDuringReload, "Movement must not be blocked by the reload action.");
            Assert.IsNotNull(trace);
            for (var id = 30010601; id <= 30010605; id++)
                Assert.AreEqual(1, trace.GetNodesByKind((int)MobaExecutionKind.SkillCast)
                    .Count(node => node.Metadata?.ConfigId == id), $"Cast count for {id}");
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
        public IEnumerator JhinE_ChargesFromScenario_RendersAndReleasesAllFourThrows()
        {
            const string caseId = "jhin-e-charges";
            PrepareCapture(caseId, previewOnly: true);
            yield return new EnterPlayMode();
            yield return CaptureJhinTrapCharges(caseId);
        }

        private static IEnumerator CaptureJhinTrapCharges(string caseId)
        {
            const int skillId = 30010301;
            var position = new Vector2(0f, -6f);
            var stages = new[] { (Id: 90013003, Label: "flight"), (Id: 90013019, Label: "ground"),
                (Id: 90031011, Label: "charge"), (Id: 90013018, Label: "explosion") };
            var directory = CreatePreviewEvidenceDirectory(caseId);
            var samples = new List<string>();
            var castFrames = new List<int>();
            var activations = stages.ToDictionary(stage => stage.Id, _ => new List<TrapVfxActivation>());
            var active = new Dictionary<BattleVfxPoolableTag, TrapVfxActivation>();
            MobaTraceRegistry trace = null;
            var lastFrame = -1;
            var playingCast = false;
            yield return WaitForPlayback(caseId, () =>
            {
                if (!TryGetPreviewContext(lastFrame, out var context) || !context.Session.TryGetWorld(out var world)) return;
                lastFrame = context.LastFrame;
                trace ??= world.Services.Resolve<MobaTraceRegistry>();

                var playing = Object.FindObjectsOfType<Animator>().Any(animator => animator.isInitialized &&
                    animator.GetComponentsInChildren<SkinnedMeshRenderer>().Any(renderer => renderer.enabled) &&
                    animator.GetCurrentAnimatorClipInfo(0).Any(info => info.clip.name == "Jhin_Model_SpellE"));
                if (playing && !playingCast)
                {
                    castFrames.Add(lastFrame);
                    Capture(directory, $"throw-{castFrames.Count}-cast", lastFrame, samples);
                }
                playingCast = playing;

                foreach (var tag in active.Keys.Where(tag => tag == null || !tag.gameObject.activeInHierarchy).ToArray())
                {
                    active[tag].ReturnFrame = lastFrame;
                    active.Remove(tag);
                }
                foreach (var tag in Object.FindObjectsOfType<BattleVfxPoolableTag>())
                {
                    if (!activations.TryGetValue(tag.VfxId, out var history)) continue;
                    if (!active.TryGetValue(tag, out var activation))
                    {
                        var label = stages.First(stage => stage.Id == tag.VfxId).Label;
                        activation = new TrapVfxActivation($"throw-{history.Count + 1}-{label}", lastFrame, tag.transform.position);
                        history.Add(activation);
                        active.Add(tag, activation);
                    }
                    activation.Observe(tag, lastFrame, directory, samples);
                }
            }, previewOnly: true);

            Capture(directory, "finished", lastFrame, samples);
            File.WriteAllLines(Path.Combine(directory, "samples.txt"), samples.Concat(activations.Values.SelectMany(history => history)
                .Select(sample => $"{sample.Name}: start={sample.StartFrame} return={sample.ReturnFrame} " +
                                  $"position={sample.Position} travel={sample.Travel:F2} visiblePixels={sample.Pixels}")));
            Assert.IsNotNull(trace, directory);
            var launches = trace.GetNodesByKind((int)MobaExecutionKind.EffectExecution)
                .Where(node => node.Metadata?.ConfigId == skillId).Select(node => node.CreatedFrame).OrderBy(frame => frame).ToArray();
            Assert.That(launches, Has.Length.EqualTo(4), directory);
            Assert.That(castFrames, Has.Count.EqualTo(launches.Length), directory);
            Assert.That(active, Is.Empty, $"Trap VFX did not return before preview ended. {directory}");
            Assert.That(EditorApplication.isPaused, Is.True, "Pure preview must pause at completion.");
            foreach (var stage in stages)
                Assert.That(activations[stage.Id], Has.Count.EqualTo(launches.Length), $"{stage.Label}: {directory}");

            for (var index = 0; index < launches.Length; index++)
            {
                var chain = stages.Select(stage => activations[stage.Id][index]).ToArray();
                Assert.That(castFrames[index], Is.InRange(launches[index] - 1, launches[index] + 6), directory);
                Assert.That(chain[0].StartFrame, Is.GreaterThanOrEqualTo(castFrames[index]), directory);
                for (var stageIndex = 1; stageIndex < chain.Length; stageIndex++)
                    Assert.That(chain[stageIndex].StartFrame, Is.GreaterThan(chain[stageIndex - 1].StartFrame), directory);
                Assert.That(chain[0].Travel, Is.GreaterThan(0.1f), directory);
                Assert.That(chain[1].ReturnFrame, Is.InRange(chain[2].StartFrame - 1, chain[2].StartFrame + 1), directory);
                foreach (var sample in chain)
                {
                    Assert.That(sample.ReturnFrame, Is.GreaterThan(sample.StartFrame), sample.Name);
                    Assert.That(sample.Pixels, Is.GreaterThan(20), $"{sample.Name} contributed no visible pixels. {directory}");
                }
                foreach (var sample in chain.Skip(1))
                    Assert.That(Vector2.Distance(position, new Vector2(sample.Position.x, sample.Position.z)),
                        Is.LessThan(0.15f), $"{sample.Name} did not render at the trap center. {directory}");
            }
            Debug.Log($"[SkillScenarioCapture] {caseId}: {directory}; {launches.Length} rendered throws.");
        }

        private sealed class TrapVfxActivation
        {
            public string Name { get; }
            public int StartFrame { get; }
            public Vector3 Position { get; }
            public int ReturnFrame { get; set; } = -1;
            public float Travel { get; private set; }
            public int Pixels { get; private set; }

            public TrapVfxActivation(string name, int frame, Vector3 position)
            {
                Name = name;
                StartFrame = frame;
                Position = position;
            }

            public void Observe(BattleVfxPoolableTag tag, int frame, string directory, List<string> samples)
            {
                Travel = Mathf.Max(Travel, Vector3.Distance(Position, tag.transform.position));
                if (Pixels <= 20 && tag.GetComponentsInChildren<Renderer>().Any(renderer => renderer.enabled))
                    Pixels = Mathf.Max(Pixels, Capture(directory, Name, frame, samples, new[] { tag }, previousBest: Pixels));
            }
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
        public IEnumerator AnnieR_TargetRetention_RecordsTurnsAndReturnsToOwner()
        {
            const string caseId = "annie-r-tibbers-target-retention";
            PrepareCapture(caseId, previewOnly: true);
            yield return new EnterPlayMode();
            yield return CaptureTibbersTurns(caseId);
        }

        private static IEnumerator CaptureTibbersTurns(string caseId)
        {
            var directory = CreatePreviewEvidenceDirectory(caseId);
            var samples = new List<string>();
            var summons = new List<int>();
            var lastFrame = -1;
            var lastSharpTurn = -100;
            var previousYaw = 0f;
            var peakZ = float.NegativeInfinity;
            var startZ = float.NaN;
            var finalZ = float.NaN;
            var quickReversals = 0;
            var petId = 0;
            System.IDisposable subscription = null;
            yield return WaitForPlayback(caseId, () =>
            {
                if (!TryGetPreviewContext(lastFrame, out var context) || !context.Session.TryGetWorld(out var world)) return;
                if (!world.Services.Resolve<MobaSummonService>().TryGetSummons(context.LocalActorId, summons, 30030502)) return;
                Assert.That(summons, Has.Count.EqualTo(1), directory);
                petId = summons[0];
                var shell = GameObject.Find($"Shell_Summon_30030502_{petId}")?.transform;
                if (shell == null || !world.Services.Resolve<MobaActorRegistry>().TryGet(petId, out var pet)) return;
                subscription ??= world.Services.Resolve<IEventBus>().Subscribe(
                    new EventKey<DamageResult>(TriggeringIdUtil.GetEventEid(DamagePipelineEvents.AfterApply)), result =>
                    {
                        if (result.AttackerActorId == petId)
                            samples.Add($"{context.LastFrame}: attack target={result.TargetActorId}");
                    });
                var pose = pet.transform.Value;
                var yaw = shell.eulerAngles.y;
                if (lastFrame >= 0 && Mathf.Abs(Mathf.DeltaAngle(previousYaw, yaw)) >= 90f)
                {
                    if (context.LastFrame - lastSharpTurn <= 4) quickReversals++;
                    lastSharpTurn = context.LastFrame;
                }
                if (float.IsNaN(startZ)) startZ = shell.position.z;
                peakZ = Mathf.Max(peakZ, shell.position.z);
                finalZ = shell.position.z;
                previousYaw = yaw;
                lastFrame = context.LastFrame;
                samples.Add($"{lastFrame}: authority={pose.Position}/{pose.Rotation} view={shell.position} yaw={yaw:F2}");
            }, () => subscription?.Dispose(), previewOnly: true);
            samples.Add($"startZ={startZ:F2} peakZ={peakZ:F2} finalZ={finalZ:F2} rapidReversals={quickReversals}");
            File.WriteAllLines(Path.Combine(directory, "samples.txt"), samples);
            Assert.That(peakZ, Is.GreaterThan(startZ + 0.5f), directory);
            Assert.That(finalZ, Is.LessThan(peakZ - 1f), directory);
            Assert.That(quickReversals, Is.Zero, directory);
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
        public IEnumerator VeigarQ_TwoTargets_RenderSeparateImpactsAndRetireProjectile()
        {
            const string caseId = "veigar-q-two-targets";
            PrepareCapture(caseId, previewOnly: true);
            yield return new EnterPlayMode();
            var flight = new PreviewEffectSample("flight", "Veigar_Hextech_Q_Projectile", 90034008);
            var first = new PreviewEffectSample("first-hit", "Veigar_Hextech_Q_Hit", 90034007,
                filter: node => node.position.z < -1f);
            var second = new PreviewEffectSample("second-hit", "Veigar_Hextech_Q_Hit", 90034007,
                filter: node => node.position.z >= -1f && node.position.z < .85f);
            var unexpected = new PreviewEffectSample("unexpected-hit", "Veigar_Hextech_Q_Hit", 90034007,
                filter: node => node.position.z >= .85f);
            var playback = new AttachedEffectPlayback(caseId, first.AssetId);
            yield return playback.Observe(flight, first, second, unexpected);
            flight.AssertPlayed(playback.Directory);
            first.AssertPlayed(playback.Directory);
            second.AssertPlayed(playback.Directory);
            Assert.That(flight.ObservedInstances, Is.EqualTo(1), playback.Directory);
            Assert.That(first.ObservedInstances + second.ObservedInstances, Is.EqualTo(2), playback.Directory);
            Assert.That(flight.Moved, Is.True, playback.Directory);
            Assert.That(first.FirstFrame, Is.LessThan(second.FirstFrame), playback.Directory);
            Assert.That(flight.LastFrame, Is.InRange(second.FirstFrame - 4, second.FirstFrame), playback.Directory);
            Assert.That(unexpected.FirstFrame, Is.EqualTo(-1), playback.Directory);
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator VeigarR_DisplacedTarget_RendersFlightAndImpactAtNewPosition()
        {
            const string caseId = "veigar-r-track-displaced-target";
            PrepareCapture(caseId, previewOnly: true);
            yield return new EnterPlayMode();
            yield return CaptureVeigarTracking(caseId);
        }

        private static IEnumerator CaptureVeigarTracking(string caseId)
        {
            var flight = new PreviewEffectSample("tracking-flight", "Veigar_Hextech_R_Projectile", 90034011);
            var impact = new PreviewEffectSample("impact", "Veigar_Hextech_R_Hit", 90034010);
            var playback = new AttachedEffectPlayback(caseId, impact.AssetId);
            var displacedFrame = -1;
            yield return playback.ObserveWithContext(context =>
            {
                if (displacedFrame >= 0 || !context.Session.TryGetWorld(out var world)) return;
                var players = world.Services.Resolve<MobaPlayerActorMapService>();
                var actors = world.Services.Resolve<MobaActorRegistry>();
                if (players.TryGetActorId(new PlayerId("preview_target"), out var targetId) &&
                    actors.TryGet(targetId, out var target) && target.transform.Value.Position.X > 9f)
                    displacedFrame = context.LastFrame;
            }, flight, impact);
            flight.AssertPlayed(playback.Directory);
            impact.AssertPlayed(playback.Directory);
            Assert.That(flight.ObservedInstances, Is.EqualTo(1), playback.Directory);
            Assert.That(impact.ObservedInstances, Is.EqualTo(1), playback.Directory);
            Assert.That(displacedFrame, Is.GreaterThan(flight.FirstFrame), playback.Directory);
            Assert.That(flight.EndPosition.x, Is.GreaterThan(8.5f), playback.Directory);
            Assert.That(impact.FirstFrame, Is.GreaterThan(displacedFrame), playback.Directory);
            Assert.That(DistanceOnGround(impact.StartPosition, new Vector3(10f, 0f, 2f)), Is.LessThan(1f), playback.Directory);
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator LuxQ_TwoTargets_RenderCagesAndClearOnRootExpiry()
        {
            const string caseId = "lux-q-two-targets";
            PrepareCapture(caseId, previewOnly: true);
            yield return new EnterPlayMode();
            var flight = new PreviewEffectSample("flight", "Lux_DarkStar_Q_Projectile", 90037012);
            var first = new PreviewEffectSample("first-cage", "Lux_DarkStar_Q_Cage", 90037010,
                filter: node => DistanceOnGround(node.position, new Vector3(7f, 0f, -.5f)) < .75f);
            var second = new PreviewEffectSample("second-cage", "Lux_DarkStar_Q_Cage", 90037010,
                filter: node => DistanceOnGround(node.position, new Vector3(7f, 0f, 2f)) < .75f);
            var stray = new PreviewEffectSample("stray-cage", "Lux_DarkStar_Q_Cage", 90037010,
                filter: node => DistanceOnGround(node.position, new Vector3(7f, 0f, -.5f)) >= .75f &&
                    DistanceOnGround(node.position, new Vector3(7f, 0f, 2f)) >= .75f);
            var playback = new AttachedEffectPlayback(caseId, first.AssetId);
            yield return playback.Observe(flight, first, second, stray);
            flight.AssertPlayed(playback.Directory);
            first.AssertPlayed(playback.Directory);
            second.AssertPlayed(playback.Directory);
            Assert.That(flight.Moved, Is.True, playback.Directory);
            Assert.That(first.FirstFrame, Is.GreaterThan(flight.FirstFrame), playback.Directory);
            Assert.That(second.FirstFrame, Is.GreaterThan(first.FirstFrame), playback.Directory);
            Assert.That(stray.FirstFrame, Is.EqualTo(-1), playback.Directory);
            var firstRoot = playback.FindFrame(MobaExecutionKind.BuffApply, 30070290);
            var secondRoot = playback.FindFrame(MobaExecutionKind.BuffApply, 30070290, firstRoot);
            Assert.That(first.FirstFrame, Is.InRange(firstRoot, firstRoot + 4), playback.Directory);
            Assert.That(second.FirstFrame, Is.InRange(secondRoot, secondRoot + 4), playback.Directory);
            Assert.That(first.LastFrame, Is.LessThan(second.LastFrame), playback.Directory);
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator LuxE_Recast_RendersFlightFieldExplosionAndCleanup()
        {
            const string caseId = "lux-e";
            PrepareCapture(caseId, previewOnly: true);
            yield return new EnterPlayMode();
            yield return CaptureLuxOrb(caseId, recast: true);
        }

        [UnityTest, Category("SkillPresentationCapture")]
        public IEnumerator LuxE_NaturalExpiry_RendersFlightFieldExplosionAndCleanup()
        {
            const string caseId = "lux-e-natural-expiry";
            PrepareCapture(caseId, previewOnly: true);
            yield return new EnterPlayMode();
            yield return CaptureLuxOrb(caseId, recast: false);
        }

        private static IEnumerator CaptureLuxOrb(string caseId, bool recast)
        {
            var flight = new PreviewEffectSample("flight", "Lux_DarkStar_E_Projectile", 90037006);
            var field = new PreviewEffectSample("field", "Lux_DarkStar_E_Charge", 90037004);
            var explosion = new PreviewEffectSample("explosion", "Lux_DarkStar_E_Explosion", 90037005);
            var playback = new AttachedEffectPlayback(caseId, explosion.AssetId);
            yield return playback.Observe(flight, field, explosion);
            foreach (var effect in new[] { flight, field, explosion })
            {
                effect.AssertPlayed(playback.Directory);
                Assert.That(effect.ObservedInstances, Is.EqualTo(1), effect.Summary);
            }
            Assert.That(flight.Moved, Is.True, playback.Directory);
            Assert.That(field.FirstFrame, Is.GreaterThan(flight.FirstFrame), playback.Directory);
            Assert.That(flight.LastFrame, Is.InRange(field.FirstFrame - 4, field.FirstFrame), playback.Directory);
            Assert.That(field.LastFrame, Is.InRange(explosion.FirstFrame - 4, explosion.FirstFrame), playback.Directory);
            Assert.That(DistanceOnGround(field.StartPosition, new Vector3(7f, 0f, 1f)), Is.LessThan(.1f), playback.Directory);
            Assert.That(DistanceOnGround(field.StartPosition, explosion.StartPosition), Is.LessThan(.1f), playback.Directory);
            Assert.That(playback.FindFrame(MobaExecutionKind.SkillCast, 30070402) >= 0, Is.EqualTo(recast), playback.Directory);
            var damage = playback.FindFrame(MobaExecutionKind.DamageApply, 30070401);
            Assert.That(damage, Is.InRange(explosion.FirstFrame - 4, explosion.FirstFrame + 4), playback.Directory);
            Assert.That(playback.FindFrame(MobaExecutionKind.DamageApply, 30070401, damage), Is.EqualTo(-1), playback.Directory);
        }

        private static float DistanceOnGround(Vector3 first, Vector3 second)
            => Vector2.Distance(new Vector2(first.x, first.z), new Vector2(second.x, second.z));

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
            SessionState.EraseBool(MobaBattleScenarioPlayback.PendingTraceDiagnosticsKey);
            Time.captureDeltaTime = 0f;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        private static void AssertLocalDebugPanelVisibility(bool expected)
        {
            var entry = GameEntry.Instance;
            var bridge = entry.GetComponent<GameEntryRuntimeGuiBridge>();
            Assert.IsNotNull(bridge);
            var previousDebugEnabled = entry.DebugEnabled;
            try
            {
                entry.DebugEnabled = true;
                Assert.AreEqual(expected, bridge.ShouldDrawLocalDebugShortcuts(),
                    "The preview profile must hide the local hero replacement panel even with debugging enabled.");
            }
            finally
            {
                entry.DebugEnabled = previousDebugEnabled;
            }
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
            SessionState.SetBool(MobaBattleScenarioPlayback.PendingTraceDiagnosticsKey, true);
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

            public IEnumerator Observe(params PreviewEffectSample[] effects) => ObserveWithContext(null, effects);

            public IEnumerator ObserveWithContext(System.Action<BattleContext> observe, params PreviewEffectSample[] effects)
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
                    observe?.Invoke(context);
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
            private readonly System.Predicate<Transform> _filter;
            private readonly HashSet<int> _instances = new HashSet<int>();
            private int _sampleFrame = -1;
            private int _pixels;
            private int _peakInstances;
            private int _activeCount;
            private Vector3 _start;

            public PreviewEffectSample(string label, string prefabName, int assetId,
                int minimumAgeFrames = 0, int maximumAgeFrames = int.MaxValue,
                System.Predicate<Transform> filter = null)
            {
                _label = label;
                _prefabName = prefabName + "(Clone)";
                AssetId = assetId;
                _minimumAgeFrames = minimumAgeFrames;
                _maximumAgeFrames = maximumAgeFrames;
                _filter = filter;
            }

            public int AssetId { get; }
            public int FirstFrame { get; private set; } = -1;
            public int LastFrame { get; private set; } = -1;
            public int ObservedInstances => _instances.Count;
            public int SampleFrame => _sampleFrame;
            public Vector3 StartPosition => _start;
            public Vector3 EndPosition { get; private set; }
            public bool Moved { get; private set; }
            public string Summary => $"{_label} frames={FirstFrame}..{LastFrame} from={_start} to={EndPosition} pixels={_pixels} instances={_instances.Count} peak={_peakInstances} activeAtEnd={_activeCount}";

            public bool Matches(Transform node) => node.name == _prefabName || node.name == "Vfx_" + AssetId;

            public void Observe(Transform[] active, int frame, string directory, List<string> samples)
            {
                var matches = active.Where(node => Matches(node) && (_filter == null || _filter(node))).ToArray();
                _activeCount = matches.Length;
                _peakInstances = System.Math.Max(_peakInstances, _activeCount);
                if (_activeCount == 0) return;
                foreach (var match in matches) _instances.Add(match.GetInstanceID());
                var instance = matches[0];
                LastFrame = frame;
                EndPosition = instance.position;
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

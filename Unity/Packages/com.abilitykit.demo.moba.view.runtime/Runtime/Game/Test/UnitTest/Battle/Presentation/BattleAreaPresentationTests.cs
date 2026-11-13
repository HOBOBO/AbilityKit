using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Area;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Protocol.Moba;
using MobaPresentationCueSnapshotCodec = AbilityKit.Protocol.Moba.StateSync.MobaPresentationCueSnapshotCodec;
using AbilityKit.World.ECS;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace AbilityKit.Game.Test.UnitTest
{
    /// <summary>Real skill -> area runtime -> wire cue -> configured Unity visual.
    /// Rendering here is an isolated projection check; HUD PlayMode acceptance is separate.</summary>
    public sealed class BattleAreaPresentationTests
    {
        [Test]
        public void AreaSnapshotPositionAlreadyIncludesConfiguredSpawnOffset()
        {
            // SpawnAreaPlanActionModule applies the table offset before RegisterSpawn.
            // Both the range/model view and the VFX cue must use that same world center.
            var snapshot = new AreaEventData(AreaPresentationEventKind.Spawn,
                71, 1, 40030201, 12f, 1f, 8f, 3.5f);
            var handle = new BattleAreaViewHandleFactory().TryCreate(null, in snapshot);
            Assert.IsNotNull(handle);
            try
            {
                Assert.IsNotNull(handle.ModelGo);
                Assert.IsNotNull(handle.RangeGo);
                Assert.AreEqual(new Vector3(12f, 1f, 8f), handle.ModelGo.transform.position,
                    "The model must not apply the source table's spawn offset a second time.");
                Assert.AreEqual(new Vector3(12f, 1f, 8f), handle.RangeGo.transform.position,
                    "The range must agree with the VFX cue's authoritative world center.");
                Assert.IsNull(handle.VfxGo, "Configured AOE VFX are owned by the unified cue, not duplicated here.");
            }
            finally { handle.Destroy(); }
        }

        [TestCase(3001, 30010301, 3, 42010301, 1, false)]
        [TestCase(3002, 30020301, 3, 42020301, 3, false)]
        [TestCase(3003, 30030301, 2, 42030301, 1, false)]
        [TestCase(3004, 30040301, 2, 42040301, 1, false)]
        [TestCase(3004, 30040401, 3, 42040401, 1, false)]
        [TestCase(3002, 30020301, 3, 42020301, 3, true)]
        [TestCase(3004, 30040401, 3, 42040401, 1, true)]
        public void RealAreaCuesProjectConfiguredVisualAndReleaseWithArea(
            int heroId, int skillId, int slot, int areaTemplateId, int expectedCount, bool removeEarly)
        {
            using var routine = RunRealArea(heroId, skillId, slot, areaTemplateId, expectedCount, removeEarly);
            while (routine.MoveNext()) { }
        }

        [UnityTest]
        public IEnumerator PlayMode_RealFixedAreasRenderAndRelease()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            Time.captureDeltaTime = 1f / 30f;
            var failures = new List<string>();
            foreach (var entry in new[]
            {
                (Hero: 3001, Skill: 30010301, Slot: 3, Area: 42010301, Count: 1),
                (Hero: 3002, Skill: 30020301, Slot: 3, Area: 42020301, Count: 3),
                (Hero: 3003, Skill: 30030301, Slot: 2, Area: 42030301, Count: 1),
                (Hero: 3004, Skill: 30040301, Slot: 2, Area: 42040301, Count: 1),
                (Hero: 3004, Skill: 30040401, Slot: 3, Area: 42040401, Count: 1),
            })
            {
                using var routine = RunRealArea(entry.Hero, entry.Skill, entry.Slot, entry.Area, entry.Count, false);
                while (true)
                {
                    bool next;
                    try { next = routine.MoveNext(); }
                    catch (AssertionException exception)
                    {
                        failures.Add(entry.Area + ": " + exception.Message);
                        break;
                    }
                    if (!next) break;
                    yield return routine.Current;
                }
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [UnityTearDown]
        public IEnumerator ExitNativeAcceptance()
        {
            Time.captureDeltaTime = 0f;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        private static IEnumerator<object> RunRealArea(
            int heroId, int skillId, int slot, int areaTemplateId, int expectedCount, bool removeEarly)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("AOE pixel acceptance requires a graphics device.");
            using var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                new[] { skillId }, worldId: "area_projection_" + skillId + "_" + removeEarly,
                heroId: heroId, attributeTemplateId: heroId);
            harness.EnterGameAndWarmup();
            var ownerId = harness.AssertPlayerActorBound();
            Assert.IsTrue(harness.Config.TryGetAoe(areaTemplateId, out var config));
            Assert.Greater(config.VfxId, 0, "This scenario must exercise configured AOE VFX.");
            Assert.AreEqual(0, config.AttachMode, "This scenario covers fixed world areas.");
            var visualWorld = new EntityWorld();
            var root = visualWorld.Create("area-projection-vfx");
            var owner = visualWorld.Create("area-projection-owner");
            var transform = new BattleTransformComponent { Position = Vector3.zero, Forward = Vector3.forward };
            owner.WithRef(transform);
            var lookup = new BattleEntityLookup();
            lookup.Bind(new AbilityKit.Game.Battle.Entity.BattleNetId(ownerId), owner);
            var query = new BattleEntityQuery(visualWorld, lookup);
            var database = BattleViewFactory.GetOrLoadVfxDb();
            Assert.IsTrue(database.TryGet(config.VfxId, out var resource));
            var resourceKey = string.IsNullOrEmpty(resource.ResourceKey) ? resource.Resource : resource.ResourceKey;
            Assert.IsNotNull(Resources.Load<GameObject>(resourceKey), "AOE resource must resolve: " + resourceKey);
            var manager = new BattleVfxManager(database);
            var handler = new BattlePresentationCueViewEventHandler(visualWorld, query, manager, root);
            var runtime = harness.World.Services.Resolve<MobaAreaRuntimeService>();
            var starts = new HashSet<int>();
            var ends = new HashSet<int>();
            var visuals = new List<GameObject>();
            var positions = new List<Vector3>();
            var pixelsPerVisual = new List<int>();
            var sampledVisuals = new HashSet<GameObject>();
            var areaForward = Vector3.forward;
            var visiblePixels = 0;
            var removed = false;
            var aim = new Vec3(2.4f, 0f, 3.2f);
            try
            {
                var cast = harness.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(
                    ownerId, slot, aimPos: aim, aimDir: new Vec3(0.6f, 0f, 0.8f), targetActorId: 0);
                Assert.IsTrue(cast.Success, "Real cast failed: " + cast.FailReason);
                for (var tick = 0; tick < 270; tick++)
                {
                    harness.Tick(1);
                    var snapshots = new List<WorldStateSnapshot>();
                    harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>()
                        .CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
                    foreach (var snapshot in snapshots)
                    {
                        if (snapshot.OpCode != MobaOpCodes.Snapshot.PresentationCue) continue;
                        foreach (var entry in MobaPresentationCueSnapshotCodec.Deserialize(snapshot.Payload))
                        {
                            if (entry.TemplateId != areaTemplateId) continue;
                            var cue = PresentationCueSnapshotMapper.Map(in entry);
                            Assert.AreEqual(PresentationCueDomain.Aoe, cue.Domain);
                            Assert.AreEqual(PresentationCueLifetime.PersistentState,
                                BattlePresentationCueResolver.ResolveLifetime(in cue));
                            var decision = new BattlePresentationCueResolver().Resolve(in cue);
                            if (cue.Stage == PresentationCueStage.Started)
                            {
                                Assert.AreEqual(BattlePresentationCueDecisionKind.Play, decision.Kind);
                                Assert.IsTrue(runtime.TryGetArea((int)cue.InstanceId, out var area));
                                var position = decision.SpawnRequest.ExplicitPosition;
                                Assert.AreEqual(area.Center.X, position.X, 0.0001f);
                                Assert.AreEqual(area.Center.Y, position.Y, 0.0001f);
                                Assert.AreEqual(area.Center.Z, position.Z, 0.0001f);
                                Assert.AreEqual(config.DurationMs, decision.SpawnRequest.DurationMsOverride);
                                areaForward = new Vector3(area.Forward.X, area.Forward.Y, area.Forward.Z);
                                starts.Add(area.AreaId);
                                handler.HandleSnapshot(new[] { cue });
                                var count = root.ChildCount;
                                handler.HandleSnapshot(new[] { cue });
                                Assert.AreEqual(count, root.ChildCount, "Re-delivery must not duplicate an area visual.");
                            }
                            else
                            {
                                if (BattlePresentationCueResolver.ShouldStop(cue.Stage)) ends.Add((int)cue.InstanceId);
                                handler.HandleSnapshot(new[] { cue });
                            }
                        }
                    }
                    manager.Tick(root, null, query);
                    if (starts.Count == expectedCount && visuals.Count == 0)
                    {
                        Assert.AreEqual(expectedCount, root.ChildCount, "Each AreaId must own one formal VFX.");
                        for (var index = 0; index < root.ChildCount; index++)
                        {
                            var entity = root.GetChild(index);
                            Assert.IsTrue(entity.TryGetRef(out BattleVfxComponent vfx));
                            Assert.AreEqual(config.VfxId, vfx.VfxId);
                            Assert.IsTrue(entity.TryGetRef(out BattleViewGameObjectComponent view));
                            Assert.IsNotNull(view.GameObject);
                            Assert.IsTrue(entity.TryGetRef(out BattlePresentationLifetimeComponent presentation));
                            Assert.AreEqual(resourceKey, presentation.Request.ResourceKey);
                            Assert.AreEqual(0, presentation.Request.Attachment.ActorId,
                                "Fixed AOE VFX must not follow the caster after release.");
                            var position = presentation.Request.Position;
                            Assert.Less(Vector3.Distance(view.GameObject.transform.position,
                                new Vector3(position.X, position.Y, position.Z)), 0.001f);
                            var forward = view.GameObject.transform.forward;
                            Assert.Greater(Vector3.Dot(forward, areaForward), 0.99f,
                                "AOE VFX must retain the authoritative area's direction.");
                            visuals.Add(view.GameObject);
                            positions.Add(view.GameObject.transform.position);
                            pixelsPerVisual.Add(0);
                        }
                        // A later caster transform must not drag an already released fixed area.
                        transform.Position = new Vector3(20f, 0f, -15f);
                        transform.Forward = Vector3.left;
                        manager.Tick(root, null, query);
                    }
                    if (visuals.Count == expectedCount && (!Application.isPlaying || tick % 3 == 0))
                        for (var i = 0; i < visuals.Count; i++)
                        {
                            if (visuals[i] == null || !visuals[i].activeInHierarchy) continue;
                            Assert.Less(Vector3.Distance(positions[i], visuals[i].transform.position), 0.001f);
                            if (!Application.isPlaying && !sampledVisuals.Add(visuals[i])) continue;
                            var pixels = RenderContribution(visuals[i], areaTemplateId + "-" + i,
                                config.Radius, config.DurationMs, pixelsPerVisual[i]);
                            pixelsPerVisual[i] = Math.Max(pixelsPerVisual[i], pixels);
                            visiblePixels = Math.Max(visiblePixels, pixels);
                        }
                    if (removeEarly && !removed && visuals.Count == expectedCount && visiblePixels > 20)
                    {
                        Assert.AreEqual(expectedCount, runtime.DespawnAreas(ownerId, areaTemplateId, true));
                        removed = true;
                    }
                    if (starts.Count == expectedCount && ends.Count == expectedCount) break;
                    if (Application.isPlaying) yield return null;
                }
                Assert.AreEqual(expectedCount, starts.Count, "Missing authoritative area start cues.");
                Assert.AreEqual(expectedCount, ends.Count, "Every area must emit a matching end cue.");
                manager.Tick(root, null, query);
                Assert.AreEqual(0, root.ChildCount, "Area completion/removal must release its formal VFX.");
                foreach (var id in starts) Assert.IsFalse(runtime.TryGetArea(id, out _));
                TestContext.WriteLine($"AOE {areaTemplateId}: count={starts.Count}, resource={resourceKey}, visiblePixels={visiblePixels}, native={Application.isPlaying}");
                foreach (var pixels in pixelsPerVisual)
                    Assert.Greater(pixels, 20, "Each configured AOE VFX must visibly render at its real anchor.");
            }
            finally
            {
                handler.Clear();
                manager.Clear(root);
                if (root.IsValid) root.Destroy();
                if (owner.IsValid) owner.Destroy();
            }
        }

        private static int RenderContribution(GameObject instance, string label, float radius, int durationMs, int previousBest)
        {
            var cameraObject = new GameObject("AOE acceptance camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.16f, 0.16f);
            camera.depthTextureMode = DepthTextureMode.Depth;
            camera.cullingMask = 1 << 30;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(4f, radius * 1.4f);
            camera.transform.position = instance.transform.position + new Vector3(0f, 13f, -10f);
            camera.transform.LookAt(instance.transform.position);
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            var states = Array.ConvertAll(renderers, renderer => renderer.enabled);
            foreach (var child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 30;
            // EditMode has no native particle update. Advance only authored autoplay systems;
            // do not enable inactive objects or trigger otherwise dormant emitters.
            var target = RenderTexture.GetTemporary(960, 540, 24);
            var previousActive = RenderTexture.active;
            var texture = new Texture2D(960, 540, TextureFormat.RGB24, false);
            try
            {
                var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../local/Logs/aoe-presentation"));
                Directory.CreateDirectory(directory);
                var best = -1;
                // Respect authored emission delays and sample during the area's lifetime.
                var steps = Application.isPlaying ? 1 : Mathf.Min(16, Mathf.Max(1, (durationMs - 1) / 100));
                for (var step = 0; step < steps; step++)
                {
                    if (!Application.isPlaying)
                    {
                        foreach (var animator in instance.GetComponentsInChildren<Animator>()) animator.Update(0.1f);
                        foreach (var particles in instance.GetComponentsInChildren<ParticleSystem>())
                            if (particles.main.playOnAwake || particles.isPlaying)
                                particles.Simulate(0.1f, false, false, true);
                    }
                    states = Array.ConvertAll(renderers, renderer => renderer.enabled);
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
                    for (var i = 0; i < renderers.Length; i++) renderers[i].enabled = states[i];
                    var changed = 0;
                    for (var i = 0; i < visible.Length; i++)
                        if (Math.Abs(visible[i].r - hidden[i].r) + Math.Abs(visible[i].g - hidden[i].g) +
                            Math.Abs(visible[i].b - hidden[i].b) > 24) changed++;
                    if (changed <= best) continue;
                    best = changed;
                    var capturePath = Path.Combine(directory, (Application.isPlaying ? "play-" : "edit-") + label + ".png");
                    if (changed > previousBest || !File.Exists(capturePath)) File.WriteAllBytes(capturePath, png);
                }
                if (!Application.isPlaying)
                {
                    TestContext.WriteLine($"AOE pixels {label}: best={best}");
                    if (best == 0)
                        foreach (var renderer in renderers)
                        {
                            var particles = renderer.GetComponent<ParticleSystem>();
                            TestContext.WriteLine($"  {renderer.name}: active={renderer.gameObject.activeInHierarchy}, enabled={renderer.enabled}, bounds={renderer.bounds}, shader={renderer.sharedMaterial?.shader.name}, particles={(particles != null ? particles.particleCount : 0)}");
                        }
                }
                return best;
            }
            finally
            {
                for (var i = 0; i < renderers.Length; i++) renderers[i].enabled = states[i];
                RenderTexture.active = previousActive;
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}

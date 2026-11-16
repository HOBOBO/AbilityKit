using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.World.ECS;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PresentationCueStage = AbilityKit.Demo.Moba.Share.PresentationCueStage;

namespace AbilityKit.Game.Tests
{
    public sealed class JhinWEEventVfxTests
    {
        private const string Root = "ChampionVfx/13.Jhin_Battlecast/";

        [UnityTest]
        public IEnumerator E_RealLandingCueDeploysMatchingTrapAndReleasesAtAreaEnd()
        {
            using var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(new[] { 30010301 },
                worldId: "jhin_e_landing_visual", heroId: 3001, attributeTemplateId: 3001);
            harness.EnterGameAndWarmup();
            var owner = harness.AssertPlayerActorBound();
            var cast = harness.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(owner, 3,
                new Vec3(0, 0, 5), Vec3.Forward, 0);
            Assert.IsTrue(cast.Success, cast.FailReason);
            var world = new EntityWorld();
            var root = world.Create("jhin-e-event-vfx");
            var query = new BattleEntityQuery(world, new BattleEntityLookup());
            var database = BattleViewFactory.GetOrLoadVfxDb();
            var manager = new BattleVfxManager(database);
            var cues = new BattlePresentationCueViewEventHandler(world, query, manager, root);
            GameObject landing = null;
            var started = false;
            var stopped = false;
            try
            {
                for (var tick = 0; tick < 260 && !stopped; tick++)
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
                            if (entry.TemplateId != 42010301) continue;
                            var cue = PresentationCueSnapshotMapper.Map(in entry);
                            cues.HandleSnapshot(new[] { cue });
                            if (cue.Stage == PresentationCueStage.Started)
                            {
                                started = true;
                                Assert.AreEqual(1, root.ChildCount);
                                var entity = root.GetChild(0);
                                Assert.IsTrue(entity.TryGetRef(out BattleViewGameObjectComponent view));
                                landing = view.GameObject;
                                Assert.IsNotNull(landing);
                                Assert.IsTrue(entity.TryGetRef(out BattlePresentationLifetimeComponent life));
                                Assert.AreEqual(Root + "Jhin_Battlecast_E_Trap", life.Request.ResourceKey);
                                Assert.AreEqual(0, life.Request.Attachment.ActorId);
                                Assert.AreEqual(5f, landing.transform.position.z, 0.15f);
                                var trap = landing.transform.Find("Trap_1");
                                Assert.IsNotNull(trap);
                                Assert.AreEqual(0f, trap.localPosition.z, 0.001f);
                                Assert.IsNull(landing.transform.Find("Trap_Missile"));
                                var flight = Resources.Load<GameObject>(Root + "Jhin_Battlecast_E_Projectile");
                                Assert.That(Vector3.Distance(trap.localScale,
                                    flight.transform.Find("Trap_Missile").localScale), Is.LessThan(0.001f));
                                var animator = landing.GetComponent<Animator>();
                                Assert.IsNotNull(animator);
                                animator.Update(0);
                                var arm = trap.Find("Arm1");
                                Assert.IsNotNull(arm);
                                var closed = arm.localRotation;
                                animator.Update(0.25f);
                                Assert.Greater(Quaternion.Angle(closed, arm.localRotation), 60,
                                    "Landing must actually animate the authored four-arm opening.");
                                var deployed = arm.localRotation;
                                animator.Update(2f);
                                Assert.Less(Quaternion.Angle(deployed, arm.localRotation), 0.01f,
                                    "The armed trap must hold its deployed pose while the area persists.");
                                cues.HandleSnapshot(new[] { cue });
                                Assert.AreEqual(1, root.ChildCount, "Repeated snapshots must not restart or duplicate deployment.");
                            }
                            else if (BattlePresentationCueResolver.ShouldStop(cue.Stage))
                            {
                                stopped = true;
                                Assert.IsTrue(landing == null || !landing.activeInHierarchy,
                                    "Area end must destroy or return its landing visual to the inactive pool.");
                                Assert.AreEqual(0, root.ChildCount);
                            }
                        }
                    }
                    if (started && !stopped) Assert.IsNotNull(landing);
                }
                Assert.IsTrue(started, "A real E landing must start the persistent area visual.");
                Assert.IsTrue(stopped, "The existing area lifetime must stop the deployed trap visual.");
            }
            finally { cues.Clear(); manager.Clear(root); if (root.IsValid) root.Destroy(); }
            yield return null;
        }

        [TestCase("Jhin_Battlecast_W_Projectile", 0.1f)]
        [TestCase("Jhin_Battlecast_W_Projectile", 0.8f)]
        [TestCase("Jhin_Battlecast_E_Projectile", 0.1f)]
        [TestCase("Jhin_Battlecast_E_Projectile", 0.8f)]
        [TestCase("Jhin_Battlecast_E_Trap", 0f)]
        [TestCase("Jhin_Battlecast_E_Trap", 0.3f)]
        [TestCase("Jhin_Battlecast_E_Trap", 1f)]
        [TestCase("Jhin_Battlecast_R_Ground", 0f)]
        [TestCase("Jhin_Battlecast_R_Ground", 1.8f)]
        [TestCase("Jhin_Battlecast_R_Ground", 8f)]
        public void EventPrefabHasVisiblePixelsAndStaysAtItsRuntimeAnchor(string name, float seconds)
        {
            Assert.AreNotEqual(UnityEngine.Rendering.GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType,
                "This acceptance requires a real graphics device.");
            var prefab = Resources.Load<GameObject>(Root + name);
            Assert.IsNotNull(prefab);
            var visual = Object.Instantiate(prefab);
            var cameraObject = new GameObject("jhin-event-acceptance-camera");
            var lightObject = new GameObject("jhin-event-acceptance-light");
            var texture = new RenderTexture(512, 512, 24);
            var pixels = new Texture2D(512, 512, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                visual.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (var transform in visual.GetComponentsInChildren<Transform>(true))
                    transform.gameObject.layer = 31;
                var animator = visual.GetComponent<Animator>();
                if (animator != null) { animator.Update(0); animator.Update(seconds); }
                foreach (var particle in visual.GetComponentsInChildren<ParticleSystem>())
                    particle.Simulate(seconds, false, true, true);
                var meshBounds = new Bounds();
                var first = true;
                foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled))
                {
                    if (first) { meshBounds = renderer.bounds; first = false; }
                    else meshBounds.Encapsulate(renderer.bounds);
                }
                if (name.Contains("_E_"))
                {
                    Assert.IsFalse(first);
                    Assert.Less(new Vector2(meshBounds.center.x, meshBounds.center.z).magnitude, 0.1f);
                    Assert.That(Mathf.Max(meshBounds.size.x, meshBounds.size.z), Is.InRange(0.45f, 0.75f),
                        "Both flight and landing must use the smaller trap size.");
                }
                if (name.Contains("_R_"))
                {
                    Assert.AreEqual(2, visual.GetComponentsInChildren<MeshRenderer>().Length);
                    Assert.IsNotNull(visual.transform.Find("R_Area_L"));
                    Assert.IsNotNull(visual.transform.Find("R_Area_R"));
                    Assert.Less(meshBounds.max.y, 0.3f, "The area must stay on the ground.");
                    if (seconds >= 1.8f)
                        foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>())
                        {
                            var animated = new MaterialPropertyBlock();
                            renderer.GetPropertyBlock(animated);
                            Assert.AreEqual(0.8f, animated.GetFloat("_FinalMix"), 0.01f,
                                "Ground deployment must hold its final material pose across independent shots.");
                        }
                }
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.cullingMask = 1 << 31;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.orthographicSize = 2.2f;
                camera.transform.position = new Vector3(3, 4, -5);
                camera.transform.LookAt(new Vector3(0, 0.6f, 0));
                if (name.Contains("_R_"))
                {
                    camera.orthographicSize = Mathf.Max(meshBounds.size.x, meshBounds.size.z) * 0.65f;
                    camera.transform.position = meshBounds.center + new Vector3(0, camera.orthographicSize * 2, -0.01f);
                    camera.transform.LookAt(meshBounds.center);
                }
                camera.targetTexture = texture;
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.5f;
                light.cullingMask = 1 << 31;
                light.transform.rotation = Quaternion.Euler(45, -30, 0);
                camera.Render();
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, 512, 512), 0, 0);
                pixels.Apply();
                var count = pixels.GetPixels32().Count(p => p.r > 20 || p.g > 20 || p.b > 20);
                Assert.Greater(count, 40, "The authored effect must produce visible pixels, not just have a renderer component.");
                var output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../local/jhin-we"));
                Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output, name + "-" + Mathf.RoundToInt(seconds * 1000) + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(visual); Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(lightObject); Object.DestroyImmediate(texture); Object.DestroyImmediate(pixels);
            }
        }
    }
}

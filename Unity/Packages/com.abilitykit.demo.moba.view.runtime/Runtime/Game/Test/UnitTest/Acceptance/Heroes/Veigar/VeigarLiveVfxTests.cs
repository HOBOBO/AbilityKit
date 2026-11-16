using System.IO;
using System.Collections;
using UnityEngine.TestTools;
using System.Linq;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.World.ECS;
using NUnit.Framework;
using UnityEngine;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class VeigarLiveVfxTests
    {
        [UnityTest]
        public IEnumerator LocalSession_CageSurvivesHandoffUntilAreaEnds()
        {
            DemoGameplayTestLauncher.OpenMobaLocalForPlay();
            yield return new EnterPlayMode();
            Time.captureDeltaTime = 1f / 60f;
            yield return SessionCage();
        }

        private static IEnumerator SessionCage()
        {
            for (var frame = 0; frame < 18000 && (!GameEntry.IsInitialized ||
                GameEntry.Instance.Get<GameFlowDomain>().CurrentBattlePhase != MobaBattleState.InMatch); frame++) yield return null;
            Assert.IsTrue(GameEntry.Instance.TryGet(out BattleContext context));
            var plan = context.Plan;
            var player = BattleInputSessionIdentity.ResolvePlayerId((IBattleInputSessionIdentityPort)context);
            var replace = BattleInputCommandFactory.CreateDebugReplaceHero(
                SessionSimRuntimeTuning.ResolveInputSubmitFrame(context.LastFrame, in plan), player, 3004);
            Assert.IsTrue(new BattleInputSubmitter(context.InputRuntime, player,
                BattleInputSessionIdentity.ResolveWorldId(in plan)).Submit(in replace));
            Assert.IsTrue(context.Session.TryGetWorld(out var world));
            var actors = world.Services.Resolve<MobaActorLookupService>();
            ActorEntity caster = null;
            for (var frame = 0; frame < 360; frame++)
            {
                if (actors.TryGetActorEntity(context.LocalActorId, out caster) && caster.hasSkillLoadout &&
                    caster.skillLoadout.ActiveSkills[2].SkillId == 30040401) break;
                yield return null;
            }
            Assert.IsNotNull(caster);
            var transform = caster.transform.Value;
            caster.ReplaceTransform(new Transform3(new Vec3(1,0,-7),transform.Rotation,transform.Scale));
            yield return null;
            var cast = world.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(context.LocalActorId,3,
                new Vec3(1,0,-1),Vec3.Forward,0);
            Assert.IsTrue(cast.Success,cast.FailReason);
            GameObject visual = null;
            for (var frame = 0; frame < 120 && visual == null; frame++)
            {
                visual = Object.FindObjectsOfType<BattleVfxCueMetadata>().Select(v=>v.gameObject)
                    .FirstOrDefault(v=>v.name == "Vfx_90034017");
                yield return null;
            }
            Assert.IsNotNull(visual,"Real Area Cue must create the cage.");
            var boundary=visual.transform.Find("Boundary");
            Assert.IsNotNull(boundary, "Actual cage hierarchy: " + string.Join(", ",
                visual.GetComponentsInChildren<Transform>(true).Select(t => t.name)) +
                "; components: " + string.Join(", ", visual.GetComponents<Component>().Select(c => c.GetType().Name)));
            Assert.Less(Vector3.Distance(Vector3.one, visual.transform.localScale), .001f,
                "Unit-radius prefab and actual Cue radius must restore the intended world footprint.");
            for (var frame=0;frame<195;frame++)
            {
                if (frame==55)
                {
                    var next=world.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(context.LocalActorId,1,
                        new Vec3(1,0,1),Vec3.Forward,0);
                    Assert.IsTrue(next.Success,next.FailReason);
                }
                if (frame>=45)
                {
                    Assert.IsTrue(visual != null && visual.activeInHierarchy,"Cage lost at frame "+frame);
                    Assert.IsTrue(boundary.gameObject.activeInHierarchy,"Boundary hidden at frame "+frame);
                }
                if (frame == 150)
                {
                    var envelope = boundary.Find("Cage/CageEnveloppe").GetComponent<MeshRenderer>();
                    Assert.That(envelope.bounds.size.x, Is.InRange(6f, 8f),
                        "Actual Cue must match the cage collision footprint.");
                    Assert.IsTrue(boundary.GetComponentsInChildren<ParticleSystem>()
                        .Any(p => p.IsAlive(false)), "The actual cage must still render particles after 2.5 seconds.");
                    CaptureLiveCage(visual);
                }
                yield return null;
            }
            for (var frame=0;frame<45;frame++) yield return null;
            Assert.IsTrue(visual == null || !visual.activeInHierarchy,"Area expiry must release the cage.");
        }

        private static void CaptureLiveCage(GameObject visual)
        {
            var transforms = visual.GetComponentsInChildren<Transform>(true);
            var layers = transforms.Select(t => t.gameObject.layer).ToArray();
            var cameraObject = new GameObject("live-cage-capture-camera");
            var lightObject = new GameObject("live-cage-capture-light");
            var target = new RenderTexture(640, 640, 24);
            var pixels = new Texture2D(640, 640, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                foreach (var transform in transforms) transform.gameObject.layer = 31;
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << 31;
                camera.orthographic = true;
                camera.orthographicSize = 5.5f;
                var anchor = visual.transform.position;
                camera.transform.position = anchor + new Vector3(5, 9, -10);
                camera.transform.LookAt(anchor + Vector3.up);
                camera.targetTexture = target;
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.5f;
                light.cullingMask = 1 << 31;
                light.transform.rotation = Quaternion.Euler(45, -30, 0);
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
                pixels.Apply();
                Assert.Greater(pixels.GetPixels32().Count(p => p.r > 20 || p.g > 20 || p.b > 20), 80,
                    "The actual battle instance must render visible cage pixels after 2.5 seconds.");
                var folder = Path.GetFullPath(Path.Combine(Application.dataPath,"../../local/Logs/veigar-live-vfx"));
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder,"E_ActualSession-2500.png"), pixels.EncodeToPNG());
            }
            finally
            {
                for (var i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = layers[i];
                RenderTexture.active = previous;
                Object.Destroy(cameraObject);
                Object.Destroy(lightObject);
                Object.Destroy(target);
                Object.Destroy(pixels);
            }
        }

        [UnityTearDown]
        public IEnumerator CleanupSession()
        {
            Time.captureDeltaTime=0;
            if (Application.isPlaying) yield return new ExitPlayMode();
            DemoGameplayTestLauncher.Clear();
        }

        [TestCase(2, 42040301)]
        [TestCase(3, 42040402)]
        public void RealAreaCue_KeepsOneAnchoredVisualThroughHandoffAndReleasesAtExpiry(int slot, int template)
        {
            using var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                new[] { slot == 2 ? 30040301 : 30040401, 30040201 },
                worldId: "veigar-live-area-" + slot, heroId: 3004, attributeTemplateId: 3004);
            harness.EnterGameAndWarmup();
            var owner = harness.AssertPlayerActorBound();
            var casts = harness.World.Services.Resolve<SkillCastCoordinator>();
            var cast = casts.TryCastBySlot(owner, slot, new Vec3(0, 0, 5), Vec3.Forward, 0);
            Assert.IsTrue(cast.Success, cast.FailReason);
            var world = new EntityWorld();
            var root = world.Create("veigar-real-area-vfx");
            var manager = new BattleVfxManager(BattleViewFactory.GetOrLoadVfxDb());
            var handler = new BattlePresentationCueViewEventHandler(world,
                new BattleEntityQuery(world, new BattleEntityLookup()), manager, root);
            var starts = 0;
            var ends = 0;
            try
            {
                for (var tick = 0; tick < 140; tick++)
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
                            if (entry.TemplateId != template) continue;
                            var cue = PresentationCueSnapshotMapper.Map(in entry);
                            handler.HandleSnapshot(new[] { cue });
                            if (cue.Stage == AbilityKit.Demo.Moba.Share.PresentationCueStage.Started)
                            {
                                starts++;
                                Assert.AreEqual(1, root.ChildCount);
                                Assert.IsTrue(root.GetChild(0).TryGetRef(out BattleViewGameObjectComponent view));
                                Assert.Less(Vector3.Distance(view.GameObject.transform.position, new Vector3(0, 0, 5)), .02f);
                                Assert.IsNotNull(view.GameObject.GetComponent<Animator>());
                            }
                            if (cue.Stage == AbilityKit.Demo.Moba.Share.PresentationCueStage.Completed) ends++;
                        }
                    }
                    if (tick == 12)
                    {
                        Assert.AreEqual(1, root.ChildCount, "The committed Area must outlive a normal cast handoff.");
                        var next = casts.TryCastBySlot(owner, 1, new Vec3(0, 0, 8), Vec3.Forward, 0);
                        Assert.IsTrue(next.Success, "Q should hand off after 250ms: " + next.FailReason);
                        Assert.AreEqual(1, root.ChildCount);
                    }
                }
                Assert.AreEqual(1, starts);
                Assert.AreEqual(1, ends);
                Assert.AreEqual(0, root.ChildCount, "Expired Area must release its full visual sequence.");
            }
            finally { handler.Clear(); manager.Clear(root); if (root.IsValid) root.Destroy(); }
        }

        [TestCase(90034016, "W_Area", "Warning", "Impact")]
        [TestCase(90034017, "E_Area", "Warning", "Boundary")]
        public void AreaVisual_PoolReuseRestartsFullSequence(int vfxId, string state, string first, string second)
        {
            var manager = new BattleVfxManager(BattleViewFactory.GetOrLoadVfxDb());
            var pool = manager.Pool;
            try
            {
                for (var lease = 0; lease < 2; lease++)
                {
                    Assert.IsTrue(pool.TryRent(vfxId, out var visual));
                    var animator = visual.GetComponent<Animator>();
                    animator.Update(0);
                    Assert.IsTrue(visual.transform.Find(first).gameObject.activeSelf, "Lease " + lease + " must restart the warning.");
                    Assert.IsFalse(visual.transform.Find(second).gameObject.activeSelf);
                    animator.Update(.8f);
                    if (state == "E_Area") Assert.IsTrue(visual.transform.Find(second).gameObject.activeSelf);
                    animator.Update(4f);
                    pool.Return(vfxId, visual);
                }
            }
            finally { pool.Clear(); }
        }

        [TestCase("W_Area", .3f)]
        [TestCase("W_Area", .95f)]
        [TestCase("W_Area", 1.3f)]
        [TestCase("E_Area", .2f)]
        [TestCase("E_Area", .8f)]
        [TestCase("E_Area", 1.5f)]
        [TestCase("E_Area", 2.5f)]
        [TestCase("E_Area", 3.2f)]
        [TestCase("E_Area", 3.45f)]
        [TestCase("Q_Projectile", .1f)]
        [TestCase("R_Projectile", .1f)]
        [TestCase("Q_Hit", .1f)]
        [TestCase("R_Hit", .1f)]
        public void RuntimeAnchoredVfx_DeploysCorrectStageAndRendersVisiblePixels(string name, float seconds)
        {
            Assert.AreNotEqual(UnityEngine.Rendering.GraphicsDeviceType.Null, SystemInfo.graphicsDeviceType);
            var prefab = Resources.Load<GameObject>("MobaVfx/Veigar/" + name);
            Assert.IsNotNull(prefab);
            var visual = Object.Instantiate(prefab);
            if (name == "W_Area" || name == "E_Area")
                BattlePresentationScaleState.ApplyCueScale(visual, 1f, name == "W_Area" ? 2.4f : 3.25f);
            var cameraObject = new GameObject("veigar-vfx-camera");
            var lightObject = new GameObject("veigar-vfx-light");
            var target = new RenderTexture(640, 640, 24);
            var pixels = new Texture2D(640, 640, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                // A nonzero world anchor catches retained showcase offsets.
                var anchor = new Vector3(12, 0, 17);
                visual.transform.position = anchor;
                var animator = visual.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.Rebind();
                    animator.Update(0);
                    if (name == "E_Area")
                    {
                        // Advance from creation without seeking/restarting late stages.
                        foreach (var particle in visual.GetComponentsInChildren<ParticleSystem>(true))
                            particle.Simulate(0f, false, true, true);
                        for (var elapsed = 0f; elapsed < seconds;)
                        {
                            var step = Mathf.Min(.01f, seconds - elapsed);
                            animator.Update(step);
                            foreach (var particle in visual.GetComponentsInChildren<ParticleSystem>())
                                particle.Simulate(step, false, false, false);
                            elapsed += step;
                        }
                    }
                    else
                    {
                        animator.Play(name, 0, seconds / animator.runtimeAnimatorController.animationClips[0].length);
                        animator.Update(0);
                    }
                }
                if (name.EndsWith("Projectile"))
                {
                    var branch = visual.transform.GetChild(0);
                    Assert.Less(new Vector2(branch.localPosition.x, branch.localPosition.z).magnitude, .01f);
                    Assert.That(branch.position.y, Is.InRange(.7f, 1.2f));
                }
                if (name == "W_Area")
                {
                    Assert.AreEqual(seconds < 1.221f, visual.transform.Find("Warning").gameObject.activeSelf);
                    Assert.AreEqual(seconds >= .65f && seconds < 1.221f, visual.transform.Find("Falling").gameObject.activeSelf);
                    Assert.AreEqual(seconds >= 1.221f, visual.transform.Find("Impact").gameObject.activeSelf);
                    var meteor = visual.transform.Find("Falling/Veigar_W_MainProjectile/Meteor").GetComponent<MeshRenderer>();
                    Assert.IsTrue(meteor.enabled);
                    Assert.Less(new Vector2(meteor.bounds.center.x - anchor.x, meteor.bounds.center.z - anchor.z).magnitude, .05f);
                }
                if (name == "E_Area")
                {
                    Assert.AreEqual(seconds < .5f, visual.transform.Find("Warning").gameObject.activeSelf);
                    Assert.AreEqual(seconds >= .5f, visual.transform.Find("Boundary").gameObject.activeSelf);
                    var cage = visual.transform.Find("Boundary/Cage/CageEnveloppe").GetComponent<MeshRenderer>();
                    Assert.Less(new Vector2(cage.bounds.center.x - anchor.x, cage.bounds.center.z - anchor.z).magnitude, .05f);
                    Assert.That(cage.bounds.size.x, Is.InRange(6f, 8f));
                }
                foreach (var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
                foreach (var system in name == "E_Area" ? new ParticleSystem[0] : visual.GetComponentsInChildren<ParticleSystem>())
                {
                    var age = seconds;
                    if (name == "W_Area" && system.transform.IsChildOf(visual.transform.Find("Impact"))) age -= 1.221f;
                    if (name == "W_Area" && system.transform.IsChildOf(visual.transform.Find("Falling"))) age -= .65f;
                    if (name == "E_Area" && system.transform.IsChildOf(visual.transform.Find("Boundary"))) age -= .5f;
                    system.Simulate(Mathf.Max(0.01f, age), false, true, true);
                }
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << 31;
                camera.orthographic = true;
                camera.orthographicSize = name.EndsWith("Projectile") ? 2 : 5.5f;
                camera.transform.position = anchor + new Vector3(5, 9, -10);
                camera.transform.LookAt(anchor + Vector3.up);
                camera.targetTexture = target;
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.5f;
                light.cullingMask = 1 << 31;
                light.transform.rotation = Quaternion.Euler(45, -30, 0);
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
                pixels.Apply();
                var count = pixels.GetPixels32().Count(p => p.r > 20 || p.g > 20 || p.b > 20);
                TestContext.WriteLine(name + "@" + seconds + " pixels=" + count);
                Assert.Greater(count, 80, "The real stage must be visible at the gameplay anchor.");
                var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../local/Logs/veigar-live-vfx"));
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, name + "-" + Mathf.RoundToInt(seconds * 1000) + ".png"), pixels.EncodeToPNG());
                if (name == "W_Area" && seconds < .65f)
                {
                    camera.transform.position = anchor + Vector3.up * 12;
                    camera.transform.rotation = Quaternion.Euler(90, 0, 0);
                    camera.Render();
                    pixels.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
                    pixels.Apply();
                    var values = pixels.GetPixels32();
                    var boundaryPixels = 0;
                    for (var y = 0; y < 640; y++)
                        for (var x = 0; x < 640; x++)
                        {
                            var radius = new Vector2(x - 319.5f, y - 319.5f).magnitude * 11f / 640f;
                            var value = values[y * 640 + x];
                            if (radius >= 2.1f && radius <= 2.7f && (value.r > 20 || value.g > 20 || value.b > 20))
                                boundaryPixels++;
                        }
                    Assert.Greater(boundaryPixels, 200, "Warning must reach the 2.4-unit damage boundary.");
                    File.WriteAllBytes(Path.Combine(folder, "W_Warning-Footprint.png"), pixels.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(visual);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(lightObject);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(pixels);
            }
        }
    }
}

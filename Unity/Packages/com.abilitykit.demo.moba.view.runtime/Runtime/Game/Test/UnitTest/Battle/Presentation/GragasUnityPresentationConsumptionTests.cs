using System.Collections;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.View.Config;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using PresentationCueStage = AbilityKit.Demo.Moba.Share.PresentationCueStage;
using AbilityKit.World.ECS;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AbilityKit.Game.Test.UnitTest
{
    public sealed class GragasUnityPresentationConsumptionTests
    {
        [TestCase(3, 30050401, 3f, 1f)]
        [TestCase(4, 30050501, 6f, 7f)]
        public void Impact_displaces_enemy_in_Unity_battle(int slot, int skill, float distance, float minimumDisplacement)
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(new[] { skill },
                       worldId: "gragas_knockback_" + slot, heroId: 3005, attributeTemplateId: 3005))
            {
                harness.EnterGameAndWarmup();
                var caster = harness.AssertPlayerActorBound();
                harness.MoveScenarioActor(caster, new MobaAcceptanceVector3Expectation { x = 100, z = 100 });
                var enemy = harness.SpawnScenarioActor("enemy", 0, "hero", 2, 1001, 1001,
                    1, 0, 0, "p2", 0, null, 0,
                    new MobaAcceptanceVector3Expectation { x = 100, z = 100 + distance });
                harness.Tick(2);
                var entity = harness.AssertActorEntity(enemy);
                if (entity.hasActorBrain) entity.RemoveActorBrain();
                var origin = entity.transform.Value.Position;
                var hp = new AbilityKit.Demo.Moba.Attributes.MobaAttrs(entity).Hp;
                Assert.IsTrue(harness.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(caster, slot,
                    slot == 3 ? Vec3.Zero : origin, Vec3.Forward, 0).Success);
                harness.Tick(48);
                Assert.Less(new AbilityKit.Demo.Moba.Attributes.MobaAttrs(entity).Hp, hp, "Impact must damage the enemy.");
                Assert.Greater(entity.transform.Value.Position.Z - origin.Z, minimumDisplacement - .15f,
                    "Impact must move the enemy, independently of its animation.");
            }
        }

        [TestCase("life/alive/action/idle", 0, "Gragas_Model_Idle")]
        [TestCase("life/alive/action/barrel.flight", 0, "Gragas_Model_Idle")]
        [TestCase("life/alive/action/barrel.deployed", 0, "Gragas_Model_Idle")]
        [TestCase("life/alive/action/empowered", 0, "Gragas_Model_Idle")]
        [TestCase("life/alive/action/casting", 30050101, "Gragas_Model_AutoAttack")]
        [TestCase("life/alive/action/casting", 30050201, "Gragas_Model_SpellQ")]
        [TestCase("life/alive/action/casting", 30050401, "Gragas_Model_SpellE")]
        [TestCase("life/alive/action/casting", 30050501, "Gragas_Model_SpellR")]
        [TestCase("life/alive/action/casting", 30050601, "Gragas_Model_SpellQ")]
        [TestCase("life/alive/action/casting", 30050701, "Gragas_Model_AutoAttack_2")]
        public void Hfsm_actions_drive_the_actual_model_animator(string path, int skill, string state)
        {
            var resources = new BattleViewResourceProvider();
            var shell = resources.CreateShellGameObject(3005001, 3005);
            Assert.IsNotNull(shell);
            try
            {
                var loader = new ResourcesTextAssetLoader();
                var component = new BattleCharacterHfsmComponent(
                    MobaCharacterHfsmProfile.ResolveDefinition(loader, 3005),
                    CharacterPresentationActionCatalog.Load(loader), 3005);
                component.ApplyPresentation(new CharacterActionData(actorId: 3005001, entityVersion: 1,
                    snapshotFrame: 0, path: path, instanceId: ((long)3005001 << 32) | 1u,
                    startFrame: 0, localFrame: 0, castInstanceId: skill > 0 ? 1 : 0, skillId: skill));
                var view = shell.GetComponent<MonoCharacterHfsmView>() ?? shell.AddComponent<MonoCharacterHfsmView>();
                view.Bind(component, 24);
                var animator = shell.GetComponentInChildren<Animator>(true);
                Assert.IsNotNull(animator);
                Assert.AreEqual(Animator.StringToHash(state), animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
                Assert.IsNotEmpty(shell.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            }
            finally { Object.DestroyImmediate(shell); }
        }

        [UnityTest]
        public IEnumerator Q_keeps_same_bucket_through_deployed_and_automatic_detonation() =>
            Projectile(1, 30050201, 32050201, 90035009);

        [Test]
        public void Q_derived_segments_sample_visible_deployed_bucket_and_complete_half_second_tail()
        {
            var prefab = Resources.Load<GameObject>("moba/presentation/gragas/Q_Barrel");
            Assert.IsNotNull(prefab);
            var instance = Object.Instantiate(prefab);
            try
            {
                var animator = instance.GetComponentInChildren<Animator>(true);
                var barrel = animator.transform.Find("Barrel_Q");
                Assert.IsNotNull(barrel);
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.speed = 0;
                animator.Play("Base Layer.deployed", 0, .5f); animator.Update(0);
                Assert.AreEqual(4.25f, animator.GetCurrentAnimatorClipInfo(0)[0].clip.length, .001f);
                Assert.AreEqual(.01f, barrel.localScale.x, .0001f);
                Assert.AreEqual(.01f, barrel.localScale.y, .0001f);
                Assert.AreEqual(.01f, barrel.localScale.z, .0001f);
                animator.Play("Base Layer.detonating", 0, 1f); animator.Update(0);
                Assert.AreEqual(.5f, animator.GetCurrentAnimatorClipInfo(0)[0].clip.length, .001f);
                Assert.AreEqual(.002f, barrel.localScale.x, .0001f);
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [UnityTest]
        public IEnumerator Q_manual_detonation_seeks_tail_and_releases_same_bucket() =>
            Projectile(1, 30050201, 32050201, 90035009, manual: true);

        [UnityTest]
        public IEnumerator R_real_flight_creates_actor_following_vfx_and_releases_on_arrival() =>
            Projectile(4, 30050501, 32050501, 90035013);

        [TestCase(1f, 0f)]
        [TestCase(0f, 1f)]
        public void E_hud_direction_release_moves_actor_without_an_aim_point(float dx, float dz)
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(new[] { 30050401 },
                       worldId: "gragas_e_hud_direction", heroId: 3005, attributeTemplateId: 3005))
            {
                harness.EnterGameAndWarmup();
                var actor = harness.AssertPlayerActorBound();
                // The default map has a blocker near +X from the spawn; use an empty lane for this input test.
                harness.MoveScenarioActor(actor, new MobaAcceptanceVector3Expectation { x = 100, z = 100 });
                harness.Tick(2);
                var origin = harness.AssertActorEntity(actor).transform.Value.Position;
                var context = BattleContext.Rent();
                try
                {
                    context.ApplyLocalActorIdentity(actor);
                    var lookup = new BattleHudSkillButtonTemplateConfigLookup(new BattleViewResourceProvider(harness.Config, null));
                    Assert.IsTrue(lookup.TryResolve(30050401, out _, out _, out var spec));
                    var dispatcher = new BattleHudInputEventDispatcher(context);
                    dispatcher.SetSkillSpecs(new Dictionary<int, BattleHudSkillPresentationSpec> { [3] = spec });
                    dispatcher.OnSkillAimStart(3, new Vector2(-dz, dx));
                    dispatcher.OnSkillAimEnd(3, new Vector2(dx, dz));
                    Assert.IsTrue(context.TryConsumeHudSkillAimSubmit(out var slot, out var px, out var py,
                        out var pz, out var ax, out var ay, out var az));
                    Assert.AreEqual(3, slot);
                    Assert.AreEqual(Vec3.Zero, new Vec3(px, py, pz));
                    Assert.AreEqual(new Vec3(dx, 0f, dz), new Vec3(ax, ay, az));
                    harness.SubmitSkillInput(harness.PlayerId, slot, SkillInputPhase.Release,
                        aimPos: new Vec3(px, py, pz), aimDir: new Vec3(ax, ay, az));
                    harness.Tick(1);
                    Assert.AreEqual(30050401, CaptureAction(harness, actor).SkillId);
                    harness.Tick(48);
                    var position = harness.AssertActorEntity(actor).transform.Value.Position;
                    Assert.AreEqual(origin.X + dx * 6f, position.X, .05f);
                    Assert.AreEqual(origin.Z + dz * 6f, position.Z, .05f);
                }
                finally { BattleContext.Return(context); }
            }
        }

        [UnityTest]
        public IEnumerator W_real_cast_plays_full_drink_clip_at_authored_speed_and_restores_idle()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(new[] { 30050301 },
                       worldId: "gragas_w_animator", heroId: 3005, attributeTemplateId: 3005))
            {
                harness.EnterGameAndWarmup();
                var actor = harness.AssertPlayerActorBound();
                var cast = harness.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(actor, 2,
                    Vec3.Zero, Vec3.Forward, 0);
                Assert.IsTrue(cast.Success, cast.FailReason);
                harness.Tick(1);
                var action = CaptureAction(harness, actor);
                Assert.AreEqual(30050301, action.SkillId);
                var shell = new BattleViewResourceProvider().CreateShellGameObject(actor, 3005);
                try
                {
                    var loader = new ResourcesTextAssetLoader();
                    var component = new BattleCharacterHfsmComponent(MobaCharacterHfsmProfile.ResolveDefinition(loader, 3005),
                        CharacterPresentationActionCatalog.Load(loader), 3005);
                    component.ApplyPresentation(action);
                    var view = shell.GetComponent<MonoCharacterHfsmView>() ?? shell.AddComponent<MonoCharacterHfsmView>();
                    view.Bind(component, harness.TickRate);
                    var animator = shell.GetComponentInChildren<Animator>(true);
                    for (var frame = 0; frame < 60 && animator.GetCurrentAnimatorStateInfo(0).shortNameHash !=
                         Animator.StringToHash("Gragas_Model_SpellW"); frame++) yield return null;
                    animator.Update(0f);
                    Assert.AreEqual(Animator.StringToHash("Gragas_Model_SpellW"), animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
                    Assert.AreEqual(6f, animator.speed, 0.001f);
                    view.SeekToFrame(action.SnapshotFrame + 1, harness.FixedDelta);
                    Assert.AreEqual(6f, animator.speed, 0.001f, "HFSM wait must not overwrite timeline animation speed.");
                    // Restore midway through the cast: source time must include the 6x playback speed.
                    view.SeekToFrame(action.StartFrame - 1, harness.FixedDelta);
                    view.SeekToFrame(action.StartFrame + 12, harness.FixedDelta);
                    for (var frame = 0; frame < 3; frame++) yield return null;
                    animator.Update(0f);
                    Assert.AreEqual(12 * harness.FixedDelta, animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 0.01f);
                    harness.Tick(40);
                    var after = CaptureAction(harness, actor);
                    Assert.AreEqual(0, after.CastInstanceId);
                    component.ApplyPresentation(after);
                    view.SeekToFrame(after.SnapshotFrame, harness.FixedDelta);
                    animator.Update(0f);
                    Assert.AreEqual(Animator.StringToHash("Gragas_Model_Idle"), animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
                    Assert.AreEqual(1f, animator.speed, 0.001f);
                }
                finally { Object.DestroyImmediate(shell); }
            }
        }

        [TestCase(1, 30050201, 42050201, 90035006)]
        [TestCase(1, 30050201, 42050202, 90035007)]
        [TestCase(4, 30050501, 42050501, 90035012)]
        public void Real_area_cues_create_at_actual_center_and_release_the_owned_vfx(int slot, int skill, int area, int expectedVfx)
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(new[] { skill },
                       worldId: "gragas_area_" + area, heroId: 3005, attributeTemplateId: 3005))
            {
                harness.EnterGameAndWarmup();
                var actor = harness.AssertPlayerActorBound();
                var world = new EntityWorld();
                var root = world.Create("gragas-area-view");
                var query = new BattleEntityQuery(world, new BattleEntityLookup());
                var manager = new BattleVfxManager(BattleViewFactory.GetOrLoadVfxDb());
                var handler = new BattlePresentationCueViewEventHandler(world, query, manager, root);
                var started = false;
                var ended = false;
                try
                {
                    Assert.IsTrue(harness.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(actor, slot,
                        new Vec3(6, 0, 0), Vec3.Right, 0).Success);
                    for (var tick = 0; tick < 180 && !ended; tick++)
                    {
                        harness.Tick(1);
                        var snapshots = new List<WorldStateSnapshot>();
                        harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>().CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
                        foreach (var snapshot in snapshots)
                        {
                            if (snapshot.OpCode != MobaOpCodes.Snapshot.PresentationCue) continue;
                            foreach (var entry in MobaPresentationCueSnapshotCodec.Deserialize(snapshot.Payload))
                            {
                                if (entry.TemplateId != area) continue;
                                var cue = PresentationCueSnapshotMapper.Map(in entry);
                                handler.HandleSnapshot(new[] { cue });
                                if (cue.Stage == PresentationCueStage.Started)
                                {
                                    started = true;
                                    Assert.AreEqual(1, root.ChildCount);
                                    var entity = root.GetChild(0);
                                    Assert.IsTrue(entity.TryGetRef(out BattleVfxComponent vfx));
                                    Assert.AreEqual(expectedVfx, vfx.VfxId);
                                    Assert.IsTrue(entity.TryGetRef(out BattleViewGameObjectComponent visual));
                                    Assert.IsNotNull(visual.GameObject);
                                    Assert.IsNotEmpty(visual.GameObject.GetComponentsInChildren<Renderer>(true));
                                    Assert.Less(Vector3.Distance(new Vector3(6, 0, 0), visual.GameObject.transform.position), 0.05f);
                                    handler.HandleSnapshot(new[] { cue });
                                    Assert.AreEqual(1, root.ChildCount);
                                }
                                else if (BattlePresentationCueResolver.ShouldStop(cue.Stage)) ended = true;
                            }
                        }
                    }
                    Assert.IsTrue(started, "Missing formal area start cue.");
                    Assert.IsTrue(ended, "Missing formal area end cue.");
                    Assert.AreEqual(0, root.ChildCount);
                }
                finally { handler.Clear(); manager.Clear(root); if (root.IsValid) root.Destroy(); }
            }
        }

        private static CharacterActionData CaptureAction(MobaSkillConfigTestHarness harness, int actor)
        {
            var snapshots = new List<WorldStateSnapshot>();
            harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>().CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
            foreach (var snapshot in snapshots)
                if (CharacterActionSnapshotRoute.TryDecode(snapshot, out var entries))
                    foreach (var entry in entries) if (entry.ActorId == actor) return entry;
            Assert.Fail("Missing character action snapshot.");
            return default;
        }

        private static IEnumerator Projectile(int slot, int skill, int projectileTemplate, int vfxId, bool manual = false)
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(new[] { skill },
                       worldId: "gragas_vfx_" + skill, heroId: 3005, attributeTemplateId: 3005))
            {
                harness.EnterGameAndWarmup();
                var actor = harness.AssertPlayerActorBound();
                var cast = harness.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(actor, slot,
                    new Vec3(6, 0, 0), Vec3.Right, 0);
                Assert.IsTrue(cast.Success, cast.FailReason);
                var world = new EntityWorld();
                var root = world.Create("gragas-projectile-view");
                var query = new BattleEntityQuery(world, new BattleEntityLookup());
                var database = BattleViewFactory.GetOrLoadVfxDb();
                var manager = new BattleVfxManager(database);
                var handler = new BattleProjectileViewEventHandler(world, query, manager, in root,
                    new BattleViewResourceProvider(harness.Config, database));
                var instanceId = default(IEntityId);
                var spawned = false;
                var exited = false;
                var deployed = false;
                var detonating = false;
                var recast = false;
                var recovered = false;
                var tailFrame = -1;
                try
                {
                    for (var tick = 0; tick < 240 && !exited; tick++)
                    {
                        harness.Tick(1);
                        var snapshots = new List<WorldStateSnapshot>();
                        harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>()
                            .CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
                        foreach (var snapshot in snapshots)
                        {
                            if (snapshot.OpCode != MobaOpCodes.Snapshot.ProjectileEvent) continue;
                            foreach (var entry in MobaProjectileEventSnapshotCodec.Deserialize(snapshot.Payload))
                            {
                                if (entry.TemplateId != projectileTemplate) continue;
                                handler.HandleSnapshot(new[] { ProjectileEventSnapshotMapper.Map(in entry) });
                                if (entry.Kind == (int)ProjectileEventKind.Spawn)
                                {
                                    spawned = true;
                                    Assert.AreEqual(1, root.ChildCount);
                                    var entity = root.GetChild(0);
                                    instanceId = entity.Id;
                                    Assert.IsTrue(entity.TryGetRef(out BattleVfxComponent vfx));
                                    Assert.AreEqual(vfxId, vfx.VfxId);
                                    Assert.IsTrue(entity.TryGetRef(out BattleViewGameObjectComponent view));
                                    Assert.IsNotNull(view.GameObject);
                                    Assert.IsNotEmpty(view.GameObject.GetComponentsInChildren<Renderer>(true));
                                    Assert.IsTrue(entity.TryGetRef(out BattleViewFollowComponent follow));
                                    Assert.AreEqual(entry.ProjectileActorId, follow.TargetActorId);
                                    foreach (var animator in view.GameObject.GetComponentsInChildren<Animator>(true))
                                        Assert.AreEqual(0, animator.parameterCount, "These authored VFX use their default animation state.");
                                }
                                else if (entry.Kind == (int)ProjectileEventKind.Exit)
                                {
                                    exited = true;
                                    Assert.IsFalse(world.IsAlive(instanceId));
                                    if (projectileTemplate == 32050201)
                                    {
                                        Assert.IsTrue(deployed);
                                        Assert.IsTrue(detonating);
                                        Assert.GreaterOrEqual(harness.FrameTime.Frame.Value - tailFrame,
                                            Mathf.CeilToInt(.5f / harness.FixedDelta) - 1);
                                    }
                                }
                                else if (entry.Kind == (int)ProjectileEventKind.Phase && projectileTemplate == 32050201)
                                {
                                    Assert.AreEqual(1, root.ChildCount, "Phase updates must reuse the same bucket.");
                                    Assert.AreEqual(instanceId, root.GetChild(0).Id);
                                    var entity = root.GetChild(0);
                                    Assert.IsTrue(entity.TryGetRef(out BattleViewGameObjectComponent view));
                                    var animator = view.GameObject.GetComponentInChildren<Animator>(true);
                                    Assert.AreEqual(Animator.StringToHash(entry.StatePath.TrimStart('/')), animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
                                    Assert.AreEqual(0, animator.speed);
                                    if (entry.StatePath == "/deployed")
                                    {
                                        deployed = true;
                                        Assert.IsTrue(harness.World.Services.Resolve<IProjectileService>()
                                            .TryGetRuntimeState(new ProjectileId(entry.ProjectileId), out var state));
                                        Assert.IsTrue(state.HasArrived);
                                        Assert.AreEqual(6f, state.Position.X, .02f);
                                        if (!recovered && entry.LocalSeconds > .5f)
                                        {
                                            VerifyPhaseRecovery(in entry, harness);
                                            recovered = true;
                                        }
                                        if (manual && !recast && entry.LocalSeconds > 1f)
                                        {
                                            var result = harness.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(actor, 1,
                                                new Vec3(8, 0, 0), Vec3.Right, 0);
                                            Assert.IsTrue(result.Success, result.FailReason);
                                            recast = true;
                                        }
                                    }
                                    if (entry.StatePath == "/detonating")
                                    {
                                        if (!detonating) tailFrame = entry.EnteredFrame;
                                        detonating = true;
                                        Assert.AreEqual(Mathf.Clamp01(entry.LocalSeconds / .5f),
                                            animator.GetCurrentAnimatorStateInfo(0).normalizedTime, .005f);
                                    }
                                }
                            }
                        }
                    }
                    Assert.IsTrue(spawned);
                    Assert.IsTrue(exited);
                    Assert.AreEqual(0, root.ChildCount);
                    if (projectileTemplate == 32050201) Assert.IsTrue(recovered);
                }
                finally { manager.Clear(root); if (root.IsValid) root.Destroy(); }
            }
            yield return null;
        }

        private static void VerifyPhaseRecovery(in MobaProjectileEventSnapshotEntry entry, MobaSkillConfigTestHarness harness)
        {
            var world = new EntityWorld();
            var root = world.Create("q-late-phase-consumer");
            var database = BattleViewFactory.GetOrLoadVfxDb();
            var manager = new BattleVfxManager(database);
            var handler = new BattleProjectileViewEventHandler(world,
                new BattleEntityQuery(world, new BattleEntityLookup()), manager, in root,
                new BattleViewResourceProvider(harness.Config, database));
            try
            {
                var data = ProjectileEventSnapshotMapper.Map(in entry);
                handler.HandleSnapshot(new[] { data });
                handler.HandleSnapshot(new[] { data });
                Assert.AreEqual(1, root.ChildCount, "Repeated recovery must not duplicate the bucket.");
                Assert.IsTrue(root.GetChild(0).TryGetRef(out BattleViewGameObjectComponent view));
                var animator = view.GameObject.GetComponentInChildren<Animator>(true);
                Assert.AreEqual(Animator.StringToHash("deployed"), animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
                Assert.AreEqual(Mathf.Clamp01(entry.LocalSeconds / 4f), animator.GetCurrentAnimatorStateInfo(0).normalizedTime, .005f);
                Assert.IsNotEmpty(view.GameObject.GetComponentsInChildren<Renderer>(true));
                var current = entry;
                current.ActorVersion++;
                handler.HandleSnapshot(new[] { ProjectileEventSnapshotMapper.Map(in current) });
                var replacement = root.GetChild(0).Id;
                Assert.AreNotEqual(default(IEntityId), replacement);
                handler.HandleSnapshot(new[] { data }); // An old generation cannot replace the current bucket.
                Assert.AreEqual(replacement, root.GetChild(0).Id);
                handler.Clear();
                Assert.AreEqual(0, root.ChildCount);
            }
            finally { manager.Clear(root); if (root.IsValid) root.Destroy(); }
        }
    }
}

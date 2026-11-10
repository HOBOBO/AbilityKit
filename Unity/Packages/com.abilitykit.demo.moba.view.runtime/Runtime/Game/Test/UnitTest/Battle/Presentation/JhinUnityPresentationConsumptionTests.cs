using System.Collections;
using System.Collections.Generic;
using AbilityKit.Ability.Host;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.View.Config;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Game.Platform.Unity.Presentation;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.World.ECS;
using AbilityKit.Game.Test.UnitTest;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AbilityKit.Game.Tests
{
    /// <summary>
    /// Runtime-facing smoke tests for the Jhin model and presentation backend.
    /// The resource/config tests prove that assets are published; these tests
    /// prove the Unity consumers actually play and release them.
    /// </summary>
    public sealed class JhinUnityPresentationConsumptionTests
    {
        private const string JhinModelResource =
            "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_Model";
        private const string JhinQVfxResource =
            "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_Q";
        private const string JhinEHitVfxResource =
            "ChampionVfx/13.Jhin_Battlecast/Jhin_Battlecast_E_Hit";

        [TestCase("life/alive/action/attack.slot.1", 0, 10, "AutoAttack_1")]
        [TestCase("life/alive/action/attack.slot.2", 0, 10, "AutoAttack_2")]
        [TestCase("life/alive/action/attack.slot.3", 0, 10, "AutoAttack_3")]
        [TestCase("life/alive/action/attack.slot.4", 0, 10, "AutoAttack_4")]
        [TestCase("life/alive/action/attack.slot.5", 0, 10, "AutoAttack_Reload")]
        [TestCase("life/alive/action/casting", 30010101, 10, "Spell_Q")]
        [TestCase("life/alive/action/casting", 30010201, 10, "Spell_W")]
        [TestCase("life/alive/action/casting", 30010301, 10, "Spell_E")]
        [TestCase("life/alive/action/casting", 30010401, 10, "Spell_R_Deploy")]
        [TestCase("life/alive/action/casting", 30010401, 43, "Spell_R_Idle")]
        [TestCase("life/alive/action/r.aiming", 0, 10, "Spell_R_Idle")]
        [TestCase("life/alive/action/r.shooting", 0, 10, "Spell_R_Shoot")]
        [TestCase("life/alive/action/r.final_shot", 0, 10, "Spell_R_ShootLast")]
        [TestCase("life/alive/action/idle", 0, 10, "Idle")]
        [TestCase("life/alive/action/controlled", 0, 10, "Idle")]
        [TestCase("life/dead", 0, 10, "Death")]
        public void JhinHfsmView_ConsumesPublishedAnimatorAction(
            string path, int skillId, int localFrame, string expectedState)
        {
            var prefab = Resources.Load<GameObject>(JhinModelResource);
            Assert.IsNotNull(prefab, "Jhin model prefab must be available through Resources.");

            var instance = Object.Instantiate(prefab);
            try
            {
                var animator = instance.GetComponentInChildren<Animator>(true);
                Assert.IsNotNull(animator, "Jhin model must expose an Animator.");

                var view = instance.GetComponent<MonoCharacterHfsmView>();
                if (view == null) view = instance.AddComponent<MonoCharacterHfsmView>();
                var component = new BattleCharacterHfsmComponent(
                    MobaCharacterHfsmTestFixture.CreateDefinition(),
                    CharacterPresentationActionCatalog.Load(new ResourcesTextAssetLoader()),
                    entityCode: 3001);

                component.ApplyPresentation(new CharacterActionData(
                    actorId: 41,
                    entityVersion: 1,
                    snapshotFrame: 0,
                    path: path,
                    instanceId: ((long)41 << 32) | 100u,
                    startFrame: 0,
                    localFrame: 0,
                    castInstanceId: 0,
                    skillId: skillId));
                view.Bind(component, 24);
                view.SeekToFrame(localFrame, 1f / 24f);
                animator.Update(0f);
                Assert.AreEqual(
                    Animator.StringToHash(expectedState),
                    animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                    path + " must select the published model action.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [UnityTest]
        public IEnumerator Q_RealCastSnapshotPlaysModelAndTimelineVfx() =>
            VerifyRealCastPresentation(30010101, 1, "Spell_Q", "Jhin_Battlecast_Q");

        [UnityTest]
        public IEnumerator Q_RealProjectileSnapshotsPlayAndReleaseConfiguredProjectileVfx()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                new[] { 30010101 }, worldId: "jhin_unity_q_projectile_vfx",
                heroId: 3001, attributeTemplateId: 3001))
            {
                harness.EnterGameAndWarmup();
                var actorId = harness.AssertPlayerActorBound();
                var targetId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "jhin_q_projectile_vfx_target", 9701, x: 0f, z: 4f);
                var cast = harness.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(
                    actorId, 1, aimPos: new Vec3(0f, 0f, 4f),
                    aimDir: Vec3.Forward, targetActorId: targetId);
                Assert.IsTrue(cast.Success, "Jhin Q cast failed: " + cast.FailReason);

                var vfxWorld = new EntityWorld();
                var vfxRoot = vfxWorld.Create("jhin-q-projectile-vfx-root");
                var lookup = new BattleEntityLookup();
                var query = new BattleEntityQuery(vfxWorld, lookup);
                var vfxDb = BattleViewFactory.GetOrLoadVfxDb();
                Assert.IsNotNull(vfxDb);
                var manager = new BattleVfxManager(vfxDb);
                var resources = new BattleViewResourceProvider(harness.Config, vfxDb);
                var handler = new BattleProjectileViewEventHandler(
                    vfxWorld, query, manager, in vfxRoot, resources);
                var projectileActorId = 0;
                var projectileVfxEntityId = default(IEntityId);
                var sawHit = false;
                var sawExit = false;

                try
                {
                    for (var tick = 0; tick < 120 && !sawExit; tick++)
                    {
                        harness.Tick(1);
                        var snapshots = new List<WorldStateSnapshot>();
                        harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>()
                            .CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
                        for (var snapshotIndex = 0; snapshotIndex < snapshots.Count; snapshotIndex++)
                        {
                            if (snapshots[snapshotIndex].OpCode != MobaOpCodes.Snapshot.ProjectileEvent)
                                continue;
                            var entries = MobaProjectileEventSnapshotCodec.Deserialize(
                                snapshots[snapshotIndex].Payload);
                            for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                            {
                                var entry = entries[entryIndex];
                                if (entry.TemplateId != 32010101) continue;
                                var data = ProjectileEventSnapshotMapper.Map(in entry);
                                handler.HandleSnapshot(new[] { data });

                                if (entry.Kind == (int)ProjectileEventKind.Spawn)
                                {
                                    projectileActorId = entry.ProjectileActorId;
                                    Assert.Greater(projectileActorId, 0,
                                        "Q projectile spawn snapshot must expose the moving projectile actor.");
                                    Assert.AreEqual(1, vfxRoot.ChildCount,
                                        "Q projectile spawn must create one configured VFX entity.");
                                    var vfxEntity = vfxRoot.GetChild(0);
                                    projectileVfxEntityId = vfxEntity.Id;
                                    Assert.IsTrue(vfxEntity.TryGetRef(out BattleVfxComponent vfxComponent));
                                    Assert.AreEqual(90013001, vfxComponent.VfxId,
                                        "Q's projectile template must resolve its configured Battlecast projectile VFX.");
                                    Assert.IsTrue(vfxEntity.TryGetRef(out BattleViewGameObjectComponent viewComponent));
                                    Assert.IsNotNull(viewComponent.GameObject);
                                    Assert.IsNotNull(viewComponent.GameObject.GetComponent<BattleVfxPoolableTag>());
                                    Assert.IsNotNull(viewComponent.GameObject.GetComponentInChildren<ParticleSystem>(true),
                                        "The snapshot path must instantiate the authored particle prefab.");
                                    var animator = FindAnimatorWithTrigger(viewComponent.GameObject, "Q_Shot");
                                    Assert.IsNotNull(animator, "Q projectile VFX must expose its authored spawn animation.");
                                    Assert.IsTrue(HasTrigger(animator, "Q_Shot"),
                                        "Projectile spawn must activate the authored Q shot animation.");
                                    animator.SetTrigger("Q_Shot");
                                    animator.Update(1f / 24f);
                                    Assert.AreEqual(Animator.StringToHash("Shot"),
                                        animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                                        "Projectile spawn snapshot must enter the authored Q shot state.");
                                    var missile = FindChild(viewComponent.GameObject.transform, "Grenade_Missile");
                                    Assert.IsNotNull(missile, "Q projectile VFX must contain its authored grenade mesh.");
                                    Assert.IsTrue(missile.gameObject.activeSelf,
                                        "The authored Q shot animation must activate its grenade mesh.");
                                    Assert.IsNotNull(missile.GetComponentInChildren<Renderer>(true),
                                        "The visible grenade object must contain a renderer.");
                                    Assert.IsTrue(vfxEntity.TryGetRef(out BattleViewFollowComponent follow));
                                    Assert.AreEqual(projectileActorId, follow.TargetActorId,
                                        "Q projectile VFX must follow the authoritative projectile actor.");
                                }
                                else if (entry.Kind == (int)ProjectileEventKind.Hit)
                                {
                                    sawHit = true;
                                    Assert.GreaterOrEqual(vfxRoot.ChildCount, 2,
                                        "A Q hit must retain the moving projectile VFX and add its configured hit VFX.");
                                }
                                else if (entry.Kind == (int)ProjectileEventKind.Exit)
                                {
                                    sawExit = true;
                                    Assert.IsFalse(vfxWorld.IsAlive(projectileVfxEntityId),
                                        "Projectile exit snapshot must release the VFX following that projectile actor.");
                                }
                            }
                        }
                    }

                    Assert.Greater(projectileActorId, 0,
                        "A real Q cast must emit a projectile spawn snapshot.");
                    Assert.IsTrue(sawHit, "Q projectile should hit its locked target in the test scenario.");
                    Assert.IsTrue(sawExit, "Q projectile should emit an exit snapshot after its final available bounce.");
                    Assert.AreEqual(0, vfxRoot.ChildCount,
                        "Q projectile VFX entity must be removed after projectile exit.");
                }
                finally
                {
                    manager.Clear(vfxRoot);
                    if (vfxRoot.IsValid) vfxRoot.Destroy();
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator W_RealCastSnapshotPlaysModelAndTimelineVfx() =>
            VerifyRealCastPresentation(30010201, 2, "Spell_W", "Jhin_Battlecast_W");

        [UnityTest]
        public IEnumerator W_RealProjectileSnapshotsPlayAndReleaseConfiguredProjectileVfx()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                new[] { 30010201 }, worldId: "jhin_unity_w_projectile_vfx",
                heroId: 3001, attributeTemplateId: 3001))
            {
                harness.EnterGameAndWarmup();
                var actorId = harness.AssertPlayerActorBound();
                var targetId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "jhin_w_projectile_vfx_target", 9702, x: 0f, z: 8f);
                var cast = harness.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(
                    actorId, 2, aimPos: new Vec3(0f, 0f, 8f),
                    aimDir: Vec3.Forward, targetActorId: targetId);
                Assert.IsTrue(cast.Success, "Jhin W cast failed: " + cast.FailReason);

                var vfxWorld = new EntityWorld();
                var vfxRoot = vfxWorld.Create("jhin-w-projectile-vfx-root");
                var lookup = new BattleEntityLookup();
                var query = new BattleEntityQuery(vfxWorld, lookup);
                var vfxDb = BattleViewFactory.GetOrLoadVfxDb();
                Assert.IsNotNull(vfxDb);
                var manager = new BattleVfxManager(vfxDb);
                var resources = new BattleViewResourceProvider(harness.Config, vfxDb);
                var handler = new BattleProjectileViewEventHandler(
                    vfxWorld, query, manager, in vfxRoot, resources);
                var projectileActorId = 0;
                var projectileVfxEntityId = default(IEntityId);
                var sawHit = false;
                var sawExit = false;

                try
                {
                    for (var tick = 0; tick < 180 && !sawExit; tick++)
                    {
                        harness.Tick(1);
                        var snapshots = new List<WorldStateSnapshot>();
                        harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>()
                            .CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
                        for (var snapshotIndex = 0; snapshotIndex < snapshots.Count; snapshotIndex++)
                        {
                            if (snapshots[snapshotIndex].OpCode != MobaOpCodes.Snapshot.ProjectileEvent)
                                continue;
                            var entries = MobaProjectileEventSnapshotCodec.Deserialize(
                                snapshots[snapshotIndex].Payload);
                            for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                            {
                                var entry = entries[entryIndex];
                                if (entry.TemplateId != 32010201) continue;
                                var data = ProjectileEventSnapshotMapper.Map(in entry);
                                handler.HandleSnapshot(new[] { data });

                                if (entry.Kind == (int)ProjectileEventKind.Spawn)
                                {
                                    projectileActorId = entry.ProjectileActorId;
                                    Assert.Greater(projectileActorId, 0,
                                        "W projectile spawn snapshot must expose the moving projectile actor.");
                                    Assert.AreEqual(1, vfxRoot.ChildCount,
                                        "W projectile spawn must create one configured VFX entity.");
                                    var vfxEntity = vfxRoot.GetChild(0);
                                    projectileVfxEntityId = vfxEntity.Id;
                                    Assert.IsTrue(vfxEntity.TryGetRef(out BattleVfxComponent vfxComponent));
                                    Assert.AreEqual(90013002, vfxComponent.VfxId,
                                        "W's projectile template must resolve its configured Battlecast projectile VFX.");
                                    Assert.IsTrue(vfxEntity.TryGetRef(out BattleViewGameObjectComponent viewComponent));
                                    Assert.IsNotNull(viewComponent.GameObject);
                                    Assert.IsNotNull(viewComponent.GameObject.GetComponent<BattleVfxPoolableTag>());
                                    Assert.IsNotNull(viewComponent.GameObject.GetComponentInChildren<ParticleSystem>(true),
                                        "The W snapshot path must instantiate the authored particle prefab.");
                                    var animator = FindAnimatorWithTrigger(viewComponent.GameObject, "W_Shot");
                                    Assert.IsNotNull(animator,
                                        "W projectile prefab must expose its authored shot animation trigger.");
                                    Assert.AreEqual(Animator.StringToHash("Shot"),
                                        animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                                        "W projectile spawn must activate the authored shot animation.");
                                    Assert.IsTrue(vfxEntity.TryGetRef(out BattleViewFollowComponent follow));
                                    Assert.AreEqual(projectileActorId, follow.TargetActorId,
                                        "W projectile VFX must follow the authoritative projectile actor.");
                                }
                                else if (entry.Kind == (int)ProjectileEventKind.Hit)
                                {
                                    sawHit = true;
                                    Assert.GreaterOrEqual(vfxRoot.ChildCount, 2,
                                        "A W hit must retain the moving projectile VFX and add its configured hit VFX.");
                                    var sawConfiguredHitVfx = false;
                                    for (var childIndex = 0; childIndex < vfxRoot.ChildCount; childIndex++)
                                    {
                                        var child = vfxRoot.GetChild(childIndex);
                                        if (child.TryGetRef(out BattleVfxComponent hitVfx) && hitVfx.VfxId == 90013017)
                                        {
                                            sawConfiguredHitVfx = true;
                                            break;
                                        }
                                    }
                                    Assert.IsTrue(sawConfiguredHitVfx,
                                        "W hit snapshot must instantiate the configured Battlecast hit VFX.");
                                }
                                else if (entry.Kind == (int)ProjectileEventKind.Exit)
                                {
                                    sawExit = true;
                                    Assert.IsFalse(vfxWorld.IsAlive(projectileVfxEntityId),
                                        "W projectile exit snapshot must release the following VFX.");
                                }
                            }
                        }
                    }

                    Assert.Greater(projectileActorId, 0,
                        "A real W cast must emit a projectile spawn snapshot.");
                    Assert.IsTrue(sawHit, "W projectile should hit the locked target in the test scenario.");
                    Assert.IsTrue(sawExit, "W projectile should emit an exit snapshot after its single hit.");
                    Assert.AreEqual(0, vfxRoot.ChildCount,
                        "W projectile VFX entity must be removed after projectile exit.");
                }
                finally
                {
                    manager.Clear(vfxRoot);
                    if (vfxRoot.IsValid) vfxRoot.Destroy();
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator E_RealCastSnapshotPlaysModelAndTimelineVfx() =>
            VerifyRealCastPresentation(30010301, 3, "Spell_E", "Jhin_Battlecast_E");

        [UnityTest]
        public IEnumerator E_RealProjectileSnapshotsPlayAndReleaseConfiguredTrapVfx()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                new[] { 30010301 }, worldId: "jhin_unity_e_projectile_vfx",
                heroId: 3001, attributeTemplateId: 3001))
            {
                harness.EnterGameAndWarmup();
                var actorId = harness.AssertPlayerActorBound();
                var cast = harness.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(
                    actorId, 3, aimPos: new Vec3(0f, 0f, 5f),
                    aimDir: Vec3.Forward, targetActorId: 0);
                Assert.IsTrue(cast.Success, "Jhin E cast failed: " + cast.FailReason);

                var vfxWorld = new EntityWorld();
                var vfxRoot = vfxWorld.Create("jhin-e-projectile-vfx-root");
                var lookup = new BattleEntityLookup();
                var query = new BattleEntityQuery(vfxWorld, lookup);
                var vfxDb = BattleViewFactory.GetOrLoadVfxDb();
                Assert.IsNotNull(vfxDb);
                var manager = new BattleVfxManager(vfxDb);
                var resources = new BattleViewResourceProvider(harness.Config, vfxDb);
                var handler = new BattleProjectileViewEventHandler(
                    vfxWorld, query, manager, in vfxRoot, resources);
                var projectileActorId = 0;
                var projectileVfxEntityId = default(IEntityId);
                var sawExit = false;

                try
                {
                    for (var tick = 0; tick < 180 && !sawExit; tick++)
                    {
                        harness.Tick(1);
                        var snapshots = new List<WorldStateSnapshot>();
                        harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>()
                            .CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
                        for (var snapshotIndex = 0; snapshotIndex < snapshots.Count; snapshotIndex++)
                        {
                            if (snapshots[snapshotIndex].OpCode != MobaOpCodes.Snapshot.ProjectileEvent)
                                continue;
                            var entries = MobaProjectileEventSnapshotCodec.Deserialize(
                                snapshots[snapshotIndex].Payload);
                            for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                            {
                                var entry = entries[entryIndex];
                                if (entry.TemplateId != 32010301) continue;
                                handler.HandleSnapshot(new[] { ProjectileEventSnapshotMapper.Map(in entry) });

                                if (entry.Kind == (int)ProjectileEventKind.Spawn)
                                {
                                    projectileActorId = entry.ProjectileActorId;
                                    Assert.Greater(projectileActorId, 0,
                                        "E projectile spawn snapshot must expose the moving trap actor.");
                                    Assert.AreEqual(1, vfxRoot.ChildCount,
                                        "E projectile spawn must instantiate one configured trap VFX.");
                                    var vfxEntity = vfxRoot.GetChild(0);
                                    projectileVfxEntityId = vfxEntity.Id;
                                    Assert.IsTrue(vfxEntity.TryGetRef(out BattleVfxComponent vfxComponent));
                                    Assert.AreEqual(90013003, vfxComponent.VfxId,
                                        "E projectile must resolve its configured Battlecast trap prefab.");
                                    Assert.IsTrue(vfxEntity.TryGetRef(out BattleViewGameObjectComponent viewComponent));
                                    Assert.IsNotNull(viewComponent.GameObject);
                                    Assert.IsNotNull(viewComponent.GameObject.GetComponent<BattleVfxPoolableTag>());
                                    Assert.IsNotNull(viewComponent.GameObject.GetComponentInChildren<ParticleSystem>(true),
                                        "The E snapshot path must instantiate the authored trap particle prefab.");
                                    var animator = FindAnimatorWithTrigger(viewComponent.GameObject, "Spell_E");
                                    Assert.IsNotNull(animator,
                                        "E trap projectile prefab must expose its authored deployment animation trigger.");
                                    Assert.AreEqual(Animator.StringToHash("Deploy"),
                                        animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                                        "Projectile spawn must activate the authored E trap deployment animation.");

                                    animator.Play("Deploy", 0, 0.8f);
                                    animator.Update(0f);
                                    handler.Clear();
                                    manager.Clear(vfxRoot);
                                    Assert.AreEqual(0, vfxRoot.ChildCount,
                                        "Resetting the projectile presentation handler should release its current VFX before replay.");
                                    handler.HandleSnapshot(new[] { ProjectileEventSnapshotMapper.Map(in entry) });
                                    vfxEntity = vfxRoot.GetChild(0);
                                    projectileVfxEntityId = vfxEntity.Id;
                                    Assert.IsTrue(vfxEntity.TryGetRef(out BattleViewGameObjectComponent replayedView));
                                    animator = FindAnimatorWithTrigger(replayedView.GameObject, "Spell_E");
                                    Assert.IsNotNull(animator);
                                    animator.Update(1f / 60f);
                                    Assert.Less(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 0.1f,
                                        "The projectile spawn consumer must restart E deployment from the authored trigger.");
                                    Assert.IsTrue(vfxEntity.TryGetRef(out BattleViewFollowComponent follow));
                                    Assert.AreEqual(projectileActorId, follow.TargetActorId,
                                        "E trap VFX must follow the authoritative flying projectile actor.");
                                }
                                else if (entry.Kind == (int)ProjectileEventKind.Exit)
                                {
                                    sawExit = true;
                                    Assert.AreEqual((int)ProjectileExitReason.MaxDistance, entry.ExitReason,
                                        "E trap projectile should finish at its selected ground point.");
                                    Assert.IsFalse(vfxWorld.IsAlive(projectileVfxEntityId),
                                        "E projectile exit must release its actor-following trap VFX.");
                                }
                            }
                        }
                    }

                    Assert.Greater(projectileActorId, 0,
                        "A real E cast must emit a projectile spawn snapshot.");
                    Assert.IsTrue(sawExit, "E projectile must emit an exit snapshot after reaching its ground point.");
                    Assert.AreEqual(0, vfxRoot.ChildCount,
                        "E projectile VFX entity must be removed after projectile exit.");
                }
                finally
                {
                    manager.Clear(vfxRoot);
                    if (vfxRoot.IsValid) vfxRoot.Destroy();
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator R_ModeSnapshotPlaysAimingModelAction() =>
            VerifyRModePresentation();

        [UnityTest]
        public IEnumerator R_RegularShotSnapshotPlaysShootActionAndProjectileVfx() =>
            VerifyRShotPresentation("life/alive/action/r.shooting", 30010501,
                "Spell_R_Shoot", "Jhin_Battlecast_R_Projectile");

        [UnityTest]
        public IEnumerator R_FinalShotSnapshotPlaysFinalActionAndProjectileVfx() =>
            VerifyRShotPresentation("life/alive/action/r.final_shot", 30010502,
                "Spell_R_ShootLast", "Jhin_Battlecast_R_Last_Projectile");

        [UnityTest]
        public IEnumerator R_ShotInterruptedBackToAimingReleasesProjectileVfx()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                new[] { 30010401 }, worldId: "jhin_unity_r_shot_interrupted",
                heroId: 3001, attributeTemplateId: 3001))
            {
                harness.EnterGameAndWarmup();
                var actorId = harness.AssertPlayerActorBound();
                var targetId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "jhin_unity_r_interrupt_target", 9703, x: 0f, z: 4f);
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var enter = skills.TryCastBySlot(actorId, 4, aimPos: default,
                    aimDir: Vec3.Forward, targetActorId: 0);
                Assert.IsTrue(enter.Success, "R mode cast failed: " + enter.FailReason);

                CharacterActionData aiming = default;
                for (var tick = 0; tick < 30; tick++)
                {
                    harness.Tick(1);
                    aiming = CaptureAction(harness, actorId);
                    if (aiming.Path.EndsWith("action/r.aiming", System.StringComparison.Ordinal)) break;
                }
                StringAssert.EndsWith("action/r.aiming", aiming.Path);

                var shot = skills.TryCastBySlot(actorId, 5, aimPos: default,
                    aimDir: Vec3.Forward, targetActorId: targetId);
                Assert.IsTrue(shot.Success, "R shot cast failed: " + shot.FailReason);
                CharacterActionData shooting = default;
                for (var tick = 0; tick < 120; tick++)
                {
                    harness.Tick(1);
                    shooting = CaptureAction(harness, actorId);
                    if (shooting.CastInstanceId > 0 && shooting.SkillId == 30010501 &&
                        shooting.Path.EndsWith("action/r.shooting", System.StringComparison.Ordinal)) break;
                }
                Assert.AreEqual(30010501, shooting.SkillId);
                Assert.Greater(shooting.CastInstanceId, 0);
                StringAssert.EndsWith("action/r.shooting", shooting.Path);

                var instance = Object.Instantiate(Resources.Load<GameObject>(JhinModelResource));
                try
                {
                    var view = instance.GetComponent<MonoCharacterHfsmView>() ??
                        instance.AddComponent<MonoCharacterHfsmView>();
                    var component = new BattleCharacterHfsmComponent(
                        MobaCharacterHfsmTestFixture.CreateDefinition(),
                        CharacterPresentationActionCatalog.Load(new ResourcesTextAssetLoader()), 3001);
                    component.ApplyPresentation(shooting);
                    view.Bind(component, harness.TickRate);
                    for (var frame = 0; frame < 180 &&
                         FindTimelineVfx(instance, "Jhin_Battlecast_R_Projectile") == null; frame++)
                        yield return null;
                    var projectileVfx = FindTimelineVfx(instance, "Jhin_Battlecast_R_Projectile");
                    Assert.IsNotNull(projectileVfx,
                        "The real R shot snapshot must start its timeline projectile VFX.");

                    // Roll back before this cast snapshot, then re-apply the
                    // same authoritative cast. The old visual must be released
                    // and the timeline must rebuild a fresh instance.
                    view.SeekToFrame(shooting.SnapshotFrame - 1, harness.FixedDelta);
                    Assert.IsTrue(projectileVfx == null,
                        "Rewinding before the R shot snapshot must release its projectile visual.");
                    component.ApplyPresentation(shooting);
                    view.SeekToFrame(shooting.SnapshotFrame, harness.FixedDelta);
                    for (var frame = 0; frame < 180 && FindTimelineVfx(instance,
                             "Jhin_Battlecast_R_Projectile") == null; frame++)
                        yield return null;
                    var replayedProjectileVfx = FindTimelineVfx(instance,
                        "Jhin_Battlecast_R_Projectile");
                    Assert.IsNotNull(replayedProjectileVfx,
                        "Re-applying the authoritative R shot after rewind must rebuild its VFX.");
                    Assert.AreNotSame(projectileVfx, replayedProjectileVfx,
                        "Replayed R shots must use a fresh visual instance after rollback cleanup.");

                    // Model an authoritative HFSM interruption snapshot: the short
                    // shot cast ends while the persistent R aiming mode remains active.
                    var resumedAiming = new CharacterActionData(
                        actorId: shooting.ActorId,
                        entityVersion: shooting.EntityVersion,
                        snapshotFrame: shooting.SnapshotFrame + 1,
                        path: "life/alive/action/r.aiming",
                        instanceId: shooting.InstanceId + 1,
                        startFrame: shooting.SnapshotFrame + 1,
                        localFrame: 0,
                        castInstanceId: 0,
                        skillId: 0,
                        modeId: shooting.ModeId,
                        modeInstanceId: shooting.ModeInstanceId,
                        modeStartFrame: shooting.ModeStartFrame,
                        modeLocalFrame: shooting.ModeLocalFrame + 1);
                    component.ApplyPresentation(resumedAiming);
                    view.SeekToFrame(resumedAiming.SnapshotFrame, harness.FixedDelta);
                    yield return null;
                    yield return null;

                    Assert.IsTrue(replayedProjectileVfx == null,
                        "Interrupting the short R shot must release its projectile visual.");
                    Assert.AreEqual(Animator.StringToHash("Spell_R_Idle"),
                        instance.GetComponentInChildren<Animator>(true)
                            .GetCurrentAnimatorStateInfo(0).shortNameHash,
                        "The presentation HFSM must resume the persistent R aiming action.");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        [UnityTest]
        public IEnumerator ActiveSkillFromAttackSlotPlaysSkillAndRestoresNextAutoAttack()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                new[] { 30010101 }, worldId: "jhin_unity_skill_from_attack_slot",
                heroId: 3001, attributeTemplateId: 3001))
            {
                harness.EnterGameAndWarmup();
                var actorId = harness.AssertPlayerActorBound();
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                harness.AssertSlotSkill(5, 30010601);
                var basicAttack = skills.TryCastBySlot(
                    actorId, 5, aimPos: default, aimDir: Vec3.Forward, targetActorId: 0);
                Assert.IsTrue(basicAttack.Success,
                    "Jhin first basic attack failed: " + basicAttack.FailReason);
                harness.TickUntilSkillStops(actorId, 5, maxTicks: 30,
                    message: "The first basic attack must finish before the active-skill interruption.");

                CharacterActionData attackSlotTwo = default;
                for (var tick = 0; tick < 30; tick++)
                {
                    harness.Tick(1);
                    attackSlotTwo = CaptureAction(harness, actorId);
                    if (attackSlotTwo.Path.EndsWith("action/attack.slot.2",
                            System.StringComparison.Ordinal) &&
                        attackSlotTwo.CastInstanceId == 0)
                        break;
                }
                StringAssert.EndsWith("action/attack.slot.2", attackSlotTwo.Path,
                    "The first shot must advance the selected normal-attack action to slot 2.");

                var targetId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "skill_after_attack_target", 9701, x: 0f, z: 4f);
                var q = skills.TryCastBySlot(actorId, 1,
                    aimPos: new Vec3(0f, 0f, 4f), aimDir: Vec3.Forward, targetActorId: targetId);
                Assert.IsTrue(q.Success, "Jhin Q after a basic attack failed: " + q.FailReason);

                CharacterActionData qAction = default;
                for (var tick = 0; tick < 30; tick++)
                {
                    harness.Tick(1);
                    qAction = CaptureAction(harness, actorId);
                    if (qAction.Path.EndsWith("action/casting", System.StringComparison.Ordinal) &&
                        qAction.SkillId == 30010101 && qAction.CastInstanceId > 0)
                        break;
                }
                StringAssert.EndsWith("action/casting", qAction.Path,
                    "An active skill must interrupt the retained attack-slot action.");
                Assert.AreEqual(30010101, qAction.SkillId);
                Assert.Greater(qAction.CastInstanceId, 0);

                var instance = Object.Instantiate(Resources.Load<GameObject>(JhinModelResource));
                try
                {
                    var view = instance.GetComponent<MonoCharacterHfsmView>() ??
                        instance.AddComponent<MonoCharacterHfsmView>();
                    var component = new BattleCharacterHfsmComponent(
                        MobaCharacterHfsmTestFixture.CreateDefinition(),
                        CharacterPresentationActionCatalog.Load(new ResourcesTextAssetLoader()), 3001);
                    component.ApplyPresentation(qAction);
                    view.Bind(component, harness.TickRate);
                    var animator = instance.GetComponentInChildren<Animator>(true);
                    Assert.AreEqual(Animator.StringToHash("Spell_Q"),
                        animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                        "The real cast snapshot must select Jhin's Q model action.");

                    harness.TickUntilSkillStops(actorId, 1, maxTicks: 60,
                        message: "Jhin Q should complete before normal-attack presentation resumes.");
                    CharacterActionData resumed = default;
                    for (var tick = 0; tick < 30; tick++)
                    {
                        harness.Tick(1);
                        resumed = CaptureAction(harness, actorId);
                        if (resumed.Path.EndsWith("action/attack.slot.2",
                                System.StringComparison.Ordinal) &&
                            resumed.CastInstanceId == 0)
                            break;
                    }

                    StringAssert.EndsWith("action/attack.slot.2", resumed.Path,
                        "After Q ends, AttackSlot must drive the HFSM back to the next normal-attack group.");
                    Assert.AreEqual(2f, harness.World.Services
                        .Resolve<MobaCharacterHfsmParameterService>()
                        .Resolve(actorId, MobaCharacterHfsmTestFixture.AttackSlot));
                    component.ApplyPresentation(resumed);
                    view.SeekToFrame(resumed.SnapshotFrame, harness.FixedDelta);
                    yield return null;
                    Assert.AreEqual(Animator.StringToHash("AutoAttack_2"),
                        animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                        "The next normal attack must resume its matching model action.");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        [UnityTest]
        public IEnumerator WAndEFromAttackSlotRestoreTheSelectedNormalAttackAction()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                new[] { 30010101, 30010201, 30010301 },
                worldId: "jhin_unity_active_skill_attack_slot_restore",
                heroId: 3001, attributeTemplateId: 3001))
            {
                harness.EnterGameAndWarmup();
                var actorId = harness.AssertPlayerActorBound();
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var parameters = harness.World.Services.Resolve<MobaCharacterHfsmParameterService>();
                var targetId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "active_skill_attack_slot_target", 9703, x: 0f, z: 8f);

                var firstAttack = skills.TryCastBySlot(actorId, 5, default, Vec3.Forward, 0);
                Assert.IsTrue(firstAttack.Success, "Jhin first basic attack failed: " + firstAttack.FailReason);
                harness.TickUntilSkillStops(actorId, 5, maxTicks: 30,
                    message: "The first basic attack must advance the retained attack slot.");
                CharacterActionData selectedSlot = default;
                for (var tick = 0; tick < 30; tick++)
                {
                    harness.Tick(1);
                    selectedSlot = CaptureAction(harness, actorId);
                    if (selectedSlot.Path.EndsWith("action/attack.slot.2", System.StringComparison.Ordinal) &&
                        selectedSlot.CastInstanceId == 0) break;
                }
                StringAssert.EndsWith("action/attack.slot.2", selectedSlot.Path);

                var activeSkills = new[] { (slot: 2, id: 30010201), (slot: 3, id: 30010301) };
                foreach (var skill in activeSkills)
                {
                    var cast = skills.TryCastBySlot(actorId, skill.slot,
                        new Vec3(0f, 0f, 8f), Vec3.Forward,
                        skill.slot == 2 ? targetId : 0);
                    Assert.IsTrue(cast.Success, $"Jhin skill {skill.id} failed: {cast.FailReason}");

                    CharacterActionData casting = default;
                    for (var tick = 0; tick < 30; tick++)
                    {
                        harness.Tick(1);
                        casting = CaptureAction(harness, actorId);
                        if (casting.SkillId == skill.id && casting.CastInstanceId > 0) break;
                    }
                    Assert.AreEqual(skill.id, casting.SkillId,
                        "The active skill should interrupt the current HFSM attack-slot action.");
                    Assert.Greater(casting.CastInstanceId, 0);

                    harness.TickUntilSkillStops(actorId, skill.slot, maxTicks: 120,
                        message: $"Jhin skill {skill.id} pipeline should finish.");
                    CharacterActionData resumed = default;
                    for (var tick = 0; tick < 30; tick++)
                    {
                        harness.Tick(1);
                        resumed = CaptureAction(harness, actorId);
                        if (resumed.Path.EndsWith("action/attack.slot.2", System.StringComparison.Ordinal) &&
                            resumed.CastInstanceId == 0) break;
                    }
                    StringAssert.EndsWith("action/attack.slot.2", resumed.Path,
                        $"Skill {skill.id} must resume the HFSM-selected normal-attack group.");
                    Assert.AreEqual(2f, parameters.Resolve(actorId,
                            MobaCharacterHfsmTestFixture.AttackSlot),
                        $"Skill {skill.id} must preserve the source-owned next-attack slot.");

                    var instance = Object.Instantiate(Resources.Load<GameObject>(JhinModelResource));
                    try
                    {
                        var view = instance.GetComponent<MonoCharacterHfsmView>() ??
                            instance.AddComponent<MonoCharacterHfsmView>();
                        var component = new BattleCharacterHfsmComponent(
                            MobaCharacterHfsmTestFixture.CreateDefinition(),
                            CharacterPresentationActionCatalog.Load(new ResourcesTextAssetLoader()), 3001);
                        component.ApplyPresentation(resumed);
                        view.Bind(component, harness.TickRate);
                        view.SeekToFrame(resumed.SnapshotFrame, harness.FixedDelta);
                        yield return null;
                        Assert.AreEqual(Animator.StringToHash("AutoAttack_2"),
                            instance.GetComponentInChildren<Animator>(true)
                                .GetCurrentAnimatorStateInfo(0).shortNameHash,
                            $"Skill {skill.id} must restore the matching normal-attack model action.");
                    }
                    finally
                    {
                        Object.DestroyImmediate(instance);
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator BasicAttackRealCastSnapshotsPlayEachSlotVfxAndReloadAction()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                new[] { 30010101 }, worldId: "jhin_unity_basic_attack_slots",
                heroId: 3001, attributeTemplateId: 3001))
            {
                harness.EnterGameAndWarmup();
                var actorId = harness.AssertPlayerActorBound();
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var expectedSkills = new[] { 30010601, 30010602, 30010603, 30010604 };
                var expectedPaths = new[]
                {
                    "life/alive/action/attack.slot.1",
                    "life/alive/action/attack.slot.2",
                    "life/alive/action/attack.slot.3",
                    "life/alive/action/attack.slot.4",
                };
                var expectedParticles = new[]
                {
                    "Jhin_Battlecast_AA_Cast",
                    "Jhin_Battlecast_AA_Cast",
                    "Jhin_Battlecast_AA_Cast",
                    "Jhin_Battlecast_AA_Last_Cast",
                };
                var instance = Object.Instantiate(Resources.Load<GameObject>(JhinModelResource));
                try
                {
                    var view = instance.GetComponent<MonoCharacterHfsmView>() ??
                        instance.AddComponent<MonoCharacterHfsmView>();
                    var component = new BattleCharacterHfsmComponent(
                        MobaCharacterHfsmTestFixture.CreateDefinition(),
                        CharacterPresentationActionCatalog.Load(new ResourcesTextAssetLoader()), 3001);
                    Transform previousVfx = null;

                    for (var shotIndex = 0; shotIndex < expectedSkills.Length; shotIndex++)
                    {
                        harness.AssertSlotSkill(5, expectedSkills[shotIndex]);
                        var cast = skills.TryCastBySlot(actorId, 5, aimPos: default,
                            aimDir: Vec3.Forward, targetActorId: 0);
                        Assert.IsTrue(cast.Success,
                            "Jhin basic attack cast failed: " + cast.FailReason);

                        CharacterActionData action = default;
                        for (var tick = 0; tick < 30; tick++)
                        {
                            harness.Tick(1);
                            action = CaptureAction(harness, actorId);
                            if (action.CastInstanceId > 0 && action.SkillId == expectedSkills[shotIndex] &&
                                string.Equals(action.Path, expectedPaths[shotIndex],
                                    System.StringComparison.Ordinal))
                                break;
                        }

                        Assert.AreEqual(expectedPaths[shotIndex], action.Path,
                            "The committed attack must be owned by its HFSM slot state. " +
                            $"actor={actorId}, skill={action.SkillId}, cast={action.CastInstanceId}, " +
                            $"attackSlot={harness.World.Services.Resolve<MobaCharacterHfsmParameterService>()?.Resolve(actorId, MobaCharacterHfsmTestFixture.AttackSlot)}, " +
                            $"committedSlot={harness.World.Services.Resolve<MobaCharacterHfsmParameterService>()?.Resolve(actorId, MobaCharacterHfsmTestFixture.CommittedAttackSlot)}, " +
                            $"expectedPath={expectedPaths[shotIndex]}, actualPath={action.Path}");
                        Assert.AreEqual(expectedSkills[shotIndex], action.SkillId);
                        Assert.Greater(action.CastInstanceId, 0);
                        Assert.AreNotEqual(0, action.InstanceId);

                        component.ApplyPresentation(action);
                        view.Bind(component, harness.TickRate);
                        var particle = expectedParticles[shotIndex];
                        for (var frame = 0; frame < 180 && FindTimelineVfx(instance, particle) == null; frame++)
                            yield return null;
                        var currentVfx = FindTimelineVfx(instance, particle);
                        Assert.IsNotNull(currentVfx,
                            "The real basic attack must consume its exported timeline VFX. " +
                            $"shot={shotIndex + 1}, skill={action.SkillId}, cast={action.CastInstanceId}, " +
                            $"frame={action.SnapshotFrame}, startFrame={action.StartFrame}, " +
                            $"localFrame={action.LocalFrame}, path={action.Path}, particle={particle}");

                        // Re-consuming the same authoritative snapshot must seek the
                        // existing cast driver instead of emitting another visual.
                        view.SeekToFrame(action.SnapshotFrame, harness.FixedDelta);
                        yield return null;
                        Assert.AreSame(currentVfx, FindTimelineVfx(instance, particle),
                            "Repeated snapshots must not duplicate basic attack VFX.");

                        harness.TickUntilSkillStops(actorId, 5, maxTicks: 30,
                            message: "Jhin basic attack flow should finish before the next slot is submitted.");
                        if (previousVfx != null)
                            Assert.IsTrue(previousVfx == null,
                                "Changing attack slots must release the previous timeline visual.");
                        previousVfx = currentVfx;

                        if (shotIndex < expectedSkills.Length - 1)
                        {
                            CharacterActionData next = default;
                            for (var tick = 0; tick < 30; tick++)
                            {
                                harness.Tick(1);
                                next = CaptureAction(harness, actorId);
                                if (string.Equals(next.Path, expectedPaths[shotIndex + 1],
                                        System.StringComparison.Ordinal))
                                    break;
                            }
                            Assert.AreEqual(expectedPaths[shotIndex + 1], next.Path,
                                "The launch counter must drive the next HFSM attack slot.");
                            Assert.AreEqual(0, next.CastInstanceId,
                                "A new slot must not inherit the prior short-flow CastInstanceId.");
                            component.ApplyPresentation(next);
                            view.SeekToFrame(next.SnapshotFrame, harness.FixedDelta);
                            yield return null;
                            Assert.IsTrue(currentVfx == null,
                                "Entering the next attack slot must stop the old timeline VFX.");
                        }
                    }

                    CharacterActionData reload = default;
                    for (var tick = 0; tick < 30; tick++)
                    {
                        harness.Tick(1);
                        reload = CaptureAction(harness, actorId);
                        if (reload.Path.EndsWith("action/attack.slot.5",
                                System.StringComparison.Ordinal)) break;
                    }
                    StringAssert.EndsWith("action/attack.slot.5", reload.Path,
                        "The fourth shot must enter the authored reload HFSM state.");
                    Assert.AreEqual(0, reload.CastInstanceId,
                        "Reload is a state-owned action and must not carry a short skill cast.");
                    component.ApplyPresentation(reload);
                    view.SeekToFrame(reload.SnapshotFrame, harness.FixedDelta);
                    yield return null;
                    var animator = instance.GetComponentInChildren<Animator>(true);
                    Assert.AreEqual(Animator.StringToHash("AutoAttack_Reload"),
                        animator.GetCurrentAnimatorStateInfo(0).shortNameHash);

                    var parameters = harness.World.Services.Resolve<MobaCharacterHfsmParameterService>();
                    CharacterActionData afterReload = reload;
                    for (var tick = 0; tick < 80; tick++)
                    {
                        harness.Tick(1);
                        afterReload = CaptureAction(harness, actorId);
                        if (!afterReload.Path.EndsWith("action/attack.slot.5",
                                System.StringComparison.Ordinal)) break;
                    }
                    Assert.IsFalse(afterReload.Path.EndsWith("action/attack.slot.5",
                            System.StringComparison.Ordinal),
                        "The authored 75-frame reload action must finish and leave slot 5 through its HFSM condition.");
                    Assert.AreEqual(0f, parameters.Resolve(actorId,
                            MobaCharacterHfsmTestFixture.AttackSlot),
                        "The reload state's logical exit action must clear the source-owned AttackSlot contribution.");
                    Assert.AreEqual(0, afterReload.CastInstanceId,
                        "Reload completion is state-driven and must not create or retain a cast context.");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        [UnityTest]
        public IEnumerator CurtainCallRealShotsReplaceCastVfxAndFinalShotClearsMode()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                new[] { 30010401 }, worldId: "jhin_unity_r_full_chain",
                heroId: 3001, attributeTemplateId: 3001))
            {
                harness.EnterGameAndWarmup();
                var actorId = harness.AssertPlayerActorBound();
                var targetId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "jhin_unity_r_target", 9702, x: 0f, z: 4f);
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var instance = Object.Instantiate(Resources.Load<GameObject>(JhinModelResource));
                try
                {
                    var view = instance.GetComponent<MonoCharacterHfsmView>() ??
                        instance.AddComponent<MonoCharacterHfsmView>();
                    var component = new BattleCharacterHfsmComponent(
                        MobaCharacterHfsmTestFixture.CreateDefinition(),
                        CharacterPresentationActionCatalog.Load(new ResourcesTextAssetLoader()), 3001);
                    var enter = skills.TryCastBySlot(actorId, 4, aimPos: default,
                        aimDir: Vec3.Forward, targetActorId: 0);
                    Assert.IsTrue(enter.Success, "R mode cast failed: " + enter.FailReason);

                    CharacterActionData aiming = default;
                    for (var tick = 0; tick < 30; tick++)
                    {
                        harness.Tick(1);
                        aiming = CaptureAction(harness, actorId);
                        if (aiming.Path.EndsWith("action/r.aiming",
                                System.StringComparison.Ordinal)) break;
                    }
                    StringAssert.EndsWith("action/r.aiming", aiming.Path);
                    component.ApplyPresentation(aiming);
                    view.Bind(component, harness.TickRate);
                    var animator = instance.GetComponentInChildren<Animator>(true);
                    Assert.AreEqual(Animator.StringToHash("Spell_R_Idle"),
                        animator.GetCurrentAnimatorStateInfo(0).shortNameHash);

                    Transform previousVfx = null;
                    for (var shotIndex = 0; shotIndex < 4; shotIndex++)
                    {
                        var shot = skills.TryCastBySlot(actorId, 5, aimPos: default,
                            aimDir: Vec3.Forward, targetActorId: targetId);
                        Assert.IsTrue(shot.Success,
                            "R shot " + (shotIndex + 1) + " cast failed: " + shot.FailReason);
                        var expectedSkill = shotIndex == 3 ? 30010502 : 30010501;
                        var expectedPath = shotIndex == 3
                            ? "life/alive/action/r.final_shot"
                            : "life/alive/action/r.shooting";
                        var expectedParticle = shotIndex == 3
                            ? "Jhin_Battlecast_R_Last_Projectile"
                            : "Jhin_Battlecast_R_Projectile";
                        CharacterActionData action = default;
                        for (var tick = 0; tick < 120; tick++)
                        {
                            harness.Tick(1);
                            action = CaptureAction(harness, actorId);
                            if (action.CastInstanceId > 0 && action.SkillId == expectedSkill &&
                                string.Equals(action.Path, expectedPath,
                                    System.StringComparison.Ordinal)) break;
                        }
                        Assert.AreEqual(expectedPath, action.Path);
                        Assert.AreEqual(expectedSkill, action.SkillId);
                        Assert.Greater(action.CastInstanceId, 0);
                        // Consume each independent short R pipeline at its
                        // authoritative cast boundary. A later simulation
                        // snapshot can already be partway through the 0.5s
                        // projectile clip while Unity is still resolving the
                        // async prefab request, making this lifecycle check
                        // depend on render-frame scheduling.
                        action = new CharacterActionData(
                            actorId: action.ActorId,
                            entityVersion: action.EntityVersion,
                            snapshotFrame: action.StartFrame,
                            path: action.Path,
                            instanceId: action.InstanceId,
                            startFrame: action.StartFrame,
                            localFrame: 0,
                            castInstanceId: action.CastInstanceId,
                            skillId: action.SkillId,
                            modeId: action.ModeId,
                            modeInstanceId: action.ModeInstanceId,
                            modeStartFrame: action.ModeStartFrame,
                            modeLocalFrame: action.ModeLocalFrame);
                        component.ApplyPresentation(action);
                        view.SeekToFrame(action.SnapshotFrame, harness.FixedDelta);
                        for (var frame = 0; frame < 180 && FindTimelineVfx(instance, expectedParticle) == null; frame++)
                            yield return null;
                        var currentVfx = FindTimelineVfx(instance, expectedParticle);
                        Assert.IsNotNull(currentVfx,
                            "Each independent R shot must consume its projectile timeline VFX. " +
                            $"shot={shotIndex + 1}, skill={action.SkillId}, cast={action.CastInstanceId}, " +
                            $"frame={action.SnapshotFrame}, startFrame={action.StartFrame}, " +
                            $"localFrame={action.LocalFrame}, path={action.Path}, particle={expectedParticle}");
                        view.SeekToFrame(action.SnapshotFrame, harness.FixedDelta);
                        yield return null;
                        Assert.AreSame(currentVfx, FindTimelineVfx(instance, expectedParticle),
                            "Repeated R shot snapshots must not duplicate projectile VFX.");
                        if (previousVfx != null)
                            Assert.IsTrue(previousVfx == null,
                                "A new R CastInstanceId must release the previous shot visual.");
                        previousVfx = currentVfx;

                        harness.TickUntilSkillStops(actorId, 5, maxTicks: 120,
                            message: "Each R shot must finish as an independent short skill flow.");
                        if (shotIndex == 2)
                        {
                            for (var tick = 0; tick < 30 && !harness.HasActorBuff(actorId, 32010405); tick++)
                                harness.Tick(1);
                            Assert.IsTrue(harness.HasActorBuff(actorId, 32010405),
                                "The third R shot must promote the final-shot candidate.");
                        }
                    }

                    for (var tick = 0; tick < 30 && harness.HasActorBuff(actorId, 32010404); tick++)
                        harness.Tick(1);
                    Assert.IsFalse(harness.HasActorBuff(actorId, 32010404),
                        "The final R flow must clear the persistent mode modifier.");
                    // Continuous HFSM modifiers are projected after the character
                    // HFSM tick.  The frame that removes the mode buff can
                    // therefore still publish the final-shot action; wait for
                    // the next authoritative snapshot in which the parameter
                    // driven transition has been evaluated before asking the
                    // presentation view to clean up the cast timeline.
                    CharacterActionData after = default;
                    for (var tick = 0; tick < 30; tick++)
                    {
                        harness.Tick(1);
                        after = CaptureAction(harness, actorId);
                        if (!after.Path.EndsWith("action/r.final_shot",
                                System.StringComparison.Ordinal) && after.CastInstanceId == 0)
                            break;
                    }
                    Assert.IsFalse(after.Path.EndsWith("action/r.final_shot",
                            System.StringComparison.Ordinal),
                        "Clearing the R mode parameter must leave the final-shot HFSM state. " +
                        $"phase={harness.World.Services.Resolve<MobaCharacterHfsmParameterService>()?.Resolve(actorId, MobaCharacterHfsmTestFixture.RPhase)}, " +
                        $"sources={FormatHfsmParameterSources(harness.World.Services.Resolve<MobaCharacterHfsmParameterService>(), actorId)}, " +
                        $"mode={harness.HasActorBuff(actorId, 32010404)}, candidate={harness.HasActorBuff(actorId, 32010405)}, " +
                        $"finalPhase={harness.HasActorBuff(actorId, 32010407)}, path={after.Path}, cast={after.CastInstanceId}");
                    Assert.AreEqual(0, after.CastInstanceId,
                        "Leaving the R mode must clear the final-shot cast context.");
                    component.ApplyPresentation(after);
                    view.SeekToFrame(after.SnapshotFrame, harness.FixedDelta);
                    yield return null;
                    Assert.IsNull(FindTimelineVfx(instance, "Jhin_Battlecast_R_Last_Projectile"),
                        "Leaving the R mode must release the final projectile timeline visual.");
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        private static string FormatHfsmParameterSources(MobaCharacterHfsmParameterService service, int actorId)
        {
            if (service == null) return "<null>";
            var entries = service.CaptureSnapshot();
            var values = new List<string>();
            for (var i = 0; i < entries.Length; i++)
                if (entries[i].ActorId == actorId)
                    values.Add(entries[i].ParameterId + ":" + entries[i].OwnerId + "=" + entries[i].Modifier.Magnitude.BaseValue);
            return string.Join(",", values);
        }

        private static IEnumerator VerifyRShotPresentation(
            string path, int skillId, string animatorState, string particleName)
        {
            var instance = Object.Instantiate(Resources.Load<GameObject>(JhinModelResource));
            try
            {
                var view = instance.GetComponent<MonoCharacterHfsmView>() ??
                    instance.AddComponent<MonoCharacterHfsmView>();
                var component = new BattleCharacterHfsmComponent(
                    MobaCharacterHfsmTestFixture.CreateDefinition(),
                    CharacterPresentationActionCatalog.Load(new ResourcesTextAssetLoader()), 3001);
                component.ApplyPresentation(new CharacterActionData(
                    actorId: 41,
                    entityVersion: 1,
                    snapshotFrame: 0,
                    path: path,
                    instanceId: ((long)41 << 32) | (uint)skillId,
                    startFrame: 0,
                    localFrame: 0,
                    castInstanceId: skillId,
                    skillId: skillId));
                view.Bind(component, 24);

                var animator = instance.GetComponentInChildren<Animator>(true);
                Assert.AreEqual(Animator.StringToHash(animatorState),
                    animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                    path + " must select the authored R shot action.");

                for (var frame = 0; frame < 600 && FindTimelineVfx(instance, particleName) == null; frame++)
                    yield return null;
                Assert.IsNotNull(FindTimelineVfx(instance, particleName),
                    "The R shot timeline must instantiate its configured projectile VFX.");

                view.SeekToFrame(-1, 1f / 24f);
                yield return null;
                Assert.IsNull(FindTimelineVfx(instance, particleName),
                    "Leaving the R shot snapshot must release the timeline-owned projectile VFX.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static IEnumerator VerifyRealCastPresentation(
            int skillId, int slot, string animatorState, string particleName)
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                new[] { skillId }, worldId: "jhin_unity_cast_" + skillId,
                heroId: 3001, attributeTemplateId: 3001))
            {
                harness.EnterGameAndWarmup();
                var actorId = harness.AssertPlayerActorBound();
                var targetId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "presentation_target", 9701, x: 0f, z: 4f);
                var cast = harness.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(
                    actorId, slot, aimPos: new Vec3(0f, 0f, 4f),
                    aimDir: Vec3.Forward, targetActorId: skillId == 30010101 ? targetId : 0);
                Assert.IsTrue(cast.Success, "Real skill cast failed: " + cast.FailReason);
                CharacterActionData casting = default;
                for (var tick = 0; tick < 30; tick++)
                {
                    harness.Tick(1);
                    casting = CaptureAction(harness, actorId);
                    if (casting.SkillId == skillId && casting.CastInstanceId > 0) break;
                }
                Assert.AreEqual(skillId, casting.SkillId, "The authoritative snapshot must identify this cast.");
                Assert.Greater(casting.CastInstanceId, 0);
                // Test the authoritative cast boundary, rather than a late
                // snapshot after the short pipeline has already elapsed.
                casting = new CharacterActionData(
                    actorId: casting.ActorId,
                    entityVersion: casting.EntityVersion,
                    snapshotFrame: casting.StartFrame,
                    path: casting.Path,
                    instanceId: casting.InstanceId,
                    startFrame: casting.StartFrame,
                    localFrame: 0,
                    castInstanceId: casting.CastInstanceId,
                    skillId: casting.SkillId,
                    modeId: casting.ModeId,
                    modeInstanceId: casting.ModeInstanceId,
                    modeStartFrame: casting.ModeStartFrame,
                    modeLocalFrame: casting.ModeLocalFrame);
                var instance = Object.Instantiate(Resources.Load<GameObject>(JhinModelResource));
                try
                {
                    var view = instance.GetComponent<MonoCharacterHfsmView>() ??
                        instance.AddComponent<MonoCharacterHfsmView>();
                    var component = new BattleCharacterHfsmComponent(
                        MobaCharacterHfsmTestFixture.CreateDefinition(),
                        CharacterPresentationActionCatalog.Load(new ResourcesTextAssetLoader()), 3001);
                    component.ApplyPresentation(casting);
                    view.Bind(component, harness.TickRate);
                    var animator = instance.GetComponentInChildren<Animator>(true);
                    Assert.AreEqual(Animator.StringToHash(animatorState),
                        animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
                    for (var frame = 0; frame < 600 && FindTimelineVfx(instance, particleName) == null; frame++)
                        yield return null;
                    var first = FindTimelineVfx(instance, particleName);
                    Assert.IsNotNull(first, "The exported timeline/template must instantiate the real VFX.");
                    Assert.That(first.localPosition, Is.EqualTo(new Vector3(0f, 0.15f, 0f)));
                    view.SeekToFrame(casting.SnapshotFrame, harness.FixedDelta);
                    yield return null;
                    Assert.AreSame(first, FindTimelineVfx(instance, particleName),
                        "Repeated snapshot consumption must not duplicate the cast VFX.");

                    // Rewind before the installed snapshot: cancel its resources,
                    // then seek the authoritative cast again to rebuild presentation.
                    view.SeekToFrame(casting.SnapshotFrame - 1, harness.FixedDelta);
                    Assert.IsTrue(first == null, "Rewind must release the old timeline visual.");
                    view.SeekToFrame(casting.SnapshotFrame, harness.FixedDelta);
                    for (var frame = 0; frame < 600 && FindTimelineVfx(instance, particleName) == null; frame++)
                        yield return null;
                    Assert.IsNotNull(FindTimelineVfx(instance, particleName),
                        "Seeking the cast after rewind must rebuild its timeline visual.");

                    CharacterActionData afterCast = default;
                    for (var tick = 0; tick < 120; tick++)
                    {
                        harness.Tick(1);
                        afterCast = CaptureAction(harness, actorId);
                        if (afterCast.CastInstanceId == 0) break;
                    }
                    Assert.AreEqual(0, afterCast.CastInstanceId, "The individual skill pipeline must end.");
                    component.ApplyPresentation(afterCast);
                    view.SeekToFrame(afterCast.SnapshotFrame, harness.FixedDelta);
                    Assert.IsNull(FindTimelineVfx(instance, particleName),
                        "Leaving the cast must release all timeline-owned VFX.");
                    Assert.AreEqual(Animator.StringToHash("Idle"),
                        animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        private static IEnumerator VerifyRModePresentation()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                new[] { 30010401 }, worldId: "jhin_unity_r_mode", heroId: 3001,
                attributeTemplateId: 3001))
            {
                harness.EnterGameAndWarmup();
                var actorId = harness.AssertPlayerActorBound();
                var skills = harness.World.Services.Resolve<SkillCastCoordinator>();
                var cast = skills.TryCastBySlot(actorId, 4, aimPos: default,
                    aimDir: Vec3.Forward, targetActorId: 0);
                Assert.IsTrue(cast.Success, "R mode cast failed: " + cast.FailReason);
                CharacterActionData aiming = default;
                for (var tick = 0; tick < 30; tick++)
                {
                    harness.Tick(1);
                    aiming = CaptureAction(harness, actorId);
                    if (aiming.Path.EndsWith("r.aiming", System.StringComparison.Ordinal)) break;
                }
                StringAssert.EndsWith("r.aiming", aiming.Path);
                var instance = Object.Instantiate(Resources.Load<GameObject>(JhinModelResource));
                try
                {
                    var view = instance.GetComponent<MonoCharacterHfsmView>() ??
                        instance.AddComponent<MonoCharacterHfsmView>();
                    var component = new BattleCharacterHfsmComponent(
                        MobaCharacterHfsmTestFixture.CreateDefinition(),
                        CharacterPresentationActionCatalog.Load(new ResourcesTextAssetLoader()), 3001);
                    component.ApplyPresentation(aiming);
                    view.Bind(component, harness.TickRate);
                    var animator = instance.GetComponentInChildren<Animator>(true);
                    Assert.AreEqual(Animator.StringToHash("Spell_R_Idle"),
                        animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
                    yield return null;
                }
                finally { Object.DestroyImmediate(instance); }
            }
        }

        private static CharacterActionData CaptureAction(MobaSkillConfigTestHarness harness, int actorId)
        {
            var snapshots = new List<WorldStateSnapshot>();
            harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>()
                .CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
            foreach (var snapshot in snapshots)
            {
                if (!CharacterActionSnapshotRoute.TryDecode(snapshot, out var entries)) continue;
                foreach (var entry in entries)
                    if (entry.ActorId == actorId) return entry;
            }
            Assert.Fail("Missing authoritative character action snapshot for actor " + actorId);
            return default;
        }

        private static Transform FindTimelineVfx(GameObject actor, string prefabName)
        {
            var transforms = actor.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
            {
                var candidate = transforms[i];
                if (candidate == actor.transform) continue;
                var inspection = candidate.GetComponent<UnityPresentationInstanceState>();
                if (inspection != null && inspection.IsBound &&
                    inspection.Request.ResourceKey.EndsWith("/" + prefabName,
                        System.StringComparison.Ordinal))
                    return candidate;

                // Keep supporting direct prefab instances used by lower-level tests,
                // while timeline VFX are identified by the requested resource key.
                if (candidate.name == prefabName + "(Clone)" ||
                    candidate.name.StartsWith(prefabName + "(", System.StringComparison.Ordinal))
                    return candidate;
            }
            return null;
        }

        [UnityTest]
        public IEnumerator PresentationUnityBackend_InstantiatesAttachedJhinVfxAndReleasesIt()
        {
            var prefab = Resources.Load<GameObject>(JhinQVfxResource);
            Assert.IsNotNull(prefab, "Jhin Q VFX prefab must be available through Resources.");

            var root = new GameObject("jhin-presentation-root");
            var actor = new GameObject("jhin-presentation-actor");
            actor.transform.position = new Vector3(3f, 2f, -4f);
            var attachments = new UnityPresentationAttachmentResolver();
            attachments.Register(41, actor.transform);
            var backend = new PresentationUnityBackend(root.transform, attachments);
            var request = new PresentationRequest(
                logicalKey: "jhin:test:q",
                assetId: 90013001,
                position: PresentationVector3.Zero,
                attachment: new PresentationAttachment(
                    PresentationAttachmentKind.Actor,
                    actorId: 41,
                    offset: new PresentationVector3(0f, 1.25f, 0.5f)),
                durationMs: 100,
                scale: 0.75f,
                resourceKey: JhinQVfxResource,
                assetKind: PresentationAssetKind.Particle);

            try
            {
                var playTask = backend.PlayAsync(request);
                yield return new WaitUntil(() => playTask.IsCompleted);
                var handle = playTask.GetAwaiter().GetResult();
                Assert.IsNotNull(handle, "A valid Jhin VFX request should create a Unity handle.");
                Assert.IsTrue(backend.IsAlive(handle.BackendHandle), "The created VFX handle should be alive.");
                Assert.AreEqual(1, actor.transform.childCount,
                    "Attached VFX must be parented below the resolved actor target.");

                var visual = actor.transform.GetChild(0);
                Assert.That(visual.localPosition, Is.EqualTo(new Vector3(0f, 1.25f, 0.5f)));
                Assert.That(visual.localScale, Is.EqualTo(prefab.transform.localScale * 0.75f));
                Assert.That(visual.GetComponentsInChildren<ParticleSystem>(true), Is.Not.Empty);
                backend.Tick(0.05f);
                Assert.IsTrue(backend.IsAlive(handle.BackendHandle));
                backend.Tick(0.06f);
                Assert.IsFalse(backend.IsAlive(handle.BackendHandle),
                    "A duration-bound VFX must be released after its lifetime elapses.");
                Assert.AreEqual(0, actor.transform.childCount);
            }
            finally
            {
                backend.ClearAsync().GetAwaiter().GetResult();
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator PresentationUnityBackend_InstantiatesJhinEHitVfxAtTargetAndReleasesIt()
        {
            var prefab = Resources.Load<GameObject>(JhinEHitVfxResource);
            Assert.IsNotNull(prefab, "Jhin E hit VFX prefab must be available through Resources.");

            var root = new GameObject("jhin-e-hit-presentation-root");
            var target = new GameObject("jhin-e-hit-target");
            target.transform.position = new Vector3(-2f, 0.5f, 6f);
            var attachments = new UnityPresentationAttachmentResolver();
            attachments.Register(52, target.transform);
            var backend = new PresentationUnityBackend(root.transform, attachments);
            var request = new PresentationRequest(
                logicalKey: "jhin:test:e-hit",
                assetId: 90013018,
                position: PresentationVector3.Zero,
                attachment: new PresentationAttachment(
                    PresentationAttachmentKind.Actor,
                    actorId: 52,
                    offset: PresentationVector3.Zero),
                durationMs: 100,
                scale: 1f,
                resourceKey: JhinEHitVfxResource,
                assetKind: PresentationAssetKind.Particle);

            try
            {
                var playTask = backend.PlayAsync(request);
                yield return new WaitUntil(() => playTask.IsCompleted);
                var handle = playTask.GetAwaiter().GetResult();
                Assert.IsNotNull(handle, "A valid Jhin E hit VFX request should create a Unity handle.");
                Assert.IsTrue(backend.IsAlive(handle.BackendHandle));
                Assert.AreEqual(1, target.transform.childCount,
                    "E hit VFX must attach to the enemy target that entered the trap.");
                Assert.IsNotEmpty(target.GetComponentsInChildren<ParticleSystem>(true),
                    "E hit VFX must instantiate the authored particle system.");

                backend.Tick(0.11f);
                Assert.IsFalse(backend.IsAlive(handle.BackendHandle),
                    "E hit VFX must release after its transient duration.");
                Assert.AreEqual(0, target.transform.childCount);
            }
            finally
            {
                backend.ClearAsync().GetAwaiter().GetResult();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator PresentationUnityBackend_AppliesWorldOffsetAndCanIgnoreParentScale()
        {
            var prefab = Resources.Load<GameObject>(JhinQVfxResource);
            Assert.IsNotNull(prefab, "Jhin Q VFX prefab must be available through Resources.");

            var root = new GameObject("jhin-presentation-root-policy");
            var actor = new GameObject("jhin-presentation-actor-policy");
            actor.transform.position = new Vector3(3f, 2f, -4f);
            actor.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            actor.transform.localScale = new Vector3(2f, 2f, 2f);
            var attachments = new UnityPresentationAttachmentResolver();
            attachments.Register(43, actor.transform);
            var backend = new PresentationUnityBackend(root.transform, attachments);
            var offset = new Vector3(0f, 1.25f, 0.5f);
            var request = new PresentationRequest(
                logicalKey: "jhin:test:q:world-offset-ignore-parent-scale",
                assetId: 90013001,
                position: PresentationVector3.Zero,
                attachment: new PresentationAttachment(
                    PresentationAttachmentKind.Actor,
                    actorId: 43,
                    offset: new PresentationVector3(offset.x, offset.y, offset.z),
                    offsetSpace: PresentationOffsetSpace.World),
                durationMs: 3000,
                resourceKey: JhinQVfxResource,
                assetKind: PresentationAssetKind.Particle,
                parentScaleMode: PresentationParentScaleMode.Ignore);

            try
            {
                var playTask = backend.PlayAsync(request);
                yield return new WaitUntil(() => playTask.IsCompleted);
                var handle = playTask.GetAwaiter().GetResult();
                Assert.IsNotNull(handle);
                var visual = actor.transform.GetChild(0);
                Assert.That(visual.position, Is.EqualTo(actor.transform.position + offset),
                    "World offsets must not rotate or scale with the attachment target.");
                Assert.That(visual.lossyScale.x, Is.EqualTo(prefab.transform.localScale.x).Within(0.001f));
                Assert.That(visual.lossyScale.y, Is.EqualTo(prefab.transform.localScale.y).Within(0.001f));
                Assert.That(visual.lossyScale.z, Is.EqualTo(prefab.transform.localScale.z).Within(0.001f));

                actor.transform.position += new Vector3(-2f, 0.5f, 3f);
                actor.transform.rotation = Quaternion.Euler(0f, -35f, 0f);
                backend.Tick(0.016f);
                var expectedWorldPosition = actor.transform.position + offset;
                Assert.That(visual.position.x, Is.EqualTo(expectedWorldPosition.x).Within(0.0001f),
                    "A followed world offset must stay aligned to world axes as the target moves and rotates.");
                Assert.That(visual.position.y, Is.EqualTo(expectedWorldPosition.y).Within(0.0001f));
                Assert.That(visual.position.z, Is.EqualTo(expectedWorldPosition.z).Within(0.0001f));
            }
            finally
            {
                backend.ClearAsync().GetAwaiter().GetResult();
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator PresentationUnityBackend_DelegatesAbstractOrderingToUnityAdapter()
        {
            var prefab = Resources.Load<GameObject>(JhinQVfxResource);
            Assert.IsNotNull(prefab, "Jhin Q VFX prefab must be available through Resources.");

            var root = new GameObject("jhin-presentation-root-ordering");
            var actor = new GameObject("jhin-presentation-actor-ordering");
            var targetRenderer = actor.AddComponent<ParticleSystem>().GetComponent<ParticleSystemRenderer>();
            targetRenderer.sortingOrder = 17;
            var attachments = new UnityPresentationAttachmentResolver();
            attachments.Register(44, actor.transform);
            var backend = new PresentationUnityBackend(root.transform, attachments);
            var request = new PresentationRequest(
                logicalKey: "jhin:test:q:relative-ordering",
                assetId: 90013001,
                position: PresentationVector3.Zero,
                attachment: new PresentationAttachment(PresentationAttachmentKind.Actor, actorId: 44),
                durationMs: 3000,
                resourceKey: JhinQVfxResource,
                assetKind: PresentationAssetKind.Particle,
                ordering: new PresentationOrdering(PresentationOrderingMode.RelativeToAttachment, 5));

            try
            {
                var playTask = backend.PlayAsync(request);
                yield return new WaitUntil(() => playTask.IsCompleted);
                Assert.IsNotNull(playTask.GetAwaiter().GetResult());
                var visual = actor.transform.GetChild(0).gameObject;
                var renderer = visual.GetComponentInChildren<Renderer>(true);
                Assert.IsNotNull(renderer);
                Assert.AreEqual(22, renderer.sortingOrder,
                    "The Unity adapter should realize relative priority while preserving the asset sorting layer.");
            }
            finally
            {
                backend.ClearAsync().GetAwaiter().GetResult();
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator PresentationUnityBackend_DoesNotKeepParentWhenAttachmentFollowIsDisabled()
        {
            var prefab = Resources.Load<GameObject>(JhinQVfxResource);
            Assert.IsNotNull(prefab, "Jhin Q VFX prefab must be available through Resources.");

            var root = new GameObject("jhin-presentation-root-once");
            var actor = new GameObject("jhin-presentation-actor-once");
            actor.transform.position = new Vector3(3f, 2f, -4f);
            var attachments = new UnityPresentationAttachmentResolver();
            attachments.Register(42, actor.transform);
            var backend = new PresentationUnityBackend(root.transform, attachments);
            var request = new PresentationRequest(
                logicalKey: "jhin:test:q:one-shot",
                assetId: 90013001,
                position: PresentationVector3.Zero,
                attachment: new PresentationAttachment(
                    PresentationAttachmentKind.Actor,
                    actorId: 42,
                    offset: new PresentationVector3(0f, 1.25f, 0.5f)),
                durationMs: 3000,
                resourceKey: JhinQVfxResource,
                assetKind: PresentationAssetKind.Particle,
                followAttachment: false);

            try
            {
                var playTask = backend.PlayAsync(request);
                yield return new WaitUntil(() => playTask.IsCompleted);
                var handle = playTask.GetAwaiter().GetResult();
                Assert.IsNotNull(handle);
                Assert.AreEqual(0, actor.transform.childCount,
                    "One-shot attachment placement must not retain the source as a parent.");
                Assert.AreEqual(1, root.transform.childCount,
                    "The one-shot VFX should be owned by the presentation root.");

                var visual = root.transform.GetChild(0);
                var initialPosition = visual.position;
                actor.transform.position += new Vector3(5f, 0f, 0f);
                backend.SetScale(new PresentationScaleSource(
                    "test:vfx-scale",
                    PresentationScaleChannel.Vfx,
                    new PresentationVector3(1.5f, 1.5f, 1.5f)));
                Assert.That(visual.position, Is.EqualTo(initialPosition),
                    "Refreshing a scale source must not resolve the attachment and move a one-shot VFX.");
            }
            finally
            {
                backend.ClearAsync().GetAwaiter().GetResult();
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(root);
            }
        }

        private static bool HasTrigger(Animator animator, string parameterName)
        {
            var parameters = animator.parameters;
            for (var i = 0; i < parameters.Length; i++)
                if (parameters[i].type == AnimatorControllerParameterType.Trigger && parameters[i].name == parameterName)
                    return true;
            return false;
        }

        private static Animator FindAnimatorWithTrigger(GameObject root, string parameterName)
        {
            if (root == null) return null;
            var animators = root.GetComponentsInChildren<Animator>(true);
            for (var i = 0; i < animators.Length; i++)
                if (animators[i].runtimeAnimatorController != null && HasTrigger(animators[i], parameterName))
                    return animators[i];
            return null;
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var result = FindChild(root.GetChild(i), name);
                if (result != null) return result;
            }
            return null;
        }
    }
}

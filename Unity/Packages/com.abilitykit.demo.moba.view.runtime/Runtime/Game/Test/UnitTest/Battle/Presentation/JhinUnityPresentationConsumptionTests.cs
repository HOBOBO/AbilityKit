using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Ability.FrameSync.Rollback;
using AbilityKit.Ability.Host;
using AbilityKit.Ability.Host.Extensions.FrameSync;
using AbilityKit.Ability.Host.Framework;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Attributes;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Demo.Moba.View.Config;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Game.Platform.Unity.Presentation;
using AbilityKit.Network.Runtime;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.World.ECS;
using AbilityKit.Game.Test.UnitTest;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

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

        [TestCase("life/alive/action/attack.slot.1", 30010601, 10, "AutoAttack_1")]
        [TestCase("life/alive/action/attack.slot.2", 30010602, 10, "AutoAttack_2")]
        [TestCase("life/alive/action/attack.slot.3", 30010603, 10, "AutoAttack_3")]
        [TestCase("life/alive/action/attack.slot.4", 30010604, 10, "AutoAttack_4")]
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
                HeroSkillHeadlessContract.SpawnEnemyHero(harness, "bounce_2", 9702, x: -2f, z: 3f);
                HeroSkillHeadlessContract.SpawnEnemyHero(harness, "bounce_3", 9703, x: -2f, z: 7f);
                HeroSkillHeadlessContract.SpawnEnemyHero(harness, "bounce_4", 9704, x: -2f, z: 10f);
                var cast = harness.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(
                    actorId, 1, aimPos: default,
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
                var hits = new HashSet<int>();
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
                                    animator.Update(1f / 24f);
                                    Assert.AreEqual(Animator.StringToHash("Shot"),
                                        animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                                        "Projectile spawn snapshot must enter the authored Q shot state.");
                                    animator.Update(1f / 24f);
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
                                    Assert.Greater(entry.SnapshotFrame, 0, "Hit snapshot must carry its original logic frame.");
                                    Assert.IsTrue(hits.Add(entry.HitCollider), "The bounce should hit a different target.");
                                    Assert.AreEqual(hits.Count + 1, vfxRoot.ChildCount,
                                        "Each contact needs one additional hit VFX while the projectile is alive.");
                                    handler.HandleSnapshot(new[] { data });
                                    Assert.AreEqual(hits.Count + 1, vfxRoot.ChildCount,
                                        "Replaying the same snapshot must not create another hit VFX.");
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
                    Assert.AreEqual(4, hits.Count, "The locked target and all three bounces should produce hit VFX.");
                    Assert.IsTrue(sawExit, "Q projectile should emit an exit snapshot after its final available bounce.");
                    Assert.AreEqual(4, vfxRoot.ChildCount, "Exit releases flight VFX only; impact VFX expire independently.");
                    for (var index = 0; index < vfxRoot.ChildCount; index++)
                    {
                        var impact = vfxRoot.GetChild(index);
                        Assert.IsTrue(impact.TryGetRef(out BattleVfxComponent vfx));
                        Assert.AreEqual(90013016, vfx.VfxId);
                        Assert.IsTrue(impact.TryGetRef(out BattlePresentationLifetimeComponent presentation));
                        Assert.AreEqual(700, presentation.Request.DurationMs);
                        Assert.IsTrue(impact.TryGetRef(out BattleVfxLifetimeComponent lifetime));
                        lifetime.ExpireAtTime = Time.time - 1f;
                    }
                    manager.Tick(vfxRoot);
                    Assert.AreEqual(0, vfxRoot.ChildCount,
                        "The lifetime consumer must release expired impact VFX.");
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
            VerifyRealCastPresentation(30010201, 2, "Spell_W", "Jhin_Battlecast_W_Cast");

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
                var hitVfxEntityId = default(IEntityId);
                var hitAndExit = new List<ProjectileEventData>();
                PresentationCueData? impactCue = null;
                var sawHit = false;
                var sawExit = false;

                try
                {
                    for (var tick = 0; tick < 180 && (!sawExit || !impactCue.HasValue); tick++)
                    {
                        harness.Tick(1);
                        var snapshots = new List<WorldStateSnapshot>();
                        harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>()
                            .CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
                        for (var snapshotIndex = 0; snapshotIndex < snapshots.Count; snapshotIndex++)
                        {
                            if (snapshots[snapshotIndex].OpCode == MobaOpCodes.Snapshot.PresentationCue)
                            {
                                foreach (var entry in MobaPresentationCueSnapshotCodec.Deserialize(snapshots[snapshotIndex].Payload))
                                {
                                    if (entry.TemplateId != 32010201 || entry.VfxId != 90013017) continue;
                                    Assert.IsFalse(impactCue.HasValue, "W should emit a single impact cue.");
                                    impactCue = PresentationCueSnapshotMapper.Map(in entry);
                                }
                                continue;
                            }
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
                                    Assert.AreEqual(90013020, vfxComponent.VfxId,
                                        "W's projectile template must resolve its configured Battlecast projectile VFX.");
                                    Assert.IsTrue(vfxEntity.TryGetRef(out BattleViewGameObjectComponent viewComponent));
                                    Assert.IsNotNull(viewComponent.GameObject);
                                    Assert.IsNotNull(viewComponent.GameObject.GetComponent<BattleVfxPoolableTag>());
                                    Assert.IsNotNull(viewComponent.GameObject.GetComponentInChildren<ParticleSystem>(true),
                                        "The W snapshot path must instantiate the authored particle prefab.");
                                    var missile = FindChild(viewComponent.GameObject.transform, "W_Missile");
                                    Assert.IsNotNull(missile);
                                    Assert.IsTrue(missile.gameObject.activeInHierarchy,
                                        "W must be visible on Spawn, including before an Animator could be culled.");
                                    Assert.IsNull(viewComponent.GameObject.GetComponentInChildren<Animator>(true));
                                    Assert.AreEqual(0f, missile.localPosition.z, 0.001f,
                                        "The projectile must not carry the showcase muzzle's forward offset.");
                                    var visibleParticles = 0;
                                    foreach (var particle in missile.GetComponentsInChildren<ParticleSystem>())
                                    {
                                        particle.Simulate(0.1f, false, true, true);
                                        visibleParticles += particle.particleCount;
                                    }
                                    Assert.Greater(visibleParticles, 0, "An Animator state alone does not prove a visible W.");
                                    Assert.IsTrue(vfxEntity.TryGetRef(out BattleViewFollowComponent follow));
                                    Assert.AreEqual(projectileActorId, follow.TargetActorId,
                                        "W projectile VFX must follow the authoritative projectile actor.");
                                }
                                else if (entry.Kind == (int)ProjectileEventKind.Hit)
                                {
                                    sawHit = true;
                                    Assert.GreaterOrEqual(vfxRoot.ChildCount, 2,
                                        "A W hit must retain the moving projectile VFX and add its configured hit VFX.");
                                    hitAndExit.Add(data);
                                    for (var childIndex = 0; childIndex < vfxRoot.ChildCount; childIndex++)
                                    {
                                        var child = vfxRoot.GetChild(childIndex);
                                        if (child.TryGetRef(out BattleVfxComponent hitVfx) && hitVfx.VfxId == 90013017)
                                        {
                                            hitVfxEntityId = child.Id;
                                            Assert.IsTrue(child.TryGetRef(out BattleViewGameObjectComponent hitView));
                                            Assert.IsNotNull(hitView.GameObject.GetComponent<BattleVfxPoolableTag>());
                                            Assert.Less(Vector3.Distance(hitView.GameObject.transform.position,
                                                new Vector3(entry.X, entry.Y, entry.Z)), 0.001f);
                                            break;
                                        }
                                    }
                                    Assert.IsTrue(vfxWorld.IsAlive(hitVfxEntityId),
                                        "W hit snapshot must instantiate the configured Battlecast hit VFX.");
                                }
                                else if (entry.Kind == (int)ProjectileEventKind.Exit)
                                {
                                    hitAndExit.Add(data);
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
                    Assert.IsTrue(vfxWorld.IsAlive(hitVfxEntityId), "Exit must not stop the detached hit VFX.");
                    handler.HandleSnapshot(hitAndExit.ToArray());
                    Assert.AreEqual(1, vfxRoot.ChildCount, "A replayed Hit + Exit batch must not spawn another hit.");
                    Assert.AreEqual(hitVfxEntityId, vfxRoot.GetChild(0).Id);

                    Assert.IsTrue(impactCue.HasValue, "W must emit its configured impact cue.");
                    var hit = hitAndExit.Find(entry => entry.Kind == ProjectilePresentationEventKind.Hit);
                    Assert.Greater(hit.SnapshotFrame, 0);
                    Assert.AreEqual(hit.SnapshotFrame, impactCue.Value.SourceFrame,
                        "Both impact snapshots must retain the collision frame.");
                    Assert.AreEqual((long)hit.ProjectileId, impactCue.Value.InstanceId);
                    var cues = new[] { impactCue.Value };
                    var cuePacketFrame = hit.SnapshotFrame + 2;

                    var cueHandler = new BattlePresentationCueViewEventHandler(vfxWorld, query, manager, in vfxRoot);
                    cueHandler.HandleSnapshot(cues, handler, cuePacketFrame);
                    Assert.AreEqual(1, vfxRoot.ChildCount, "A delayed cue must pair with the impact already shown.");
                    Assert.AreEqual(hitVfxEntityId, vfxRoot.GetChild(0).Id);

                    handler.Clear();
                    manager.Clear(vfxRoot);
                    var cueFirstHandler = new BattlePresentationCueViewEventHandler(vfxWorld, query, manager, in vfxRoot);
                    cueFirstHandler.HandleSnapshot(cues, handler, cuePacketFrame);
                    Assert.AreEqual(1, vfxRoot.ChildCount, "The cue must render when it arrives before the hit snapshot.");
                    var cueVfxEntityId = vfxRoot.GetChild(0).Id;
                    handler.HandleSnapshot(hitAndExit.ToArray(), cuePacketFrame + 1);
                    Assert.AreEqual(1, vfxRoot.ChildCount, "The later hit must pair with the previously rendered cue.");
                    Assert.AreEqual(cueVfxEntityId, vfxRoot.GetChild(0).Id);
                }
                finally
                {
                    manager.Clear(vfxRoot);
                    if (vfxRoot.IsValid) vfxRoot.Destroy();
                }
            }

            yield return null;
        }

        /// <summary>
        /// 在真实预测宿主内跨过 W 命中回滚，另用下一次 W 检查新 Cue 没有被过滤。
        /// </summary>
        [Test]
        public void W_PredictionRollbackDoesNotReplayImpactOrDropNextCue()
        {
            const int projectileTemplateId = 32010201;
            const int impactVfxId = 90013017;
            using var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                new[] { 30010201 }, worldId: "jhin_w_rollback_vfx", heroId: 3001, attributeTemplateId: 3001);
            harness.EnterGameAndWarmup();
            var casterId = harness.AssertPlayerActorBound();
            var targetId = HeroSkillHeadlessContract.SpawnEnemyHero(harness, "jhin_w_rollback_target", 9702, x: 0f, z: 8f);
            var world = harness.World;
            var actors = world.Services.Resolve<MobaActorRegistry>();
            var gate = world.Services.Resolve<MobaLogicWorldRunGateService>();
            var calculator = new MobaAuthoritativeStateHashCalculator();
            var provider = world.Services.Resolve<IWorldStateSnapshotBatchProvider>();
            var frameLimit = harness.FrameTime.Frame.Value;
            var mismatchFrame = -1;
            var sequence = 0;
            var impacts = new List<PresentationCueData>();
            var hits = new List<ProjectileEventData>();
            var originalImpactPackets = new List<FramePacket>();
            var aim = new Vec3(0f, 0f, 8f);
            var options = new HostRuntimeOptions();
            var host = new HostRuntime(harness.Worlds, options);
            using var remote = new FrameJitterBuffer<PlayerInputCommand[]>(0);
            uint BattleHash() => calculator.Compute(gate.InGame, actors);
            var prediction = new ClientPredictionDriverModule(_ => remote, _ => null,
                resolveIdealFrameLimit: _ => frameLimit, minPredictionWindow: 4, enableRollback: true,
                buildRollbackRegistry: MobaRollbackRegistryBuilder.CreateForPrediction,
                buildComputeHash: _ => frame => new WorldStateHash(BattleHash() ^ (frame.Value == mismatchFrame ? 1u : 0u)));

            var viewWorld = new EntityWorld();
            var vfxRoot = viewWorld.Create("jhin-w-rewind-vfx");
            var query = new BattleEntityQuery(viewWorld, new BattleEntityLookup());
            var vfxDb = BattleViewFactory.GetOrLoadVfxDb();
            Assert.IsNotNull(vfxDb);
            var vfx = new BattleVfxManager(vfxDb);
            var context = BattleContext.Rent();
            context.EntityWorld = viewWorld;
            context.PredictionRuntime.Bind(prediction, null, null, null);
            var sink = new BattleViewEventSink(context, query, null, vfx, vfxRoot, null, null,
                new BattleViewResourceProvider(harness.Config, vfxDb));
            var projectileRouteEnabled = true;
            prediction.Install(host, options);
            try
            {
                options.WorldCreated.Invoke(world);
                Assert.IsTrue(prediction.TryRebase(world.Id, harness.FrameTime.Frame));
                Push(SkillInputPhase.Press);
                Push(SkillInputPhase.Release);
                for (var tick = 0; tick < 120 && hits.Count == 0; tick++) Push(injectMismatch: true);
                Assert.That(hits, Has.Count.EqualTo(1), "The first W must hit in the prediction host.");
                Assert.That(impacts, Has.Count.EqualTo(1), "The first W must emit both impact notifications.");
                var original = impacts[0];
                var hitFrame = hits[0].SnapshotFrame;
                Assert.AreEqual(frameLimit, hitFrame);
                Assert.AreEqual(hitFrame, original.SourceFrame);
                Assert.That(ImpactViews(), Has.Length.EqualTo(1));
                var originalView = ImpactViews()[0];
                var expectedHash = BattleHash();
                Assert.IsTrue(actors.TryGet(targetId, out var target));
                var hpAfterHit = new MobaAttrs(target).Hp;
                Assert.That(originalImpactPackets, Has.Count.EqualTo(2));

                mismatchFrame = -1;
                prediction.OnAuthoritativeStateHash(world.Id, new FrameIndex(hitFrame), new WorldStateHash(expectedHash));
                Assert.That(prediction.TotalRollbackRestoreFailed, Is.Zero, $"Restore before W hit at frame {hitFrame}.");
                Assert.That(prediction.TotalRollbackCount, Is.EqualTo(1));
                var restoredFrame = prediction.LastRollbackFrame.Value;
                Assert.That(restoredFrame, Is.EqualTo(hitFrame - 1));
                Assert.IsTrue(prediction.IsReplaying);
                Assert.That(new MobaAttrs(target).Hp, Is.GreaterThan(hpAfterHit), "Rewind must undo the first W damage.");
                sink.Tick();
                Advance(hitFrame);
                Assert.AreEqual(expectedHash, BattleHash(), "Replaying W must reach the same authority hash.");
                Assert.AreEqual(hpAfterHit, new MobaAttrs(target).Hp);
                host.Tick(harness.FixedDelta);
                Assert.IsFalse(prediction.IsReplaying);
                Push();
                var viewsAfterReplay = ImpactViews();
                TestContext.WriteLine($"W: restored={restoredFrame}; hit={hitFrame}; " +
                    $"hits after replay={hits.Count}; cues after replay={impacts.Count}; impact views={viewsAfterReplay.Length}");

                foreach (var packet in originalImpactPackets)
                    Consume(packet, record: false);
                var viewsAfterRedelivery = ImpactViews();
                TestContext.WriteLine($"W: impact views after delayed original packets={viewsAfterRedelivery.Length}");

                projectileRouteEnabled = false;
                var sourceCuesAfterReplay = impacts.Count;
                for (var tick = 0; tick < 600; tick++) Push();
                Push(SkillInputPhase.Press);
                Push(SkillInputPhase.Release);
                for (var tick = 0; tick < 120 && impacts.Count == sourceCuesAfterReplay; tick++) Push();
                Assert.That(impacts, Has.Count.EqualTo(sourceCuesAfterReplay + 1), "A later real W must emit a fresh cue.");
                Assert.AreNotEqual(original.InstanceId, impacts.Last().InstanceId);
                TestContext.WriteLine($"Next W: cue generation={impacts.Last().Generation}; instance={impacts.Last().InstanceId}; " +
                    $"impact views={ImpactViews().Length}");
                Assert.That(new[] { viewsAfterReplay.SequenceEqual(new[] { originalView }),
                    viewsAfterRedelivery.SequenceEqual(new[] { originalView }), ImpactViews().Except(viewsAfterRedelivery).Count() == 1 },
                    Is.EqualTo(new[] { true, true, true }), "Expected [no duplicate replay impact, no duplicate redelivery, next Cue played].");
            }
            finally
            {
                prediction.Uninstall(host, options);
                sink.Clear();
                vfx.Clear(vfxRoot);
                BattleContext.Return(context);
            }

            void Push(SkillInputPhase? phase = null, bool injectMismatch = false)
            {
                var frame = new FrameIndex(++frameLimit);
                if (injectMismatch) mismatchFrame = frame.Value;
                var commands = Array.Empty<PlayerInputCommand>();
                if (phase.HasValue)
                {
                    var input = new SkillInputEvent(2, phase.Value, aimPos: in aim, targetActorId: targetId,
                        predictionKey: ++sequence, targetFrame: frame.Value, inputSequence: sequence,
                        entityVersion: actors.GetEntityVersion(casterId));
                    commands = new[] { new PlayerInputCommand(frame, harness.PlayerId, MobaOpCodes.Input.SkillInput, SkillInputCodec.Serialize(in input)) };
                }
                remote.Add(frame.Value, commands);
                Advance(frame.Value);
            }

            void Advance(int frame)
            {
                host.Tick(harness.FixedDelta);
                Assert.IsTrue(prediction.TryGetFrames(world.Id, out var confirmed, out var predicted));
                Assert.AreEqual(frame, predicted.Value);
                Assert.AreEqual(frame, confirmed.Value);
                sink.Tick();
                var snapshots = new List<WorldStateSnapshot>();
                provider.CollectSnapshots(new FrameIndex(frame), snapshots, 32);
                foreach (var snapshot in snapshots)
                    Consume(new FramePacket(world.Id, new FrameIndex(frame), null, snapshot), record: true);
            }

            void Consume(FramePacket packet, bool record)
            {
                var snapshot = packet.Snapshot.Value;
                if (snapshot.OpCode == MobaOpCodes.Snapshot.ProjectileEvent)
                {
                    var data = MobaProjectileEventSnapshotCodec.Deserialize(snapshot.Payload)
                        .Where(entry => entry.TemplateId == projectileTemplateId)
                        .Select(entry => ProjectileEventSnapshotMapper.Map(in entry)).ToArray();
                    if (record)
                    {
                        if (hits.Count == 0 && data.Any(entry => entry.Kind == ProjectilePresentationEventKind.Hit))
                            originalImpactPackets.Add(packet);
                        hits.AddRange(data.Where(entry => entry.Kind == ProjectilePresentationEventKind.Hit));
                    }
                    if (projectileRouteEnabled) sink.OnProjectileEventSnapshot(packet, data);
                }
                else if (snapshot.OpCode == MobaOpCodes.Snapshot.PresentationCue)
                {
                    var data = MobaPresentationCueSnapshotCodec.Deserialize(snapshot.Payload)
                        .Where(entry => entry.TemplateId == projectileTemplateId && entry.VfxId == impactVfxId)
                        .Select(entry => PresentationCueSnapshotMapper.Map(in entry)).ToArray();
                    if (record)
                    {
                        if (impacts.Count == 0 && data.Length > 0) originalImpactPackets.Add(packet);
                        impacts.AddRange(data);
                    }
                    sink.OnPresentationCueSnapshot(packet, data);
                }
            }

            IEntityId[] ImpactViews() => Enumerable.Range(0, vfxRoot.ChildCount).Select(vfxRoot.GetChild)
                .Where(child => child.TryGetRef(out BattleVfxComponent component) && component.VfxId == impactVfxId)
                .Select(child => child.Id).ToArray();
        }

        [UnityTest]
        public IEnumerator E_RealCastSnapshotPlaysModelWithoutDuplicateTrap() =>
            VerifyRealCastPresentation(30010301, 3, "Spell_E", null);

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
                                    Assert.IsNull(viewComponent.GameObject.GetComponentInChildren<Animator>(true),
                                        "Flight must not run the landing deployment animation.");
                                    Assert.IsNull(FindChild(viewComponent.GameObject.transform, "Trap_1"),
                                        "Flight must not show a second trap at a showcase landing offset.");
                                    var missile = FindChild(viewComponent.GameObject.transform, "Trap_Missile");
                                    Assert.IsNotNull(missile);
                                    Assert.IsTrue(missile.gameObject.activeInHierarchy);
                                    Assert.AreEqual(0f, missile.localPosition.z, 0.001f);
                                    Assert.AreEqual(5, missile.GetComponentsInChildren<MeshRenderer>().Length);
                                    handler.Clear();
                                    manager.Clear(vfxRoot);
                                    Assert.AreEqual(0, vfxRoot.ChildCount,
                                        "Resetting the projectile presentation handler should release its current VFX before replay.");
                                    handler.HandleSnapshot(new[] { ProjectileEventSnapshotMapper.Map(in entry) });
                                    vfxEntity = vfxRoot.GetChild(0);
                                    projectileVfxEntityId = vfxEntity.Id;
                                    Assert.IsTrue(vfxEntity.TryGetRef(out BattleViewGameObjectComponent replayedView));
                                    missile = FindChild(replayedView.GameObject.transform, "Trap_Missile");
                                    Assert.IsNotNull(missile);
                                    Assert.IsTrue(missile.gameObject.activeInHierarchy,
                                        "Replayed flight must be immediately visible without deployment timing.");
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
                harness.TickUntilSkillStops(actorId, 5, maxTicks: BasicAttackTimeoutTicks(harness, 30010601),
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
                    Assert.AreEqual(Animator.StringToHash("Idle"),
                        animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                        "The next magazine slot must stay idle until another basic attack is cast.");
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
                harness.TickUntilSkillStops(actorId, 5, maxTicks: BasicAttackTimeoutTicks(harness, 30010601),
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
                        Assert.AreEqual(Animator.StringToHash("Idle"),
                            instance.GetComponentInChildren<Animator>(true)
                                .GetCurrentAnimatorStateInfo(0).shortNameHash,
                            $"Skill {skill.id} must leave the next magazine slot visually idle.");
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
                    var animator = instance.GetComponentInChildren<Animator>(true);

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
                        Assert.AreEqual(Animator.StringToHash("AutoAttack_" + (shotIndex + 1)),
                            animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
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

                        if (shotIndex == expectedSkills.Length - 1)
                            Assert.IsFalse(skills.CheckCombatRules(actorId, expectedSkills[shotIndex]).Passed,
                                "The successful fourth launch must lock attacks before reload starts.");

                        harness.TickUntilSkillStops(actorId, 5, maxTicks: BasicAttackTimeoutTicks(harness, expectedSkills[shotIndex]),
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
                            Assert.AreEqual(Animator.StringToHash("Idle"),
                                animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                                $"After shot {shotIndex + 1}, the next magazine slot is ready but has no cast.");
                            Assert.IsTrue(currentVfx == null,
                                "Entering the next attack slot must stop the old timeline VFX.");
                        }
                    }

                    CharacterActionData reload = default;
                    for (var tick = 0; tick < 30; tick++)
                    {
                        harness.Tick(1);
                        reload = CaptureAction(harness, actorId);
                        if (reload.SkillId == 30010605 && reload.CastInstanceId > 0) break;
                    }
                    StringAssert.EndsWith("action/attack.slot.5", reload.Path,
                        "The fourth shot must enter the authored reload HFSM state.");
                    Assert.AreEqual(30010605, reload.SkillId);
                    Assert.Greater(reload.CastInstanceId, 0);
                    Assert.AreEqual(2500, reload.CastDurationMs);
                    Assert.IsFalse(skills.TryCastBySlot(actorId, 5).Success,
                        "The authoritative slot must reject an attack at reload start.");
                    component.ApplyPresentation(reload);
                    view.SeekToFrame(reload.SnapshotFrame, harness.FixedDelta);
                    yield return null;
                    Assert.AreEqual(Animator.StringToHash("AutoAttack_Reload"),
                        animator.GetCurrentAnimatorStateInfo(0).shortNameHash);

                    var qTarget = HeroSkillHeadlessContract.SpawnEnemyHero(harness, "jhin_reload_q_target", 9711, x: 0f, z: 4f);
                    var qCast = skills.TryCastBySlot(actorId, 1, default, Vec3.Forward, qTarget);
                    Assert.IsTrue(qCast.Success, qCast.FailReason);
                    harness.Tick(1);
                    var qAction = CaptureAction(harness, actorId);
                    Assert.AreEqual(30010101, qAction.SkillId);
                    component.ApplyPresentation(qAction);
                    view.Bind(component, harness.TickRate);
                    Assert.IsFalse(skills.TryCastBySlot(actorId, 5).Success);
                    harness.TickUntilSkillStops(actorId, 1, 35);
                    harness.Tick(1);
                    var resumed = CaptureAction(harness, actorId);
                    Assert.AreEqual(reload.CastInstanceId, resumed.CastInstanceId);
                    Assert.AreEqual(reload.CastStartFrame, resumed.CastStartFrame);
                    component.ApplyPresentation(resumed);
                    view.Bind(component, harness.TickRate);
                    var resumedState = animator.GetCurrentAnimatorStateInfo(0);
                    Assert.AreEqual(Animator.StringToHash("AutoAttack_Reload"), resumedState.shortNameHash);
                    Assert.AreEqual((resumed.SnapshotFrame - reload.CastStartFrame) * harness.FixedDelta / 2.5f,
                        resumedState.normalizedTime, 0.03f);

                    var parameters = harness.World.Services.Resolve<MobaCharacterHfsmParameterService>();
                    CharacterActionData afterReload = reload;
                    for (var tick = 0; tick < 80; tick++)
                    {
                        Assert.IsFalse(skills.TryCastBySlot(actorId, 5).Success,
                            "Attacks must remain blocked until the last reload frame finishes.");
                        harness.Tick(1);
                        afterReload = CaptureAction(harness, actorId);
                        if (!afterReload.Path.EndsWith("action/attack.slot.5",
                                System.StringComparison.Ordinal)) break;
                        Assert.AreEqual(reload.CastInstanceId, afterReload.CastInstanceId);
                    }
                    Assert.IsFalse(afterReload.Path.EndsWith("action/attack.slot.5",
                            System.StringComparison.Ordinal),
                        "The auxiliary Timeline must finish before leaving the reload state.");
                    Assert.AreEqual(0f, parameters.Resolve(actorId,
                            MobaCharacterHfsmTestFixture.AttackSlot),
                        "Normal cast completion must clear the source-owned AttackSlot contribution.");
                    Assert.AreEqual(0, afterReload.CastInstanceId,
                        "The completed reload must no longer own the model action.");
                    component.ApplyPresentation(afterReload);
                    view.SeekToFrame(afterReload.SnapshotFrame, harness.FixedDelta);
                    Assert.AreEqual(Animator.StringToHash("Idle"),
                        animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
                    harness.AssertSlotSkill(5, 30010601);
                    Assert.IsTrue(skills.TryCastBySlot(actorId, 5, default, Vec3.Forward, 0).Success,
                        "Normal completion must unlock the next magazine's first attack.");
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

        private static int BasicAttackTimeoutTicks(MobaSkillConfigTestHarness harness, int skillId)
        {
            var flow = harness.Config.GetSkillFlow(skillId);
            Assert.IsNotNull(flow);
            var durationMs = 0;
            foreach (var phase in flow.Phases)
            {
                Assert.IsTrue(phase.Type == 18 || phase.Type == 2,
                    "Review the timeout if the basic attack gains a different phase topology.");
                if (phase.Type == 2) durationMs += phase.Timeline.DurationMs;
            }
            Assert.Greater(durationMs, 0);
            return Mathf.CeilToInt(durationMs / 1000f / harness.FixedDelta) + 8;
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
                    if (particleName != null)
                    {
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
                    }
                    else
                    {
                        yield return null;
                        Assert.IsNull(FindTimelineVfx(instance, "Jhin_Battlecast_E"));
                        Assert.IsNull(FindTimelineVfx(instance, "Jhin_Battlecast_E_Projectile"),
                            "E flight and landing belong to their authoritative actors, not the caster timeline.");
                    }

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
                    if (particleName != null)
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

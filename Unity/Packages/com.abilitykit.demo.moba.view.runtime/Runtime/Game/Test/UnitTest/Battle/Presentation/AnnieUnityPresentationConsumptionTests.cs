using System;
using System.Collections;
using System.Collections.Generic;
using AbilityKit.ActionSchema;
using AbilityKit.Ability.Host;
using AbilityKit.Combat.Projectile;
using AbilityKit.Core.Eventing;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Events.Summon;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateMachine;
using AbilityKit.Demo.Moba.Share;
using AbilityKit.Demo.Moba.Config.BattleDemo.MO;
using AbilityKit.Ability.Config;
using AbilityKit.Demo.Moba.View.Config;
using AbilityKit.Game.Battle.Component;
using AbilityKit.Game.Battle.Entity;
using AbilityKit.Game.Battle.Vfx;
using AbilityKit.Game.Flow;
using AbilityKit.Game.Flow.Battle.ViewEvents;
using AbilityKit.Game.Platform.Unity.Presentation;
using AbilityKit.Game.Flow.Battle.Presentation.Timeline;
using AbilityKit.Game.Test.UnitTest;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Triggering.Eventing;
using AbilityKit.World.ECS;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using EntityBattleNetId = AbilityKit.Game.Battle.Entity.BattleNetId;

namespace AbilityKit.Game.Tests
{
    /// <summary>
    /// End-to-end Unity consumption checks for Annie R and the resulting Tibbers spawn.
    /// Combat acceptance tests remain responsible for authoritative damage and summon rules.
    /// </summary>
    public sealed class AnnieUnityPresentationConsumptionTests
    {
        private const int AnnieHeroId = 3003;
        private const int AnnieBasicAttackSkillId = 30030101;
        private const int AnnieQSkillId = 30030201;
        private const int AnnieWSkillId = 30030301;
        private const int AnnieESkillId = 30030401;
        private const int AnnieRSkillId = 30030501;
        private const int AnnieStunBuffId = 30030211;
        private const int AnnieEBuffId = 30030402;
        private const int AnnieStunPresentationId = 90033003;
        private const int AnnieRPreviewVfxId = 90033005;
        private const int TibbersSummonId = 30030502;
        private const string AnnieWAreaVfxResource =
            "ChampionVfx/14.Void_Annie/Void_Annie_W_Area";
        private const string AnnieModelResource =
            "ChampionVfx/14.Void_Annie/Void_Annie_Model";
        private const string TibbersSpawnVfxResource =
            "ChampionVfx/14.Void_Annie/Void_Annie_Tibbers_Spawn";

        [Test]
        public void PublishedSkillAndBuffMappingsResolveToLinkedAnnieVfx()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                       new[] { AnnieQSkillId, AnnieWSkillId, AnnieESkillId, AnnieRSkillId },
                       worldId: "annie_unity_presentation_mapping",
                       heroId: AnnieHeroId,
                       attributeTemplateId: AnnieHeroId))
            {
                var expected = new[]
                {
                    (SkillId: AnnieQSkillId, VfxId: 90033001,
                        Resource: "ChampionVfx/14.Void_Annie/Void_Annie_Q_Projectile"),
                    (SkillId: AnnieWSkillId, VfxId: 90033002,
                        Resource: "ChampionVfx/14.Void_Annie/Void_Annie_W_Area"),
                    (SkillId: AnnieESkillId, VfxId: 90033004,
                        Resource: "ChampionVfx/14.Void_Annie/Void_Annie_E"),
                    (SkillId: AnnieRSkillId, VfxId: AnnieRPreviewVfxId,
                        Resource: "ChampionVfx/14.Void_Annie/Void_Annie_R_Summon"),
                };
                var templates = harness.Config.GetTable<PresentationTemplateMO>();
                var vfxDb = BattleViewFactory.GetOrLoadVfxDb();
                Assert.IsNotNull(vfxDb, "The published VFX database must load.");

                foreach (var entry in expected)
                {
                    Assert.IsTrue(harness.Config.TryGetSkill(entry.SkillId, out var skill),
                        "Annie skill config is missing: " + entry.SkillId);
                    Assert.AreEqual(entry.VfxId, skill.PreviewVfxId,
                        "Annie skill must reference its own published preview VFX.");
                    Assert.IsTrue(templates.TryGet(entry.VfxId, out var template),
                        "Annie presentation template is missing: " + entry.VfxId);
                    Assert.AreEqual(entry.VfxId, template.AssetId,
                        "Annie presentation template must reference its authored VFX asset.");
                    Assert.IsTrue(vfxDb.TryGet(entry.VfxId, out var vfx),
                        "Annie VFX row is missing: " + entry.VfxId);
                    Assert.AreEqual(entry.Resource,
                        string.IsNullOrEmpty(vfx.ResourceKey) ? vfx.Resource : vfx.ResourceKey,
                        "Annie VFX row must resolve through the linked extracted resource path.");
                    Assert.IsNotNull(Resources.Load<GameObject>(entry.Resource),
                        "Annie linked VFX prefab is missing: " + entry.Resource);
                }

                AssertBuffPresentation(harness, templates, vfxDb,
                    AnnieStunBuffId, AnnieStunPresentationId,
                    "ChampionVfx/14.Void_Annie/Void_Annie_Q_Hit");
                AssertBuffPresentation(harness, templates, vfxDb,
                    AnnieEBuffId, 90033004,
                    "ChampionVfx/14.Void_Annie/Void_Annie_E");
            }
        }

        [Test]
        public void AnnieSkillTimelines_ArePublishedAndDoNotDuplicateGameplayVfx()
        {
            var skillIds = new[] { AnnieQSkillId, AnnieWSkillId, AnnieESkillId, AnnieRSkillId };
            foreach (var skillId in skillIds)
            {
                var asset = Resources.Load<TextAsset>(
                    "moba/action_timeline/skill_" + skillId + ".moba.presentation");
                Assert.IsNotNull(asset, "Annie presentation timeline is missing: " + skillId);

                var sink = new TimelineRecordingSink();
                var runtime = new MobaSkillPresentationTimelineRuntime(skillId, asset.text, sink);
                runtime.StartAt(0f);
                Assert.AreEqual(0, sink.ClipCount,
                    "Annie skill VFX are emitted by their projectile, area, Buff, or summon gameplay cues.");
                runtime.Stop();
                Assert.AreEqual(1, sink.StopCount);
            }
        }

        private static void AssertBuffPresentation(
            MobaSkillConfigTestHarness harness,
            IConfigTable<PresentationTemplateMO> templates,
            VfxDatabase vfxDb,
            int buffId,
            int expectedVfxId,
            string expectedResource)
        {
            Assert.IsTrue(harness.Config.TryGetBuff(buffId, out var buff),
                "Annie Buff config is missing: " + buffId);
            Assert.AreEqual(expectedVfxId, buff.PresentationTemplateId,
                "Annie Buff must reference its authored presentation template.");
            Assert.IsTrue(templates.TryGet(expectedVfxId, out var template));
            Assert.AreEqual(expectedVfxId, template.AssetId);
            Assert.IsTrue(vfxDb.TryGet(expectedVfxId, out var vfx));
            Assert.AreEqual(expectedResource,
                string.IsNullOrEmpty(vfx.ResourceKey) ? vfx.Resource : vfx.ResourceKey);
            Assert.IsNotNull(Resources.Load<GameObject>(expectedResource),
                "Annie linked Buff VFX prefab is missing: " + expectedResource);
        }

        [UnityTest]
        public IEnumerator Q_RealProjectileSnapshotsConsumeLinkedMovingVfxAndReleaseOnExit()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                       new[] { AnnieQSkillId },
                       worldId: "annie_unity_q_projectile_vfx",
                       heroId: AnnieHeroId,
                       attributeTemplateId: AnnieHeroId))
            {
                harness.EnterGameAndWarmup();
                var actorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "annie_q_projectile_vfx_target", 9308, x: 3f, z: 0f);
                var cast = harness.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(
                    actorId,
                    1,
                    aimPos: new Vec3(3f, 0f, 0f),
                    aimDir: Vec3.Right,
                    targetActorId: targetActorId);
                Assert.IsTrue(cast.Success, "Annie Q cast failed: " + cast.FailReason);

                var vfxWorld = new EntityWorld();
                var vfxRoot = vfxWorld.Create("annie-q-projectile-vfx-root");
                var lookup = new BattleEntityLookup();
                var query = new BattleEntityQuery(vfxWorld, lookup);
                var vfxDb = BattleViewFactory.GetOrLoadVfxDb();
                Assert.IsNotNull(vfxDb, "The published VFX database must load.");
                var manager = new BattleVfxManager(vfxDb);
                var resources = new BattleViewResourceProvider(harness.Config, vfxDb);
                var handler = new BattleProjectileViewEventHandler(
                    vfxWorld, query, manager, in vfxRoot, resources);
                var projectileActorId = 0;
                var projectileVfxEntityId = default(IEntityId);
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
                                if (entry.TemplateId != 32030101) continue;
                                handler.HandleSnapshot(new[] { ProjectileEventSnapshotMapper.Map(in entry) });

                                if (entry.Kind == (int)ProjectileEventKind.Spawn)
                                {
                                    projectileActorId = entry.ProjectileActorId;
                                    Assert.Greater(projectileActorId, 0,
                                        "Annie Q spawn snapshot must expose the moving projectile actor.");
                                    Assert.AreEqual(1, vfxRoot.ChildCount,
                                        "Annie Q spawn must create one configured VFX entity.");
                                    var vfxEntity = vfxRoot.GetChild(0);
                                    projectileVfxEntityId = vfxEntity.Id;
                                    Assert.IsTrue(vfxEntity.TryGetRef(out BattleVfxComponent vfxComponent));
                                    Assert.AreEqual(90033001, vfxComponent.VfxId,
                                        "Annie Q must resolve its configured linked projectile VFX.");
                                    Assert.IsTrue(vfxEntity.TryGetRef(out BattleViewGameObjectComponent viewComponent));
                                    Assert.IsNotNull(viewComponent.GameObject);
                                    Assert.IsNotNull(viewComponent.GameObject.GetComponent<BattleVfxPoolableTag>());
                                    Assert.IsNotEmpty(
                                        viewComponent.GameObject.GetComponentsInChildren<ParticleSystem>(true),
                                        "Annie Q spawn must instantiate the linked particle prefab.");
                                    Assert.IsTrue(vfxEntity.TryGetRef(out BattleViewFollowComponent follow));
                                    Assert.AreEqual(projectileActorId, follow.TargetActorId,
                                        "Annie Q projectile VFX must follow the authoritative projectile actor.");
                                }
                                else if (entry.Kind == (int)ProjectileEventKind.Exit)
                                {
                                    sawExit = true;
                                    Assert.IsFalse(vfxWorld.IsAlive(projectileVfxEntityId),
                                        "Annie Q projectile exit must release its actor-following VFX.");
                                }
                            }
                        }
                    }

                    Assert.Greater(projectileActorId, 0,
                        "A real Annie Q cast must emit a projectile spawn snapshot.");
                    Assert.IsTrue(sawExit,
                        "Annie Q projectile must emit an exit snapshot after its configured flight.");
                    Assert.AreEqual(0, vfxRoot.ChildCount,
                        "Annie Q projectile VFX must be removed after projectile exit.");
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
        public IEnumerator E_RealBuffCueConsumesConfiguredShieldVfx()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                       new[] { AnnieESkillId },
                       worldId: "annie_unity_e_buff_vfx",
                       heroId: AnnieHeroId,
                       attributeTemplateId: AnnieHeroId))
            {
                harness.EnterGameAndWarmup();
                var actorId = harness.AssertPlayerActorBound();
                var cast = harness.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(
                    actorId,
                    3,
                    aimPos: default,
                    aimDir: Vec3.Right);
                Assert.IsTrue(cast.Success, "Annie E cast failed: " + cast.FailReason);

                var vfxWorld = new EntityWorld();
                var vfxRoot = vfxWorld.Create("annie-e-buff-vfx-root");
                var viewActor = vfxWorld.Create("annie-e-buff-vfx-actor");
                viewActor.WithRef(new BattleTransformComponent
                {
                    Position = Vector3.zero,
                    Forward = Vector3.forward,
                });
                var lookup = new BattleEntityLookup();
                lookup.Bind(new AbilityKit.Game.Battle.Entity.BattleNetId(actorId), viewActor);
                var query = new BattleEntityQuery(vfxWorld, lookup);
                var vfxDb = BattleViewFactory.GetOrLoadVfxDb();
                Assert.IsNotNull(vfxDb, "The published VFX database must load.");
                var manager = new BattleVfxManager(vfxDb);
                var handler = new BattlePresentationCueViewEventHandler(
                    vfxWorld, query, manager, in vfxRoot);
                var sawShieldCue = false;

                try
                {
                    for (var tick = 0; tick < 60 && !sawShieldCue; tick++)
                    {
                        harness.Tick(1);
                        var snapshots = new List<WorldStateSnapshot>();
                        harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>()
                            .CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
                        for (var snapshotIndex = 0; snapshotIndex < snapshots.Count; snapshotIndex++)
                        {
                            if (snapshots[snapshotIndex].OpCode != MobaOpCodes.Snapshot.PresentationCue)
                                continue;
                            var entries = MobaPresentationCueSnapshotCodec.Deserialize(
                                snapshots[snapshotIndex].Payload);
                            for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                            {
                                var entry = entries[entryIndex];
                                if (entry.TemplateId != 90033004 ||
                                    (entry.Stage != (int)AbilityKit.Protocol.Moba.StateSync.PresentationCueStage.Started &&
                                     entry.Stage != (int)AbilityKit.Protocol.Moba.StateSync.PresentationCueStage.Executed &&
                                     entry.Stage != (int)AbilityKit.Protocol.Moba.StateSync.PresentationCueStage.Refreshed &&
                                     entry.Stage != (int)AbilityKit.Protocol.Moba.StateSync.PresentationCueStage.StackChanged))
                                    continue;
                                Assert.AreEqual(actorId, entry.TargetActorId,
                                    "Annie E shield cue must target the protected actor.");
                                handler.HandleSnapshot(new[] { PresentationCueSnapshotMapper.Map(in entry) });
                                sawShieldCue = true;
                                break;
                            }
                        }
                    }

                    Assert.IsTrue(sawShieldCue,
                        "Annie E must publish a cue using its configured shield VFX template.");
                    yield return null;
                    Assert.AreEqual(1, vfxRoot.ChildCount,
                        "Annie E shield cue must instantiate one linked VFX entity.");
                    var vfxEntity = vfxRoot.GetChild(0);
                    Assert.IsTrue(vfxEntity.TryGetRef(out BattleVfxComponent vfxComponent));
                    Assert.AreEqual(90033004, vfxComponent.VfxId,
                        "Annie E shield cue must resolve the authored Annie shield VFX.");
                    Assert.IsTrue(vfxEntity.TryGetRef(out BattleViewGameObjectComponent viewComponent));
                    Assert.IsNotNull(viewComponent.GameObject);
                    Assert.IsNotEmpty(
                        viewComponent.GameObject.GetComponentsInChildren<ParticleSystem>(true),
                        "Annie E shield cue must instantiate the linked particle prefab.");
                }
                finally
                {
                    handler.Clear();
                    manager.Clear(vfxRoot);
                    if (vfxRoot.IsValid) vfxRoot.Destroy();
                    if (viewActor.IsValid) viewActor.Destroy();
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator W_RealAreaCueConsumesLinkedConeVfxWithCastDirectionAndFootprint()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                       new[] { AnnieWSkillId },
                       worldId: "annie_unity_w_area_vfx",
                       heroId: AnnieHeroId,
                       attributeTemplateId: AnnieHeroId))
            {
                harness.EnterGameAndWarmup();
                var actorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "annie_w_area_vfx_target", 9308, x: 3f, z: 0f);
                var cast = harness.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(
                    actorId, 2, aimPos: new Vec3(3f, 0f, 0f), aimDir: Vec3.Right,
                    targetActorId: targetActorId);
                Assert.IsTrue(cast.Success, "Annie W cast failed: " + cast.FailReason);

                var vfxWorld = new EntityWorld();
                var vfxRoot = vfxWorld.Create("annie-w-area-vfx-root");
                var viewActor = vfxWorld.Create("annie-w-area-vfx-actor");
                viewActor.WithRef(new BattleTransformComponent
                {
                    Position = Vector3.zero,
                    Forward = Vector3.forward,
                });
                var lookup = new BattleEntityLookup();
                lookup.Bind(new AbilityKit.Game.Battle.Entity.BattleNetId(actorId), viewActor);
                var query = new BattleEntityQuery(vfxWorld, lookup);
                var vfxDb = BattleViewFactory.GetOrLoadVfxDb();
                Assert.IsNotNull(vfxDb, "The published VFX database must load.");
                var manager = new BattleVfxManager(vfxDb);
                var handler = new BattlePresentationCueViewEventHandler(
                    vfxWorld, query, manager, in vfxRoot);
                var sawAreaCue = false;
                var observedCues = new System.Text.StringBuilder();
                var areaRuntime = harness.World.Services.Resolve<AbilityKit.Demo.Moba.Services.Area.MobaAreaRuntimeService>();
                var hasAoeConfig = harness.Config.TryGetAoe(42030301, out var aoeConfig);
                var hasAreaReporter = harness.World.Services.TryResolve(
                    out AbilityKit.Demo.Moba.Services.Area.MobaAreaPresentationCueReporter _);
                var sawActiveArea = areaRuntime.ActiveCount > 0;
                var vfxCreatedAtTime = 0f;

                try
                {
                    for (var tick = 0; tick < 60 && !sawAreaCue; tick++)
                    {
                        harness.Tick(1);
                        sawActiveArea |= areaRuntime.ActiveCount > 0;
                        var snapshots = new List<WorldStateSnapshot>();
                        harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>()
                            .CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
                        for (var snapshotIndex = 0; snapshotIndex < snapshots.Count; snapshotIndex++)
                        {
                            if (snapshots[snapshotIndex].OpCode != MobaOpCodes.Snapshot.PresentationCue)
                                continue;
                            var entries = MobaPresentationCueSnapshotCodec.Deserialize(
                                snapshots[snapshotIndex].Payload);
                            for (var entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                            {
                                var entry = entries[entryIndex];
                                observedCues.Append('[').Append(entry.TemplateId).Append('/')
                                    .Append(entry.VfxId).Append(" stage=").Append(entry.Stage)
                                    .Append(" domain=").Append(entry.Domain).Append("] ");
                                if (entry.TemplateId != 42030301 || entry.VfxId != 90033002 ||
                                    entry.Stage != (int)AbilityKit.Protocol.Moba.StateSync.PresentationCueStage.Started)
                                    continue;

                                var cue = PresentationCueSnapshotMapper.Map(in entry);
                                var decision = new BattlePresentationCueResolver().Resolve(in cue);
                                Assert.AreEqual(BattlePresentationCueDecisionKind.Play, decision.Kind);
                                Assert.AreEqual(42030301, decision.SpawnRequest.TemplateId);
                                Assert.AreEqual(1f, decision.SpawnRequest.Forward.X, 0.0001f);
                                Assert.AreEqual(0f, decision.SpawnRequest.Forward.Z, 0.0001f);
                                Assert.AreEqual(1, decision.SpawnRequest.ShapeKind);
                                Assert.AreEqual(45f, decision.SpawnRequest.HalfAngleDeg, 0.0001f);
                                Assert.AreEqual(11f, decision.SpawnRequest.Width, 0.0001f);
                                Assert.AreEqual(11f, decision.SpawnRequest.Length, 0.0001f);
                                Assert.AreEqual(700, decision.SpawnRequest.DurationMsOverride);
                                Assert.AreEqual(5.5f, decision.SpawnRequest.Radius, 0.0001f);
                                handler.HandleSnapshot(new[] { cue });
                                vfxCreatedAtTime = Time.time;
                                sawAreaCue = true;
                                break;
                            }
                        }
                    }

                    Assert.IsTrue(sawAreaCue,
                        "A real Annie W cast must publish its configured persistent area cue. Observed cues: " +
                        observedCues + " areaCount=" + areaRuntime.ActiveCount +
                        " sawActiveArea=" + sawActiveArea +
                        " hasReporter=" + hasAreaReporter +
                        " hasAoe=" + hasAoeConfig +
                        " aoeVfx=" + (aoeConfig == null ? 0 : aoeConfig.VfxId));
                    Assert.IsNotNull(Resources.Load<GameObject>(AnnieWAreaVfxResource),
                        "Annie W area VFX must resolve to its linked prefab.");
                    yield return null;
                    Assert.AreEqual(1, vfxRoot.ChildCount,
                        "Annie W area cue must instantiate one linked cone VFX.");
                    var vfxEntity = vfxRoot.GetChild(0);
                    Assert.IsTrue(vfxEntity.TryGetRef(out BattleVfxComponent vfxComponent));
                    Assert.AreEqual(90033002, vfxComponent.VfxId);
                    Assert.IsTrue(vfxEntity.TryGetRef(out BattleViewGameObjectComponent viewComponent));
                    Assert.IsNotNull(viewComponent.GameObject);
                    Assert.IsNotEmpty(viewComponent.GameObject.GetComponentsInChildren<ParticleSystem>(true));
                    Assert.Greater(Vector3.Dot(
                        viewComponent.GameObject.transform.forward, Vector3.right), 0.99f,
                        "The linked cone VFX must face the committed W cast direction.");
                    var scaleState = viewComponent.GameObject.GetComponent<BattlePresentationScaleState>();
                    Assert.IsNotNull(scaleState);
                    Assert.AreEqual(scaleState.BaseModelScale.x * 5.5f,
                        scaleState.EffectiveScale.x, 0.01f);
                    Assert.IsTrue(vfxEntity.TryGetRef(out BattleVfxLifetimeComponent lifetime));
                    Assert.AreEqual(0.7f, lifetime.ExpireAtTime - vfxCreatedAtTime, 0.001f);
                    Assert.Greater(lifetime.ExpireAtTime, Time.time,
                        "The linked cone VFX must remain alive for its configured area lifetime.");
                    Assert.IsTrue(vfxEntity.TryGetRef(out BattlePresentationLifetimeComponent presentation));
                    Assert.IsTrue(HasParameter(presentation.Request.Parameters, "areaShapeKind", 1f));
                    Assert.IsTrue(HasParameter(presentation.Request.Parameters, "areaHalfAngleDeg", 45f));
                    Assert.IsTrue(HasParameter(presentation.Request.Parameters, "areaWidth", 11f));
                    Assert.IsTrue(HasParameter(presentation.Request.Parameters, "areaLength", 11f));
                }
                finally
                {
                    handler.Clear();
                    manager.Clear(vfxRoot);
                    if (vfxRoot.IsValid) vfxRoot.Destroy();
                    if (viewActor.IsValid) viewActor.Destroy();
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator QWE_RealCastSnapshotsPlayLinkedModelActions()
        {
            var cases = new[]
            {
                (SkillId: AnnieQSkillId, Slot: 1, AnimatorState: "Void_Annie_Model_SpellQ"),
                (SkillId: AnnieWSkillId, Slot: 2, AnimatorState: "Void_Annie_Model_SpellW"),
                (SkillId: AnnieESkillId, Slot: 3, AnimatorState: "Void_Annie_Model_SpellE"),
            };

            foreach (var testCase in cases)
            {
                using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                           new[] { testCase.SkillId },
                           worldId: "annie_unity_skill_action_" + testCase.SkillId,
                           heroId: AnnieHeroId,
                           attributeTemplateId: AnnieHeroId))
                {
                    harness.EnterGameAndWarmup();
                    var actorId = harness.AssertPlayerActorBound();
                    var targetActorId = testCase.SkillId == AnnieESkillId
                        ? 0
                        : HeroSkillHeadlessContract.SpawnEnemyHero(
                            harness, "annie_action_target_" + testCase.SkillId, 9308, x: 3f, z: 0f);
                    var cast = harness.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(
                        actorId,
                        testCase.Slot,
                        aimPos: new Vec3(3f, 0f, 0f),
                        aimDir: Vec3.Right,
                        targetActorId: targetActorId);
                    Assert.IsTrue(cast.Success,
                        "Annie skill cast failed for " + testCase.SkillId + ": " + cast.FailReason);

                    CharacterActionData casting = default;
                    for (var tick = 0; tick < 30; tick++)
                    {
                        harness.Tick(1);
                        if (TryCaptureAction(harness, actorId, out casting) &&
                            casting.SkillId == testCase.SkillId && casting.CastInstanceId > 0)
                            break;
                    }

                    Assert.AreEqual(testCase.SkillId, casting.SkillId,
                        "The live character action snapshot must identify the cast skill.");
                    Assert.Greater(casting.CastInstanceId, 0,
                        "The model action must be driven by the real cast instance.");

                    var anniePrefab = Resources.Load<GameObject>(AnnieModelResource);
                    Assert.IsNotNull(anniePrefab, "Annie linked model prefab is missing.");
                    var annie = UnityEngine.Object.Instantiate(anniePrefab);
                    try
                    {
                        var component = new BattleCharacterHfsmComponent(
                            MobaCharacterHfsmTestFixture.CreateDefinition(),
                            CharacterPresentationActionCatalog.Load(new ResourcesTextAssetLoader()),
                            AnnieHeroId);
                        component.ApplyPresentation(AtCastBoundary(casting));
                        var view = annie.GetComponent<MonoCharacterHfsmView>() ??
                                   annie.AddComponent<MonoCharacterHfsmView>();

                        view.Bind(component, harness.TickRate);
                        var animator = annie.GetComponentInChildren<Animator>(true);
                        Assert.IsNotNull(animator, "Annie linked model must expose an Animator.");
                        Assert.AreEqual(
                            Animator.StringToHash(testCase.AnimatorState),
                            animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                            "The real skill snapshot must select the matching linked model action.");
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(annie);
                    }
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator R_RealCastSnapshotPlaysModelAndSummonEventConsumesLinkedSpawnVfx()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                       new[] { AnnieRSkillId },
                       worldId: "annie_unity_r_and_tibbers_presentation",
                       heroId: AnnieHeroId,
                       attributeTemplateId: AnnieHeroId))
            {
                harness.EnterGameAndWarmup();
                var actorId = harness.AssertPlayerActorBound();
                var targetActorId = HeroSkillHeadlessContract.SpawnEnemyHero(
                    harness, "annie_unity_r_target", 9308, x: 3f, z: 0f);
                var eventId = TriggeringIdUtil.GetEventEid(MobaSummonTriggering.Events.Spawned);
                SummonEventPayload summonSpawn = null;
                var eventBus = harness.World.Services.Resolve<IEventBus>();
                using (eventBus.Subscribe(
                           new EventKey<SummonEventPayload>(eventId),
                           payload => summonSpawn = payload))
                {
                    var cast = harness.World.Services.Resolve<SkillCastCoordinator>().TryCastBySlot(
                        actorId,
                        4,
                        aimPos: new Vec3(3f, 0f, 0f),
                        aimDir: Vec3.Right,
                        targetActorId: targetActorId);
                    Assert.IsTrue(cast.Success, "Annie R cast failed: " + cast.FailReason);

                    CharacterActionData casting = default;
                    for (var tick = 0; tick < 30; tick++)
                    {
                        harness.Tick(1);
                        if (TryCaptureAction(harness, actorId, out casting) &&
                            casting.SkillId == AnnieRSkillId && casting.CastInstanceId > 0)
                            break;
                    }
                    Assert.AreEqual(AnnieRSkillId, casting.SkillId,
                        "The live character action snapshot must identify Annie R.");
                    Assert.Greater(casting.CastInstanceId, 0,
                        "Annie R presentation must consume the real cast instance.");

                    var anniePrefab = Resources.Load<GameObject>(AnnieModelResource);
                    Assert.IsNotNull(anniePrefab, "Annie linked model prefab is missing.");
                    var annie = UnityEngine.Object.Instantiate(anniePrefab);
                    try
                    {
                        var component = new BattleCharacterHfsmComponent(
                            MobaCharacterHfsmTestFixture.CreateDefinition(),
                            CharacterPresentationActionCatalog.Load(new ResourcesTextAssetLoader()),
                            AnnieHeroId);
                        component.ApplyPresentation(AtCastBoundary(casting));
                        var view = annie.GetComponent<MonoCharacterHfsmView>() ??
                                   annie.AddComponent<MonoCharacterHfsmView>();

                        // R impact and Tibbers spawn VFX are emitted by their gameplay events.
                        view.Bind(component, harness.TickRate);
                        var animator = annie.GetComponentInChildren<Animator>(true);
                        Assert.IsNotNull(animator, "Annie linked model must expose an Animator.");
                        Assert.AreEqual(
                            Animator.StringToHash("Void_Annie_Model_SpellR"),
                            animator.GetCurrentAnimatorStateInfo(0).shortNameHash,
                            "The live R snapshot must select Annie's linked R model action.");
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(annie);
                    }

                    for (var tick = 0; tick < 120 && summonSpawn == null; tick++)
                        harness.Tick(1);
                    Assert.IsNotNull(summonSpawn,
                        "The live R effect must publish the summon lifecycle event consumed by the view.");
                    Assert.AreEqual(TibbersSummonId, summonSpawn.SummonId);
                    Assert.Greater(summonSpawn.SummonActorId, 0);

                    // The summon brain must reach the same authoritative action
                    // snapshot path as a hero: target selection/cast intent is
                    // observable as a generic attack-slot action, rather than
                    // only as a damage side effect.
                    CharacterActionData tibbersAttack = default;
                    for (var tick = 0; tick < 180; tick++)
                    {
                        harness.Tick(1);
                        if (TryCaptureAction(harness, summonSpawn.SummonActorId, out tibbersAttack) &&
                            tibbersAttack.Path != null &&
                            tibbersAttack.Path.StartsWith("life/alive/action/attack.slot.", StringComparison.Ordinal))
                            break;
                    }
                    Assert.IsTrue(tibbersAttack.Path != null &&
                                  tibbersAttack.Path.StartsWith("life/alive/action/attack.slot.", StringComparison.Ordinal),
                        "Tibbers behavior must publish a generic attack-slot CharacterAction snapshot.");
                    Assert.Greater(tibbersAttack.CastInstanceId, 0,
                        "Tibbers attack action must retain the authoritative skill cast instance.");
                    Assert.AreEqual(AnnieBasicAttackSkillId, tibbersAttack.SkillId,
                        "Tibbers attack action must identify the configured shared basic attack skill.");
                    Assert.IsNotNull(Resources.Load<GameObject>(TibbersSpawnVfxResource),
                        "Tibbers spawn VFX must resolve to the linked authored prefab.");
                    var vfxDb = BattleViewFactory.GetOrLoadVfxDb();
                    Assert.IsNotNull(vfxDb, "The published VFX database must load.");
                    var manager = new BattleVfxManager(vfxDb);
                    var resources = new BattleViewResourceProvider(harness.Config, vfxDb);
                    Assert.IsTrue(resources.GetOrLoadConfigs().TryGetSummon(TibbersSummonId, out var tibbersConfig),
                        "Tibbers summon presentation must resolve the production summon config.");
                    Assert.AreEqual(90033006, tibbersConfig.SpawnVfxId,
                        "Tibbers summon config must select the linked spawn VFX.");

                    var summonEntity = harness.AssertActorEntity(summonSpawn.SummonActorId);
                    var spawnPosition = summonEntity.transform.Value.Position;
                    var spawnPositionUnity = new Vector3(spawnPosition.X, spawnPosition.Y, spawnPosition.Z);
                    var vfxWorld = new EntityWorld();
                    var vfxRoot = vfxWorld.Create("annie-tibbers-presentation-vfx-root");
                    var identity = Quaternion.identity;
                    Assert.IsTrue(manager.TryCreateVfxEntity(
                            vfxWorld,
                            vfxRoot,
                            90033006,
                            default,
                            in spawnPositionUnity,
                            in identity,
                            out var directVfx),
                        "The VFX manager must create Tibbers' configured spawn asset in the acceptance world.");
                    manager.DestroyVfxEntity(vfxWorld, directVfx.Id);
                    Assert.AreEqual(0, vfxRoot.ChildCount,
                        "The direct VFX manager probe must be removed before testing summon event consumption.");

                    var viewActor = vfxWorld.Create("annie-tibbers-presentation-actor");
                    viewActor.WithRef(new BattleTransformComponent
                    {
                        Position = new Vector3(spawnPosition.X, spawnPosition.Y, spawnPosition.Z),
                        Forward = Vector3.forward,
                    });
                    var lookup = new BattleEntityLookup();
                    lookup.Bind(
                        new AbilityKit.Game.Battle.Entity.BattleNetId(summonSpawn.SummonActorId),
                        viewActor);
                    var query = new BattleEntityQuery(vfxWorld, lookup);
                    var sink = new BattleViewEventSink(
                        null, query, null, manager, vfxRoot, null, null, resources);

                    try
                    {
                        sink.OnSummonEvent(
                            MobaSummonTriggering.Events.Spawned,
                            new DemoMobaSummonEventPayload(
                                summonSpawn.SummonActorId,
                                summonSpawn.SummonId,
                                summonSpawn.OwnerActorId,
                                summonSpawn.RootOwnerActorId,
                                summonSpawn.Reason));

                        for (var frame = 0; frame < 180 && vfxRoot.ChildCount == 0; frame++)
                            yield return null;

                        Assert.AreEqual(1, vfxRoot.ChildCount,
                            "The actual Tibbers spawn event must instantiate its configured lifecycle VFX.");
                        var vfxEntity = vfxRoot.GetChild(0);
                        Assert.IsTrue(vfxEntity.TryGetRef(out BattleVfxComponent vfxComponent));
                        Assert.AreEqual(90033006, vfxComponent.VfxId,
                            "Tibbers spawn must resolve the production-configured Annie spawn VFX.");
                        Assert.IsTrue(vfxEntity.TryGetRef(out BattleViewGameObjectComponent viewComponent));
                        Assert.IsNotNull(viewComponent.GameObject);
                        Assert.IsNotNull(viewComponent.GameObject.GetComponent<BattleVfxPoolableTag>());
                        Assert.IsNotEmpty(
                            viewComponent.GameObject.GetComponentsInChildren<ParticleSystem>(true),
                            "The spawned lifecycle VFX must instantiate the linked particle prefab.");
                        Assert.That(
                            Vector3.Distance(viewComponent.GameObject.transform.position,
                                new Vector3(spawnPosition.X, spawnPosition.Y, spawnPosition.Z)),
                            Is.LessThan(0.01f),
                            "Tibbers spawn VFX must use the authoritative summon position.");
                    }
                    finally
                    {
                        manager.Clear(vfxRoot);
                        if (vfxRoot.IsValid) vfxRoot.Destroy();
                        if (viewActor.IsValid) viewActor.Destroy();
                    }
                }
            }

            yield return null;
        }

        [Test]
        public void TibbersLifecycleWithoutAuthoredDeathVfx_DoesNotPlayDevelopmentPlaceholder()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                       new[] { AnnieRSkillId },
                       worldId: "annie_tibbers_lifecycle_vfx_contract",
                       heroId: AnnieHeroId,
                       attributeTemplateId: AnnieHeroId))
            {
                var vfxDb = BattleViewFactory.GetOrLoadVfxDb();
                Assert.IsNotNull(vfxDb, "The published VFX database must load.");
                var resources = new BattleViewResourceProvider(harness.Config, vfxDb);
                Assert.IsTrue(resources.GetOrLoadConfigs().TryGetSummon(
                    TibbersSummonId, out var tibbers),
                    "Tibbers summon config must resolve for lifecycle VFX consumption.");
                Assert.AreEqual(0, tibbers.DeathVfxId,
                    "Tibbers death VFX must remain explicitly unset until a linked asset exists.");
                Assert.AreEqual(0, tibbers.DespawnVfxId,
                    "Tibbers despawn VFX must remain explicitly unset until a linked asset exists.");

                var resolver = new BattleSummonVfxResolver(resources.GetOrLoadConfigs());
                Assert.AreEqual(0, resolver.ResolveDeathVfxId(TibbersSummonId));
                Assert.AreEqual(0, resolver.ResolveDespawnVfxId(TibbersSummonId));

                var vfxWorld = new EntityWorld();
                var vfxRoot = vfxWorld.Create("annie-tibbers-lifecycle-vfx-root");
                var viewActor = vfxWorld.Create("annie-tibbers-lifecycle-actor");
                viewActor.WithRef(new BattleTransformComponent
                {
                    Position = new Vector3(2f, 0f, 0f),
                    Forward = Vector3.forward,
                });
                var lookup = new BattleEntityLookup();
                const int lifecycleActorId = 300305021;
                lookup.Bind(new AbilityKit.Game.Battle.Entity.BattleNetId(lifecycleActorId), viewActor);
                var query = new BattleEntityQuery(vfxWorld, lookup);
                var sink = new BattleViewEventSink(
                    null, query, null, new BattleVfxManager(vfxDb), vfxRoot,
                    null, null, resources);
                var payload = new DemoMobaSummonEventPayload(
                    lifecycleActorId, TibbersSummonId, 3003, 3003, (int)SummonDespawnReason.Killed);

                sink.OnSummonEvent(MobaSummonTriggering.Events.Died, in payload);
                sink.OnSummonEvent(MobaSummonTriggering.Events.Despawned, in payload);
                Assert.AreEqual(0, vfxRoot.ChildCount,
                    "Unset Tibbers death/despawn VFX must not fall back to generic placeholder assets.");

                if (viewActor.IsValid) viewActor.Destroy();
                if (vfxRoot.IsValid) vfxRoot.Destroy();
            }
        }

        [Test]
        public void TibbersSpawnSnapshot_CreatesLinkedModelAndDespawnReleasesView()
        {
            using (var harness = MobaSkillConfigTestHarness.CreateForSinglePlayer(
                       new[] { AnnieRSkillId },
                       worldId: "annie_tibbers_model_lifecycle",
                       heroId: AnnieHeroId,
                       attributeTemplateId: AnnieHeroId))
            {
                var world = new EntityWorld();
                var lookup = new BattleEntityLookup();
                var factory = new BattleEntityFactory(world, lookup);
                var vfxRoot = world.Create("annie-tibbers-view-vfx");
                var vfxDb = BattleViewFactory.GetOrLoadVfxDb();
                Assert.IsNotNull(vfxDb, "The published VFX database must load.");
                var resources = new BattleViewResourceProvider(harness.Config, vfxDb);
                var vfx = new BattleVfxManager(vfxDb);
                var binder = new BattleViewBinder(vfx, in vfxRoot, resources, null, null, null);
                var context = new BattleContext
                {
                    EntityWorld = world,
                    EntityLookup = lookup,
                    EntityFactory = factory,
                    ViewVfxManager = vfx,
                    ViewVfxNode = vfxRoot,
                };

                const int tibbersActorId = 300305021;
                var spawn = new ActorSpawnData(
                    ActorSpawnKind.Summon,
                    tibbersActorId,
                    AnnieHeroId,
                    TibbersSummonId,
                    TibbersSummonId,
                    "Tibbers",
                    4f,
                    0f,
                    6f,
                    0f,
                    1f,
                    1,
                    1000f,
                    1000f);

                try
                {
                    BattleSnapshotEntityApplier.ApplySpawn(context, new[] { spawn });
                    Assert.IsTrue(lookup.TryResolve(world,
                        new EntityBattleNetId(tibbersActorId), out var entity));
                    Assert.IsTrue(entity.TryGetRef(out BattleEntityMetaComponent meta));
                    Assert.AreEqual(BattleEntityKind.Summon, meta.Kind);
                    Assert.AreEqual(TibbersSummonId, meta.EntityCode);
                    Assert.AreEqual(30030502, resources.ResolveModelId(meta));

                    binder.InterpolationEnabled = false;
                    binder.SmoothingHz = 0f;
                    binder.Sync(entity);
                    binder.TickInterpolation(context, context, 1f / 30f);
                    Assert.IsTrue(binder.TryGetShellGameObject(entity.Id, out var shell));
                    Assert.IsNotNull(shell);
                    Assert.AreEqual("Summon_" + tibbersActorId, shell.name);
                    Assert.That(Vector3.Distance(new Vector3(4f, 0f, 6f), shell.transform.position),
                        Is.LessThan(0.01f), "Tibbers view must use the authoritative spawn position.");
                    Assert.IsNotNull(Resources.Load<GameObject>(
                        "ChampionVfx/14.Void_Annie/Void_Annie_Tibbers_Model"));
                    Assert.IsNotNull(shell.GetComponentInChildren<Animator>(true),
                        "The linked Tibbers model must expose its animator.");

                    // Summons consume the same authoritative CharacterAction snapshot
                    // contract as characters.  This verifies the view bridge creates
                    // the generic HFSM state and Mono view from the entity kind alone.
                    var tibbersAction = new CharacterActionData(
                        tibbersActorId,
                        1,
                        12,
                        "life/alive/action/idle",
                        ((long)tibbersActorId << 32) | 1u,
                        1,
                        0,
                        0,
                        0);
                    binder.Sync(entity, new SummonActionRuntimeContext(tibbersAction));
                    Assert.IsTrue(entity.TryGetRef(out BattleCharacterHfsmComponent tibbersHfsm));
                    Assert.AreEqual("life/alive/action/idle", tibbersHfsm.Action.Path);
                    Assert.IsNotNull(shell.GetComponent<MonoCharacterHfsmView>(),
                        "Summon action snapshots must bind the generic HFSM view component.");

                    AssertAnimatorState(shell, "Void_Annie_Tibbers_Idle");
                    var actionContext = new SummonActionRuntimeContext(tibbersAction);
                    actionContext.SetAction(new CharacterActionData(
                        tibbersActorId, 1, 24, "life/alive/action/moving",
                        ((long)tibbersActorId << 32) | 2u, 2, 0, 0, 0));
                    binder.Sync(entity, actionContext);
                    AssertAnimatorState(shell, "Void_Annie_Tibbers_Run");

                    actionContext.SetAction(new CharacterActionData(
                        tibbersActorId, 1, 48, "life/alive/action/attack.slot.1",
                        ((long)tibbersActorId << 32) | 4u, 4, 0, 901, AnnieBasicAttackSkillId));
                    LogAssert.Expect(LogType.Error,
                        "Missing MOBA presentation timeline: moba/action_timeline/skill_" +
                        AnnieBasicAttackSkillId + ".moba.presentation");
                    binder.Sync(entity, actionContext);
                    Assert.AreEqual("life/alive/action/attack.slot.1", tibbersHfsm.Action.Path);
                    var attackPlayback = tibbersHfsm.Evaluate(48, 30, null);
                    Assert.IsTrue(attackPlayback.HasValue, "Tibbers attack action catalog did not resolve.");
                    Assert.AreEqual("Void_Annie_Tibbers_Attack1", attackPlayback.Value.AnimatorState);
                    AssertAnimatorState(shell, "Void_Annie_Tibbers_Attack1");

                    actionContext.SetAction(new CharacterActionData(
                        tibbersActorId, 1, 60, "life/alive/action/attack.slot.2",
                        ((long)tibbersActorId << 32) | 5u, 5, 0, 902, AnnieBasicAttackSkillId));
                    binder.Sync(entity, actionContext);
                    AssertAnimatorState(shell, "Void_Annie_Tibbers_Attack2");

                    actionContext.SetAction(new CharacterActionData(
                        tibbersActorId, 1, 72, "life/dead",
                        ((long)tibbersActorId << 32) | 3u, 3, 0, 0, 0));
                    binder.Sync(entity, actionContext);
                    AssertAnimatorState(shell, "Void_Annie_Tibbers_Death");

                    var entityId = entity.Id;
                    BattleSnapshotEntityApplier.ApplyDespawn(context, new[]
                    {
                        new ActorDespawnData(tibbersActorId, ActorDespawnReason.SummonKilled)
                    });
                    // The presentation feature receives this through EntityDestroyed.
                    binder.OnDestroyed(entityId);

                    Assert.IsFalse(world.IsAlive(entityId));
                    Assert.IsFalse(lookup.TryResolve(world,
                        new EntityBattleNetId(tibbersActorId), out _));
                    Assert.IsFalse(binder.TryGetShellGameObject(entityId, out _));
                }
                finally
                {
                    binder.Clear();
                    if (vfxRoot.IsValid) vfxRoot.Destroy();
                }
            }
        }

        private static bool HasParameter(
            IReadOnlyList<PresentationParameter> parameters,
            string key,
            float expected)
        {
            if (parameters == null) return false;
            for (var i = 0; i < parameters.Count; i++)
            {
                if (parameters[i].Key == key)
                    return Mathf.Abs(parameters[i].Value - expected) <= 0.0001f;
            }

            return false;
        }

        private static CharacterActionData AtCastBoundary(CharacterActionData action) =>
            new CharacterActionData(
                action.ActorId,
                action.EntityVersion,
                action.StartFrame,
                action.Path,
                action.InstanceId,
                action.StartFrame,
                localFrame: 0,
                action.CastInstanceId,
                action.SkillId,
                action.ModeId,
                action.ModeInstanceId,
                action.ModeStartFrame,
                action.ModeLocalFrame);

        private static bool TryCaptureAction(
            MobaSkillConfigTestHarness harness,
            int actorId,
            out CharacterActionData action)
        {
            var snapshots = new List<WorldStateSnapshot>();
            harness.World.Services.Resolve<IWorldStateSnapshotBatchProvider>()
                .CollectSnapshots(harness.FrameTime.Frame, snapshots, 32);
            foreach (var snapshot in snapshots)
            {
                if (!CharacterActionSnapshotRoute.TryDecode(snapshot, out var entries)) continue;
                foreach (var entry in entries)
                {
                    if (entry.ActorId != actorId) continue;
                    action = entry;
                    return true;
                }
            }

            action = default;
            return false;
        }

        private static void AssertAnimatorState(GameObject shell, string stateName)
        {
            var animator = shell.GetComponentInChildren<Animator>(true);
            Assert.IsNotNull(animator, "Summon model must expose an Animator.");
            animator.Update(0f);
            var current = animator.GetCurrentAnimatorStateInfo(0);
            var expectedHash = Animator.StringToHash(stateName);
            var hasState = animator.HasState(0, expectedHash);
            var clipInfo = animator.GetCurrentAnimatorClipInfo(0);
            var clipMatched = false;
            var clipNames = string.Empty;
            for (var i = 0; i < clipInfo.Length; i++)
            {
                if (i > 0) clipNames += ",";
                clipNames += clipInfo[i].clip == null ? "<null>" : clipInfo[i].clip.name;
                if (clipInfo[i].clip != null &&
                    string.Equals(clipInfo[i].clip.name, stateName, StringComparison.Ordinal))
                {
                    clipMatched = true;
                    break;
                }
            }
            Assert.IsTrue(current.shortNameHash == expectedHash ||
                          current.fullPathHash == expectedHash || clipMatched,
                "Summon action did not drive the expected Animator state: " + stateName +
                " (shortHash=" + current.shortNameHash + ", fullHash=" + current.fullPathHash +
                ", hasState=" + hasState + ", clips=" + clipNames + ")");
        }

        private sealed class SummonActionRuntimeContext : IBattleRuntimeContext, IBattleCharacterActionReadPort
        {
            private CharacterActionData _action;

            public SummonActionRuntimeContext(CharacterActionData action)
            {
                _action = action;
            }

            public void SetAction(CharacterActionData action)
            {
                _action = action;
            }

            public AbilityKit.Game.Battle.BattleLogicSession Session => null;
            public AbilityKit.Game.Flow.BattleStartPlan Plan => TestPlan;
            public int LastFrame => _action.SnapshotFrame;
            public double LogicTimeSeconds => 0d;
            public int LocalActorId => 0;
            public AbilityKit.Game.Flow.Battle.Modules.BattleSessionHooks Hooks => null;

            private static AbilityKit.Game.Flow.BattleStartPlan TestPlan => new AbilityKit.Game.Flow.BattleStartPlan(
                worldId: "annie-summon-action", worldType: "moba", clientId: "test", playerId: "test",
                tickRate: 30, inputDelayFrames: 0, useGatewayTransport: false,
                gatewayHost: string.Empty, gatewayPort: 0, numericRoomId: 0,
                gatewaySessionToken: string.Empty, gatewayRegion: string.Empty,
                gatewayServerId: string.Empty, gatewayAutoCreateRoom: false,
                gatewayAutoJoinRoom: false, gatewayJoinRoomId: string.Empty,
                gatewayCreateRoomOpCode: 0, gatewayJoinRoomOpCode: 0,
                autoConnect: false, autoCreateWorld: false, autoJoin: false,
                autoReady: false, syncMode: BattleSyncMode.Lockstep,
                viewEventSourceMode: AbilityKit.Game.Flow.BattleViewEventSourceMode.SnapshotOnly,
                enableConfirmedAuthorityWorld: false, enableInputRecording: false,
                inputRecordOutputPath: string.Empty, enableInputReplay: false,
                inputReplayPath: string.Empty, createWorldOpCode: 0,
                createWorldPayload: null);

            public bool TryGetCharacterAction(int actorId, out CharacterActionData action)
            {
                if (actorId == _action.ActorId)
                {
                    action = _action;
                    return true;
                }

                action = default;
                return false;
            }
        }

        private sealed class TimelineRecordingSink : IMobaSkillPresentationTimelineSink
        {
            public int ClipCount { get; private set; }
            public int StopCount { get; private set; }

            public void OnClipStart(long skillInstanceId, GroupDto group, ClipDto clip, float offsetSeconds)
            {
                ClipCount++;
            }

            public void OnTimelineStop(long skillInstanceId)
            {
                StopCount++;
            }
        }
    }
}

using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Area;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    /// <summary>
    /// 验证 Area Spawn/Ended Producer（MobaAreaSyncSystem）的诊断草稿生成与采集。
    /// </summary>
    public sealed class MobaAreaDiagnosticProducerTests
    {
        private BattleDiagnosticSessionScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("session", "world", 1);
        }

        private static MobaAreaRuntimeInfo CreateAreaInfo(
            int areaId = 301,
            int templateId = 601,
            int ownerActorId = 7,
            long sourceContextId = 700L,
            long rootContextId = 500L,
            long ownerContextId = 0L)
        {
            return new MobaAreaRuntimeInfo(
                areaId,
                templateId,
                ownerActorId,
                center: default,
                radius: 3f,
                collisionLayerMask: 0,
                maxTargets: 5,
                spawnFrame: 10,
                delayTriggerFrame: 0,
                sourceContextId,
                rootContextId,
                ownerContextId);
        }

        // ===== AreaSpawned 草稿映射 =====

        [Test]
        public void CreateAreaSpawnedDraft_MapsAllFields()
        {
            var info = CreateAreaInfo(
                areaId: 301,
                templateId: 601,
                ownerActorId: 7,
                sourceContextId: 700L,
                rootContextId: 500L);

            var draft = MobaAreaDiagnosticProducer.CreateAreaSpawnedDraft(in info);

            Assert.That(draft.Kind, Is.EqualTo(BattleDiagnosticEventKind.AreaSpawned));
            Assert.That(draft.Channel, Is.EqualTo(BattleDiagnosticEventChannel.TemporaryEntity));
            Assert.That(draft.Outcome, Is.EqualTo(BattleDiagnosticEventOutcome.Succeeded));
            Assert.That(draft.SourceActorId, Is.EqualTo(7));
            Assert.That(draft.TargetActorId, Is.EqualTo(0));
            Assert.That(draft.ConfigId, Is.EqualTo(601));
            Assert.That(draft.RootContextId, Is.EqualTo(500));
            Assert.That(draft.ContextId, Is.EqualTo(700));
            Assert.That(draft.Summary, Does.Contain("301"));
            Assert.That(draft.Summary, Does.Contain("601"));
            Assert.That(draft.Summary, Does.Contain("owner=7"));
        }

        [Test]
        public void RuntimeMetadata_RetainsAreaGroupClaimConfiguration()
        {
            var center = Vec3.Zero;
            var forward = Vec3.Forward;
            var runtime = new MobaAreaRuntimeService();

            runtime.RegisterSpawn(
                new AbilityKit.Combat.Projectile.AreaId(301),
                templateId: 601,
                ownerActorId: 7,
                in center,
                radius: 3f,
                collisionLayerMask: 0,
                maxTargets: 5,
                frame: 10,
                delayFrames: 0,
                sourceContextId: 700L,
                rootContextId: 500L,
                ownerContextId: 0L,
                shapeKind: 0,
                in forward,
                halfAngleDeg: 0f,
                width: 6f,
                length: 6f,
                groupKey: 3,
                claimTargetsWithinGroup: true);

            Assert.That(runtime.TryGetArea(301, out var info), Is.True);
            Assert.That(info.RootContextId, Is.EqualTo(500L));
            Assert.That(info.GroupKey, Is.EqualTo(3));
            Assert.That(info.ClaimTargetsWithinGroup, Is.True);
        }

        [Test]
        public void CreateAreaSpawnedDraft_WithoutRootContext_FallsBackToSourceContextId()
        {
            var info = CreateAreaInfo(
                sourceContextId: 900L,
                rootContextId: 0L);

            var draft = MobaAreaDiagnosticProducer.CreateAreaSpawnedDraft(in info);

            Assert.That(draft.ContextId, Is.EqualTo(900));
            Assert.That(draft.RootContextId, Is.EqualTo(900));
        }

        // ===== AreaEnded 草稿映射 =====

        [Test]
        public void CreateAreaEndedDraft_MapsAllFields()
        {
            var info = CreateAreaInfo(
                areaId: 301,
                templateId: 601,
                ownerActorId: 7,
                sourceContextId: 700L,
                rootContextId: 500L);

            var draft = MobaAreaDiagnosticProducer.CreateAreaEndedDraft(in info);

            Assert.That(draft.Kind, Is.EqualTo(BattleDiagnosticEventKind.AreaEnded));
            Assert.That(draft.Channel, Is.EqualTo(BattleDiagnosticEventChannel.TemporaryEntity));
            Assert.That(draft.Outcome, Is.EqualTo(BattleDiagnosticEventOutcome.Succeeded));
            Assert.That(draft.SourceActorId, Is.EqualTo(7));
            Assert.That(draft.TargetActorId, Is.EqualTo(0));
            Assert.That(draft.ConfigId, Is.EqualTo(601));
            Assert.That(draft.RootContextId, Is.EqualTo(500));
            Assert.That(draft.ContextId, Is.EqualTo(700));
            Assert.That(draft.Summary, Does.Contain("301"));
            Assert.That(draft.Summary, Does.Contain("601"));
        }

        [Test]
        public void CreateAreaEndedDraft_WithoutRootContext_FallsBackToSourceContextId()
        {
            var info = CreateAreaInfo(
                sourceContextId: 800L,
                rootContextId: 0L);

            var draft = MobaAreaDiagnosticProducer.CreateAreaEndedDraft(in info);

            Assert.That(draft.ContextId, Is.EqualTo(800));
            Assert.That(draft.RootContextId, Is.EqualTo(800));
        }

        // ===== Collector 流转 =====

        [Test]
        public void AreaSpawnedDraft_FlowsThroughCollector()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);
            var info = CreateAreaInfo();

            var draft = MobaAreaDiagnosticProducer.CreateAreaSpawnedDraft(in info);
            var accepted = collector.TryCollect(in draft);

            Assert.That(accepted, Is.True);
            Assert.That(collector.LastSequence, Is.EqualTo(1));
            Assert.That(collector.Store.Count, Is.EqualTo(1));
        }

        [Test]
        public void AreaEndedDraft_FlowsThroughCollector()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);
            var info = CreateAreaInfo();

            var draft = MobaAreaDiagnosticProducer.CreateAreaEndedDraft(in info);
            var accepted = collector.TryCollect(in draft);

            Assert.That(accepted, Is.True);
            Assert.That(collector.LastSequence, Is.EqualTo(1));
            Assert.That(collector.Store.Count, Is.EqualTo(1));
        }

        [Test]
        public void AreaDrafts_RespectDisabledChannel()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);
            collector.EnabledChannels = BattleDiagnosticEventChannel.Skill;

            var info = CreateAreaInfo();

            var spawnDraft = MobaAreaDiagnosticProducer.CreateAreaSpawnedDraft(in info);
            var endedDraft = MobaAreaDiagnosticProducer.CreateAreaEndedDraft(in info);

            Assert.That(collector.TryCollect(in spawnDraft), Is.False);
            Assert.That(collector.TryCollect(in endedDraft), Is.False);
            Assert.That(collector.LastSequence, Is.Zero);
            Assert.That(collector.Store.Count, Is.Zero);
        }

        [Test]
        public void AreaSpawnAndEnd_ProduceStrictSequence()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);
            var info = CreateAreaInfo();

            var spawnDraft = MobaAreaDiagnosticProducer.CreateAreaSpawnedDraft(in info);
            var endedDraft = MobaAreaDiagnosticProducer.CreateAreaEndedDraft(in info);

            collector.TryCollect(in spawnDraft);
            collector.TryCollect(in endedDraft);

            Assert.That(collector.LastSequence, Is.EqualTo(2));
            Assert.That(collector.Store.Count, Is.EqualTo(2));
        }
    }
}

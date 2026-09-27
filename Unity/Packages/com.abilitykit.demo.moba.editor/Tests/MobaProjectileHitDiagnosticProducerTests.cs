using AbilityKit.Core.Mathematics;
using AbilityKit.Combat.Projectile;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.Projectile;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    /// <summary>
    /// 验证 Projectile Hit Producer（ProjectileHit）的诊断草稿生成与采集。
    /// </summary>
    public sealed class MobaProjectileHitDiagnosticProducerTests
    {
        private BattleDiagnosticSessionScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("session", "world", 1);
        }

        private static ProjectileSourceContext CreateSourceContext(
            int sourceActorId = 7,
            int targetActorId = 11,
            int projectileConfigId = 301,
            long sourceContextId = 500L,
            long rootContextId = 400L,
            long ownerContextId = 0L,
            MobaSkillCastRuntimeHandle skillRuntimeHandle = default,
            MobaGameplayOrigin origin = default)
        {
            return new ProjectileSourceContext(
                sourceActorId,
                targetActorId,
                projectileConfigId,
                sourceContextId,
                rootContextId,
                ownerContextId,
                in skillRuntimeHandle,
                in origin);
        }

        private static ProjectileHitEvent CreateHitEvent(
            int projectileValue = 1024,
            int ownerId = 7,
            int templateId = 301,
            int launcherActorId = 7,
            int rootActorId = 7,
            int hitColliderValue = 55,
            float distance = 1.5f,
            int frame = 42,
            int hitCount = 1)
        {
            return new ProjectileHitEvent(
                new ProjectileId(projectileValue),
                ownerId,
                templateId,
                launcherActorId,
                rootActorId,
                new ColliderId(hitColliderValue),
                distance,
                default,
                default,
                frame,
                hitCount);
        }

        // ===== ProjectileHit 草稿映射 =====

        [Test]
        public void CreateProjectileHitDraft_MapsAllFields()
        {
            var sourceContext = CreateSourceContext(
                sourceActorId: 7,
                targetActorId: 11,
                projectileConfigId: 301,
                sourceContextId: 500L,
                rootContextId: 400L,
                skillRuntimeHandle: new MobaSkillCastRuntimeHandle(42, 2, 400));
            var hit = CreateHitEvent(frame: 88);

            var draft = MobaProjectileHitDiagnosticProducer.CreateProjectileHitDraft(in hit, 11, in sourceContext);

            Assert.That(draft.Kind, Is.EqualTo(BattleDiagnosticEventKind.ProjectileHit));
            Assert.That(draft.Channel, Is.EqualTo(BattleDiagnosticEventChannel.TemporaryEntity));
            Assert.That(draft.Outcome, Is.EqualTo(BattleDiagnosticEventOutcome.Succeeded));
            Assert.That(draft.SourceActorId, Is.EqualTo(7));
            Assert.That(draft.TargetActorId, Is.EqualTo(11));
            Assert.That(draft.ConfigId, Is.EqualTo(301));
            Assert.That(draft.RootContextId, Is.EqualTo(400));
            Assert.That(draft.ContextId, Is.EqualTo(500));
            Assert.That(draft.SkillRuntime, Is.EqualTo(new BattleDiagnosticRuntimeHandle(42, 2)));
            Assert.That(draft.Summary, Does.Contain("301"));
            Assert.That(draft.Summary, Does.Contain("hitActorId=11"));
            Assert.That(draft.Summary, Does.Contain("frame=88"));
        }

        [Test]
        public void CreateProjectileHitDraft_WithoutSkillRuntime_ProducesDefaultHandle()
        {
            var sourceContext = CreateSourceContext(
                sourceActorId: 7,
                projectileConfigId: 301,
                sourceContextId: 500L,
                rootContextId: 400L,
                skillRuntimeHandle: default);
            var hit = CreateHitEvent();

            var draft = MobaProjectileHitDiagnosticProducer.CreateProjectileHitDraft(in hit, 11, in sourceContext);

            Assert.That(draft.SkillRuntime, Is.EqualTo(default(BattleDiagnosticRuntimeHandle)));
        }

        [Test]
        public void CreateProjectileHitDraft_WithoutRootContext_FallsBackToSourceContextId()
        {
            var sourceContext = CreateSourceContext(
                sourceActorId: 7,
                projectileConfigId: 301,
                sourceContextId: 900L,
                rootContextId: 0L,
                skillRuntimeHandle: default);
            var hit = CreateHitEvent();

            var draft = MobaProjectileHitDiagnosticProducer.CreateProjectileHitDraft(in hit, 11, in sourceContext);

            Assert.That(draft.ContextId, Is.EqualTo(900));
            Assert.That(draft.RootContextId, Is.EqualTo(900));
        }

        [Test]
        public void CreateProjectileHitDraft_WithOrigin_ResolvesRootFromOrigin()
        {
            var origin = new MobaGameplayOrigin(
                7,
                11,
                MobaExecutionKind.ProjectileLaunch,
                301,
                500L,
                500L,
                700L,
                0L,
                default);

            var sourceContext = CreateSourceContext(
                sourceActorId: 7,
                targetActorId: 11,
                projectileConfigId: 301,
                sourceContextId: 500L,
                rootContextId: 0L,
                origin: origin);
            var hit = CreateHitEvent();

            var draft = MobaProjectileHitDiagnosticProducer.CreateProjectileHitDraft(in hit, 11, in sourceContext);

            Assert.That(draft.RootContextId, Is.EqualTo(700));
            Assert.That(draft.ContextId, Is.EqualTo(500));
        }

        // ===== Collector 流转 =====

        [Test]
        public void ProjectileHitDraft_FlowsThroughCollector()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);
            var sourceContext = CreateSourceContext(
                sourceActorId: 7,
                projectileConfigId: 301,
                sourceContextId: 500L,
                rootContextId: 400L);
            var hit = CreateHitEvent();

            var draft = MobaProjectileHitDiagnosticProducer.CreateProjectileHitDraft(in hit, 11, in sourceContext);
            var accepted = collector.TryCollect(in draft);

            Assert.That(accepted, Is.True);
            Assert.That(collector.LastSequence, Is.EqualTo(1));
            Assert.That(collector.Store.Count, Is.EqualTo(1));
        }

        [Test]
        public void ProjectileHitDraft_RespectsDisabledChannel()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);
            collector.EnabledChannels = BattleDiagnosticEventChannel.Skill;

            var sourceContext = CreateSourceContext(
                sourceActorId: 7,
                projectileConfigId: 301,
                sourceContextId: 500L,
                rootContextId: 400L);
            var hit = CreateHitEvent();

            var draft = MobaProjectileHitDiagnosticProducer.CreateProjectileHitDraft(in hit, 11, in sourceContext);

            Assert.That(collector.TryCollect(in draft), Is.False);
            Assert.That(collector.LastSequence, Is.Zero);
            Assert.That(collector.Store.Count, Is.Zero);
        }

        [Test]
        public void ProjectileHitDrafts_ProduceStrictSequence()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);
            var sourceContext = CreateSourceContext(
                sourceActorId: 7,
                projectileConfigId: 301,
                sourceContextId: 500L,
                rootContextId: 400L);
            var hit = CreateHitEvent();

            var draft1 = MobaProjectileHitDiagnosticProducer.CreateProjectileHitDraft(in hit, 11, in sourceContext);
            var draft2 = MobaProjectileHitDiagnosticProducer.CreateProjectileHitDraft(in hit, 12, in sourceContext);

            collector.TryCollect(in draft1);
            collector.TryCollect(in draft2);

            Assert.That(collector.LastSequence, Is.EqualTo(2));
            Assert.That(collector.Store.Count, Is.EqualTo(2));
        }
    }
}

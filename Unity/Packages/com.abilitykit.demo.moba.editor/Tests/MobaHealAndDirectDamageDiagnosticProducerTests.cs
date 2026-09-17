using AbilityKit.Demo.Moba.Services;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    /// <summary>
    /// 验证 Heal 与直接伤害 Producer（MobaDamageService）的诊断草稿生成与采集。
    /// </summary>
    public sealed class MobaHealAndDirectDamageDiagnosticProducerTests
    {
        private BattleDiagnosticSessionScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("session", "world", 1);
        }

        // ===== 直接伤害草稿映射 =====

        [Test]
        public void DirectDamageCommitDraft_RetainsOriginAndAppliedValue()
        {
            var runtime = new MobaSkillCastRuntimeHandle(55, 3, 500);
            var origin = new MobaGameplayOrigin(7, 11, MobaTraceKind.EffectExecution,
                201, 601, 600, 500, 400, runtime);
            var result = new MobaHealthChangeResult(MobaHealthChangeKind.Damage,
                7, 11, 1, 2, 401, 25f, 10f, 100f, 90f, 100f, in origin);

            var draft = MobaDamageService.CreateDirectDamageDraft(in result);

            Assert.That(draft.RootContextId, Is.EqualTo(500));
            Assert.That(draft.ContextId, Is.EqualTo(601));
            Assert.That(draft.SkillRuntime, Is.EqualTo(new BattleDiagnosticRuntimeHandle(55, 3)));
            Assert.That(draft.Summary, Does.Contain("requested=25"));
            Assert.That(draft.Summary, Does.Contain("applied=10"));
        }

        [Test]
        public void CreateDirectDamageDraft_MapsAllFields()
        {
            var draft = MobaDamageService.CreateDirectDamageDraft(
                attackerActorId: 7,
                targetActorId: 11,
                damageType: 2,
                value: 35.5f,
                reasonKind: 1,
                reasonParam: 301,
                targetHp: 64.5f,
                maxHp: 100f);

            Assert.That(draft.Kind, Is.EqualTo(BattleDiagnosticEventKind.Damage));
            Assert.That(draft.Channel, Is.EqualTo(BattleDiagnosticEventChannel.DamageAndHeal));
            Assert.That(draft.Outcome, Is.EqualTo(BattleDiagnosticEventOutcome.Succeeded));
            Assert.That(draft.SourceActorId, Is.EqualTo(7));
            Assert.That(draft.TargetActorId, Is.EqualTo(11));
            Assert.That(draft.ConfigId, Is.EqualTo(301));
            Assert.That(draft.Summary, Does.Contain("35"));
            Assert.That(draft.Summary, Does.Contain("64"));
            Assert.That(draft.Summary, Does.Contain("damageType=2"));
            Assert.That(draft.Summary, Does.Contain("reasonKind=1"));
        }

        [Test]
        public void CreateDirectDamageDraft_WithoutReasonParam_ProducesZeroConfigId()
        {
            var draft = MobaDamageService.CreateDirectDamageDraft(
                attackerActorId: 7,
                targetActorId: 11,
                damageType: 0,
                value: 10f,
                reasonKind: 0,
                reasonParam: 0,
                targetHp: 90f,
                maxHp: 100f);

            Assert.That(draft.ConfigId, Is.EqualTo(0));
        }

        // ===== 治疗草稿映射 =====

        [Test]
        public void HealCommitDraft_RetainsRequestedAppliedAndOrigin()
        {
            var runtime = new MobaSkillCastRuntimeHandle(55, 3, 500);
            var origin = new MobaGameplayOrigin(7, 11, MobaTraceKind.EffectExecution,
                201, 601, 600, 500, 400, runtime);
            var result = new MobaHealthChangeResult(MobaHealthChangeKind.Heal,
                7, 11, 1, 2, 401, 25f, 10f, 90f, 100f, 100f, in origin);

            var draft = MobaDamageService.CreateHealDraft(in result);

            Assert.That(draft.RootContextId, Is.EqualTo(500));
            Assert.That(draft.ContextId, Is.EqualTo(601));
            Assert.That(draft.SkillRuntime, Is.EqualTo(new BattleDiagnosticRuntimeHandle(55, 3)));
            Assert.That(draft.Summary, Does.Contain("requested=25"));
            Assert.That(draft.Summary, Does.Contain("applied=10"));
            Assert.That(draft.Summary, Does.Contain("hp=90->100"));
        }

        [Test]
        public void CreateHealDraft_MapsAllFields()
        {
            var draft = MobaDamageService.CreateHealDraft(
                healerActorId: 7,
                targetActorId: 11,
                healType: 1,
                value: 25.5f,
                reasonKind: 2,
                reasonParam: 401,
                targetHp: 75.5f,
                maxHp: 100f);

            Assert.That(draft.Kind, Is.EqualTo(BattleDiagnosticEventKind.Heal));
            Assert.That(draft.Channel, Is.EqualTo(BattleDiagnosticEventChannel.DamageAndHeal));
            Assert.That(draft.Outcome, Is.EqualTo(BattleDiagnosticEventOutcome.Succeeded));
            Assert.That(draft.SourceActorId, Is.EqualTo(7));
            Assert.That(draft.TargetActorId, Is.EqualTo(11));
            Assert.That(draft.ConfigId, Is.EqualTo(401));
            Assert.That(draft.Summary, Does.Contain("25"));
            Assert.That(draft.Summary, Does.Contain("75"));
            Assert.That(draft.Summary, Does.Contain("healType=1"));
            Assert.That(draft.Summary, Does.Contain("reasonKind=2"));
        }

        [Test]
        public void CreateHealDraft_WithoutReasonParam_ProducesZeroConfigId()
        {
            var draft = MobaDamageService.CreateHealDraft(
                healerActorId: 7,
                targetActorId: 11,
                healType: 0,
                value: 10f,
                reasonKind: 0,
                reasonParam: 0,
                targetHp: 90f,
                maxHp: 100f);

            Assert.That(draft.ConfigId, Is.EqualTo(0));
        }

        // ===== Collector 流转 =====

        [Test]
        public void DirectDamageDraft_FlowsThroughCollector()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);

            var draft = MobaDamageService.CreateDirectDamageDraft(
                attackerActorId: 7,
                targetActorId: 11,
                damageType: 2,
                value: 35.5f,
                reasonKind: 1,
                reasonParam: 301,
                targetHp: 64.5f,
                maxHp: 100f);

            var accepted = collector.TryCollect(in draft);

            Assert.That(accepted, Is.True);
            Assert.That(collector.LastSequence, Is.EqualTo(1));
            Assert.That(collector.Store.Count, Is.EqualTo(1));
        }

        [Test]
        public void HealDraft_FlowsThroughCollector()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);

            var draft = MobaDamageService.CreateHealDraft(
                healerActorId: 7,
                targetActorId: 11,
                healType: 1,
                value: 25.5f,
                reasonKind: 2,
                reasonParam: 401,
                targetHp: 75.5f,
                maxHp: 100f);

            var accepted = collector.TryCollect(in draft);

            Assert.That(accepted, Is.True);
            Assert.That(collector.LastSequence, Is.EqualTo(1));
            Assert.That(collector.Store.Count, Is.EqualTo(1));
        }

        [Test]
        public void DamageAndHealDrafts_RespectDisabledChannel()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);
            // 关闭 DamageAndHeal 通道，仅保留 Skill
            collector.EnabledChannels = BattleDiagnosticEventChannel.Skill;

            var damageDraft = MobaDamageService.CreateDirectDamageDraft(
                attackerActorId: 7,
                targetActorId: 11,
                damageType: 2,
                value: 35.5f,
                reasonKind: 1,
                reasonParam: 301,
                targetHp: 64.5f,
                maxHp: 100f);

            var healDraft = MobaDamageService.CreateHealDraft(
                healerActorId: 7,
                targetActorId: 11,
                healType: 1,
                value: 25.5f,
                reasonKind: 2,
                reasonParam: 401,
                targetHp: 75.5f,
                maxHp: 100f);

            Assert.That(collector.TryCollect(in damageDraft), Is.False);
            Assert.That(collector.TryCollect(in healDraft), Is.False);
            Assert.That(collector.LastSequence, Is.Zero);
            Assert.That(collector.Store.Count, Is.Zero);
        }

        [Test]
        public void DamageAndHealDrafts_ProduceStrictSequence()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);

            var damageDraft = MobaDamageService.CreateDirectDamageDraft(
                attackerActorId: 7,
                targetActorId: 11,
                damageType: 2,
                value: 35.5f,
                reasonKind: 1,
                reasonParam: 301,
                targetHp: 64.5f,
                maxHp: 100f);

            var healDraft = MobaDamageService.CreateHealDraft(
                healerActorId: 7,
                targetActorId: 11,
                healType: 1,
                value: 25.5f,
                reasonKind: 2,
                reasonParam: 401,
                targetHp: 75.5f,
                maxHp: 100f);

            collector.TryCollect(in damageDraft);
            collector.TryCollect(in healDraft);

            Assert.That(collector.LastSequence, Is.EqualTo(2));
            Assert.That(collector.Store.Count, Is.EqualTo(2));
        }
    }
}

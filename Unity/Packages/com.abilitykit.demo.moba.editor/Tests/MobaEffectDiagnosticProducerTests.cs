using AbilityKit.Demo.Moba.Services;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    /// <summary>
    /// 验证 Effect 执行 Producer（MobaEffectExecutionService）的诊断草稿生成与采集。
    /// </summary>
    public sealed class MobaEffectDiagnosticProducerTests
    {
        private BattleDiagnosticSessionScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("session", "world", 1);
        }

        // ===== EffectStarted 草稿映射 =====

        [Test]
        public void CreateEffectStartedDraft_MapsAllFields()
        {
            var draft = MobaEffectDiagnosticProducer.CreateEffectStartedDraft(
                effectConfigId: 801,
                triggerId: 802,
                sourceActorId: 7,
                targetActorId: 9,
                effectContextId: 8001L,
                rootContextId: 500L);

            Assert.That(draft.Kind, Is.EqualTo(BattleDiagnosticEventKind.EffectStarted));
            Assert.That(draft.Channel, Is.EqualTo(BattleDiagnosticEventChannel.Effect));
            Assert.That(draft.Outcome, Is.EqualTo(BattleDiagnosticEventOutcome.None));
            Assert.That(draft.SourceActorId, Is.EqualTo(7));
            Assert.That(draft.TargetActorId, Is.EqualTo(9));
            Assert.That(draft.ConfigId, Is.EqualTo(801));
            Assert.That(draft.RootContextId, Is.EqualTo(500));
            Assert.That(draft.ContextId, Is.EqualTo(8001));
            Assert.That(draft.Summary, Does.Contain("801"));
            Assert.That(draft.Summary, Does.Contain("802"));
            Assert.That(draft.Summary, Does.Contain("8001"));
        }

        [Test]
        public void CreateEffectStartedDraft_WithoutRootContext_FallsBackToEffectContextId()
        {
            var draft = MobaEffectDiagnosticProducer.CreateEffectStartedDraft(
                effectConfigId: 801,
                triggerId: 802,
                sourceActorId: 7,
                targetActorId: 9,
                effectContextId: 9001L,
                rootContextId: 0L);

            Assert.That(draft.ContextId, Is.EqualTo(9001));
            Assert.That(draft.RootContextId, Is.EqualTo(9001));
        }

        // ===== EffectEnded 草稿映射 =====

        [Test]
        public void CreateEffectEndedDraft_Executed_MapsSucceededOutcome()
        {
            var draft = MobaEffectDiagnosticProducer.CreateEffectEndedDraft(
                effectConfigId: 801,
                triggerId: 802,
                sourceActorId: 7,
                targetActorId: 9,
                effectContextId: 8001L,
                rootContextId: 500L,
                executed: true);

            Assert.That(draft.Kind, Is.EqualTo(BattleDiagnosticEventKind.EffectEnded));
            Assert.That(draft.Channel, Is.EqualTo(BattleDiagnosticEventChannel.Effect));
            Assert.That(draft.Outcome, Is.EqualTo(BattleDiagnosticEventOutcome.Succeeded));
            Assert.That(draft.SourceActorId, Is.EqualTo(7));
            Assert.That(draft.TargetActorId, Is.EqualTo(9));
            Assert.That(draft.ConfigId, Is.EqualTo(801));
            Assert.That(draft.RootContextId, Is.EqualTo(500));
            Assert.That(draft.ContextId, Is.EqualTo(8001));
            Assert.That(draft.Summary, Does.Contain("executed=True"));
        }

        [Test]
        public void CreateEffectEndedDraft_NotExecuted_MapsFailedOutcome()
        {
            var draft = MobaEffectDiagnosticProducer.CreateEffectEndedDraft(
                effectConfigId: 801,
                triggerId: 802,
                sourceActorId: 7,
                targetActorId: 9,
                effectContextId: 8001L,
                rootContextId: 500L,
                executed: false);

            Assert.That(draft.Outcome, Is.EqualTo(BattleDiagnosticEventOutcome.Failed));
            Assert.That(draft.Summary, Does.Contain("executed=False"));
        }

        [Test]
        public void CreateEffectEndedDraft_WithoutRootContext_FallsBackToEffectContextId()
        {
            var draft = MobaEffectDiagnosticProducer.CreateEffectEndedDraft(
                effectConfigId: 801,
                triggerId: 802,
                sourceActorId: 7,
                targetActorId: 9,
                effectContextId: 7001L,
                rootContextId: 0L,
                executed: true);

            Assert.That(draft.ContextId, Is.EqualTo(7001));
            Assert.That(draft.RootContextId, Is.EqualTo(7001));
        }

        // ===== TriggerAnalysis 草稿映射 =====

        [Test]
        public void CreateTriggerAnalysisDraft_MapsPayloadAndFailedOutcome()
        {
            var draft = MobaEffectDiagnosticProducer.CreateTriggerAnalysisDraft(
                triggerId: 701,
                contextKind: 2,
                originKind: 3,
                stage: BattleDiagnosticTriggerAnalysisStage.Conditions,
                result: BattleDiagnosticTriggerAnalysisResult.Failed,
                sourceActorId: 7,
                targetActorId: 9,
                contextId: 8001L,
                rootContextId: 500L,
                detailCode: 11,
                currentDepth: 1,
                currentFrameCount: 2,
                currentRootCount: 3,
                currentSameTriggerCount: 4,
                failureKey: "missingMana",
                reason: "Missing mana for trigger.");

            Assert.That(draft.Kind, Is.EqualTo(BattleDiagnosticEventKind.TriggerAnalysis));
            Assert.That(draft.Channel, Is.EqualTo(BattleDiagnosticEventChannel.Effect));
            Assert.That(draft.Outcome, Is.EqualTo(BattleDiagnosticEventOutcome.Failed));
            Assert.That(draft.ConfigId, Is.EqualTo(701));
            Assert.That(draft.RootContextId, Is.EqualTo(500));
            Assert.That(draft.ContextId, Is.EqualTo(8001));
            Assert.That(draft.PayloadVersion, Is.EqualTo(BattleDiagnosticTriggerAnalysisPayload.CurrentSchemaVersion));
            Assert.That(draft.Payload.TryGetTriggerAnalysis(out var payload), Is.True);
            Assert.That(payload.TriggerId, Is.EqualTo(701));
            Assert.That(payload.Stage, Is.EqualTo(BattleDiagnosticTriggerAnalysisStage.Conditions));
            Assert.That(payload.Result, Is.EqualTo(BattleDiagnosticTriggerAnalysisResult.Failed));
            Assert.That(payload.FailureKey, Is.EqualTo("missingMana"));
        }

        [Test]
        public void TriggerAnalysisDraft_FlowsThroughCollector()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);
            var draft = MobaEffectDiagnosticProducer.CreateTriggerAnalysisDraft(
                triggerId: 701,
                contextKind: 2,
                originKind: 3,
                stage: BattleDiagnosticTriggerAnalysisStage.Budget,
                result: BattleDiagnosticTriggerAnalysisResult.Blocked,
                sourceActorId: 7,
                targetActorId: 9,
                contextId: 8001L,
                rootContextId: 500L,
                failureKey: "DepthLimit");

            Assert.That(collector.TryCollect(in draft), Is.True);
            Assert.That(collector.LastSequence, Is.EqualTo(1));
            var snapshot = collector.Store.CaptureEventSnapshot();
            Assert.That(snapshot.Events[0].Payload.TryGetTriggerAnalysis(out var payload), Is.True);
            Assert.That(payload.Result, Is.EqualTo(BattleDiagnosticTriggerAnalysisResult.Blocked));
            Assert.That(payload.FailureKey, Is.EqualTo("DepthLimit"));
        }

        // ===== Collector 流转 =====

        [Test]
        public void EffectStartedDraft_FlowsThroughCollector()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);

            var draft = MobaEffectDiagnosticProducer.CreateEffectStartedDraft(
                effectConfigId: 801, triggerId: 802, sourceActorId: 7, targetActorId: 9,
                effectContextId: 8001L, rootContextId: 500L);
            var accepted = collector.TryCollect(in draft);

            Assert.That(accepted, Is.True);
            Assert.That(collector.LastSequence, Is.EqualTo(1));
            Assert.That(collector.Store.Count, Is.EqualTo(1));
        }

        [Test]
        public void EffectEndedDraft_FlowsThroughCollector()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);

            var draft = MobaEffectDiagnosticProducer.CreateEffectEndedDraft(
                effectConfigId: 801, triggerId: 802, sourceActorId: 7, targetActorId: 9,
                effectContextId: 8001L, rootContextId: 500L, executed: true);
            var accepted = collector.TryCollect(in draft);

            Assert.That(accepted, Is.True);
            Assert.That(collector.LastSequence, Is.EqualTo(1));
            Assert.That(collector.Store.Count, Is.EqualTo(1));
        }

        [Test]
        public void EffectDrafts_RespectDisabledChannel()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);
            collector.EnabledChannels = BattleDiagnosticEventChannel.Skill;

            var startedDraft = MobaEffectDiagnosticProducer.CreateEffectStartedDraft(
                effectConfigId: 801, triggerId: 802, sourceActorId: 7, targetActorId: 9,
                effectContextId: 8001L, rootContextId: 500L);
            var endedDraft = MobaEffectDiagnosticProducer.CreateEffectEndedDraft(
                effectConfigId: 801, triggerId: 802, sourceActorId: 7, targetActorId: 9,
                effectContextId: 8001L, rootContextId: 500L, executed: true);

            Assert.That(collector.TryCollect(in startedDraft), Is.False);
            Assert.That(collector.TryCollect(in endedDraft), Is.False);
            Assert.That(collector.LastSequence, Is.Zero);
            Assert.That(collector.Store.Count, Is.Zero);
        }

        [Test]
        public void EffectStartAndEnd_ProduceStrictSequence()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);

            var startedDraft = MobaEffectDiagnosticProducer.CreateEffectStartedDraft(
                effectConfigId: 801, triggerId: 802, sourceActorId: 7, targetActorId: 9,
                effectContextId: 8001L, rootContextId: 500L);
            var endedDraft = MobaEffectDiagnosticProducer.CreateEffectEndedDraft(
                effectConfigId: 801, triggerId: 802, sourceActorId: 7, targetActorId: 9,
                effectContextId: 8001L, rootContextId: 500L, executed: true);

            collector.TryCollect(in startedDraft);
            collector.TryCollect(in endedDraft);

            Assert.That(collector.LastSequence, Is.EqualTo(2));
            Assert.That(collector.Store.Count, Is.EqualTo(2));
        }
    }
}

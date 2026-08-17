using System;
using System.Reflection;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Diagnostics;
using AbilityKit.Trace;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    /// <summary>
    /// 验证 Effect 执行 Producer（MobaEffectExecutionService）的诊断草稿生成与采集。
    /// </summary>
    public sealed class MobaEffectDiagnosticProducerTests
    {
        private BattleDiagnosticSessionScope _scope;
        private EditorProfiler _profiler;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("session", "world", 1);
            _profiler = new EditorProfiler();
            _profiler.Start();
            ProfilerHub.SetProfiler(_profiler);
        }

        [TearDown]
        public void TearDown()
        {
            ProfilerHub.SetProfiler(null);
            _profiler?.Stop();
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

        [Test]
        public void ActionExecution_Success_RecordsLifecycleAndSampledMetrics()
        {
            var service = CreateEffectService(out var trace, out var diagnostics);
            diagnostics.SetSampleInterval(MobaBattleDiagnosticChannel.TriggerHook, 1);
            BeginEffectScope(service);

            service.EnterActionExecution(0, 901L);
            var actionContextId = service.CurrentActionChain[0];
            service.ExitActionExecution(0, 901L, true);

            Assert.That(trace.TryGetNodeSnapshot(actionContextId, out var action), Is.True);
            Assert.That(action.EndReason, Is.EqualTo((int)TraceLifecycleReason.Completed));
            Assert.That(GetCounter(MobaBattleDiagnosticMetric.EffectActionInvoked), Is.EqualTo(1L));
            Assert.That(GetCounter(MobaBattleDiagnosticMetric.EffectActionSucceeded), Is.EqualTo(1L));
            Assert.That(GetCounter(MobaBattleDiagnosticMetric.EffectActionFailed), Is.Zero);
            Assert.That(HasSamples(MobaBattleDiagnosticMetric.EffectActionDuration + ".ms"), Is.True);
            Assert.That(HasSamples(MobaBattleDiagnosticMetric.EffectActionAllocatedBytes), Is.True);
        }

        [Test]
        public void ActionExecution_FailureWithoutSampling_StillRecordsCounters()
        {
            var service = CreateEffectService(out var trace, out var diagnostics);
            diagnostics.SetChannelEnabled(MobaBattleDiagnosticChannel.TriggerHook, false);
            BeginEffectScope(service);

            service.EnterActionExecution(0, 902L);
            var actionContextId = service.CurrentActionChain[0];
            service.ExitActionExecution(0, 902L, false);

            Assert.That(trace.TryGetNodeSnapshot(actionContextId, out var action), Is.True);
            Assert.That(action.EndReason, Is.EqualTo((int)TraceLifecycleReason.Failed));
            Assert.That(GetCounter(MobaBattleDiagnosticMetric.EffectActionInvoked), Is.EqualTo(1L));
            Assert.That(GetCounter(MobaBattleDiagnosticMetric.EffectActionFailed), Is.EqualTo(1L));
            Assert.That(HasSamples(MobaBattleDiagnosticMetric.EffectActionDuration + ".ms"), Is.False);
            Assert.That(HasSamples(MobaBattleDiagnosticMetric.EffectActionAllocatedBytes), Is.False);
        }

        [Test]
        public void ActionExecution_DuplicateExit_DoesNotDoubleCountResult()
        {
            var service = CreateEffectService(out _, out _);
            BeginEffectScope(service);

            service.EnterActionExecution(0, 903L);
            service.ExitActionExecution(0, 903L, true);
            service.ExitActionExecution(0, 903L, true);

            Assert.That(GetCounter(MobaBattleDiagnosticMetric.EffectActionInvoked), Is.EqualTo(1L));
            Assert.That(GetCounter(MobaBattleDiagnosticMetric.EffectActionSucceeded), Is.EqualTo(1L));
        }

        [Test]
        public void ActionExecution_TraceCleanup_RecordsSingleFailure()
        {
            var service = CreateEffectService(out var trace, out _);
            BeginEffectScope(service);
            service.EnterActionExecution(0, 904L);
            var actionContextId = service.CurrentActionChain[0];

            InvokePrivate(service, "EndCurrentTrace", (int)TraceLifecycleReason.Failed);
            service.ExitActionExecution(0, 904L, false);

            Assert.That(trace.TryGetNodeSnapshot(actionContextId, out var action), Is.True);
            Assert.That(action.EndReason, Is.EqualTo((int)TraceLifecycleReason.Failed));
            Assert.That(GetCounter(MobaBattleDiagnosticMetric.EffectActionInvoked), Is.EqualTo(1L));
            Assert.That(GetCounter(MobaBattleDiagnosticMetric.EffectActionFailed), Is.EqualTo(1L));
            Assert.That(service.CurrentActionChain, Is.Empty);
        }

        private static MobaEffectExecutionService CreateEffectService(
            out MobaTraceRegistry trace,
            out MobaBattleDiagnosticsService diagnostics)
        {
            trace = new MobaTraceRegistry();
            diagnostics = new MobaBattleDiagnosticsService();
            var service = new MobaEffectExecutionService();
            SetMember(service, "Trace", trace);
            SetMember(service, "_diagnostics", diagnostics);
            return service;
        }

        private static void BeginEffectScope(MobaEffectExecutionService service)
        {
            var lineage = new MobaEffectLineageInput(
                EffectContextKind.Skill,
                MobaTraceKind.SkillEffect,
                7,
                9,
                0L,
                0L,
                0L,
                801);
            InvokePrivate(service, "BeginEffectTraceScope", 801, 802, lineage);
        }

        private long GetCounter(string name)
        {
            var snapshot = _profiler.GetSnapshot();
            return snapshot.Counters != null && snapshot.Counters.TryGetValue(name, out var counter)
                ? counter.Value
                : 0L;
        }

        private bool HasSamples(string name)
        {
            var snapshot = _profiler.GetSnapshot();
            return snapshot.Samples != null &&
                   snapshot.Samples.TryGetValue(name, out var samples) &&
                   samples.Count > 0;
        }

        private static object InvokePrivate(object target, string methodName, params object[] args)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(target, args);
        }

        private static void SetMember(object target, string memberName, object value)
        {
            var property = target.GetType().GetProperty(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
            {
                property.SetValue(target, value);
                return;
            }

            var field = target.GetType().GetField(
                memberName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, memberName);
            field.SetValue(target, value);
        }
    }
}

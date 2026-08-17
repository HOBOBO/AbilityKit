using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Trace;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    /// <summary>
    /// 验证 TraceNode Started/Ended Producer（MobaTraceRegistry RegistryEvent 订阅）的诊断草稿生成与采集。
    /// </summary>
    public sealed class MobaTraceDiagnosticProducerTests
    {
        private BattleDiagnosticSessionScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("session", "world", 1);
        }

        // ===== TraceNodeStarted 草稿映射 =====

        [Test]
        public void CreateTraceNodeStartedDraft_MapsAllFields()
        {
            var draft = MobaTraceRegistry.CreateTraceNodeStartedDraft(
                contextId: 700L,
                rootContextId: 500L,
                parentContextId: 500L,
                traceKind: 3,
                configId: 501,
                sourceActorId: 7,
                targetActorId: 21);

            Assert.That(draft.Kind, Is.EqualTo(BattleDiagnosticEventKind.TraceNodeStarted));
            Assert.That(draft.Channel, Is.EqualTo(BattleDiagnosticEventChannel.Skill));
            Assert.That(draft.Outcome, Is.EqualTo(BattleDiagnosticEventOutcome.Succeeded));
            Assert.That(draft.SourceActorId, Is.EqualTo(7));
            Assert.That(draft.TargetActorId, Is.EqualTo(21));
            Assert.That(draft.ConfigId, Is.EqualTo(501));
            Assert.That(draft.RootContextId, Is.EqualTo(500));
            Assert.That(draft.ContextId, Is.EqualTo(700));
            Assert.That(draft.Summary, Does.Contain("traceKind=3"));
            Assert.That(draft.Summary, Does.Contain("configId=501"));
            Assert.That(draft.Summary, Does.Contain("contextId=700"));
            Assert.That(draft.Summary, Does.Contain("parentContextId=500"));
        }

        [Test]
        public void CreateTraceNodeStartedDraft_WithoutRootContext_FallsBackToContextId()
        {
            var draft = MobaTraceRegistry.CreateTraceNodeStartedDraft(
                contextId: 700L,
                rootContextId: 0L,
                parentContextId: 0L,
                traceKind: 1,
                configId: 501,
                sourceActorId: 7,
                targetActorId: 0);

            Assert.That(draft.RootContextId, Is.EqualTo(700));
            Assert.That(draft.ContextId, Is.EqualTo(700));
        }

        // ===== TraceNodeEnded 草稿映射 =====

        [Test]
        public void CreateTraceNodeEndedDraft_MapsAllFields()
        {
            var draft = MobaTraceRegistry.CreateTraceNodeEndedDraft(
                contextId: 700L,
                rootContextId: 500L,
                parentContextId: 500L,
                traceKind: 3,
                configId: 501,
                sourceActorId: 7,
                targetActorId: 21,
                reason: 2);

            Assert.That(draft.Kind, Is.EqualTo(BattleDiagnosticEventKind.TraceNodeEnded));
            Assert.That(draft.Channel, Is.EqualTo(BattleDiagnosticEventChannel.Skill));
            Assert.That(draft.Outcome, Is.EqualTo(BattleDiagnosticEventOutcome.Succeeded));
            Assert.That(draft.SourceActorId, Is.EqualTo(7));
            Assert.That(draft.TargetActorId, Is.EqualTo(21));
            Assert.That(draft.ConfigId, Is.EqualTo(501));
            Assert.That(draft.RootContextId, Is.EqualTo(500));
            Assert.That(draft.ContextId, Is.EqualTo(700));
            Assert.That(draft.Summary, Does.Contain("traceKind=3"));
            Assert.That(draft.Summary, Does.Contain("reason=2"));
        }

        [Test]
        public void CreateTraceNodeEndedDraft_WithoutRootContext_FallsBackToContextId()
        {
            var draft = MobaTraceRegistry.CreateTraceNodeEndedDraft(
                contextId: 700L,
                rootContextId: 0L,
                parentContextId: 0L,
                traceKind: 1,
                configId: 501,
                sourceActorId: 7,
                targetActorId: 0,
                reason: 1);

            Assert.That(draft.RootContextId, Is.EqualTo(700));
        }

        // ===== Collector 流转 =====

        [Test]
        public void TraceNodeStartedDraft_FlowsThroughCollector()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);

            var draft = MobaTraceRegistry.CreateTraceNodeStartedDraft(
                contextId: 700L,
                rootContextId: 500L,
                parentContextId: 500L,
                traceKind: 3,
                configId: 501,
                sourceActorId: 7,
                targetActorId: 21);
            var accepted = collector.TryCollect(in draft);

            Assert.That(accepted, Is.True);
            Assert.That(collector.LastSequence, Is.EqualTo(1));
            Assert.That(collector.Store.Count, Is.EqualTo(1));
        }

        [Test]
        public void TraceNodeEndedDraft_FlowsThroughCollector()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);

            var draft = MobaTraceRegistry.CreateTraceNodeEndedDraft(
                contextId: 700L,
                rootContextId: 500L,
                parentContextId: 500L,
                traceKind: 3,
                configId: 501,
                sourceActorId: 7,
                targetActorId: 21,
                reason: 2);
            var accepted = collector.TryCollect(in draft);

            Assert.That(accepted, Is.True);
            Assert.That(collector.LastSequence, Is.EqualTo(1));
            Assert.That(collector.Store.Count, Is.EqualTo(1));
        }

        [Test]
        public void TraceNodeDrafts_RespectDisabledChannel()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);
            collector.EnabledChannels = BattleDiagnosticEventChannel.TemporaryEntity;

            var startedDraft = MobaTraceRegistry.CreateTraceNodeStartedDraft(
                contextId: 700L,
                rootContextId: 500L,
                parentContextId: 500L,
                traceKind: 3,
                configId: 501,
                sourceActorId: 7,
                targetActorId: 21);
            var endedDraft = MobaTraceRegistry.CreateTraceNodeEndedDraft(
                contextId: 700L,
                rootContextId: 500L,
                parentContextId: 500L,
                traceKind: 3,
                configId: 501,
                sourceActorId: 7,
                targetActorId: 21,
                reason: 1);

            Assert.That(collector.TryCollect(in startedDraft), Is.False);
            Assert.That(collector.TryCollect(in endedDraft), Is.False);
            Assert.That(collector.LastSequence, Is.Zero);
            Assert.That(collector.Store.Count, Is.Zero);
        }

        [Test]
        public void TraceNodeStartAndEnd_ProduceStrictSequence()
        {
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 8);

            var startedDraft = MobaTraceRegistry.CreateTraceNodeStartedDraft(
                contextId: 700L,
                rootContextId: 500L,
                parentContextId: 500L,
                traceKind: 3,
                configId: 501,
                sourceActorId: 7,
                targetActorId: 21);
            var endedDraft = MobaTraceRegistry.CreateTraceNodeEndedDraft(
                contextId: 700L,
                rootContextId: 500L,
                parentContextId: 500L,
                traceKind: 3,
                configId: 501,
                sourceActorId: 7,
                targetActorId: 21,
                reason: 1);

            collector.TryCollect(in startedDraft);
            collector.TryCollect(in endedDraft);

            Assert.That(collector.LastSequence, Is.EqualTo(2));
            Assert.That(collector.Store.Count, Is.EqualTo(2));
        }

        // ===== 端到端：RegistryEvent 订阅触发采集 =====

        private static BattleDiagnosticEventQuery NewQuery()
        {
            return new BattleDiagnosticEventQuery(
                1L,
                BattleDiagnosticFilter.Default,
                new BattleDiagnosticPageRequest(0, 0, BattleDiagnosticPageRequest.DefaultPageSize));
        }

        [Test]
        public void RegistryEvent_RootCreated_CollectsTraceNodeStarted()
        {
            var registry = new MobaTraceRegistry();
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 16);
            registry.AttachDiagnosticCollector(collector);

            var contextId = registry.CreateRootContext(
                MobaTraceKind.SkillCast,
                501,
                7,
                21);

            Assert.That(contextId, Is.Not.Zero);
            Assert.That(collector.Store.Count, Is.EqualTo(1));
            Assert.That(collector.LastSequence, Is.EqualTo(1));

            var result = collector.Store.Query(NewQuery());
            Assert.That(result.Items.Count, Is.EqualTo(1));
            var evt = result.Items[0];
            Assert.That(evt.Kind, Is.EqualTo(BattleDiagnosticEventKind.TraceNodeStarted));
            Assert.That(evt.SourceActorId, Is.EqualTo(7));
            Assert.That(evt.TargetActorId, Is.EqualTo(21));
            Assert.That(evt.ConfigId, Is.EqualTo(501));
            Assert.That(evt.ContextId, Is.EqualTo(contextId));
            Assert.That(evt.RootContextId, Is.EqualTo(contextId));
        }

        [Test]
        public void RegistryEvent_ChildCreated_CollectsTraceNodeStarted()
        {
            var registry = new MobaTraceRegistry();
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 16);
            registry.AttachDiagnosticCollector(collector);

            var rootId = registry.CreateRootContext(MobaTraceKind.SkillCast, 501, 7, 21);
            var childId = registry.CreateChildContext(rootId, MobaTraceKind.SkillPhase, 502, 7, 21);

            Assert.That(childId, Is.Not.Zero);
            Assert.That(collector.Store.Count, Is.EqualTo(2));

            var result = collector.Store.Query(NewQuery());
            // 第二条是子节点 Started
            var childEvt = result.Items[1];
            Assert.That(childEvt.Kind, Is.EqualTo(BattleDiagnosticEventKind.TraceNodeStarted));
            Assert.That(childEvt.ContextId, Is.EqualTo(childId));
            Assert.That(childEvt.RootContextId, Is.EqualTo(rootId));
            Assert.That(childEvt.ConfigId, Is.EqualTo(502));
        }

        [Test]
        public void ValidateChainDetailed_ValidTree_ReturnsValid()
        {
            var registry = new MobaTraceRegistry();
            var rootId = registry.CreateRootContext(MobaTraceKind.SkillCast, 501, 7, 21);
            var childId = registry.CreateChildContext(rootId, MobaTraceKind.SkillPhase, 502, 7, 21);
            registry.CreateChildContext(childId, MobaTraceKind.EffectExecution, 503, 7, 21);

            var result = registry.ValidateChainDetailed(rootId);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Error, Is.EqualTo(MobaTraceValidationError.None));
            Assert.That(registry.ValidateChain(rootId), Is.True);
        }

        [Test]
        public void ValidateChainDetailed_MissingRoot_ReturnsStableError()
        {
            var registry = new MobaTraceRegistry();

            var result = registry.ValidateChainDetailed(999L);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Error, Is.EqualTo(MobaTraceValidationError.RootNotFound));
            Assert.That(result.ContextId, Is.EqualTo(999L));
        }

        [Test]
        public void ValidateChainDetailed_ChildUsedAsRoot_ReturnsInvalidRoot()
        {
            var registry = new MobaTraceRegistry();
            var rootId = registry.CreateRootContext(MobaTraceKind.SkillCast, 501, 7, 21);
            var childId = registry.CreateChildContext(rootId, MobaTraceKind.SkillPhase, 502, 7, 21);

            var result = registry.ValidateChainDetailed(childId);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Error, Is.EqualTo(MobaTraceValidationError.InvalidRoot));
            Assert.That(result.ContextId, Is.EqualTo(childId));
        }

        [Test]
        public void RegistryEvent_NodeEnded_CollectsTraceNodeEnded()
        {
            var registry = new MobaTraceRegistry();
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 16);
            registry.AttachDiagnosticCollector(collector);

            var contextId = registry.CreateRootContext(MobaTraceKind.SkillCast, 501, 7, 21);
            var ended = registry.EndContext(contextId, TraceLifecycleReason.Completed);

            Assert.That(ended, Is.True);
            Assert.That(collector.Store.Count, Is.EqualTo(2));

            var result = collector.Store.Query(NewQuery());
            var endedEvt = result.Items[1];
            Assert.That(endedEvt.Kind, Is.EqualTo(BattleDiagnosticEventKind.TraceNodeEnded));
            Assert.That(endedEvt.ContextId, Is.EqualTo(contextId));
            Assert.That(endedEvt.Summary, Does.Contain("reason=" + (int)TraceLifecycleReason.Completed));
        }

        [Test]
        public void RegistryEvent_FullLifecycle_ProducesStartThenEnd()
        {
            var registry = new MobaTraceRegistry();
            var collector = new MobaBattleDiagnosticEventCollector(_scope, 16);
            registry.AttachDiagnosticCollector(collector);

            var contextId = registry.CreateRootContext(MobaTraceKind.SkillCast, 501, 7, 21);
            registry.EndContext(contextId, TraceLifecycleReason.Cancelled);

            Assert.That(collector.Store.Count, Is.EqualTo(2));

            var result = collector.Store.Query(NewQuery());
            Assert.That(result.Items[0].Kind, Is.EqualTo(BattleDiagnosticEventKind.TraceNodeStarted));
            Assert.That(result.Items[1].Kind, Is.EqualTo(BattleDiagnosticEventKind.TraceNodeEnded));
            Assert.That(collector.LastSequence, Is.EqualTo(2));
        }
    }
}

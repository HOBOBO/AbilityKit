using System;
using AbilityKit.Demo.Moba.Services;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaBattleDiagnosticEventCollectorTests
    {
        private BattleDiagnosticSessionScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("session", "world", 3);
        }

        [Test]
        public void DisabledChannel_DoesNotAllocateSequenceOrWrite()
        {
            var collector = CreateCollector();
            collector.EnabledChannels = BattleDiagnosticEventChannel.DamageAndHeal;
            var draft = SkillDraft();

            var accepted = collector.TryCollect(in draft);

            Assert.That(accepted, Is.False);
            Assert.That(collector.LastSequence, Is.Zero);
            Assert.That(collector.Store.Count, Is.Zero);
            Assert.That(collector.CollectFailureCount, Is.Zero);
            Assert.That(collector.LastCollectError, Is.Empty);
        }

        [Test]
        public void AcceptedEvents_UseScopeFrameTimestampAndStrictSequence()
        {
            var frame = 17;
            var timestamp = 100L;
            var collector = new MobaBattleDiagnosticEventCollector(
                _scope,
                4,
                () => frame,
                () => timestamp++);
            var first = SkillDraft();
            var second = new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.Damage,
                BattleDiagnosticEventChannel.DamageAndHeal);

            Assert.That(collector.TryCollect(in first), Is.True);
            frame = 18;
            Assert.That(collector.TryCollect(in second), Is.True);

            var items = QueryAll(collector);
            Assert.That(items[0].Scope, Is.EqualTo(_scope));
            Assert.That(items[0].Frame, Is.EqualTo(17));
            Assert.That(items[0].Sequence, Is.EqualTo(1));
            Assert.That(items[0].MonotonicTimestamp, Is.EqualTo(100));
            Assert.That(items[1].Frame, Is.EqualTo(18));
            Assert.That(items[1].Sequence, Is.EqualTo(2));
            Assert.That(items[1].MonotonicTimestamp, Is.EqualTo(101));
        }

        [Test]
        public void FrozenStore_RejectionDoesNotConsumeSequence()
        {
            var collector = CreateCollector();
            var draft = SkillDraft();
            collector.Store.SetFrozen(true);

            Assert.That(collector.TryCollect(in draft), Is.False);
            Assert.That(collector.LastSequence, Is.Zero);
            Assert.That(collector.CollectFailureCount, Is.EqualTo(1));
            StringAssert.Contains("rejected", collector.LastCollectError);

            collector.Store.SetFrozen(false);
            Assert.That(collector.TryCollect(in draft), Is.True);
            Assert.That(QueryAll(collector)[0].Sequence, Is.EqualTo(1));
            Assert.That(collector.CollectFailureCount, Is.EqualTo(1));
            Assert.That(collector.LastCollectError, Is.Empty);
        }

        [Test]
        public void ProviderException_IsIsolatedAndDoesNotConsumeSequence()
        {
            var collector = new MobaBattleDiagnosticEventCollector(
                _scope,
                4,
                () => throw new InvalidOperationException("frame failed"),
                () => 1L);
            var draft = SkillDraft();

            Assert.DoesNotThrow(() => collector.TryCollect(in draft));
            Assert.That(collector.TryCollect(in draft), Is.False);
            Assert.That(collector.LastSequence, Is.Zero);
            Assert.That(collector.Store.Count, Is.Zero);
            Assert.That(collector.CollectFailureCount, Is.EqualTo(2));
            StringAssert.Contains("frame failed", collector.LastCollectError);
        }

        [TestCase(MobaSkillTriggering.Events.CastStart, BattleDiagnosticEventKind.SkillRuntimeStarted, BattleDiagnosticEventOutcome.None)]
        [TestCase(MobaSkillTriggering.Events.CastComplete, BattleDiagnosticEventKind.SkillRuntimeEnded, BattleDiagnosticEventOutcome.Succeeded)]
        [TestCase(MobaSkillTriggering.Events.CastFail, BattleDiagnosticEventKind.SkillRuntimeEnded, BattleDiagnosticEventOutcome.Failed)]
        [TestCase(MobaSkillTriggering.Events.CastInterrupt, BattleDiagnosticEventKind.SkillRuntimeEnded, BattleDiagnosticEventOutcome.Interrupted)]
        public void SkillProducer_MapsLifecycleAndCorrelation(
            string eventId,
            BattleDiagnosticEventKind expectedKind,
            BattleDiagnosticEventOutcome expectedOutcome)
        {
            var context = new SkillCastContext
            {
                SkillId = 101,
                CasterActorId = 7,
                TargetActorId = 9,
                SourceContextId = 500,
                RuntimeHandle = new MobaSkillCastRuntimeHandle(42, 2, 400),
                FailReason = "reason"
            };

            var created = MobaSkillTriggering.TryCreateDiagnosticDraft(eventId, context, out var draft);

            Assert.That(created, Is.True);
            Assert.That(draft.Kind, Is.EqualTo(expectedKind));
            Assert.That(draft.Outcome, Is.EqualTo(expectedOutcome));
            Assert.That(draft.ConfigId, Is.EqualTo(101));
            Assert.That(draft.SourceActorId, Is.EqualTo(7));
            Assert.That(draft.TargetActorId, Is.EqualTo(9));
            Assert.That(draft.RootContextId, Is.EqualTo(400));
            Assert.That(draft.ContextId, Is.EqualTo(500));
            Assert.That(draft.SkillRuntime, Is.EqualTo(new BattleDiagnosticRuntimeHandle(42, 2)));
            Assert.That(draft.Summary, Does.Contain("reason"));
        }

        [Test]
        public void NarrowInterfaces_ExposeOnlyOwnedResponsibilities()
        {
            var sink = typeof(IMobaBattleDiagnosticEventSink);
            Assert.That(sink.GetProperty("Scope"), Is.Null);
            Assert.That(sink.GetProperty("EnabledChannels"), Is.Null);
            Assert.That(sink.GetMethod("TryCollect"), Is.Not.Null);

            var eventReadStore = typeof(IBattleDiagnosticEventReadStore);
            Assert.That(eventReadStore.GetMethod("Query"), Is.Not.Null);
            Assert.That(eventReadStore.GetMethod("TryAppend"), Is.Null);
            Assert.That(eventReadStore.GetMethod("SetFrozen"), Is.Null);
            Assert.That(eventReadStore.GetMethod("Clear"), Is.Null);

            var stateReadStore = typeof(IBattleDiagnosticStateReadStore);
            Assert.That(stateReadStore.GetMethod("QueryWorld"), Is.Not.Null);
            Assert.That(stateReadStore.GetMethod("QueryActors"), Is.Not.Null);
            Assert.That(stateReadStore.GetMethod("TryReplaceSnapshot"), Is.Null);
            Assert.That(stateReadStore.GetMethod("SetFrozen"), Is.Null);
            Assert.That(stateReadStore.GetMethod("Clear"), Is.Null);
        }

        [Test]
        public void CollectorPorts_ShareCollectorAndControlBothStores()
        {
            var collector = CreateCollector();
            var ports = new MobaBattleDiagnosticCollectorPorts(collector);
            var sink = (IMobaBattleDiagnosticEventSink)ports;
            var control = (IMobaBattleDiagnosticCaptureControl)ports;
            var eventStore = (IBattleDiagnosticEventReadStore)ports;
            var stateStore = (IBattleDiagnosticStateStore)ports;
            var stateReadStore = (IBattleDiagnosticStateReadStore)ports;
            var draft = SkillDraft();

            Assert.That(sink.TryCollect(in draft), Is.True);
            Assert.That(eventStore.Revision, Is.EqualTo(1));
            Assert.That(control.LastSequence, Is.EqualTo(1));

            var world = new BattleDiagnosticWorldSummary(_scope, 12, 99L, 0, 0, 0);
            Assert.That(stateStore.TryReplaceSnapshot(
                world,
                new BattleDiagnosticActorSummary[0]), Is.True);
            Assert.That(stateReadStore.Revision, Is.EqualTo(1));

            control.SetFrozen(true);
            Assert.That(collector.Store.IsFrozen, Is.True);
            Assert.That(collector.StateStore.IsFrozen, Is.True);
            Assert.That(sink.TryCollect(in draft), Is.False);
            Assert.That(stateStore.TryReplaceWorld(world), Is.False);

            control.Clear();
            Assert.That(collector.Store.Count, Is.Zero);
            Assert.That(collector.StateStore.ActorCount, Is.Zero);
        }

        [Test]
        public void DamageProducer_MapsOriginCorrelation()
        {
            var runtime = new MobaSkillCastRuntimeHandle(55, 3, 700);
            var origin = new MobaGameplayOrigin(
                7,
                9,
                MobaTraceKind.EffectExecution,
                201,
                601,
                600,
                500,
                400,
                runtime);
            var result = new DamageResult
            {
                AttackerActorId = 7,
                TargetActorId = 9,
                ReasonParam = 301,
                Value = 12.5f,
                TargetHp = 87.5f
            };
            result.SetOrigin(in origin);

            var draft = DamagePipelineService.CreateDiagnosticDraft(result);

            Assert.That(draft.Kind, Is.EqualTo(BattleDiagnosticEventKind.Damage));
            Assert.That(draft.Channel, Is.EqualTo(BattleDiagnosticEventChannel.DamageAndHeal));
            Assert.That(draft.SourceActorId, Is.EqualTo(7));
            Assert.That(draft.TargetActorId, Is.EqualTo(9));
            Assert.That(draft.ConfigId, Is.EqualTo(301));
            Assert.That(draft.RootContextId, Is.EqualTo(500));
            Assert.That(draft.ContextId, Is.EqualTo(601));
            Assert.That(draft.SkillRuntime, Is.EqualTo(new BattleDiagnosticRuntimeHandle(55, 3)));
            Assert.That(draft.AttackId, Is.Zero);
        }

        private MobaBattleDiagnosticEventCollector CreateCollector()
        {
            return new MobaBattleDiagnosticEventCollector(_scope, 4, () => 12, () => 99L);
        }

        private static MobaBattleDiagnosticEventDraft SkillDraft()
        {
            return new MobaBattleDiagnosticEventDraft(
                BattleDiagnosticEventKind.SkillRuntimeStarted,
                BattleDiagnosticEventChannel.Skill);
        }

        private static System.Collections.Generic.IReadOnlyList<BattleDiagnosticEvent> QueryAll(
            MobaBattleDiagnosticEventCollector collector)
        {
            return collector.Store.Query(new BattleDiagnosticEventQuery(
                1,
                BattleDiagnosticFilter.Default,
                new BattleDiagnosticPageRequest(0, 0, 100))).Items;
        }
    }
}

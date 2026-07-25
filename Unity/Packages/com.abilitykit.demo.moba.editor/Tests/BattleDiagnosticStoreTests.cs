using System;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class BattleDiagnosticStoreTests
    {
        private BattleDiagnosticSessionScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("session", "world", 1);
        }

        [Test]
        public void SessionInfo_Supports_RequiresAllRequestedCapabilities()
        {
            var info = new BattleDiagnosticSessionInfo(
                _scope,
                "Local Battle",
                "build",
                1,
                TimeSpan.TicksPerSecond,
                BattleDiagnosticCapabilities.Events | BattleDiagnosticCapabilities.Trace,
                BattleDiagnosticConnectionState.Connected,
                BattleDiagnosticCaptureState.Capturing);

            Assert.That(info.IsValid, Is.True);
            Assert.That(info.Supports(BattleDiagnosticCapabilities.Events), Is.True);
            Assert.That(
                info.Supports(BattleDiagnosticCapabilities.Events | BattleDiagnosticCapabilities.Trace),
                Is.True);
            Assert.That(info.Supports(BattleDiagnosticCapabilities.ActorState), Is.False);
        }

        [Test]
        public void RuntimeHandle_GenerationParticipatesInIdentity()
        {
            var firstGeneration = new BattleDiagnosticRuntimeHandle(42, 1);
            var nextGeneration = new BattleDiagnosticRuntimeHandle(42, 2);

            Assert.That(firstGeneration.IsValid, Is.True);
            Assert.That(firstGeneration, Is.Not.EqualTo(nextGeneration));
        }

        [Test]
        public void RingStore_RejectsForeignScopeAndNonIncreasingSequence()
        {
            var store = new BattleDiagnosticEventRingStore(_scope, 4);
            var foreignScope = new BattleDiagnosticSessionScope("session", "world", 2);

            Assert.That(store.TryAppend(Event(_scope, 1, 1)), Is.True);
            Assert.That(store.TryAppend(Event(_scope, 2, 1)), Is.False);
            Assert.That(store.TryAppend(Event(foreignScope, 2, 2)), Is.False);
            Assert.That(store.Count, Is.EqualTo(1));
            Assert.That(store.Metrics.RejectedCount, Is.EqualTo(2));
        }

        [Test]
        public void RingStore_ExceedingCapacity_EvictsOldestAndKeepsSequenceOrder()
        {
            var store = new BattleDiagnosticEventRingStore(_scope, 2);
            store.TryAppend(Event(_scope, 10, 1));
            store.TryAppend(Event(_scope, 20, 2));
            store.TryAppend(Event(_scope, 30, 3));

            var result = store.Query(Query(1, 0, 10));

            Assert.That(result.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Ready));
            Assert.That(result.Items.Count, Is.EqualTo(2));
            Assert.That(result.Items[0].Sequence, Is.EqualTo(2));
            Assert.That(result.Items[1].Sequence, Is.EqualTo(3));
            Assert.That(store.Metrics.EvictedCount, Is.EqualTo(1));
        }

        [Test]
        public void RingStore_Frozen_RejectsWritesWithoutChangingRevision()
        {
            var store = new BattleDiagnosticEventRingStore(_scope, 2);
            store.TryAppend(Event(_scope, 10, 1));
            var revision = store.Revision;
            store.SetFrozen(true);

            var accepted = store.TryAppend(Event(_scope, 20, 2));

            Assert.That(accepted, Is.False);
            Assert.That(store.Revision, Is.EqualTo(revision));
            Assert.That(store.Count, Is.EqualTo(1));
            Assert.That(store.Metrics.IsFrozen, Is.True);
        }

        [Test]
        public void RingStore_Query_AppliesActorChannelAndTextFilters()
        {
            var store = new BattleDiagnosticEventRingStore(_scope, 8);
            store.TryAppend(Event(
                _scope,
                10,
                1,
                BattleDiagnosticEventChannel.DamageAndHeal,
                sourceActorId: 7,
                targetActorId: 9,
                summary: "Fire Ball hit"));
            store.TryAppend(Event(
                _scope,
                11,
                2,
                BattleDiagnosticEventChannel.Skill,
                sourceActorId: 7,
                summary: "Skill ended"));

            var filter = new BattleDiagnosticFilter(
                    new BattleDiagnosticFrameFilter(BattleDiagnosticFrames.Invalid, BattleDiagnosticFrames.Invalid),
                    BattleDiagnosticEventChannel.DamageAndHeal)
                .WithActor(9, BattleDiagnosticActorRelation.Target)
                .WithSearchText("fire ball");

            var result = store.Query(new BattleDiagnosticEventQuery(
                1,
                filter,
                new BattleDiagnosticPageRequest(0, 0, 10)));

            Assert.That(result.Items.Count, Is.EqualTo(1));
            Assert.That(result.Items[0].Sequence, Is.EqualTo(1));
        }

        [Test]
        public void RingStore_QueryNewestFirst_AppliesRecentWindowBeforePaging()
        {
            var store = new BattleDiagnosticEventRingStore(_scope, 8);
            store.TryAppend(Event(_scope, 10, 1));
            store.TryAppend(Event(_scope, 20, 2));
            store.TryAppend(Event(_scope, 30, 3));
            store.TryAppend(Event(_scope, 40, 4));

            var result = store.Query(new BattleDiagnosticEventQuery(
                1,
                BattleDiagnosticFilter.Default,
                new BattleDiagnosticPageRequest(0, 0, 2),
                newestFirst: true,
                recentFrameCount: 15));

            Assert.That(result.Items.Count, Is.EqualTo(2));
            Assert.That(result.Items[0].Sequence, Is.EqualTo(4));
            Assert.That(result.Items[1].Sequence, Is.EqualTo(3));
            Assert.That(result.Status.HasMore, Is.False);
        }

        [TestCase("damage")]
        [TestCase("9001")]
        [TestCase("7001")]
        public void RingStore_TextSearch_MatchesKindAndCorrelationIds(string searchText)
        {
            var store = new BattleDiagnosticEventRingStore(_scope, 4);
            store.TryAppend(Event(
                _scope,
                20,
                1,
                BattleDiagnosticEventChannel.DamageAndHeal,
                sourceActorId: 7,
                targetActorId: 9,
                kind: BattleDiagnosticEventKind.Damage,
                configId: 9001,
                rootContextId: 7001));

            var result = store.Query(new BattleDiagnosticEventQuery(
                1,
                BattleDiagnosticFilter.Default.WithSearchText(searchText),
                new BattleDiagnosticPageRequest(0, 0, 10)));

            Assert.That(result.Items.Count, Is.EqualTo(1));
            Assert.That(result.Items[0].Sequence, Is.EqualTo(1));
        }

        [Test]
        public void RingStore_Query_AppliesTriggerAnalysisFiltersAndSearch()
        {
            var store = new BattleDiagnosticEventRingStore(_scope, 8);
            var failedCondition = new BattleDiagnosticTriggerAnalysisPayload(
                7001,
                contextKind: 2,
                originKind: 3,
                BattleDiagnosticTriggerAnalysisStage.Conditions,
                BattleDiagnosticTriggerAnalysisResult.Failed,
                detailCode: 11,
                failureKey: "missingMana",
                reason: "Missing mana for trigger.");
            var passedPlan = new BattleDiagnosticTriggerAnalysisPayload(
                7002,
                contextKind: 2,
                originKind: 4,
                BattleDiagnosticTriggerAnalysisStage.Plan,
                BattleDiagnosticTriggerAnalysisResult.Passed);

            store.TryAppend(TriggerEvent(_scope, 20, 1, in failedCondition));
            store.TryAppend(TriggerEvent(_scope, 21, 2, in passedPlan));

            var filter = BattleDiagnosticFilter.Default
                .WithTriggerAnalysis(
                    BattleDiagnosticTriggerAnalysisStage.Conditions,
                    BattleDiagnosticTriggerAnalysisResult.Failed,
                    contextKind: 2,
                    originKind: 3)
                .WithSearchText("missing mana");
            var result = store.Query(new BattleDiagnosticEventQuery(
                1,
                filter,
                new BattleDiagnosticPageRequest(0, 0, 10)));

            Assert.That(result.Items.Count, Is.EqualTo(1));
            Assert.That(result.Items[0].Sequence, Is.EqualTo(1));
        }

        [Test]
        public void TriggerAnalysisPayload_RoundTripsEveryFieldAndRejectsWrongKind()
        {
            var trigger = new BattleDiagnosticTriggerAnalysisPayload(
                7001,
                contextKind: 2,
                originKind: 3,
                BattleDiagnosticTriggerAnalysisStage.Budget,
                BattleDiagnosticTriggerAnalysisResult.Blocked,
                detailCode: 4,
                currentDepth: 5,
                currentFrameCount: 6,
                currentRootCount: 7,
                currentSameTriggerCount: 8,
                failureKey: "DepthLimit",
                reason: "Budget blocked trigger.");
            var payload = BattleDiagnosticEventPayload.FromTriggerAnalysis(in trigger);

            Assert.That(payload.Kind, Is.EqualTo(BattleDiagnosticPayloadKind.TriggerAnalysis));
            Assert.That(payload.SchemaVersion, Is.EqualTo(BattleDiagnosticTriggerAnalysisPayload.CurrentSchemaVersion));
            Assert.That(payload.TryGetTriggerAnalysis(out var restored), Is.True);
            Assert.That(restored, Is.EqualTo(trigger));
            Assert.Throws<System.ArgumentException>(() => new BattleDiagnosticEvent(
                _scope,
                10,
                1,
                100L,
                BattleDiagnosticEventKind.EffectStarted,
                BattleDiagnosticEventChannel.Effect,
                BattleDiagnosticEventOutcome.Failed,
                payload: payload));
        }

        [Test]
        public void RingStore_NextPage_UsesOriginalRevisionWhileNewEventsArrive()
        {
            var store = new BattleDiagnosticEventRingStore(_scope, 8);
            store.TryAppend(Event(_scope, 10, 1));
            store.TryAppend(Event(_scope, 20, 2));
            store.TryAppend(Event(_scope, 30, 3));

            var first = store.Query(Query(10, 0, 2));
            store.TryAppend(Event(_scope, 40, 4));
            var second = store.Query(new BattleDiagnosticEventQuery(
                11,
                BattleDiagnosticFilter.Default,
                new BattleDiagnosticPageRequest(first.Status.StoreRevision, 2, 2)));

            Assert.That(first.Items.Count, Is.EqualTo(2));
            Assert.That(first.Status.HasMore, Is.True);
            Assert.That(second.Status.StoreRevision, Is.EqualTo(first.Status.StoreRevision));
            Assert.That(second.Items.Count, Is.EqualTo(1));
            Assert.That(second.Items[0].Sequence, Is.EqualTo(3));
        }

        [Test]
        public void RingStore_QueryingDiscardedRevision_ReturnsEvicted()
        {
            var store = new BattleDiagnosticEventRingStore(_scope, 8, 1);
            store.TryAppend(Event(_scope, 10, 1));
            var first = store.Query(Query(1, 0, 1));

            store.TryAppend(Event(_scope, 20, 2));
            store.Query(Query(2, 0, 1));

            store.TryAppend(Event(_scope, 30, 3));
            store.Query(Query(3, 0, 1));

            var stale = store.Query(new BattleDiagnosticEventQuery(
                4,
                BattleDiagnosticFilter.Default,
                new BattleDiagnosticPageRequest(first.Status.StoreRevision, 0, 1)));

            Assert.That(stale.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Unavailable));
            Assert.That(stale.Status.Availability, Is.EqualTo(BattleDiagnosticDataAvailability.Evicted));
            Assert.That(stale.Items, Is.Empty);
        }

        [Test]
        public void RingStore_Snapshot_CopiesFullCapacityWithoutPaging()
        {
            var store = new BattleDiagnosticEventRingStore(
                _scope,
                BattleDiagnosticEventRingStore.DefaultCapacity);
            for (var sequence = 1; sequence <= BattleDiagnosticEventRingStore.DefaultCapacity; sequence++)
            {
                Assert.That(store.TryAppend(Event(_scope, sequence, sequence)), Is.True);
            }

            var snapshot = store.CaptureEventSnapshot();
            store.TryAppend(Event(
                _scope,
                BattleDiagnosticEventRingStore.DefaultCapacity + 1,
                BattleDiagnosticEventRingStore.DefaultCapacity + 1L));

            Assert.That(snapshot.Events.Count, Is.EqualTo(BattleDiagnosticEventRingStore.DefaultCapacity));
            Assert.That(snapshot.FirstSequence, Is.EqualTo(1));
            Assert.That(snapshot.LastSequence, Is.EqualTo(BattleDiagnosticEventRingStore.DefaultCapacity));
            Assert.That(snapshot.Revision, Is.EqualTo(BattleDiagnosticEventRingStore.DefaultCapacity));
            Assert.That(snapshot.Metrics.Count, Is.EqualTo(BattleDiagnosticEventRingStore.DefaultCapacity));
            Assert.That(snapshot.Metrics.EvictedCount, Is.Zero);
            Assert.That(store.Metrics.EvictedCount, Is.EqualTo(1));
        }

        [Test]
        public void RingStore_Snapshot_RemainsUnchangedAfterClear()
        {
            var store = new BattleDiagnosticEventRingStore(_scope, 4);
            store.TryAppend(Event(_scope, 10, 1));
            store.TryAppend(Event(_scope, 20, 2));

            var snapshot = store.CaptureEventSnapshot();
            store.Clear();

            Assert.That(snapshot.Events.Count, Is.EqualTo(2));
            Assert.That(snapshot.Events[0].Sequence, Is.EqualTo(1));
            Assert.That(snapshot.Events[1].Sequence, Is.EqualTo(2));
            Assert.That(snapshot.Metrics.Count, Is.EqualTo(2));
            Assert.That(store.Count, Is.Zero);
        }

        [Test]
        public void QueryResult_CopiesInputList()
        {
            var source = new[] { Event(_scope, 10, 1) };

            var result = BattleDiagnosticQueryResult<BattleDiagnosticEvent>.FromItems(1, 1, source, false);
            source[0] = Event(_scope, 20, 2);

            Assert.That(result.Items[0].Sequence, Is.EqualTo(1));
        }

        [Test]
        public void Event_RejectsStructuredPayloadVersionMismatch()
        {
            var sync = new BattleDiagnosticSyncSnapshotReceivedPayload(10, 20U);
            var payload = BattleDiagnosticEventPayload.FromSyncSnapshotReceived(in sync);

            Assert.Throws<System.ArgumentException>(() => new BattleDiagnosticEvent(
                _scope,
                10,
                1,
                100L,
                BattleDiagnosticEventKind.Sync,
                BattleDiagnosticEventChannel.Sync,
                BattleDiagnosticEventOutcome.Succeeded,
                payloadVersion: 2,
                payload: payload));
        }

        [Test]
        public void Event_RejectsPayloadForIncompatibleEventKind()
        {
            var sync = new BattleDiagnosticSyncSnapshotReceivedPayload(10, 20U);
            var payload = BattleDiagnosticEventPayload.FromSyncSnapshotReceived(in sync);

            Assert.Throws<System.ArgumentException>(() => new BattleDiagnosticEvent(
                _scope,
                10,
                1,
                100L,
                BattleDiagnosticEventKind.Warning,
                BattleDiagnosticEventChannel.WarningAndException,
                BattleDiagnosticEventOutcome.None,
                payload: payload));
        }

        [Test]
        public void Event_WithoutStructuredPayload_RemainsBackwardCompatible()
        {
            var diagnosticEvent = new BattleDiagnosticEvent(
                _scope,
                10,
                1,
                100L,
                BattleDiagnosticEventKind.Warning,
                BattleDiagnosticEventChannel.WarningAndException,
                BattleDiagnosticEventOutcome.None,
                payloadVersion: 3,
                summary: "legacy");

            Assert.That(diagnosticEvent.Payload.HasValue, Is.False);
            Assert.That(diagnosticEvent.PayloadVersion, Is.EqualTo(3));
            Assert.That(diagnosticEvent.Summary, Is.EqualTo("legacy"));
        }

        private BattleDiagnosticEventQuery Query(long requestId, int offset, int limit)
        {
            return new BattleDiagnosticEventQuery(
                requestId,
                BattleDiagnosticFilter.Default,
                new BattleDiagnosticPageRequest(0, offset, limit));
        }

        private static BattleDiagnosticEvent TriggerEvent(
            BattleDiagnosticSessionScope scope,
            int frame,
            long sequence,
            in BattleDiagnosticTriggerAnalysisPayload triggerPayload)
        {
            var payload = BattleDiagnosticEventPayload.FromTriggerAnalysis(in triggerPayload);
            return new BattleDiagnosticEvent(
                scope,
                frame,
                sequence,
                frame * 100L,
                BattleDiagnosticEventKind.TriggerAnalysis,
                BattleDiagnosticEventChannel.Effect,
                triggerPayload.Result == BattleDiagnosticTriggerAnalysisResult.Passed
                    ? BattleDiagnosticEventOutcome.Succeeded
                    : BattleDiagnosticEventOutcome.Failed,
                sourceActorId: 7,
                targetActorId: 9,
                configId: triggerPayload.TriggerId,
                rootContextId: 7000,
                contextId: 7001,
                payloadVersion: BattleDiagnosticTriggerAnalysisPayload.CurrentSchemaVersion,
                summary: triggerPayload.Reason,
                payload: payload);
        }

        private static BattleDiagnosticEvent Event(
            BattleDiagnosticSessionScope scope,
            int frame,
            long sequence,
            BattleDiagnosticEventChannel channel = BattleDiagnosticEventChannel.Skill,
            long sourceActorId = 0,
            long targetActorId = 0,
            string summary = "event",
            BattleDiagnosticEventKind kind = BattleDiagnosticEventKind.SkillRuntimeStarted,
            int configId = 0,
            long rootContextId = 0)
        {
            return new BattleDiagnosticEvent(
                scope,
                frame,
                sequence,
                frame * 100L,
                kind,
                channel,
                BattleDiagnosticEventOutcome.Succeeded,
                sourceActorId,
                targetActorId,
                configId,
                rootContextId,
                summary: summary);
        }
    }
}

using System;
using System.Linq;
using AbilityKit.Game.Editor;
using NUnit.Framework;
using UnityEngine;

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
        public void RingStore_ValuableTriggerFilter_HidesConditionNoiseAndKeepsOtherEvents()
        {
            var store = new BattleDiagnosticEventRingStore(_scope, 8);
            var conditionMiss = new BattleDiagnosticTriggerAnalysisPayload(
                7001,
                contextKind: 2,
                originKind: 3,
                BattleDiagnosticTriggerAnalysisStage.Conditions,
                BattleDiagnosticTriggerAnalysisResult.Failed,
                failureKey: "predicateMiss");
            var executedPlan = new BattleDiagnosticTriggerAnalysisPayload(
                7001,
                contextKind: 2,
                originKind: 3,
                BattleDiagnosticTriggerAnalysisStage.Plan,
                BattleDiagnosticTriggerAnalysisResult.Passed);

            store.TryAppend(TriggerEvent(_scope, 20, 1, in conditionMiss));
            store.TryAppend(TriggerEvent(_scope, 21, 2, in executedPlan));
            store.TryAppend(Event(
                _scope,
                22,
                3,
                BattleDiagnosticEventChannel.DamageAndHeal,
                kind: BattleDiagnosticEventKind.Damage));

            var filter = BattleDiagnosticFilter.Default.WithTriggerAnalysis(
                BattleDiagnosticTriggerAnalysisStage.Unknown,
                BattleDiagnosticTriggerAnalysisResult.Unknown,
                value: BattleDiagnosticTriggerValueFilter.Valuable);
            var result = store.Query(new BattleDiagnosticEventQuery(
                1,
                filter,
                new BattleDiagnosticPageRequest(0, 0, 10)));

            Assert.That(result.Items.Select(item => item.Sequence), Is.EqualTo(new long[] { 2, 3 }));
        }

        [Test]
        public void RingStore_ExplicitTriggerStage_OverridesValuablePreset()
        {
            var store = new BattleDiagnosticEventRingStore(_scope, 4);
            var conditionMiss = new BattleDiagnosticTriggerAnalysisPayload(
                7001,
                contextKind: 2,
                originKind: 3,
                BattleDiagnosticTriggerAnalysisStage.Conditions,
                BattleDiagnosticTriggerAnalysisResult.Failed);
            store.TryAppend(TriggerEvent(_scope, 20, 1, in conditionMiss));

            var filter = BattleDiagnosticFilter.Default.WithTriggerAnalysis(
                BattleDiagnosticTriggerAnalysisStage.Conditions,
                BattleDiagnosticTriggerAnalysisResult.Failed,
                value: BattleDiagnosticTriggerValueFilter.Valuable);
            var result = store.Query(new BattleDiagnosticEventQuery(
                1,
                filter,
                new BattleDiagnosticPageRequest(0, 0, 10)));

            Assert.That(result.Items.Count, Is.EqualTo(1));
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
        public void TriggerAnalysisAggregatePayload_RoundTripsEveryField()
        {
            var aggregate = new BattleDiagnosticTriggerAnalysisAggregatePayload(
                7001,
                contextKind: 2,
                originKind: 3,
                BattleDiagnosticTriggerAnalysisStage.Conditions,
                BattleDiagnosticTriggerAnalysisResult.Failed,
                detailCode: 4,
                occurrenceCount: 59,
                firstFrame: 10,
                lastFrame: 68,
                firstContextId: 101,
                lastContextId: 159,
                firstRootContextId: 201,
                lastRootContextId: 259,
                failureKey: "predicateMiss",
                sampleReason: "Time limit not reached.");
            var payload = BattleDiagnosticEventPayload.FromTriggerAnalysisAggregate(in aggregate);

            Assert.That(payload.Kind, Is.EqualTo(BattleDiagnosticPayloadKind.TriggerAnalysisAggregate));
            Assert.That(payload.TryGetTriggerAnalysisAggregate(out var restored), Is.True);
            Assert.That(restored, Is.EqualTo(aggregate));
            Assert.That(restored.FrameSpan, Is.EqualTo(58));
            Assert.Throws<ArgumentException>(() => new BattleDiagnosticEvent(
                _scope,
                68,
                1,
                100L,
                BattleDiagnosticEventKind.TriggerAnalysis,
                BattleDiagnosticEventChannel.Trigger,
                BattleDiagnosticEventOutcome.Failed,
                payload: payload));
        }

        [Test]
        public void BuffLifecyclePayload_RoundTripsEveryFieldAndRejectsWrongEventKind()
        {
            var lifecycle = new BattleDiagnosticBuffLifecyclePayload(
                BattleDiagnosticBuffLifecycleStage.StackChanged,
                stackCount: 3,
                previousStackCount: 2,
                durationMilliseconds: 12000,
                remainingMilliseconds: 8750,
                intervalRemainingMilliseconds: 750,
                maxStacks: 5,
                modifierBindingCount: 4,
                modifierSourceId: 77,
                removeReason: 9);
            var payload = BattleDiagnosticEventPayload.FromBuffLifecycle(in lifecycle);

            Assert.That(payload.Kind, Is.EqualTo(BattleDiagnosticPayloadKind.BuffLifecycle));
            Assert.That(payload.SchemaVersion, Is.EqualTo(BattleDiagnosticBuffLifecyclePayload.CurrentSchemaVersion));
            Assert.That(payload.TryGetBuffLifecycle(out var restored), Is.True);
            Assert.That(restored, Is.EqualTo(lifecycle));
            Assert.That(restored.GetHashCode(), Is.EqualTo(lifecycle.GetHashCode()));
            Assert.That(payload, Is.EqualTo(BattleDiagnosticEventPayload.FromBuffLifecycle(in lifecycle)));
            Assert.That(BattleDiagnosticEventPayload.FromSkillFailure(
                new BattleDiagnosticSkillFailurePayload()).TryGetBuffLifecycle(out _), Is.False);

            Assert.DoesNotThrow(() => new BattleDiagnosticEvent(
                _scope,
                10,
                1,
                100L,
                BattleDiagnosticEventKind.BuffAdded,
                BattleDiagnosticEventChannel.Buff,
                BattleDiagnosticEventOutcome.Succeeded,
                payloadVersion: BattleDiagnosticBuffLifecyclePayload.CurrentSchemaVersion,
                payload: payload));
            Assert.DoesNotThrow(() => new BattleDiagnosticEvent(
                _scope,
                10,
                2,
                101L,
                BattleDiagnosticEventKind.BuffRemoved,
                BattleDiagnosticEventChannel.Buff,
                BattleDiagnosticEventOutcome.Succeeded,
                payloadVersion: BattleDiagnosticBuffLifecyclePayload.CurrentSchemaVersion,
                payload: payload));
            Assert.Throws<System.ArgumentException>(() => new BattleDiagnosticEvent(
                _scope,
                10,
                3,
                102L,
                BattleDiagnosticEventKind.Damage,
                BattleDiagnosticEventChannel.DamageAndHeal,
                BattleDiagnosticEventOutcome.Succeeded,
                payloadVersion: BattleDiagnosticBuffLifecyclePayload.CurrentSchemaVersion,
                payload: payload));
        }

        [TestCase("removed")]
        [TestCase("13")]
        [TestCase("83")]
        public void RingStore_TextSearch_MatchesStructuredBuffLifecycleFields(string searchText)
        {
            var store = new BattleDiagnosticEventRingStore(_scope, 4);
            var lifecycle = new BattleDiagnosticBuffLifecyclePayload(
                BattleDiagnosticBuffLifecycleStage.Removed,
                stackCount: 13,
                previousStackCount: 17,
                durationMilliseconds: 23000,
                remainingMilliseconds: 310,
                intervalRemainingMilliseconds: 470,
                maxStacks: 19,
                modifierBindingCount: 2,
                modifierSourceId: 6107,
                removeReason: 83);
            var payload = BattleDiagnosticEventPayload.FromBuffLifecycle(in lifecycle);
            store.TryAppend(new BattleDiagnosticEvent(
                _scope,
                20,
                1,
                100L,
                BattleDiagnosticEventKind.BuffRemoved,
                BattleDiagnosticEventChannel.Buff,
                BattleDiagnosticEventOutcome.Succeeded,
                sourceActorId: 7,
                targetActorId: 11,
                configId: 701,
                payloadVersion: BattleDiagnosticBuffLifecyclePayload.CurrentSchemaVersion,
                summary: "Structured lifecycle",
                payload: payload));

            var result = store.Query(new BattleDiagnosticEventQuery(
                1,
                BattleDiagnosticFilter.Default.WithSearchText(searchText),
                new BattleDiagnosticPageRequest(0, 0, 10)));
            var unrelated = store.Query(new BattleDiagnosticEventQuery(
                2,
                BattleDiagnosticFilter.Default.WithSearchText("not-a-lifecycle-value"),
                new BattleDiagnosticPageRequest(0, 0, 10)));

            Assert.That(result.Items.Count, Is.EqualTo(1));
            Assert.That(result.Items[0].Sequence, Is.EqualTo(1));
            Assert.That(unrelated.Items, Is.Empty);
        }

        [Test]
        public void SkillFailurePayload_RoundTripsEveryFieldAndRejectsWrongKind()
        {
            var failure = new BattleDiagnosticSkillFailurePayload(
                slot: 3,
                source: "Cast",
                stage: "Preparation",
                code: "Cast.TargetOutOfRange",
                message: "Target is outside cast range.",
                commandId: 4401);
            var payload = BattleDiagnosticEventPayload.FromSkillFailure(in failure);

            Assert.That(payload.Kind, Is.EqualTo(BattleDiagnosticPayloadKind.SkillFailure));
            Assert.That(payload.SchemaVersion, Is.EqualTo(BattleDiagnosticSkillFailurePayload.CurrentSchemaVersion));
            Assert.That(payload.TryGetSkillFailure(out var restored), Is.True);
            Assert.That(restored, Is.EqualTo(failure));
            Assert.That(restored.CommandId, Is.EqualTo(4401));
            Assert.That(payload, Is.EqualTo(BattleDiagnosticEventPayload.FromSkillFailure(in failure)));
            Assert.Throws<System.ArgumentException>(() => new BattleDiagnosticEvent(
                _scope,
                10,
                1,
                100L,
                BattleDiagnosticEventKind.SkillRuntimeEnded,
                BattleDiagnosticEventChannel.Skill,
                BattleDiagnosticEventOutcome.Failed,
                payload: payload));
        }

        [TestCase("Cast.TargetOutOfRange")]
        [TestCase("outside cast range")]
        [TestCase("3")]
        [TestCase("4401")]
        public void RingStore_TextSearch_MatchesStructuredSkillFailureFields(string searchText)
        {
            var store = new BattleDiagnosticEventRingStore(_scope, 4);
            var failure = new BattleDiagnosticSkillFailurePayload(
                slot: 3,
                source: "Cast",
                stage: "Preparation",
                code: "Cast.TargetOutOfRange",
                message: "Target is outside cast range.",
                commandId: 4401);
            var payload = BattleDiagnosticEventPayload.FromSkillFailure(in failure);
            store.TryAppend(new BattleDiagnosticEvent(
                _scope,
                20,
                1,
                100L,
                BattleDiagnosticEventKind.SkillFailure,
                BattleDiagnosticEventChannel.Skill,
                BattleDiagnosticEventOutcome.Failed,
                sourceActorId: 7,
                configId: 9001,
                payloadVersion: BattleDiagnosticSkillFailurePayload.CurrentSchemaVersion,
                summary: "Structured failure",
                payload: payload));

            var result = store.Query(new BattleDiagnosticEventQuery(
                1,
                BattleDiagnosticFilter.Default.WithSearchText(searchText),
                new BattleDiagnosticPageRequest(0, 0, 10)));

            Assert.That(result.Items.Count, Is.EqualTo(1));
            Assert.That(result.Items[0].Sequence, Is.EqualTo(1));
        }

        [Test]
        public void InputCommandPayload_RoundTripsEveryFieldAndRejectsWrongEventKind()
        {
            var input = new BattleDiagnosticInputCommandPayload(
                commandId: 4401,
                inputFrame: 120,
                playerId: "player-one",
                opCode: 17,
                succeeded: false,
                failureCode: 8,
                message: "Skill rejected.",
                skillSlot: 2,
                skillPhase: 1,
                targetActorId: 41);
            var payload = BattleDiagnosticEventPayload.FromInputCommand(in input);

            Assert.That(payload.Kind, Is.EqualTo(BattleDiagnosticPayloadKind.InputCommand));
            Assert.That(payload.SchemaVersion, Is.EqualTo(BattleDiagnosticInputCommandPayload.CurrentSchemaVersion));
            Assert.That(payload.TryGetInputCommand(out var restored), Is.True);
            Assert.That(restored.CommandId, Is.EqualTo(4401));
            Assert.That(restored.InputFrame, Is.EqualTo(120));
            Assert.That(restored.PlayerId, Is.EqualTo("player-one"));
            Assert.That(restored.OpCode, Is.EqualTo(17));
            Assert.That(restored.Succeeded, Is.False);
            Assert.That(restored.FailureCode, Is.EqualTo(8));
            Assert.That(restored.Message, Is.EqualTo("Skill rejected."));
            Assert.That(restored.SkillSlot, Is.EqualTo(2));
            Assert.That(restored.SkillPhase, Is.EqualTo(1));
            Assert.That(restored.TargetActorId, Is.EqualTo(41));
            Assert.That(payload, Is.EqualTo(BattleDiagnosticEventPayload.FromInputCommand(in input)));
            Assert.That(payload.TryGetTargetSearch(out _), Is.False);
            Assert.DoesNotThrow(() => new BattleDiagnosticEvent(
                _scope,
                20,
                1,
                100L,
                BattleDiagnosticEventKind.InputCommand,
                BattleDiagnosticEventChannel.Input,
                BattleDiagnosticEventOutcome.Failed,
                payload: payload));
            Assert.Throws<ArgumentException>(() => new BattleDiagnosticEvent(
                _scope,
                20,
                2,
                101L,
                BattleDiagnosticEventKind.TargetSearch,
                BattleDiagnosticEventChannel.Targeting,
                BattleDiagnosticEventOutcome.Failed,
                payload: payload));
        }

        [TestCase("4401")]
        [TestCase("player-one")]
        [TestCase("Skill rejected")]
        [TestCase("41")]
        public void RingStore_TextSearch_MatchesStructuredInputCommandFields(string searchText)
        {
            var store = new BattleDiagnosticEventRingStore(_scope, 4);
            var input = new BattleDiagnosticInputCommandPayload(
                4401, 120, "player-one", 17, false, 8, "Skill rejected.", 2, 1, 41);
            var payload = BattleDiagnosticEventPayload.FromInputCommand(in input);
            store.TryAppend(new BattleDiagnosticEvent(
                _scope,
                120,
                1,
                100L,
                BattleDiagnosticEventKind.InputCommand,
                BattleDiagnosticEventChannel.Input,
                BattleDiagnosticEventOutcome.Failed,
                sourceActorId: 7,
                targetActorId: 41,
                payloadVersion: BattleDiagnosticInputCommandPayload.CurrentSchemaVersion,
                summary: "Input processed",
                payload: payload));

            var result = store.Query(new BattleDiagnosticEventQuery(
                1,
                BattleDiagnosticFilter.Default.WithSearchText(searchText),
                new BattleDiagnosticPageRequest(0, 0, 10)));

            Assert.That(result.Items.Count, Is.EqualTo(1));
            Assert.That(result.Items[0].Sequence, Is.EqualTo(1));
        }

        [Test]
        public void TargetSearchPayload_RoundTripsEveryFieldAndRejectsWrongEventKind()
        {
            var search = new BattleDiagnosticTargetSearchPayload(
                commandId: 4401,
                explicitTargetActorId: 41,
                candidateCount: 8,
                eligibleCount: 3,
                selectedCount: 2,
                selectedActorIds: "41,52",
                decisionDetails: "41:eligible:rank=1;52:eligible:rank=2");
            var payload = BattleDiagnosticEventPayload.FromTargetSearch(in search);

            Assert.That(payload.Kind, Is.EqualTo(BattleDiagnosticPayloadKind.TargetSearch));
            Assert.That(payload.SchemaVersion, Is.EqualTo(BattleDiagnosticTargetSearchPayload.CurrentSchemaVersion));
            Assert.That(payload.TryGetTargetSearch(out var restored), Is.True);
            Assert.That(restored.CommandId, Is.EqualTo(4401));
            Assert.That(restored.ExplicitTargetActorId, Is.EqualTo(41));
            Assert.That(restored.CandidateCount, Is.EqualTo(8));
            Assert.That(restored.EligibleCount, Is.EqualTo(3));
            Assert.That(restored.SelectedCount, Is.EqualTo(2));
            Assert.That(restored.SelectedActorIds, Is.EqualTo("41,52"));
            Assert.That(restored.DecisionDetails, Is.EqualTo("41:eligible:rank=1;52:eligible:rank=2"));
            Assert.That(payload, Is.EqualTo(BattleDiagnosticEventPayload.FromTargetSearch(in search)));
            Assert.That(payload.TryGetInputCommand(out _), Is.False);
            Assert.DoesNotThrow(() => new BattleDiagnosticEvent(
                _scope,
                20,
                1,
                100L,
                BattleDiagnosticEventKind.TargetSearch,
                BattleDiagnosticEventChannel.Targeting,
                BattleDiagnosticEventOutcome.Succeeded,
                payload: payload));
            Assert.Throws<ArgumentException>(() => new BattleDiagnosticEvent(
                _scope,
                20,
                2,
                101L,
                BattleDiagnosticEventKind.InputCommand,
                BattleDiagnosticEventChannel.Input,
                BattleDiagnosticEventOutcome.Succeeded,
                payload: payload));
        }

        [TestCase("4401")]
        [TestCase("41,52")]
        [TestCase("eligible:rank")]
        public void RingStore_TextSearch_MatchesStructuredTargetSearchFields(string searchText)
        {
            var store = new BattleDiagnosticEventRingStore(_scope, 4);
            var search = new BattleDiagnosticTargetSearchPayload(
                4401, 41, 8, 3, 2, "41,52", "41:eligible:rank=1;52:eligible:rank=2");
            var payload = BattleDiagnosticEventPayload.FromTargetSearch(in search);
            store.TryAppend(new BattleDiagnosticEvent(
                _scope,
                120,
                1,
                100L,
                BattleDiagnosticEventKind.TargetSearch,
                BattleDiagnosticEventChannel.Targeting,
                BattleDiagnosticEventOutcome.Succeeded,
                sourceActorId: 7,
                targetActorId: 41,
                payloadVersion: BattleDiagnosticTargetSearchPayload.CurrentSchemaVersion,
                summary: "Target search completed",
                payload: payload));

            var result = store.Query(new BattleDiagnosticEventQuery(
                1,
                BattleDiagnosticFilter.Default.WithSearchText(searchText),
                new BattleDiagnosticPageRequest(0, 0, 10)));

            Assert.That(result.Items.Count, Is.EqualTo(1));
            Assert.That(result.Items[0].Sequence, Is.EqualTo(1));
        }

        [Test]
        public void SkillExecutionPayload_RoundTripsEveryFieldAndRejectsWrongEventKind()
        {
            var execution = new BattleDiagnosticSkillExecutionPayload(
                commandId: 4401,
                BattleDiagnosticSkillExecutionStage.EconomyCommitted,
                skillSlot: 2,
                skillLevel: 3,
                castSequence: 17,
                endReason: 1,
                resourceType: 4,
                resourceAmountRaw: 10L << 32,
                resourceBeforeRaw: 100L << 32,
                resourceAfterRaw: 90L << 32,
                chargeCost: 1,
                cooldownMs: 800,
                sharedCooldownMs: 600,
                globalCooldownMs: 300,
                pendingChildren: 2,
                forced: true,
                detail: "cast.release");
            var payload = BattleDiagnosticEventPayload.FromSkillExecution(in execution);

            Assert.That(payload.Kind, Is.EqualTo(BattleDiagnosticPayloadKind.SkillExecution));
            Assert.That(payload.SchemaVersion, Is.EqualTo(BattleDiagnosticSkillExecutionPayload.CurrentSchemaVersion));
            Assert.That(payload.TryGetSkillExecution(out var restored), Is.True);
            Assert.That(restored.CommandId, Is.EqualTo(4401));
            Assert.That(restored.Stage, Is.EqualTo(BattleDiagnosticSkillExecutionStage.EconomyCommitted));
            Assert.That(restored.SkillSlot, Is.EqualTo(2));
            Assert.That(restored.SkillLevel, Is.EqualTo(3));
            Assert.That(restored.CastSequence, Is.EqualTo(17));
            Assert.That(restored.EndReason, Is.EqualTo(1));
            Assert.That(restored.ResourceType, Is.EqualTo(4));
            Assert.That(restored.ResourceAmountRaw, Is.EqualTo(10L << 32));
            Assert.That(restored.ResourceBeforeRaw, Is.EqualTo(100L << 32));
            Assert.That(restored.ResourceAfterRaw, Is.EqualTo(90L << 32));
            Assert.That(restored.ChargeCost, Is.EqualTo(1));
            Assert.That(restored.CooldownMs, Is.EqualTo(800));
            Assert.That(restored.SharedCooldownMs, Is.EqualTo(600));
            Assert.That(restored.GlobalCooldownMs, Is.EqualTo(300));
            Assert.That(restored.PendingChildren, Is.EqualTo(2));
            Assert.That(restored.Forced, Is.True);
            Assert.That(restored.Detail, Is.EqualTo("cast.release"));
            Assert.DoesNotThrow(() => new BattleDiagnosticEvent(
                _scope, 20, 1, 100L,
                BattleDiagnosticEventKind.SkillEconomy,
                BattleDiagnosticEventChannel.Skill,
                BattleDiagnosticEventOutcome.Succeeded,
                payload: payload));
            Assert.Throws<ArgumentException>(() => new BattleDiagnosticEvent(
                _scope, 20, 2, 101L,
                BattleDiagnosticEventKind.Damage,
                BattleDiagnosticEventChannel.DamageAndHeal,
                BattleDiagnosticEventOutcome.Succeeded,
                payload: payload));
        }

        [TestCase("4401")]
        [TestCase("EconomyCommitted")]
        [TestCase("cast.release")]
        [TestCase("800")]
        public void RingStore_TextSearch_MatchesStructuredSkillExecutionFields(string searchText)
        {
            var store = new BattleDiagnosticEventRingStore(_scope, 4);
            var execution = new BattleDiagnosticSkillExecutionPayload(
                4401, BattleDiagnosticSkillExecutionStage.EconomyCommitted,
                2, 3, 17, resourceType: 4, resourceAmountRaw: 10L << 32,
                resourceBeforeRaw: 100L << 32, resourceAfterRaw: 90L << 32,
                chargeCost: 1, cooldownMs: 800, detail: "cast.release");
            var payload = BattleDiagnosticEventPayload.FromSkillExecution(in execution);
            store.TryAppend(new BattleDiagnosticEvent(
                _scope, 120, 1, 100L,
                BattleDiagnosticEventKind.SkillEconomy,
                BattleDiagnosticEventChannel.Skill,
                BattleDiagnosticEventOutcome.Succeeded,
                sourceActorId: 7,
                configId: 101,
                payloadVersion: BattleDiagnosticSkillExecutionPayload.CurrentSchemaVersion,
                summary: "Economy committed",
                payload: payload));

            var result = store.Query(new BattleDiagnosticEventQuery(
                1,
                BattleDiagnosticFilter.Default.WithSearchText(searchText),
                new BattleDiagnosticPageRequest(0, 0, 10)));

            Assert.That(result.Items.Count, Is.EqualTo(1));
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

        [Test]
        public void MetricRingStore_EvictsOldestAndQueriesStableRevisionPages()
        {
            var store = new BattleDiagnosticMetricRingStore(_scope, capacity: 3);
            Assert.That(store.TryAppend(Metric(1, 10, BattleDiagnosticMetricCategory.Prediction, "prediction.backlog", 2)), Is.True);
            Assert.That(store.TryAppend(Metric(2, 11, BattleDiagnosticMetricCategory.Network, "network.buffered", 3)), Is.True);
            Assert.That(store.TryAppend(Metric(3, 12, BattleDiagnosticMetricCategory.Prediction, "prediction.backlog", 4)), Is.True);
            Assert.That(store.TryAppend(Metric(4, 13, BattleDiagnosticMetricCategory.Prediction, "prediction.backlog", 5)), Is.True);

            var firstPage = store.QueryMetrics(new BattleDiagnosticMetricQuery(
                1,
                new BattleDiagnosticFrameRange(10, 13),
                new BattleDiagnosticPageRequest(0, 0, 1),
                BattleDiagnosticMetricCategory.Prediction,
                "prediction.backlog"));

            Assert.That(store.Count, Is.EqualTo(3));
            Assert.That(store.Metrics.EvictedCount, Is.EqualTo(1));
            Assert.That(firstPage.Status.HasMore, Is.True);
            Assert.That(firstPage.Items.Count, Is.EqualTo(1));
            Assert.That(firstPage.Items[0].Frame, Is.EqualTo(12));

            var secondPage = store.QueryMetrics(new BattleDiagnosticMetricQuery(
                2,
                new BattleDiagnosticFrameRange(10, 13),
                new BattleDiagnosticPageRequest(firstPage.Status.StoreRevision, 1, 1),
                BattleDiagnosticMetricCategory.Prediction,
                "prediction.backlog"));
            Assert.That(secondPage.Items.Single().Frame, Is.EqualTo(13));
            Assert.That(secondPage.Status.HasMore, Is.False);
        }

        [Test]
        public void MetricRingStore_SnapshotPreservesRetainedOrderAndMetrics()
        {
            var store = new BattleDiagnosticMetricRingStore(_scope, capacity: 2);
            store.TryAppend(Metric(1, 20, BattleDiagnosticMetricCategory.Rollback, "rollback.total", 1));
            store.TryAppend(Metric(2, 21, BattleDiagnosticMetricCategory.Rollback, "rollback.total", 2));

            var snapshot = store.CaptureMetricSnapshot();

            Assert.That(snapshot.Revision, Is.EqualTo(store.Revision));
            Assert.That(snapshot.Metrics, Is.EqualTo(store.Metrics));
            Assert.That(snapshot.Samples.Select(item => item.Sequence), Is.EqualTo(new long[] { 1, 2 }));
        }

        [Test]
        public void MetricRingStore_AggregatesLongRangesPerSeriesAndPreservesExtrema()
        {
            var store = new BattleDiagnosticMetricRingStore(_scope, capacity: 32);
            var sequence = 0L;
            for (var frame = 0; frame < 100; frame += 10)
            {
                var backlog = frame == 20 ? 99d : frame / 10d;
                Assert.That(store.TryAppend(new BattleDiagnosticMetricSample(
                    _scope,
                    ++sequence,
                    frame,
                    frame * 100L,
                    BattleDiagnosticMetricCategory.Prediction,
                    BattleDiagnosticMetricValueKind.Gauge,
                    BattleDiagnosticFrameMetricKeys.PredictionBacklog,
                    backlog)), Is.True);
                Assert.That(store.TryAppend(new BattleDiagnosticMetricSample(
                    _scope,
                    ++sequence,
                    frame,
                    frame * 100L + 1L,
                    BattleDiagnosticMetricCategory.Prediction,
                    BattleDiagnosticMetricValueKind.Gauge,
                    BattleDiagnosticFrameMetricKeys.PredictionWindow,
                    6d)), Is.True);
            }

            var firstPage = store.QueryMetricAggregates(new BattleDiagnosticMetricAggregateQuery(
                1,
                new BattleDiagnosticFrameRange(0, 99),
                new BattleDiagnosticPageRequest(0L, 0, 3),
                BattleDiagnosticMetricCategory.Prediction,
                bucketCount: 2));

            Assert.That(firstPage.Status.HasMore, Is.True);
            Assert.That(firstPage.Items.Count, Is.EqualTo(3));
            var firstBacklogBucket = firstPage.Items.Single(item =>
                item.Metric == BattleDiagnosticFrameMetricKeys.PredictionBacklog && item.FirstFrame == 0);
            Assert.That(firstBacklogBucket.LastFrame, Is.EqualTo(49));
            Assert.That(firstBacklogBucket.SampleCount, Is.EqualTo(5));
            Assert.That(firstBacklogBucket.FirstValue, Is.EqualTo(0d));
            Assert.That(firstBacklogBucket.LastValue, Is.EqualTo(4d));
            Assert.That(firstBacklogBucket.MinimumValue, Is.EqualTo(0d));
            Assert.That(firstBacklogBucket.MaximumValue, Is.EqualTo(99d));

            var secondPage = store.QueryMetricAggregates(new BattleDiagnosticMetricAggregateQuery(
                2,
                new BattleDiagnosticFrameRange(0, 99),
                new BattleDiagnosticPageRequest(firstPage.Status.StoreRevision, 3, 3),
                BattleDiagnosticMetricCategory.Prediction,
                bucketCount: 2));
            Assert.That(secondPage.Status.HasMore, Is.False);
            Assert.That(secondPage.Items.Single().FirstFrame, Is.EqualTo(50));

            store.TryAppend(new BattleDiagnosticMetricSample(
                _scope,
                ++sequence,
                100,
                10000L,
                BattleDiagnosticMetricCategory.Prediction,
                BattleDiagnosticMetricValueKind.Gauge,
                BattleDiagnosticFrameMetricKeys.PredictionBacklog,
                10d));
            var stalePage = store.QueryMetricAggregates(new BattleDiagnosticMetricAggregateQuery(
                3,
                new BattleDiagnosticFrameRange(0, 100),
                new BattleDiagnosticPageRequest(firstPage.Status.StoreRevision, 0, 3),
                BattleDiagnosticMetricCategory.Prediction,
                bucketCount: 2));
            Assert.That(stalePage.Status.Availability, Is.EqualTo(BattleDiagnosticDataAvailability.Evicted));
        }

        [Test]
        public void FrameMetricCatalog_EvaluatesWindowMaximumAndCounterDelta()
        {
            Assert.That(
                BattleDiagnosticFrameMetricCatalog.All.Select(item => item.Metric).Distinct().Count(),
                Is.EqualTo(BattleDiagnosticFrameMetricCatalog.All.Count));
            Assert.That(BattleDiagnosticFrameMetricCatalog.TryGet(
                BattleDiagnosticFrameMetricKeys.PredictionBacklog,
                out var backlogDescriptor), Is.True);
            Assert.That(backlogDescriptor.Unit, Is.EqualTo("frames"));
            Assert.That(backlogDescriptor.Group, Is.EqualTo("prediction.pressure"));
            Assert.That(backlogDescriptor.HasSuggestedRange, Is.True);

            var store = new BattleDiagnosticMetricRingStore(_scope, capacity: 8);
            store.TryAppend(new BattleDiagnosticMetricSample(
                _scope, 1, 10, 100L,
                BattleDiagnosticMetricCategory.Prediction,
                BattleDiagnosticMetricValueKind.Gauge,
                BattleDiagnosticFrameMetricKeys.PredictionBacklog,
                1d));
            store.TryAppend(new BattleDiagnosticMetricSample(
                _scope, 2, 20, 200L,
                BattleDiagnosticMetricCategory.Prediction,
                BattleDiagnosticMetricValueKind.Gauge,
                BattleDiagnosticFrameMetricKeys.PredictionBacklog,
                9d));
            store.TryAppend(new BattleDiagnosticMetricSample(
                _scope, 3, 10, 201L,
                BattleDiagnosticMetricCategory.Network,
                BattleDiagnosticMetricValueKind.Counter,
                BattleDiagnosticFrameMetricKeys.NetworkDuplicateTotal,
                100d));
            store.TryAppend(new BattleDiagnosticMetricSample(
                _scope, 4, 20, 202L,
                BattleDiagnosticMetricCategory.Network,
                BattleDiagnosticMetricValueKind.Counter,
                BattleDiagnosticFrameMetricKeys.NetworkDuplicateTotal,
                101d));

            var aggregates = store.QueryMetricAggregates(new BattleDiagnosticMetricAggregateQuery(
                1,
                new BattleDiagnosticFrameRange(10, 20),
                new BattleDiagnosticPageRequest(0L, 0, 10),
                bucketCount: 1));
            var assessments = BattleDiagnosticFrameMetricCatalog.Evaluate(aggregates.Items);

            var backlog = assessments.Single(item =>
                item.Descriptor.Metric == BattleDiagnosticFrameMetricKeys.PredictionBacklog);
            Assert.That(backlog.Severity, Is.EqualTo(BattleDiagnosticMetricSeverity.Critical));
            Assert.That(backlog.ActualValue, Is.EqualTo(9d));
            var duplicates = assessments.Single(item =>
                item.Descriptor.Metric == BattleDiagnosticFrameMetricKeys.NetworkDuplicateTotal);
            Assert.That(duplicates.Severity, Is.EqualTo(BattleDiagnosticMetricSeverity.Warning));
            Assert.That(duplicates.ActualValue, Is.EqualTo(1d));
        }

        [Test]
        public void MetricProfileResolver_AppliesMatchingLayersFromGeneralToSpecific()
        {
            var context = new BattleDiagnosticMetricProfileContext(
                "AbilityKit.Demo.Moba",
                "ranked",
                "wan",
                "low");
            var metric = BattleDiagnosticFrameMetricKeys.PredictionBacklog;
            var layers = new[]
            {
                new BattleDiagnosticMetricProfileLayer(
                    "Global",
                    0,
                    new[] { new BattleDiagnosticMetricThresholdOverride(metric, 7d, 10d) }),
                new BattleDiagnosticMetricProfileLayer(
                    "Project",
                    0,
                    new[] { new BattleDiagnosticMetricThresholdOverride(metric, 6d, 9d) },
                    project: "AbilityKit.Demo.Moba"),
                new BattleDiagnosticMetricProfileLayer(
                    "Ranked WAN",
                    0,
                    new[] { new BattleDiagnosticMetricThresholdOverride(metric, 4d, 7d) },
                    project: "AbilityKit.Demo.Moba",
                    gameMode: "ranked",
                    networkMode: "wan"),
                new BattleDiagnosticMetricProfileLayer(
                    "Low Device",
                    0,
                    new[] { new BattleDiagnosticMetricThresholdOverride(metric, 3d, 6d, 0d, 6d) },
                    project: "AbilityKit.Demo.Moba",
                    gameMode: "ranked",
                    networkMode: "wan",
                    deviceTier: "low"),
                new BattleDiagnosticMetricProfileLayer(
                    "Wrong Network",
                    100,
                    new[] { new BattleDiagnosticMetricThresholdOverride(metric, 1d, 2d) },
                    networkMode: "lan")
            };

            var profile = BattleDiagnosticMetricProfileResolver.Resolve(in context, layers);

            Assert.That(profile.MatchedLayers, Is.EqualTo(new[] { "Global", "Project", "Ranked WAN", "Low Device" }));
            Assert.That(profile.TryGet(metric, out var descriptor), Is.True);
            Assert.That(descriptor.WarningThreshold, Is.EqualTo(3d));
            Assert.That(descriptor.CriticalThreshold, Is.EqualTo(6d));
            Assert.That(descriptor.SuggestedMaximum, Is.EqualTo(6d));
        }

        [Test]
        public void MetricProfileComparer_ReportsContextAndEffectiveThresholdDifferences()
        {
            var capturedContext = new BattleDiagnosticMetricProfileContext(
                "AbilityKit.Demo.Moba",
                "ranked",
                "wan",
                "low");
            var currentContext = new BattleDiagnosticMetricProfileContext(
                "AbilityKit.Demo.Moba",
                "ranked",
                "lan",
                "high");
            var metric = BattleDiagnosticFrameMetricKeys.PredictionBacklog;
            var captured = BattleDiagnosticMetricProfileResolver.Resolve(
                in capturedContext,
                new[]
                {
                    new BattleDiagnosticMetricProfileLayer(
                        "Captured Low",
                        0,
                        new[] { new BattleDiagnosticMetricThresholdOverride(metric, 2d, 5d, 0d, 6d) })
                });
            var current = BattleDiagnosticMetricProfileResolver.Resolve(
                in currentContext,
                new[]
                {
                    new BattleDiagnosticMetricProfileLayer(
                        "Current High",
                        0,
                        new[] { new BattleDiagnosticMetricThresholdOverride(metric, 4d, 8d, 0d, 10d) })
                });

            var comparison = BattleDiagnosticMetricProfileComparer.Compare(captured, current);

            Assert.That(comparison.HasDifferences, Is.True);
            Assert.That(comparison.ContextMatches, Is.False);
            var difference = comparison.ThresholdDifferences.Single(item => item.Metric == metric);
            Assert.That(difference.WarningChanged, Is.True);
            Assert.That(difference.CriticalChanged, Is.True);
            Assert.That(difference.SuggestedRangeChanged, Is.True);
            Assert.That(difference.CapturedWarningThreshold, Is.EqualTo(2d));
            Assert.That(difference.CurrentCriticalThreshold, Is.EqualTo(8d));
        }

        [Test]
        public void MetricProfileComparer_EquivalentProfilesHaveNoDifferences()
        {
            var context = new BattleDiagnosticMetricProfileContext("AbilityKit.Demo.Moba");
            var captured = BattleDiagnosticMetricProfileResolver.Resolve(in context);
            var current = BattleDiagnosticMetricProfileResolver.Resolve(in context);

            var comparison = BattleDiagnosticMetricProfileComparer.Compare(captured, current);

            Assert.That(comparison.HasDifferences, Is.False);
            Assert.That(comparison.ContextMatches, Is.True);
            Assert.That(comparison.ThresholdDifferences, Is.Empty);
        }

        [Test]
        public void MetricProfileAsset_ValidConfigurationBuildsEffectivePreview()
        {
            var asset = ScriptableObject.CreateInstance<BattleDiagnosticMetricProfileAsset>();
            try
            {
                asset.GameMode = "ranked";
                asset.NetworkMode = "wan";
                asset.DeviceTier = "low";
                var layer = new BattleDiagnosticMetricProfileLayerConfig
                {
                    Name = "Ranked WAN Low",
                    Priority = 10,
                    Project = "AbilityKit.Demo.Moba",
                    GameMode = "ranked",
                    NetworkMode = "wan",
                    DeviceTier = "low"
                };
                layer.Overrides.Add(new BattleDiagnosticMetricThresholdOverrideConfig
                {
                    Metric = BattleDiagnosticFrameMetricKeys.PredictionBacklog,
                    WarningThreshold = 2d,
                    CriticalThreshold = 5d,
                    OverrideSuggestedRange = true,
                    SuggestedMinimum = 0d,
                    SuggestedMaximum = 6d
                });
                asset.Layers.Add(layer);

                var built = asset.TryBuild(out var context, out var layers, out var issues);
                var preview = asset.BuildPreview();

                Assert.That(built, Is.True);
                Assert.That(issues, Is.Empty);
                Assert.That(context, Is.EqualTo(asset.Context));
                Assert.That(layers.Single().Name, Is.EqualTo("Ranked WAN Low"));
                Assert.That(preview.MatchedLayers, Is.EqualTo(new[] { "Ranked WAN Low" }));
                Assert.That(preview.TryGet(
                    BattleDiagnosticFrameMetricKeys.PredictionBacklog,
                    out var descriptor), Is.True);
                Assert.That(descriptor.WarningThreshold, Is.EqualTo(2d));
                Assert.That(descriptor.CriticalThreshold, Is.EqualTo(5d));
                Assert.That(descriptor.SuggestedMaximum, Is.EqualTo(6d));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void MetricProfileAsset_InvalidRulesAreReportedAndDoNotBuild()
        {
            var asset = ScriptableObject.CreateInstance<BattleDiagnosticMetricProfileAsset>();
            try
            {
                var first = new BattleDiagnosticMetricProfileLayerConfig { Name = "Duplicate" };
                first.Overrides.Add(new BattleDiagnosticMetricThresholdOverrideConfig
                {
                    Metric = "unknown.metric",
                    WarningThreshold = 5d,
                    CriticalThreshold = 2d
                });
                first.Overrides.Add(new BattleDiagnosticMetricThresholdOverrideConfig
                {
                    Metric = "unknown.metric",
                    WarningThreshold = 1d,
                    CriticalThreshold = 2d
                });
                asset.Layers.Add(first);
                asset.Layers.Add(new BattleDiagnosticMetricProfileLayerConfig { Name = "Duplicate" });

                var built = asset.TryBuild(out _, out var layers, out var issues);

                Assert.That(built, Is.False);
                Assert.That(layers, Is.Empty);
                Assert.That(issues.Count(item =>
                    item.Severity == BattleDiagnosticMetricProfileValidationSeverity.Error),
                    Is.GreaterThanOrEqualTo(4));
                Assert.That(issues.Any(item =>
                    item.Severity == BattleDiagnosticMetricProfileValidationSeverity.Warning), Is.True);
                Assert.That(issues.All(item => !string.IsNullOrWhiteSpace(item.Message)), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void FrameMetricCatalog_CompoundRulesRequireBothSignalsInSameDimension()
        {
            BattleDiagnosticFrameMetricCatalog.TryGet(
                BattleDiagnosticFrameMetricKeys.PredictionBacklog,
                out var backlogDescriptor);
            BattleDiagnosticFrameMetricCatalog.TryGet(
                BattleDiagnosticFrameMetricKeys.PredictionStalled,
                out var stalledDescriptor);
            BattleDiagnosticFrameMetricCatalog.TryGet(
                BattleDiagnosticFrameMetricKeys.NetworkTargetGap,
                out var gapDescriptor);
            BattleDiagnosticFrameMetricCatalog.TryGet(
                BattleDiagnosticFrameMetricKeys.NetworkLateTotal,
                out var lateDescriptor);
            BattleDiagnosticFrameMetricCatalog.TryGet(
                BattleDiagnosticFrameMetricKeys.RollbackTotal,
                out var rollbackDescriptor);
            BattleDiagnosticFrameMetricCatalog.TryGet(
                BattleDiagnosticFrameMetricKeys.RollbackRestoreFailedTotal,
                out var restoreDescriptor);
            var assessments = new[]
            {
                Assessment(in backlogDescriptor, "local", BattleDiagnosticMetricSeverity.Warning),
                Assessment(in stalledDescriptor, "local", BattleDiagnosticMetricSeverity.Critical),
                Assessment(in gapDescriptor, "local", BattleDiagnosticMetricSeverity.Warning),
                Assessment(in lateDescriptor, "local", BattleDiagnosticMetricSeverity.Warning),
                Assessment(in rollbackDescriptor, "local", BattleDiagnosticMetricSeverity.Warning),
                Assessment(in restoreDescriptor, "local", BattleDiagnosticMetricSeverity.Critical),
                Assessment(in backlogDescriptor, "remote", BattleDiagnosticMetricSeverity.Warning)
            };

            var compounds = BattleDiagnosticFrameMetricCatalog.EvaluateCompounds(assessments);

            Assert.That(compounds.Select(item => item.Rule.Id), Is.EqualTo(new[]
            {
                "prediction.backlog_stall",
                "rollback.restore_failure",
                "network.late_target_gap"
            }));
            Assert.That(compounds.Single(item => item.Rule.Id == "prediction.backlog_stall").Severity,
                Is.EqualTo(BattleDiagnosticMetricSeverity.Critical));
            Assert.That(compounds.Any(item => item.Dimension == "remote"), Is.False);
        }

        private BattleDiagnosticEventQuery Query(long requestId, int offset, int limit)
        {
            return new BattleDiagnosticEventQuery(
                requestId,
                BattleDiagnosticFilter.Default,
                new BattleDiagnosticPageRequest(0, offset, limit));
        }

        private BattleDiagnosticMetricSample Metric(
            long sequence,
            int frame,
            BattleDiagnosticMetricCategory category,
            string metric,
            double value)
        {
            return new BattleDiagnosticMetricSample(
                _scope,
                sequence,
                frame,
                frame * 100L,
                category,
                BattleDiagnosticMetricValueKind.Gauge,
                metric,
                value);
        }

        private static BattleDiagnosticMetricAssessment Assessment(
            in BattleDiagnosticMetricDescriptor descriptor,
            string dimension,
            BattleDiagnosticMetricSeverity severity)
        {
            return new BattleDiagnosticMetricAssessment(
                in descriptor,
                dimension,
                severity,
                severity == BattleDiagnosticMetricSeverity.Critical
                    ? descriptor.CriticalThreshold
                    : descriptor.WarningThreshold,
                10,
                20,
                2);
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

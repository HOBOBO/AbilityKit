using System;
using System.Linq;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Diagnostics.Analysis;
using AbilityKit.Game.Editor;
using NUnit.Framework;

namespace AbilityKit.Demo.Moba.Diagnostics.Tests
{
    public sealed class MobaBattleDiagnosticArtifactCodecTests
    {
        private const int Frame = 120;
        private const long EventRevision = 11;
        private BattleDiagnosticSessionScope _scope;

        [SetUp]
        public void SetUp()
        {
            _scope = new BattleDiagnosticSessionScope("offline-session", "battle-world", 3);
        }

        [Test]
        public void ExportImportSnapshot_RoundTripsEveryTrackAndStructuredPayload()
        {
            var source = CreateSnapshot();
            var artifact = MobaBattleDiagnosticArtifactCodec.Attach(
                new AbilityKitAnalysisArtifact(),
                source);

            var json = MobaBattleDiagnosticArtifactCodec.ExportToString(artifact);
            var restored = MobaBattleDiagnosticArtifactCodec.ImportSnapshot(json);

            StringAssert.Contains("\"battleDiagnostics\"", json);
            StringAssert.DoesNotContain("\"BattleDiagnostics\"", json);
            Assert.That(restored.SessionInfo, Is.EqualTo(source.SessionInfo));
            Assert.That(restored.CapturedAtTimestamp, Is.EqualTo(source.CapturedAtTimestamp));
            Assert.That(restored.Events.Revision, Is.EqualTo(EventRevision));
            Assert.That(restored.Events.Metrics, Is.EqualTo(source.Events.Metrics));
            Assert.That(restored.Events.Events, Is.EqualTo(source.Events.Events));
            Assert.That(restored.State.World, Is.EqualTo(source.State.World));
            Assert.That(restored.State.Actors, Is.EqualTo(source.State.Actors));
            Assert.That(restored.Trace.Nodes, Is.EqualTo(source.Trace.Nodes));
            Assert.That(restored.Attributes.Attributes, Is.EqualTo(source.Attributes.Attributes));
            Assert.That(restored.Attributes.Modifiers, Is.EqualTo(source.Attributes.Modifiers));
            Assert.That(restored.Buffs.Items, Is.EqualTo(source.Buffs.Items));
            Assert.That(restored.Tags.Items, Is.EqualTo(source.Tags.Items));
            Assert.That(restored.Effects.Items, Is.EqualTo(source.Effects.Items));

            Assert.That(restored.Events.Events[1].Payload.TryGetSyncSnapshotReceived(out var payload), Is.True);
            Assert.That(payload.AuthoritativeFrame, Is.EqualTo(Frame - 1));
            Assert.That(payload.StateHash, Is.EqualTo(0xAABBCCDDU));
            Assert.That(restored.Events.Events[2].Payload.TryGetTriggerAnalysis(out var trigger), Is.True);
            Assert.That(trigger.TriggerId, Is.EqualTo(7001));
            Assert.That(trigger.Stage, Is.EqualTo(BattleDiagnosticTriggerAnalysisStage.Conditions));
            Assert.That(trigger.Result, Is.EqualTo(BattleDiagnosticTriggerAnalysisResult.Failed));
            Assert.That(trigger.FailureKey, Is.EqualTo("missingMana"));
            Assert.That(trigger.Reason, Is.EqualTo("Missing mana for trigger."));
            Assert.That(restored.Events.Events[3].Payload.TryGetSkillFailure(out var failure), Is.True);
            Assert.That(failure.Slot, Is.EqualTo(2));
            Assert.That(failure.Source, Is.EqualTo("Cast"));
            Assert.That(failure.Stage, Is.EqualTo("Preparation"));
            Assert.That(failure.Code, Is.EqualTo("Cast.TargetOutOfRange"));
            Assert.That(failure.Message, Is.EqualTo("Target is outside cast range."));
        }

        [Test]
        public void ExportSnapshotToString_CreatesStandardArtifactRoot()
        {
            var json = MobaBattleDiagnosticArtifactCodec.ExportSnapshotToString(CreateSnapshot());

            StringAssert.Contains("\"schemaVersion\": \"abilitykit-analysis.v1\"", json);
            StringAssert.Contains("\"battleDiagnostics\"", json);
            Assert.That(MobaBattleDiagnosticArtifactCodec.ImportSnapshot(json).State.Actors.Count, Is.EqualTo(2));
        }

        [Test]
        public void DiagnosticSource_OpenAndReturnToLive_SwitchesOfflineStateAndActors()
        {
            using (var source = new BattleDebugDiagnosticSource())
            {
                source.Open(Export(CreateSnapshot()), @"C:\captures\battle.json");

                Assert.That(source.IsOffline, Is.True);
                Assert.That(source.DisplayName, Is.EqualTo("battle.json"));
                Assert.That(source.FilePath, Is.EqualTo(@"C:\captures\battle.json"));
                Assert.That(source.Actors.Select(actor => actor.ActorId), Is.EqualTo(new long[] { 1, 2 }));
                Assert.That(source.Session.SessionInfo.ConnectionState, Is.EqualTo(BattleDiagnosticConnectionState.Disconnected));
                Assert.That(source.Session.SessionInfo.CaptureState, Is.EqualTo(BattleDiagnosticCaptureState.Frozen));

                source.ReturnToLive();

                Assert.That(source.IsOffline, Is.False);
                Assert.That(source.Session, Is.Null);
                Assert.That(source.Actors, Is.Empty);
                Assert.That(source.FilePath, Is.Empty);
            }
        }

        [Test]
        public void DiagnosticSource_InvalidReplacement_PreservesCurrentOfflineArtifact()
        {
            using (var source = new BattleDebugDiagnosticSource())
            {
                source.Open(Export(CreateSnapshot()), "valid.json");
                var currentSession = source.Session;

                var exception = Assert.Throws<MobaBattleDiagnosticArtifactException>(
                    () => source.Open("{broken", "broken.json"));

                Assert.That(exception.ErrorCode, Is.EqualTo("Artifact.MalformedJson"));
                Assert.That(source.IsOffline, Is.True);
                Assert.That(source.Session, Is.SameAs(currentSession));
                Assert.That(source.DisplayName, Is.EqualTo("valid.json"));
                Assert.That(source.Actors.Count, Is.EqualTo(2));
            }
        }

        [Test]
        public void ImportArtifact_LegacyArtifactWithoutBattleSection_RemainsValid()
        {
            const string json = "{\"schemaVersion\":\"abilitykit-analysis.v1\",\"futureRoot\":true}";

            var artifact = MobaBattleDiagnosticArtifactCodec.ImportArtifact(json);

            Assert.That(artifact.BattleDiagnostics, Is.Null);
            var exception = Assert.Throws<MobaBattleDiagnosticArtifactException>(
                () => MobaBattleDiagnosticArtifactCodec.ImportSnapshot(json));
            Assert.That(exception.ErrorCode, Is.EqualTo("BattleDiagnostics.Missing"));
        }

        [TestCase("", "Artifact.Empty")]
        [TestCase("{broken", "Artifact.MalformedJson")]
        [TestCase("{\"schemaVersion\":\"abilitykit-analysis.v2\"}", "Artifact.SchemaVersion")]
        public void ImportArtifact_InvalidRoot_ReturnsStableErrorCode(string json, string expectedErrorCode)
        {
            var exception = Assert.Throws<MobaBattleDiagnosticArtifactException>(
                () => MobaBattleDiagnosticArtifactCodec.ImportArtifact(json));

            Assert.That(exception.ErrorCode, Is.EqualTo(expectedErrorCode));
        }

        [Test]
        public void ImportSnapshot_RejectsUnsupportedSectionVersion()
        {
            var json = Export(CreateSnapshot()).Replace(
                AnalysisBattleDiagnosticSchema.Version,
                "abilitykit-battle-diagnostics.v2");

            var exception = Assert.Throws<MobaBattleDiagnosticArtifactException>(
                () => MobaBattleDiagnosticArtifactCodec.ImportSnapshot(json));

            Assert.That(exception.ErrorCode, Is.EqualTo("BattleDiagnostics.SchemaVersion"));
        }

        [Test]
        public void ImportSnapshot_RejectsInconsistentEventMetricsAndSequence()
        {
            var countArtifact = CreateArtifact(CreateSnapshot());
            countArtifact.BattleDiagnostics.Events.Metrics.Count = 1;
            var countException = Assert.Throws<MobaBattleDiagnosticArtifactException>(
                () => MobaBattleDiagnosticArtifactCodec.ImportSnapshot(
                    MobaBattleDiagnosticArtifactCodec.ExportToString(countArtifact)));
            Assert.That(countException.ErrorCode, Is.EqualTo("BattleDiagnostics.EventMetrics"));

            var sequenceArtifact = CreateArtifact(CreateSnapshot());
            sequenceArtifact.BattleDiagnostics.Events.Items[1].Sequence = 1;
            var sequenceException = Assert.Throws<MobaBattleDiagnosticArtifactException>(
                () => MobaBattleDiagnosticArtifactCodec.ImportSnapshot(
                    MobaBattleDiagnosticArtifactCodec.ExportToString(sequenceArtifact)));
            Assert.That(sequenceException.ErrorCode, Is.EqualTo("BattleDiagnostics.EventSequence"));
        }

        [Test]
        public void OfflineSession_UsesFrozenDisconnectedIdentityAndTrackRevisions()
        {
            using (var session = new BattleDiagnosticOfflineSession(CreateSnapshot()))
            {
                Assert.That(session.SessionInfo.ConnectionState, Is.EqualTo(BattleDiagnosticConnectionState.Disconnected));
                Assert.That(session.SessionInfo.CaptureState, Is.EqualTo(BattleDiagnosticCaptureState.Frozen));
                Assert.That(session.EventStoreRevision, Is.EqualTo(EventRevision));
                Assert.That(session.StateStoreRevision, Is.EqualTo(12));
                Assert.That(session.TraceStoreRevision, Is.EqualTo(13));
                Assert.That(session.ActorAttributeStoreRevision, Is.EqualTo(14));
                Assert.That(session.ActorBuffStoreRevision, Is.EqualTo(15));
                Assert.That(session.ActorTagStoreRevision, Is.EqualTo(16));
                Assert.That(session.ActorEffectStoreRevision, Is.EqualTo(17));
            }
        }

        [Test]
        public void OfflineSession_QueryEvents_AppliesFilterPagingAndFixedRevision()
        {
            using (var session = new BattleDiagnosticOfflineSession(CreateSnapshot()))
            {
                var filter = new BattleDiagnosticFilter(
                    new BattleDiagnosticFrameFilter(Frame, Frame),
                    BattleDiagnosticEventChannel.DamageAndHeal,
                    actorId: 2,
                    actorRelation: BattleDiagnosticActorRelation.Target,
                    failuresOnly: true,
                    searchText: "fire strike");
                var filtered = session.QueryEvents(new BattleDiagnosticEventQuery(
                    1,
                    filter,
                    new BattleDiagnosticPageRequest(0, 0, 10)));

                Assert.That(filtered.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Ready));
                Assert.That(filtered.Items.Count, Is.EqualTo(1));
                Assert.That(filtered.Items[0].Sequence, Is.EqualTo(1));

                var firstPage = session.QueryEvents(new BattleDiagnosticEventQuery(
                    2,
                    BattleDiagnosticFilter.Default,
                    new BattleDiagnosticPageRequest(0, 0, 1)));
                Assert.That(firstPage.Items.Count, Is.EqualTo(1));
                Assert.That(firstPage.Status.HasMore, Is.True);

                var stale = session.QueryEvents(new BattleDiagnosticEventQuery(
                    3,
                    BattleDiagnosticFilter.Default,
                    new BattleDiagnosticPageRequest(EventRevision + 1, 0, 1)));
                Assert.That(stale.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Unavailable));
                Assert.That(stale.Status.Availability, Is.EqualTo(BattleDiagnosticDataAvailability.Evicted));
            }
        }

        [Test]
        public void OfflineSession_QueryEvents_AppliesTriggerAnalysisFiltersAndSearch()
        {
            using (var session = new BattleDiagnosticOfflineSession(CreateSnapshot()))
            {
                var filter = BattleDiagnosticFilter.Default
                    .WithTriggerAnalysis(
                        BattleDiagnosticTriggerAnalysisStage.Conditions,
                        BattleDiagnosticTriggerAnalysisResult.Failed,
                        contextKind: 2,
                        originKind: 3)
                    .WithSearchText("missingMana");

                var filtered = session.QueryEvents(new BattleDiagnosticEventQuery(
                    10,
                    filter,
                    new BattleDiagnosticPageRequest(0, 0, 10)));

                Assert.That(filtered.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Ready));
                Assert.That(filtered.Items.Count, Is.EqualTo(1));
                Assert.That(filtered.Items[0].Sequence, Is.EqualTo(3));
            }
        }

        [Test]
        public void OfflineSession_QueryLatestTracks_HandlesLatestFrameAndMissingActor()
        {
            using (var session = new BattleDiagnosticOfflineSession(CreateSnapshot()))
            {
                var world = session.QueryWorld(1, 0);
                var actors = session.QueryActors(2, Frame);
                var attributes = session.QueryActorAttributes(3, 0, 1);
                var modifiers = session.QueryActorAttributeModifiers(4, Frame, 1);
                var buffs = session.QueryActorBuffs(5, 0, 1);
                var tags = session.QueryActorTags(6, 0, 1);
                var effects = session.QueryActorEffects(7, 0, 1);
                var wrongFrame = session.QueryActorBuffs(8, Frame - 1, 1);
                var missingActor = session.QueryActorTags(9, Frame, 99);

                Assert.That(world.Items.Single().Frame, Is.EqualTo(Frame));
                Assert.That(actors.Items.Count, Is.EqualTo(2));
                Assert.That(attributes.Items.Single().Name, Is.EqualTo("Attack"));
                Assert.That(modifiers.Items.Single().Magnitude, Is.EqualTo(5f));
                Assert.That(buffs.Items.Single().Name, Is.EqualTo("Power"));
                Assert.That(tags.Items.Single().Name, Is.EqualTo("Empowered"));
                Assert.That(effects.Items.Single().InstanceId, Is.EqualTo(501));
                Assert.That(wrongFrame.Status.Availability, Is.EqualTo(BattleDiagnosticDataAvailability.NotCaptured));
                Assert.That(missingActor.Status.Availability, Is.EqualTo(BattleDiagnosticDataAvailability.NotCaptured));
            }
        }

        [Test]
        public void OfflineSession_QueryTrace_ReportsReadyOrPartialFromSnapshotStability()
        {
            using (var stable = new BattleDiagnosticOfflineSession(CreateSnapshot()))
            using (var unstable = new BattleDiagnosticOfflineSession(CreateSnapshot(traceStable: false)))
            {
                var ready = stable.QueryTrace(1, 900);
                var partial = unstable.QueryTrace(2, 900);
                var empty = stable.QueryTrace(3, 999);

                Assert.That(ready.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Ready));
                Assert.That(ready.Items.Count, Is.EqualTo(2));
                Assert.That(partial.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Partial));
                Assert.That(partial.Status.Availability, Is.EqualTo(BattleDiagnosticDataAvailability.Truncated));
                Assert.That(empty.Status.Phase, Is.EqualTo(BattleDiagnosticQueryPhase.Empty));
            }
        }

        private AbilityKitAnalysisArtifact CreateArtifact(BattleDiagnosticSessionSnapshot snapshot)
        {
            return MobaBattleDiagnosticArtifactCodec.Attach(new AbilityKitAnalysisArtifact(), snapshot);
        }

        private string Export(BattleDiagnosticSessionSnapshot snapshot)
        {
            return MobaBattleDiagnosticArtifactCodec.ExportToString(CreateArtifact(snapshot));
        }

        private BattleDiagnosticSessionSnapshot CreateSnapshot(bool traceStable = true)
        {
            var info = new BattleDiagnosticSessionInfo(
                _scope,
                "Offline Battle",
                "build-42",
                1,
                TimeSpan.TicksPerSecond,
                BattleDiagnosticCapabilities.WorldState |
                BattleDiagnosticCapabilities.ActorState |
                BattleDiagnosticCapabilities.Events |
                BattleDiagnosticCapabilities.Trace |
                BattleDiagnosticCapabilities.ActorAttributes |
                BattleDiagnosticCapabilities.ActorBuffs |
                BattleDiagnosticCapabilities.ActorTags |
                BattleDiagnosticCapabilities.ActorEffects |
                BattleDiagnosticCapabilities.Export,
                BattleDiagnosticConnectionState.Connected,
                BattleDiagnosticCaptureState.Capturing);

            var syncData = new BattleDiagnosticSyncSnapshotReceivedPayload(Frame - 1, 0xAABBCCDDU);
            var syncPayload = BattleDiagnosticEventPayload.FromSyncSnapshotReceived(in syncData);
            var triggerData = new BattleDiagnosticTriggerAnalysisPayload(
                7001,
                contextKind: 2,
                originKind: 3,
                BattleDiagnosticTriggerAnalysisStage.Conditions,
                BattleDiagnosticTriggerAnalysisResult.Failed,
                detailCode: 11,
                currentDepth: 1,
                currentFrameCount: 2,
                currentRootCount: 3,
                currentSameTriggerCount: 4,
                failureKey: "missingMana",
                reason: "Missing mana for trigger.");
            var triggerPayload = BattleDiagnosticEventPayload.FromTriggerAnalysis(in triggerData);
            var failureData = new BattleDiagnosticSkillFailurePayload(
                slot: 2,
                source: "Cast",
                stage: "Preparation",
                code: "Cast.TargetOutOfRange",
                message: "Target is outside cast range.");
            var failurePayload = BattleDiagnosticEventPayload.FromSkillFailure(in failureData);
            var events = new[]
            {
                new BattleDiagnosticEvent(
                    _scope,
                    Frame,
                    1,
                    1000,
                    BattleDiagnosticEventKind.Damage,
                    BattleDiagnosticEventChannel.DamageAndHeal,
                    BattleDiagnosticEventOutcome.Failed,
                    sourceActorId: 1,
                    targetActorId: 2,
                    configId: 101,
                    rootContextId: 900,
                    contextId: 901,
                    skillRuntime: new BattleDiagnosticRuntimeHandle(700, 2),
                    attackId: 800,
                    summary: "Fire Strike failed"),
                new BattleDiagnosticEvent(
                    _scope,
                    Frame,
                    2,
                    1010,
                    BattleDiagnosticEventKind.Sync,
                    BattleDiagnosticEventChannel.Sync,
                    BattleDiagnosticEventOutcome.Succeeded,
                    sourceActorId: 1,
                    payloadVersion: BattleDiagnosticSyncSnapshotReceivedPayload.CurrentSchemaVersion,
                    summary: "Snapshot received",
                    payload: syncPayload),
                new BattleDiagnosticEvent(
                    _scope,
                    Frame,
                    3,
                    1020,
                    BattleDiagnosticEventKind.TriggerAnalysis,
                    BattleDiagnosticEventChannel.Effect,
                    BattleDiagnosticEventOutcome.Failed,
                    sourceActorId: 1,
                    targetActorId: 2,
                    configId: 7001,
                    rootContextId: 900,
                    contextId: 902,
                    payloadVersion: BattleDiagnosticTriggerAnalysisPayload.CurrentSchemaVersion,
                    summary: "Missing mana for trigger.",
                    payload: triggerPayload),
                new BattleDiagnosticEvent(
                    _scope,
                    Frame,
                    4,
                    1025,
                    BattleDiagnosticEventKind.SkillFailure,
                    BattleDiagnosticEventChannel.Skill,
                    BattleDiagnosticEventOutcome.Failed,
                    sourceActorId: 1,
                    targetActorId: 2,
                    configId: 101,
                    rootContextId: 900,
                    contextId: 900,
                    skillRuntime: new BattleDiagnosticRuntimeHandle(700, 2),
                    payloadVersion: BattleDiagnosticSkillFailurePayload.CurrentSchemaVersion,
                    summary: "Structured cast failure",
                    payload: failurePayload)
            };
            var metrics = new BattleDiagnosticStoreMetrics(8, events.Length, EventRevision, 2, 0, 1, true);
            var actors = new[]
            {
                new BattleDiagnosticActorSummary(_scope, Frame, 1, BattleDiagnosticActorKind.Hero, 101, 1, 1f, 2f, 3f, 90f, 100f, true, "Mage"),
                new BattleDiagnosticActorSummary(_scope, Frame, 2, BattleDiagnosticActorKind.Hero, 102, 2, 4f, 5f, 6f, 70f, 100f, true, "Target")
            };
            var traces = new[]
            {
                new BattleDiagnosticTraceNodeSummary(_scope, 900, 900, 0, Frame - 2, Frame, BattleDiagnosticTraceNodeState.Ended, 1, 101, "Skill", "Completed"),
                new BattleDiagnosticTraceNodeSummary(_scope, 900, 901, 900, Frame - 1, Frame, BattleDiagnosticTraceNodeState.Ended, 2, 201, "Damage", "Failed")
            };
            var attributes = new[]
            {
                new BattleDiagnosticActorAttribute(_scope, Frame, 1, 10, 20f, 25f, 1, "Attack")
            };
            var modifiers = new[]
            {
                new BattleDiagnosticActorAttributeModifier(_scope, Frame, 1, 10, 1, 5f, 10, 77, 2)
            };
            var buffs = new[]
            {
                new BattleDiagnosticActorBuff(_scope, Frame, 1, 301, 1, 2, 4f, 1f, 901, 902, 1, new BattleDiagnosticRuntimeHandle(700, 2), 900, 1, 3, "Power")
            };
            var tags = new[]
            {
                new BattleDiagnosticActorTag(_scope, Frame, 1, 401, "Empowered")
            };
            var effects = new[]
            {
                new BattleDiagnosticActorEffect(_scope, Frame, 1, 501, BattleDiagnosticEffectDurationPolicy.Duration, 1, 1f, 4f, true, 0.5f, true, 5f, 1f, 2, true)
            };
            var world = new BattleDiagnosticWorldSummary(_scope, Frame, 1020, actors.Length, 1, 1, "state-hash");

            return new BattleDiagnosticSessionSnapshot(
                in info,
                1030,
                new BattleDiagnosticEventTrackSnapshot(EventRevision, in metrics, events),
                new BattleDiagnosticStateTrackSnapshot(12, Frame, world, actors),
                new BattleDiagnosticTraceTrackSnapshot(13, traces, false, traceStable),
                new BattleDiagnosticAttributeTrackSnapshot(14, Frame, attributes, modifiers),
                new BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorBuff>(15, Frame, buffs),
                new BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorTag>(16, Frame, tags),
                new BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorEffect>(17, Frame, effects));
        }
    }
}

using System.Reflection;
using AbilityKit.Context;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Diagnostics;

public sealed class MobaEffectExecutionSnapshotTests
{
    private static readonly BattleDiagnosticSessionScope Scope = new("effect-facts", "world", 1);

    [Fact]
    public void Disabled_hook_does_not_access_payload_or_create_snapshots()
    {
        using var trace = new MobaTraceRegistry();
        using var store = new MobaEffectExecutionSnapshotStore(trace);
        var id = CreateEffect(trace);
        var payload = new StagePayload { Throws = true };
        var context = Context(payload);
        store.OnExecutionStarted(id, 101, 201, context);
        Assert.False(Reference(trace, id).IsValid);
        Assert.Equal(0, payload.ReadCount);
        Assert.Equal(0, store.Revision);
    }

    [Fact]
    public void Capture_follows_event_mode_channel_and_freeze_not_metrics_or_window_state()
    {
        using var trace = new MobaTraceRegistry();
        var collector = new MobaBattleDiagnosticEventCollector(Scope);
        using var store = new MobaEffectExecutionSnapshotStore(trace, collector);
        collector.CaptureMode = BattleDiagnosticCaptureMode.Metrics;
        Assert.False(store.IsEnabled);
        collector.CaptureMode = BattleDiagnosticCaptureMode.Events;
        collector.EnabledChannels = BattleDiagnosticEventChannel.Skill;
        Assert.True(store.IsEnabled);
        collector.SetFrozen(true);
        Assert.False(store.IsEnabled);
        var id = CreateEffect(trace);
        store.OnExecutionStarted(id, 101, 201, Context(new StagePayload()));
        Assert.False(Reference(trace, id).IsValid);
        collector.SetFrozen(false);
        collector.EnabledChannels = BattleDiagnosticEventChannel.None;
        Assert.False(store.IsEnabled);
        collector.EnabledChannels = BattleDiagnosticEventChannel.Skill;
        store.OnExecutionStarted(id, 101, 201, Context(new StagePayload()));
        Assert.True(Reference(trace, id).IsValid);
    }

    [Fact]
    public void Entry_facts_are_detached_read_once_and_never_overwritten()
    {
        using var trace = new MobaTraceRegistry();
        using var store = new MobaEffectExecutionSnapshotStore(trace, 8, () => true);
        var id = CreateEffect(trace);
        var payload = new StagePayload { StackCount = 3, Remaining = 4 };
        var context = Context(payload);
        store.OnExecutionStarted(id, 101, 201, context);
        var reference = Reference(trace, id);
        Assert.True(reference.IsValid);
        payload.StackCount = 99;
        payload.Remaining = 99;
        store.OnExecutionStarted(id, 102, 202, context);
        var facts = store.Read(reference);
        Assert.True(facts.IsCaptured);
        Assert.True(facts.HasStageSnapshot);
        Assert.Equal(3, facts.StackCount);
        Assert.Equal(4f, facts.RemainingSeconds);
        Assert.Equal(10, facts.Frame);
        Assert.Equal(101, facts.EffectConfigId);
        Assert.Equal(201, facts.TriggerId);
        Assert.True(facts.HasRuntimeContext);
        Assert.Equal(77, facts.RuntimeContextId);
        Assert.Equal(7, facts.RuntimeContextVersion);
        Assert.Equal("moba.effect.execution-entry", facts.TypeId);
        Assert.Equal(1, facts.SchemaVersion);
        Assert.Equal(1, payload.ReadCount);
        Assert.Equal(1, store.Revision);
    }

    [Fact]
    public void Zero_stage_is_present_and_missing_stage_is_not_reported_as_zero()
    {
        using var trace = new MobaTraceRegistry();
        using var store = new MobaEffectExecutionSnapshotStore(trace, 8, () => true);
        var zero = CreateEffect(trace);
        store.OnExecutionStarted(zero, 0, 201, Context(new StagePayload()));
        var facts = store.Read(Reference(trace, zero));
        Assert.True(facts.HasStageSnapshot);
        Assert.Equal(0, facts.StackCount);
        Assert.Equal(0, facts.EffectConfigId); // Direct trigger IDs are not invented effect config IDs.
        var absent = CreateEffect(trace);
        store.OnExecutionStarted(absent, 101, 201, Context(new object()));
        Assert.False(store.Read(Reference(trace, absent)).HasStageSnapshot);
        Assert.False(store.Read(Reference(trace, absent)).HasRuntimeContext);
    }

    [Fact]
    public void Provider_failure_and_ended_or_wrong_nodes_do_not_change_execution_state()
    {
        using var trace = new MobaTraceRegistry();
        using var store = new MobaEffectExecutionSnapshotStore(trace, 8, () => true);
        var id = CreateEffect(trace);
        var context = Context(new StagePayload { Throws = true });
        Assert.Null(Record.Exception(() => store.OnExecutionStarted(id, 101, 201, context)));
        Assert.False(Reference(trace, id).IsValid);
        Assert.True(trace.TryGetNodeSnapshot(id, out var node));
        Assert.False(node.IsEnded);
        trace.EndContext(id);
        Assert.False(store.TryCapture(id, 101, 201, Context(new StagePayload())));
        var action = trace.CreateRootContext(MobaTraceKind.EffectAction, 301);
        Assert.False(store.TryCapture(action, 101, 201, Context(new StagePayload())));
    }

    [Fact]
    public void Capacity_eviction_never_returns_another_execution_facts()
    {
        using var trace = new MobaTraceRegistry();
        using var store = new MobaEffectExecutionSnapshotStore(trace, 1, () => true);
        var first = CreateEffect(trace);
        store.OnExecutionStarted(first, 101, 201, Context(new StagePayload { StackCount = 1 }));
        var old = Reference(trace, first);
        var second = CreateEffect(trace);
        store.OnExecutionStarted(second, 101, 201, Context(new StagePayload { StackCount = 2 }));
        Assert.Equal(BattleDiagnosticDataAvailability.Evicted, store.Read(old).Availability);
        Assert.Equal(2, store.Read(Reference(trace, second)).StackCount);
        Assert.True(trace.Contains(first));
    }

    [Fact]
    public void Purge_prediction_retraction_clear_and_dispose_invalidate_owned_records()
    {
        using var trace = new MobaTraceRegistry();
        using var store = new MobaEffectExecutionSnapshotStore(trace, 8, () => true);
        var confirmed = CreateEffect(trace);
        store.OnExecutionStarted(confirmed, 101, 201, Context(new StagePayload()));
        var kept = Reference(trace, confirmed);
        var boundary = trace.NextContextId;
        var predicted = trace.CreateChildContext(confirmed, MobaTraceKind.EffectExecution, 101);
        store.OnExecutionStarted(predicted, 101, 201, Context(new StagePayload()));
        var removed = Reference(trace, predicted);
        trace.RetractPrediction(boundary);
        Assert.True(store.Read(kept).IsCaptured);
        Assert.Equal(BattleDiagnosticDataAvailability.Evicted, store.Read(removed).Availability);
        trace.PurgeRoot(confirmed);
        Assert.Equal(BattleDiagnosticDataAvailability.Evicted, store.Read(kept).Availability);

        var beforeClear = CreateEffect(trace);
        store.OnExecutionStarted(beforeClear, 101, 201, Context(new StagePayload()));
        var stale = Reference(trace, beforeClear);
        trace.Clear();
        var replay = CreateEffect(trace);
        store.OnExecutionStarted(replay, 101, 201, Context(new StagePayload()));
        var fresh = Reference(trace, replay);
        Assert.True(fresh.SnapshotId > stale.SnapshotId);
        Assert.True(fresh.Generation > stale.Generation);
        Assert.Equal(BattleDiagnosticDataAvailability.Evicted, store.Read(stale).Availability);
        store.Dispose();
        Assert.False(store.IsEnabled);
        Assert.Equal(BattleDiagnosticDataAvailability.Evicted, store.Read(fresh).Availability);
        Assert.False(store.TryCapture(replay, 101, 201, Context(new StagePayload())));
    }

    [Fact]
    public void Query_revision_and_artifact_roundtrip_preserve_facts_and_legacy_missing_state()
    {
        using var trace = new MobaTraceRegistry();
        using var store = new MobaEffectExecutionSnapshotStore(trace, 8, () => true);
        var collector = new MobaBattleDiagnosticEventCollector(Scope);
        var reader = new MobaBattleDiagnosticTraceReadStore(trace, collector.Store);
        typeof(MobaBattleDiagnosticTraceReadStore).GetField("_executionSnapshots", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(reader, store);
        var id = CreateEffect(trace);
        var revision = reader.Revision;
        store.OnExecutionStarted(id, 101, 201, Context(new StagePayload { StackCount = 3 }));
        Assert.True(reader.Revision > revision);
        var snapshot = reader.CaptureTraceSnapshot();
        var node = Assert.Single(snapshot.Nodes);
        Assert.Equal(3, node.ExecutionFacts.StackCount);
        var section = MobaBattleDiagnosticArtifactCodec.ToSection(CreateSnapshot(snapshot));
        var restored = MobaBattleDiagnosticArtifactCodec.FromSection(section);
        Assert.Equal(node, Assert.Single(restored.Trace.Nodes));
        var serializer = new MobaBattleDiagnosticJsonArtifactSerializer();
        var wire = serializer.Serialize(CreateSnapshot(snapshot));
        Assert.Equal(node, Assert.Single(serializer.Deserialize(wire).Trace.Nodes));
        section.Trace.Nodes[0].ExecutionFacts = null;
        var legacy = MobaBattleDiagnosticArtifactCodec.FromSection(section);
        Assert.Equal(BattleDiagnosticDataAvailability.NotCaptured, Assert.Single(legacy.Trace.Nodes).ExecutionFacts.Availability);
    }

    private static long CreateEffect(MobaTraceRegistry trace) => trace.CreateRootContext(MobaTraceKind.EffectExecution, 101);
    private static ContextSnapshotReference Reference(MobaTraceRegistry trace, long id)
    {
        Assert.True(trace.TryGetNodeSnapshot(id, out var node));
        return ((MobaTraceMetadata)node.Metadata).ExecutionSnapshot;
    }
    private static MobaCombatExecutionContext Context(object payload) => new(payload, default, default, default, default, 10);
    private static BattleDiagnosticSessionSnapshot CreateSnapshot(BattleDiagnosticTraceTrackSnapshot trace)
    {
        var info = new BattleDiagnosticSessionInfo(Scope, "Facts", "test", 1, TimeSpan.TicksPerSecond,
            BattleDiagnosticCapabilities.Trace | BattleDiagnosticCapabilities.Export,
            BattleDiagnosticConnectionState.Connected, BattleDiagnosticCaptureState.Capturing);
        var metrics = new BattleDiagnosticStoreMetrics(16, 0, 1, 0, 0, 0, true);
        return new BattleDiagnosticSessionSnapshot(info, 10,
            new BattleDiagnosticEventTrackSnapshot(1, metrics, Array.Empty<BattleDiagnosticEvent>()),
            new BattleDiagnosticStateTrackSnapshot(0, -1, null, Array.Empty<BattleDiagnosticActorSummary>()), trace,
            new BattleDiagnosticAttributeTrackSnapshot(0, -1, Array.Empty<BattleDiagnosticActorAttribute>(), Array.Empty<BattleDiagnosticActorAttributeModifier>()),
            new BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorBuff>(0, -1, Array.Empty<BattleDiagnosticActorBuff>()),
            new BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorTag>(0, -1, Array.Empty<BattleDiagnosticActorTag>()),
            new BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorEffect>(0, -1, Array.Empty<BattleDiagnosticActorEffect>()));
    }

    private sealed class StagePayload : IMobaTriggerStageSnapshotProvider, IMobaRuntimeContextPayload
    {
        public int StackCount;
        public float Remaining;
        public int ReadCount;
        public bool Throws;
        public bool TryGetStageSnapshot(out MobaTriggerStageSnapshot snapshot)
        {
            ReadCount++;
            if (Throws) throw new InvalidOperationException("snapshot provider failed");
            snapshot = new MobaTriggerStageSnapshot(StackCount, remainingSeconds: Remaining);
            return true;
        }
        public bool TryGetRuntimeContext(out MobaRuntimeContextReference reference)
        {
            reference = new MobaRuntimeContextReference(77, 7);
            return true;
        }
    }
}

using System.Reflection;
using AbilityKit.Context;
using AbilityKit.Core.Eventing;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Diagnostics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Triggering.Eventing;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Diagnostics;

public sealed class MobaActionExecutionSnapshotTests
{
    private static readonly BattleDiagnosticSessionScope Scope = new("action-facts", "world", 1);
    private static readonly EventKey<MobaHealthChangeResult> Key = new(TriggeringIdUtil.GetEventEid(DamagePipelineEvents.HealthCommitted));

    [Fact]
    public void Before_after_are_frozen_and_absent_resources_are_not_zero()
    {
        using var trace = new MobaTraceRegistry();
        using var actors = new MobaActorRegistry();
        var actor = new ActorContext().CreateEntity();
        var hp = new ResourceState { Current = 0 };
        actor.AddResourceContainer(new ResourceContainer { Map = new() { [ResourceType.Hp] = hp } }, true);
        actors.Register(1, actor);
        using var store = Store(trace, actors);
        var id = Action(trace);
        store.OnActionStarted(id, 0, 301, 1, 2, 10);
        var entry = Reference(trace, id);
        hp.Current = 20;
        store.OnActionEnded(id, 0, 301, true, false, 11);
        var facts = store.Read(Reference(trace, id));
        hp.Current = 99;
        Assert.Equal(0f, facts.SourceBefore.Hp);
        Assert.True(facts.SourceBefore.HasHp);
        Assert.False(facts.SourceBefore.HasMana);
        Assert.False(facts.TargetBefore.HasActor);
        Assert.Equal(20f, facts.SourceAfter.Hp);
        Assert.True(facts.SourceBefore.IsSameBinding(facts.SourceAfter));
        Assert.False(store.Read(entry).HasAfter);
        Assert.True(facts.HasAfter);
        Assert.Equal(BattleDiagnosticActionOutcome.Completed, facts.Outcome);
        Assert.Empty(facts.Commits);
        Assert.False(facts.CommitsComplete); // No event bus is not proof of zero commits.
        Assert.Equal(10, facts.Frame);
        Assert.Equal(11, facts.EndFrame);
    }

    [Fact]
    public void Reads_do_not_create_missing_components_and_rebinding_prevents_delta()
    {
        using var trace = new MobaTraceRegistry();
        using var actors = new MobaActorRegistry();
        var first = new ActorContext().CreateEntity();
        var replacement = new ActorContext().CreateEntity(); // Same creationIndex in a different context.
        actors.Register(1, first);
        using var store = Store(trace, actors);
        var id = Action(trace);
        store.OnActionStarted(id, 0, 301, 1, 1, 10);
        actors.Register(1, replacement);
        store.OnActionEnded(id, 0, 301, true, false, 10);
        var facts = store.Read(Reference(trace, id));
        Assert.False(first.hasResourceContainer);
        Assert.False(replacement.hasResourceContainer);
        Assert.True(facts.SourceBefore.HasActor);
        Assert.True(facts.SourceAfter.HasActor);
        Assert.False(facts.SourceBefore.IsSameBinding(facts.SourceAfter));
    }

    [Theory]
    [InlineData(true, false, BattleDiagnosticActionOutcome.Completed)]
    [InlineData(false, false, BattleDiagnosticActionOutcome.Failed)]
    [InlineData(false, true, BattleDiagnosticActionOutcome.Aborted)]
    public void Call_outcome_is_independent_of_real_commits(bool succeeded, bool aborted, BattleDiagnosticActionOutcome expected)
    {
        using var trace = new MobaTraceRegistry();
        var bus = new EventBus();
        using var store = Store(trace, bus: bus);
        var id = Action(trace);
        store.OnActionStarted(id, 0, 301, 1, 2, 10);
        var result = Commit(trace, id, target: 7); // Result targets can differ from the context target.
        bus.Publish(Key, in result);
        store.OnActionEnded(id, 0, 301, succeeded, aborted, 10);
        trace.EndContext(id);
        var facts = store.Read(Reference(trace, id));
        Assert.Equal(expected, facts.Outcome);
        Assert.True(facts.CommitsComplete);
        Assert.Equal(7, Assert.Single(facts.Commits).TargetActorId);
        Assert.Equal(4f, facts.Commits[0].AppliedValue);
        Assert.Equal(100f, facts.Commits[0].RequestedValue);
    }

    [Fact]
    public void Nested_action_results_belong_only_to_nearest_real_ancestor()
    {
        using var trace = new MobaTraceRegistry();
        var bus = new EventBus();
        using var store = Store(trace, bus: bus);
        var outer = Action(trace);
        store.OnActionStarted(outer, 0, 301, 1, 2, 10);
        var effect = trace.CreateChildContext(outer, MobaTraceKind.EffectExecution, 101);
        var inner = trace.CreateChildContext(effect, MobaTraceKind.EffectAction, 302);
        store.OnActionStarted(inner, 1, 302, 1, 3, 10);
        var apply = trace.CreateChildContext(inner, MobaTraceKind.DamageApply, 0);
        var result = Commit(trace, apply, target: 3);
        bus.Publish(Key, in result);
        store.OnActionEnded(inner, 1, 302, false, false, 10);
        var unsampled = trace.CreateChildContext(effect, MobaTraceKind.EffectAction, 303);
        var ignored = Commit(trace, unsampled);
        bus.Publish(Key, in ignored);
        var missing = Commit(trace, 99999);
        bus.Publish(Key, in missing);
        var wrongRoot = Commit(trace, outer, root: outer + 10000);
        bus.Publish(Key, in wrongRoot);
        store.OnActionEnded(outer, 0, 301, true, false, 10);
        Assert.Single(store.Read(Reference(trace, inner)).Commits);
        Assert.Empty(store.Read(Reference(trace, outer)).Commits);
    }

    [Fact]
    public void Actual_zero_commits_is_not_a_gameplay_failure_and_result_list_is_bounded()
    {
        using var trace = new MobaTraceRegistry();
        var bus = new EventBus();
        using var store = Store(trace, bus: bus);
        var empty = Action(trace);
        store.OnActionStarted(empty, 0, 301, 1, 2, 10);
        store.OnActionEnded(empty, 0, 301, true, false, 10);
        var facts = store.Read(Reference(trace, empty));
        Assert.True(facts.CommitsComplete);
        Assert.Empty(facts.Commits);
        Assert.Equal(BattleDiagnosticActionOutcome.Completed, facts.Outcome);
        var id = Action(trace);
        store.OnActionStarted(id, 0, 301, 1, 2, 10);
        var result = Commit(trace, id);
        for (var i = 0; i < 40; i++) bus.Publish(Key, in result);
        store.OnActionEnded(id, 0, 301, true, false, 10);
        facts = store.Read(Reference(trace, id));
        Assert.Equal(32, facts.Commits.Count);
        Assert.True(facts.CommitsTruncated);
        Assert.IsNotType<BattleDiagnosticActionHealthCommit[]>(facts.Commits);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Capture_switches_mid_action_mark_result_coverage_incomplete(int change)
    {
        using var trace = new MobaTraceRegistry();
        var bus = new EventBus();
        var collector = Collector();
        using var store = new MobaActionExecutionSnapshotStore(trace, null, collector, bus);
        var id = Action(trace);
        store.OnActionStarted(id, 0, 301, 1, 2, 10);
        if (change == 0) { collector.SetFrozen(true); collector.SetFrozen(false); }
        if (change == 1) { collector.CaptureMode = BattleDiagnosticCaptureMode.Metrics; collector.CaptureMode = BattleDiagnosticCaptureMode.Events; }
        if (change == 2) { collector.EnabledChannels = BattleDiagnosticEventChannel.None; collector.EnabledChannels = BattleDiagnosticEventChannel.Skill; }
        var result = Commit(trace, id);
        bus.Publish(Key, in result);
        store.OnActionEnded(id, 0, 301, true, false, 10);
        var facts = store.Read(Reference(trace, id));
        Assert.True(facts.HasAfter);
        Assert.False(facts.CommitsComplete);
        Assert.Single(facts.Commits);
    }

    [Fact]
    public void Disabled_start_is_not_backfilled_and_frozen_end_keeps_entry_only()
    {
        using var trace = new MobaTraceRegistry();
        var collector = Collector();
        collector.CaptureMode = BattleDiagnosticCaptureMode.Metrics;
        using var store = new MobaActionExecutionSnapshotStore(trace, null, collector, new EventBus());
        var absent = Action(trace);
        store.OnActionStarted(absent, 0, 301, 1, 2, 10);
        Assert.False(Reference(trace, absent).IsValid);
        collector.CaptureMode = BattleDiagnosticCaptureMode.Events;
        store.OnActionEnded(absent, 0, 301, true, false, 10);
        Assert.False(Reference(trace, absent).IsValid);
        var id = Action(trace);
        store.OnActionStarted(id, 0, 301, 1, 2, 10);
        var entry = Reference(trace, id);
        collector.SetFrozen(true);
        store.OnActionEnded(id, 0, 301, false, false, 10);
        collector.SetFrozen(false);
        store.OnActionEnded(id, 0, 301, true, false, 10);
        Assert.Equal(entry, Reference(trace, id));
        Assert.False(store.Read(entry).HasAfter);
    }

    [Fact]
    public void Queued_delivery_is_not_reported_as_complete_and_observer_never_flushes_business_events()
    {
        using var trace = new MobaTraceRegistry();
        var bus = new EventBus(new EventBusOptions(EEventDispatchMode.Queued, 16));
        using var store = Store(trace, bus: bus);
        var received = 0;
        using var subscription = bus.Subscribe(Key, _ => received++);
        var id = Action(trace);
        store.OnActionStarted(id, 0, 301, 1, 2, 10);
        var result = Commit(trace, id);
        bus.Publish(Key, in result);
        store.OnActionEnded(id, 0, 301, true, false, 10);
        var facts = store.Read(Reference(trace, id));
        Assert.False(facts.CommitsComplete);
        Assert.Empty(facts.Commits);
        Assert.Equal(0, received);
        bus.Flush(); // Only the normal event-loop owner may flush.
        Assert.Equal(1, received);
        Assert.Equal(facts, store.Read(Reference(trace, id)));
    }

    [Fact]
    public void Pairing_rejects_wrong_actions_duplicates_and_ended_nodes()
    {
        using var trace = new MobaTraceRegistry();
        using var store = Store(trace);
        var id = Action(trace);
        store.OnActionStarted(id, 0, 302, 1, 2, 10);
        Assert.False(Reference(trace, id).IsValid);
        store.OnActionStarted(id, 0, 301, 1, 2, 10);
        var entry = Reference(trace, id);
        store.OnActionStarted(id, 1, 302, 3, 4, 10);
        store.OnActionEnded(id, 1, 302, true, false, 10);
        Assert.Equal(entry, Reference(trace, id));
        store.OnActionEnded(id, 0, 301, false, false, 11);
        var end = Reference(trace, id);
        store.OnActionEnded(id, 0, 301, true, false, 12);
        Assert.Equal(end, Reference(trace, id));
        Assert.Equal(BattleDiagnosticActionOutcome.Failed, store.Read(end).Outcome);
        var externalEnd = Action(trace);
        store.OnActionStarted(externalEnd, 0, 301, 1, 2, 10);
        trace.EndContext(externalEnd);
        store.OnActionEnded(externalEnd, 0, 301, true, false, 10);
        Assert.False(store.Read(Reference(trace, externalEnd)).HasAfter);
    }

    [Fact]
    public void Subscription_failure_leaves_resource_observation_usable_without_affecting_execution()
    {
        using var trace = new MobaTraceRegistry();
        using var store = new MobaActionExecutionSnapshotStore(trace, null, Collector(), new ThrowingSubscriptionBus());
        var id = Action(trace);
        store.OnActionStarted(id, 0, 301, 1, 2, 10);
        store.OnActionEnded(id, 0, 301, true, false, 10);
        var facts = store.Read(Reference(trace, id));
        Assert.True(facts.HasAfter);
        Assert.False(facts.CommitsComplete);
    }

    [Fact]
    public void Eviction_purge_retraction_clear_and_dispose_invalidate_exact_references()
    {
        using var trace = new MobaTraceRegistry();
        var bus = new EventBus();
        using var store = Store(trace, bus: bus, capacity: 2);
        var first = Action(trace);
        store.OnActionStarted(first, 0, 301, 1, 2, 10);
        store.OnActionEnded(first, 0, 301, true, false, 10);
        var old = Reference(trace, first);
        var boundary = trace.NextContextId;
        var second = trace.CreateChildContext(first, MobaTraceKind.EffectAction, 301);
        store.OnActionStarted(second, 0, 301, 1, 2, 10);
        store.OnActionEnded(second, 0, 301, true, false, 10);
        var removed = Reference(trace, second);
        Assert.Equal(BattleDiagnosticDataAvailability.Evicted, store.Read(old).Availability);
        trace.RetractPrediction(boundary);
        Assert.Equal(BattleDiagnosticDataAvailability.Evicted, store.Read(removed).Availability);
        var third = trace.CreateChildContext(first, MobaTraceKind.EffectAction, 301);
        store.OnActionStarted(third, 0, 301, 1, 2, 10);
        var purged = Reference(trace, third);
        trace.PurgeRoot(first);
        Assert.Equal(BattleDiagnosticDataAvailability.Evicted, store.Read(purged).Availability);
        var beforeClear = Action(trace);
        store.OnActionStarted(beforeClear, 0, 301, 1, 2, 10);
        var stale = Reference(trace, beforeClear);
        trace.Clear();
        var replay = Action(trace);
        store.OnActionStarted(replay, 0, 301, 1, 2, 10);
        var fresh = Reference(trace, replay);
        Assert.True(fresh.Generation > stale.Generation);
        Assert.True(fresh.SnapshotId > stale.SnapshotId);
        store.OnActionEnded(replay, 0, 301, true, false, 10);
        fresh = Reference(trace, replay);
        store.Dispose();
        Assert.False(bus.HasSubscribers(Key));
        Assert.Equal(BattleDiagnosticDataAvailability.Evicted, store.Read(fresh).Availability);
        store.OnActionStarted(Action(trace), 0, 301, 1, 2, 10);
    }

    [Fact]
    public void Evicted_pending_entry_never_captures_later_live_values()
    {
        using var trace = new MobaTraceRegistry();
        using var store = Store(trace, capacity: 1);
        var first = Action(trace);
        store.OnActionStarted(first, 0, 301, 1, 2, 10);
        var old = Reference(trace, first);
        var second = Action(trace);
        store.OnActionStarted(second, 0, 301, 1, 2, 10);
        Assert.True(Reference(trace, second).IsValid);
        Assert.Equal(BattleDiagnosticDataAvailability.Evicted, store.Read(old).Availability);
        store.OnActionEnded(first, 0, 301, true, false, 10);
        store.OnActionStarted(second, 0, 301, 1, 2, 10);
        Assert.Equal(BattleDiagnosticDataAvailability.Evicted, store.Read(old).Availability);
        store.OnActionEnded(first, 0, 301, true, false, 10);
        Assert.Equal(BattleDiagnosticDataAvailability.Evicted, store.Read(old).Availability);
    }

    [Fact]
    public void Trace_projection_and_artifact_roundtrip_preserve_action_facts_and_legacy_defaults()
    {
        using var trace = new MobaTraceRegistry();
        var collector = Collector();
        collector.EnabledChannels |= BattleDiagnosticEventChannel.DamageAndHeal;
        using var store = new MobaActionExecutionSnapshotStore(trace, null, collector, new EventBus());
        var reader = new MobaBattleDiagnosticTraceReadStore(trace, collector.Store);
        typeof(MobaBattleDiagnosticTraceReadStore).GetField("_actionSnapshots", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(reader, store);
        var id = Action(trace);
        var revision = reader.Revision;
        store.OnActionStarted(id, 0, 301, 1, 2, 10);
        store.OnHealthCommitted(Commit(trace, id));
        var draft = DamageDraft(trace, id, BattleDiagnosticDamageStage.Completed, 20, 20, 0, 0);
        Assert.True(collector.TryCollect(draft));
        store.OnActionEnded(id, 0, 301, false, false, 11);
        Assert.True(reader.Revision > revision);
        var track = reader.CaptureTraceSnapshot();
        var snapshot = Snapshot(track);
        var serializer = new MobaBattleDiagnosticJsonArtifactSerializer();
        var restored = serializer.Deserialize(serializer.Serialize(snapshot));
        Assert.Equal(Assert.Single(track.Nodes), Assert.Single(restored.Trace.Nodes));
        var damage = Assert.Single(Assert.Single(restored.Trace.Nodes).ActionFacts.DamageResults);
        Assert.Equal(BattleDiagnosticActionDamageOutcome.FullyAbsorbed, damage.Outcome);
        Assert.Equal(2, Assert.Single(restored.Trace.Nodes).ActionFacts.SchemaVersion);
        var section = MobaBattleDiagnosticArtifactCodec.ToSection(snapshot);
        section.Trace.Nodes[0].ActionFacts.DamageResults = null;
        section.Trace.Nodes[0].ActionFacts.DamageAvailability = (int)BattleDiagnosticDataAvailability.NotCaptured;
        section.Trace.Nodes[0].ActionFacts.DamageCoverageContinuous = false;
        var legacyAction = Assert.Single(MobaBattleDiagnosticArtifactCodec.FromSection(section).Trace.Nodes).ActionFacts;
        Assert.Equal(BattleDiagnosticDataAvailability.NotCaptured, legacyAction.DamageAvailability);
        Assert.Empty(legacyAction.DamageResults);
        section.Trace.Nodes[0].ActionFacts = null;
        Assert.Equal(BattleDiagnosticDataAvailability.NotCaptured,
            Assert.Single(MobaBattleDiagnosticArtifactCodec.FromSection(section).Trace.Nodes).ActionFacts.Availability);
    }

    [Theory]
    [InlineData(BattleDiagnosticDamageStage.Completed, 20, 20, 0, 0, BattleDiagnosticActionDamageOutcome.FullyAbsorbed)]
    [InlineData(BattleDiagnosticDamageStage.Completed, 0, 0, 0, 0, BattleDiagnosticActionDamageOutcome.NoHpDamage)]
    [InlineData(BattleDiagnosticDamageStage.Completed, 20, 0, 0, 0, BattleDiagnosticActionDamageOutcome.NoHpDamage)]
    [InlineData(BattleDiagnosticDamageStage.Completed, 20, 5, 15, 15, BattleDiagnosticActionDamageOutcome.Applied)]
    [InlineData(BattleDiagnosticDamageStage.Completed, 20, 0, 20, 0, BattleDiagnosticActionDamageOutcome.Unknown)]
    [InlineData(BattleDiagnosticDamageStage.TargetMissing, 0, 0, 0, 0, BattleDiagnosticActionDamageOutcome.Rejected)]
    [InlineData(BattleDiagnosticDamageStage.InvalidRequest, 0, 0, 0, 0, BattleDiagnosticActionDamageOutcome.Rejected)]
    [InlineData(BattleDiagnosticDamageStage.TransactionRejected, 0, 0, 0, 0, BattleDiagnosticActionDamageOutcome.Rejected)]
    [InlineData(BattleDiagnosticDamageStage.ShieldCommitRejected, 20, 5, 15, 0, BattleDiagnosticActionDamageOutcome.Rejected)]
    [InlineData(BattleDiagnosticDamageStage.HealthCommitRejected, 20, 5, 15, 0, BattleDiagnosticActionDamageOutcome.Rejected)]
    [InlineData(BattleDiagnosticDamageStage.ExecutionFailed, 0, 0, 0, 0, BattleDiagnosticActionDamageOutcome.ExecutionFailed)]
    [InlineData(BattleDiagnosticDamageStage.PostCommitNotificationFailed, 0, 0, 0, 0, BattleDiagnosticActionDamageOutcome.PostCommitNotificationFailed)]
    public void Damage_facts_classify_frozen_terminal_values_without_reading_live_state(BattleDiagnosticDamageStage stage,
        int mitigated, int shield, int planned, int applied, BattleDiagnosticActionDamageOutcome expected)
    {
        using var trace = new MobaTraceRegistry();
        var collector = Collector();
        collector.EnabledChannels |= BattleDiagnosticEventChannel.DamageAndHeal;
        using var store = new MobaActionExecutionSnapshotStore(trace, null, collector);
        var id = Action(trace);
        store.OnActionStarted(id, 0, 301, 1, 2, 10);
        Assert.True(collector.TryCollect(DamageDraft(trace, id, stage, mitigated, shield, planned, applied)));
        store.OnActionEnded(id, 0, 301, true, false, 10);
        var facts = store.Read(Reference(trace, id));
        Assert.True(facts.DamageCoverageContinuous);
        Assert.Empty(facts.Commits);
        Assert.Equal(expected, Assert.Single(facts.DamageResults).Outcome);
        Assert.Equal(stage, facts.DamageResults[0].Calculation.Stage);
        Assert.Equal(BattleDiagnosticActionOutcome.Completed, facts.Outcome);
    }

    [Fact]
    public void Damage_results_copy_accepted_events_survive_event_eviction_and_bound_record_count()
    {
        using var trace = new MobaTraceRegistry();
        var collector = new MobaBattleDiagnosticEventCollector(Scope, 1, () => 10);
        using var store = new MobaActionExecutionSnapshotStore(trace, null, collector);
        var id = Action(trace);
        store.OnActionStarted(id, 0, 301, 1, 2, 10);
        var draft = DamageDraft(trace, id, BattleDiagnosticDamageStage.Completed, 0, 0, 0, 0);
        for (var i = 0; i < 40; i++) Assert.True(collector.TryCollect(draft));
        store.OnActionEnded(id, 0, 301, true, false, 10);
        var facts = store.Read(Reference(trace, id));
        Assert.Equal(32, facts.DamageResults.Count);
        Assert.Equal(1, facts.DamageResults[0].Sequence);
        Assert.Equal(32, facts.DamageResults[31].Sequence);
        Assert.True(facts.DamageResultsTruncated);
        Assert.IsNotType<BattleDiagnosticActionDamageResult[]>(facts.DamageResults);
    }

    [Fact]
    public void Damage_observation_never_attributes_unsampled_nested_or_other_root_events_to_outer_action()
    {
        using var trace = new MobaTraceRegistry();
        var collector = new MobaBattleDiagnosticEventCollector(Scope, frameProvider: () => 10);
        using var store = new MobaActionExecutionSnapshotStore(trace, null, collector);
        var outer = Action(trace);
        var inner = trace.CreateChildContext(outer, MobaTraceKind.EffectAction, 302);
        store.OnActionStarted(outer, 0, 301, 1, 2, 10);
        Assert.True(collector.TryCollect(DamageDraft(trace, inner, BattleDiagnosticDamageStage.TargetMissing)));
        store.OnActionStarted(inner, 1, 302, 1, 2, 10);
        var child = trace.CreateChildContext(inner, MobaTraceKind.DamageApply, 0);
        Assert.True(collector.TryCollect(DamageDraft(trace, child, BattleDiagnosticDamageStage.HealthCommitRejected)));
        var wrong = DamageDraft(trace, outer, BattleDiagnosticDamageStage.TargetMissing, root: outer + 9000);
        Assert.True(collector.TryCollect(wrong));
        store.OnActionEnded(inner, 1, 302, false, false, 10);
        store.OnActionEnded(outer, 0, 301, true, false, 10);
        Assert.Single(store.Read(Reference(trace, inner)).DamageResults);
        Assert.Empty(store.Read(Reference(trace, outer)).DamageResults);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Damage_channel_capture_gaps_and_collection_failure_do_not_claim_continuous_coverage(int change)
    {
        using var trace = new MobaTraceRegistry();
        var collector = new MobaBattleDiagnosticEventCollector(Scope, frameProvider: () => 10);
        using var store = new MobaActionExecutionSnapshotStore(trace, null, collector);
        var id = Action(trace);
        if (change == 0) collector.EnabledChannels = BattleDiagnosticEventChannel.Skill;
        store.OnActionStarted(id, 0, 301, 1, 2, 10);
        if (change == 0) collector.EnabledChannels |= BattleDiagnosticEventChannel.DamageAndHeal;
        if (change == 1) { collector.SetFrozen(true); collector.SetFrozen(false); }
        if (change == 2)
        {
            collector.Store.SetFrozen(true);
            Assert.False(collector.TryCollect(DamageDraft(trace, id, BattleDiagnosticDamageStage.TargetMissing)));
            collector.Store.SetFrozen(false);
        }
        Assert.True(collector.TryCollect(DamageDraft(trace, id, BattleDiagnosticDamageStage.TargetMissing)));
        store.OnActionEnded(id, 0, 301, true, false, 10);
        var facts = store.Read(Reference(trace, id));
        Assert.False(facts.DamageCoverageContinuous);
        Assert.Single(facts.DamageResults);
    }

    [Fact]
    public void Throwing_event_observer_does_not_change_acceptance_or_block_damage_observation()
    {
        using var trace = new MobaTraceRegistry();
        var collector = new MobaBattleDiagnosticEventCollector(Scope, frameProvider: () => 10);
        collector.EventCollected += _ => throw new InvalidOperationException("observer failure");
        using var store = new MobaActionExecutionSnapshotStore(trace, null, collector);
        var id = Action(trace);
        store.OnActionStarted(id, 0, 301, 1, 2, 10);
        Assert.True(collector.TryCollect(DamageDraft(trace, id, BattleDiagnosticDamageStage.TargetMissing)));
        Assert.Equal(0, collector.CollectFailureCount);
        store.OnActionEnded(id, 0, 301, true, false, 10);
        Assert.Single(store.Read(Reference(trace, id)).DamageResults);
        store.Dispose();
        var revision = store.Revision;
        Assert.True(collector.TryCollect(DamageDraft(trace, id, BattleDiagnosticDamageStage.TargetMissing)));
        Assert.Equal(revision, store.Revision);
    }

    [Fact]
    public void Damage_details_are_bounded_and_default_values_roundtrip_as_empty_details()
    {
        var c = new BattleDiagnosticDamageCalculationPayload(BattleDiagnosticDamageStage.ExecutionFailed, 0, 0, 0, 0, 0, 0);
        var d = new BattleDiagnosticActionDamageResult(1, 10, 1, 2, 3, c, new string('x', 1024));
        Assert.Equal(256, d.Detail.Length);
        Assert.Equal(default(BattleDiagnosticActionDamageResult), new BattleDiagnosticActionDamageResult(0, 0, 0, 0, 0, default, ""));
    }

    private static MobaBattleDiagnosticEventDraft DamageDraft(MobaTraceRegistry trace, long context,
        BattleDiagnosticDamageStage stage, int mitigated = 0, int shield = 0, int planned = 0, int applied = 0, long root = 0)
    {
        if (root == 0 && trace.TryGetNodeSnapshot(context, out var node)) root = node.RootId;
        var data = new BattleDiagnosticDamageCalculationPayload(stage, 20L << 32, 20L << 32,
            (long)mitigated << 32, (long)shield << 32, (long)planned << 32, (long)applied << 32);
        return new MobaBattleDiagnosticEventDraft(BattleDiagnosticEventKind.Damage, BattleDiagnosticEventChannel.DamageAndHeal,
            stage == BattleDiagnosticDamageStage.Completed ? BattleDiagnosticEventOutcome.Succeeded : BattleDiagnosticEventOutcome.Failed,
            1, 7, 301, root, context, payloadVersion: 1, summary: "frozen result", payload: BattleDiagnosticEventPayload.FromDamageCalculation(data));
    }

    private static MobaActionExecutionSnapshotStore Store(MobaTraceRegistry trace, MobaActorRegistry? actors = null,
        EventBus? bus = null, int capacity = 16) => new(trace, actors, null, bus, capacity, () => true);
    private static long Action(MobaTraceRegistry trace) => trace.CreateRootContext(MobaTraceKind.EffectAction, 301);
    private static ContextSnapshotReference Reference(MobaTraceRegistry trace, long id)
    {
        Assert.True(trace.TryGetNodeSnapshot(id, out var node));
        return ((MobaTraceMetadata)node.Metadata).ActionSnapshot;
    }
    private static MobaHealthChangeResult Commit(MobaTraceRegistry trace, long context, int target = 2, long root = 0)
    {
        if (root == 0 && trace.TryGetNodeSnapshot(context, out var node)) root = node.RootId;
        var origin = new MobaGameplayOrigin(1, target, MobaTraceKind.EffectAction, 301, context, context, root, context);
        return new MobaHealthChangeResult(MobaHealthChangeKind.Damage, 1, target, 0, 1, 2, 100, 4, 10, 6, 20, origin);
    }
    private static MobaBattleDiagnosticEventCollector Collector()
    {
        var collector = new MobaBattleDiagnosticEventCollector(Scope);
        collector.CaptureMode = BattleDiagnosticCaptureMode.Events;
        collector.EnabledChannels = BattleDiagnosticEventChannel.Skill;
        return collector;
    }
    private static BattleDiagnosticSessionSnapshot Snapshot(BattleDiagnosticTraceTrackSnapshot trace)
    {
        var info = new BattleDiagnosticSessionInfo(Scope, "Facts", "test", 1, TimeSpan.TicksPerSecond,
            BattleDiagnosticCapabilities.Trace | BattleDiagnosticCapabilities.Export,
            BattleDiagnosticConnectionState.Connected, BattleDiagnosticCaptureState.Capturing);
        return new BattleDiagnosticSessionSnapshot(info, 11,
            new BattleDiagnosticEventTrackSnapshot(1, new BattleDiagnosticStoreMetrics(16, 0, 1, 0, 0, 0, true), Array.Empty<BattleDiagnosticEvent>()),
            new BattleDiagnosticStateTrackSnapshot(0, -1, null, Array.Empty<BattleDiagnosticActorSummary>()), trace,
            new BattleDiagnosticAttributeTrackSnapshot(0, -1, Array.Empty<BattleDiagnosticActorAttribute>(), Array.Empty<BattleDiagnosticActorAttributeModifier>()),
            new BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorBuff>(0, -1, Array.Empty<BattleDiagnosticActorBuff>()),
            new BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorTag>(0, -1, Array.Empty<BattleDiagnosticActorTag>()),
            new BattleDiagnosticLatestTrackSnapshot<BattleDiagnosticActorEffect>(0, -1, Array.Empty<BattleDiagnosticActorEffect>()));
    }

    private sealed class ThrowingSubscriptionBus : IEventBus
    {
        public void Publish<T>(EventKey<T> key, in T args) { }
        public void Publish<T>(EventKey<T> key, in T args, AbilityKit.Triggering.Runtime.ExecutionControl control) { }
        public bool HasSubscribers<T>(EventKey<T> key) => false;
        public IDisposable Subscribe<T>(EventKey<T> key, Action<T> handler) => throw new InvalidOperationException("subscription failure");
        public IDisposable Subscribe<T>(EventKey<T> key, Action<T, AbilityKit.Triggering.Runtime.ExecutionControl> handler) => throw new InvalidOperationException("subscription failure");
        public void Flush() => throw new InvalidOperationException("observer must never flush");
    }
}

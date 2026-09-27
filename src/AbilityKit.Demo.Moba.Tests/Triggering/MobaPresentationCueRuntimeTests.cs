using System.Collections.Generic;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Triggering;
using AbilityKit.Protocol.Moba;
using AbilityKit.Protocol.Moba.StateSync;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.Config;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Triggering;

public sealed class MobaPresentationCueRuntimeTests
{
    [Fact]
    public void Registry_and_resolver_merge_registered_definition_with_descriptor_overrides_and_fallback_to_descriptor()
    {
        var registry = new MobaPresentationCueRegistry();
        registry.Register(new MobaPresentationCueDefinition(
            cueId: "fireball.cast",
            kind: "skill.cast",
            templateId: 1001,
            vfxId: 2001,
            sfxId: 3001,
            primaryAssetId: "vfx.fireball.registered",
            secondaryAssetId: "sfx.fireball.registered",
            payload: "registered-payload"));
        registry.Register(new MobaPresentationCueDefinition(
            cueId: string.Empty,
            kind: "buff.refresh",
            templateId: 1002,
            vfxId: 2002,
            sfxId: 3002,
            primaryAssetId: "vfx.buff.registered",
            secondaryAssetId: "sfx.buff.registered",
            payload: "buff-payload"));
        var resolver = new MobaPresentationCueResolver(registry);

        var byCueId = resolver.Resolve(new TriggerCueDescriptor(
            kind: "skill.cast.override",
            cueId: "fireball.cast",
            primaryAssetId: "2101",
            payload: "descriptor-payload"));

        Assert.Equal("fireball.cast", byCueId.CueId);
        Assert.Equal("skill.cast.override", byCueId.Kind);
        Assert.Equal(1001, byCueId.TemplateId);
        Assert.Equal(2101, byCueId.VfxId);
        Assert.Equal(3001, byCueId.SfxId);
        Assert.Equal("2101", byCueId.PrimaryAssetId);
        Assert.Equal("sfx.fireball.registered", byCueId.SecondaryAssetId);
        Assert.Equal("descriptor-payload", byCueId.Payload);

        var byKind = resolver.Resolve(new TriggerCueDescriptor(kind: "buff.refresh"));

        Assert.Equal("buff.refresh", byKind.Kind);
        Assert.Equal(1002, byKind.TemplateId);
        Assert.Equal(2002, byKind.VfxId);
        Assert.Equal(3002, byKind.SfxId);

        var fallback = resolver.Resolve(new TriggerCueDescriptor(
            kind: "777",
            cueId: "888",
            primaryAssetId: "999",
            secondaryAssetId: "1000",
            payload: "fallback-payload"));

        Assert.Equal("888", fallback.CueId);
        Assert.Equal("777", fallback.Kind);
        Assert.Equal(888, fallback.TemplateId);
        Assert.Equal(999, fallback.VfxId);
        Assert.Equal(1000, fallback.SfxId);
        Assert.Equal("fallback-payload", fallback.Payload);

        var empty = resolver.Resolve(TriggerCueDescriptor.Empty);
        Assert.True(empty.IsEmpty);
    }

    [Fact]
    public void Active_cue_store_tracks_start_update_and_stop_lifecycle_by_public_snapshot_entry()
    {
        var store = new MobaActivePresentationCueStore();
        var started = CreateEntry(MobaPresentationCueStage.Started, requestKey: "cue-lifecycle-1");

        store.Observe(in started);

        Assert.Equal(1, store.Count);
        Assert.True(store.Active.TryGetValue("cue-lifecycle-1", out var activeStart));
        Assert.True(store.Contains("cue-lifecycle-1"));
        Assert.True(store.TryGet("cue-lifecycle-1", out var activeByKey));
        Assert.True(store.TryGet(in started, out var activeByEntry));
        Assert.Equal((int)MobaPresentationCueStage.Started, activeStart.Entry.Stage);
        Assert.Equal(activeStart.Revision, activeByKey.Revision);
        Assert.Equal(activeStart.Revision, activeByEntry.Revision);
        Assert.Equal(1, activeStart.Revision);

        var copied = new List<MobaActivePresentationCue>();
        Assert.Equal(1, store.CopyActiveTo(copied));
        Assert.Single(copied);
        Assert.Equal("cue-lifecycle-1", copied[0].Key);

        var ticked = started;
        ticked.Stage = (int)MobaPresentationCueStage.Ticked;
        ticked.ElapsedSeconds = 0.5f;
        store.Observe(in ticked);

        Assert.True(store.Active.TryGetValue("cue-lifecycle-1", out var activeTick));
        Assert.Equal((int)MobaPresentationCueStage.Ticked, activeTick.Entry.Stage);
        Assert.Equal(0.5f, activeTick.Entry.ElapsedSeconds);
        Assert.Equal(2, activeTick.Revision);

        var completed = started;
        completed.Stage = (int)MobaPresentationCueStage.Completed;
        store.Observe(in completed);

        Assert.False(store.Active.ContainsKey("cue-lifecycle-1"));
        Assert.Equal(0, store.Count);

        store.Observe(in started);
        Assert.True(store.Remove("cue-lifecycle-1"));
        Assert.False(store.TryGet("cue-lifecycle-1", out _));
    }

    [Fact]
    public void Entry_pool_rents_returns_clears_and_reuses_arrays_for_snapshot_entry_batches()
    {
        var pool = new MobaPresentationCueEntryPool(defaultCapacity: 2);
        var first = pool.Rent(1);
        first[0] = CreateEntry(MobaPresentationCueStage.Started, requestKey: "pooled-entry");

        pool.Return(first);
        Assert.Equal(1, pool.AvailableCount);
        var second = pool.Rent(1);

        Assert.Same(first, second);
        Assert.Equal(0, second[0].Stage);
        Assert.Null(second[0].RequestKey);

        pool.Return(second);
        var exact = pool.RentExact(1);
        Assert.Single(exact);
        pool.Return(exact);
        var exactAgain = pool.RentExact(1);
        Assert.Same(exact, exactAgain);

        var larger = pool.Rent(8);
        Assert.True(larger.Length >= 8);
        Assert.NotSame(exactAgain, larger);
    }

    [Fact]
    public void Snapshot_service_applies_replication_and_prediction_defaults_and_codec_roundtrips_metadata()
    {
        var phase = new MobaLogicWorldRunGateService();
        phase.SetInGame("cue snapshot test");
        var service = new MobaPresentationCueSnapshotService(phase);
        var entry = CreateEntry(MobaPresentationCueStage.Started, requestKey: "snapshot-defaults");
        entry.ReplicationMode = 0;
        entry.ReplicationId = null;
        entry.PredictionKey = 0;
        entry.PredictionState = 0;
        entry.PredictedFrame = 12;
        entry.ConfirmedFrame = 13;

        service.Report(in entry);

        Assert.True(service.TryGetSnapshot(new FrameIndex(100), out var snapshot));
        Assert.Equal(MobaOpCodes.Snapshot.PresentationCue, snapshot.OpCode);
        Assert.NotEmpty(snapshot.Payload);

        var decoded = MobaPresentationCueSnapshotCodec.Deserialize(snapshot.Payload);
        var actual = Assert.Single(decoded);
        Assert.Equal((int)MobaPresentationCueStage.Started, actual.Stage);
        Assert.Equal((int)MobaPresentationCueReplicationMode.ReliableForLifecycle, actual.ReplicationMode);
        Assert.Equal("snapshot-defaults", actual.ReplicationId);
        Assert.Equal(MobaPresentationCueKeys.StableHash("snapshot-defaults"), actual.PredictionKey);
        Assert.Equal((int)MobaPresentationCuePredictionState.ServerConfirmed, actual.PredictionState);
        Assert.Equal(12, actual.PredictedFrame);
        Assert.Equal(13, actual.ConfirmedFrame);

        Assert.False(service.TryGetSnapshot(new FrameIndex(100), out _));
    }

    [Fact]
    public void Snapshot_service_lifecycle_helpers_track_active_cues_and_emit_expected_stages()
    {
        var phase = new MobaLogicWorldRunGateService();
        phase.SetInGame("cue lifecycle helper test");
        var service = new MobaPresentationCueSnapshotService(phase);
        var entry = CreateEntry(MobaPresentationCueStage.None, requestKey: "helper-lifecycle");

        service.Start(in entry);

        Assert.Equal(1, service.ActiveCueCount);
        Assert.True(service.TryGetActiveCue("helper-lifecycle", out var activeStart));
        Assert.Equal((int)MobaPresentationCueStage.Started, activeStart.Entry.Stage);

        service.Tick(in entry, elapsedSeconds: 0.25f, remainingSeconds: 1.75f);

        Assert.Equal(1, service.ActiveCueCount);
        Assert.True(service.TryGetActiveCue(in entry, out var activeTick));
        Assert.Equal((int)MobaPresentationCueStage.Ticked, activeTick.Entry.Stage);
        Assert.Equal(0.25f, activeTick.Entry.ElapsedSeconds);
        Assert.Equal(1.75f, activeTick.Entry.RemainingSeconds);

        service.Refresh(in entry);
        Assert.Equal(1, service.ActiveCues.Count);

        service.Complete(in entry);

        Assert.Equal(0, service.ActiveCueCount);
        Assert.False(service.TryGetActiveCue("helper-lifecycle", out _));
        Assert.True(service.TryGetSnapshot(new FrameIndex(200), out var snapshot));
        var decoded = MobaPresentationCueSnapshotCodec.Deserialize(snapshot.Payload);
        Assert.Equal(4, decoded.Length);
        Assert.Collection(
            decoded,
            first => Assert.Equal((int)MobaPresentationCueStage.Started, first.Stage),
            second =>
            {
                Assert.Equal((int)MobaPresentationCueStage.Ticked, second.Stage);
                Assert.Equal(0.25f, second.ElapsedSeconds);
                Assert.Equal(1.75f, second.RemainingSeconds);
            },
            third => Assert.Equal((int)MobaPresentationCueStage.Refreshed, third.Stage),
            fourth => Assert.Equal((int)MobaPresentationCueStage.Completed, fourth.Stage));
    }

    [Fact]
    public void Snapshot_service_execute_and_remove_helpers_emit_instant_and_stop_stages()
    {
        var phase = new MobaLogicWorldRunGateService();
        phase.SetInGame("cue execute remove helper test");
        var service = new MobaPresentationCueSnapshotService(phase);
        var execute = CreateEntry(MobaPresentationCueStage.None, requestKey: "helper-execute");
        var remove = CreateEntry(MobaPresentationCueStage.Started, requestKey: "helper-remove");

        service.Execute(in execute);
        service.Start(in remove);
        Assert.Equal(2, service.ActiveCueCount);

        service.Remove(in remove);

        Assert.Equal(1, service.ActiveCueCount);
        Assert.True(service.TryGetActiveCue("helper-execute", out _));
        Assert.False(service.TryGetActiveCue("helper-remove", out _));
        Assert.True(service.TryGetSnapshot(new FrameIndex(210), out var snapshot));
        var decoded = MobaPresentationCueSnapshotCodec.Deserialize(snapshot.Payload);
        Assert.Equal(3, decoded.Length);
        Assert.Equal((int)MobaPresentationCueStage.Executed, decoded[0].Stage);
        Assert.Equal((int)MobaPresentationCueStage.Started, decoded[1].Stage);
        Assert.Equal((int)MobaPresentationCueStage.Removed, decoded[2].Stage);
    }

    [Fact]
    public void Trigger_cue_reports_end_to_end_snapshot_with_resolved_assets_context_replication_and_prediction_metadata()
    {
        var registry = new MobaPresentationCueRegistry();
        registry.Register(new MobaPresentationCueDefinition(
            cueId: "skill.fireball",
            kind: "skill.cast",
            templateId: 7001,
            vfxId: 7101,
            sfxId: 7201,
            primaryAssetId: "vfx.fireball",
            secondaryAssetId: "sfx.fireball",
            payload: "registered-payload"));
        var resolver = new MobaPresentationCueResolver(registry);
        var phase = new MobaLogicWorldRunGateService();
        phase.SetInGame("trigger cue test");
        var snapshots = new MobaPresentationCueSnapshotService(phase);
        var descriptor = new TriggerCueDescriptor(kind: "skill.cast", cueId: "skill.fireball");
        var cue = new MobaPresentationTriggerCue(snapshots, resolver, in descriptor);
        var args = new PresentationEventArgs
        {
            EventId = "presentation.fireball",
            TemplateId = 9001,
            RequestKey = "predict-key-42",
            DurationMsOverride = 1250,
            Targets = new[] { 22, 33 },
            Positions = new[] { new Vec3(1f, 2f, 3f), new Vec3(4f, 5f, 6f) },
            SourceActorId = 11,
            TargetActorId = 22,
            SourceContextId = 101,
            RootContextId = 202,
            OwnerContextId = 303,
            ExecutionKind = MobaExecutionKind.SkillEffect,
            Scale = 1.5f,
            Radius = 6,
            Color = "#ff8800"
        };
        var context = new TriggerCueContext(
            eventId: 501,
            eventName: "SkillCast",
            args: args,
            phase: 2,
            priority: 9,
            order: 123,
            triggerId: 6001,
            triggerTypeName: "unit-test-trigger",
            interruptReason: ETriggerShortCircuitReason.None,
            interruptSourceName: null,
            interruptTriggerId: 0,
            interruptConditionPassed: false,
            control: null,
            cueLevel: ECueLevel.Trigger,
            cueStage: ECueLifecycleStage.ConditionPassed,
            actionIndex: -1,
            cueDescriptor: descriptor,
            cueData: args,
            cuePayload: args.RequestKey);

        cue.OnConditionPassed(in context);

        Assert.True(snapshots.TryGetSnapshot(new FrameIndex(1), out var snapshot));
        Assert.Equal(MobaOpCodes.Snapshot.PresentationCue, snapshot.OpCode);
        var decoded = MobaPresentationCueSnapshotCodec.Deserialize(snapshot.Payload);
        var actual = Assert.Single(decoded);

        Assert.Equal((int)MobaPresentationCueStage.ConditionPassed, actual.Stage);
        Assert.Equal("skill.cast", actual.CueKind);
        Assert.Equal("vfx.fireball", actual.CueVfxId);
        Assert.Equal("sfx.fireball", actual.CueSfxId);
        Assert.Equal(7001, actual.TemplateId);
        Assert.Equal(7101, actual.VfxId);
        Assert.Equal(7201, actual.SfxId);
        Assert.Equal("predict-key-42", actual.RequestKey);
        Assert.Equal("predict-key-42", actual.ReplicationId);
        Assert.Equal((int)MobaPresentationCueReplicationMode.ReliableForLifecycle, actual.ReplicationMode);
        Assert.Equal(MobaPresentationCueKeys.StableHash("predict-key-42"), actual.PredictionKey);
        Assert.Equal((int)MobaPresentationCuePredictionState.ServerConfirmed, actual.PredictionState);
        Assert.Equal(11, actual.SourceActorId);
        Assert.Equal(22, actual.TargetActorId);
        Assert.Equal(new[] { 22, 33 }, actual.Targets);
        Assert.Equal(new[] { 1f, 2f, 3f, 4f, 5f, 6f }, actual.Positions);
        Assert.Equal(501, actual.TriggerEventId);
        Assert.Equal("SkillCast", actual.TriggerEventName);
        Assert.Equal(6001, actual.TriggerId);
        Assert.Equal(2, actual.Phase);
        Assert.Equal(9, actual.Priority);
        Assert.Equal(123, actual.Order);
        Assert.Equal(-1, actual.ActionIndex);
        Assert.Equal(1250, actual.DurationMsOverride);
        Assert.Equal("presentation.fireball", actual.ContextEventId);
        Assert.Equal(101, actual.SourceContextId);
        Assert.Equal(202, actual.RootContextId);
        Assert.Equal(303, actual.OwnerContextId);
        Assert.Equal(9001, actual.SourceConfigId);
        Assert.Equal(new[] { 1, 2 }, actual.NumericParamKeys);
        Assert.Equal(new[] { 1.5f, 6f }, actual.NumericParamValues);
        Assert.Equal(new[] { "color" }, actual.StringParamKeys);
        Assert.Equal(new[] { "#ff8800" }, actual.StringParamValues);
        Assert.Equal(1.5f, actual.Scale);
        Assert.Equal((int)ECueLevel.Trigger, actual.CueLevel);
        Assert.Equal((int)ECueLifecycleStage.ConditionPassed, actual.CueStage);
    }

    [Fact]
    public void Trigger_cue_public_publish_can_emit_business_lifecycle_stage()
    {
        var phase = new MobaLogicWorldRunGateService();
        phase.SetInGame("public publish cue test");
        var snapshots = new MobaPresentationCueSnapshotService(phase);
        var descriptor = new TriggerCueDescriptor(kind: "buff.aura", cueId: "buff.aura.loop", primaryAssetId: "8101", secondaryAssetId: "8201");
        var cue = new MobaPresentationTriggerCue(snapshots, in descriptor);
        var args = new PresentationEventArgs
        {
            EventId = "presentation.aura.started",
            RequestKey = "aura-instance-1",
            SourceActorId = 100,
            TargetActorId = 101,
            DurationMsOverride = 3000
        };
        var context = new TriggerCueContext(
            eventId: 601,
            eventName: "AuraStarted",
            args: args,
            phase: 3,
            priority: 5,
            order: 44,
            triggerId: 7001,
            triggerTypeName: "unit-test-trigger",
            interruptReason: ETriggerShortCircuitReason.None,
            interruptSourceName: null,
            interruptTriggerId: 0,
            interruptConditionPassed: false,
            control: null,
            cueLevel: ECueLevel.Behavior,
            cueStage: ECueLifecycleStage.Executed,
            actionIndex: 2,
            cueDescriptor: descriptor,
            cueData: args,
            cuePayload: args.RequestKey);

        cue.Publish(MobaPresentationCueStage.Started, in context, actionIndex: 2);

        Assert.True(snapshots.TryGetActiveCue("aura-instance-1", out var active));
        Assert.Equal((int)MobaPresentationCueStage.Started, active.Entry.Stage);
        Assert.True(snapshots.TryGetSnapshot(new FrameIndex(20), out var snapshot));
        var actual = Assert.Single(MobaPresentationCueSnapshotCodec.Deserialize(snapshot.Payload));
        Assert.Equal((int)MobaPresentationCueStage.Started, actual.Stage);
        Assert.Equal("buff.aura", actual.CueKind);
        Assert.Equal(8101, actual.VfxId);
        Assert.Equal(8201, actual.SfxId);
        Assert.Equal("aura-instance-1", actual.RequestKey);
        Assert.Equal(2, actual.ActionIndex);
        Assert.Equal((int)ECueLevel.Behavior, actual.CueLevel);
        Assert.Equal((int)ECueLifecycleStage.Executed, actual.CueStage);
    }

    [Fact]
    public void Snapshot_service_uses_pool_backed_snapshot_flow_for_multiple_batches_without_leaking_previous_entries()
    {
        var phase = new MobaLogicWorldRunGateService();
        phase.SetInGame("pool snapshot flow test");
        var service = new MobaPresentationCueSnapshotService(phase);
        var first = CreateEntry(MobaPresentationCueStage.Started, requestKey: "batch-1");
        var second = CreateEntry(MobaPresentationCueStage.Started, requestKey: "batch-2");

        service.Report(in first);
        Assert.True(service.TryGetSnapshot(new FrameIndex(1), out var firstSnapshot));
        Assert.Equal("batch-1", Assert.Single(MobaPresentationCueSnapshotCodec.Deserialize(firstSnapshot.Payload)).RequestKey);

        service.Report(in second);
        Assert.True(service.TryGetSnapshot(new FrameIndex(2), out var secondSnapshot));
        var secondDecoded = MobaPresentationCueSnapshotCodec.Deserialize(secondSnapshot.Payload);

        var actual = Assert.Single(secondDecoded);
        Assert.Equal("batch-2", actual.RequestKey);
        Assert.DoesNotContain(secondDecoded, entry => entry.RequestKey == "batch-1");
    }

    private static MobaPresentationCueSnapshotEntry CreateEntry(MobaPresentationCueStage stage, string requestKey)
    {
        return new MobaPresentationCueSnapshotEntry
        {
            Stage = (int)stage,
            CueKind = "unit-test",
            RequestKey = requestKey,
            SourceActorId = 1,
            TargetActorId = 2,
            TriggerEventId = 3,
            TriggerId = 4,
            ActionIndex = -1,
            Order = 5,
            Scale = 1f,
            ColorR = 1f,
            ColorG = 1f,
            ColorB = 1f,
            ColorA = 1f
        };
    }
}

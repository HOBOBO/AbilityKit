using System.Reflection;
using AbilityKit.Ability.FrameSync;
using AbilityKit.Core.Mathematics;
using AbilityKit.Demo.Moba.Components;
using AbilityKit.Demo.Moba.Rollback;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Demo.Moba.Services.StateSync;
using AbilityKit.Trace;
using MemoryPack;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Trace;

public sealed class MobaSkillTraceRollbackTests
{
    [Fact]
    public void Local_rollback_restores_skill_and_child_lifecycle_without_replaying_started_or_ended_events()
    {
        using var trace = new MobaTraceRegistry();
        using var skills = CreateSkills(trace);
        var root = trace.CreateRootContext(MobaTraceKind.SkillCast, 1001, 1, 2);
        var childId = trace.CreateChildContext(root, MobaTraceKind.BuffApply, 2001, 1, 2);
        var unrelated = trace.CreateChildContext(root, MobaTraceKind.EffectExecution, 3001);
        var runtime = CreateRuntime(skills, root);
        var handle = runtime.Handle;
        var child = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Buff, 2001, childId, 2001);
        Assert.True(skills.RetainChild(in handle, in child, out _));
        var provider = new MobaSkillRuntimeRollbackProvider(skills);
        var frame = new FrameIndex(0);
        var snapshot = provider.Export(frame);
        trace.EndContext(childId, TraceLifecycleReason.Completed);
        trace.EndContext(unrelated, TraceLifecycleReason.Completed);
        skills.ForceTerminate(in handle);
        trace.RetainRoot(root);
        var events = new List<TraceRegistryEventKind>();
        trace.RegistryEvent += evt => events.Add(evt.Kind);
        var revision = trace.Revision;

        provider.ValidateImport(frame, snapshot);
        provider.Import(frame, snapshot);

        Assert.True(skills.TryGet(in handle, out _));
        Assert.False(trace.TryGetSnapshot(root).IsEnded);
        Assert.False(trace.TryGetSnapshot(childId).IsEnded);
        Assert.True(trace.TryGetSnapshot(unrelated).IsEnded);
        Assert.True(trace.TryGetRootState(root, out var state));
        Assert.Equal(2, state.ActiveCount);
        Assert.Equal(1, state.ExternalRefCount);
        Assert.True(trace.Revision > revision);
        Assert.Equal(new[] { TraceRegistryEventKind.LifecycleRestored }, events);
        Assert.Equal(0, trace.PurgeReleased(100, 0));
    }

    [Fact]
    public void Local_trace_snapshot_does_not_change_authoritative_payload_or_hash()
    {
        using var trace = new MobaTraceRegistry();
        using var skills = CreateSkills(trace);
        var root = trace.CreateRootContext(MobaTraceKind.SkillCast, 1001);
        CreateRuntime(skills, root);
        var provider = new MobaSkillRuntimeRollbackProvider(skills);
        var frame = new FrameIndex(10);
        var payload = provider.ExportState(frame);
        var before = new MobaStateHashBuilder(2166136261u);
        provider.AddStateHash(frame, ref before);
        var local = MemoryPackSerializer.Deserialize<MobaSkillRuntimeLocalRollbackPayload>(provider.Export(frame));
        Assert.Equal(payload, local.RuntimeState);
        Assert.Single(local.TraceNodes);
        trace.EndContext(root, TraceLifecycleReason.Completed);
        var after = new MobaStateHashBuilder(2166136261u);
        provider.AddStateHash(frame, ref after);
        Assert.Equal(before.Value, after.Value);
        Assert.Equal(payload, provider.ExportState(frame));
        provider.ImportState(frame, payload);
        Assert.True(trace.TryGetSnapshot(root).IsEnded);
    }

    [Fact]
    public void Missing_trace_identity_is_rejected_before_runtime_mutation()
    {
        using var trace = new MobaTraceRegistry();
        using var skills = CreateSkills(trace);
        var root = trace.CreateRootContext(MobaTraceKind.SkillCast, 1001);
        var runtime = CreateRuntime(skills, root);
        var handle = runtime.Handle;
        var provider = new MobaSkillRuntimeRollbackProvider(skills);
        var frame = new FrameIndex(10);
        var snapshot = provider.Export(frame);
        trace.EndContext(root, TraceLifecycleReason.Completed);
        Assert.True(trace.TryPurgeReleasedRoot(root));
        skills.UpdateStage(runtime.RuntimeId, SkillCastStage.Cancelled);

        Assert.Throws<InvalidOperationException>(() => provider.ValidateImport(frame, snapshot));
        Assert.Throws<InvalidOperationException>(() => provider.Import(frame, snapshot));
        Assert.True(skills.TryGet(in handle, out var current));
        Assert.Same(runtime, current);
        Assert.Equal(SkillCastStage.Cancelled, current.Stage);
    }

    [Fact]
    public void Local_rollback_without_trace_still_restores_runtime()
    {
        using var skills = new MobaSkillCastRuntimeService();
        var runtime = CreateRuntime(skills, 9001);
        var handle = runtime.Handle;
        var provider = new MobaSkillRuntimeRollbackProvider(skills);
        var frame = new FrameIndex(10);
        var snapshot = provider.Export(frame);
        skills.ForceTerminate(in handle);
        provider.Import(frame, snapshot);
        Assert.True(skills.TryGet(in handle, out _));
    }

    [Fact]
    public void Lifecycle_restore_preserves_ended_at_frame_zero_and_validates_entire_batch()
    {
        using var trace = new MobaTraceRegistry();
        var root = trace.CreateRootContext(MobaTraceKind.SkillCast, 1001);
        trace.EndContext(root, TraceLifecycleReason.Completed);
        trace.TryGetNodeSnapshot(root, out var ended);
        Assert.True(ended.IsEnded);
        Assert.Equal(0, ended.EndedFrame);
        var active = new TraceNodeSnapshot(root, root, 0, ended.Kind, 0, 0, 0, 0, null, false);
        trace.RestoreLifecycle(new[] { active });
        var revision = trace.Revision;
        Assert.Throws<InvalidOperationException>(() => trace.RestoreLifecycle(new[] { ended, ended }));
        Assert.False(trace.TryGetSnapshot(root).IsEnded);
        Assert.Equal(revision, trace.Revision);
        trace.RestoreLifecycle(new[] { ended });
        Assert.True(trace.TryGetSnapshot(root).IsEnded);
        Assert.True(trace.TryGetRootState(root, out var state));
        Assert.Equal(0, state.ActiveCount);
    }

    [Fact]
    public void Local_rollback_rejects_trace_nodes_outside_runtime_scope_and_missing_entries()
    {
        using var trace = new MobaTraceRegistry();
        using var skills = CreateSkills(trace);
        var root = trace.CreateRootContext(MobaTraceKind.SkillCast, 1001);
        CreateRuntime(skills, root);
        var unrelated = trace.CreateRootContext(MobaTraceKind.SkillCast, 2001);
        trace.TryGetNodeSnapshot(unrelated, out var node);
        var provider = new MobaSkillRuntimeRollbackProvider(skills);
        var frame = new FrameIndex(10);
        var authority = provider.ExportState(frame);
        var invalidScope = MemoryPackSerializer.Serialize(new MobaSkillRuntimeLocalRollbackPayload(1, authority,
            new[] { new MobaSkillTraceLifecycleRollbackEntry(node) }));
        var incomplete = MemoryPackSerializer.Serialize(new MobaSkillRuntimeLocalRollbackPayload(1, authority,
            Array.Empty<MobaSkillTraceLifecycleRollbackEntry>()));
        Assert.Throws<InvalidOperationException>(() => provider.Import(frame, invalidScope));
        Assert.Throws<InvalidOperationException>(() => provider.Import(frame, incomplete));
        Assert.Equal(authority, provider.ExportState(frame));
    }

    [Fact]
    public void Rollback_retracts_same_frame_predicted_branches_and_roots_without_reusing_ids()
    {
        using var trace = new MobaTraceRegistry();
        using var skills = CreateSkills(trace);
        var root = trace.CreateRootContext(MobaTraceKind.SkillCast, 1001);
        var confirmedChild = trace.CreateChildContext(root, MobaTraceKind.SkillPhase, 1001);
        CreateRuntime(skills, root);
        trace.RetainRoot(root);
        var provider = new MobaSkillRuntimeRollbackProvider(skills);
        var frame = new FrameIndex(0);
        var snapshot = provider.Export(frame);
        var predicted = trace.CreateChildContext(confirmedChild, MobaTraceKind.EffectExecution, 2001);
        var descendant = trace.CreateChildContext(predicted, MobaTraceKind.EffectAction, 2002);
        var predictedRoot = trace.CreateRootContext(MobaTraceKind.SkillCast, 3001);
        trace.RetainRoot(predictedRoot);
        var predictedRuntime = CreateRuntime(skills, predictedRoot);
        var predictedHandle = predictedRuntime.Handle;
        var nextId = trace.NextContextId;
        var events = new List<TraceRegistryEventKind>();
        trace.RegistryEvent += evt => events.Add(evt.Kind);

        provider.Import(frame, snapshot);

        Assert.False(trace.Contains(predicted));
        Assert.False(trace.Contains(descendant));
        Assert.False(trace.Contains(predictedRoot));
        Assert.False(skills.TryGet(in predictedHandle, out _));
        Assert.True(trace.Contains(root));
        Assert.True(trace.Contains(confirmedChild));
        Assert.False(trace.MetadataStore.TryGetMetadata(descendant, out _));
        Assert.True(trace.TryGetChildren(confirmedChild, out var children));
        Assert.Empty(children);
        Assert.True(trace.TryGetRootState(root, out var state));
        Assert.Equal(2, state.ActiveCount);
        Assert.Equal(1, state.ExternalRefCount);
        Assert.Equal(new[] { TraceRegistryEventKind.PredictionRetracted, TraceRegistryEventKind.LifecycleRestored }, events);
        var replayRoot = trace.CreateRootContext(MobaTraceKind.SkillCast, 3001);
        Assert.Equal(nextId, replayRoot);
        trace.RetainRoot(replayRoot);
        trace.ReleaseRoot(predictedRoot);
        Assert.True(trace.TryGetRootState(replayRoot, out var replayState));
        Assert.Equal(1, replayState.ExternalRefCount);
    }

    [Fact]
    public void Empty_skill_snapshot_still_retracts_new_prediction_and_repeated_rollback_is_safe()
    {
        using var trace = new MobaTraceRegistry();
        using var skills = CreateSkills(trace);
        var provider = new MobaSkillRuntimeRollbackProvider(skills);
        var frame = new FrameIndex(0);
        var snapshot = provider.Export(frame);
        var root = trace.CreateRootContext(MobaTraceKind.SkillCast, 1001);
        CreateRuntime(skills, root);
        trace.EndContext(root, TraceLifecycleReason.Completed);
        provider.Import(frame, snapshot);
        Assert.Equal(0, trace.TotalNodeCount);
        Assert.Equal(0, skills.Count);
        var revision = trace.Revision;
        provider.Import(frame, snapshot);
        Assert.Equal(revision, trace.Revision);
        var replay = trace.CreateRootContext(MobaTraceKind.SkillCast, 1001);
        provider.Import(frame, snapshot);
        Assert.False(trace.Contains(replay));
        Assert.True(trace.NextContextId > replay);
    }

    [Fact]
    public void Invalid_prediction_boundary_is_rejected_before_runtime_or_trace_mutation()
    {
        using var trace = new MobaTraceRegistry();
        using var skills = CreateSkills(trace);
        var root = trace.CreateRootContext(MobaTraceKind.SkillCast, 1001);
        CreateRuntime(skills, root);
        var provider = new MobaSkillRuntimeRollbackProvider(skills);
        var frame = new FrameIndex(0);
        var snapshot = MemoryPackSerializer.Deserialize<MobaSkillRuntimeLocalRollbackPayload>(provider.Export(frame));
        foreach (var boundary in new[] { 0L, root, trace.NextContextId + 1L })
        {
            var invalid = MemoryPackSerializer.Serialize(new MobaSkillRuntimeLocalRollbackPayload(2,
                snapshot.RuntimeState, snapshot.TraceNodes, boundary));
            Assert.Throws<InvalidOperationException>(() => provider.Import(frame, invalid));
            Assert.Equal(snapshot.RuntimeState, provider.ExportState(frame));
            Assert.True(trace.Contains(root));
        }
    }

    private static MobaSkillCastRuntimeService CreateSkills(MobaTraceRegistry trace)
    {
        var skills = new MobaSkillCastRuntimeService();
        typeof(MobaSkillCastRuntimeService).GetField("_trace", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(skills, trace);
        return skills;
    }

    private static MobaSkillCastRuntime CreateRuntime(MobaSkillCastRuntimeService skills, long root) => skills.Create(
        new MobaSkillCastRuntimeCreateRequest(1001, 1, 1, 1, 1, 2, Vec3.Zero, Vec3.Zero, root));
}

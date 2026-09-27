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

public sealed class MobaSkillContextRollbackTests
{
    [Fact]
    public void Execution_context_rollback_entry_roundtrips_combat_facts()
    {
        using var contexts = new MobaExecutionContextRegistry();
        var node = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.DamageApply,
            3001,
            1,
            2,
            combatFlags: MobaCombatExecutionFlags.ReflectedDamage
                         | MobaCombatExecutionFlags.LifestealSuppressed));
        var bytes = MemoryPackSerializer.Serialize(
            new MobaSkillExecutionContextRollbackEntry(in node));
        var restored = MemoryPackSerializer
            .Deserialize<MobaSkillExecutionContextRollbackEntry>(bytes)
            .ToNode();

        Assert.Equal(node.CombatFacts, restored.CombatFacts);
        Assert.True(restored.CombatFacts.IsReflectedDamage);
        Assert.False(restored.CombatFacts.CanTriggerReflection);
        Assert.False(restored.CombatFacts.CanTriggerLifesteal);
    }

    [Fact]
    public void Local_rollback_restores_skill_and_child_lifecycle_without_replaying_started_or_ended_events()
    {
        using var contexts = new MobaExecutionContextRegistry();
        using var trace = new MobaTraceRegistry();
        trace.OnInit(new ExecutionContextResolver(contexts));
        using var skills = CreateSkills(contexts);
        var root = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillCast, 1001, 1, 2));
        var childNode = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.BuffApply, 2001, 1, 2, parentContextId: root.ContextId));
        var unrelatedNode = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.EffectExecution, 3001, 1, 2, parentContextId: root.ContextId));
        var childId = childNode.ContextId;
        var unrelated = unrelatedNode.ContextId;
        var runtime = CreateRuntime(skills, root.ContextId);
        var handle = runtime.Handle;
        var child = new MobaSkillRuntimeChildRef(MobaSkillRuntimeChildKind.Buff, 2001, childId, 2001);
        Assert.True(skills.RetainChild(in handle, in child, out _));
        var provider = new MobaSkillRuntimeRollbackProvider(skills);
        var frame = new FrameIndex(0);
        var snapshot = provider.Export(frame);
        contexts.End(childId, (int)MobaExecutionEndReason.Completed, 1);
        contexts.End(unrelated, (int)MobaExecutionEndReason.Completed, 1);
        skills.ForceTerminate(in handle);
        trace.RetainRoot(root.ContextId);
        var events = new List<TraceRegistryEventKind>();
        trace.RegistryEvent += evt => events.Add(evt.Kind);
        var revision = trace.Revision;

        provider.ValidateImport(frame, snapshot);
        provider.Import(frame, snapshot);

        Assert.True(skills.TryGet(in handle, out _));
        Assert.False(trace.TryGetSnapshot(root.ContextId).IsEnded);
        Assert.False(trace.TryGetSnapshot(childId).IsEnded);
        Assert.True(trace.TryGetSnapshot(unrelated).IsEnded);
        Assert.True(trace.TryGetRootState(root.ContextId, out var state));
        Assert.Equal(2, state.ActiveCount);
        Assert.Equal(1, state.ExternalRefCount);
        Assert.True(trace.Revision > revision);
        Assert.Equal(new[] { TraceRegistryEventKind.LifecycleRestored }, events);
        Assert.Equal(0, trace.PurgeReleased(100, 0));
        trace.OnDeinit(null!);
    }

    [Fact]
    public void Optional_trace_state_does_not_change_authoritative_payload_or_hash()
    {
        using var trace = new MobaTraceRegistry();
        using var skills = CreateSkills();
        var root = trace.CreateObservationRoot(MobaExecutionKind.SkillCast, 1001);
        CreateRuntime(skills, root);
        var provider = new MobaSkillRuntimeRollbackProvider(skills);
        var frame = new FrameIndex(10);
        var payload = provider.ExportState(frame);
        var before = new MobaStateHashBuilder(2166136261u);
        provider.AddStateHash(frame, ref before);
        var local = MemoryPackSerializer.Deserialize<MobaSkillRuntimeLocalRollbackPayload>(provider.Export(frame));
        Assert.Equal(1, local.Version);
        Assert.Equal(payload, local.RuntimeState);
        Assert.Null(local.ExecutionContextNodes);
        trace.EndContext(root, MobaExecutionEndReason.Completed);
        var after = new MobaStateHashBuilder(2166136261u);
        provider.AddStateHash(frame, ref after);
        Assert.Equal(before.Value, after.Value);
        Assert.Equal(payload, provider.ExportState(frame));
        provider.ImportState(frame, payload);
        Assert.True(trace.TryGetSnapshot(root).IsEnded);
    }

    [Fact]
    public void Missing_trace_identity_does_not_block_authoritative_runtime_restore()
    {
        using var trace = new MobaTraceRegistry();
        using var skills = CreateSkills();
        var root = trace.CreateObservationRoot(MobaExecutionKind.SkillCast, 1001);
        var runtime = CreateRuntime(skills, root);
        var handle = runtime.Handle;
        var provider = new MobaSkillRuntimeRollbackProvider(skills);
        var frame = new FrameIndex(10);
        var snapshot = provider.Export(frame);
        var originalStage = runtime.Stage;
        trace.EndContext(root, MobaExecutionEndReason.Completed);
        Assert.True(trace.TryPurgeReleasedRoot(root));
        skills.UpdateStage(runtime.RuntimeId, SkillCastStage.Cancelled);

        provider.ValidateImport(frame, snapshot);
        provider.Import(frame, snapshot);
        Assert.True(skills.TryGet(in handle, out var current));
        Assert.Equal(originalStage, current.Stage);
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
    public void Optional_trace_detachment_does_not_affect_runtime_restore()
    {
        using var trace = new MobaTraceRegistry();
        using var skills = CreateSkills();
        var root = trace.CreateObservationRoot(MobaExecutionKind.SkillCast, 1001);
        var runtime = CreateRuntime(skills, root);
        var handle = runtime.Handle;
        var snapshot = new MobaSkillRuntimeRollbackProvider(skills)
            .Export(new FrameIndex(10));
        skills.ForceTerminate(in handle);

        var providerWithoutTrace = new MobaSkillRuntimeRollbackProvider(skills);
        providerWithoutTrace.Import(new FrameIndex(10), snapshot);

        Assert.True(skills.TryGet(in handle, out _));
    }

    [Fact]
    public void Context_owned_rollback_restores_root_and_retracts_predicted_branches_without_trace()
    {
        using var contexts = new MobaExecutionContextRegistry();
        using var skills = CreateSkills(contexts);
        var root = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillCast, 1001, 1, 2, frame: 10));
        var runtime = CreateRuntime(skills, root.ContextId);
        var handle = runtime.Handle;
        var provider = new MobaSkillRuntimeRollbackProvider(skills);
        var frame = new FrameIndex(10);
        var snapshot = provider.Export(frame);
        var boundary = contexts.NextContextId;
        var effect = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.EffectExecution, 2001, 1, 2,
            parentContextId: root.ContextId,
            frame: 11));
        var action = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.EffectAction, 2002, 1, 2,
            parentContextId: effect.ContextId,
            frame: 11));
        skills.ForceTerminate(in handle);
        Assert.True(contexts.TryGet(root.ContextId, out var ended));
        Assert.True(ended.IsEnded);
        var nextId = contexts.NextContextId;

        provider.Import(frame, snapshot);

        Assert.True(skills.TryGet(in handle, out _));
        Assert.True(contexts.TryGet(root.ContextId, out var restored));
        Assert.False(restored.IsEnded);
        Assert.False(contexts.TryGet(effect.ContextId, out _));
        Assert.False(contexts.TryGet(action.ContextId, out _));
        Assert.Equal(boundary, effect.ContextId);
        var replay = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.EffectExecution, 2001, 1, 2,
            parentContextId: root.ContextId,
            frame: 11));
        Assert.Equal(nextId, replay.ContextId);
    }

    [Fact]
    public void Context_and_trace_rollback_retract_their_own_prediction_partitions()
    {
        using var contexts = new MobaExecutionContextRegistry();
        using var trace = new MobaTraceRegistry();
        trace.OnInit(new ExecutionContextResolver(contexts));
        using var skills = CreateSkills(contexts);
        var root = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillCast, 1001, 1, 2, frame: 10));
        var runtime = CreateRuntime(skills, root.ContextId);
        var handle = runtime.Handle;
        var provider = new MobaSkillRuntimeRollbackProvider(skills);
        var frame = new FrameIndex(10);
        var snapshot = provider.Export(frame);
        var predicted = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.EffectExecution, 2001, 1, 2,
            parentContextId: root.ContextId,
            frame: 11));
        var observationOnlyChild = trace.CreateObservationChild(
            predicted.ContextId,
            MobaExecutionKind.EffectAction,
            2002,
            1,
            2);
        skills.ForceTerminate(in handle);

        provider.Import(frame, snapshot);

        Assert.True(contexts.TryGet(root.ContextId, out var restored));
        Assert.False(restored.IsEnded);
        Assert.False(contexts.TryGet(predicted.ContextId, out _));
        Assert.True(trace.Contains(root.ContextId));
        Assert.False(trace.TryGetSnapshot(root.ContextId).IsEnded);
        Assert.False(trace.Contains(predicted.ContextId));
        Assert.False(trace.Contains(observationOnlyChild));
        trace.OnDeinit(null!);
    }

    [Fact]
    public void Lifecycle_restore_preserves_ended_at_frame_zero_and_validates_entire_batch()
    {
        using var trace = new MobaTraceRegistry();
        var root = trace.CreateObservationRoot(MobaExecutionKind.SkillCast, 1001);
        trace.EndContext(root, MobaExecutionEndReason.Completed);
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
    public void Rollback_retracts_same_frame_predicted_branches_and_roots_without_reusing_ids()
    {
        using var contexts = new MobaExecutionContextRegistry();
        using var trace = new MobaTraceRegistry();
        trace.OnInit(new ExecutionContextResolver(contexts));
        using var skills = CreateSkills(contexts);
        var root = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillCast, 1001, 1, 2));
        var confirmedChild = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.EffectExecution, 2001, 1, 2,
            parentContextId: root.ContextId));
        var runtime = CreateRuntime(skills, root.ContextId);
        var confirmedRef = new MobaSkillRuntimeChildRef(
            MobaSkillRuntimeChildKind.Effect,
            2001,
            confirmedChild.ContextId,
            2001);
        var runtimeHandle = runtime.Handle;
        Assert.True(skills.RetainChild(in runtimeHandle, in confirmedRef, out _));
        trace.RetainRoot(root.ContextId);
        var provider = new MobaSkillRuntimeRollbackProvider(skills);
        var frame = new FrameIndex(0);
        var snapshot = provider.Export(frame);
        var predicted = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.EffectAction, 2002, 1, 2,
            parentContextId: confirmedChild.ContextId));
        var descendant = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.EffectAction, 2003, 1, 2,
            parentContextId: predicted.ContextId));
        var predictedRoot = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillCast, 3001, 1, 2));
        trace.RetainRoot(predictedRoot.ContextId);
        var predictedRuntime = CreateRuntime(skills, predictedRoot.ContextId);
        var predictedHandle = predictedRuntime.Handle;
        var nextId = contexts.NextContextId;
        var events = new List<TraceRegistryEventKind>();
        trace.RegistryEvent += evt => events.Add(evt.Kind);

        provider.Import(frame, snapshot);

        Assert.False(trace.Contains(predicted.ContextId));
        Assert.False(trace.Contains(descendant.ContextId));
        Assert.False(trace.Contains(predictedRoot.ContextId));
        Assert.False(skills.TryGet(in predictedHandle, out _));
        Assert.True(trace.Contains(root.ContextId));
        Assert.True(trace.Contains(confirmedChild.ContextId));
        Assert.False(trace.MetadataStore.TryGetMetadata(descendant.ContextId, out _));
        Assert.True(trace.TryGetChildren(confirmedChild.ContextId, out var children));
        Assert.Empty(children);
        Assert.True(trace.TryGetRootState(root.ContextId, out var state));
        Assert.Equal(2, state.ActiveCount);
        Assert.Equal(1, state.ExternalRefCount);
        Assert.Equal(new[] { TraceRegistryEventKind.PredictionRetracted, TraceRegistryEventKind.LifecycleRestored }, events);
        var replayRoot = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillCast, 3001, 1, 2));
        Assert.Equal(nextId, replayRoot.ContextId);
        trace.RetainRoot(replayRoot.ContextId);
        trace.ReleaseRoot(predictedRoot.ContextId);
        Assert.True(trace.TryGetRootState(replayRoot.ContextId, out var replayState));
        Assert.Equal(1, replayState.ExternalRefCount);
        trace.OnDeinit(null!);
    }

    [Fact]
    public void Empty_skill_snapshot_still_retracts_new_prediction_and_repeated_rollback_is_safe()
    {
        using var contexts = new MobaExecutionContextRegistry();
        using var trace = new MobaTraceRegistry();
        trace.OnInit(new ExecutionContextResolver(contexts));
        using var skills = CreateSkills(contexts);
        var provider = new MobaSkillRuntimeRollbackProvider(skills);
        var frame = new FrameIndex(0);
        var snapshot = provider.Export(frame);
        var root = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillCast, 1001, 1, 2));
        CreateRuntime(skills, root.ContextId);
        contexts.End(root.ContextId, (int)MobaExecutionEndReason.Completed, 0);
        provider.Import(frame, snapshot);
        Assert.Equal(0, trace.TotalNodeCount);
        Assert.Equal(0, skills.Count);
        var revision = trace.Revision;
        provider.Import(frame, snapshot);
        Assert.Equal(revision, trace.Revision);
        var replay = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillCast, 1001, 1, 2));
        provider.Import(frame, snapshot);
        Assert.False(trace.Contains(replay.ContextId));
        Assert.True(contexts.NextContextId > replay.ContextId);
        trace.OnDeinit(null!);
    }

    [Fact]
    public void Invalid_context_prediction_boundary_is_rejected_before_runtime_mutation()
    {
        using var contexts = new MobaExecutionContextRegistry();
        using var trace = new MobaTraceRegistry();
        trace.OnInit(new ExecutionContextResolver(contexts));
        using var skills = CreateSkills(contexts);
        var root = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillCast, 1001, 1, 2));
        CreateRuntime(skills, root.ContextId);
        var provider = new MobaSkillRuntimeRollbackProvider(skills);
        var frame = new FrameIndex(0);
        var snapshot = MemoryPackSerializer.Deserialize<MobaSkillRuntimeLocalRollbackPayload>(provider.Export(frame));
        foreach (var boundary in new[] { 0L, root.ContextId, contexts.NextContextId + 1L })
        {
            var invalid = MemoryPackSerializer.Serialize(new MobaSkillRuntimeLocalRollbackPayload(
                1,
                snapshot.RuntimeState,
                snapshot.ExecutionContextNodes,
                boundary));
            Assert.Throws<InvalidOperationException>(() => provider.Import(frame, invalid));
            Assert.Equal(snapshot.RuntimeState, provider.ExportState(frame));
            Assert.True(contexts.TryGet(root.ContextId, out _));
        }
        trace.OnDeinit(null!);
    }

    [Fact]
    public void Export_rejects_missing_or_mismatched_formal_execution_context()
    {
        using var contexts = new MobaExecutionContextRegistry();
        using var skills = CreateSkills(contexts);
        var rootContextId = contexts.NextContextId;
        CreateRuntime(skills, rootContextId);
        var provider = new MobaSkillRuntimeRollbackProvider(skills);
        var frame = new FrameIndex(0);

        Assert.Throws<InvalidOperationException>(() => provider.Export(frame));

        var wrongKind = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.EffectExecution, 1001, 1, 2));
        Assert.Equal(rootContextId, wrongKind.ContextId);
        Assert.Throws<InvalidOperationException>(() => provider.Export(frame));
    }

    private static MobaSkillCastRuntimeService CreateSkills(
        MobaExecutionContextRegistry? contexts = null)
    {
        var skills = new MobaSkillCastRuntimeService();
        typeof(MobaSkillCastRuntimeService).GetField("_executionContexts", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(skills, contexts);
        return skills;
    }

    private static MobaSkillCastRuntime CreateRuntime(MobaSkillCastRuntimeService skills, long root) => skills.Create(
        new MobaSkillCastRuntimeCreateRequest(1001, 1, 1, 1, 1, 2, Vec3.Zero, Vec3.Zero, root));

    private sealed class ExecutionContextResolver : AbilityKit.Ability.World.DI.IWorldResolver
    {
        private readonly MobaExecutionContextRegistry _contexts;

        public ExecutionContextResolver(MobaExecutionContextRegistry contexts) => _contexts = contexts;
        public object? Resolve(Type serviceType) => serviceType == typeof(MobaExecutionContextRegistry) ? _contexts : null;
        public T? Resolve<T>() => TryResolve<T>(out var instance) ? instance : default;
        public bool TryResolve(Type serviceType, out object? instance)
        {
            instance = Resolve(serviceType);
            return instance != null;
        }

        public bool TryResolve<T>(out T? instance)
        {
            if (_contexts is T resolved)
            {
                instance = resolved;
                return true;
            }
            instance = default;
            return false;
        }
    }
}

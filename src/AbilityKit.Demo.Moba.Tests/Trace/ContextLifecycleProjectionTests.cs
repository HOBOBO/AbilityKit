using AbilityKit.Context;
using AbilityKit.Demo.Moba.Services;
using AbilityKit.Trace;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Trace;

public sealed class ContextLifecycleProjectionTests
{
    [Fact]
    public void Lifecycle_source_replays_committed_nodes_and_disposal_stops_observation()
    {
        using var contexts = new MobaExecutionContextRegistry();
        var root = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillCast, 1001, 1, 2, frame: 10));
        contexts.End(root.ContextId, (int)MobaExecutionEndReason.Completed, 11);
        var events = new List<ContextLifecycleEvent<MobaExecutionContextNode>>();
        IContextLifecycleSource<MobaExecutionContextNode> source = contexts;

        var subscription = source.Observe(
            (in ContextLifecycleEvent<MobaExecutionContextNode> contextEvent) =>
                events.Add(contextEvent));

        Assert.Collection(
            events,
            item =>
            {
                Assert.Equal(ContextLifecycleEventKind.Created, item.Kind);
                Assert.True(item.IsReplay);
                Assert.Equal(root.ContextId, item.Node.ContextId);
            },
            item =>
            {
                Assert.Equal(ContextLifecycleEventKind.Ended, item.Kind);
                Assert.True(item.IsReplay);
                Assert.Equal(root.ContextId, item.Node.ContextId);
            });

        subscription.Dispose();
        contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.EffectExecution, 2001, 1, 2));
        Assert.Equal(2, events.Count);
    }

    [Fact]
    public void Trace_reconciles_lifecycle_after_context_restore()
    {
        using var contexts = new MobaExecutionContextRegistry();
        var root = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillCast, 1001, 1, 2, frame: 10));
        var activeChild = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.EffectExecution,
            2001,
            1,
            2,
            parentContextId: root.ContextId,
            frame: 11));
        using var trace = new MobaTraceRegistry();
        trace.AttachExecutionContexts(contexts);

        contexts.End(activeChild.ContextId, (int)MobaExecutionEndReason.Completed, 12);
        Assert.True(trace.TryGetSnapshot(activeChild.ContextId).IsEnded);

        contexts.RestoreLifecycle(new[] { root, activeChild });

        var restored = trace.TryGetSnapshot(activeChild.ContextId);
        Assert.False(restored.IsEnded);
        Assert.Equal(root.ContextId, restored.RootId);
        Assert.Equal(root.ContextId, restored.ParentId);
        trace.DetachExecutionContexts();
    }

    [Fact]
    public void Trace_removes_context_projection_when_context_source_is_cleared()
    {
        using var contexts = new MobaExecutionContextRegistry();
        var root = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillCast, 1001, 1, 2));
        using var trace = new MobaTraceRegistry();
        trace.AttachExecutionContexts(contexts);
        Assert.True(trace.Contains(root.ContextId));

        contexts.Clear();

        Assert.False(trace.Contains(root.ContextId));
        trace.DetachExecutionContexts();
    }

    [Fact]
    public void Trace_disposal_detaches_context_observation()
    {
        using var contexts = new MobaExecutionContextRegistry();
        var trace = new MobaTraceRegistry();
        trace.AttachExecutionContexts(contexts);

        trace.Dispose();
        var context = contexts.Create(new MobaExecutionContextCreateRequest(
            MobaExecutionKind.SkillCast, 1001, 1, 2));

        Assert.False(trace.Contains(context.ContextId));
    }
}

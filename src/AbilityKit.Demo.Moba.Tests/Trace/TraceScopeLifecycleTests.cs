using System.Reflection;
using AbilityKit.Trace;
using AbilityKit.Demo.Moba.Services;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Trace;

public sealed class TraceScopeLifecycleTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void Effect_service_releases_action_and_root_scopes(bool succeeded, bool aborted)
    {
        using var registry = new MobaTraceRegistry();
        var service = new MobaEffectExecutionService();
        typeof(MobaEffectExecutionService).GetProperty("Trace")!.SetValue(service, registry);
        var lineage = new MobaEffectLineageInput(EffectContextKind.Skill, MobaTraceKind.SkillEffect,
            7, 9, 0L, 0L, 0L, 801);
        Invoke(service, "BeginEffectTraceScope", 801, 802, lineage);
        var root = registry.RootIds.Single();
        service.EnterActionExecution(0, 901);
        Assert.True(registry.TryGetRootState(root, out var state));
        Assert.Equal(2, state.ExternalRefCount);
        var child = registry.GetNodeSnapshotsByRoot(root).Single(node => !node.IsRoot).ContextId;
        if (!aborted)
        {
            service.ExitActionExecution(0, 901, succeeded);
            service.ExitActionExecution(0, 901, succeeded);
            Assert.True(registry.TryGetRootState(root, out state));
            Assert.Equal(1, state.ExternalRefCount);
        }
        Invoke(service, "EndCurrentTrace", (int)(succeeded ? TraceLifecycleReason.Completed : TraceLifecycleReason.Failed));
        Assert.True(registry.TryGetRootState(root, out state));
        Assert.Equal(0, state.ExternalRefCount);
        Assert.Equal(0, state.ActiveCount);
        Assert.Equal((int)(succeeded ? TraceLifecycleReason.Completed : TraceLifecycleReason.Failed),
            registry.TryGetSnapshot(child).EndReason);
        Assert.Equal(1, registry.Purge(100));
    }

    private static object? Invoke(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);

    [Fact]
    public void Nested_scopes_balance_retains_and_allow_purge()
    {
        using var registry = new TestRegistry();
        var root = registry.CreateRootScope(1);
        var child = registry.CreateChildScope(root.RootId, 2);
        var grandchild = registry.CreateChildScope(child.ContextId, 3);
        Assert.Equal(3, State(registry, root.RootId).ExternalRefCount);

        root.End(10);
        root.Dispose();
        Assert.Equal(0, registry.Purge(100));
        grandchild.Dispose();
        child.Dispose();

        Assert.Equal(0, State(registry, root.RootId).ActiveCount);
        Assert.Equal(0, State(registry, root.RootId).ExternalRefCount);
        Assert.Equal(1, registry.Purge(100));
    }

    [Fact]
    public void Child_end_and_copied_dispose_release_once_and_keep_first_reason()
    {
        using var registry = new TestRegistry();
        var root = registry.CreateRoot(1);
        registry.RetainRoot(root);
        var child = registry.CreateChildScope(root, 2);
        var copy = child;
        child.End(7);
        copy.End(8);
        copy.Dispose();
        child.Dispose();

        Assert.False(child.IsValid);
        Assert.False(copy.IsValid);
        Assert.Equal(7, registry.TryGetSnapshot(child.ContextId).EndReason);
        Assert.Equal(1, State(registry, root).ExternalRefCount);
    }

    [Fact]
    public void Copied_root_dispose_does_not_release_another_consumer()
    {
        using var registry = new TestRegistry();
        var root = registry.CreateRootScope(1);
        var copy = root;
        registry.RetainRoot(root.RootId);
        root.Dispose();
        copy.Dispose();

        Assert.False(copy.IsValid);
        Assert.Equal(1, State(registry, root.RootId).ExternalRefCount);
        Assert.False(registry.TryGetSnapshot(root.RootId).IsEnded);
    }

    [Fact]
    public void Root_manual_release_then_dispose_does_not_release_another_consumer()
    {
        using var registry = new TestRegistry();
        var root = registry.CreateRootScope(1);
        registry.RetainRoot(root.RootId);
        root.Release();
        root.Release();
        root.Dispose();
        Assert.Equal(1, State(registry, root.RootId).ExternalRefCount);
    }

    [Fact]
    public void Root_extra_retains_remain_manual_and_can_be_released_after_dispose()
    {
        using var registry = new TestRegistry();
        var root = registry.CreateRootScope(1);
        root.Retain();
        root.Retain();
        root.Release();
        root.Dispose();
        root.Dispose();
        Assert.Equal(1, State(registry, root.RootId).ExternalRefCount);
        root.Retain();
        Assert.Equal(1, State(registry, root.RootId).ExternalRefCount);
        root.Release();
        root.Release();
        Assert.Equal(0, State(registry, root.RootId).ExternalRefCount);
    }

    [Fact]
    public void Clear_does_not_reuse_ids_or_let_old_scopes_mutate_new_nodes()
    {
        using var registry = new TestRegistry();
        var oldRoot = registry.CreateRootScope(1);
        var oldChild = registry.CreateChildScope(oldRoot.RootId, 2);
        var boundary = registry.NextContextId;
        registry.Clear();
        Assert.Equal(boundary, registry.NextContextId);
        var newRoot = registry.CreateRootScope(1);
        var newChild = registry.CreateChildScope(newRoot.RootId, 2);

        Assert.True(newRoot.RootId > oldChild.ContextId);
        Assert.False(oldRoot.IsValid);
        Assert.False(oldChild.IsValid);
        oldRoot.Retain();
        oldRoot.End(9);
        oldRoot.Release();
        oldRoot.Dispose();
        oldChild.End(9);
        oldChild.Dispose();
        registry.ReleaseRoot(oldRoot.RootId);
        registry.End(oldChild.ContextId, 9);

        Assert.Equal(2, State(registry, newRoot.RootId).ActiveCount);
        Assert.Equal(2, State(registry, newRoot.RootId).ExternalRefCount);
        Assert.False(registry.TryGetSnapshot(newChild.ContextId).IsEnded);
        newChild.Dispose();
        newRoot.End();
        newRoot.Dispose();
        Assert.Equal(1, registry.Purge(100));
    }

    [Fact]
    public void Clear_rejects_old_prediction_boundary_without_retracting_new_nodes()
    {
        using var registry = new TestRegistry();
        var staleBoundary = registry.NextContextId;
        registry.CreateRoot(1);
        registry.Clear();
        var currentBoundary = registry.NextContextId;
        var current = registry.CreateRootScope(1);
        Assert.Throws<InvalidOperationException>(() => registry.ValidatePredictionRetraction(staleBoundary));
        Assert.Throws<InvalidOperationException>(() => registry.RetractPrediction(staleBoundary));
        Assert.True(current.IsValid);
        Assert.Equal(1, State(registry, current.RootId).ExternalRefCount);
        Assert.Equal(1, registry.RetractPrediction(currentBoundary));
        current.Dispose();
    }

    [Fact]
    public void Retracted_child_scope_still_releases_its_retained_confirmed_root()
    {
        using var registry = new TestRegistry();
        var root = registry.CreateRootScope(1);
        var boundary = registry.NextContextId;
        var child = registry.CreateChildScope(root.RootId, 2);
        Assert.Equal(1, registry.RetractPrediction(boundary));
        Assert.False(child.IsValid);
        child.Dispose();
        child.Dispose();
        Assert.Equal(1, State(registry, root.RootId).ExternalRefCount);
        root.End();
        root.Dispose();
        Assert.Equal(1, registry.Purge(100));
    }

    [Fact]
    public void Retracted_root_scope_cannot_release_replayed_root()
    {
        using var registry = new TestRegistry();
        var boundary = registry.NextContextId;
        var old = registry.CreateRootScope(1);
        registry.RetractPrediction(boundary);
        var replay = registry.CreateRootScope(1);
        old.Dispose();
        Assert.True(replay.RootId > old.RootId);
        Assert.Equal(1, State(registry, replay.RootId).ExternalRefCount);
    }

    [Fact]
    public void Child_cleanup_releases_retain_even_when_end_observer_throws()
    {
        using var registry = new TestRegistry();
        var root = registry.CreateRootScope(1);
        var child = registry.CreateChildScope(root.RootId, 2);
        registry.RegistryEvent += evt =>
        {
            if (evt.Kind == TraceRegistryEventKind.NodeEnded)
                throw new InvalidOperationException("observer");
        };
        Assert.Throws<InvalidOperationException>(() => child.Dispose());
        child.Dispose();
        Assert.Equal(1, State(registry, root.RootId).ExternalRefCount);
        Assert.True(registry.TryGetSnapshot(child.ContextId).IsEnded);
    }

    [Fact]
    public void Origin_overloads_and_configured_end_reason_balance_retains()
    {
        using var registry = new TestRegistry();
        var origin = new TraceOrigin(kind: 1);
        var root = registry.CreateRootScope(in origin);
        var childOrigin = new TraceOrigin(kind: 2, parentContextId: root.RootId);
        var child = registry.CreateChildScope(in childOrigin);
        var other = registry.CreateChildScope(root.RootId, 3, endReason: 42);
        child.Dispose();
        other.Dispose();
        Assert.Equal(42, registry.TryGetSnapshot(other.ContextId).EndReason);
        Assert.Equal(1, State(registry, root.RootId).ExternalRefCount);
        root.End();
        root.Dispose();
        Assert.Equal(1, registry.Purge(100));
    }

    [Fact]
    public void Raw_begin_child_and_end_keep_manual_retain_semantics()
    {
        using var registry = new TestRegistry();
        var root = registry.CreateRoot(1);
        var child = registry.BeginChild(root, 2);
        registry.End(child);
        Assert.Equal(1, State(registry, root).ExternalRefCount);
        registry.ReleaseRoot(root);
        Assert.Equal(0, State(registry, root).ExternalRefCount);
    }

    [Fact]
    public void Default_scopes_are_safe_to_end_release_and_dispose()
    {
        var child = default(TraceTreeScope);
        var root = default(TraceRootScope);
        child.End();
        child.End(1);
        child.Dispose();
        root.Retain();
        root.Release();
        Assert.Equal(0, root.End());
        root.Dispose();
        Assert.False(child.IsValid);
        Assert.False(root.IsValid);
    }

    [Fact]
    public void Clear_and_dispose_remove_owned_metadata_and_leaf_mappings_only()
    {
        var metadata = new DictionaryTraceMetadataStore<TestMetadata>();
        var leaves = new DictionaryTraceLeafDataStore();
        using var registry = new TestRegistry(metadata, leaves);
        var root = registry.CreateRoot(1);
        var child = registry.CreateChild(root, 2);
        leaves.Set(root, new object());
        leaves.Set(child, new object());
        metadata.SetMetadata(999, new TestMetadata());
        leaves.Set(999, new object());
        var notified = false;
        registry.RegistryEvent += evt =>
        {
            if (evt.Kind != TraceRegistryEventKind.RegistryCleared) return;
            notified = true;
            Assert.False(metadata.TryGetMetadata(root, out _));
            Assert.False(leaves.TryGet(child, out _));
            Assert.Equal(0, registry.TotalNodeCount);
        };
        registry.Clear();
        Assert.True(notified);
        Assert.False(metadata.TryGetMetadata(child, out _));
        Assert.False(leaves.TryGet(root, out _));
        Assert.True(metadata.TryGetMetadata(999, out _));
        Assert.True(leaves.TryGet(999, out _));

        var next = registry.CreateRoot(1);
        leaves.Set(next, new object());
        registry.Dispose();
        Assert.False(metadata.TryGetMetadata(next, out _));
        Assert.False(leaves.TryGet(next, out _));
    }

    [Fact]
    public void Id_exhaustion_fails_without_wrapping_or_creating_nodes()
    {
        using var registry = new TestRegistry();
        typeof(TraceTreeRegistryBase).GetField("_nextId", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(registry, long.MaxValue);
        Assert.Throws<InvalidOperationException>(() => registry.CreateRoot(1));
        registry.Clear();
        Assert.Throws<InvalidOperationException>(() => registry.CreateRoot(1));
        Assert.Equal(0, registry.TotalNodeCount);
        Assert.Equal(long.MaxValue, registry.NextContextId);
    }

    private static RootState State(TestRegistry registry, long root)
    {
        Assert.True(registry.TryGetRootState(root, out var state));
        return state;
    }

    private sealed class TestMetadata : TraceMetadata { }

    private sealed class TestRegistry : TraceTreeRegistry<TestMetadata>
    {
        public TestRegistry() : base(new DictionaryTraceMetadataStore<TestMetadata>()) { }

        public TestRegistry(ITraceMetadataStore<TestMetadata> metadata, ITraceLeafDataStore leaves)
            : base(metadata, leafDataStore: leaves) { }

        protected override TestMetadata CreateMetadata(
            long rootId, int kind, long sourceActorId, long targetActorId,
            long originId, string originDisplay, long targetId, string targetDisplay, int configId) => new();
    }
}

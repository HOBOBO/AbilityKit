using System.Reflection;
using AbilityKit.Trace;
using AbilityKit.Demo.Moba.Services;
using Xunit;

namespace AbilityKit.Demo.Moba.Tests.Trace;

public sealed class TraceScopeLifecycleTests
{
    [Fact]
    public void Effect_service_keeps_context_lineage_when_trace_is_absent()
    {
        using var contexts = new MobaExecutionContextRegistry();
        var service = new MobaEffectExecutionService();
        typeof(MobaEffectExecutionService).GetProperty("ExecutionContexts", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(service, contexts);
        var lineage = new MobaEffectLineageInput(
            EffectContextKind.Skill,
            MobaExecutionKind.SkillEffect,
            7,
            9,
            0L,
            0L,
            0L,
            801);

        Invoke(service, "BeginEffectExecutionScope", 801, 802, lineage);
        var effectContextId = service.CurrentEffectContextId;
        service.EnterActionExecution(0, 901);
        var actionContextId = service.CurrentActionChain.Single();
        service.ExitActionExecution(0, 901, true);
        Invoke(service, "EndCurrentExecutionScope", (int)MobaExecutionEndReason.Completed);

        Assert.True(contexts.TryGet(effectContextId, out var effect));
        Assert.True(contexts.TryGet(actionContextId, out var action));
        Assert.Equal(MobaExecutionKind.EffectExecution, effect.Kind);
        Assert.Equal(MobaExecutionKind.EffectAction, action.Kind);
        Assert.Equal(effectContextId, action.ParentContextId);
        Assert.Equal(effectContextId, action.RootContextId);
        Assert.True(effect.IsEnded);
        Assert.True(action.IsEnded);
        Assert.Equal((int)MobaExecutionEndReason.Completed, effect.EndReason);
        Assert.Equal((int)MobaExecutionEndReason.Completed, action.EndReason);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void Effect_service_releases_action_and_root_scopes(bool succeeded, bool aborted)
    {
        using var registry = new MobaTraceRegistry();
        using var contexts = new MobaExecutionContextRegistry();
        var service = new MobaEffectExecutionService();
        typeof(MobaEffectExecutionService).GetProperty("ExecutionContexts", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(service, contexts);
        registry.OnInit(new ContextResolver(contexts));
        var lineage = new MobaEffectLineageInput(EffectContextKind.Skill, MobaExecutionKind.SkillEffect,
            7, 9, 0L, 0L, 0L, 801);
        Invoke(service, "BeginEffectExecutionScope", 801, 802, lineage);
        var root = registry.RootIds.Single();
        service.EnterActionExecution(0, 901);
        Assert.True(registry.TryGetRootState(root, out var state));
        Assert.Equal(0, state.ExternalRefCount);
        var child = registry.GetNodeSnapshotsByRoot(root).Single(node => !node.IsRoot).ContextId;
        if (!aborted)
        {
            service.ExitActionExecution(0, 901, succeeded);
            service.ExitActionExecution(0, 901, succeeded);
            Assert.True(registry.TryGetRootState(root, out state));
            Assert.Equal(0, state.ExternalRefCount);
        }
        Invoke(service, "EndCurrentExecutionScope", (int)(succeeded ? MobaExecutionEndReason.Completed : MobaExecutionEndReason.Failed));
        Assert.True(registry.TryGetRootState(root, out state));
        Assert.Equal(0, state.ExternalRefCount);
        Assert.Equal(0, state.ActiveCount);
        Assert.Equal((int)(succeeded ? MobaExecutionEndReason.Completed : MobaExecutionEndReason.Failed),
            registry.TryGetSnapshot(child).EndReason);
        Assert.Equal(1, registry.Purge(100));
        registry.OnDeinit(null!);
    }

    private sealed class ContextResolver : AbilityKit.Ability.World.DI.IWorldResolver
    {
        private readonly MobaExecutionContextRegistry _contexts;

        public ContextResolver(MobaExecutionContextRegistry contexts) => _contexts = contexts;
        public object? Resolve(Type serviceType) =>
            serviceType == typeof(MobaExecutionContextRegistry) ? _contexts : null;
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
    public void Child_cleanup_isolates_end_observer_failure_and_releases_retain()
    {
        using var registry = new TestRegistry();
        var root = registry.CreateRootScope(1);
        var child = registry.CreateChildScope(root.RootId, 2);
        var observerFailures = 0;
        registry.ObserverException = (_, exception) =>
        {
            Assert.IsType<InvalidOperationException>(exception);
            observerFailures++;
        };
        registry.RegistryEvent += evt =>
        {
            if (evt.Kind == TraceRegistryEventKind.NodeEnded)
                throw new InvalidOperationException("observer");
        };
        child.Dispose();
        child.Dispose();
        Assert.Equal(1, observerFailures);
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
    public void Purge_rejects_child_missing_active_and_retained_roots_without_mutation()
    {
        using var registry = new TestRegistry();
        var root = registry.CreateRoot(1);
        var child = registry.CreateChild(root, 2);
        var revision = registry.Revision;
        registry.PurgeRoot(child);
        Assert.False(registry.TryPurgeRoot(child));
        Assert.False(registry.TryPurgeRoot(long.MaxValue));
        Assert.False(registry.TryPurgeRoot(root));
        Assert.Equal(0, registry.Purge(100));
        Assert.Equal(revision, registry.Revision);
        Assert.True(registry.TryGetChildren(root, out var children));
        Assert.Equal(child, Assert.Single(children));
        Assert.True(registry.Contains(child));
        Assert.Equal(2, State(registry, root).ActiveCount);

        registry.End(root);
        Assert.False(registry.TryPurgeRoot(root));
        registry.RetainRoot(root);
        registry.End(child);
        Assert.False(registry.TryPurgeRoot(root));
        Assert.Equal(0, registry.Purge(100));
        Assert.Equal(1, State(registry, root).ExternalRefCount);
        registry.ReleaseRoot(root);
        Assert.True(registry.TryPurgeRoot(root));
        Assert.False(registry.TryPurgeRoot(root));
    }

    [Theory]
    [InlineData(false, 20)]
    [InlineData(true, 30)]
    public void Purge_waits_from_last_end_or_final_release(bool retained, int eligibleFrame)
    {
        using var registry = new TestRegistry();
        var root = registry.CreateRoot(1);
        var child = registry.CreateChild(root, 2);
        if (retained) registry.RetainRoot(root);
        registry.EndAtFrame(root, 0, 5);
        registry.EndAtFrame(child, 0, 20);
        if (retained)
        {
            Assert.Equal(0, registry.Purge(100, 10));
            registry.CurrentFrame = 30;
            registry.ReleaseRoot(root);
        }

        registry.CurrentFrame = eligibleFrame + 9;
        var revision = registry.Revision;
        registry.ReleaseRoot(root);
        Assert.Equal(revision, registry.Revision);
        Assert.Equal(eligibleFrame, State(registry, root).LastTouchedFrame);
        Assert.Equal(0, registry.Purge(eligibleFrame + 9, 10));
        Assert.Equal(1, registry.Purge(eligibleFrame + 10, 10));
    }

    [Fact]
    public void Purge_notifies_each_removed_node_after_clearing_the_whole_tree()
    {
        var metadata = new DictionaryTraceMetadataStore<TestMetadata>();
        var leaves = new DictionaryTraceLeafDataStore();
        using var registry = new TestRegistry(metadata, leaves);
        var root = registry.CreateRoot(1);
        var child = registry.CreateChild(root, 2);
        var leaf = registry.CreateChild(child, 3);
        registry.SetLeafData(leaf, new object());
        registry.EndRoot(root);

        var notifications = new List<(long Id, long Root, long Parent, bool Cleared)>();
        registry.RegistryEvent += evt =>
        {
            if (evt.Kind != TraceRegistryEventKind.RootPurged) return;
            notifications.Add((evt.ContextId, evt.RootId, evt.ParentId,
                registry.TotalNodeCount == 0 && !registry.TryGetRootState(root, out _)
                && !registry.TryGetChildren(root, out _) && !registry.TryGetChildren(child, out _)
                && !metadata.TryGetMetadata(root, out _) && !metadata.TryGetMetadata(child, out _)
                && !metadata.TryGetMetadata(leaf, out _) && !leaves.TryGet(leaf, out _)));
        };

        Assert.True(registry.TryPurgeRoot(root));
        Assert.Equal(new[] { (leaf, root, child, true), (child, root, root, true), (root, root, 0L, true) }, notifications);
    }

    [Fact]
    public void Batch_purge_rechecks_roots_that_a_notification_retains()
    {
        using var registry = new TestRegistry();
        var first = registry.CreateRoot(1);
        var second = registry.CreateRoot(1);
        registry.End(first);
        registry.End(second);
        registry.RegistryEvent += evt =>
        {
            if (evt.Kind == TraceRegistryEventKind.RootPurged)
                registry.RetainRoot(evt.RootId == first ? second : first);
        };

        Assert.Equal(1, registry.Purge(100));
        var remaining = Assert.Single(registry.GetRootStates());
        Assert.Equal(1, remaining.ExternalRefCount);
        registry.ReleaseRoot(remaining.RootId);
        Assert.Equal(1, registry.Purge(100));
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

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void Metadata_failure_does_not_commit_creation(bool child, bool externalId, bool storeFailure)
    {
        var store = new FailingMetadataStore();
        using var registry = new TestRegistry(store, new DictionaryTraceLeafDataStore()) { CurrentFrame = 10 };
        var root = registry.CreateRoot(1);
        var cursor = registry.NextContextId;
        var attemptedId = externalId ? 500 : cursor;
        var revision = registry.Revision;
        var notifications = new List<(long Id, TraceRegistryEventKind Kind, bool Complete)>();
        registry.RegistryEvent += evt => notifications.Add((evt.ContextId, evt.Kind,
            registry.TryGetNodeSnapshot(evt.ContextId, out var snapshot) && snapshot.Metadata != null
            && registry.TryGetChildren(evt.ContextId, out _)
            && State(registry, evt.RootId).ActiveCount == (child ? 2 : 1)
            && (!child || registry.TryGetChildren(root, out var siblings) && siblings.Contains(evt.ContextId))
            && registry.NextContextId == attemptedId + 1));
        registry.FailMetadataCreation = !storeFailure;
        store.FailWrite = storeFailure;
        registry.CurrentFrame = 20;

        long Create() => (child, externalId) switch
        {
            (true, true) => registry.CreateChildWithId(attemptedId, root, 2, createdFrame: 30),
            (true, false) => registry.CreateChild(root, 2),
            (false, true) => registry.CreateRootWithId(attemptedId, 2, createdFrame: 30),
            _ => registry.CreateRoot(2),
        };

        Assert.Throws<InvalidOperationException>(() => Create());
        Assert.Equal(cursor, registry.NextContextId);
        Assert.Equal(revision, registry.Revision);
        Assert.Empty(notifications);
        Assert.Equal(1, registry.TotalNodeCount);
        Assert.Single(registry.GetRootStates());
        Assert.Equal(1, State(registry, root).ActiveCount);
        Assert.Equal(10, State(registry, root).LastTouchedFrame);
        Assert.False(registry.Contains(attemptedId));
        Assert.False(registry.TryGetChildren(attemptedId, out _));
        Assert.False(store.TryGetMetadata(attemptedId, out _));
        Assert.True(registry.TryGetChildren(root, out var children));
        Assert.Empty(children);

        registry.FailMetadataCreation = false;
        store.FailWrite = false;
        Assert.Equal(attemptedId, Create());
        Assert.Equal((attemptedId, child ? TraceRegistryEventKind.ChildCreated : TraceRegistryEventKind.RootCreated, true),
            Assert.Single(notifications));
        Assert.Equal(externalId ? 30 : 20, registry.TryGetSnapshot(attemptedId).CreatedFrame);
        Assert.Equal(child && externalId ? root : attemptedId, registry.TryGetSnapshot(attemptedId).Metadata.FactoryId);
    }

    [Fact]
    public void Child_view_does_not_expose_writable_collection_and_tracks_registry_updates()
    {
        using var registry = new TestRegistry();
        var root = registry.CreateRoot(1);
        Assert.True(registry.TryGetChildren(root, out var children));
        Assert.Empty(children);
        Assert.False(children is IList<long>);
        Assert.False(children is System.Collections.ICollection);

        var firstChild = registry.CreateChild(root, 2);
        var boundary = registry.NextContextId;
        var secondChild = registry.CreateChild(root, 2);
        Assert.Equal(new[] { firstChild, secondChild }, children);
        Assert.True(registry.TryGetChildren(root, out var sameView));
        Assert.Same(children, sameView);
        Assert.Equal(1, registry.RetractPrediction(boundary));
        Assert.Equal(firstChild, Assert.Single(children));
    }

    [Fact]
    public void Local_creation_does_not_overwrite_an_external_id_at_the_cursor()
    {
        using var registry = new TestRegistry();
        var externalId = registry.NextContextId;
        registry.CreateRootWithId(externalId, 1, advanceAllocationFloor: false);
        Assert.Equal(externalId, registry.NextContextId);

        var localId = registry.CreateChild(externalId, 2);
        Assert.NotEqual(externalId, localId);
        Assert.Equal(1, registry.TryGetSnapshot(externalId).Kind);
        Assert.Equal(externalId, registry.TryGetSnapshot(localId).ParentId);
        Assert.Equal(localId + 1, registry.NextContextId);
    }

    private sealed class TestMetadata : TraceMetadata
    {
        public long FactoryId { get; init; }
    }

    private sealed class FailingMetadataStore : ITraceMetadataStore<TestMetadata>
    {
        private readonly DictionaryTraceMetadataStore<TestMetadata> _store = new();
        public bool FailWrite { get; set; }

        public void SetMetadata(long contextId, TestMetadata metadata)
        {
            if (FailWrite) throw new InvalidOperationException("metadata write failed");
            _store.SetMetadata(contextId, metadata);
        }

        public bool TryGetMetadata(long contextId, out TestMetadata metadata) => _store.TryGetMetadata(contextId, out metadata);
        public void Clear(long contextId) => _store.Clear(contextId);
    }

    private sealed class TestRegistry : TraceTreeRegistry<TestMetadata>
    {
        public TestRegistry() : base(new DictionaryTraceMetadataStore<TestMetadata>()) { }

        public TestRegistry(ITraceMetadataStore<TestMetadata> metadata, ITraceLeafDataStore leaves)
            : base(metadata, leafDataStore: leaves) { }

        public int CurrentFrame { get; set; }
        public bool FailMetadataCreation { get; set; }

        protected override int Frame => CurrentFrame;

        protected override TestMetadata CreateMetadata(
            long rootId, int kind, long sourceActorId, long targetActorId,
            long originId, string originDisplay, long targetId, string targetDisplay, int configId)
        {
            if (FailMetadataCreation) throw new InvalidOperationException("metadata factory failed");
            return new TestMetadata { FactoryId = rootId };
        }
    }
}

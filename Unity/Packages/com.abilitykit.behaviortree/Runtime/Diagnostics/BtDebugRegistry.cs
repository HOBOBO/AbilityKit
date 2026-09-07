using System.Collections.Generic;

namespace AbilityKit.BehaviorTree
{
    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public sealed class BtNodeDebugInfo
    {
        public string NodeId { get; }
        public string Name { get; }
        public string TypeId { get; }
        public BtNodeKind Kind { get; }
        public BtNodeState State { get; }
        public int Depth { get; }
        public int OnStackCount { get; }
        public int RunningChildIndex { get; }
        public string? SourceTreeId { get; }

        public BtNodeDebugInfo(
            string nodeId,
            string name,
            string typeId,
            BtNodeKind kind,
            BtNodeState state,
            int depth,
            int onStackCount,
            int runningChildIndex,
            string? sourceTreeId = null)
        {
            NodeId = nodeId;
            Name = name;
            TypeId = typeId;
            Kind = kind;
            State = state;
            Depth = depth;
            OnStackCount = onStackCount;
            RunningChildIndex = runningChildIndex;
            SourceTreeId = sourceTreeId;
        }
    }

    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public interface IBtTreeDebugView
    {
        string TreeId { get; }
        string DisplayName { get; }
        string OwnerLabel { get; }
        int NodeCount { get; }
        int LastFrame { get; }
        BtTreeDefinition TreeDefinition { get; }
        IReadOnlyDictionary<string, string>? NodeSourceTree { get; }
        IReadOnlyDictionary<string, string>? NodeSourceNode { get; }
        IReadOnlyList<BtSubtreeInstance> SubtreeInstances { get; }
        List<BtNodeDebugInfo> GetNodeStates();
        BtBlackboardValueSnapshot GetBlackboard();
        BtTreeRuntimeSnapshot CaptureState();
    }

    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public sealed class BtTreeDebugHandle
    {
        internal AbilityKit.BehaviorTree.Diagnostics.DebugHandle Inner { get; }
        internal long Id => Inner.Id;

        internal BtTreeDebugHandle(AbilityKit.BehaviorTree.Diagnostics.DebugHandle inner)
            => Inner = inner;
    }

    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public sealed class BtDebugRegistryEntry
    {
        public long Id { get; }
        public IBtTreeDebugView View { get; }

        internal BtDebugRegistryEntry(AbilityKit.BehaviorTree.Diagnostics.DebugRegistryEntry source)
        {
            Id = source.Id;
            View = ToLegacyView(source.View);
        }

        private static IBtTreeDebugView ToLegacyView(AbilityKit.BehaviorTree.Diagnostics.TreeDebugView view)
            => view is AbilityKit.BehaviorTree.Diagnostics.TreeDebugDeltaView deltaView
                ? new AbilityKit.BehaviorTree.Diagnostics.LegacyDebugDeltaViewAdapter(view, deltaView)
                : new AbilityKit.BehaviorTree.Diagnostics.LegacyDebugViewAdapter(view);
    }

    [System.Obsolete("Use the unprefixed BehaviorTree API in segmented namespaces.", false)]
    public static class BtDebugRegistry
    {
        public static BtTreeDebugHandle Register(IBtTreeDebugView view)
        {
            if (view == null) throw new System.ArgumentNullException(nameof(view));
            return new BtTreeDebugHandle(
                AbilityKit.BehaviorTree.Diagnostics.DebugRegistry.Register(
                    AbilityKit.BehaviorTree.Diagnostics.DebugViewAdapter.Create(view)));
        }

        public static void Unregister(BtTreeDebugHandle handle)
        {
            if (handle == null) return;
            AbilityKit.BehaviorTree.Diagnostics.DebugRegistry.Unregister(handle.Inner);
        }

        public static List<IBtTreeDebugView> GetViews()
        {
            var entries = AbilityKit.BehaviorTree.Diagnostics.DebugRegistry.GetEntries();
            var result = new List<IBtTreeDebugView>(entries.Count);
            foreach (var entry in entries)
            {
                result.Add(entry.View is AbilityKit.BehaviorTree.Diagnostics.TreeDebugDeltaView deltaView
                    ? new AbilityKit.BehaviorTree.Diagnostics.LegacyDebugDeltaViewAdapter(entry.View, deltaView)
                    : new AbilityKit.BehaviorTree.Diagnostics.LegacyDebugViewAdapter(entry.View));
            }
            return result;
        }

        public static List<BtDebugRegistryEntry> GetEntries()
        {
            var canonical = AbilityKit.BehaviorTree.Diagnostics.DebugRegistry.GetEntries();
            var result = new List<BtDebugRegistryEntry>(canonical.Count);
            foreach (var entry in canonical)
            {
                result.Add(new BtDebugRegistryEntry(entry));
            }
            return result;
        }

        public static void CopyEntries(List<BtDebugRegistryEntry> target)
        {
            if (target == null) throw new System.ArgumentNullException(nameof(target));
            target.Clear();
            target.AddRange(GetEntries());
        }

        public static int Count => AbilityKit.BehaviorTree.Diagnostics.DebugRegistry.Count;

        public static void ClearForTests()
            => AbilityKit.BehaviorTree.Diagnostics.DebugRegistry.ClearForTests();
    }
}

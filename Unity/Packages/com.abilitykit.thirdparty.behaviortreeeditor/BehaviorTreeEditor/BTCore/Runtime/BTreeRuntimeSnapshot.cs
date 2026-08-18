using System.Collections.Generic;

namespace BTCore.Runtime
{
    public interface IBTNodeRuntimeSnapshot
    {
        string RuntimeSnapshotType { get; }
        string CaptureRuntimeSnapshot();
        void RestoreRuntimeSnapshot(string payload);
    }

    /// <summary>
    /// Serializable runtime state for rollback. The tree definition itself is not included;
    /// RestoreRuntimeSnapshot validates the current definition before applying this state.
    /// </summary>
    public sealed class BTreeRuntimeSnapshot
    {
        public int Version { get; set; } = 1;
        public NodeState TreeState { get; set; }
        public List<BTreeNodeRuntimeSnapshot> Nodes { get; set; } = new();
        public List<BTreeBlackboardValueSnapshot> BlackboardValues { get; set; } = new();
        public List<BTreeConditionalReevaluateSnapshot> ConditionalReevaluates { get; set; } = new();
        public List<BTreeRunStackSnapshot> RunStacks { get; set; } = new();
        public int PreIndex { get; set; }
        public NodeState PreState { get; set; }
    }

    public sealed class BTreeNodeRuntimeSnapshot
    {
        public string Guid { get; set; }
        public NodeState State { get; set; }
        public int ChildIndex { get; set; } = -1;
        public string CustomSnapshotType { get; set; }
        public string CustomSnapshot { get; set; }
    }

    public sealed class BTreeBlackboardValueSnapshot
    {
        public string Name { get; set; }
        public string TypeName { get; set; }
        public string JsonValue { get; set; }
    }

    public sealed class BTreeConditionalReevaluateSnapshot
    {
        public int Index { get; set; }
        public NodeState State { get; set; }
        public int CompositeIndex { get; set; }
    }

    public sealed class BTreeRunStackSnapshot
    {
        public List<int> Nodes { get; set; } = new();
    }
}

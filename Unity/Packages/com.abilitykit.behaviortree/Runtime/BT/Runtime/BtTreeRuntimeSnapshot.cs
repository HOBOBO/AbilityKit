using System.Collections.Generic;

namespace AbilityKit.BehaviorTree
{
    /// <summary>
    /// 树运行时快照（只含运行态，不含树定义）。恢复前用 DefinitionHash 校验当前定义兼容。
    /// 节点顺序与引擎扁平化顺序一致，NodeId 逐项比对。
    /// </summary>
    public sealed class BtTreeRuntimeSnapshot
    {
        public int SnapshotVersion { get; set; } = 1;
        public long DefinitionHash { get; set; }
        public bool Enabled { get; set; }
        public BtNodeState TreeState { get; set; }
        public List<BtNodeRuntimeSnapshot> Nodes { get; set; } = new();
        public List<BtRunStackSnapshot> RunStacks { get; set; } = new();
        public List<BtConditionalReevaluateSnapshot> ConditionalReevaluates { get; set; } = new();
        public BtBlackboardValueSnapshot? Blackboard { get; set; }
    }

    public sealed class BtNodeRuntimeSnapshot
    {
        public string NodeId { get; set; } = "";
        public BtNodeState State { get; set; }
        public int RunningChildIndex { get; set; } = -1;
        public string? CustomState { get; set; }
        public ulong RandomS0 { get; set; }
        public ulong RandomS1 { get; set; }
        public ulong RandomSequence { get; set; }
    }

    /// <summary>运行栈（自底向顶的扁平索引序列）。</summary>
    public sealed class BtRunStackSnapshot
    {
        public List<int> NodeIndexes { get; set; } = new();
    }

    public sealed class BtConditionalReevaluateSnapshot
    {
        public int Index { get; set; }
        public BtNodeState State { get; set; }
        public int CompositeIndex { get; set; }
        public int BranchIndex { get; set; }
    }
}

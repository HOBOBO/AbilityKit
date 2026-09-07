using System.Collections.Generic;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Execution
{
    /// <summary>
    /// 树运行时快照（只含运行态，不含树定义）。恢复前DefinitionHash 校验当前定义兼容    /// 节点顺序与引擎扁平化顺序一致，NodeId 逐项比对    /// </summary>
    public sealed class TreeRuntimeSnapshot
    {
        public const int CurrentSnapshotVersion = 1;

        public int SnapshotVersion { get; set; } = CurrentSnapshotVersion;
        public long DefinitionHash { get; set; }
        public bool Enabled { get; set; }
        public NodeState TreeState { get; set; }
        public List<NodeRuntimeSnapshot> Nodes { get; set; } = new();
        public List<RunStackSnapshot> RunStacks { get; set; } = new();
        public List<ConditionalReevaluateSnapshot> ConditionalReevaluates { get; set; } = new();
        public BlackboardValueSnapshot? Blackboard { get; set; }
    }

}

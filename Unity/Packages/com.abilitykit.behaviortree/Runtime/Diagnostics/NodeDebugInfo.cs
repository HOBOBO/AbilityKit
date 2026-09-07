using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Deterministic;

namespace AbilityKit.BehaviorTree.Diagnostics
{
    using AbilityKit.BehaviorTree.Blackboard;
    using AbilityKit.BehaviorTree.Definition;
    using AbilityKit.BehaviorTree.Execution;
    using AbilityKit.BehaviorTree.Registry;

    public sealed class NodeDebugInfo
    {
        public string NodeId { get; }
        public string Name { get; }
        public string TypeId { get; }
        public NodeKind Kind { get; }
        public NodeState State { get; }
        public int Depth { get; }
        public int OnStackCount { get; }
        public int RunningChildIndex { get; }
        public string? SourceTreeId { get; }

        public NodeDebugInfo(
            string nodeId,
            string name,
            string typeId,
            NodeKind kind,
            NodeState state,
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

        internal NodeDebugInfo(AbilityKit.BehaviorTree.BtNodeDebugInfo source)
            : this(
                source.NodeId,
                source.Name,
                source.TypeId,
                source.Kind.ToApi(),
                source.State.ToApi(),
                source.Depth,
                source.OnStackCount,
                source.RunningChildIndex,
                source.SourceTreeId)
        {
        }
    }
}

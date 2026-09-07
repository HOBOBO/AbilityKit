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

    public sealed class TreeDebugDelta
    {
        public long Sequence { get; set; }
        public bool IsFull { get; set; }
        public int LastFrame { get; set; }
        public List<NodeDebugInfo> Nodes { get; set; } = new();
        public BlackboardValueSnapshot? Blackboard { get; set; }

        internal static TreeDebugDelta FromLegacy(AbilityKit.BehaviorTree.BtTreeDebugDelta source)
        {
            var delta = new TreeDebugDelta
            {
                Sequence = source.Sequence,
                IsFull = source.IsFull,
                LastFrame = source.LastFrame,
                Blackboard = source.Blackboard == null ? null : BlackboardValueSnapshot.FromLegacy(source.Blackboard),
            };
            foreach (var node in source.Nodes) delta.Nodes.Add(new NodeDebugInfo(node));
            return delta;
        }

        internal AbilityKit.BehaviorTree.BtTreeDebugDelta ToLegacy()
        {
            var delta = new AbilityKit.BehaviorTree.BtTreeDebugDelta
            {
                Sequence = Sequence,
                IsFull = IsFull,
                LastFrame = LastFrame,
                Blackboard = Blackboard?.ToLegacy(),
            };
            foreach (var node in Nodes)
            {
                delta.Nodes.Add(new AbilityKit.BehaviorTree.BtNodeDebugInfo(
                    node.NodeId,
                    node.Name,
                    node.TypeId,
                    node.Kind.ToLegacy(),
                    node.State.ToLegacy(),
                    node.Depth,
                    node.OnStackCount,
                    node.RunningChildIndex,
                    node.SourceTreeId));
            }
            return delta;
        }
    }
}

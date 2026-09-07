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

    internal class DebugViewAdapter : TreeDebugView
    {
        private readonly AbilityKit.BehaviorTree.IBtTreeDebugView _inner;

        protected DebugViewAdapter(AbilityKit.BehaviorTree.IBtTreeDebugView inner) => _inner = inner;

        public static TreeDebugView Create(AbilityKit.BehaviorTree.IBtTreeDebugView inner)
            => inner is AbilityKit.BehaviorTree.IBtTreeDebugDeltaView deltaView
                ? new DebugDeltaViewAdapter(inner, deltaView)
                : new DebugViewAdapter(inner);

        public string TreeId => _inner.TreeId;
        public string DisplayName => _inner.DisplayName;
        public string OwnerLabel => _inner.OwnerLabel;
        public int NodeCount => _inner.NodeCount;
        public int LastFrame => _inner.LastFrame;
        public TreeDefinition TreeDefinition => TreeDefinition.FromLegacy(_inner.TreeDefinition);
        public IReadOnlyDictionary<string, string>? NodeSourceTree => _inner.NodeSourceTree;
        public IReadOnlyDictionary<string, string>? NodeSourceNode => _inner.NodeSourceNode;
        public IReadOnlyList<SubtreeInstance> SubtreeInstances
        {
            get
            {
                var result = new List<SubtreeInstance>(_inner.SubtreeInstances.Count);
                foreach (var instance in _inner.SubtreeInstances)
                {
                    result.Add(new SubtreeInstance(instance.InlinedRootNodeId, instance.ReferencedTreeId));
                }
                return result;
            }
        }

        public List<NodeDebugInfo> GetNodeStates()
        {
            var legacy = _inner.GetNodeStates();
            var result = new List<NodeDebugInfo>(legacy.Count);
            foreach (var node in legacy) result.Add(new NodeDebugInfo(node));
            return result;
        }

        public BlackboardValueSnapshot GetBlackboard() => BlackboardValueSnapshot.FromLegacy(_inner.GetBlackboard());
        public TreeRuntimeSnapshot CaptureState() => _inner.CaptureState().ToCanonical();
    }
}

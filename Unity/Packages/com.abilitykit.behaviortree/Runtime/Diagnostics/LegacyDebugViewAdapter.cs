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

    internal class LegacyDebugViewAdapter : AbilityKit.BehaviorTree.IBtTreeDebugView
    {
        private readonly TreeDebugView _inner;

        public LegacyDebugViewAdapter(TreeDebugView inner) => _inner = inner;

        public string TreeId => _inner.TreeId;
        public string DisplayName => _inner.DisplayName;
        public string OwnerLabel => _inner.OwnerLabel;
        public int NodeCount => _inner.NodeCount;
        public int LastFrame => _inner.LastFrame;
        public AbilityKit.BehaviorTree.BtTreeDefinition TreeDefinition => _inner.TreeDefinition.ToLegacy();
        public IReadOnlyDictionary<string, string>? NodeSourceTree => _inner.NodeSourceTree;
        public IReadOnlyDictionary<string, string>? NodeSourceNode => _inner.NodeSourceNode;
        public IReadOnlyList<AbilityKit.BehaviorTree.BtSubtreeInstance> SubtreeInstances
        {
            get
            {
                var result = new List<AbilityKit.BehaviorTree.BtSubtreeInstance>(_inner.SubtreeInstances.Count);
                foreach (var instance in _inner.SubtreeInstances)
                {
                    result.Add(new AbilityKit.BehaviorTree.BtSubtreeInstance(
                        instance.InlinedRootNodeId,
                        instance.ReferencedTreeId));
                }
                return result;
            }
        }

        public List<AbilityKit.BehaviorTree.BtNodeDebugInfo> GetNodeStates()
        {
            var source = _inner.GetNodeStates();
            var result = new List<AbilityKit.BehaviorTree.BtNodeDebugInfo>(source.Count);
            foreach (var node in source)
            {
                result.Add(new AbilityKit.BehaviorTree.BtNodeDebugInfo(
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
            return result;
        }

        public AbilityKit.BehaviorTree.BtBlackboardValueSnapshot GetBlackboard()
            => _inner.GetBlackboard().ToLegacy();

        public AbilityKit.BehaviorTree.BtTreeRuntimeSnapshot CaptureState()
            => AbilityKit.BehaviorTree.BtTreeRuntimeSnapshot.FromCanonical(_inner.CaptureState());
    }
}

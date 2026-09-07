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

    public interface TreeDebugView
    {
        string TreeId { get; }
        string DisplayName { get; }
        string OwnerLabel { get; }
        int NodeCount { get; }
        int LastFrame { get; }
        TreeDefinition TreeDefinition { get; }
        IReadOnlyDictionary<string, string>? NodeSourceTree { get; }
        IReadOnlyDictionary<string, string>? NodeSourceNode { get; }
        IReadOnlyList<SubtreeInstance> SubtreeInstances { get; }
        List<NodeDebugInfo> GetNodeStates();
        BlackboardValueSnapshot GetBlackboard();
        TreeRuntimeSnapshot CaptureState();
    }
}

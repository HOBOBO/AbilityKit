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




    }
}

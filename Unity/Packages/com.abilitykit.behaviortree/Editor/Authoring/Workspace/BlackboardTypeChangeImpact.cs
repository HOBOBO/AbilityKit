#nullable enable

using System.Collections.Generic;
using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;

namespace AbilityKit.BehaviorTree.Editor.Authoring.Workspace
{
    internal sealed class BlackboardTypeChangeImpact
    {
        public BlackboardTypeChangeImpact(string keyName, ValueType fromType, ValueType toType)
        {
            KeyName = keyName ?? "";
            FromType = fromType;
            ToType = toType;
        }

        public string KeyName { get; }
        public ValueType FromType { get; }
        public ValueType ToType { get; }
        public List<BlackboardUsage> Usages { get; } = new();
        public List<string> Warnings { get; } = new();
        public bool ChangesType => FromType != ToType;
        public bool HasImpact => ChangesType && (Usages.Count > 0 || Warnings.Count > 0);
    }
}

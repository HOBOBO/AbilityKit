using AbilityKit.BehaviorTree.Authoring.Model;
using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Diagnostics;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Nodes;
using AbilityKit.BehaviorTree.Registry;
using AbilityKit.BehaviorTree.Serialization;
using ValueType = AbilityKit.BehaviorTree.Definition.ValueType;
#nullable enable

namespace AbilityKit.BehaviorTree.Editor.Authoring.Workspace
{
    internal sealed class AuthoringBatchPropertyField
    {
        public AuthoringBatchPropertyField(
            PropertyField schema,
            AuthoringBatchValueState state,
            PropertyValue? sharedValue,
            int availableNodeCount)
        {
            Schema = schema;
            State = state;
            SharedValue = sharedValue;
            AvailableNodeCount = availableNodeCount;
        }

        public PropertyField Schema { get; }
        public AuthoringBatchValueState State { get; }
        public PropertyValue? SharedValue { get; }
        public int AvailableNodeCount { get; }
        public bool CanApply => AvailableNodeCount > 0;
    }
}

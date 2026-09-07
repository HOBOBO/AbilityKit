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
    internal sealed class BlackboardUsage
    {
        public BlackboardUsage(
            string keyName,
            string nodeId,
            string nodeType,
            string propertyName,
            ValueType? declaredType,
            AuthoringBlackboardAccess access,
            AuthoringJumpTarget jumpTarget)
        {
            KeyName = keyName ?? "";
            NodeId = nodeId ?? "";
            NodeType = nodeType ?? "";
            PropertyName = propertyName ?? "";
            DeclaredType = declaredType;
            Access = access;
            JumpTarget = jumpTarget;
        }

        public string KeyName { get; }
        public string NodeId { get; }
        public string NodeType { get; }
        public string PropertyName { get; }
        public ValueType? DeclaredType { get; }
        public AuthoringBlackboardAccess Access { get; }
        public AuthoringJumpTarget JumpTarget { get; }
    }
}

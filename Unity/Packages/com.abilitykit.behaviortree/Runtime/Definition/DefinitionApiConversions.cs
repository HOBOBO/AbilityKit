namespace AbilityKit.BehaviorTree.Definition
{
    internal static class DefinitionApiConversions
    {
        public static AbilityKit.BehaviorTree.BtNodeState ToLegacy(this NodeState value) => (AbilityKit.BehaviorTree.BtNodeState)(int)value;
        public static NodeState ToApi(this AbilityKit.BehaviorTree.BtNodeState value) => (NodeState)(int)value;
        public static AbilityKit.BehaviorTree.BtAbortType ToLegacy(this AbortType value) => (AbilityKit.BehaviorTree.BtAbortType)(int)value;
        public static AbortType ToApi(this AbilityKit.BehaviorTree.BtAbortType value) => (AbortType)(int)value;
        public static AbilityKit.BehaviorTree.BtNodeKind ToLegacy(this NodeKind value) => (AbilityKit.BehaviorTree.BtNodeKind)(int)value;
        public static NodeKind ToApi(this AbilityKit.BehaviorTree.BtNodeKind value) => (NodeKind)(int)value;
        public static AbilityKit.BehaviorTree.BtValueType ToLegacy(this ValueType value) => (AbilityKit.BehaviorTree.BtValueType)(int)value;
        public static ValueType ToApi(this AbilityKit.BehaviorTree.BtValueType value) => (ValueType)(int)value;
    }
}

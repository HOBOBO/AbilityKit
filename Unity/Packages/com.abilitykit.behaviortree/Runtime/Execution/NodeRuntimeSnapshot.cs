using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Execution
{
    public sealed class NodeRuntimeSnapshot
    {
        public string NodeId { get; set; } = "";
        public NodeState State { get; set; }
        public int RunningChildIndex { get; set; } = -1;
        public string? CustomState { get; set; }
        public ulong RandomS0 { get; set; }
        public ulong RandomS1 { get; set; }
        public ulong RandomSequence { get; set; }
    }
}

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Execution
{
    public sealed class ConditionalReevaluateSnapshot
    {
        public int Index { get; set; }
        public NodeState State { get; set; }
        public int CompositeIndex { get; set; }
        public int BranchIndex { get; set; }
    }
}

using AbilityKit.Deterministic;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>强制失败：子 Success 也返Failure（Running 透传�?/summary>
    public class ForceFailureNode : DecoratorNode
    {
        protected internal override bool CanExecute()
            => State is NodeState.Inactive or NodeState.Running;

        protected internal override void OnChildExecuted(int childIndex, NodeState childState)
            => State = childState;

        public override NodeState Decorate(NodeState state)
            => state == NodeState.Success ? NodeState.Failure : state;
    }
}

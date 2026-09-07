using AbilityKit.Deterministic;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>强制成功：子 Failure 也返Success（Running 透传�?/summary>
    public class ForceSuccessNode : DecoratorNode
    {
        protected internal override bool CanExecute()
            => State is NodeState.Inactive or NodeState.Running;

        protected internal override void OnChildExecuted(int childIndex, NodeState childState)
            => State = childState;

        public override NodeState Decorate(NodeState state)
            => state == NodeState.Failure ? NodeState.Success : state;
    }
}

using AbilityKit.Deterministic;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>反转节点：子 Success -> Failure，子 Failure -> Success</summary>
    public class InverterNode : DecoratorNode
    {
        protected internal override bool CanExecute()
            => State is NodeState.Inactive or NodeState.Running;

        protected internal override void OnChildExecuted(int childIndex, NodeState childState)
            => State = childState;

        public override NodeState Decorate(NodeState state) => state switch
        {
            NodeState.Failure => NodeState.Success,
            NodeState.Success => NodeState.Failure,
            _ => state,
        };
    }
}

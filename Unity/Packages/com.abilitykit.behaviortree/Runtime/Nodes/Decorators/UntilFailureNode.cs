using AbilityKit.Deterministic;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>直到失败：子节点 Success 时下tick 重试，Failure 时以 Failure 完成</summary>
    public class UntilFailureNode : DecoratorNode
    {
        protected internal override bool CanExecute() => State != NodeState.Failure;

        protected internal override void OnChildExecuted(int childIndex, NodeState childState)
            => State = childState;
    }
}

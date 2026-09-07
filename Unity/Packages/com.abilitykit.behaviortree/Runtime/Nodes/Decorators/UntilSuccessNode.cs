using AbilityKit.Deterministic;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>直到成功：子节点 Failure 时下tick 重试，Success 时以 Success 完成</summary>
    public class UntilSuccessNode : DecoratorNode
    {
        protected internal override bool CanExecute() => State != NodeState.Success;

        protected internal override void OnChildExecuted(int childIndex, NodeState childState)
            => State = childState;
    }
}

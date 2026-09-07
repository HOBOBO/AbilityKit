using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>选择节点：依次执行子节点，任一 Success 立即 Success，全Failure Failure</summary>
    public class SelectorNode : CompositeNode
    {
        public override void OnStart(AbilityKit.BehaviorTree.Execution.ExecutionContext context)
        {
            RunningIndex = 0;
        }

        protected internal override bool CanExecute()
            => RunningIndex < ChildCount && State != NodeState.Success;

        protected internal override void OnChildExecuted(int childIndex, NodeState childState)
        {
            State = childState == NodeState.Failure
                ? (++RunningIndex >= ChildCount ? NodeState.Failure : NodeState.Running)
                : childState;
        }

        protected internal override void OnConditionalAbort(int childIndex)
        {
            RunningIndex = childIndex;
            State = NodeState.Running;
        }
    }
}

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>顺序节点：依次执行子节点，全Success Success，任一 Failure 立即 Failure</summary>
    public class SequenceNode : CompositeNode
    {
        public override void OnStart(AbilityKit.BehaviorTree.Execution.ExecutionContext context)
        {
            RunningIndex = 0;
        }

        protected internal override bool CanExecute()
            => RunningIndex < ChildCount && State != NodeState.Failure;

        protected internal override void OnChildExecuted(int childIndex, NodeState childState)
        {
            RunningIndex++;
            State = childState == NodeState.Success
                ? (RunningIndex >= ChildCount ? NodeState.Success : NodeState.Running)
                : childState;
        }

        protected internal override void OnConditionalAbort(int childIndex)
        {
            RunningIndex = childIndex;
            State = NodeState.Running;
        }
    }
}

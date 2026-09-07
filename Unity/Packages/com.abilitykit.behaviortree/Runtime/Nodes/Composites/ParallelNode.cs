using System.Text;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>
    /// 并行节点：所有子节点各占一个运行栈分支
    /// RequireAll（默认）：任一 Failure 立即 Failure，全部完成才 Success
    /// FirstSuccess：任一 Success Failure 立即完成
    /// </summary>
    public class ParallelNode : CompositeNode, NodeStateful
    {
        public const string SuccessPolicyProperty = "successPolicy";

        private NodeState[] _childStates = System.Array.Empty<NodeState>();
        private bool _firstSuccess;

        protected override void OnCompositeInit(in NodeInitContext context)
        {
            _firstSuccess = context.Properties.GetInt64(SuccessPolicyProperty, 0) == 1;
            _childStates = new NodeState[context.ChildCount];
        }

        public override void OnStart(AbilityKit.BehaviorTree.Execution.ExecutionContext context)
        {
            RunningIndex = 0;
            for (var i = 0; i < _childStates.Length; i++) _childStates[i] = NodeState.Inactive;
        }

        protected internal override void OnChildStart()
        {
            _childStates[RunningIndex++] = NodeState.Running;
        }

        protected internal override void OnChildExecuted(int childIndex, NodeState childState)
        {
            _childStates[childIndex] = childState;
        }

        protected internal override bool CanExecute() => RunningIndex < ChildCount;

        protected internal override void OnConditionalAbort(int childIndex)
        {
            RunningIndex = 0;
            for (var i = 0; i < _childStates.Length; i++) _childStates[i] = NodeState.Inactive;
        }

        protected internal override bool CanRunParallel() => true;

        protected internal override NodeState OverrideState(NodeState state)
        {
            var allComplete = true;
            for (var i = 0; i < _childStates.Length; i++)
            {
                if (_childStates[i] == NodeState.Running)
                {
                    allComplete = false;
                }
                else if (_childStates[i] == NodeState.Failure)
                {
                    return State = NodeState.Failure;
                }
                else if (_firstSuccess && _childStates[i] == NodeState.Success)
                {
                    return State = NodeState.Success;
                }
            }

            return State = allComplete ? NodeState.Success : NodeState.Running;
        }

        public string CaptureState()
        {
            var builder = new StringBuilder();
            for (var i = 0; i < _childStates.Length; i++)
            {
                if (i > 0) builder.Append(',');
                builder.Append((int)_childStates[i]);
            }
            return builder.ToString();
        }

        public void RestoreState(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                for (var i = 0; i < _childStates.Length; i++) _childStates[i] = NodeState.Inactive;
                return;
            }

            var parts = payload.Split(',');
            if (parts.Length != _childStates.Length)
                throw new System.InvalidOperationException("BT parallel snapshot child count mismatch.");
            for (var i = 0; i < parts.Length; i++) _childStates[i] = (NodeState)int.Parse(parts[i]);
        }
    }
}

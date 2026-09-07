using AbilityKit.Deterministic;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>仅执行一次子节点，后tick 直接返回配置结果</summary>
    public class OnceNode : DecoratorNode, NodeStateful
    {
        public const string ResultAfterFirstProperty = "resultAfterFirst"; // 0=Failure, 1=Success

        private NodeState _result = NodeState.Failure;
        private bool _executed;

        protected override void OnInitParent(in NodeInitContext context)
        {
            _result = context.Properties.GetInt64(ResultAfterFirstProperty, 0) == 1
                ? NodeState.Success
                : NodeState.Failure;
        }

        protected internal override bool CanExecute()
            => State is NodeState.Inactive or NodeState.Running;

        protected internal override bool TryTickOverride(AbilityKit.BehaviorTree.Execution.ExecutionContext context, out NodeState state)
        {
            if (_executed)
            {
                state = _result;
                return true;
            }

            state = default;
            return false;
        }

        protected internal override void OnChildExecuted(int childIndex, NodeState childState)
        {
            _executed = true;
            State = childState;
        }

        public string CaptureState() => _executed ? "1" : "0";

        public void RestoreState(string payload)
        {
            _executed = payload == "1";
        }
    }
}

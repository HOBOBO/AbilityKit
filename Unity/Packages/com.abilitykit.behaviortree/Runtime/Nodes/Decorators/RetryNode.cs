using AbilityKit.Deterministic;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>子节点失败时按配置次数重试，成功时立即完�?/summary>
    public class RetryNode : DecoratorNode, NodeStateful
    {
        public const string CountProperty = "count";

        private long _count = 1;
        private long _attempts;

        protected override void OnInitParent(in NodeInitContext context)
        {
            _count = context.Properties.GetInt64(CountProperty, 1);
        }

        public override void OnStart(AbilityKit.BehaviorTree.Execution.ExecutionContext context)
        {
            _attempts = 0;
        }

        protected internal override bool CanExecute()
            => State is NodeState.Inactive or NodeState.Running;

        protected internal override void OnChildExecuted(int childIndex, NodeState childState)
        {
            if (childState == NodeState.Success)
            {
                State = NodeState.Success;
                return;
            }

            _attempts++;
            State = _count >= 0 && _attempts > _count ? NodeState.Failure : NodeState.Running;
        }

        public string CaptureState() => _attempts.ToString();

        public void RestoreState(string payload)
        {
            _attempts = long.Parse(payload);
        }
    }
}

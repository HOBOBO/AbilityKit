using AbilityKit.Deterministic;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>按配置次数重复执行子节点；负数次数表示无限重�?/summary>
    public class RepeaterNode : DecoratorNode, NodeStateful
    {
        public const string CountProperty = "count";

        private long _count = 1;
        private long _completed;

        protected override void OnInitParent(in NodeInitContext context)
        {
            _count = context.Properties.GetInt64(CountProperty, 1);
        }

        public override void OnStart(AbilityKit.BehaviorTree.Execution.ExecutionContext context)
        {
            _completed = 0;
        }

        protected internal override bool CanExecute()
            => _count < 0 || _completed < _count;

        protected internal override void OnChildExecuted(int childIndex, NodeState childState)
        {
            _completed++;
            State = childState != NodeState.Success
                ? childState
                : (CanExecute() ? NodeState.Running : NodeState.Success);
        }

        public string CaptureState() => _completed.ToString();

        public void RestoreState(string payload)
        {
            _completed = long.Parse(payload);
        }
    }
}

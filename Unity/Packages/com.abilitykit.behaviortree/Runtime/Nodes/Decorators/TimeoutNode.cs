using AbilityKit.Deterministic;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>限制子节点执行时长，超过确定性截止时间后返回失败</summary>
    public class TimeoutNode : DecoratorNode, NodeStateful
    {
        public const string DurationSecondsProperty = "durationSeconds";

        private Fixed64 _duration = Fixed64.One;
        private Fixed64 _deadline;

        protected override void OnInitParent(in NodeInitContext context)
        {
            _duration = context.Properties.GetFixed64(DurationSecondsProperty, Fixed64.One);
        }

        protected internal override bool CanExecute()
            => State is NodeState.Inactive or NodeState.Running;

        protected internal override void OnChildExecuted(int childIndex, NodeState childState)
            => State = childState;

        public override void OnStart(AbilityKit.BehaviorTree.Execution.ExecutionContext context)
        {
            _deadline = context.Time + _duration;
        }

        protected internal override bool TryTickOverride(AbilityKit.BehaviorTree.Execution.ExecutionContext context, out NodeState state)
        {
            if (State == NodeState.Running && context.Time >= _deadline)
            {
                state = NodeState.Failure;
                return true;
            }

            state = default;
            return false;
        }

        public string CaptureState() => _deadline.RawValue.ToString();

        public void RestoreState(string payload)
        {
            _deadline = Fixed64.FromRaw(long.Parse(payload));
        }
    }
}

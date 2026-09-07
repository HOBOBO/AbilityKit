using AbilityKit.Deterministic;

using AbilityKit.BehaviorTree.Blackboard;
using AbilityKit.BehaviorTree.Definition;
using AbilityKit.BehaviorTree.Execution;
using AbilityKit.BehaviorTree.Registry;
namespace AbilityKit.BehaviorTree.Nodes
{
    /// <summary>在冷却期内直接返回配置结果，子树完成后重新开始计�?/summary>
    public class CooldownNode : DecoratorNode, NodeStateful
    {
        public const string CooldownSecondsProperty = "cooldownSeconds";
        public const string ResultOnCooldownProperty = "resultOnCooldown"; // 0=Failure, 1=Success

        private Fixed64 _cooldown = Fixed64.One;
        private NodeState _result = NodeState.Failure;
        private Fixed64 _readyAt;
        private bool _gateFired;

        protected override void OnInitParent(in NodeInitContext context)
        {
            _cooldown = context.Properties.GetFixed64(CooldownSecondsProperty, Fixed64.One);
            _result = context.Properties.GetInt64(ResultOnCooldownProperty, 0) == 1
                ? NodeState.Success
                : NodeState.Failure;
        }

        protected internal override bool CanExecute()
            => State is NodeState.Inactive or NodeState.Running;

        protected internal override void OnChildExecuted(int childIndex, NodeState childState)
            => State = childState;

        protected internal override bool TryTickOverride(AbilityKit.BehaviorTree.Execution.ExecutionContext context, out NodeState state)
        {
            if (State == NodeState.Running && context.Time < _readyAt)
            {
                _gateFired = true;
                state = _result;
                return true;
            }

            state = default;
            return false;
        }

        public override void OnStop(AbilityKit.BehaviorTree.Execution.ExecutionContext context)
        {
            // 门控弹栈（冷却期内直接完成）不代表子树执行过，不重置冷却计时
            if (_gateFired)
            {
                _gateFired = false;
                return;
            }

            _readyAt = context.Time + _cooldown;
        }

        public string CaptureState() => _readyAt.RawValue.ToString();

        public void RestoreState(string payload)
        {
            _readyAt = Fixed64.FromRaw(long.Parse(payload));
        }
    }
}

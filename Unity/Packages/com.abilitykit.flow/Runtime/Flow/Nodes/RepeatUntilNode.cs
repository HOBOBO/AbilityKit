using System;

namespace AbilityKit.Ability.Flow.Nodes
{
    public sealed class RepeatUntilNode : IFlowNode
    {
        private readonly Action<FlowContext, float> _onTick;
        private readonly Func<FlowContext, bool> _until;

        public RepeatUntilNode(Action<FlowContext, float> onTick, Func<FlowContext, bool> until)
        {
            _onTick = onTick;
            _until = until ?? throw new ArgumentNullException(nameof(until));
        }

        public void Enter(FlowContext ctx)
        {
        }

        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            _onTick?.Invoke(ctx, deltaTime);
            return _until(ctx) ? FlowStatus.Succeeded : FlowStatus.Running;
        }

        public void Exit(FlowContext ctx)
        {
        }

        public void Interrupt(FlowContext ctx)
        {
        }
    }
}

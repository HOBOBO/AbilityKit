using System;

namespace AbilityKit.Ability.Flow.Blocks
{
    public sealed class DoNode : IFlowNode
    {
        private readonly Action<FlowContext> _onEnter;
        private readonly Func<FlowContext, float, FlowStatus> _onTick;
        private readonly Action<FlowContext> _onExit;
        private readonly Action<FlowContext> _onInterrupt;

        public DoNode(Action<FlowContext> onEnter = null, Func<FlowContext, float, FlowStatus> onTick = null, Action<FlowContext> onExit = null, Action<FlowContext> onInterrupt = null)
        {
            _onEnter = onEnter;
            _onTick = onTick;
            _onExit = onExit;
            _onInterrupt = onInterrupt;
        }

        public void Enter(FlowContext ctx)
        {
            _onEnter?.Invoke(ctx);
        }

        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            if (_onTick != null) return _onTick(ctx, deltaTime);
            return FlowStatus.Succeeded;
        }

        public void Exit(FlowContext ctx)
        {
            _onExit?.Invoke(ctx);
        }

        public void Interrupt(FlowContext ctx)
        {
            _onInterrupt?.Invoke(ctx);
        }
    }
}

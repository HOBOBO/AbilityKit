using System;

namespace AbilityKit.Ability.Flow.Blocks
{
    public sealed class TickWhileNode : IFlowNode
    {
        private readonly Action<FlowContext> _onEnter;
        private readonly Action<FlowContext, float> _onTick;
        private readonly Func<FlowContext, bool> _while;
        private readonly Action<FlowContext> _onExit;
        private readonly Action<FlowContext> _onInterrupt;

        public TickWhileNode(
            Func<FlowContext, bool> @while,
            Action<FlowContext, float> onTick,
            Action<FlowContext> onEnter = null,
            Action<FlowContext> onExit = null,
            Action<FlowContext> onInterrupt = null)
        {
            _while = @while ?? throw new ArgumentNullException(nameof(@while));
            _onTick = onTick;
            _onEnter = onEnter;
            _onExit = onExit;
            _onInterrupt = onInterrupt;
        }

        public void Enter(FlowContext ctx)
        {
            _onEnter?.Invoke(ctx);
        }

        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            if (!_while(ctx)) return FlowStatus.Succeeded;
            _onTick?.Invoke(ctx, deltaTime);
            return FlowStatus.Running;
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

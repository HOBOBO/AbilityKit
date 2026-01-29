using System;

namespace AbilityKit.Ability.Flow.Blocks
{
    public sealed class RunUntilCompletionNode : IFlowNode
    {
        private readonly Func<FlowContext, FlowCompletion> _getCompletion;
        private readonly Action<FlowContext, float> _tick;

        public RunUntilCompletionNode(Func<FlowContext, FlowCompletion> getCompletion, Action<FlowContext, float> tick)
        {
            _getCompletion = getCompletion ?? throw new ArgumentNullException(nameof(getCompletion));
            _tick = tick;
        }

        public void Enter(FlowContext ctx)
        {
            _getCompletion(ctx).AttachWakeUp(ctx.Get<FlowWakeUp>());
        }

        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            _tick?.Invoke(ctx, deltaTime);

            var c = _getCompletion(ctx);
            if (!c.IsDone) return FlowStatus.Running;
            return c.Succeeded ? FlowStatus.Succeeded : FlowStatus.Failed;
        }

        public void Exit(FlowContext ctx)
        {
            _getCompletion(ctx).DetachWakeUp();
        }

        public void Interrupt(FlowContext ctx)
        {
            _getCompletion(ctx).DetachWakeUp();
        }
    }
}

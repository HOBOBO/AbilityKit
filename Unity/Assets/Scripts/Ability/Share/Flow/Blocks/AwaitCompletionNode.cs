using System;

namespace AbilityKit.Ability.Flow.Blocks
{
    public sealed class AwaitCompletionNode : IFlowNode
    {
        private readonly Func<FlowContext, FlowCompletion> _get;

        public AwaitCompletionNode(Func<FlowContext, FlowCompletion> get)
        {
            _get = get ?? throw new ArgumentNullException(nameof(get));
        }

        public void Enter(FlowContext ctx)
        {
            var completion = _get(ctx);
            completion.AttachWakeUp(ctx.Get<FlowWakeUp>());
        }

        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            var c = _get(ctx);
            if (!c.IsDone) return FlowStatus.Running;
            return c.Succeeded ? FlowStatus.Succeeded : FlowStatus.Failed;
        }

        public void Exit(FlowContext ctx)
        {
            var c = _get(ctx);
            c.DetachWakeUp();
        }

        public void Interrupt(FlowContext ctx)
        {
            var c = _get(ctx);
            c.DetachWakeUp();
        }
    }
}

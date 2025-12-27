using System;

namespace AbilityKit.Ability.Flow.Nodes
{
    public sealed class WaitUntilNode : IFlowNode
    {
        private readonly Func<FlowContext, bool> _predicate;

        public WaitUntilNode(Func<FlowContext, bool> predicate)
        {
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        }

        public void Enter(FlowContext ctx)
        {
        }

        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            return _predicate(ctx) ? FlowStatus.Succeeded : FlowStatus.Running;
        }

        public void Exit(FlowContext ctx)
        {
        }

        public void Interrupt(FlowContext ctx)
        {
        }
    }
}

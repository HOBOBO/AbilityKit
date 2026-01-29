using System;

namespace AbilityKit.Ability.Flow.Blocks
{
    public sealed class DisposeResourceNode<T> : IFlowNode
    {
        private readonly Action<T> _dispose;

        public DisposeResourceNode(Action<T> dispose)
        {
            _dispose = dispose;
        }

        public void Enter(FlowContext ctx)
        {
        }

        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            return FlowStatus.Succeeded;
        }

        public void Exit(FlowContext ctx)
        {
            if (!ctx.TryGet(out T value)) return;
            ctx.Remove<T>();
            _dispose?.Invoke(value);
        }

        public void Interrupt(FlowContext ctx)
        {
            Exit(ctx);
        }
    }
}

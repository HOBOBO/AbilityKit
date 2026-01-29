using System;

namespace AbilityKit.Ability.Flow.Blocks
{
    public sealed class UseResourceNode<T> : IFlowNode
    {
        private readonly Func<FlowContext, T> _create;
        private readonly Action<T> _dispose;
        private T _value;
        private bool _created;

        public UseResourceNode(Func<FlowContext, T> create, Action<T> dispose)
        {
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _dispose = dispose;
        }

        public void Enter(FlowContext ctx)
        {
            _value = _create(ctx);
            _created = true;
            ctx.Set(_value);
        }

        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            return FlowStatus.Succeeded;
        }

        public void Exit(FlowContext ctx)
        {
            if (_created)
            {
                ctx.Remove<T>();
                _dispose?.Invoke(_value);
            }

            _value = default;
            _created = false;
        }

        public void Interrupt(FlowContext ctx)
        {
            Exit(ctx);
        }
    }
}

using System;

namespace AbilityKit.Ability.Flow.Blocks
{
    public sealed class UsingResourceNode<T> : IFlowNode
    {
        private readonly Func<FlowContext, T> _create;
        private readonly Action<T> _dispose;
        private readonly IFlowNode _body;

        private T _value;
        private bool _created;
        private bool _bodyEntered;

        public UsingResourceNode(Func<FlowContext, T> create, Action<T> dispose, IFlowNode body)
        {
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _body = body ?? throw new ArgumentNullException(nameof(body));
            _dispose = dispose;
        }

        public void Enter(FlowContext ctx)
        {
            _value = _create(ctx);
            _created = true;
            ctx.Set(_value);

            _bodyEntered = false;
        }

        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            if (!_bodyEntered)
            {
                _body.Enter(ctx);
                _bodyEntered = true;
            }

            var s = _body.Tick(ctx, deltaTime);
            if (s == FlowStatus.Running) return FlowStatus.Running;

            _body.Exit(ctx);
            _bodyEntered = false;
            return s;
        }

        public void Exit(FlowContext ctx)
        {
            if (_bodyEntered)
            {
                _body.Exit(ctx);
                _bodyEntered = false;
            }

            Dispose(ctx);
        }

        public void Interrupt(FlowContext ctx)
        {
            if (_bodyEntered)
            {
                _body.Interrupt(ctx);
                _bodyEntered = false;
            }

            Dispose(ctx);
        }

        private void Dispose(FlowContext ctx)
        {
            if (_created)
            {
                ctx.Remove<T>();
                _dispose?.Invoke(_value);
            }

            _value = default;
            _created = false;
        }
    }
}

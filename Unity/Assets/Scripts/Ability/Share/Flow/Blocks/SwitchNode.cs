using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Flow.Blocks
{
    public sealed class SwitchNode<TKey> : IFlowNode
    {
        private readonly Func<FlowContext, TKey> _select;
        private readonly IReadOnlyDictionary<TKey, IFlowNode> _cases;
        private readonly IFlowNode _default;

        private IFlowNode _active;
        private bool _entered;

        public SwitchNode(Func<FlowContext, TKey> select, IReadOnlyDictionary<TKey, IFlowNode> cases, IFlowNode defaultNode = null)
        {
            _select = select ?? throw new ArgumentNullException(nameof(select));
            _cases = cases ?? throw new ArgumentNullException(nameof(cases));
            _default = defaultNode;
        }

        public void Enter(FlowContext ctx)
        {
            var key = _select(ctx);
            if (!_cases.TryGetValue(key, out _active))
            {
                _active = _default;
            }
            _entered = false;
        }

        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            if (_active == null) return FlowStatus.Succeeded;

            if (!_entered)
            {
                _active.Enter(ctx);
                _entered = true;
            }

            var s = _active.Tick(ctx, deltaTime);
            if (s == FlowStatus.Running) return FlowStatus.Running;

            _active.Exit(ctx);
            _active = null;
            _entered = false;
            return s;
        }

        public void Exit(FlowContext ctx)
        {
            if (_active != null && _entered)
            {
                _active.Exit(ctx);
            }

            _active = null;
            _entered = false;
        }

        public void Interrupt(FlowContext ctx)
        {
            if (_active != null && _entered)
            {
                _active.Interrupt(ctx);
            }

            _active = null;
            _entered = false;
        }
    }
}

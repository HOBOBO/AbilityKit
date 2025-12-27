using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Flow.Nodes
{
    public sealed class SequenceNode : IFlowNode
    {
        private readonly IFlowNode[] _nodes;
        private int _index;
        private bool _childEntered;

        public SequenceNode(params IFlowNode[] nodes)
        {
            _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        }

        public SequenceNode(IReadOnlyList<IFlowNode> nodes)
        {
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            _nodes = new IFlowNode[nodes.Count];
            for (int i = 0; i < nodes.Count; i++) _nodes[i] = nodes[i];
        }

        public void Enter(FlowContext ctx)
        {
            _index = 0;
            _childEntered = false;
        }

        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            while (_index < _nodes.Length)
            {
                var n = _nodes[_index];
                if (n == null) throw new InvalidOperationException("SequenceNode contains null node");

                if (!_childEntered)
                {
                    n.Enter(ctx);
                    _childEntered = true;
                }

                var s = n.Tick(ctx, deltaTime);
                if (s == FlowStatus.Running) return FlowStatus.Running;

                n.Exit(ctx);
                _childEntered = false;

                if (s != FlowStatus.Succeeded) return s;

                _index++;
            }

            return FlowStatus.Succeeded;
        }

        public void Exit(FlowContext ctx)
        {
            if (_childEntered && _index < _nodes.Length)
            {
                _nodes[_index].Exit(ctx);
            }

            _childEntered = false;
        }

        public void Interrupt(FlowContext ctx)
        {
            if (_childEntered && _index < _nodes.Length)
            {
                _nodes[_index].Interrupt(ctx);
            }

            _childEntered = false;
        }
    }
}

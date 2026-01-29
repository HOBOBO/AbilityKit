using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Flow.Blocks
{
    public sealed class RaceNode : IFlowNode
    {
        private readonly IFlowNode[] _nodes;
        private readonly FlowStatus[] _status;
        private bool _entered;

        public RaceNode(params IFlowNode[] nodes)
        {
            _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            _status = new FlowStatus[_nodes.Length];
        }

        public RaceNode(IReadOnlyList<IFlowNode> nodes)
        {
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            _nodes = new IFlowNode[nodes.Count];
            for (int i = 0; i < nodes.Count; i++) _nodes[i] = nodes[i];
            _status = new FlowStatus[_nodes.Length];
        }

        public void Enter(FlowContext ctx)
        {
            for (int i = 0; i < _nodes.Length; i++)
            {
                if (_nodes[i] == null) throw new InvalidOperationException("RaceNode contains null node");
                _status[i] = FlowStatus.Running;
                _nodes[i].Enter(ctx);
            }
            _entered = true;
        }

        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            if (!_entered) return FlowStatus.Succeeded;

            for (int i = 0; i < _nodes.Length; i++)
            {
                if (_status[i] != FlowStatus.Running) continue;

                var s = _nodes[i].Tick(ctx, deltaTime);
                if (s == FlowStatus.Running) continue;

                _status[i] = s;
                _nodes[i].Exit(ctx);

                for (int k = 0; k < _nodes.Length; k++)
                {
                    if (k == i) continue;
                    if (_status[k] != FlowStatus.Running) continue;
                    _nodes[k].Interrupt(ctx);
                    _status[k] = FlowStatus.Canceled;
                }

                _entered = false;
                return s;
            }

            return FlowStatus.Running;
        }

        public void Exit(FlowContext ctx)
        {
            if (!_entered) return;

            for (int i = 0; i < _nodes.Length; i++)
            {
                if (_status[i] == FlowStatus.Running)
                {
                    _nodes[i].Exit(ctx);
                    _status[i] = FlowStatus.Succeeded;
                }
            }

            _entered = false;
        }

        public void Interrupt(FlowContext ctx)
        {
            if (!_entered) return;

            for (int i = 0; i < _nodes.Length; i++)
            {
                if (_status[i] == FlowStatus.Running)
                {
                    _nodes[i].Interrupt(ctx);
                    _status[i] = FlowStatus.Canceled;
                }
            }

            _entered = false;
        }
    }
}

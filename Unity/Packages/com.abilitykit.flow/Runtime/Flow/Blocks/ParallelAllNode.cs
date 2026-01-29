using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Flow.Blocks
{
    public sealed class ParallelAllNode : IFlowNode
    {
        private readonly IFlowNode[] _nodes;
        private readonly FlowStatus[] _status;
        private bool _entered;

        public ParallelAllNode(params IFlowNode[] nodes)
        {
            _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            _status = new FlowStatus[_nodes.Length];
        }

        public ParallelAllNode(IReadOnlyList<IFlowNode> nodes)
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
                if (_nodes[i] == null) throw new InvalidOperationException("ParallelAllNode contains null node");
                _status[i] = FlowStatus.Running;
                _nodes[i].Enter(ctx);
            }
            _entered = true;
        }

        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            if (!_entered) return FlowStatus.Succeeded;

            var anyFailed = false;
            var allDone = true;

            for (int i = 0; i < _nodes.Length; i++)
            {
                if (_status[i] != FlowStatus.Running) continue;

                var s = _nodes[i].Tick(ctx, deltaTime);
                if (s == FlowStatus.Running)
                {
                    allDone = false;
                    continue;
                }

                _status[i] = s;
                _nodes[i].Exit(ctx);

                if (s != FlowStatus.Succeeded) anyFailed = true;
            }

            if (!allDone) return FlowStatus.Running;
            return anyFailed ? FlowStatus.Failed : FlowStatus.Succeeded;
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

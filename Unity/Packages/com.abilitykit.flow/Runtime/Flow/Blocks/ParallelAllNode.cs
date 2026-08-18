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
                FlowDiagnostics.Enter(ctx, _nodes[i]);
            }
            _entered = true;
        }

        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            if (!_entered) return FlowStatus.Succeeded;

            var allDone = true;

            for (int i = 0; i < _nodes.Length; i++)
            {
                if (_status[i] != FlowStatus.Running) continue;

                var s = FlowDiagnostics.Tick(ctx, _nodes[i], deltaTime);
                if (s == FlowStatus.Running)
                {
                    allDone = false;
                    continue;
                }

                _status[i] = s;
                FlowDiagnostics.Exit(ctx, _nodes[i], s);
            }

            if (!allDone) return FlowStatus.Running;

            // 终态判定看全部子节点的最终状态，而不是本次 Tick 的局部标记——
            // 早先轮次失败的子节点必须参与最终判定（任一非 Succeeded 即整体 Failed）。
            for (int i = 0; i < _nodes.Length; i++)
            {
                if (_status[i] != FlowStatus.Succeeded) return FlowStatus.Failed;
            }

            return FlowStatus.Succeeded;
        }

        public void Exit(FlowContext ctx)
        {
            if (!_entered) return;

            for (int i = 0; i < _nodes.Length; i++)
            {
                if (_status[i] == FlowStatus.Running)
                {
                    FlowDiagnostics.Exit(ctx, _nodes[i]);
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
                    FlowDiagnostics.Interrupt(ctx, _nodes[i]);
                    _status[i] = FlowStatus.Canceled;
                }
            }

            _entered = false;
        }
    }
}

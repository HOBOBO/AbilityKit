using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Flow.Nodes
{
    /// <summary>
    /// 按输入顺序推进子节点；只有当前节点成功才推进下一项。
    /// </summary>
    public sealed class SequenceNode : IFlowNode
    {
        private readonly IFlowNode[] _nodes;
        private int _index;
        private bool _childEntered;

        /// <summary>
        /// 使用给定数组作为节点列表。
        /// </summary>
        public SequenceNode(params IFlowNode[] nodes)
        {
            _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        }

        /// <summary>
        /// 按顺序复制列表中的节点引用。
        /// </summary>
        public SequenceNode(IReadOnlyList<IFlowNode> nodes)
        {
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            _nodes = new IFlowNode[nodes.Count];
            for (int i = 0; i < nodes.Count; i++) _nodes[i] = nodes[i];
        }

        /// <summary>
        /// 重置执行位置，子节点在推进时进入。
        /// </summary>
        public void Enter(FlowContext ctx)
        {
            _index = 0;
            _childEntered = false;
        }

        /// <summary>
        /// 推进节点，遇到运行中或非成功终态时暂停推进后续节点。
        /// </summary>
        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            while (_index < _nodes.Length)
            {
                var child = _nodes[_index];
                if (child == null) throw new InvalidOperationException("SequenceNode contains null node");

                if (!_childEntered)
                {
                    FlowDiagnostics.EnterTracked(ctx, child, ref _childEntered);
                }

                var status = FlowDiagnostics.Tick(ctx, child, deltaTime);
                if (status == FlowStatus.Running) return FlowStatus.Running;

                FlowDiagnostics.CloseTracked(ctx, child, ref _childEntered, interrupt: false, status: status);

                if (status != FlowStatus.Succeeded) return status;

                _index++;
            }

            return FlowStatus.Succeeded;
        }

        /// <summary>
        /// 退出当前活动子节点，不推进后续节点。
        /// </summary>
        public void Exit(FlowContext ctx)
        {
            if (_childEntered && _index < _nodes.Length)
            {
                FlowDiagnostics.CloseTracked(ctx, _nodes[_index], ref _childEntered, interrupt: false, status: FlowStatus.Succeeded);
            }
        }

        /// <summary>
        /// 中断当前活动子节点，不进入后续节点。
        /// </summary>
        public void Interrupt(FlowContext ctx)
        {
            if (_childEntered && _index < _nodes.Length)
            {
                FlowDiagnostics.CloseTracked(ctx, _nodes[_index], ref _childEntered, interrupt: true, status: FlowStatus.Canceled);
            }
        }
    }
}

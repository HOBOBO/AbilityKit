using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Flow.Blocks
{
    /// <summary>
    /// 按输入顺序推进分支，第一个返回终态的分支决定结果，其余分支被中断。
    /// </summary>
    public sealed class RaceNode : IFlowNode
    {
        private readonly IFlowNode[] _nodes;
        private readonly FlowStatus[] _status;
        private bool _entered;

        /// <summary>
        /// 使用给定数组作为分支列表。
        /// </summary>
        public RaceNode(params IFlowNode[] nodes)
        {
            _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            _status = new FlowStatus[_nodes.Length];
        }

        /// <summary>
        /// 按顺序复制列表中的分支引用。
        /// </summary>
        public RaceNode(IReadOnlyList<IFlowNode> nodes)
        {
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            _nodes = new IFlowNode[nodes.Count];
            for (int i = 0; i < nodes.Count; i++) _nodes[i] = nodes[i];
            _status = new FlowStatus[_nodes.Length];
        }

        /// <summary>
        /// 按顺序进入分支；部分进入失败后可通过中断清理已尝试进入的分支。
        /// </summary>
        public void Enter(FlowContext ctx)
        {
            Array.Clear(_status, 0, _status.Length);
            _entered = true;
            for (var index = 0; index < _nodes.Length; index++)
            {
                var node = _nodes[index];
                if (node == null) throw new InvalidOperationException("RaceNode contains null node");
                FlowDiagnostics.RecordEnter(ctx, node);
                _status[index] = FlowStatus.Running;
                node.Enter(ctx);
            }
        }

        /// <summary>
        /// 推进到第一个终态；先退出该分支，再中断其余分支，全部尝试后抛出收尾异常。
        /// 空组首次推进即成功。
        /// </summary>
        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            if (!_entered) return FlowStatus.Succeeded;
            if (_nodes.Length == 0) return FlowStatus.Succeeded;

            for (var index = 0; index < _nodes.Length; index++)
            {
                if (_status[index] != FlowStatus.Running) continue;

                var result = FlowDiagnostics.Tick(ctx, _nodes[index], deltaTime);
                if (result == FlowStatus.Running) continue;

                _status[index] = result;
                _entered = false;
                var cleanup = new FlowCleanup();
                cleanup.CloseNode(ctx, _nodes[index], interrupt: false, status: result);
                cleanup.CloseRunningChildren(ctx, _nodes, _status, interrupt: true);
                cleanup.ThrowIfFailed();
                return result;
            }

            return FlowStatus.Running;
        }

        /// <summary>
        /// 退出所有仍在运行的分支，全部尝试后抛出收尾异常。
        /// </summary>
        public void Exit(FlowContext ctx) => Close(ctx, interrupt: false);

        /// <summary>
        /// 中断所有仍在运行的分支，全部尝试后抛出收尾异常。
        /// </summary>
        public void Interrupt(FlowContext ctx) => Close(ctx, interrupt: true);

        private void Close(FlowContext ctx, bool interrupt)
        {
            if (!_entered) return;
            _entered = false;
            var cleanup = new FlowCleanup();
            cleanup.CloseRunningChildren(ctx, _nodes, _status, interrupt);
            cleanup.ThrowIfFailed();
        }
    }
}

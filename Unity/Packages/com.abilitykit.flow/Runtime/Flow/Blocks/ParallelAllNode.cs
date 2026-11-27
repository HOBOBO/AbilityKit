using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Flow.Blocks
{
    /// <summary>
    /// 按输入顺序推进所有分支，等待全部结束；仅当所有分支成功时返回成功。
    /// </summary>
    public sealed class ParallelAllNode : IFlowNode
    {
        private readonly IFlowNode[] _nodes;
        private readonly FlowStatus[] _status;
        private bool _entered;

        /// <summary>
        /// 使用给定数组作为分支列表。
        /// </summary>
        public ParallelAllNode(params IFlowNode[] nodes)
        {
            _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
            _status = new FlowStatus[_nodes.Length];
        }

        /// <summary>
        /// 按顺序复制列表中的分支引用。
        /// </summary>
        public ParallelAllNode(IReadOnlyList<IFlowNode> nodes)
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
                if (node == null) throw new InvalidOperationException("ParallelAllNode contains null node");
                FlowDiagnostics.RecordEnter(ctx, node);
                _status[index] = FlowStatus.Running;
                node.Enter(ctx);
            }
        }

        /// <summary>
        /// 推进仍在运行的分支，终态分支立即退出。
        /// </summary>
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

            /*
             * 早于本轮完成的分支仍参与最终判定。
             */
            for (int i = 0; i < _nodes.Length; i++)
            {
                if (_status[i] != FlowStatus.Succeeded) return FlowStatus.Failed;
            }

            return FlowStatus.Succeeded;
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

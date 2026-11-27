#nullable enable annotations

using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Flow.Blocks
{
    /// <summary>
    /// 在进入时按键选择一个分支；未命中且没有默认分支时返回成功。
    /// </summary>
    public sealed class SwitchNode<TKey> : IFlowNode
    {
        private readonly Func<FlowContext, TKey> _select;
        private readonly IReadOnlyDictionary<TKey, IFlowNode> _cases;
        private readonly IFlowNode? _default;

        private IFlowNode? _active;
        private bool _entered;

        /// <summary>
        /// 指定选键函数、分支映射和可选的默认分支。
        /// </summary>
        public SwitchNode(Func<FlowContext, TKey> select, IReadOnlyDictionary<TKey, IFlowNode> cases, IFlowNode? defaultNode = null)
        {
            _select = select ?? throw new ArgumentNullException(nameof(select));
            _cases = cases ?? throw new ArgumentNullException(nameof(cases));
            _default = defaultNode;
        }

        /// <summary>
        /// 根据当前键选择分支，推迟到首次推进时进入该分支。
        /// </summary>
        public void Enter(FlowContext ctx)
        {
            var key = _select(ctx);
            if (!_cases.TryGetValue(key, out _active))
            {
                _active = _default;
            }
            _entered = false;
        }

        /// <summary>
        /// 推进已选分支并返回其结果，进入终态后退出该分支。
        /// </summary>
        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            var child = _active;
            if (child == null) return FlowStatus.Succeeded;

            if (!_entered)
            {
                FlowDiagnostics.EnterTracked(ctx, child, ref _entered);
            }

            var status = FlowDiagnostics.Tick(ctx, child, deltaTime);
            if (status != FlowStatus.Running) Close(ctx, interrupt: false, status: status);
            return status;
        }

        /// <summary>
        /// 退出仍活动的分支并清除本次选择。
        /// </summary>
        public void Exit(FlowContext ctx) => Close(ctx, interrupt: false, status: FlowStatus.Succeeded);

        /// <summary>
        /// 中断仍活动的分支并清除本次选择。
        /// </summary>
        public void Interrupt(FlowContext ctx) => Close(ctx, interrupt: true, status: FlowStatus.Canceled);

        private void Close(FlowContext ctx, bool interrupt, FlowStatus status)
        {
            var child = _active;
            _active = null;
            if (child != null) FlowDiagnostics.CloseTracked(ctx, child, ref _entered, interrupt, status);
        }
    }
}

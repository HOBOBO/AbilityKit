#nullable enable annotations

using System;

namespace AbilityKit.Ability.Flow.Blocks
{
    /// <summary>
    /// 在进入时按条件选择一个分支；未选择分支时返回成功。
    /// </summary>
    public sealed class IfNode : IFlowNode
    {
        private readonly Func<FlowContext, bool> _predicate;
        private readonly IFlowNode _then;
        private readonly IFlowNode? _else;

        private IFlowNode? _active;
        private bool _entered;

        /// <summary>
        /// 指定判定条件、真分支和可选的假分支。
        /// </summary>
        public IfNode(Func<FlowContext, bool> predicate, IFlowNode thenNode, IFlowNode? elseNode = null)
        {
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            _then = thenNode ?? throw new ArgumentNullException(nameof(thenNode));
            _else = elseNode;
        }

        /// <summary>
        /// 选择本次运行的分支，推迟到首次推进时进入该分支。
        /// </summary>
        public void Enter(FlowContext ctx)
        {
            _active = _predicate(ctx) ? _then : _else;
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

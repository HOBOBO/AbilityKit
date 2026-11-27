using System;

namespace AbilityKit.Ability.Flow.Blocks
{
    /// <summary>
    /// 主阶段返回终态后推进后续阶段，整体结果取自主阶段；中断不会启动后续阶段。
    /// </summary>
    public sealed class FinallyNode : IFlowNode
    {
        private readonly IFlowNode _try;
        private readonly IFlowNode _finally;

        private bool _tryEntered;
        private bool _finallyEntered;
        private FlowStatus _tryStatus;

        /// <summary>
        /// 指定主阶段和主阶段正常返回终态后执行的后续阶段。
        /// </summary>
        public FinallyNode(IFlowNode tryNode, IFlowNode finallyNode)
        {
            _try = tryNode ?? throw new ArgumentNullException(nameof(tryNode));
            _finally = finallyNode ?? throw new ArgumentNullException(nameof(finallyNode));
        }

        /// <summary>
        /// 重置阶段状态，在首次推进时进入主阶段。
        /// </summary>
        public void Enter(FlowContext ctx)
        {
            _tryEntered = false;
            _finallyEntered = false;
            _tryStatus = FlowStatus.Running;
        }

        /// <summary>
        /// 主阶段退出后进入后续阶段；后续阶段结束时返回主阶段结果。
        /// </summary>
        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            if (!_finallyEntered)
            {
                if (!_tryEntered)
                {
                    FlowDiagnostics.EnterTracked(ctx, _try, ref _tryEntered);
                }

                var status = FlowDiagnostics.Tick(ctx, _try, deltaTime);
                if (status == FlowStatus.Running) return FlowStatus.Running;

                _tryStatus = status;
                FlowDiagnostics.CloseTracked(ctx, _try, ref _tryEntered, interrupt: false, status: status);

                FlowDiagnostics.EnterTracked(ctx, _finally, ref _finallyEntered);
            }

            var finallyStatus = FlowDiagnostics.Tick(ctx, _finally, deltaTime);
            if (finallyStatus == FlowStatus.Running) return FlowStatus.Running;

            FlowDiagnostics.CloseTracked(ctx, _finally, ref _finallyEntered, interrupt: false, status: finallyStatus);
            return _tryStatus;
        }

        /// <summary>
        /// 退出当前活动阶段，不启动其他阶段。
        /// </summary>
        public void Exit(FlowContext ctx) => Close(ctx, interrupt: false, status: FlowStatus.Succeeded);

        /// <summary>
        /// 中断当前活动阶段，不启动其他阶段。
        /// </summary>
        public void Interrupt(FlowContext ctx) => Close(ctx, interrupt: true, status: FlowStatus.Canceled);

        private void Close(FlowContext ctx, bool interrupt, FlowStatus status)
        {
            FlowDiagnostics.CloseTracked(ctx, _try, ref _tryEntered, interrupt, status);
            FlowDiagnostics.CloseTracked(ctx, _finally, ref _finallyEntered, interrupt, status);
        }
    }
}

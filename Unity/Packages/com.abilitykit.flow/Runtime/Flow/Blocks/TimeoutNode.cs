using System;

namespace AbilityKit.Ability.Flow.Blocks
{
    /// <summary>
    /// 累计推进时间；超过时限时中断子节点并返回失败。
    /// </summary>
    public sealed class TimeoutNode : IFlowNode
    {
        private readonly float _seconds;
        private readonly IFlowNode _child;

        private float _elapsed;
        private bool _entered;

        /// <summary>
        /// 指定非负时限（秒）和受限子节点。
        /// </summary>
        public TimeoutNode(float seconds, IFlowNode child)
        {
            if (seconds < 0f) throw new ArgumentOutOfRangeException(nameof(seconds));
            _seconds = seconds;
            _child = child ?? throw new ArgumentNullException(nameof(child));
        }

        /// <summary>
        /// 重置累计时间，子节点在首次推进时进入。
        /// </summary>
        public void Enter(FlowContext ctx)
        {
            _elapsed = 0f;
            _entered = false;
        }

        /// <summary>
        /// 先检查累积时间是否严格大于时限，未超时时推进子节点。
        /// </summary>
        public FlowStatus Tick(FlowContext ctx, float deltaTime)
        {
            if (!_entered)
            {
                FlowDiagnostics.EnterTracked(ctx, _child, ref _entered);
            }

            _elapsed += deltaTime;
            if (_elapsed > _seconds)
            {
                FlowDiagnostics.CloseTracked(ctx, _child, ref _entered, interrupt: true, status: FlowStatus.Failed);
                return FlowStatus.Failed;
            }

            var status = FlowDiagnostics.Tick(ctx, _child, deltaTime);
            if (status == FlowStatus.Running) return FlowStatus.Running;

            FlowDiagnostics.CloseTracked(ctx, _child, ref _entered, interrupt: false, status: status);
            return status;
        }

        /// <summary>
        /// 退出仍活动的子节点。
        /// </summary>
        public void Exit(FlowContext ctx) =>
            FlowDiagnostics.CloseTracked(ctx, _child, ref _entered, interrupt: false, status: FlowStatus.Succeeded);

        /// <summary>
        /// 中断仍活动的子节点。
        /// </summary>
        public void Interrupt(FlowContext ctx) =>
            FlowDiagnostics.CloseTracked(ctx, _child, ref _entered, interrupt: true, status: FlowStatus.Canceled);
    }
}

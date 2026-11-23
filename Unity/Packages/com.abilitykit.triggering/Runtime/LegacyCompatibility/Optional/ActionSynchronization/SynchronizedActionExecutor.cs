using System;
using AbilityKit.Triggering.Runtime.Executable;

namespace AbilityKit.Triggering.Runtime.ActionScheduler
{
    /// <summary>
    /// 旧版信号门控执行器，仅作兼容保留；只在尝试执行时检查信号，不登记等待或恢复回调。
    /// </summary>
    public sealed class SynchronizedActionExecutor : ActionExecutorBase
    {
        private readonly ActionExecutorBase _inner;
        private readonly TriggerWaitHandle _waitHandle;

        /// <summary>
        /// 使用给定信号句柄控制内部执行器的进入条件。
        /// </summary>
        public SynchronizedActionExecutor(ActionExecutorBase inner, TriggerWaitHandle waitHandle)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _waitHandle = waitHandle ?? throw new ArgumentNullException(nameof(waitHandle));
        }

        /// <summary>
        /// 未置位时跳过；已置位时尝试内部执行器，不消耗信号。
        /// </summary>
        protected override ExecutionResult ExecuteCore(ActionExecutionContext ctx)
        {
            if (!_waitHandle.IsSignaled)
            {
                return ExecutionResult.Skipped("Wait handle not signaled");
            }

            return _inner.TryExecute(ctx, out var result) ? result : ExecutionResult.Failed("Inner executor failed");
        }

        /// <summary>
        /// 将取消原因转发给内部执行器。
        /// </summary>
        public override void Cancel(string reason)
        {
            _inner.Cancel(reason);
        }

        /// <summary>
        /// 设置信号句柄，不主动推进或再次执行动作。
        /// </summary>
        public void Signal()
        {
            _waitHandle.Set();
        }
    }
}

using System;

namespace AbilityKit.Ability.Flow
{
    /// <summary>
    /// Flow 节点执行结果，统一表达最终状态、执行步数与捕获异常。
    /// </summary>
    public readonly struct FlowExecutionResult
    {
        public FlowExecutionResult(FlowStatus status, int steps, Exception? exception)
        {
            Status = status;
            Steps = steps;
            Exception = exception;
        }

        /// <summary>
        /// 执行结束时的状态。
        /// </summary>
        public FlowStatus Status { get; }

        /// <summary>
        /// 已执行的 Step 次数。
        /// </summary>
        public int Steps { get; }

        /// <summary>
        /// 执行过程中捕获到的首个异常。无异常时为 null。
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// 是否成功完成。
        /// </summary>
        public bool Succeeded => Status == FlowStatus.Succeeded;

        /// <summary>
        /// 是否失败或被取消。
        /// </summary>
        public bool IsTerminalFailure => Status == FlowStatus.Failed || Status == FlowStatus.Canceled;
    }
}

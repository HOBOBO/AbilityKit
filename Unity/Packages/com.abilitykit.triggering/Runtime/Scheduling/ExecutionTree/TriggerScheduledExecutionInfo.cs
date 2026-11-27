namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 单条任务的进度值，不包含执行定义或宿主引用。
    /// </summary>
    public readonly struct TriggerScheduledExecutionInfo
    {
        /// <summary>
        /// 构造待查询或恢复的进度值；不在此校验 ID、时间或状态。
        /// </summary>
        public TriggerScheduledExecutionInfo(
            long id,
            TriggerScheduledExecutionState state,
            double elapsedMs,
            double lastExecutionMs,
            int executionCount,
            string failureReason)
        {
            Id = id;
            State = state;
            ElapsedMs = elapsedMs;
            LastExecutionMs = lastExecutionMs;
            ExecutionCount = executionCount;
            FailureReason = failureReason ?? string.Empty;
        }

        /// <summary>
        /// 进度所属的任务 ID。
        /// </summary>
        public long Id { get; }

        /// <summary>
        /// 当前等待状态或终态。
        /// </summary>
        public TriggerScheduledExecutionState State { get; }

        /// <summary>
        /// 等待期间累积的已推进毫秒数。
        /// </summary>
        public double ElapsedMs { get; }

        /// <summary>
        /// 最近一次计入执行次数时的累计毫秒数。
        /// </summary>
        public double LastExecutionMs { get; }

        /// <summary>
        /// 已计入的非失败执行次数。
        /// </summary>
        public int ExecutionCount { get; }

        /// <summary>
        /// 记录的失败或中断原因；未提供原因时构造器写为空字符串。
        /// </summary>
        public string FailureReason { get; }
    }
}

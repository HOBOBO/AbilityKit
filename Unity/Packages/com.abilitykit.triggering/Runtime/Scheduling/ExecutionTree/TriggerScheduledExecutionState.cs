namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 执行树调度的等待状态和终态。
    /// </summary>
    public enum TriggerScheduledExecutionState : byte
    {
        /// <summary>
        /// 任务可继续推进或等待外部触发。
        /// </summary>
        Waiting = 0,

        /// <summary>
        /// 任务已按调度方式或次数上限完成。
        /// </summary>
        Completed = 1,

        /// <summary>
        /// 等待中的任务已被中断。
        /// </summary>
        Interrupted = 2,

        /// <summary>
        /// 执行返回失败或抛出异常，任务终止。
        /// </summary>
        Failed = 3,
    }
}

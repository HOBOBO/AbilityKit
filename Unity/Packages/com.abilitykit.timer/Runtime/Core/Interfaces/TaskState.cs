namespace AbilityKit.Timer
{
    /// <summary>
    /// 调度任务状态
    /// </summary>
    public enum TaskState : byte
    {
        /// <summary>等待执行</summary>
        Pending = 0,

        /// <summary>正在执行</summary>
        Running = 1,

        /// <summary>已完成</summary>
        Completed = 2,

        /// <summary>已取消</summary>
        Canceled = 3,

        /// <summary>已超时</summary>
        TimedOut = 4,
    }
}
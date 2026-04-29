namespace AbilityKit.Triggering.Runtime.Schedule.Data
{
    /// <summary>
    /// 调度器全局数据（纯数据结构）
    /// 框架层定义，不依赖任何运行时实现
    /// </summary>
    public struct ScheduleData
    {
        /// <summary>下一个可用实例ID</summary>
        public int NextInstanceId;

        /// <summary>活跃的调度项数量</summary>
        public int ActiveItemCount;

        /// <summary>总注册数量</summary>
        public int TotalRegisteredCount;

        /// <summary>全局暂停标志</summary>
        public bool IsPaused;

        /// <summary>是否已销毁</summary>
        public bool IsDisposed;
    }
}

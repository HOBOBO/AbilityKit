using AbilityKit.Triggering.Runtime.Schedule.Data;

namespace AbilityKit.Triggering.Runtime.Schedule.Behavior
{
    /// <summary>
    /// 调度上下文
    /// 传递给 IScheduleEffect.Execute 的信息载体
    /// </summary>
    public readonly struct ScheduleContext
    {
        /// <summary>实例ID</summary>
        public readonly int InstanceId;

        /// <summary>业务对象ID（关联具体业务对象，如 BuffId、子弹Id）</summary>
        public readonly int BusinessId;

        /// <summary>帧间隔（毫秒）</summary>
        public readonly float DeltaTimeMs;

        /// <summary>从启动至今的总消耗时间（毫秒）</summary>
        public readonly float ElapsedMs;

        /// <summary>本次帧的增量时间（考虑速度倍率）</summary>
        public readonly float ScaledDeltaMs;

        /// <summary>间隔时间（毫秒）</summary>
        public readonly float IntervalMs;

        /// <summary>已执行次数</summary>
        public readonly int ExecutionCount;

        /// <summary>最大执行次数，-1表示无限</summary>
        public readonly int MaxExecutions;

        /// <summary>速度倍率</summary>
        public readonly float Speed;

        /// <summary>中断原因（如果被中断）</summary>
        public readonly string InterruptReason;

        /// <summary>
        /// 是否是周期性执行
        /// </summary>
        public bool IsPeriodic => MaxExecutions < 0 || MaxExecutions > 1;

        /// <summary>
        /// 是否达到最大执行次数
        /// </summary>
        public bool IsMaxExecutionsReached => MaxExecutions > 0 && ExecutionCount >= MaxExecutions;

        /// <summary>
        /// 距离下次执行的时间（毫秒）
        /// </summary>
        public float TimeToNextExecute => IntervalMs > 0 ? IntervalMs - (ElapsedMs % IntervalMs) : 0;

        /// <summary>
        /// 总进度（0-1），仅对有限次数的周期性有效
        /// </summary>
        public float Progress => MaxExecutions > 0 ? (float)ExecutionCount / MaxExecutions : 0;

        internal ScheduleContext(
            int instanceId,
            int businessId,
            float deltaTimeMs,
            float elapsedMs,
            float scaledDeltaMs,
            float intervalMs,
            int executionCount,
            int maxExecutions,
            float speed,
            string interruptReason)
        {
            InstanceId = instanceId;
            BusinessId = businessId;
            DeltaTimeMs = deltaTimeMs;
            ElapsedMs = elapsedMs;
            ScaledDeltaMs = scaledDeltaMs;
            IntervalMs = intervalMs;
            ExecutionCount = executionCount;
            MaxExecutions = maxExecutions;
            Speed = speed;
            InterruptReason = interruptReason;
        }

        /// <summary>
        /// 从 ScheduleItemData 创建上下文
        /// </summary>
        internal static ScheduleContext Create(ScheduleItemData item, float deltaTimeMs, float scaledDeltaMs)
        {
            return new ScheduleContext(
                instanceId: item.Handle.Index,
                businessId: item.BusinessId,
                deltaTimeMs: deltaTimeMs,
                elapsedMs: item.ElapsedMs,
                scaledDeltaMs: scaledDeltaMs,
                intervalMs: item.IntervalMs,
                executionCount: item.ExecutionCount,
                maxExecutions: item.MaxExecutions,
                speed: item.Speed,
                interruptReason: item.State == EScheduleItemState.Interrupted ? item.InterruptReason : null
            );
        }
    }
}

namespace AbilityKit.Triggering.Runtime.Schedule.Data
{
    /// <summary>
    /// 调度注册请求
    /// 业务方通过此结构向注册中心注册调度需求
    /// </summary>
    public struct ScheduleRegisterRequest
    {
        /// <summary>调度模式</summary>
        public EScheduleMode Mode;

        /// <summary>关联的 Trigger ID（用于分组管理）</summary>
        public int TriggerId;

        /// <summary>延迟时间（毫秒），首次执行前等待</summary>
        public float DelayMs;

        /// <summary>间隔时间（毫秒），周期性执行间隔</summary>
        public float IntervalMs;

        /// <summary>最大执行次数，-1表示无限</summary>
        public int MaxExecutions;

        /// <summary>速度倍率（1.0 = 正常速度）</summary>
        public float Speed;

        /// <summary>业务对象ID（由业务方指定，用于关联具体业务对象，如 BuffId、子弹Id）</summary>
        public int BusinessId;

        /// <summary>是否可被中断</summary>
        public bool CanBeInterrupted;

        /// <summary>
        /// 创建周期性调度请求
        /// </summary>
        public static ScheduleRegisterRequest Periodic(
            float intervalMs,
            int maxExecutions = -1,
            float delayMs = 0,
            float speed = 1.0f,
            int businessId = 0,
            int triggerId = 0,
            bool canBeInterrupted = true)
        {
            return new ScheduleRegisterRequest
            {
                Mode = EScheduleMode.Periodic,
                TriggerId = triggerId,
                DelayMs = delayMs,
                IntervalMs = intervalMs,
                MaxExecutions = maxExecutions,
                Speed = speed,
                BusinessId = businessId,
                CanBeInterrupted = canBeInterrupted
            };
        }

        /// <summary>
        /// 创建延迟一次性调度请求
        /// </summary>
        public static ScheduleRegisterRequest Delayed(
            float delayMs,
            int businessId = 0,
            int triggerId = 0,
            bool canBeInterrupted = true)
        {
            return new ScheduleRegisterRequest
            {
                Mode = EScheduleMode.Delayed,
                TriggerId = triggerId,
                DelayMs = delayMs,
                IntervalMs = 0,
                MaxExecutions = 1,
                Speed = 1.0f,
                BusinessId = businessId,
                CanBeInterrupted = canBeInterrupted
            };
        }

        /// <summary>
        /// 创建持续调度请求（需要手动终止）
        /// </summary>
        public static ScheduleRegisterRequest Continuous(
            float intervalMs,
            bool canBeInterrupted = true,
            int maxExecutions = -1,
            int businessId = 0,
            int triggerId = 0)
        {
            return new ScheduleRegisterRequest
            {
                Mode = EScheduleMode.Continuous,
                TriggerId = triggerId,
                DelayMs = 0,
                IntervalMs = intervalMs,
                MaxExecutions = maxExecutions,
                Speed = 1.0f,
                BusinessId = businessId,
                CanBeInterrupted = canBeInterrupted
            };
        }
    }

    /// <summary>
    /// 调度修改请求
    /// 用于运行时修改已注册的调度项参数
    /// </summary>
    public struct ScheduleModifyRequest
    {
        /// <summary>是否设置间隔</summary>
        public bool HasIntervalMs;
        
        /// <summary>新的间隔时间（毫秒）</summary>
        public float IntervalMs;

        /// <summary>是否设置速度</summary>
        public bool HasSpeed;
        
        /// <summary>新的速度倍率</summary>
        public float Speed;

        /// <summary>是否设置最大执行次数</summary>
        public bool HasMaxExecutions;
        
        /// <summary>新的最大执行次数</summary>
        public int MaxExecutions;

        /// <summary>是否设置延迟</summary>
        public bool HasDelayMs;
        
        /// <summary>新的延迟时间（毫秒）</summary>
        public float DelayMs;

        /// <summary>
        /// 创建修改请求：设置间隔
        /// </summary>
        public static ScheduleModifyRequest SetInterval(float intervalMs)
        {
            return new ScheduleModifyRequest { HasIntervalMs = true, IntervalMs = intervalMs };
        }

        /// <summary>
        /// 创建修改请求：设置速度
        /// </summary>
        public static ScheduleModifyRequest SetSpeed(float speed)
        {
            return new ScheduleModifyRequest { HasSpeed = true, Speed = speed };
        }

        /// <summary>
        /// 创建修改请求：设置最大执行次数
        /// </summary>
        public static ScheduleModifyRequest SetMaxExecutions(int maxExecutions)
        {
            return new ScheduleModifyRequest { HasMaxExecutions = true, MaxExecutions = maxExecutions };
        }

        /// <summary>
        /// 创建修改请求：设置延迟
        /// </summary>
        public static ScheduleModifyRequest SetDelay(float delayMs)
        {
            return new ScheduleModifyRequest { HasDelayMs = true, DelayMs = delayMs };
        }

        /// <summary>
        /// 创建修改请求：增加执行次数
        /// </summary>
        public static ScheduleModifyRequest AddExecutions(int count)
        {
            return new ScheduleModifyRequest { HasMaxExecutions = true, MaxExecutions = count };
        }
    }
}

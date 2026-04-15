using System;

namespace AbilityKit.Triggering.Runtime.Config.Schedule
{
    /// <summary>
    /// 调度配置实现（静态配置数据）
    /// </summary>
    [Serializable]
    public struct ScheduleConfig : IScheduleConfig
    {
        public EScheduleMode Mode { get; set; }
        public float DurationMs { get; set; }
        public float PeriodMs { get; set; }
        public int MaxExecutions { get; set; }
        public bool CanBeInterrupted { get; set; }

        public static ScheduleConfig Transient => new ScheduleConfig { Mode = EScheduleMode.Transient };

        public static ScheduleConfig Timed(float delayMs) => new ScheduleConfig
        {
            Mode = EScheduleMode.Timed,
            DurationMs = delayMs
        };

        public static ScheduleConfig Periodic(float periodMs, int maxExecutions = -1) => new ScheduleConfig
        {
            Mode = EScheduleMode.Periodic,
            PeriodMs = periodMs,
            MaxExecutions = maxExecutions
        };

        public static ScheduleConfig TimedPeriodic(float delayMs, float periodMs, int maxExecutions = -1) => new ScheduleConfig
        {
            Mode = EScheduleMode.Periodic,
            DurationMs = delayMs,
            PeriodMs = periodMs,
            MaxExecutions = maxExecutions
        };

        /// <summary>
        /// 持续行为（每帧驱动直到外部终止）
        /// </summary>
        /// <param name="canBeInterrupted">是否可被中断</param>
        /// <param name="maxExecutions">最大执行次数，-1=无限</param>
        public static ScheduleConfig Continuous(bool canBeInterrupted = true, int maxExecutions = -1) => new ScheduleConfig
        {
            Mode = EScheduleMode.Continuous,
            CanBeInterrupted = canBeInterrupted,
            MaxExecutions = maxExecutions
        };

        public bool IsEmpty => Mode == EScheduleMode.Transient && DurationMs == 0 && PeriodMs == 0;
    }
}
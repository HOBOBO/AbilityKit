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

        public bool IsEmpty => Mode == EScheduleMode.Transient && DurationMs == 0 && PeriodMs == 0;
    }
}
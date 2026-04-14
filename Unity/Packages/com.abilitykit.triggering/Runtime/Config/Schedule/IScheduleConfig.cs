namespace AbilityKit.Triggering.Runtime.Config.Schedule
{
    /// <summary>
    /// 调度配置接口（静态配置数据）
    /// </summary>
    public interface IScheduleConfig
    {
        EScheduleMode Mode { get; }
        float DurationMs { get; }
        float PeriodMs { get; }
        int MaxExecutions { get; }
        bool CanBeInterrupted { get; }
        bool IsEmpty { get; }
    }
}
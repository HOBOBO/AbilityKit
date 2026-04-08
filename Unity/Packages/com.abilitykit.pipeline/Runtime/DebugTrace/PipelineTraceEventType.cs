namespace AbilityKit.Ability
{
    /// <summary>
    /// 管线追踪事件类型
    /// </summary>
    public enum PipelineTraceEventType
    {
        RunStart = 0,
        RunEnd = 1,
        PhaseStart = 2,
        PhaseComplete = 3,
        PhaseError = 4,
        Tick = 5,
    }
}

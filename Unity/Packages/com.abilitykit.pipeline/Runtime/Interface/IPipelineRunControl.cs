namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 非泛型的管线运行控制面，供不掌握具体上下文类型的调试工具使用。
    /// </summary>
    public interface IPipelineRunControl : IPipelineInterruptible
    {
        EAbilityPipelineState State { get; }
        bool IsPaused { get; }
        void Pause();
        void Resume();
        void Cancel();
    }
}

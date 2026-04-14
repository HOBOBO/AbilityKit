namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 能力管线状态
    /// </summary>
    public enum EAbilityPipelineState
    {
        Ready = 0,
        Executing = 1,
        Completed = 2,
        Failed = 3,
        Paused = 4
    }
}

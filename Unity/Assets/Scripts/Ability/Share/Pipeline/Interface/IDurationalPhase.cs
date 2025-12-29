namespace AbilityKit.Ability
{
    /// <summary>
    /// 持续性阶段标记接口（可选）
    /// 注意：现在 IAbilityPipelinePhase 已统一包含 IsComplete 和 OnUpdate
    /// 此接口仅用于向后兼容和语义标记
    /// </summary>
    public interface IDurationalPhase : IAbilityPipelinePhase
    {
        /// <summary>
        /// 阶段持续时间（-1表示无限）
        /// </summary>
        float Duration { get; }
    }
}
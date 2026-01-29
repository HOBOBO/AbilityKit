namespace AbilityKit.Ability
{
    /// <summary>
    /// 管线上下文接口
    /// </summary>
    public interface IAbilityPipelineContext
    {
        /// <summary>
        /// 当前阶段ID
        /// </summary>
        AbilityPipelinePhaseId CurrentPhaseId { get; set; }
        
        /// <summary>
        /// 管线状态
        /// </summary>
        EAbilityPipelineState PipelineState { get; set; }
        
        /// <summary>
        /// 是否被中断
        /// </summary>
        bool IsAborted { get; set; }
        
        /// <summary>
        /// 是否暂停
        /// </summary>
        bool IsPaused { get; set; }
        
        /// <summary>
        /// 管线开始时间
        /// </summary>
        float StartTime { get; set; }
        
        /// <summary>
        /// 已运行时间
        /// </summary>
        float ElapsedTime { get; }
        
        /// <summary>
        /// 重置上下文
        /// </summary>
        void Reset();
    }

    /// <summary>
    /// 管线上下文接口（强类型 AbilityInstance）
    /// </summary>
    public interface IAbilityPipelineContext<TAbilityInstance> : IAbilityPipelineContext
    {
        /// <summary>
        /// 能力实例（技能、Buff等）
        /// </summary>
        TAbilityInstance AbilityInstance { get; }
    }
}
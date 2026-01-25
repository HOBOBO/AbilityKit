namespace AbilityKit.Ability
{
    /// <summary>
    /// 能力管线接口
    /// </summary>
    public interface IAbilityPipeline
    {
        /// <summary>
        /// 管线事件
        /// </summary>
        AbilityPipelineEvents Events { get; }
        
        /// <summary>
        /// 当前状态
        /// </summary>
        EAbilityPipelineState State { get; }
        
        /// <summary>
        /// 当前上下文
        /// </summary>
        IAbilityPipelineContext Context { get; }
        
        /// <summary>
        /// 是否暂停
        /// </summary>
        bool IsPaused { get; }
        
        /// <summary>
        /// 执行管线
        /// </summary>
        EAbilityPipelineState Execute(IAbilityPipelineConfig config, object abilityInstance, params object[] args);
        
        /// <summary>
        /// 更新管线
        /// </summary>
        void OnUpdate(IAbilityPipelineContext context, float deltaTime);
        
        /// <summary>
        /// 暂停管线
        /// </summary>
        void Pause();
        
        /// <summary>
        /// 恢复管线
        /// </summary>
        void Resume();
        
        /// <summary>
        /// 中断管线
        /// </summary>
        void Interrupt();
        
        /// <summary>
        /// 添加阶段
        /// </summary>
        void AddPhase(IAbilityPipelinePhase phase);
        
        /// <summary>
        /// 插入阶段
        /// </summary>
        void InsertPhase(int index, IAbilityPipelinePhase phase);
        
        /// <summary>
        /// 移除阶段
        /// </summary>
        void RemovePhase(AbilityPipelinePhaseId phaseId);
        
        /// <summary>
        /// 重置管线
        /// </summary>
        void Reset();
    }
}
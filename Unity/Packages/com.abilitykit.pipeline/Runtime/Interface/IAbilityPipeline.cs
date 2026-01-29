namespace AbilityKit.Ability
{
    /// <summary>
    /// 能力管线接口
    /// </summary>
    public interface IAbilityPipeline<TCtx>
    {
        /// <summary>
        /// 管线事件
        /// </summary>
        AbilityPipelineEvents<TCtx> Events { get; }

        IAbilityPipelineRun<TCtx> Start(IAbilityPipelineConfig config, TCtx context);

        /// <summary>
        /// 添加阶段
        /// </summary>
        void AddPhase(IAbilityPipelinePhase<TCtx> phase);

        /// <summary>
        /// 插入阶段
        /// </summary>
        void InsertPhase(int index, IAbilityPipelinePhase<TCtx> phase);

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
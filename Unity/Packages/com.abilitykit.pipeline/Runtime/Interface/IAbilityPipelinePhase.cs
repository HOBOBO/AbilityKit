using System;
using System.Collections.Generic;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 管线阶段接口
    /// 统一模型：所有阶段都有 IsComplete 状态，瞬时阶段在 Execute 时立即完成
    /// </summary>
    public interface IAbilityPipelinePhase<TCtx>
    {
        /// <summary>
        /// 阶段ID
        /// </summary>
        AbilityPipelinePhaseId PhaseId { get; }
        
        /// <summary>
        /// 是否已完成
        /// </summary>
        bool IsComplete { get; }
        
        /// <summary>
        /// 是否是复合阶段
        /// </summary>
        bool IsComposite { get; }
        
        /// <summary>
        /// 子阶段列表（复合阶段使用）
        /// </summary>
        IReadOnlyList<IAbilityPipelinePhase<TCtx>> SubPhases { get; }
        
        /// <summary>
        /// 判断是否应执行该阶段
        /// </summary>
        bool ShouldExecute(TCtx context);
        
        /// <summary>
        /// 执行阶段逻辑（瞬时阶段在此方法中设置 IsComplete = true）
        /// </summary>
        void Execute(TCtx context);
        
        /// <summary>
        /// 更新阶段（每帧调用，持续性阶段在此更新状态）
        /// </summary>
        void OnUpdate(TCtx context, float deltaTime);
        
        /// <summary>
        /// 重置阶段状态（用于复用）
        /// </summary>
        void Reset();
        
        /// <summary>
        /// 处理阶段执行中的错误
        /// </summary>
        void HandleError(TCtx context, Exception exception);
    }
}

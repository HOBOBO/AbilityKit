using System;

namespace AbilityKit.Ability
{
    /// <summary>
    /// 管线事件定义
    /// </summary>
    public class AbilityPipelineEvents<TCtx>
    {
        /// <summary>
        /// 管线开始
        /// </summary>
        public Action<TCtx> OnPipelineStart;
        
        /// <summary>
        /// 管线完成
        /// </summary>
        public Action<TCtx> OnPipelineComplete;
        
        /// <summary>
        /// 管线错误
        /// </summary>
        public Action<TCtx, Exception> OnPipelineError;
        
        /// <summary>
        /// 管线中断
        /// </summary>
        public Action<TCtx, bool> OnPipelineInterrupt;
        
        /// <summary>
        /// 管线暂停
        /// </summary>
        public Action<TCtx> OnPipelinePause;
        
        /// <summary>
        /// 管线恢复
        /// </summary>
        public Action<TCtx> OnPipelineResume;
        
        /// <summary>
        /// 阶段开始
        /// </summary>
        public Action<IAbilityPipelinePhase<TCtx>, TCtx> OnPhaseStart;
        
        /// <summary>
        /// 阶段完成
        /// </summary>
        public Action<IAbilityPipelinePhase<TCtx>, TCtx> OnPhaseComplete;
        
        /// <summary>
        /// 阶段错误
        /// </summary>
        public Action<IAbilityPipelinePhase<TCtx>, TCtx, Exception> OnPhaseError;

        /// <summary>
        /// 清除所有事件
        /// </summary>
        public void Clear()
        {
            OnPipelineStart = null;
            OnPipelineComplete = null;
            OnPipelineError = null;
            OnPipelineInterrupt = null;
            OnPipelinePause = null;
            OnPipelineResume = null;
            OnPhaseStart = null;
            OnPhaseComplete = null;
            OnPhaseError = null;
        }
    }
}
using System;

namespace AbilityKit.Ability
{
    /// <summary>
    /// 管线事件定义
    /// </summary>
    public class AbilityPipelineEvents
    {
        /// <summary>
        /// 管线开始
        /// </summary>
        public Action<IAbilityPipelineContext> OnPipelineStart;
        
        /// <summary>
        /// 管线完成
        /// </summary>
        public Action<IAbilityPipelineContext> OnPipelineComplete;
        
        /// <summary>
        /// 管线错误
        /// </summary>
        public Action<IAbilityPipelineContext, Exception> OnPipelineError;
        
        /// <summary>
        /// 管线中断
        /// </summary>
        public Action<IAbilityPipelineContext, bool> OnPipelineInterrupt;
        
        /// <summary>
        /// 管线暂停
        /// </summary>
        public Action<IAbilityPipelineContext> OnPipelinePause;
        
        /// <summary>
        /// 管线恢复
        /// </summary>
        public Action<IAbilityPipelineContext> OnPipelineResume;
        
        /// <summary>
        /// 阶段开始
        /// </summary>
        public Action<IAbilityPipelinePhase, IAbilityPipelineContext> OnPhaseStart;
        
        /// <summary>
        /// 阶段完成
        /// </summary>
        public Action<IAbilityPipelinePhase, IAbilityPipelineContext> OnPhaseComplete;
        
        /// <summary>
        /// 阶段错误
        /// </summary>
        public Action<IAbilityPipelinePhase, IAbilityPipelineContext, Exception> OnPhaseError;

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
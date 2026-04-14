using System;
using AbilityKit.Pipeline;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 管线事件定义
    /// </summary>
    public class AbilityPipelineEvents<TCtx>
    {
        private int _sequence;

        /// <summary>
        /// 生成序列�?
        /// </summary>
        internal int NextSequence => ++_sequence;

        /// <summary>
        /// 管线开�?
        /// </summary>
        public Action<TCtx> OnPipelineStart;
        
        /// <summary>
        /// 管线完成
        /// </summary>
        public Action<TCtx> OnPipelineComplete;
        
        /// <summary>
        /// 管线失败
        /// </summary>
        public Action<TCtx, Exception> OnPipelineFailed;
        
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
        /// 阶段开�?
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
        /// 每帧 Tick（可选择订阅�?
        /// </summary>
        public Action<TCtx, float, EAbilityPipelineState> OnTick;

        /// <summary>
        /// 记录追踪数据
        /// </summary>
        internal void RecordTrace(IPipelineLifeOwner owner, EPipelineTraceEventType type, AbilityPipelinePhaseId phaseId, EAbilityPipelineState state, string message)
        {
            var data = new PipelineTraceData(_sequence++, type, phaseId, state, message);
            Pipeline.RecordTrace(owner, data);
        }

        /// <summary>
        /// 记录追踪数据（带阶段信息�?
        /// </summary>
        internal void RecordTracePhase(IPipelineLifeOwner owner, EPipelineTraceEventType type, AbilityPipelinePhaseId phaseId, string phaseName, EAbilityPipelineState state)
        {
            var data = new PipelineTraceData(_sequence++, type, phaseId, state, phaseName ?? string.Empty);
            Pipeline.RecordTrace(owner, data);
        }
        
        /// <summary>
        /// 清除所有事�?
        /// </summary>
        public void Clear()
        {
            OnPipelineStart = null;
            OnPipelineComplete = null;
            OnPipelineFailed = null;
            OnPipelineError = null;
            OnPipelineInterrupt = null;
            OnPipelinePause = null;
            OnPipelineResume = null;
            OnPhaseStart = null;
            OnPhaseComplete = null;
            OnPhaseError = null;
            OnTick = null;
            _sequence = 0;
        }
    }
}

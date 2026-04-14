using System;
using System.Collections.Generic;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 管线调试钩子注册�?
    /// Runtime 使用此类通知 Editor 进行调试追踪
    /// </summary>
    public static class PipelineDebugHooks
    {
        /// <summary>
        /// 当管线运行开始时调用（Editor 注册此回调）
        /// </summary>
        public static event Action<IPipelineLifeOwner, object, object> OnRunStarted;

        /// <summary>
        /// 当管线追踪数据记录时调用（Editor 注册此回调）
        /// </summary>
        public static event Action<IPipelineLifeOwner, PipelineTraceData> OnTrace;

        /// <summary>
        /// 是否有注册的回调
        /// </summary>
        public static bool HasHooks => OnRunStarted != null || OnTrace != null;

        /// <summary>
        /// 通知运行开�?
        /// </summary>
        public static void NotifyRunStarted<TCtx>(IPipelineLifeOwner owner, object pipeline, object config, object run)
            where TCtx : IAbilityPipelineContext
        {
            OnRunStarted?.Invoke(owner, config, run);
        }

        /// <summary>
        /// 通知追踪数据
        /// </summary>
        public static void NotifyTrace(IPipelineLifeOwner owner, PipelineTraceData data)
        {
            OnTrace?.Invoke(owner, data);
        }
    }
}

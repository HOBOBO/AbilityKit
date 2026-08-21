using System;

namespace AbilityKit.Pipeline
{
    /// <summary>
    /// 管线调试钩子注册器，运行时使用此类通知编辑器进行调试追踪。
    /// </summary>
    public static class PipelineDebugHooks
    {
        /// <summary>
        /// 管线运行开始时发送的完整诊断数据。
        /// </summary>
        public static event Action<PipelineRunStartedData>? OnRunStartedDetailed;

        /// <summary>
        /// 当管线运行开始时调用（编辑器注册此回调）。
        /// </summary>
        public static event Action<IPipelineLifeOwner, object, object>? OnRunStarted;

        /// <summary>
        /// 当管线追踪数据记录时调用（编辑器注册此回调）。
        /// </summary>
        public static event Action<IPipelineLifeOwner, PipelineTraceData>? OnTrace;

        /// <summary>
        /// 管线进入终态且尚未释放上下文时调用。
        /// </summary>
        public static event Action<PipelineRunEndedData>? OnRunEnded;

        /// <summary>
        /// 是否有注册的回调。
        /// </summary>
        public static bool HasHooks =>
            OnRunStartedDetailed != null || OnRunStarted != null || OnTrace != null || OnRunEnded != null;

        /// <summary>
        /// 通知运行开始。
        /// </summary>
        public static void NotifyRunStarted<TCtx>(
            IPipelineLifeOwner owner,
            object pipeline,
            IAbilityPipelineConfig config,
            IAbilityPipelineRun<TCtx> run)
            where TCtx : IAbilityPipelineContext
        {
            InvokeSafely(OnRunStartedDetailed, new PipelineRunStartedData(owner, pipeline, config, run, run.Context));
            InvokeSafely(OnRunStarted, owner, pipeline, run);
        }

        /// <summary>
        /// 通知追踪数据。
        /// </summary>
        public static void NotifyTrace(IPipelineLifeOwner owner, PipelineTraceData data)
        {
            InvokeSafely(OnTrace, owner, data);
        }

        /// <summary>
        /// 通知观察者一次运行已经进入终态。
        /// </summary>
        public static void NotifyRunEnded(IPipelineLifeOwner owner)
        {
            if (owner == null) return;
            InvokeSafely(OnRunEnded, new PipelineRunEndedData(owner));
        }

        private static void InvokeSafely<T>(Action<T>? handlers, T value)
        {
            if (handlers == null) return;
            foreach (Action<T> handler in handlers.GetInvocationList())
            {
                try { handler(value); }
                catch { }
            }
        }

        private static void InvokeSafely<T1, T2>(Action<T1, T2>? handlers, T1 first, T2 second)
        {
            if (handlers == null) return;
            foreach (Action<T1, T2> handler in handlers.GetInvocationList())
            {
                try { handler(first, second); }
                catch { }
            }
        }

        private static void InvokeSafely<T1, T2, T3>(Action<T1, T2, T3>? handlers, T1 first, T2 second, T3 third)
        {
            if (handlers == null) return;
            foreach (Action<T1, T2, T3> handler in handlers.GetInvocationList())
            {
                try { handler(first, second, third); }
                catch { }
            }
        }
    }
}

using AbilityKit.Core.Common.Event;

namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 触发器观察者接口
    /// 提供运行时外部监控能力（与 ITriggerLifecycle 互补，Lifecycle 用于 AOP 拦截，Observer 用于监控记录）
    /// </summary>
    public interface ITriggerObserver<TCtx>
    {
        // ========================================================================
        // 触发器级 Hook（原有）
        // ========================================================================

        /// <summary>
        /// 条件评估完成时调用
        /// </summary>
        void OnEvaluate<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, bool passed, in ExecCtx<TCtx> ctx);

        /// <summary>
        /// 行为执行完成时调用
        /// </summary>
        void OnExecute<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, in ExecCtx<TCtx> ctx);

        /// <summary>
        /// 短路发生时调用
        /// </summary>
        void OnShortCircuit<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, ETriggerShortCircuitReason reason, in ExecCtx<TCtx> ctx);

        // ========================================================================
        // 条件节点级 Hook（细粒度）
        // ========================================================================

        /// <summary>
        /// 条件节点评估成功时调用
        /// </summary>
        void OnConditionPassed<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int conditionId, string conditionName, in ExecCtx<TCtx> ctx);

        /// <summary>
        /// 条件节点评估失败时调用
        /// </summary>
        void OnConditionFailed<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int conditionId, string conditionName, in ExecCtx<TCtx> ctx);

        // ========================================================================
        // 行为节点级 Hook（细粒度）
        // ========================================================================

        /// <summary>
        /// 单个行为执行前调用
        /// </summary>
        void OnActionExecuting<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, in ExecCtx<TCtx> ctx);

        /// <summary>
        /// 单个行为执行完成后调用
        /// </summary>
        void OnActionExecuted<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, bool wasInterrupted, in ExecCtx<TCtx> ctx);

        /// <summary>
        /// 单个行为执行异常时调用
        /// </summary>
        void OnActionFailed<TArgs>(EventKey<TArgs> key, in TArgs args, int phase, int priority, long order, int actionId, string actionName, int actionIndex, int totalActions, string errorMessage, in ExecCtx<TCtx> ctx);
    }
}

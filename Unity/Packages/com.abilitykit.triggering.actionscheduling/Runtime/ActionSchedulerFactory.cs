using System;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Dispatcher;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.ActionScheduler
{
    /// <summary>
    /// 装配尚未登记的动作实例及执行器，不持有分组状态，也不运行或取消动作。
    /// </summary>
    internal static class ActionSchedulerFactory
    {
        /// <summary>
        /// 创建默认执行器；请求采用计划策略时，在外层装配队列或重试执行器。
        /// </summary>
        /// <remarks>
        /// 包装器装配失败时归还已租用的默认执行器；成功后由实例或调用方负责归还。
        /// </remarks>
        internal static IActionExecutor CreateExecutor(
            ActionSchedulerPools pools,
            in ActionCallPlan plan,
            Action<object, ITriggerDispatcherContext> action,
            bool usePlanPolicy,
            out bool ownsExecutor)
        {
            ownsExecutor = pools != null;
            var baseExecutor = pools != null
                ? pools.RentDefaultActionExecutor(action)
                : new DefaultActionExecutor(action);
            if (!usePlanPolicy) return baseExecutor;

            var execution = plan.Execution;
            try
            {
                switch (execution.Policy)
                {
                    case EActionExecutionPolicy.Queued:
                        return pools != null
                            ? pools.RentQueuedActionExecutor(baseExecutor)
                            : new QueuedActionExecutor(baseExecutor);
                    case EActionExecutionPolicy.WithRetry:
                        return pools != null
                            ? pools.RentRetryActionExecutor(baseExecutor, execution.RetryMaxRetries, execution.RetryDelayMs)
                            : new RetryActionExecutor(baseExecutor, execution.RetryMaxRetries, execution.RetryDelayMs);
                    default:
                        return baseExecutor;
                }
            }
            catch
            {
                if (ownsExecutor)
                {
                    pools.ReleaseActionExecutor(baseExecutor);
                }

                throw;
            }
        }

        /// <summary>
        /// 使用给定的身份、起始时间和执行器初始化实例；未提供池时直接创建。
        /// </summary>
        internal static ActionInstance CreateInstance(
            ActionSchedulerPools pools,
            int instanceId,
            int triggerId,
            in ActionCallPlan plan,
            IActionExecutor executor,
            object boundArgs,
            float elapsedMs,
            bool ownsExecutor) =>
            pools != null
                ? pools.RentActionInstance(instanceId, triggerId, in plan, executor, boundArgs, elapsedMs, ownsExecutor)
                : new ActionInstance(instanceId, triggerId, plan, executor, boundArgs, elapsedMs);
    }
}

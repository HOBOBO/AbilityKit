using System;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.ActionScheduler;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Dispatcher;
using RuntimeActionScheduler = AbilityKit.Triggering.Runtime.ActionScheduler.ActionScheduler;

namespace AbilityKit.Triggering.Runtime.Plan
{
    /// <summary>
    /// PlannedTrigger 的调度注册辅助，统一封装 ActionScheduler 注册与执行器选择。
    /// </summary>
    internal static class PlannedTriggerScheduleRegistrar<TArgs, TCtx>
    {
        public static void RegisterOrReplace(
            RuntimeActionScheduler actionScheduler,
            in ActionCallPlan call,
            int planIndex,
            in TArgs args,
            ExecutionControl control,
            Func<int, Action<object, ITriggerDispatcherContext>> actionDelegateFactory,
            TriggerPredicate<object> conditionDelegate)
        {
            if (actionScheduler == null) throw new ArgumentNullException(nameof(actionScheduler));
            if (actionDelegateFactory == null) throw new ArgumentNullException(nameof(actionDelegateFactory));

            var actionDelegate = actionDelegateFactory(planIndex);
            var executor = actionScheduler.CreateExecutor(in call, actionDelegate, out var ownsExecutor);

            actionScheduler.RegisterOrReplace(
                planIndex: planIndex,
                plan: call,
                actionDelegate: actionDelegate,
                conditionDelegate: conditionDelegate,
                boundArgs: args,
                executor: executor,
                ownsExecutor: ownsExecutor
            );
        }

        private static IActionExecutor CreateExecutor(in ActionCallPlan plan, Action<object, ITriggerDispatcherContext> action, ExecutionControl control)
        {
            throw new NotSupportedException("This overload is replaced by ActionScheduler.CreateExecutor.");
        }
    }
}

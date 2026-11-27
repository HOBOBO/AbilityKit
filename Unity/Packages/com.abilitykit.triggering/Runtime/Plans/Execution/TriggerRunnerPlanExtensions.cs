using System;
using System.Reflection;
using AbilityKit.Core.Eventing;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 将已构造的计划绑定到运行器的事件注册入口。
    /// </summary>
    public static class TriggerRunnerPlanExtensions
    {
        private static readonly MethodInfo RegisterPlanAsMethod = typeof(TriggerRunnerPlanExtensions).GetMethod(nameof(RegisterPlanAs), BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>
        /// 按计划的阶段和优先级注册类型化事件；只核对调度装配，不执行计划校验。
        /// </summary>
        /// <returns>由调用方持有并释放的当前注册句柄。</returns>
        /// <exception cref="InvalidOperationException">计划含非即时动作而运行器没有扁平动作登记实现；不会新增订阅。</exception>
        public static IDisposable RegisterPlan<TArgs, TCtx>(this TriggerRunner<TCtx> runner, EventKey<TArgs> key, in TriggerPlan<TArgs> plan)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (!runner.HasFlatActionScheduler)
            {
                var actionIndex = PlannedTrigger<TArgs, TCtx>.FindFirstScheduledAction(plan.Actions);
                if (actionIndex >= 0)
                    throw PlannedTrigger<TArgs, TCtx>.MissingScheduler(in plan, actionIndex);
            }

            var trigger = new PlannedTrigger<TArgs, TCtx>(plan);
            return runner.Register(key, trigger, plan.Phase, plan.Priority);
        }

        /// <summary>
        /// 使用宿主已解析的载荷类型动态绑定计划，沿用类型化入口的调度装配检查。
        /// </summary>
        /// <remarks>
        /// 此入口运行时构造泛型方法；载荷类型在 AOT 环境中的可用性由宿主验证。
        /// </remarks>
        /// <returns>由调用方持有并释放的当前注册句柄。</returns>
        public static IDisposable RegisterPlan<TCtx>(this TriggerRunner<TCtx> runner, int eventId, Type argsType, in TriggerPlan<object> plan)
        {
            if (runner == null) throw new ArgumentNullException(nameof(runner));
            if (argsType == null) throw new ArgumentNullException(nameof(argsType));
            if (eventId == 0) throw new ArgumentException("Event id must be non-zero.", nameof(eventId));
            if (RegisterPlanAsMethod == null) throw new MissingMethodException(nameof(TriggerRunnerPlanExtensions), nameof(RegisterPlanAs));

            try
            {
                var mi = RegisterPlanAsMethod.MakeGenericMethod(argsType, typeof(TCtx));
                return (IDisposable)mi.Invoke(null, new object[] { runner, eventId, plan });
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                throw ex.InnerException;
            }
        }

        private static IDisposable RegisterPlanAs<TArgs, TCtx>(TriggerRunner<TCtx> runner, int eventId, TriggerPlan<object> plan)
        {
            var typedPlan = plan.AsArgs<TArgs>();
            var key = new EventKey<TArgs>(eventId);
            return runner.RegisterPlan<TArgs, TCtx>(key, typedPlan);
        }
    }
}

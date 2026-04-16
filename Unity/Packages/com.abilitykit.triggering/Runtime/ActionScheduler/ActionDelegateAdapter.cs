using System;
using AbilityKit.Triggering.Runtime.Dispatcher;
using AbilityKit.Triggering.Runtime.Executable;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Context;
using AbilityKit.Triggering.Variables.Numeric;

namespace AbilityKit.Triggering.Runtime.ActionScheduler
{
    /// <summary>
    /// Action 委托适配器
    /// 将 PlannedTrigger 中强类型的 Action 委托适配为 ActionScheduler 使用的通用委托
    /// </summary>
    internal static class ActionDelegateAdapter
    {
        /// <summary>
        /// 创建通用 Action 委托
        /// </summary>
        public static Action<object, ITriggerDispatcherContext> Create<TArgs, TCtx>(
            Action0<TArgs, TCtx> action0,
            Action1<TArgs, TCtx> action1,
            Action2<TArgs, TCtx> action2,
            int arity,
            NumericValueRef arg0,
            NumericValueRef arg1)
            where TArgs : class
        {
            return (argsObj, dispatcherCtx) =>
            {
                var args = (TArgs)argsObj;
                var execCtx = CreateExecCtx<TArgs, TCtx>(dispatcherCtx);

                switch (arity)
                {
                    case 0:
                        action0?.Invoke(args, execCtx);
                        break;
                    case 1:
                        var v0 = arg0.Resolve(args);
                        action1?.Invoke(args, v0, execCtx);
                        break;
                    case 2:
                        var v1 = arg0.Resolve(args);
                        var v2 = arg1.Resolve(args);
                        action2?.Invoke(args, v1, v2, execCtx);
                        break;
                }
            };
        }

        /// <summary>
        /// 创建条件委托适配器
        /// </summary>
        public static TriggerPredicate<object> CreatePredicate<TArgs, TCtx>(
            Predicate0<TArgs, TCtx> predicate0,
            Predicate1<TArgs, TCtx> predicate1,
            Predicate2<TArgs, TCtx> predicate2,
            int arity,
            NumericValueRef arg0,
            NumericValueRef arg1)
            where TArgs : class
        {
            return (argsObj, dispatcherCtx) =>
            {
                var args = (TArgs)argsObj;
                var execCtx = CreateExecCtx<TArgs, TCtx>(dispatcherCtx);

                switch (arity)
                {
                    case 0:
                        return predicate0(args, execCtx);
                    case 1:
                        var v0 = arg0.Resolve(args);
                        return predicate1(args, v0, execCtx);
                    case 2:
                        var v1 = arg0.Resolve(args);
                        var v2 = arg1.Resolve(args);
                        return predicate2(args, v1, v2, execCtx);
                    default:
                        return false;
                }
            };
        }

        /// <summary>
        /// 从 ITriggerDispatcherContext 创建 ExecCtx
        /// </summary>
        private static ExecCtx<TCtx> CreateExecCtx<TArgs, TCtx>(ITriggerDispatcherContext dispatcherCtx)
            where TArgs : class
        {
            // TODO: 从 dispatcherCtx 获取服务并构建 ExecCtx
            // 当前返回默认值，实际应从 dispatcherCtx.GetService<T>() 获取各服务
            return default;
        }
    }
}

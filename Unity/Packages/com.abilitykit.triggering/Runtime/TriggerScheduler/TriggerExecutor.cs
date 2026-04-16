using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Executable;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Dispatcher;
using AbilityKit.Triggering.Runtime.Context;
using AbilityKit.Triggering.Runtime.ActionScheduler;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Registry;

namespace AbilityKit.Triggering.Runtime.TriggerScheduler
{
    /// <summary>
    /// Trigger 执行上下文
    /// 封装单次 Trigger 激活时的执行环境
    /// </summary>
    public readonly struct TriggerExecutionContext<TCtx>
    {
        public readonly TCtx Context;
        public readonly ITriggerDispatcherContext DispatcherContext;
        public readonly ExecutionControl Control;
        public readonly ActionScheduler.ActionSchedulerManager ActionSchedulerManager;

        public TriggerExecutionContext(
            TCtx context,
            ITriggerDispatcherContext dispatcherContext,
            ExecutionControl control,
            ActionScheduler.ActionSchedulerManager actionSchedulerManager)
        {
            Context = context;
            DispatcherContext = dispatcherContext;
            Control = control;
            ActionSchedulerManager = actionSchedulerManager;
        }
    }

    /// <summary>
    /// Trigger 执行器接口
    /// 负责单次 Trigger 激活时的执行策略
    /// </summary>
    public interface ITriggerExecutor<TCtx>
    {
        /// <summary>
        /// 执行 Trigger
        /// </summary>
        ExecutionResult Execute<TArgs>(TriggerPlan<TArgs> plan, TriggerExecutionContext<TCtx> ctx) where TArgs : class;
    }

    /// <summary>
    /// 默认 Trigger 执行器
    /// 按照优先级顺序执行 Actions，支持打断和优先级抢占
    /// </summary>
    public sealed class DefaultTriggerExecutor<TCtx> : ITriggerExecutor<TCtx>
    {
        private readonly IActionExecutor _defaultActionExecutor;

        public DefaultTriggerExecutor(IActionExecutor defaultActionExecutor = null)
        {
            _defaultActionExecutor = defaultActionExecutor;
        }

        public ExecutionResult Execute<TArgs>(TriggerPlan<TArgs> plan, TriggerExecutionContext<TCtx> ctx) where TArgs : class
        {
            // 创建/获取 ActionScheduler
            var actionScheduler = ctx.ActionSchedulerManager.GetOrCreateScheduler(plan.TriggerId);

            // 准备 Actions
            var actions = plan.Actions;
            if (actions == null || actions.Length == 0)
            {
                return ExecutionResult.Success(0);
            }

            int registeredCount = 0;

            // 为每个 Action 创建实例并注册到 ActionScheduler
            for (int i = 0; i < actions.Length; i++)
            {
                var actionPlan = actions[i];

                // 解析 Action 委托（延迟解析，与 PlannedTrigger 保持一致）
                // 注意：这里简化处理，实际需要从 ActionRegistry 解析
                var actionDelegate = CreateActionDelegate(actionPlan.Id);
                var conditionDelegate = CreateConditionDelegate<TArgs>(plan); // TODO: 解析条件

                // 创建或获取执行器
                var executor = _defaultActionExecutor ?? new ActionScheduler.DefaultActionExecutor(actionDelegate);

                // 注册到 ActionScheduler
                actionScheduler.Register(
                    plan: actionPlan,
                    actionDelegate: actionDelegate,
                    conditionDelegate: conditionDelegate,
                    boundArgs: ctx.Context,
                    executor: executor
                );

                registeredCount++;
            }

            // 立即执行 Immediate 模式的 Action
            // 其他模式由 ActionScheduler 自主调度
            return ExecutionResult.Success(registeredCount);
        }

        private Action<object, ITriggerDispatcherContext> CreateActionDelegate(ActionId actionId)
        {
            // TODO: 从 ActionRegistry 解析委托
            return (args, ctx) =>
            {
                // 占位实现
                Console.WriteLine($"Action[{actionId}] executed");
            };
        }

        private TriggerPredicate<object> CreateConditionDelegate<TArgs>(TriggerPlan<TArgs> plan) where TArgs : class
        {
            // TODO: 从 FunctionRegistry 解析条件委托
            return null;
        }
    }
}

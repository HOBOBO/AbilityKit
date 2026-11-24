using System;
using AbilityKit.Core.Logging;
using AbilityKit.Triggering.Blackboard;
using AbilityKit.Triggering.Payload;
using AbilityKit.Triggering.Registry;
using AbilityKit.Triggering.Runtime;
using AbilityKit.Triggering.Runtime.ActionScheduler;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Dispatcher;
using RuntimeActionScheduler = AbilityKit.Triggering.Runtime.ActionScheduler.ActionScheduler;

namespace AbilityKit.Triggering.Runtime.Plan
{
    /// <summary>
    /// 触发器计划执行器
    /// 从 TriggerPlan 解析委托，并在满足条件时执行 Actions
    /// </summary>
    public sealed class PlannedTrigger<TArgs, TCtx> : ITrigger<TArgs, TCtx>, ITriggerWithId
    {
        /// <summary>
        /// 执行计划根并返回节点结果；经此触发器派发时不读取该结果。
        /// </summary>
        public delegate TriggerPlanExecutionResult ExecutionRootDelegate(
            in TArgs args,
            in ExecCtx<TCtx> ctx);

        private readonly PlannedTriggerActionExecutor<TArgs, TCtx> _actionExecutor;

        /// <inheritdoc />
        public ITriggerCue Cue => _plan.Cue;

        /// <inheritdoc />
        public int TriggerId => _plan.TriggerId;

        public PlannedTrigger(in TriggerPlan<TArgs> plan)
            : this(in plan, null)
        {
        }

        public PlannedTrigger(in TriggerPlan<TArgs> plan, ExecutionRootDelegate executionRoot)
        {
            _plan = plan;
            _executionRoot = executionRoot;
            _actionExecutor = new PlannedTriggerActionExecutor<TArgs, TCtx>(in plan);
            _resolved = false;
            _execCtx = default;
        }

        private readonly TriggerPlan<TArgs> _plan;
        private readonly ExecutionRootDelegate _executionRoot;
        private bool _resolved;
        private ExecCtx<TCtx> _execCtx;
        private int _executionCount;
        private float _lastExecutionTimeMs;
        private bool _hasLastExecutionTime;

        private ExecCtx<TCtx> ExecCtx => _execCtx;

        public bool Evaluate(in TArgs args, in ExecCtx<TCtx> ctx)
        {
            return PlannedTriggerPredicateEvaluator<TArgs, TCtx>.Evaluate(in _plan, in args, in ctx);
        }

        /// <summary>
        /// 执行计划的有效分支；执行根优先于扁平动作列表。
        /// 执行根一旦调用即记一次执行资格尝试；正常返回时不按返回状态区分自动优先级中断。
        /// </summary>
        /// <exception cref="InvalidOperationException">扁平动作需要调度而上下文未提供管理器；不会执行部分动作。</exception>
        public void Execute(in TArgs args, in ExecCtx<TCtx> ctx)
        {
            var actions = _plan.Actions;
            var hasExecutionRoot = _executionRoot != null;
            var hasActions = hasExecutionRoot || (actions != null && actions.Length > 0);
            var shouldExecute = hasActions && CanExecuteByControl(in ctx);
            var firstScheduledAction = shouldExecute && !hasExecutionRoot ? FindFirstScheduledAction(actions) : -1;
            if (firstScheduledAction >= 0 && ctx.ActionSchedulerManager == null)
                throw MissingScheduler(in _plan, firstScheduledAction);

            _execCtx = ctx;
            if (!shouldExecute) return;

            if (hasExecutionRoot)
            {
                ExecuteRoot(in args, in ctx);
                return;
            }

            Resolve(ctx);
            if (firstScheduledAction < 0)
            {
                ExecuteImmediate(in args, in ctx);
                return;
            }

            ExecuteMixedActions(in args, in ctx, actions);
        }

        private void ExecuteRoot(in TArgs args, in ExecCtx<TCtx> ctx)
        {
            var control = ctx.Control ?? new ExecutionControl();
            try
            {
                _executionRoot(in args, in ctx);
            }
            finally
            {
                MarkExecutedByControl(in ctx);
            }

            ApplyInterruptControl(control);
        }

        /// <summary>
        /// 返回首个需要调度的扁平动作索引；没有非即时动作时返回 -1。
        /// </summary>
        internal static int FindFirstScheduledAction(ActionCallPlan[] actions)
        {
            if (actions == null) return -1;

            for (int i = 0; i < actions.Length; i++)
            {
                if (IsScheduledAction(in actions[i])) return i;
            }

            return -1;
        }

        /// <summary>
        /// 为注册检查和执行兜底构造一致的缺依赖诊断。
        /// </summary>
        internal static InvalidOperationException MissingScheduler(in TriggerPlan<TArgs> plan, int actionIndex)
        {
            var mode = plan.Actions[actionIndex].Schedule.Mode;
            return new InvalidOperationException(
                $"TriggerId={plan.TriggerId}, Actions[{actionIndex}], Mode={mode}：非即时扁平动作需要宿主显式装配并推进 ActionSchedulerManager。");
        }

        private static bool IsScheduledAction(in ActionCallPlan call)
        {
            return call.Schedule.Mode != Config.EActionScheduleMode.Immediate;
        }

        private void ExecuteMixedActions(in TArgs args, in ExecCtx<TCtx> ctx, ActionCallPlan[] actions)
        {
            var actionScheduler = ctx.ActionSchedulerManager.GetOrCreateScheduler(_plan.TriggerId);
            var control = ctx.Control ?? new ExecutionControl();

            try
            {
                for (int i = 0; i < actions.Length; i++)
                {
                    var call = actions[i];
                    if (IsScheduledAction(in call))
                    {
                        RegisterScheduledAction(actionScheduler, call, i, in args);
                    }
                    else
                    {
                        _actionExecutor.Execute(in args, in ctx, i);
                    }

                    if (ctx.Control != null && ctx.Control.IsPlanStopped)
                    {
                        return;
                    }
                }
            }
            finally
            {
                MarkExecutedByControl(in ctx);
            }

            ApplyInterruptControl(control);
        }

        /// <summary>
        /// 将单个 Action 计划注册到触发器级调度器，并按计划索引替换旧实例。
        /// </summary>
        private void RegisterScheduledAction(RuntimeActionScheduler actionScheduler, ActionCallPlan call, int planIndex, in TArgs args)
        {
            PlannedTriggerScheduleRegistrar.RegisterOrReplace(
                actionScheduler,
                in call,
                planIndex,
                in args,
                CreateActionDelegate,
                CreateConditionDelegate());
        }

        /// <summary>
        /// 按计划阈值拦截更低优先级的后续规则；是否调用由当前执行分支决定。
        /// </summary>
        private void ApplyInterruptControl(ExecutionControl control)
        {
            if (control == null || _plan.InterruptPriority <= 0)
            {
                return;
            }

            control.StopBelowPriority(
                _plan.InterruptPriority,
                conditionPassed: true,
                _plan.TriggerId,
                $"Trigger[{_plan.TriggerId}]"
            );
        }

        /// <summary>
        /// 立即执行模式
        /// </summary>
        private void ExecuteImmediate(in TArgs args, in ExecCtx<TCtx> ctx)
        {
            var actions = _plan.Actions;
            try
            {
                for (int i = 0; i < actions.Length; i++)
                {
                    _actionExecutor.Execute(in args, in ctx, i);

                    if (ctx.Control != null && ctx.Control.IsPlanStopped) return;
                }
            }
            finally
            {
                MarkExecutedByControl(in ctx);
            }
        }

        private bool CanExecuteByControl(in ExecCtx<TCtx> ctx)
        {
            var control = _plan.ExecutionControl;
            switch (control.Mode)
            {
                case ETriggerExecutionMode.Once:
                    return _executionCount <= 0;
                case ETriggerExecutionMode.Repeat:
                    return control.MaxExecutions <= 0 || _executionCount < control.MaxExecutions;
                case ETriggerExecutionMode.Cooldown:
                    if (control.CooldownMs <= 0f || !_hasLastExecutionTime)
                    {
                        return true;
                    }
                    return ctx.Policy.TotalTimeMs - _lastExecutionTimeMs >= control.CooldownMs;
                case ETriggerExecutionMode.Always:
                default:
                    return true;
            }
        }

        private void MarkExecutedByControl(in ExecCtx<TCtx> ctx)
        {
            var control = _plan.ExecutionControl;
            if (control.Mode == ETriggerExecutionMode.Always && control.MaxExecutions <= 0 && control.CooldownMs <= 0f)
            {
                return;
            }

            _executionCount++;
            _lastExecutionTimeMs = ctx.Policy.TotalTimeMs;
            _hasLastExecutionTime = true;
        }

        /// <summary>
        /// 创建 Action 委托适配器。
        /// 调度器执行时复用立即执行路径中的单 Action 解析与调用逻辑，避免调度路径维护另一套参数解析分支。
        /// </summary>
        private Action<object, ITriggerDispatcherContext> CreateActionDelegate(int index)
        {
            return _actionExecutor.CreateActionDelegate(index, () => ExecCtx);
        }

        /// <summary>
        /// 创建条件委托（如果 TriggerPlan 包含 Predicate）
        /// </summary>
        private TriggerPredicate<object> CreateConditionDelegate()
        {
            return PlannedTriggerPredicateEvaluator<TArgs, TCtx>.CreateConditionDelegate(in _plan, in _execCtx);
        }

        private void Resolve(in ExecCtx<TCtx> ctx)
        {
            if (_resolved) return;

            _actionExecutor.Resolve(in ctx);
            _resolved = true;
        }

    }
}


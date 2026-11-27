using System;
using AbilityKit.Triggering.Runtime.Executable;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Dispatcher;

namespace AbilityKit.Triggering.Runtime.ActionScheduler
{
    /// <summary>
    /// Action 实例状态
    /// </summary>
    internal enum EActionInstanceState : byte
    {
        /// <summary>已注册，等待调度</summary>
        Registered = 0,
        /// <summary>等待延迟（Delayed 模式）</summary>
        WaitingDelay = 1,
        /// <summary>等待条件满足</summary>
        WaitingCondition = 2,
        /// <summary>等待同步信号</summary>
        WaitingSignal = 3,
        /// <summary>等待队列</summary>
        WaitingQueue = 4,
        /// <summary>正在执行</summary>
        Executing = 5,
        /// <summary>执行完成</summary>
        Completed = 6,
        /// <summary>已中断</summary>
        Interrupted = 7,
        /// <summary>执行失败</summary>
        Failed = 8,
    }

    /// <summary>
    /// Action 运行时实例
    /// 由 Trigger 激活时创建，由 ActionScheduler 管理生命周期
    /// </summary>
    internal sealed class ActionInstance
    {
        public int InstanceId { get; private set; }
        public int TriggerId { get; private set; }
        public ActionCallPlan Plan { get; private set; }
        public EActionInstanceState State { get; internal set; }
        public bool IsActive => State is EActionInstanceState.Registered
                                or EActionInstanceState.WaitingDelay
                                or EActionInstanceState.WaitingCondition
                                or EActionInstanceState.WaitingSignal
                                or EActionInstanceState.WaitingQueue
                                or EActionInstanceState.Executing;
        public int ExecutionCount { get; internal set; }
        public float ElapsedMs { get; internal set; }
        public float LastExecuteMs { get; internal set; }
        public bool CanBeInterrupted => Executor != null && Plan.Schedule.CanBeInterrupted;
        public string InterruptReason { get; internal set; }
        public IActionExecutor Executor { get; private set; }
        public bool OwnsExecutor { get; private set; }
        internal Action<object, ITriggerDispatcherContext> ActionDelegate { get; set; }
        internal TriggerPredicate<object> ConditionDelegate { get; set; }
        internal object BoundArgs { get; set; }
        public object GlobalContext { get; private set; }
        public float CreatedAtMs { get; private set; }

        internal ActionInstance()
        {
        }

        internal ActionInstance(
            int instanceId,
            int triggerId,
            ActionCallPlan plan,
            IActionExecutor executor,
            object globalContext,
            float createdAtMs = 0f)
        {
            Initialize(instanceId, triggerId, in plan, executor, globalContext, createdAtMs, ownsExecutor: false);
        }

        internal void Initialize(
            int instanceId,
            int triggerId,
            in ActionCallPlan plan,
            IActionExecutor executor,
            object globalContext,
            float createdAtMs,
            bool ownsExecutor)
        {
            InstanceId = instanceId;
            TriggerId = triggerId;
            Plan = plan;
            Executor = executor ?? throw new ArgumentNullException(nameof(executor));
            OwnsExecutor = ownsExecutor;
            GlobalContext = globalContext;
            State = EActionInstanceState.Registered;
            ElapsedMs = 0;
            LastExecuteMs = 0;
            ExecutionCount = 0;
            InterruptReason = null;
            CreatedAtMs = Math.Max(0f, createdAtMs);
            ActionDelegate = null;
            ConditionDelegate = null;
            BoundArgs = null;
        }

        public ExecutionResult Update(float deltaTimeMs, ActionExecutionContext ctx)
        {
            if (State is EActionInstanceState.Completed or EActionInstanceState.Interrupted or EActionInstanceState.Failed)
                return ExecutionResult.Success(0);

            ElapsedMs += deltaTimeMs;

            if (!CanEnterExecutionWindow())
            {
                State = EActionInstanceState.WaitingDelay;
                return ExecutionResult.None;
            }

            if (ConditionDelegate != null && State != EActionInstanceState.Executing)
            {
                bool conditionMet = ConditionDelegate(BoundArgs, ctx.DispatcherContext);
                if (!conditionMet)
                {
                    State = EActionInstanceState.WaitingCondition;
                    return ExecutionResult.None;
                }

                State = EActionInstanceState.Executing;
            }

            var schedule = Plan.Schedule;
            if (schedule.Mode == Config.EActionScheduleMode.Timeline)
            {
                State = EActionInstanceState.Failed;
                return ExecutionResult.Failed("ActionScheduler 尚未定义 Timeline 子 Action 序列，不能按单一 ActionCallPlan 执行 Timeline。请改用 Plan/Executables 主线承载时间线行为。");
            }

            var execution = ExecuteInternal(ctx);

            if (execution.Result.IsFailed)
            {
                State = EActionInstanceState.Failed;
            }
            else if (execution.Executed && ShouldTerminate())
            {
                State = EActionInstanceState.Completed;
            }

            return execution.Result;
        }

        private bool CanEnterExecutionWindow()
        {
            var schedule = Plan.Schedule;
            switch (schedule.Mode)
            {
                case Config.EActionScheduleMode.Immediate:
                    return true;
                case Config.EActionScheduleMode.Delayed:
                    return ElapsedMs >= Math.Max(0f, schedule.Param);
                case Config.EActionScheduleMode.Periodic:
                case Config.EActionScheduleMode.Continuous:
                    if (schedule.Param <= 0f)
                    {
                        return true;
                    }
                    if (ExecutionCount <= 0)
                    {
                        return ElapsedMs >= schedule.Param;
                    }
                    return ElapsedMs - LastExecuteMs >= schedule.Param;
                case Config.EActionScheduleMode.Timeline:
                    return true;
                default:
                    return true;
            }
        }

        private ActionExecutionStep ExecuteInternal(ActionExecutionContext ctx)
        {
            if (!CanExecuteByPolicy(ctx))
            {
                return new ActionExecutionStep(false, ExecutionResult.Skipped("Policy check failed"));
            }

            if (Plan.Execution.Policy == Config.EActionExecutionPolicy.WithRollback)
            {
                return new ActionExecutionStep(false, ExecutionResult.Failed($"Action[{Plan.Id.Value}] 请求 WithRollback，但 ActionCallPlan 当前没有正式的补偿 Action 或回滚计划结构。"));
            }

            var executed = Executor.TryExecute(ctx, out var result);

            if (executed)
            {
                ExecutionCount++;
                LastExecuteMs = ElapsedMs;
            }

            return new ActionExecutionStep(executed, result);
        }

        private readonly struct ActionExecutionStep
        {
            public readonly bool Executed;
            public readonly ExecutionResult Result;

            public ActionExecutionStep(bool executed, ExecutionResult result)
            {
                Executed = executed;
                Result = result;
            }
        }

        private bool CanExecuteByPolicy(ActionExecutionContext ctx)
        {
            return Plan.Execution.Policy switch
            {
                Config.EActionExecutionPolicy.Conditional => ConditionDelegate?.Invoke(BoundArgs, ctx.DispatcherContext) ?? true,
                Config.EActionExecutionPolicy.Queued => !IsQueued(),
                Config.EActionExecutionPolicy.Parallel => true,
                Config.EActionExecutionPolicy.Deferred => ElapsedMs > 0,
                _ => true
            };
        }

        private bool IsQueued()
        {
            return Executor is QueuedActionExecutor queued && queued.IsQueued;
        }

        private bool ShouldTerminate()
        {
            var schedule = Plan.Schedule;
            switch (schedule.Mode)
            {
                case Config.EActionScheduleMode.Immediate:
                    return true;
                case Config.EActionScheduleMode.Delayed:
                    return ExecutionCount >= 1;
                case Config.EActionScheduleMode.Periodic:
                    if (schedule.MaxExecutions > 0 && ExecutionCount >= schedule.MaxExecutions)
                        return true;
                    break;
                case Config.EActionScheduleMode.Continuous:
                    break;
                case Config.EActionScheduleMode.Timeline:
                    return ExecutionCount >= 1;
            }

            return false;
        }

        public void RequestInterrupt(string reason)
        {
            if (!CanBeInterrupted) return;
            State = EActionInstanceState.Interrupted;
            InterruptReason = reason;
            Executor.Cancel(reason);
        }

        public void Reset()
        {
            State = EActionInstanceState.Registered;
            ElapsedMs = 0;
            LastExecuteMs = 0;
            ExecutionCount = 0;
            InterruptReason = null;
        }

        internal void ResetForPool()
        {
            State = EActionInstanceState.Registered;
            ElapsedMs = 0;
            LastExecuteMs = 0;
            ExecutionCount = 0;
            InterruptReason = null;
            InstanceId = 0;
            TriggerId = 0;
            Plan = default;
            Executor = null;
            OwnsExecutor = false;
            GlobalContext = null;
            CreatedAtMs = 0f;
            ActionDelegate = null;
            ConditionDelegate = null;
            BoundArgs = null;
        }
    }
}

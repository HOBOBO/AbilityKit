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
    public enum EActionInstanceState : byte
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
    public sealed class ActionInstance
    {
        /// <summary>实例唯一ID</summary>
        public int InstanceId { get; }

        /// <summary>关联的触发器ID</summary>
        public int TriggerId { get; }

        /// <summary>Action 调用计划</summary>
        public ActionCallPlan Plan { get; }

        /// <summary>当前状态</summary>
        public EActionInstanceState State { get; internal set; }

        /// <summary>是否活跃</summary>
        public bool IsActive => State is EActionInstanceState.Registered
                                or EActionInstanceState.WaitingDelay
                                or EActionInstanceState.WaitingCondition
                                or EActionInstanceState.WaitingSignal
                                or EActionInstanceState.WaitingQueue
                                or EActionInstanceState.Executing;

        /// <summary>已执行次数</summary>
        public int ExecutionCount { get; internal set; }

        /// <summary>总消耗时间（毫秒）</summary>
        public float ElapsedMs { get; internal set; }

        /// <summary>上次执行时间（从启动算起）</summary>
        public float LastExecuteMs { get; internal set; }

        /// <summary>是否可以中断</summary>
        public bool CanBeInterrupted => Plan.CanBeInterrupted;

        /// <summary>中断原因</summary>
        public string InterruptReason { get; internal set; }

        /// <summary>执行器</summary>
        public IActionExecutor Executor { get; }

        /// <summary>Action 委托（延迟解析）</summary>
        internal Action<object, ITriggerDispatcherContext> ActionDelegate { get; set; }

        /// <summary>条件委托（可选）</summary>
        internal TriggerPredicate<object> ConditionDelegate { get; set; }

        /// <summary>参数对象（延迟绑定）</summary>
        internal object BoundArgs { get; set; }

        /// <summary>全局上下文（从 Trigger 传递）</summary>
        public object GlobalContext { get; }

        /// <summary>创建时间戳</summary>
        public float CreatedAtMs { get; }

        private float _delayStartMs;
        private bool _hasDelayStart;

        internal ActionInstance(
            int instanceId,
            int triggerId,
            ActionCallPlan plan,
            IActionExecutor executor,
            object globalContext)
        {
            InstanceId = instanceId;
            TriggerId = triggerId;
            Plan = plan;
            Executor = executor ?? throw new ArgumentNullException(nameof(executor));
            GlobalContext = globalContext;
            State = EActionInstanceState.Registered;
            ElapsedMs = 0;
            LastExecuteMs = 0;
            ExecutionCount = 0;
            CreatedAtMs = 0; // 将由 Scheduler 设置
            _hasDelayStart = false;
        }

        /// <summary>
        /// 每帧更新（由 ActionScheduler 调用）
        /// </summary>
        /// <param name="deltaTimeMs">帧间隔（毫秒）</param>
        /// <param name="ctx">执行上下文</param>
        /// <returns>执行结果，如果已完成/中断则返回非 Continue</returns>
        public ExecutionResult Update(float deltaTimeMs, ActionExecutionContext ctx)
        {
            if (State is EActionInstanceState.Completed or EActionInstanceState.Interrupted or EActionInstanceState.Failed)
                return ExecutionResult.Success(0);

            ElapsedMs += deltaTimeMs;

            // 阶段1: 延迟等待
            if (Plan.ScheduleMode == Config.EActionScheduleMode.Delayed && !_hasDelayStart)
            {
                if (State == EActionInstanceState.Registered)
                {
                    _delayStartMs = 0;
                    _hasDelayStart = true;
                }

                if (ElapsedMs < Plan.ScheduleParam)
                {
                    State = EActionInstanceState.WaitingDelay;
                    return ExecutionResult.None;
                }

                // 延迟完成，重置时间并进入执行阶段
                ElapsedMs = 0;
                LastExecuteMs = 0;
                State = EActionInstanceState.Executing;
            }

            // 阶段2: 条件检查（如果需要）
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

            // 阶段3: 根据调度模式执行
            var result = ExecuteInternal(ctx);

            // 检查是否完成
            if (result.IsFailed)
            {
                State = EActionInstanceState.Failed;
            }
            else if (ShouldTerminate())
            {
                State = EActionInstanceState.Completed;
            }

            return result;
        }

        private ExecutionResult ExecuteInternal(ActionExecutionContext ctx)
        {
            // 检查执行条件（ExecutionPolicy）
            if (!CanExecuteByPolicy(ctx))
            {
                return ExecutionResult.Skipped("Policy check failed");
            }

            // 通过执行器执行
            if (Executor.TryExecute(ctx, out var result))
            {
                ExecutionCount++;
                LastExecuteMs = ElapsedMs;

                if (Plan.ExecutionPolicy == Config.EActionExecutionPolicy.WithRollback && result.IsFailed)
                {
                    // TODO: 触发回滚逻辑
                }
            }

            return result;
        }

        private bool CanExecuteByPolicy(ActionExecutionContext ctx)
        {
            return Plan.ExecutionPolicy switch
            {
                Config.EActionExecutionPolicy.Conditional => ConditionDelegate?.Invoke(BoundArgs, ctx.DispatcherContext) ?? true,
                Config.EActionExecutionPolicy.Queued => !IsQueued(),
                Config.EActionExecutionPolicy.Parallel => true, // 并行总是允许
                Config.EActionExecutionPolicy.Deferred => ElapsedMs > 0, // 至少等待一帧
                _ => true
            };
        }

        private bool IsQueued()
        {
            // 简单检查：如果 Executor 是队列执行器且已被标记
            return Executor is QueuedActionExecutor queued && queued.QueuePriority > 0;
        }

        private bool ShouldTerminate()
        {
            switch (Plan.ScheduleMode)
            {
                case Config.EActionScheduleMode.Immediate:
                    return true;

                case Config.EActionScheduleMode.Delayed:
                    return ExecutionCount >= 1;

                case Config.EActionScheduleMode.Periodic:
                    if (Plan.MaxExecutions > 0 && ExecutionCount >= Plan.MaxExecutions)
                        return true;
                    break;

                case Config.EActionScheduleMode.Continuous:
                    // Continuous 由外部控制终止
                    break;

                case Config.EActionScheduleMode.Timeline:
                    // TODO: Timeline 实现
                    break;
            }

            return false;
        }

        /// <summary>
        /// 请求中断
        /// </summary>
        public void RequestInterrupt(string reason)
        {
            if (!CanBeInterrupted) return;
            State = EActionInstanceState.Interrupted;
            InterruptReason = reason;
            Executor.Cancel(reason);
        }

        /// <summary>
        /// 重置（用于重用）
        /// </summary>
        public void Reset()
        {
            State = EActionInstanceState.Registered;
            ElapsedMs = 0;
            LastExecuteMs = 0;
            ExecutionCount = 0;
            InterruptReason = null;
            _hasDelayStart = false;
        }
    }
}

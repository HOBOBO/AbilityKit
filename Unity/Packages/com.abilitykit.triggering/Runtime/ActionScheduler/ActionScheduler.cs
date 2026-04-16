using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Executable;
using AbilityKit.Triggering.Runtime.Config;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Dispatcher;

namespace AbilityKit.Triggering.Runtime.ActionScheduler
{
    /// <summary>
    /// Action 调度器
    /// Trigger 激活时创建，负责管理该 Trigger 下所有 Action 的生命周期和执行
    /// </summary>
    public sealed class ActionScheduler
    {
        private readonly List<ActionInstance> _actions = new();
        private readonly Dictionary<int, ActionInstance> _instancesById = new();
        private int _nextInstanceId;
        private bool _isActive = true;

        public int ActionCount => _actions.Count;
        public int ActiveCount { get; private set; }
        public bool IsActive => _isActive;

        /// <summary>
        /// 创建调度器（指定Trigger ID）
        /// </summary>
        public ActionScheduler(int triggerId)
        {
            TriggerId = triggerId;
        }

        public int TriggerId { get; }

        /// <summary>
        /// 注册一个 Action（Trigger 激活时调用）
        /// </summary>
        public ActionInstance Register(ActionCallPlan plan, Action<object, ITriggerDispatcherContext> actionDelegate, TriggerPredicate<object> conditionDelegate, object boundArgs, IActionExecutor executor)
        {
            if (!_isActive) throw new InvalidOperationException("ActionScheduler is not active");

            var instance = new ActionInstance(
                instanceId: _nextInstanceId++,
                triggerId: TriggerId,
                plan: plan,
                executor: executor ?? new DefaultActionExecutor(actionDelegate),
                globalContext: boundArgs
            )
            {
                ActionDelegate = actionDelegate,
                ConditionDelegate = conditionDelegate,
                BoundArgs = boundArgs
            };

            _actions.Add(instance);
            _instancesById[instance.InstanceId] = instance;
            ActiveCount++;

            return instance;
        }

        /// <summary>
        /// 批量注册 Actions（Trigger 激活时调用）
        /// </summary>
        public void RegisterRange(ActionCallPlan[] plans, Action<object, ITriggerDispatcherContext>[] actionDelegates, TriggerPredicate<object>[] conditionDelegates, object boundArgs)
        {
            for (int i = 0; i < plans.Length; i++)
            {
                var executor = new DefaultActionExecutor(actionDelegates[i]);
                Register(plans[i], actionDelegates[i], conditionDelegates?[i], boundArgs, executor);
            }
        }

        /// <summary>
        /// 每帧更新（由 ActionSchedulerManager 调用）
        /// </summary>
        /// <param name="deltaTimeMs">帧间隔（毫秒）</param>
        /// <param name="ctx">执行上下文</param>
        public void Update(float deltaTimeMs, ActionExecutionContext ctx)
        {
            if (!_isActive) return;

            for (int i = _actions.Count - 1; i >= 0; i--)
            {
                var action = _actions[i];

                if (!action.IsActive)
                {
                    // 清理已完成/中断的 Action
                    _actions.RemoveAt(i);
                    _instancesById.Remove(action.InstanceId);
                    ActiveCount--;
                    continue;
                }

                try
                {
                    var result = action.Update(deltaTimeMs, ctx);

                    // 检查是否需要循环（周期性行为）
                    if (result.IsSuccess && action.Plan.ScheduleMode == EActionScheduleMode.Periodic)
                    {
                        // Periodic 已经在内部分配了多次执行，这里不需要额外处理
                    }
                }
                catch (Exception ex)
                {
                    action.State = EActionInstanceState.Failed;
                    // TODO: 错误日志
                }
            }
        }

        /// <summary>
        /// 获取实例
        /// </summary>
        public ActionInstance GetInstance(int instanceId)
        {
            _instancesById.TryGetValue(instanceId, out var instance);
            return instance;
        }

        /// <summary>
        /// 获取所有实例
        /// </summary>
        public IReadOnlyList<ActionInstance> GetAllInstances() => _actions;

        /// <summary>
        /// 中断所有 Action
        /// </summary>
        public void InterruptAll(string reason)
        {
            foreach (var action in _actions)
            {
                if (action.CanBeInterrupted)
                {
                    action.RequestInterrupt(reason);
                }
            }
        }

        /// <summary>
        /// 暂停所有 Action
        /// </summary>
        public void PauseAll()
        {
            _isActive = false;
            foreach (var action in _actions)
            {
                if (action.State == EActionInstanceState.Executing)
                {
                    action.State = EActionInstanceState.WaitingDelay; // 临时状态
                }
            }
        }

        /// <summary>
        /// 恢复所有 Action
        /// </summary>
        public void ResumeAll()
        {
            _isActive = true;
            foreach (var action in _actions)
            {
                if (action.State == EActionInstanceState.WaitingDelay && action.Plan.ScheduleMode == EActionScheduleMode.Continuous)
                {
                    action.State = EActionInstanceState.Executing;
                }
            }
        }

        /// <summary>
        /// 销毁调度器
        /// </summary>
        public void Dispose()
        {
            _isActive = false;
            _actions.Clear();
            _instancesById.Clear();
        }
    }

    /// <summary>
    /// 默认 Action 执行器（直接调用委托）
    /// </summary>
    internal sealed class DefaultActionExecutor : ActionExecutorBase
    {
        private readonly Action<object, ITriggerDispatcherContext> _action;

        public DefaultActionExecutor(Action<object, ITriggerDispatcherContext> action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        protected override ExecutionResult ExecuteCore(ActionExecutionContext ctx)
        {
            try
            {
                _action(ctx.Instance.BoundArgs, ctx.DispatcherContext);
                return ExecutionResult.Success();
            }
            catch (Exception ex)
            {
                return ExecutionResult.Failed($"Action execution error: {ex.Message}");
            }
        }
    }
}

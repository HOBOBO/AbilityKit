using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using AbilityKit.Triggering.Runtime.Executable;
using AbilityKit.Triggering.Runtime.Plan;
using AbilityKit.Triggering.Runtime.Dispatcher;

namespace AbilityKit.Triggering.Runtime.ActionScheduler
{
    /// <summary>
    /// 管理一个扁平动作分组的延迟、周期、条件等待与中断清理。
    /// 不管理业务时钟或执行树节点。
    /// </summary>
    internal sealed class ActionScheduler : IFlatActionScheduleGroup
    {
        private readonly ActionSchedulerInstances _instances = new();
        private readonly ActionSchedulerPools _pools;
        private object _planOwner;
        private int _nextInstanceId;
        private bool _isDisposed;
        private bool _isUpdating;
        private bool _isReplacing;
        private bool _isDisposing;
        private float _elapsedMs;

        /// <summary>
        /// 当前仍在分组中的实例数，包括尚未清理的终态实例。
        /// </summary>
        public int ActionCount => _instances.Count;

        public float ElapsedMs => _elapsedMs;

        /// <summary>
        /// 创建指定触发器的动作调度器；可选对象池由宿主持有。
        /// </summary>
        public ActionScheduler(int triggerId, ActionSchedulerPools pools = null)
        {
            TriggerId = triggerId;
            _pools = pools;
        }

        public int TriggerId { get; }

        /// <summary>
        /// 使用默认执行器策略登记计划动作，不向核心返回可变实例。
        /// </summary>
        void IFlatActionScheduleGroup.RegisterOrReplace(int planIndex, in ActionCallPlan plan,
            Action<object, ITriggerDispatcherContext> action, TriggerPredicate<object> condition, object boundArgs)
        {
            RegisterPlannedOrReplace(planIndex, in plan, action, condition, boundArgs);
        }

        /// <summary>
        /// 计划入口只能替换同一计划实例的槽位，不能覆盖另一计划实例的分组。
        /// 所有权随分组移除释放，不随事件退订释放。
        /// </summary>
        internal void ClaimPlanOwnership(object owner)
        {
            EnsureStructureChangeAllowed();
            EnsureNotDisposed();
            if (_planOwner == null)
                _planOwner = owner;
            else if (!ReferenceEquals(_planOwner, owner))
                throw new InvalidOperationException($"TriggerId={TriggerId} 的扁平动作分组已由另一计划实例持有；请使用不同的 TriggerId，或先清理旧分组。");
        }

        /// <summary>
        /// 注册单个 Action 实例；通常由触发器激活时调用。
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// 本分组正在推进、替换槽位、销毁或已销毁。
        /// </exception>
        public ActionInstance Register(
            ActionCallPlan plan,
            Action<object, ITriggerDispatcherContext> actionDelegate,
            TriggerPredicate<object> conditionDelegate,
            object boundArgs,
            IActionExecutor executor,
            bool ownsExecutor = false)
        {
            EnsureStructureChangeAllowed();
            EnsureNotDisposed();
            var instance = PrepareInstance(in plan, actionDelegate, conditionDelegate, boundArgs, executor, ownsExecutor, usePlanPolicy: false);
            _instances.Attach(instance);
            return instance;
        }

        /// <summary>
        /// 按计划索引注册或替换 Action，确保同一触发器的同一计划槽位只保留一个活跃实例。
        /// </summary>
        /// <remarks>
        /// 新实例准备失败时保留旧实例。新实例登记后才中断和回收旧实例；旧实例清理抛出异常时，不撤销新实例登记。
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// 本分组正在推进、替换槽位、销毁或已销毁。
        /// </exception>
        public ActionInstance RegisterOrReplace(
            int planIndex,
            ActionCallPlan plan,
            Action<object, ITriggerDispatcherContext> actionDelegate,
            TriggerPredicate<object> conditionDelegate,
            object boundArgs,
            IActionExecutor executor,
            bool ownsExecutor = false)
        {
            return ReplaceSlot(planIndex, in plan, actionDelegate, conditionDelegate, boundArgs, executor, ownsExecutor, usePlanPolicy: false);
        }

        /// <summary>
        /// 按计划执行策略创建执行器，并在实例准备完成后登记或替换槽位。
        /// </summary>
        internal ActionInstance RegisterPlannedOrReplace(
            int planIndex,
            in ActionCallPlan plan,
            Action<object, ITriggerDispatcherContext> actionDelegate,
            TriggerPredicate<object> conditionDelegate,
            object boundArgs)
        {
            return ReplaceSlot(planIndex, in plan, actionDelegate, conditionDelegate, boundArgs, null, false, usePlanPolicy: true);
        }

        private ActionInstance ReplaceSlot(
            int planIndex,
            in ActionCallPlan plan,
            Action<object, ITriggerDispatcherContext> actionDelegate,
            TriggerPredicate<object> conditionDelegate,
            object boundArgs,
            IActionExecutor executor,
            bool ownsExecutor,
            bool usePlanPolicy)
        {
            EnsureStructureChangeAllowed();
            if (planIndex < 0) throw new ArgumentOutOfRangeException(nameof(planIndex));
            EnsureNotDisposed();

            _isReplacing = true;
            try
            {
                var instance = PrepareInstance(in plan, actionDelegate, conditionDelegate, boundArgs, executor, ownsExecutor, usePlanPolicy);
                var existing = _instances.Replace(planIndex, instance);

                if (existing != null)
                {
                    try
                    {
                        existing.RequestInterrupt($"被计划索引 {planIndex} 的新实例替换。");
                    }
                    finally
                    {
                        ReleaseInstance(existing);
                    }
                }

                return instance;
            }
            finally
            {
                _isReplacing = false;
            }
        }

        private ActionInstance PrepareInstance(
            in ActionCallPlan plan,
            Action<object, ITriggerDispatcherContext> actionDelegate,
            TriggerPredicate<object> conditionDelegate,
            object boundArgs,
            IActionExecutor executor,
            bool ownsExecutor,
            bool usePlanPolicy)
        {
            var createExecutor = executor == null;
            if (createExecutor)
            {
                if (actionDelegate == null) throw new ArgumentNullException(nameof(actionDelegate));
                executor = ActionSchedulerFactory.CreateExecutor(_pools, in plan, actionDelegate, usePlanPolicy, out ownsExecutor);
            }

            try
            {
                var instanceId = _nextInstanceId++;
                var instance = ActionSchedulerFactory.CreateInstance(_pools, instanceId, TriggerId, in plan, executor, boundArgs, _elapsedMs, ownsExecutor);

                instance.ActionDelegate = actionDelegate;
                instance.ConditionDelegate = conditionDelegate;
                instance.BoundArgs = boundArgs;
                return instance;
            }
            catch
            {
                if (createExecutor && ownsExecutor)
                {
                    _pools.ReleaseActionExecutor(executor);
                }

                throw;
            }
        }

        private void EnsureNotDisposed()
        {
            if (_isDisposed) throw new InvalidOperationException("ActionScheduler 已销毁，无法登记动作。");
        }

        /// <summary>
        /// 由管理器推进本分组；已销毁的分组不再执行动作。
        /// </summary>
        /// <param name="deltaTimeMs">帧间隔，单位毫秒。</param>
        /// <param name="ctx">当前调度执行上下文。</param>
        /// <exception cref="InvalidOperationException">
        /// 本分组正在推进、替换槽位或销毁。
        /// </exception>
        public void Update(float deltaTimeMs, ActionExecutionContext ctx)
        {
            EnsureStructureChangeAllowed();
            if (_isDisposed) return;

            _isUpdating = true;
            try
            {
                AdvanceActions(deltaTimeMs, ctx);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void AdvanceActions(float deltaTimeMs, ActionExecutionContext ctx)
        {
            _elapsedMs += Math.Max(0f, deltaTimeMs);

            for (int index = _instances.Count - 1; index >= 0; index--)
            {
                var action = _instances[index];

                if (!action.IsActive)
                {
                    _instances.DetachAt(index);
                    ReleaseInstance(action);
                    continue;
                }

                try
                {
                    var actionCtx = new ActionExecutionContext(
                        instance: action,
                        globalContext: ctx.GlobalContext,
                        dispatcherContext: ctx.DispatcherContext,
                        control: ctx.Control);

                    action.Update(deltaTimeMs, actionCtx);
                }
                catch (Exception ex)
                {
                    MarkActionFailed(action, ex);
                }
            }
        }

        /// <summary>
        /// 推进、替换或销毁期间拒绝重入推进或变更本分组的实例结构。
        /// </summary>
        internal void EnsureStructureChangeAllowed()
        {
            if (_isUpdating || _isReplacing || _isDisposing)
            {
                throw new InvalidOperationException($"ActionScheduler（TriggerId={TriggerId}）正在推进、替换槽位或销毁，不能重入推进或修改实例结构。");
            }
        }

        private void MarkActionFailed(ActionInstance action, Exception executionException)
        {
            var executionReason = executionException.Message;
            action.State = EActionInstanceState.Failed;
            action.InterruptReason = executionReason;

            try
            {
                action.Executor.Cancel($"Action 调度更新异常：{executionReason}");
            }
            catch (Exception cleanupException)
            {
                action.InterruptReason = $"{executionReason}\nAction 失败清理异常：{cleanupException.Message}";
            }
        }

        private void ReleaseInstance(ActionInstance action)
        {
            if (_pools != null)
            {
                _pools.ReleaseActionInstance(action);
            }
        }

        /// <summary>
        /// 销毁调度器，取消当前活跃且可中断的动作，并清空所有实例索引。
        /// </summary>
        /// <remarks>
        /// 清理失败不阻止其余实例清理；完成后重新抛出单个异常，或将多个异常聚合报告。
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// 本分组正在推进、替换槽位或销毁。
        /// </exception>
        public void Dispose()
        {
            EnsureStructureChangeAllowed();
            _isDisposed = true;
            _isDisposing = true;
            List<Exception> failures = null;
            try
            {
                for (int index = _instances.Count - 1; index >= 0; index--)
                {
                    var action = _instances[index];
                    _instances.DetachAt(index);
                    DisposeAction(action, ref failures);
                }
            }
            finally
            {
                _isDisposing = false;
            }

            ThrowCleanupFailures(failures);
        }

        private void DisposeAction(ActionInstance action, ref List<Exception> failures)
        {
            try
            {
                if (action.IsActive)
                {
                    action.RequestInterrupt("ActionScheduler 销毁。");
                }
            }
            catch (Exception exception)
            {
                (failures ??= new List<Exception>()).Add(exception);
            }

            try
            {
                ReleaseInstance(action);
            }
            catch (Exception exception)
            {
                (failures ??= new List<Exception>()).Add(exception);
            }
        }

        /// <summary>
        /// 销毁结束后报告收集的异常，单个异常保留原始调用栈。
        /// </summary>
        internal static void ThrowCleanupFailures(List<Exception> failures)
        {
            if (failures == null) return;
            if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
            throw new AggregateException(failures);
        }
    }
}

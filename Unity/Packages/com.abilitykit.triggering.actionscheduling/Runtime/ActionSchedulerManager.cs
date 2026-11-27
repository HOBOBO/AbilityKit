using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Dispatcher;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Triggering.Runtime.ActionScheduler
{
    /// <summary>
    /// 管理当前宿主按触发器 ID 分组的扁平动作实例，由宿主串行推进。
    /// 不驱动执行树或业务定时规则，也不提供帧快照恢复。
    /// </summary>
    public sealed class ActionSchedulerManager : IFlatActionScheduler
    {
        private readonly Dictionary<int, ActionScheduler> _schedulersByTriggerId = new();
        private readonly List<ActionScheduler> _allSchedulers = new();
        private readonly ActionSchedulerPools _pools;
        private bool _isUpdating;
        private bool _isRemovingSchedulers;

        /// <summary>
        /// 创建空管理器；新增子调度器使用传入的可选对象池，池仍由宿主持有。
        /// </summary>
        public ActionSchedulerManager(ActionSchedulerPools pools = null)
        {
            _pools = pools;
        }

        /// <summary>
        /// 已登记的子调度器数量。
        /// </summary>
        public int SchedulerCount => _allSchedulers.Count;

        /// <summary>
        /// 将现有分组作为扁平计划登记入口，不改变各模式的运行约定。
        /// </summary>
        IFlatActionScheduleGroup IFlatActionScheduler.BindPlan(int triggerId, object owner, ReadOnlySpan<ActionCallPlan> actions)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            var scheduler = GetOrCreateScheduler(triggerId);
            scheduler.ClaimPlanOwnership(owner);
            return scheduler;
        }

        /// <summary>
        /// 当前由本管理器的子调度器持有的 Action 实例数，包括等待、暂停和尚未回收的终态实例。
        /// 每次读取时遍历子调度器，不依赖上一次更新的统计结果。
        /// </summary>
        public int TotalActionCount
        {
            get
            {
                var total = 0;
                foreach (var scheduler in _allSchedulers)
                {
                    total += scheduler.ActionCount;
                }

                return total;
            }
        }

        /// <summary>
        /// 使用宿主传入的毫秒增量，从最后登记的子调度器向前推进并移除已空的分组。
        /// 非空控制对象由本次更新共享；否则为每个子调度器创建独立控制对象。
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// 本管理器正在推进或移除分组，或有子调度器正在推进、替换槽位或销毁。
        /// </exception>
        internal void Update(float deltaTimeMs, ITriggerDispatcherContext context, ExecutionControl control)
        {
            EnsureAllSchedulersIdle();
            _isUpdating = true;
            try
            {
                AdvanceSchedulers(deltaTimeMs, context, control);
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void AdvanceSchedulers(float deltaTimeMs, ITriggerDispatcherContext context, ExecutionControl control)
        {
            for (int i = _allSchedulers.Count - 1; i >= 0; i--)
            {
                var scheduler = _allSchedulers[i];
                var ctx = new ActionExecutionContext(
                    instance: null,
                    globalContext: context?.Context,
                    dispatcherContext: context,
                    control: control ?? new ExecutionControl());

                scheduler.Update(deltaTimeMs, ctx);

                if (scheduler.ActionCount == 0)
                {
                    _allSchedulers.RemoveAt(i);
                    _schedulersByTriggerId.Remove(scheduler.TriggerId);
                }
            }
        }

        /// <summary>
        /// 按触发器 ID 获取已登记分组；未登记时创建并接管其生命周期。
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// 分组尚未登记，且本管理器正在推进或移除分组。
        /// </exception>
        internal ActionScheduler GetOrCreateScheduler(int triggerId)
        {
            if (_schedulersByTriggerId.TryGetValue(triggerId, out var scheduler))
            {
                return scheduler;
            }

            EnsureStructureChangeAllowed();
            scheduler = new ActionScheduler(triggerId, _pools);
            _schedulersByTriggerId[triggerId] = scheduler;
            _allSchedulers.Add(scheduler);
            return scheduler;
        }

        /// <summary>
        /// 查询触发器 ID 对应的分组；不存在时返回空引用。
        /// </summary>
        internal ActionScheduler GetScheduler(int triggerId)
        {
            _schedulersByTriggerId.TryGetValue(triggerId, out var scheduler);
            return scheduler;
        }

        /// <summary>
        /// 释放并移除指定触发器的待执行动作分组；不存在时返回否。
        /// 此操作不会解除规则的事件订阅。
        /// </summary>
        /// <remarks>
        /// 分组通过变更检查后，即使释放报告异常，也会移除其索引。
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// 本管理器正在推进或移除分组，或目标分组正在推进、替换槽位或销毁。
        /// </exception>
        public bool RemoveScheduler(int triggerId)
        {
            EnsureStructureChangeAllowed();
            if (!_schedulersByTriggerId.TryGetValue(triggerId, out var scheduler)) return false;
            scheduler.EnsureStructureChangeAllowed();
            _isRemovingSchedulers = true;
            try
            {
                scheduler.Dispose();
                return true;
            }
            finally
            {
                _schedulersByTriggerId.Remove(triggerId);
                _allSchedulers.Remove(scheduler);
                _isRemovingSchedulers = false;
            }
        }

        /// <summary>
        /// 由宿主串行传入毫秒增量，推进现有分组并移除空分组。
        /// 推进上下文不替换计划动作登记时捕获的上下文；没有推进上下文时可传空。
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// 本管理器正在推进或移除分组，或有子调度器正在推进、替换槽位或销毁。
        /// </exception>
        public void Update(float deltaTimeMs, ITriggerDispatcherContext context)
        {
            Update(deltaTimeMs, context, null);
        }

        /// <summary>
        /// 释放当前全部分组并清空索引；不解除规则订阅，也不释放宿主提供的池。
        /// </summary>
        /// <remarks>
        /// 单个分组清理失败不阻止其他分组清理；清空索引后再报告收集到的异常。
        /// </remarks>
        /// <exception cref="InvalidOperationException">
        /// 本管理器正在推进或移除分组，或有子调度器正在推进、替换槽位或销毁；拒绝时不释放分组。
        /// </exception>
        public void Clear()
        {
            EnsureAllSchedulersIdle();
            _isRemovingSchedulers = true;
            List<Exception> failures = null;
            try
            {
                foreach (var scheduler in _allSchedulers)
                {
                    try
                    {
                        scheduler.Dispose();
                    }
                    catch (Exception exception)
                    {
                        (failures ??= new List<Exception>()).Add(exception);
                    }
                }
            }
            finally
            {
                _allSchedulers.Clear();
                _schedulersByTriggerId.Clear();
                _isRemovingSchedulers = false;
            }

            ActionScheduler.ThrowCleanupFailures(failures);
        }

        private void EnsureAllSchedulersIdle()
        {
            EnsureStructureChangeAllowed();
            /*
             * 子调度器也可直接推进；先检查全部分组，再执行会影响多个分组的操作。
             */
            foreach (var scheduler in _allSchedulers)
            {
                scheduler.EnsureStructureChangeAllowed();
            }
        }

        private void EnsureStructureChangeAllowed()
        {
            if (_isUpdating || _isRemovingSchedulers)
            {
                throw new InvalidOperationException("ActionSchedulerManager 正在推进或移除分组，不能重入推进或修改分组结构。");
            }
        }
    }
}

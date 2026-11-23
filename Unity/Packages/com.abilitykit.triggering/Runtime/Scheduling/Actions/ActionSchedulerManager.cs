using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Dispatcher;

namespace AbilityKit.Triggering.Runtime.ActionScheduler
{
    /// <summary>
    /// 管理当前宿主按触发器 ID 分组的扁平动作实例，由宿主串行推进。
    /// 不驱动执行树或业务定时规则，也不提供帧快照恢复。
    /// </summary>
    public sealed class ActionSchedulerManager
    {
        private readonly Dictionary<int, ActionScheduler> _schedulersByTriggerId = new();
        private readonly List<ActionScheduler> _allSchedulers = new();
        private readonly AbilityKit.Triggering.Runtime.Pooling.TriggeringRuntimePools _pools;

        /// <summary>
        /// 创建空管理器；新增子调度器使用传入的可选对象池。
        /// </summary>
        public ActionSchedulerManager(AbilityKit.Triggering.Runtime.Pooling.TriggeringRuntimePools pools = null)
        {
            _pools = pools;
        }

        /// <summary>
        /// 已登记的子调度器数量。
        /// </summary>
        public int SchedulerCount => _allSchedulers.Count;

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
        /// 最近一次更新汇总的活动动作数量；即时持有量请读取 <see cref="TotalActionCount"/>。
        /// </summary>
        public int ActiveActionCount { get; private set; }

        /// <summary>
        /// 使用宿主传入的毫秒增量，从最后登记的子调度器向前推进并移除已空的分组。
        /// 非空控制对象由本次更新共享；否则为每个子调度器创建独立控制对象。
        /// </summary>
        public void Update(float deltaTimeMs, ITriggerDispatcherContext context, ExecutionControl control)
        {
            ActiveActionCount = 0;

            for (int i = _allSchedulers.Count - 1; i >= 0; i--)
            {
                var scheduler = _allSchedulers[i];
                var ctx = new ActionExecutionContext(
                    instance: null,
                    globalContext: context?.Context,
                    dispatcherContext: context,
                    control: control ?? new ExecutionControl());

                scheduler.Update(deltaTimeMs, ctx);
                ActiveActionCount += scheduler.ActiveCount;

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
        public ActionScheduler GetOrCreateScheduler(int triggerId)
        {
            if (_schedulersByTriggerId.TryGetValue(triggerId, out var scheduler))
            {
                return scheduler;
            }

            scheduler = new ActionScheduler(triggerId, _pools);
            _schedulersByTriggerId[triggerId] = scheduler;
            _allSchedulers.Add(scheduler);
            return scheduler;
        }

        /// <summary>
        /// 查询触发器 ID 对应的分组；不存在时返回空引用。
        /// </summary>
        public ActionScheduler GetScheduler(int triggerId)
        {
            _schedulersByTriggerId.TryGetValue(triggerId, out var scheduler);
            return scheduler;
        }

        /// <summary>
        /// 释放并移除指定分组；不存在时返回否，不刷新上次活动量统计。
        /// </summary>
        public bool RemoveScheduler(int triggerId)
        {
            if (_schedulersByTriggerId.TryGetValue(triggerId, out var scheduler))
            {
                scheduler.Dispose();
                _schedulersByTriggerId.Remove(triggerId);
                _allSchedulers.Remove(scheduler);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 使用宿主传入的毫秒增量，为每个子调度器提供独立控制对象。
        /// </summary>
        public void Update(float deltaTimeMs, ITriggerDispatcherContext context)
        {
            Update(deltaTimeMs, context, null);
        }

        /// <summary>
        /// 向指定分组中的可中断动作提出中断请求；不存在时不执行任何操作。
        /// </summary>
        public void InterruptTriggerActions(int triggerId, string reason)
        {
            if (_schedulersByTriggerId.TryGetValue(triggerId, out var scheduler))
            {
                scheduler.InterruptAll(reason);
            }
        }

        /// <summary>
        /// 向当前各分组的可中断动作提出中断请求，不直接释放分组。
        /// </summary>
        public void InterruptAll(string reason)
        {
            foreach (var scheduler in _allSchedulers)
            {
                scheduler.InterruptAll(reason);
            }
        }

        /// <summary>
        /// 暂停当前登记的子调度器，不影响以后创建的分组。
        /// </summary>
        public void PauseAll()
        {
            foreach (var scheduler in _allSchedulers)
            {
                scheduler.PauseAll();
            }
        }

        /// <summary>
        /// 恢复当前登记的子调度器，由各分组处理动作状态。
        /// </summary>
        public void ResumeAll()
        {
            foreach (var scheduler in _allSchedulers)
            {
                scheduler.ResumeAll();
            }
        }

        /// <summary>
        /// 释放当前全部分组，清空索引并重置活动量统计。
        /// </summary>
        public void Clear()
        {
            foreach (var scheduler in _allSchedulers)
            {
                scheduler.Dispose();
            }
            _allSchedulers.Clear();
            _schedulersByTriggerId.Clear();
            ActiveActionCount = 0;
        }
    }
}

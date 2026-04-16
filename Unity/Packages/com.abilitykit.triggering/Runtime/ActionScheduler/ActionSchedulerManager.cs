using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Executable;
using AbilityKit.Triggering.Runtime.Dispatcher;

namespace AbilityKit.Triggering.Runtime.ActionScheduler
{
    /// <summary>
    /// Trigger 级别同步原语
    /// 用于 Action 之间的协调
    /// </summary>

    /// <summary>
    /// 等待句柄（信号机制）
    /// 支持 AutoReset（自动重置）和 ManualReset（手动重置）
    /// </summary>
    public sealed class TriggerWaitHandle
    {
        private readonly Queue<Action> _waitingActions = new();
        private bool _isSignaled;
        private readonly bool _autoReset;

        public bool IsSignaled => _isSignaled;

        public TriggerWaitHandle(bool autoReset = true)
        {
            _autoReset = autoReset;
            _isSignaled = false;
        }

        /// <summary>
        /// 等待信号（阻塞当前 Action）
        /// </summary>
        public void WaitOne()
        {
            if (_isSignaled) return;

            // 将当前 Action 加入等待队列
            // 实际实现会在 ActionScheduler.Update 中处理
            throw new NotImplementedException("Wait 应在 ActionExecutionContext 中通过协程/状态机实现");
        }

        /// <summary>
        /// 设置信号（唤醒等待的 Action）
        /// </summary>
        public void Set()
        {
            _isSignaled = true;
            // 唤醒所有等待的 Action
            lock (_waitingActions)
            {
                foreach (var continuation in _waitingActions)
                {
                    continuation?.Invoke();
                }
                _waitingActions.Clear();
            }

            if (_autoReset)
            {
                _isSignaled = false;
            }
        }

        /// <summary>
        /// 重置信号（ManualReset 专用）
        /// </summary>
        public void Reset()
        {
            _isSignaled = false;
        }
    }

    /// <summary>
    /// 触发器互斥锁
    /// 用于保护共享资源的临界区
    /// </summary>
    public sealed class TriggerMutex : IDisposable
    {
        private readonly string _mutexId;
        private readonly object _lockObj = new();
        private int _ownerInstanceId = -1;
        private bool _isHeld;

        public string MutexId => _mutexId;
        public bool IsHeld => _isHeld;
        public int OwnerInstanceId => _ownerInstanceId;

        public TriggerMutex(string mutexId)
        {
            _mutexId = mutexId ?? throw new ArgumentNullException(nameof(mutexId));
        }

        /// <summary>
        /// 进入临界区
        /// </summary>
        /// <returns>是否成功获取锁</returns>
        public bool Enter(int instanceId)
        {
            lock (_lockObj)
            {
                if (_isHeld)
                {
                    // 当前已被占用
                    return false;
                }

                _isHeld = true;
                _ownerInstanceId = instanceId;
                return true;
            }
        }

        /// <summary>
        /// 离开临界区
        /// </summary>
        public void Leave(int instanceId)
        {
            lock (_lockObj)
            {
                if (_isHeld && _ownerInstanceId == instanceId)
                {
                    _isHeld = false;
                    _ownerInstanceId = -1;
                }
            }
        }

        public void Dispose()
        {
            Leave(_ownerInstanceId);
        }
    }

    /// <summary>
    /// ActionScheduler 全局管理器
    /// 负责全局 Action 调度器的注册、更新和生命周期管理
    /// </summary>
    public sealed class ActionSchedulerManager
    {
        private readonly Dictionary<int, ActionScheduler> _schedulersByTriggerId = new();
        private readonly List<ActionScheduler> _allSchedulers = new();
        private readonly List<ActionInstance> _allActions = new();

        /// <summary>
        /// 总调度器数量
        /// </summary>
        public int SchedulerCount => _allSchedulers.Count;

        /// <summary>
        /// 总 Action 数量
        /// </summary>
        public int TotalActionCount => _allActions.Count;

        /// <summary>
        /// 活跃的 Action 数量
        /// </summary>
        public int ActiveActionCount { get; private set; }

        /// <summary>
        /// 创建或获取 Trigger 对应的 ActionScheduler
        /// </summary>
        public ActionScheduler GetOrCreateScheduler(int triggerId)
        {
            if (_schedulersByTriggerId.TryGetValue(triggerId, out var scheduler))
            {
                return scheduler;
            }

            scheduler = new ActionScheduler(triggerId);
            _schedulersByTriggerId[triggerId] = scheduler;
            _allSchedulers.Add(scheduler);
            return scheduler;
        }

        /// <summary>
        /// 获取指定 Trigger 的 ActionScheduler
        /// </summary>
        public ActionScheduler GetScheduler(int triggerId)
        {
            _schedulersByTriggerId.TryGetValue(triggerId, out var scheduler);
            return scheduler;
        }

        /// <summary>
        /// 移除并销毁 Trigger 对应的 ActionScheduler
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
        /// 全局更新（每帧调用）
        /// </summary>
        public void Update(float deltaTimeMs, ITriggerDispatcherContext context)
        {
            ActiveActionCount = 0;

            // 遍历所有调度器
            for (int i = _allSchedulers.Count - 1; i >= 0; i--)
            {
                var scheduler = _allSchedulers[i];
                var ctx = new ActionExecutionContext(
                    instance: null,
                    globalContext: context?.Context,
                    dispatcherContext: context,
                    control: new ExecutionControl()
                );

                scheduler.Update(deltaTimeMs, ctx);
                ActiveActionCount += scheduler.ActiveCount;

                // 如果调度器不再活跃（无 Action），则清理
                if (scheduler.ActionCount == 0)
                {
                    _allSchedulers.RemoveAt(i);
                    _schedulersByTriggerId.Remove(scheduler.TriggerId);
                }
            }
        }

        /// <summary>
        /// 中断指定 Trigger 的所有 Action
        /// </summary>
        public void InterruptTriggerActions(int triggerId, string reason)
        {
            if (_schedulersByTriggerId.TryGetValue(triggerId, out var scheduler))
            {
                scheduler.InterruptAll(reason);
            }
        }

        /// <summary>
        /// 中断所有 Action
        /// </summary>
        public void InterruptAll(string reason)
        {
            foreach (var scheduler in _allSchedulers)
            {
                scheduler.InterruptAll(reason);
            }
        }

        /// <summary>
        /// 暂停所有 Action
        /// </summary>
        public void PauseAll()
        {
            foreach (var scheduler in _allSchedulers)
            {
                scheduler.PauseAll();
            }
        }

        /// <summary>
        /// 恢复所有 Action
        /// </summary>
        public void ResumeAll()
        {
            foreach (var scheduler in _allSchedulers)
            {
                scheduler.ResumeAll();
            }
        }

        /// <summary>
        /// 清理所有调度器
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

using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Schedule.Behavior;
using AbilityKit.Triggering.Runtime.Schedule.Data;

namespace AbilityKit.Triggering.Runtime.Schedule
{
    /// <summary>
    /// 简单调度管理器
    /// 实现 IScheduleManager 接口，提供基础的调度能力
    ///
    /// 使用场景：
    /// - Buff 系统：不需要 Trigger 分组管理
    /// - 子弹系统：不需要 Trigger 分组管理
    /// - AOE 系统：不需要 Trigger 分组管理
    ///
    /// 如果需要分组管理能力，请使用 GroupedScheduleManager
    /// </summary>
    public sealed class SimpleScheduleManager : IScheduleManager
    {
        // ===== 存储 =====

        private readonly List<ScheduleItemData> _items = new();
        private readonly List<IScheduleEffect> _effects = new();
        private readonly List<IScheduleEffectCallbacks> _callbacks = new();

        // ===== 调度策略 =====

        private readonly IScheduleStrategy _strategy;

        // ===== 统计 =====

        public int ActiveCount { get; private set; }
        public int TotalCount => _items.Count;

        // ===== 构造函数 =====

        /// <summary>
        /// 创建调度管理器
        /// </summary>
        /// <param name="strategy">调度策略（可选，默认使用 DefaultScheduleStrategy）</param>
        public SimpleScheduleManager(IScheduleStrategy strategy = null)
        {
            _strategy = strategy ?? new DefaultScheduleStrategy();
        }

        // ===== IScheduleManager 实现 =====

        #region 注册

        public ScheduleHandle Register(ScheduleRegisterRequest request, IScheduleEffect effect)
        {
            if (effect == null)
                throw new ArgumentNullException(nameof(effect));

            int index = _items.Count;
            int handleId = index + 1; // Start from 1

            // 创建调度项
            var item = new ScheduleItemData
            {
                Handle = new ScheduleHandle(handleId, index),
                BusinessId = request.BusinessId,
                TriggerId = request.BusinessId,
                State = EScheduleItemState.Registered,
                Mode = request.Mode,
                IntervalMs = request.IntervalMs,
                DelayMs = request.DelayMs,
                MaxExecutions = request.MaxExecutions,
                Speed = request.Speed,
                ElapsedMs = 0,
                LastExecuteMs = 0,
                ExecutionCount = 0,
                CanBeInterrupted = request.CanBeInterrupted,
                InterruptReason = null
            };

            _items.Add(item);
            _effects.Add(effect);
            _callbacks.Add(effect as IScheduleEffectCallbacks);

            ActiveCount++;

            return item.Handle;
        }

        public ScheduleHandle RegisterPeriodic(float intervalMs, int maxExecutions, int businessId, IScheduleEffect effect)
        {
            var request = ScheduleRegisterRequest.Periodic(
                intervalMs: intervalMs,
                maxExecutions: maxExecutions,
                businessId: businessId
            );
            return Register(request, effect);
        }

        public ScheduleHandle RegisterContinuous(float intervalMs, int businessId, IScheduleEffect effect)
        {
            var request = ScheduleRegisterRequest.Continuous(
                intervalMs: intervalMs,
                businessId: businessId
            );
            return Register(request, effect);
        }

        public ScheduleHandle RegisterDelayed(float delayMs, int businessId, IScheduleEffect effect)
        {
            var request = ScheduleRegisterRequest.Delayed(
                delayMs: delayMs,
                businessId: businessId
            );
            return Register(request, effect);
        }

        #endregion

        #region 查询

        public bool TryGetItem(ScheduleHandle handle, out ScheduleItemData item)
        {
            if (handle.IsValid && handle.Index >= 0 && handle.Index < _items.Count)
            {
                item = _items[handle.Index];
                return true;
            }
            item = default;
            return false;
        }

        public List<ScheduleItemData> FindByBusinessId(int businessId)
        {
            var result = new List<ScheduleItemData>();
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item.BusinessId == businessId && !item.IsTerminated)
                {
                    result.Add(item);
                }
            }
            return result;
        }

        public List<ScheduleHandle> FindHandlesByBusinessId(int businessId)
        {
            var result = new List<ScheduleHandle>();
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item.BusinessId == businessId && !item.IsTerminated)
                {
                    result.Add(item.Handle);
                }
            }
            return result;
        }

        #endregion

        #region 修改

        public bool Modify(ScheduleHandle handle, in ScheduleModifyRequest request)
        {
            if (!handle.IsValid || handle.Index < 0 || handle.Index >= _items.Count)
                return false;

            var item = _items[handle.Index];
            if (item.IsTerminated)
                return false;

            if (request.HasSpeed) item.Speed = request.Speed;
            if (request.HasIntervalMs) item.IntervalMs = request.IntervalMs;
            if (request.HasMaxExecutions) item.MaxExecutions = request.MaxExecutions;
            if (request.HasDelayMs)
            {
                item.DelayMs = request.DelayMs;
                item.ElapsedMs = 0;
            }
            _items[handle.Index] = item;

            return true;
        }

        public bool SetSpeed(ScheduleHandle handle, float speed)
        {
            if (!handle.IsValid || handle.Index < 0 || handle.Index >= _items.Count)
                return false;

            var item = _items[handle.Index];
            if (item.IsTerminated)
                return false;

            item.Speed = speed;
            _items[handle.Index] = item;
            return true;
        }

        public bool SetInterval(ScheduleHandle handle, float intervalMs)
        {
            if (!handle.IsValid || handle.Index < 0 || handle.Index >= _items.Count)
                return false;

            var item = _items[handle.Index];
            if (item.IsTerminated)
                return false;

            item.IntervalMs = intervalMs;
            _items[handle.Index] = item;
            return true;
        }

        public bool AddExecutions(ScheduleHandle handle, int count)
        {
            if (!handle.IsValid || handle.Index < 0 || handle.Index >= _items.Count)
                return false;

            var item = _items[handle.Index];
            if (item.IsTerminated)
                return false;

            if (item.MaxExecutions < 0)
                return true; // 无限执行次数，无需修改

            item.MaxExecutions += count;
            _items[handle.Index] = item;
            return true;
        }

        #endregion

        #region 控制

        public bool Pause(ScheduleHandle handle)
        {
            if (!handle.IsValid || handle.Index < 0 || handle.Index >= _items.Count)
                return false;

            var item = _items[handle.Index];
            if (item.IsTerminated)
                return false;

            item.State = EScheduleItemState.Paused;
            _items[handle.Index] = item;
            return true;
        }

        public bool Resume(ScheduleHandle handle)
        {
            if (!handle.IsValid || handle.Index < 0 || handle.Index >= _items.Count)
                return false;

            var item = _items[handle.Index];
            if (item.IsTerminated || item.State != EScheduleItemState.Paused)
                return false;

            item.State = item.ElapsedMs >= item.DelayMs
                ? EScheduleItemState.Running
                : EScheduleItemState.WaitingDelay;
            _items[handle.Index] = item;
            return true;
        }

        public bool Interrupt(ScheduleHandle handle, string reason = null)
        {
            if (!handle.IsValid || handle.Index < 0 || handle.Index >= _items.Count)
                return false;

            var item = _items[handle.Index];
            if (item.IsTerminated || !item.CanBeInterrupted)
                return false;

            item.State = EScheduleItemState.Interrupted;
            item.InterruptReason = reason;
            _items[handle.Index] = item;

            // 调用回调
            var callback = _callbacks[handle.Index];
            var context = CreateContext(item);
            callback?.OnInterrupted(context, reason ?? "Unknown");

            ActiveCount--;
            return true;
        }

        public bool Cancel(ScheduleHandle handle)
        {
            if (!handle.IsValid || handle.Index < 0 || handle.Index >= _items.Count)
                return false;

            var item = _items[handle.Index];
            if (item.IsTerminated)
                return false;

            item.State = EScheduleItemState.Terminated;
            _items[handle.Index] = item;

            ActiveCount--;
            return true;
        }

        public void PauseAll()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (!item.IsTerminated && item.State != EScheduleItemState.Paused)
                {
                    item.State = EScheduleItemState.Paused;
                    _items[i] = item;
                }
            }
        }

        public void ResumeAll()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (!item.IsTerminated && item.State == EScheduleItemState.Paused)
                {
                    item.State = item.ElapsedMs >= item.DelayMs
                        ? EScheduleItemState.Running
                        : EScheduleItemState.WaitingDelay;
                    _items[i] = item;
                }
            }
        }

        public int InterruptAll(string reason = null)
        {
            int count = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (!item.IsTerminated && item.CanBeInterrupted)
                {
                    item.State = EScheduleItemState.Interrupted;
                    item.InterruptReason = reason;
                    _items[i] = item;

                    var callback = _callbacks[i];
                    var context = CreateContext(item);
                    callback?.OnInterrupted(context, reason ?? "Unknown");

                    count++;
                }
            }
            ActiveCount -= count;
            return count;
        }

        #endregion

        #region 更新

        public void Update(float deltaTimeMs)
        {
            var indicesToRemove = new List<int>();

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];

                // 跳过已终止的
                if (item.IsTerminated)
                {
                    indicesToRemove.Add(i);
                    continue;
                }

                // 使用策略更新
                var executor = new EffectExecutor(_effects[i]);
                bool shouldRemove = _strategy.OnUpdate(ref item, deltaTimeMs, executor);
                _items[i] = item;

                if (shouldRemove)
                {
                    item.State = item.State == EScheduleItemState.Interrupted
                        ? EScheduleItemState.Interrupted
                        : EScheduleItemState.Completed;
                    _items[i] = item;

                    // 调用完成回调
                    var callback = _callbacks[i];
                    if (callback != null)
                    {
                        var context = CreateContext(item);
                        callback.OnCompleted(context);
                    }

                    indicesToRemove.Add(i);
                }
            }

            // 清理终止的调度项
            CleanupItems(indicesToRemove);
        }

        #endregion

        #region 清理

        public void Clear()
        {
            _items.Clear();
            _effects.Clear();
            _callbacks.Clear();
            ActiveCount = 0;
        }

        #endregion

        // ===== 私有辅助方法 =====

        private void CleanupItems(List<int> indices)
        {
            if (indices.Count == 0)
                return;

            // 从后往前删除，避免索引变化
            for (int i = indices.Count - 1; i >= 0; i--)
            {
                int index = indices[i];
                _items.RemoveAt(index);
                _effects.RemoveAt(index);
                _callbacks.RemoveAt(index);
            }

            // 重新构建句柄索引映射
            RebuildHandleIndices();
        }

        private void RebuildHandleIndices()
        {
            // 由于删除后索引会变化，需要更新句柄
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                item.Handle = new ScheduleHandle(item.Handle.HandleId, i);
                _items[i] = item;
            }
        }

        private static ScheduleContext CreateContext(ScheduleItemData item)
        {
            return new ScheduleContext(
                instanceId: item.Handle.Index,
                businessId: item.BusinessId,
                deltaTimeMs: 0,
                elapsedMs: item.ElapsedMs,
                scaledDeltaMs: 0,
                intervalMs: item.IntervalMs,
                executionCount: item.ExecutionCount,
                maxExecutions: item.MaxExecutions,
                speed: item.Speed,
                interruptReason: item.InterruptReason
            );
        }
    }
}

using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Schedule.Behavior;
using AbilityKit.Triggering.Runtime.Schedule.Data;

namespace AbilityKit.Triggering.Runtime.Schedule
{
    /// <summary>
    /// 默认调度管理器
    /// IScheduleManager 的框架默认实现
    /// 使用策略模式处理状态机逻辑，支持自定义调度策略
    /// 
    /// 注意：此实现已被 TriggerScheduleManager 取代
    /// TriggerScheduleManager 提供了更好的 Trigger 分组管理和更完整的 API
    /// </summary>
    [Obsolete("请使用 TriggerScheduleManager 替代")]
    public sealed class DefaultScheduleManager : IScheduleManager
    {
        #region 字段

        private readonly List<ScheduleItemData> _items = new();
        private readonly List<IScheduleEffect> _effects = new();
        private readonly List<int> _toRemove = new();
        private readonly IScheduleStrategy _strategy;
        private readonly IScheduleExecutor _executor;

        private int _nextInstanceId = 1;
        private int _nextHandleId = 1;

        #endregion

        #region 构造

        /// <summary>
        /// 使用默认策略创建管理器
        /// </summary>
        public DefaultScheduleManager() : this(new DefaultScheduleStrategy())
        {
        }

        /// <summary>
        /// 使用指定策略创建管理器
        /// </summary>
        public DefaultScheduleManager(IScheduleStrategy strategy)
        {
            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
            _executor = new EffectExecutorAdapter(this);
        }

        #endregion

        #region 属性

        /// <summary>
        /// 活跃的调度项数量
        /// </summary>
        public int ActiveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _items.Count; i++)
                {
                    if (_items[i].IsActive)
                        count++;
                }
                return count;
            }
        }

        /// <summary>
        /// 总注册数量
        /// </summary>
        public int TotalCount => _items.Count;

        #endregion

        #region 注册

        /// <summary>
        /// 注册一个调度项
        /// </summary>
        public ScheduleHandle Register(ScheduleRegisterRequest request, IScheduleEffect effect)
        {
            int index = _items.Count;

            var item = new ScheduleItemData
            {
                Handle = new ScheduleHandle(_nextInstanceId++, index),
                BusinessId = request.BusinessId,
                TriggerId = request.TriggerId,
                State = EScheduleItemState.Registered,
                Mode = request.Mode,
                DelayMs = request.DelayMs,
                IntervalMs = request.IntervalMs,
                MaxExecutions = request.MaxExecutions,
                Speed = request.Speed,
                ElapsedMs = 0,
                LastExecuteMs = 0,
                ExecutionCount = 0,
                CanBeInterrupted = request.CanBeInterrupted
            };

            _items.Add(item);
            _effects.Add(effect);

            return new ScheduleHandle(_nextHandleId++, index);
        }

        /// <summary>
        /// 注册周期性调度
        /// </summary>
        public ScheduleHandle RegisterPeriodic(
            float intervalMs,
            int maxExecutions,
            int contextId,
            IScheduleEffect effect)
        {
            return Register(ScheduleRegisterRequest.Periodic(intervalMs, maxExecutions, 0, 1.0f, contextId), effect);
        }

        /// <summary>
        /// 注册延迟调度
        /// </summary>
        public ScheduleHandle RegisterDelayed(
            float delayMs,
            int contextId,
            IScheduleEffect effect)
        {
            return Register(ScheduleRegisterRequest.Delayed(delayMs, contextId), effect);
        }

        /// <summary>
        /// 注册持续调度（需要手动终止）
        /// </summary>
        public ScheduleHandle RegisterContinuous(
            float intervalMs,
            int contextId,
            IScheduleEffect effect)
        {
            return Register(ScheduleRegisterRequest.Continuous(intervalMs, true, -1, contextId), effect);
        }

        #endregion

        #region 查询

        /// <summary>
        /// 获取调度项数据
        /// </summary>
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

        /// <summary>
        /// 根据业务ID查找所有调度项索引
        /// </summary>
        public List<int> FindByBusinessIdIndex(int businessId)
        {
            var results = new List<int>();
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].BusinessId == businessId && _items[i].IsActive)
                    results.Add(i);
            }
            return results;
        }

        /// <summary>
        /// 根据业务对象ID查找所有调度项
        /// </summary>
        public List<ScheduleItemData> FindByBusinessId(int businessId)
        {
            var results = new List<ScheduleItemData>();
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].BusinessId == businessId && _items[i].IsActive)
                    results.Add(_items[i]);
            }
            return results;
        }

        /// <summary>
        /// 根据业务对象ID查找所有调度句柄
        /// </summary>
        public List<ScheduleHandle> FindHandlesByBusinessId(int businessId)
        {
            var results = new List<ScheduleHandle>();
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].BusinessId == businessId && _items[i].IsActive)
                    results.Add(_items[i].Handle);
            }
            return results;
        }

        #endregion

        #region 修改

        /// <summary>
        /// 修改调度项参数
        /// </summary>
        public bool Modify(ScheduleHandle handle, in ScheduleModifyRequest request)
        {
            if (!handle.IsValid || handle.Index < 0 || handle.Index >= _items.Count)
                return false;

            var item = _items[handle.Index];
            if (!item.IsActive) return false;

            if (request.HasIntervalMs)
                item.IntervalMs = request.IntervalMs;

            if (request.HasSpeed)
                item.Speed = request.Speed;

            if (request.HasMaxExecutions)
            {
                if (item.MaxExecutions < 0)
                    item.MaxExecutions = request.MaxExecutions;
                else
                    item.MaxExecutions += request.MaxExecutions;
            }

            _items[handle.Index] = item;
            return true;
        }

        /// <summary>
        /// 设置速度
        /// </summary>
        public bool SetSpeed(ScheduleHandle handle, float speed)
        {
            return Modify(handle, ScheduleModifyRequest.SetSpeed(speed));
        }

        /// <summary>
        /// 设置间隔
        /// </summary>
        public bool SetInterval(ScheduleHandle handle, float intervalMs)
        {
            return Modify(handle, ScheduleModifyRequest.SetInterval(intervalMs));
        }

        /// <summary>
        /// 延长执行次数
        /// </summary>
        public bool AddExecutions(ScheduleHandle handle, int count)
        {
            return Modify(handle, ScheduleModifyRequest.AddExecutions(count));
        }

        #endregion

        #region 控制

        /// <summary>
        /// 暂停调度项
        /// </summary>
        public bool Pause(ScheduleHandle handle)
        {
            if (!handle.IsValid || handle.Index < 0 || handle.Index >= _items.Count)
                return false;

            var item = _items[handle.Index];
            if (!item.IsActive || item.IsPaused) return false;

            item.State = EScheduleItemState.Paused;
            _items[handle.Index] = item;
            return true;
        }

        /// <summary>
        /// 恢复调度项
        /// </summary>
        public bool Resume(ScheduleHandle handle)
        {
            if (!handle.IsValid || handle.Index < 0 || handle.Index >= _items.Count)
                return false;

            var item = _items[handle.Index];
            if (item.State != EScheduleItemState.Paused) return false;

            item.State = item.ElapsedMs > 0 ? EScheduleItemState.Running : EScheduleItemState.Registered;
            _items[handle.Index] = item;
            return true;
        }

        /// <summary>
        /// 中断调度项
        /// </summary>
        public bool Interrupt(ScheduleHandle handle, string reason = null)
        {
            if (!handle.IsValid || handle.Index < 0 || handle.Index >= _items.Count)
                return false;

            var item = _items[handle.Index];
            if (!item.CanBeInterrupted) return false;

            item.State = EScheduleItemState.Interrupted;
            item.InterruptReason = reason ?? "Interrupted";
            _items[handle.Index] = item;
            return true;
        }

        /// <summary>
        /// 取消调度项（立即移除）
        /// </summary>
        public bool Cancel(ScheduleHandle handle)
        {
            if (!handle.IsValid || handle.Index < 0 || handle.Index >= _items.Count)
                return false;

            var item = _items[handle.Index];
            item.State = EScheduleItemState.Terminated;
            _items[handle.Index] = item;
            _toRemove.Add(handle.Index);
            return true;
        }

        /// <summary>
        /// 暂停所有
        /// </summary>
        public void PauseAll()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item.IsActive)
                {
                    item.State = EScheduleItemState.Paused;
                    _items[i] = item;
                }
            }
        }

        /// <summary>
        /// 恢复所有
        /// </summary>
        public void ResumeAll()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item.State == EScheduleItemState.Paused)
                {
                    item.State = item.ElapsedMs > 0 ? EScheduleItemState.Running : EScheduleItemState.Registered;
                    _items[i] = item;
                }
            }
        }

        /// <summary>
        /// 中断所有可中断的
        /// </summary>
        public int InterruptAll(string reason = null)
        {
            int count = 0;
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item.CanBeInterrupted && item.IsActive)
                {
                    item.State = EScheduleItemState.Interrupted;
                    item.InterruptReason = reason ?? "Interrupted";
                    _items[i] = item;
                    count++;
                }
            }
            return count;
        }

        #endregion

        #region 更新

        /// <summary>
        /// 每帧更新
        /// </summary>
        public void Update(float deltaTimeMs)
        {
            _toRemove.Clear();

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];

                // 跳过已终止的
                if (item.IsTerminated)
                {
                    _toRemove.Add(i);
                    continue;
                }

                // 跳过暂停的（策略内部处理）
                if (item.IsPaused)
                    continue;

                // 使用策略更新
                var executor = new EffectExecutor(_effects[i]);
                bool shouldRemove = _strategy.OnUpdate(ref item, deltaTimeMs, executor);
                _items[i] = item;

                if (shouldRemove)
                {
                    _toRemove.Add(i);
                }
            }

            // 清理已完成的项
            CleanupItems();
        }

        private void CleanupItems()
        {
            if (_toRemove.Count == 0) return;

            // 从后往前移除，保持索引正确
            for (int i = _toRemove.Count - 1; i >= 0; i--)
            {
                int removeIndex = _toRemove[i];
                _items.RemoveAt(removeIndex);
                _effects.RemoveAt(removeIndex);
            }
        }

        #endregion

        #region 清理

        /// <summary>
        /// 清空所有调度项
        /// </summary>
        public void Clear()
        {
            _items.Clear();
            _effects.Clear();
            _toRemove.Clear();
        }

        #endregion

        #region 内部

        /// <summary>
        /// 内部使用的调度项数量
        /// </summary>
        internal int ItemCount => _items.Count;

        /// <summary>
        /// 内部获取 Effect
        /// </summary>
        internal IScheduleEffect GetEffect(int index)
        {
            return index >= 0 && index < _effects.Count ? _effects[index] : null;
        }

        #endregion
    }
}
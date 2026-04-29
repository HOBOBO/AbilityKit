using System;
using System.Collections.Generic;
using AbilityKit.Triggering.Runtime.Schedule.Behavior;
using AbilityKit.Triggering.Runtime.Schedule.Data;

namespace AbilityKit.Triggering.Runtime.Schedule
{
    /// <summary>
    /// 分组调度管理器
    /// 实现 IGroupedScheduleManager 接口，提供分组管理能力
    /// 
    /// 使用场景：
    /// - Trigger 系统：按 TriggerId 分组管理调度项
    /// - 需要批量控制一组相关调度项
    /// 
    /// 如果不需要分组管理能力，请使用 SimpleScheduleManager
    /// 
    /// 性能优化：
    /// - 使用 LinkedList 存储分组索引，删除操作 O(1)
    /// - 使用字典缓存节点引用，避免遍历查找
    /// </summary>
    public sealed class GroupedScheduleManager : IGroupedScheduleManager
    {
        // ===== 存储 =====

        private readonly List<ScheduleItemData> _items = new();
        private readonly List<IScheduleEffect> _effects = new();
        private readonly List<IScheduleEffectCallbacks> _callbacks = new();
        private readonly List<LinkedListNode<int>> _itemNodes = new();

        // ===== 分组索引（LinkedList 优化） =====

        private readonly Dictionary<int, LinkedList<int>> _itemsByGroup = new();
        private readonly HashSet<int> _activeGroups = new();

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
        public GroupedScheduleManager(IScheduleStrategy strategy = null)
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

            // 创建调度项
            var item = new ScheduleItemData
            {
                Handle = new ScheduleHandle(index + 1, index),
                BusinessId = request.BusinessId,
                TriggerId = request.TriggerId,
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

            // 添加到分组（LinkedList 优化）
            int groupId = request.TriggerId;
            if (!_itemsByGroup.TryGetValue(groupId, out var linkedList))
            {
                linkedList = new LinkedList<int>();
                _itemsByGroup[groupId] = linkedList;
            }
            var node = linkedList.AddLast(index);
            _itemNodes.Add(node);

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
                if (_items[i].BusinessId == businessId && !_items[i].IsTerminated)
                {
                    result.Add(_items[i]);
                }
            }
            return result;
        }

        public List<ScheduleHandle> FindHandlesByBusinessId(int businessId)
        {
            var result = new List<ScheduleHandle>();
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].BusinessId == businessId && !_items[i].IsTerminated)
                {
                    result.Add(_items[i].Handle);
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
                return true;

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
            _itemsByGroup.Clear();
            _activeGroups.Clear();
            ActiveCount = 0;
        }

        #endregion

        // ===== IGroupedScheduleManager 实现 =====

        #region 分组属性

        public IReadOnlyList<int> GetActiveGroupIds()
        {
            return new List<int>(_activeGroups);
        }

        public int GetItemCountByGroup(int groupId)
        {
            if (_itemsByGroup.TryGetValue(groupId, out var linkedList))
            {
                int count = 0;
                foreach (var index in linkedList)
                {
                    if (index < _items.Count && !_items[index].IsTerminated)
                        count++;
                }
                return count;
            }
            return 0;
        }

        #endregion

        #region 分组注册

        public ScheduleHandle RegisterForGroup(int groupId, ScheduleRegisterRequest request, IScheduleEffect effect)
        {
            request.TriggerId = groupId;
            return Register(request, effect);
        }

        public ScheduleHandle RegisterPeriodicForGroup(int groupId, float intervalMs, int maxExecutions, int businessId, IScheduleEffect effect)
        {
            var request = ScheduleRegisterRequest.Periodic(
                intervalMs: intervalMs,
                maxExecutions: maxExecutions,
                businessId: businessId,
                triggerId: groupId
            );
            return Register(request, effect);
        }

        public ScheduleHandle RegisterContinuousForGroup(int groupId, float intervalMs, int businessId, IScheduleEffect effect)
        {
            var request = ScheduleRegisterRequest.Continuous(
                intervalMs: intervalMs,
                businessId: businessId,
                triggerId: groupId
            );
            return Register(request, effect);
        }

        #endregion

        #region 分组查询

        public List<ScheduleItemData> FindByGroupId(int groupId)
        {
            var result = new List<ScheduleItemData>();
            if (_itemsByGroup.TryGetValue(groupId, out var linkedList))
            {
                foreach (var index in linkedList)
                {
                    if (index < _items.Count && !_items[index].IsTerminated)
                    {
                        result.Add(_items[index]);
                    }
                }
            }
            return result;
        }

        public List<ScheduleHandle> FindHandlesByGroupId(int groupId)
        {
            var result = new List<ScheduleHandle>();
            if (_itemsByGroup.TryGetValue(groupId, out var linkedList))
            {
                foreach (var index in linkedList)
                {
                    if (index < _items.Count && !_items[index].IsTerminated)
                    {
                        result.Add(_items[index].Handle);
                    }
                }
            }
            return result;
        }

        #endregion

        #region 分组控制

        public void PauseGroup(int groupId)
        {
            if (_itemsByGroup.TryGetValue(groupId, out var linkedList))
            {
                var indices = new List<int>(linkedList);
                foreach (var index in indices)
                {
                    if (index < 0 || index >= _items.Count) continue;
                    var item = _items[index];
                    if (!item.IsTerminated && item.State != EScheduleItemState.Paused)
                    {
                        _items[index] = UpdateItemState(item, EScheduleItemState.Paused);
                    }
                }
            }
        }

        public void ResumeGroup(int groupId)
        {
            if (_itemsByGroup.TryGetValue(groupId, out var linkedList))
            {
                var indices = new List<int>(linkedList);
                foreach (var index in indices)
                {
                    if (index < 0 || index >= _items.Count) continue;
                    var item = _items[index];
                    if (!item.IsTerminated && item.State == EScheduleItemState.Paused)
                    {
                        var newState = item.ElapsedMs >= item.DelayMs
                            ? EScheduleItemState.Running
                            : EScheduleItemState.WaitingDelay;
                        _items[index] = UpdateItemState(item, newState);
                    }
                }
            }
        }

        public int InterruptGroup(int groupId, string reason = null)
        {
            int count = 0;
            if (_itemsByGroup.TryGetValue(groupId, out var linkedList))
            {
                var indices = new List<int>(linkedList);
                foreach (var index in indices)
                {
                    if (index < 0 || index >= _items.Count) continue;
                    var item = _items[index];
                    if (!item.IsTerminated && item.CanBeInterrupted)
                    {
                        item.State = EScheduleItemState.Interrupted;
                        item.InterruptReason = reason;
                        _items[index] = item;

                        var callback = _callbacks[index];
                        if (callback != null)
                        {
                            var context = CreateContext(item);
                            callback.OnInterrupted(context, reason ?? "Unknown");
                        }
                        count++;
                    }
                }
            }
            ActiveCount -= count;
            return count;
        }

        public int RemoveGroup(int groupId)
        {
            int count = 0;
            if (_itemsByGroup.TryGetValue(groupId, out var linkedList))
            {
                var indices = new List<int>(linkedList);
                foreach (var index in indices)
                {
                    if (index < 0 || index >= _items.Count) continue;
                    var item = _items[index];
                    if (!item.IsTerminated)
                    {
                        _items[index] = UpdateItemState(item, EScheduleItemState.Terminated);
                        count++;
                    }
                }
                _itemsByGroup.Remove(groupId);
                _activeGroups.Remove(groupId);
            }
            ActiveCount -= count;
            return count;
        }

        #endregion

        #region 分组生命周期

        public void OnGroupActivated(int groupId)
        {
            _activeGroups.Add(groupId);
        }

        public void OnGroupDeactivated(int groupId)
        {
            // 默认：只更新活跃分组集合，不自动清理调度项
            // 如果需要自动清理，可以在子类中重写此方法
        }

        #endregion

        // ===== 私有辅助方法 =====

        private void CleanupItems(List<int> indices)
        {
            if (indices.Count == 0)
                return;

            // 标记要清理的项
            var indicesToCleanup = new HashSet<int>(indices);
            var groupsToRemove = new List<int>();

            // 遍历所有分组，移除对应的节点
            foreach (var kvp in _itemsByGroup)
            {
                int groupId = kvp.Key;
                var linkedList = kvp.Value;
                var node = linkedList.First;

                while (node != null)
                {
                    int itemIndex = node.Value;
                    if (indicesToCleanup.Contains(itemIndex))
                    {
                        var nextNode = node.Next;
                        linkedList.Remove(node);
                        node = nextNode;
                    }
                    else
                    {
                        node = node.Next;
                    }
                }

                // 如果分组空了，标记待删除
                if (linkedList.Count == 0)
                {
                    groupsToRemove.Add(groupId);
                }
            }

            // 移除空分组
            foreach (var groupId in groupsToRemove)
            {
                _itemsByGroup.Remove(groupId);
                _activeGroups.Remove(groupId);
            }

            // 从后往前重建列表（保留未删除的项）
            int writeIndex = _items.Count - 1;
            var indexMapping = new int[_items.Count]; // 旧索引 -> 新索引
            for (int i = 0; i < indexMapping.Length; i++)
            {
                indexMapping[i] = -1; // 初始化为 -1
            }

            // 从后往前遍历，收集需要保留的项
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                if (!indicesToCleanup.Contains(i))
                {
                    indexMapping[i] = writeIndex;
                    writeIndex--;
                }
            }

            // 重建列表
            RebuildItems(indicesToCleanup, indexMapping);

            ActiveCount -= indices.Count;
        }

        private void RebuildItems(HashSet<int> indicesToRemove, int[] indexMapping)
        {
            int newCount = _items.Count - indicesToRemove.Count;
            var newItems = new List<ScheduleItemData>(newCount);
            var newEffects = new List<IScheduleEffect>(newCount);
            var newCallbacks = new List<IScheduleEffectCallbacks>(newCount);

            for (int i = 0; i < _items.Count; i++)
            {
                if (!indicesToRemove.Contains(i))
                {
                    int newIndex = newItems.Count;
                    var item = _items[i];
                    item.Handle = new ScheduleHandle(item.Handle.HandleId, newIndex);
                    newItems.Add(item);
                    newEffects.Add(_effects[i]);
                    newCallbacks.Add(_callbacks[i]);
                }
            }

            _items.Clear();
            _effects.Clear();
            _callbacks.Clear();
            _items.AddRange(newItems);
            _effects.AddRange(newEffects);
            _callbacks.AddRange(newCallbacks);

            // 重建分组索引
            RebuildGroupIndices(indexMapping);
        }

        private void RebuildGroupIndices(int[] indexMapping)
        {
            foreach (var linkedList in _itemsByGroup.Values)
            {
                var node = linkedList.First;
                while (node != null)
                {
                    int oldIndex = node.Value;
                    int newIndex = indexMapping[oldIndex];
                    node.Value = newIndex;
                    node = node.Next;
                }
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

        private static ScheduleItemData UpdateItemState(ScheduleItemData item, EScheduleItemState newState)
        {
            item.State = newState;
            return item;
        }
    }
}

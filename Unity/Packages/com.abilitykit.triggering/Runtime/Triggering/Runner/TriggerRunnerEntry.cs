using System;
using System.Collections.Generic;

namespace AbilityKit.Triggering.Runtime
{
    /// <summary>
    /// 保存单次注册的触发器、排序键和有效状态；同一触发器的多条注册彼此独立。
    /// </summary>
    internal sealed class TriggerRunnerEntry<TArgs, TCtx>
    {
        public readonly int Phase;
        public readonly int Priority;
        public readonly long Order;
        public readonly ITrigger<TArgs, TCtx> Trigger;
        public bool IsActive { get; private set; } = true;

        public TriggerRunnerEntry(int phase, int priority, long order, ITrigger<TArgs, TCtx> trigger)
        {
            Phase = phase;
            Priority = priority;
            Order = order;
            Trigger = trigger;
        }

        public void Deactivate() => IsActive = false;
    }

    /// <summary>
    /// 维护单个事件的有序注册，供各次派发独立持有候选顺序。
    /// 仅在注册变更后重建缓存，稳定派发复用同一数组。
    /// </summary>
    internal sealed class TriggerRunnerEntryList<TArgs, TCtx>
    {
        private readonly List<TriggerRunnerEntry<TArgs, TCtx>> _entries = new List<TriggerRunnerEntry<TArgs, TCtx>>(4);
        private TriggerRunnerEntry<TArgs, TCtx>[] _snapshot;

        public int Count => _entries.Count;

        public TriggerRunnerEntry<TArgs, TCtx>[] Capture()
        {
            return _snapshot ?? (_snapshot = _entries.Count == 0 ? Array.Empty<TriggerRunnerEntry<TArgs, TCtx>>() : _entries.ToArray());
        }

        public void Remove(TriggerRunnerEntry<TArgs, TCtx> entry)
        {
            if (!_entries.Remove(entry)) return;

            entry.Deactivate();
            _snapshot = null;
        }

        public void InsertSorted(TriggerRunnerEntry<TArgs, TCtx> entry)
        {
            _snapshot = null;
            for (int index = 0; index < _entries.Count; index++)
            {
                var other = _entries[index];
                if (entry.Phase < other.Phase)
                {
                    _entries.Insert(index, entry);
                    return;
                }

                if (entry.Phase == other.Phase && entry.Priority > other.Priority)
                {
                    _entries.Insert(index, entry);
                    return;
                }

                if (entry.Phase == other.Phase && entry.Priority == other.Priority && entry.Order < other.Order)
                {
                    _entries.Insert(index, entry);
                    return;
                }
            }

            _entries.Add(entry);
        }
    }
}

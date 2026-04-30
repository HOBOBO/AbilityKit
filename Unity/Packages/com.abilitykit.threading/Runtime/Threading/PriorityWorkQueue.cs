using System;
using System.Collections.Generic;

namespace AbilityKit.Threading
{
    /// <summary>
    /// 任务优先级
    /// </summary>
    public enum WorkPriority
    {
        /// <summary>
        /// 低优先级 - 可延迟执行
        /// </summary>
        Low = 0,

        /// <summary>
        /// 普通优先级 - 默认
        /// </summary>
        Normal = 1,

        /// <summary>
        /// 高优先级 - 紧急任务
        /// </summary>
        High = 2,

        /// <summary>
        /// 最高优先级 - 必须立即执行
        /// </summary>
        Critical = 3
    }

    /// <summary>
    /// 带优先级的工作项
    /// </summary>
    public sealed class PrioritizedWork<T>
    {
        /// <summary>
        /// 优先级（数值越大优先级越高）
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// 工作项内容
        /// </summary>
        public T Item { get; }

        /// <summary>
        /// 入队时间戳
        /// </summary>
        public long Timestamp { get; }

        /// <summary>
        /// 序列号（用于同优先级 FIFO 排序）
        /// </summary>
        public long SequenceNumber { get; }

        public PrioritizedWork(T item, int priority, long timestamp, long sequenceNumber)
        {
            Item = item;
            Priority = priority;
            Timestamp = timestamp;
            SequenceNumber = sequenceNumber;
        }

        public static PrioritizedWork<T> Create(T item, WorkPriority priority, long timestamp, long sequenceNumber)
        {
            return new PrioritizedWork<T>(item, (int)priority, timestamp, sequenceNumber);
        }
    }

    /// <summary>
    /// 优先级工作队列
    /// 支持多优先级入队，高优先级任务优先出队
    /// 同优先级按 FIFO 顺序
    /// </summary>
    public sealed class PriorityWorkQueue<T>
    {
        private readonly object _lock = new();
        private readonly List<List<PrioritizedWork<T>>> _priorityBuckets;
        private long _sequenceNumber;
        private long _count;

        /// <summary>
        /// 元素数量
        /// </summary>
        public long Count
        {
            get
            {
                lock (_lock)
                {
                    return _count;
                }
            }
        }

        /// <summary>
        /// 是否为空
        /// </summary>
        public bool IsEmpty => _count == 0;

        /// <summary>
        /// 优先级级别数量
        /// </summary>
        public int PriorityLevelCount => _priorityBuckets.Count;

        /// <summary>
        /// 创建优先级队列
        /// </summary>
        /// <param name="priorityLevels">优先级级别数量（默认为 4，对应 WorkPriority 枚举）</param>
        public PriorityWorkQueue(int priorityLevels = 4)
        {
            _priorityBuckets = new List<List<PrioritizedWork<T>>>(priorityLevels);
            for (int i = 0; i < priorityLevels; i++)
            {
                _priorityBuckets.Add(new List<PrioritizedWork<T>>());
            }
        }

        /// <summary>
        /// 入队（使用 WorkPriority 枚举）
        /// </summary>
        public void Enqueue(T item, WorkPriority priority)
        {
            Enqueue(item, (int)priority);
        }

        /// <summary>
        /// 入队（使用自定义优先级数值）
        /// </summary>
        /// <param name="item">工作项</param>
        /// <param name="priority">优先级数值（数值越大优先级越高）</param>
        public void Enqueue(T item, int priority)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            lock (_lock)
            {
                EnsurePriorityBucket(priority);
                var work = new PrioritizedWork<T>(item, priority, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), _sequenceNumber++);
                _priorityBuckets[priority].Add(work);
                _count++;
            }
        }

        /// <summary>
        /// 批量入队
        /// </summary>
        public void EnqueueRange(IEnumerable<T> items, WorkPriority priority)
        {
            EnqueueRange(items, (int)priority);
        }

        /// <summary>
        /// 批量入队（自定义优先级）
        /// </summary>
        public void EnqueueRange(IEnumerable<T> items, int priority)
        {
            if (items == null)
                return;

            lock (_lock)
            {
                EnsurePriorityBucket(priority);
                var bucket = _priorityBuckets[priority];
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                foreach (var item in items)
                {
                    if (item != null)
                    {
                        var work = new PrioritizedWork<T>(item, priority, timestamp, _sequenceNumber++);
                        bucket.Add(work);
                        _count++;
                    }
                }
            }
        }

        /// <summary>
        /// 尝试出队（获取最高优先级项）
        /// </summary>
        public bool TryDequeue(out T item)
        {
            item = default;
            int foundIndex = -1;

            lock (_lock)
            {
                if (_count == 0)
                    return false;

                // 从高优先级到低优先级查找
                for (int i = _priorityBuckets.Count - 1; i >= 0; i--)
                {
                    var bucket = _priorityBuckets[i];
                    if (bucket.Count > 0)
                    {
                        // 在同优先级桶中按 FIFO 顺序查找
                        int bestIndex = -1;
                        long bestTimestamp = long.MaxValue;

                        for (int j = 0; j < bucket.Count; j++)
                        {
                            var work = bucket[j];
                            if (work.Timestamp < bestTimestamp)
                            {
                                bestTimestamp = work.Timestamp;
                                bestIndex = j;
                            }
                        }

                        if (bestIndex >= 0)
                        {
                            foundIndex = bestIndex;
                            var work = bucket[bestIndex];
                            item = work.Item;
                            bucket.RemoveAt(bestIndex);
                            _count--;
                            break;
                        }
                    }
                }
            }

            return foundIndex >= 0;
        }

        /// <summary>
        /// 尝试获取但不移除
        /// </summary>
        public bool TryPeek(out T item)
        {
            item = default;

            lock (_lock)
            {
                if (_count == 0)
                    return false;

                for (int i = _priorityBuckets.Count - 1; i >= 0; i--)
                {
                    if (_priorityBuckets[i].Count > 0)
                    {
                        item = _priorityBuckets[i][0].Item;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 清空队列
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                for (int i = 0; i < _priorityBuckets.Count; i++)
                {
                    _priorityBuckets[i].Clear();
                }
                _count = 0;
            }
        }

        /// <summary>
        /// 获取各优先级的数量
        /// </summary>
        public int[] GetPriorityCounts()
        {
            lock (_lock)
            {
                var counts = new int[_priorityBuckets.Count];
                for (int i = 0; i < _priorityBuckets.Count; i++)
                {
                    counts[i] = _priorityBuckets[i].Count;
                }
                return counts;
            }
        }

        private void EnsurePriorityBucket(int priority)
        {
            while (priority >= _priorityBuckets.Count)
            {
                _priorityBuckets.Add(new List<PrioritizedWork<T>>());
            }
        }
    }

    /// <summary>
    /// 优先级队列扩展
    /// </summary>
    public static class PriorityWorkQueueExtensions
    {
        /// <summary>
        /// 创建 Action 的优先级队列
        /// </summary>
        public static PriorityWorkQueue<Action> CreateActionQueue(int priorityLevels = 4)
        {
            return new PriorityWorkQueue<Action>(priorityLevels);
        }
    }
}

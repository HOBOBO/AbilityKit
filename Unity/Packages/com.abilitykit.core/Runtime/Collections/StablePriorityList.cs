using System;
using System.Collections;
using System.Collections.Generic;

namespace AbilityKit.Core.Collections
{
    /// <summary>
    /// 定义整数优先级的排序方向；同优先级仍按原始注册顺序排列。
    /// </summary>
    public enum PriorityDirection
    {
        /// <summary>
        /// 较小的优先级排在前面。
        /// </summary>
        Ascending = 0,

        /// <summary>
        /// 较大的优先级排在前面。
        /// </summary>
        Descending = 1,
    }

    /// <summary>
    /// 维护按优先级排序的小型注册列表，同优先级保持原始注册顺序。集合非线程安全，枚举及匹配回调期间不得修改集合。
    /// </summary>
    public sealed class StablePriorityList<T> : IReadOnlyList<T>
    {
        private readonly List<Entry> _entries;
        private readonly PriorityDirection _direction;
        private long _nextSequence;

        /// <summary>
        /// 创建指定排序方向的空列表；初始容量允许为零，不得为负数。
        /// </summary>
        /// <param name="direction">整数优先级的排序方向。</param>
        /// <param name="capacity">初始容量，允许为零，不得为负数。</param>
        public StablePriorityList(PriorityDirection direction = PriorityDirection.Ascending, int capacity = 0)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _direction = direction;
            _entries = new List<Entry>(capacity);
        }

        /// <summary>
        /// 获取当前注册项数量，包括重复元素。
        /// </summary>
        public int Count => _entries.Count;

        /// <summary>
        /// 按当前排序位置读取元素；索引从零开始，越界时抛出异常。
        /// </summary>
        /// <param name="index">从零开始的排序索引。</param>
        public T this[int index] => _entries[index].Item;

        /// <summary>
        /// 按优先级和注册顺序插入元素，允许重复；定位采用二分查找，插入仍可能搬移元素。
        /// </summary>
        /// <param name="item">要添加的元素，允许重复。</param>
        /// <param name="priority">用于排序的整数优先级。</param>
        public void Add(T item, int priority = 0)
        {
            var entry = new Entry(item, priority, _nextSequence++);
            _entries.Insert(FindInsertIndex(in entry), entry);
        }

        /// <summary>
        /// 按当前排序查找并删除首个匹配项；匹配回调在调用线程执行，异常直接传播，回调不得修改集合。
        /// </summary>
        /// <param name="match">匹配条件，不得为空或修改集合；回调异常直接传播。</param>
        /// <returns>找到并删除首个匹配项时返回 true，否则返回 false。</returns>
        public bool RemoveFirst(Predicate<T> match)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            for (int i = 0; i < _entries.Count; i++)
            {
                if (!match(_entries[i].Item)) continue;
                _entries.RemoveAt(i);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 更新当前排序中首个匹配项的优先级，保留该项最初的注册顺序；匹配回调异常直接传播，回调不得修改集合。
        /// </summary>
        /// <param name="match">匹配条件，不得为空或修改集合；回调异常直接传播。</param>
        /// <param name="priority">用于排序的整数优先级。</param>
        /// <returns>找到匹配项时返回 true，包括优先级未改变的情况；否则返回 false。</returns>
        public bool TryUpdatePriority(Predicate<T> match, int priority)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (!match(entry.Item)) continue;
                if (entry.Priority == priority) return true;

                _entries.RemoveAt(i);
                entry = new Entry(entry.Item, priority, entry.Sequence);
                _entries.Insert(FindInsertIndex(in entry), entry);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 移除全部注册项并重置注册序号，保留底层列表容量；不自动释放元素。
        /// </summary>
        public void Clear()
        {
            _entries.Clear();
            _nextSequence = 0;
        }

        /// <summary>
        /// 创建按当前优先级顺序访问元素的枚举器；枚举不是快照，期间不得修改集合。
        /// </summary>
        /// <returns>按排序访问元素的枚举器。</returns>
        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                yield return _entries[i].Item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private int FindInsertIndex(in Entry candidate)
        {
            var low = 0;
            var high = _entries.Count;
            while (low < high)
            {
                var middle = low + ((high - low) >> 1);
                var current = _entries[middle];
                if (Compare(in current, in candidate) <= 0)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return low;
        }

        private int Compare(in Entry left, in Entry right)
        {
            var byPriority = left.Priority.CompareTo(right.Priority);
            if (_direction == PriorityDirection.Descending) byPriority = -byPriority;
            return byPriority != 0 ? byPriority : left.Sequence.CompareTo(right.Sequence);
        }

        /// <summary>
        /// 保存元素、优先级和初始注册序号，使优先级更新后仍能稳定决胜。
        /// </summary>
        private readonly struct Entry
        {
            public Entry(T item, int priority, long sequence)
            {
                Item = item;
                Priority = priority;
                Sequence = sequence;
            }

            public T Item { get; }
            public int Priority { get; }
            public long Sequence { get; }
        }
    }
}

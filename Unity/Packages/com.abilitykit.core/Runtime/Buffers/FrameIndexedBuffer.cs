using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using AbilityKit.Core.Collections;

namespace AbilityKit.Core.Buffers
{
    /// <summary>
    /// 按完整整数帧键升序保留有限数量的值；重复键替换，超过容量时保留数值最大的帧键，不进行整数回绕比较。值按引用保留，不复制对象也不取得其释放责任。
    /// </summary>
    public interface IFrameIndexedBuffer<T> : IBufferCapacityControl
    {
        /// <summary>
        /// 获取当前保留的帧键数量。
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 按升序索引读取完整整数帧键；索引须在零到 Count 的左闭右开区间内。
        /// </summary>
        int GetFrameAt(int orderedIndex);

        /// <summary>
        /// 获取首个大于或等于指定帧键的升序位置；没有符合项时返回 Count。
        /// </summary>
        int LowerBound(int frame);

        /// <summary>
        /// 存储或替换帧值；满容量时仅保留数值最大的帧键，更旧的新键不会被保留，被替换或淘汰的值不自动释放。
        /// </summary>
        void Store(int frame, T value);

        /// <summary>
        /// 尝试读取指定帧值；缺失时返回 false 并输出默认值，命中时返回 true，保留的值本身仍可能为空引用。
        /// </summary>
        bool TryGet(int frame, [MaybeNullWhen(false)] out T value);

        /// <summary>
        /// 判断指定完整整数帧键是否仍被保留。
        /// </summary>
        bool Contains(int frame);

        /// <summary>
        /// 删除指定帧并报告是否存在；断开内部引用，不调用值的释放方法。
        /// </summary>
        bool Remove(int frame);

        /// <summary>
        /// 删除帧键严格小于边界的项，保留边界帧；仅断开内部引用，不自动释放值。
        /// </summary>
        void RemoveBefore(int frame);

        /// <summary>
        /// 删除帧键严格大于边界的项，保留边界帧；仅断开内部引用，不自动释放值。
        /// </summary>
        void RemoveAfter(int frame);

        /// <summary>
        /// 移除全部帧并断开内部引用，不自动释放值；保留当前容量。
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// 以字典和升序整数索引保存稀疏帧值，支持间隔较大或乱序到达的完整整数键。索引变更可能搬移元素，所有访问需要单一所有者或外部同步。
    /// </summary>
    public sealed class SparseFrameIndexedBuffer<T> : IFrameIndexedBuffer<T>
    {
        private readonly Dictionary<int, T> _values;
        private readonly SortedIntSet _frames;
        private int _capacity;

        /// <summary>
        /// 创建空的稀疏帧缓冲区；初始容量必须为正数。
        /// </summary>
        public SparseFrameIndexedBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _values = new Dictionary<int, T>(capacity);
            _frames = new SortedIntSet(capacity);
        }

        /// <summary>
        /// 获取当前保留项的数量。
        /// </summary>
        /// <inheritdoc />
        public int Count => _frames.Count;

        /// <summary>
        /// 获取当前生效的保留容量；逻辑容量不保证等于底层已分配空间。
        /// </summary>
        /// <inheritdoc />
        public int Capacity => _capacity;

        /// <summary>
        /// 按升序位置读取完整整数帧键；索引越界时抛出异常。
        /// </summary>
        /// <inheritdoc />
        public int GetFrameAt(int orderedIndex) => _frames[orderedIndex];

        /// <summary>
        /// 获取首个大于或等于指定帧键的位置；没有符合项时返回 Count。
        /// </summary>
        /// <inheritdoc />
        public int LowerBound(int frame) => _frames.LowerBound(frame);

        /// <summary>
        /// 存储或替换帧值，按整数键保留最新帧；满容量时忽略更旧的新键，不自动释放被移除的值。
        /// </summary>
        /// <inheritdoc />
        public void Store(int frame, T value)
        {
            if (_values.ContainsKey(frame))
            {
                _values[frame] = value;
                return;
            }

            if (_frames.Count == _capacity && frame < _frames[0]) return;

            _values.Add(frame, value);
            _frames.Add(frame);
            TrimToCapacity();
        }

        /// <summary>
        /// 读取指定帧值；缺失时返回 false 并输出默认值，命中时返回 true，保留的值本身仍可能为空引用。
        /// </summary>
        /// <inheritdoc />
        public bool TryGet(int frame, [MaybeNullWhen(false)] out T value) => _values.TryGetValue(frame, out value);

        /// <summary>
        /// 判断指定完整整数帧键是否仍被保留。
        /// </summary>
        /// <inheritdoc />
        public bool Contains(int frame) => _values.ContainsKey(frame);

        /// <summary>
        /// 删除指定帧并报告是否存在；仅断开内部引用，不自动释放值。
        /// </summary>
        /// <inheritdoc />
        public bool Remove(int frame)
        {
            if (!_values.Remove(frame)) return false;
            _frames.Remove(frame);
            return true;
        }

        /// <summary>
        /// 删除严格小于边界帧的项，保留边界帧；仅断开内部引用。
        /// </summary>
        /// <inheritdoc />
        public void RemoveBefore(int frame)
        {
            RemoveOrderedRange(0, _frames.LowerBound(frame));
        }

        /// <summary>
        /// 删除严格大于边界帧的项，保留边界帧；仅断开内部引用。
        /// </summary>
        /// <inheritdoc />
        public void RemoveAfter(int frame)
        {
            var firstRemoval = _frames.UpperBound(frame);
            RemoveOrderedRange(firstRemoval, _frames.Count - firstRemoval);
        }

        /// <summary>
        /// 清空全部保留项并断开内部引用，保留容量；不自动释放元素。
        /// </summary>
        /// <inheritdoc />
        public void Clear()
        {
            _values.Clear();
            _frames.Clear();
        }

        /// <summary>
        /// 尝试设置正数容量；非正数返回 false 且不改变状态，缩容时保留最新项及其原顺序。
        /// </summary>
        /// <inheritdoc />
        public bool TrySetCapacity(int capacity)
        {
            if (capacity <= 0) return false;
            if (capacity == _capacity) return true;
            _capacity = capacity;
            TrimToCapacity();
            return true;
        }

        private void TrimToCapacity()
        {
            RemoveOrderedRange(0, _frames.Count - _capacity);
        }

        private void RemoveOrderedRange(int index, int count)
        {
            if (count <= 0) return;
            for (var offset = 0; offset < count; offset++)
            {
                _values.Remove(_frames[index + offset]);
            }

            _frames.RemoveRange(index, count);
        }
    }

    /// <summary>
    /// 以环形数组保存升序帧值；查询采用二分查找，末尾写入和头部淘汰无需搬移，乱序插入和中间删除可能搬移元素。所有访问需要单一所有者或外部同步。
    /// </summary>
    public sealed class RingFrameIndexedBuffer<T> : IFrameIndexedBuffer<T>
    {
        private Entry[] _entries;
        private int _head;
        private int _count;

        /// <summary>
        /// 创建空的环形帧缓冲区；初始容量必须为正数。
        /// </summary>
        public RingFrameIndexedBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _entries = new Entry[capacity];
        }

        /// <summary>
        /// 获取当前保留项的数量。
        /// </summary>
        /// <inheritdoc />
        public int Count => _count;

        /// <summary>
        /// 获取当前生效的保留容量；逻辑容量不保证等于底层已分配空间。
        /// </summary>
        /// <inheritdoc />
        public int Capacity => _entries.Length;

        /// <summary>
        /// 按升序位置读取完整整数帧键；索引越界时抛出异常。
        /// </summary>
        /// <inheritdoc />
        public int GetFrameAt(int orderedIndex)
        {
            ValidateIndex(orderedIndex);
            return _entries[PhysicalIndex(orderedIndex)].Frame;
        }

        /// <summary>
        /// 获取首个大于或等于指定帧键的位置；没有符合项时返回 Count。
        /// </summary>
        /// <inheritdoc />
        public int LowerBound(int frame) => FindLowerBound(frame);

        /// <summary>
        /// 存储或替换帧值，按整数键保留最新帧；满容量时忽略更旧的新键，不自动释放被移除的值。
        /// </summary>
        /// <inheritdoc />
        public void Store(int frame, T value)
        {
            var orderedIndex = FindFrame(frame);
            if (orderedIndex >= 0)
            {
                _entries[PhysicalIndex(orderedIndex)] = new Entry(frame, value);
                return;
            }

            var insertionIndex = ~orderedIndex;
            if (_count == Capacity)
            {
                /*
                 * 满容量时只保留数值最大的帧键，更旧的新键会立即被淘汰。
                 * 直接忽略该插入，避免无意义地搬移整个环形缓冲区。
                 */
                if (insertionIndex == 0) return;
                RemoveFirst();
                insertionIndex--;
            }

            if (insertionIndex == _count)
            {
                var tail = PhysicalIndex(_count);
                _entries[tail] = new Entry(frame, value);
                _count++;
                return;
            }

            for (var index = _count; index > insertionIndex; index--)
            {
                CopySlot(index - 1, index);
            }

            var target = PhysicalIndex(insertionIndex);
            _entries[target] = new Entry(frame, value);
            _count++;
        }

        /// <summary>
        /// 读取指定帧值；缺失时返回 false 并输出默认值，命中时返回 true，保留的值本身仍可能为空引用。
        /// </summary>
        /// <inheritdoc />
        public bool TryGet(int frame, [MaybeNullWhen(false)] out T value)
        {
            var orderedIndex = FindFrame(frame);
            if (orderedIndex >= 0)
            {
                value = _entries[PhysicalIndex(orderedIndex)].Value;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// 判断指定完整整数帧键是否仍被保留。
        /// </summary>
        /// <inheritdoc />
        public bool Contains(int frame) => FindFrame(frame) >= 0;

        /// <summary>
        /// 删除指定帧并报告是否存在；仅断开内部引用，不自动释放值。
        /// </summary>
        /// <inheritdoc />
        public bool Remove(int frame)
        {
            var orderedIndex = FindFrame(frame);
            if (orderedIndex < 0) return false;

            if (orderedIndex == 0)
            {
                RemoveFirst();
                return true;
            }

            for (var index = orderedIndex; index < _count - 1; index++)
            {
                CopySlot(index + 1, index);
            }

            ClearSlot(_count - 1);
            _count--;
            return true;
        }

        /// <summary>
        /// 删除严格小于边界帧的项，保留边界帧；仅断开内部引用。
        /// </summary>
        /// <inheritdoc />
        public void RemoveBefore(int frame)
        {
            RemoveFirst(FindLowerBound(frame));
        }

        /// <summary>
        /// 删除严格大于边界帧的项，保留边界帧；仅断开内部引用。
        /// </summary>
        /// <inheritdoc />
        public void RemoveAfter(int frame)
        {
            var keepCount = FindUpperBound(frame);
            ClearRange(keepCount, _count - keepCount);
            _count = keepCount;
            if (_count == 0) _head = 0;
        }

        /// <summary>
        /// 清空全部保留项并断开内部引用，保留容量；不自动释放元素。
        /// </summary>
        /// <inheritdoc />
        public void Clear()
        {
            ClearRange(0, _count);
            _head = 0;
            _count = 0;
        }

        /// <summary>
        /// 尝试设置正数容量；非正数返回 false 且不改变状态，缩容时保留最新项及其原顺序。
        /// </summary>
        /// <inheritdoc />
        public bool TrySetCapacity(int capacity)
        {
            if (capacity <= 0) return false;
            if (capacity == Capacity) return true;

            var retainedCount = Math.Min(_count, capacity);
            var firstRetained = _count - retainedCount;
            var entries = new Entry[capacity];
            if (retainedCount > 0)
            {
                var source = PhysicalIndex(firstRetained);
                var firstLength = Math.Min(retainedCount, Capacity - source);
                Array.Copy(_entries, source, entries, 0, firstLength);
                if (firstLength < retainedCount)
                {
                    Array.Copy(_entries, 0, entries, firstLength, retainedCount - firstLength);
                }
            }

            _entries = entries;
            _head = 0;
            _count = retainedCount;
            return true;
        }

        private int FindFrame(int frame)
        {
            var low = 0;
            var high = _count - 1;
            while (low <= high)
            {
                var middle = low + ((high - low) >> 1);
                var candidate = _entries[PhysicalIndex(middle)].Frame;
                if (candidate == frame) return middle;
                if (candidate < frame) low = middle + 1;
                else high = middle - 1;
            }

            return ~low;
        }

        private int FindLowerBound(int frame)
        {
            var result = FindFrame(frame);
            return result >= 0 ? result : ~result;
        }

        private int FindUpperBound(int frame)
        {
            var result = FindFrame(frame);
            return result >= 0 ? result + 1 : ~result;
        }

        private void RemoveFirst() => RemoveFirst(1);

        private void RemoveFirst(int count)
        {
            if (count <= 0) return;
            if (count >= _count)
            {
                Clear();
                return;
            }

            ClearRange(0, count);
            _head = (_head + count) % Capacity;
            _count -= count;
        }

        private void CopySlot(int sourceOrderedIndex, int targetOrderedIndex)
        {
            var source = PhysicalIndex(sourceOrderedIndex);
            var target = PhysicalIndex(targetOrderedIndex);
            _entries[target] = _entries[source];
        }

        private void ClearSlot(int orderedIndex)
        {
            var physical = PhysicalIndex(orderedIndex);
            _entries[physical] = default;
        }

        private void ClearRange(int orderedIndex, int count)
        {
            if (count <= 0) return;

            var start = PhysicalIndex(orderedIndex);
            var firstLength = Math.Min(count, Capacity - start);
            Array.Clear(_entries, start, firstLength);
            if (firstLength < count)
            {
                Array.Clear(_entries, 0, count - firstLength);
            }
        }

        private int PhysicalIndex(int orderedIndex) => (_head + orderedIndex) % Capacity;

        private void ValidateIndex(int index)
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
        }

        /// <summary>
        /// 将完整整数帧键及其值保存在同一槽位，搬移时维持键值对应关系。
        /// </summary>
        private readonly struct Entry
        {
            public Entry(int frame, T value)
            {
                Frame = frame;
                Value = value;
            }

            public int Frame { get; }
            public T Value { get; }
        }
    }
}

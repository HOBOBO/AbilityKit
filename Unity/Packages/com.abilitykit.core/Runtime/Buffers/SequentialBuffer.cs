using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Buffers
{
    /// <summary>
    /// 按最旧到最新的顺序保留有限数量的元素；满容量追加和缩容均淘汰最旧项。仅保留值或对象引用，不复制对象、不自动释放元素；访问需要单一所有者或外部同步。
    /// </summary>
    public interface ISequentialBuffer<T> : IBufferCapacityControl
    {
        /// <summary>
        /// 获取当前保留的元素数量。
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 按最旧到最新的索引读取元素；索引须在零到 Count 的左闭右开区间内。
        /// </summary>
        T this[int orderedIndex] { get; }

        /// <summary>
        /// 追加最新项，满容量时淘汰最旧项；不自动释放被淘汰的元素。
        /// </summary>
        void AddLast(T item);

        /// <summary>
        /// 清空保留项并断开内部引用，保留容量；不自动释放元素。
        /// </summary>
        void Clear();
    }

    /// <summary>
    /// 以列表保存有界顺序元素；满容量追加和缩容需要搬移保留项，不保证与环形实现具有相同复杂度。此类型非线程安全。
    /// </summary>
    public sealed class ListSequentialBuffer<T> : ISequentialBuffer<T>
    {
        private readonly List<T> _items;
        private int _capacity;

        /// <summary>
        /// 创建空的列表顺序缓冲区；初始容量必须为正数。
        /// </summary>
        public ListSequentialBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _items = new List<T>(capacity);
        }

        /// <summary>
        /// 获取当前保留项的数量。
        /// </summary>
        /// <inheritdoc />
        public int Count => _items.Count;

        /// <summary>
        /// 获取当前生效的保留容量；逻辑容量不保证等于底层已分配空间。
        /// </summary>
        /// <inheritdoc />
        public int Capacity => _capacity;

        /// <summary>
        /// 按最旧到最新的索引读取元素；索引越界时抛出异常。
        /// </summary>
        /// <inheritdoc />
        public T this[int orderedIndex] => _items[orderedIndex];

        /// <summary>
        /// 追加最新项，满容量时淘汰最旧项；不自动释放被淘汰的元素。
        /// </summary>
        /// <inheritdoc />
        public void AddLast(T item)
        {
            /*
             * 满容量时先腾出最旧项的位置，避免追加后立即裁剪引起无用扩容。
             */
            if (_items.Count == _capacity) _items.RemoveAt(0);
            _items.Add(item);
        }

        /// <summary>
        /// 清空全部保留项并断开内部引用，保留容量；不自动释放元素。
        /// </summary>
        /// <inheritdoc />
        public void Clear() => _items.Clear();

        /// <summary>
        /// 尝试设置正数容量；非正数返回 false 且不改变状态，缩容时保留最新项及其原顺序。
        /// </summary>
        /// <inheritdoc />
        public bool TrySetCapacity(int capacity)
        {
            if (capacity <= 0) return false;
            if (capacity == _capacity) return true;
            if (_items.Capacity < capacity) _items.Capacity = capacity;
            _capacity = capacity;
            TrimToCapacity();
            return true;
        }

        private void TrimToCapacity()
        {
            var overflow = _items.Count - _capacity;
            if (overflow > 0) _items.RemoveRange(0, overflow);
        }
    }

    /// <summary>
    /// 以环形数组保存有界顺序元素，追加及淘汰最旧项为常数时间；容量变化需要重新分配并保留最新项及其顺序。此类型非线程安全。
    /// </summary>
    public sealed class RingSequentialBuffer<T> : ISequentialBuffer<T>
    {
        private T[] _items;
        private int _head;
        private int _count;

        /// <summary>
        /// 创建空的环形顺序缓冲区；初始容量必须为正数。
        /// </summary>
        public RingSequentialBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _items = new T[capacity];
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
        public int Capacity => _items.Length;

        /// <summary>
        /// 按最旧到最新的索引读取元素；索引越界时抛出异常。
        /// </summary>
        /// <inheritdoc />
        public T this[int orderedIndex]
        {
            get
            {
                if ((uint)orderedIndex >= (uint)_count)
                    throw new ArgumentOutOfRangeException(nameof(orderedIndex));
                return _items[PhysicalIndex(orderedIndex)];
            }
        }

        /// <summary>
        /// 追加最新项，满容量时淘汰最旧项；不自动释放被淘汰的元素。
        /// </summary>
        /// <inheritdoc />
        public void AddLast(T item)
        {
            if (_count < Capacity)
            {
                _items[PhysicalIndex(_count)] = item;
                _count++;
                return;
            }

            _items[_head] = item;
            _head = (_head + 1) % Capacity;
        }

        /// <summary>
        /// 清空全部保留项并断开内部引用，保留容量；不自动释放元素。
        /// </summary>
        /// <inheritdoc />
        public void Clear()
        {
            if (_count > 0)
            {
                if (_head + _count <= Capacity)
                {
                    Array.Clear(_items, _head, _count);
                }
                else
                {
                    var firstLength = Capacity - _head;
                    Array.Clear(_items, _head, firstLength);
                    Array.Clear(_items, 0, _count - firstLength);
                }
            }

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
            var items = new T[capacity];
            for (var index = 0; index < retainedCount; index++)
            {
                items[index] = this[firstRetained + index];
            }

            _items = items;
            _head = 0;
            _count = retainedCount;
            return true;
        }

        private int PhysicalIndex(int orderedIndex) => (_head + orderedIndex) % Capacity;
    }
}

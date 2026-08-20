using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Buffers
{
    /// <summary>
    /// Capacity-bounded sequence that evicts its oldest item when full.
    /// </summary>
    public interface ISequentialBuffer<T> : IBufferCapacityControl
    {
        /// <summary>Gets the number of retained items.</summary>
        int Count { get; }

        /// <summary>Gets an item by oldest-to-newest ordered index.</summary>
        T this[int orderedIndex] { get; }

        /// <summary>Adds a newest item and evicts the oldest item when full.</summary>
        void AddLast(T item);

        /// <summary>Removes all retained items.</summary>
        void Clear();
    }

    /// <summary>Sequential buffer backed by <see cref="List{T}"/>.</summary>
    public sealed class ListSequentialBuffer<T> : ISequentialBuffer<T>
    {
        private readonly List<T> _items;
        private int _capacity;

        /// <summary>Creates empty list-backed storage with the supplied capacity.</summary>
        public ListSequentialBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _items = new List<T>(capacity);
        }

        /// <inheritdoc />
        public int Count => _items.Count;

        /// <inheritdoc />
        public int Capacity => _capacity;

        /// <inheritdoc />
        public T this[int orderedIndex] => _items[orderedIndex];

        /// <inheritdoc />
        public void AddLast(T item)
        {
            _items.Add(item);
            TrimToCapacity();
        }

        /// <inheritdoc />
        public void Clear() => _items.Clear();

        /// <inheritdoc />
        public bool TrySetCapacity(int capacity)
        {
            if (capacity <= 0) return false;
            if (capacity == _capacity) return true;
            _capacity = capacity;
            TrimToCapacity();
            if (_items.Capacity < capacity) _items.Capacity = capacity;
            return true;
        }

        private void TrimToCapacity()
        {
            var overflow = _items.Count - _capacity;
            if (overflow > 0) _items.RemoveRange(0, overflow);
        }
    }

    /// <summary>
    /// Resizable circular sequence with O(1) append and oldest-item eviction.
    /// Capacity changes retain the newest items in their original order.
    /// </summary>
    public sealed class RingSequentialBuffer<T> : ISequentialBuffer<T>
    {
        private T[] _items;
        private int _head;
        private int _count;

        /// <summary>Creates empty circular storage with the supplied capacity.</summary>
        public RingSequentialBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _items = new T[capacity];
        }

        /// <inheritdoc />
        public int Count => _count;

        /// <inheritdoc />
        public int Capacity => _items.Length;

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

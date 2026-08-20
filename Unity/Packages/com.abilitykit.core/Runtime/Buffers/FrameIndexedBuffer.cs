using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using AbilityKit.Core.Collections;

namespace AbilityKit.Core.Buffers
{
    /// <summary>
    /// Ordered, capacity-bounded storage keyed by simulation frame.
    /// Implementations retain the newest frame keys when their capacity is exceeded.
    /// </summary>
    public interface IFrameIndexedBuffer<T> : IBufferCapacityControl
    {
        /// <summary>Gets the number of retained frames.</summary>
        int Count { get; }

        /// <summary>Gets a frame key by ascending ordered index.</summary>
        int GetFrameAt(int orderedIndex);

        /// <summary>Gets the first ordered index whose frame is at least <paramref name="frame"/>.</summary>
        int LowerBound(int frame);

        /// <summary>Stores or replaces the value for a frame.</summary>
        void Store(int frame, T value);

        /// <summary>Attempts to get the value retained for a frame.</summary>
        bool TryGet(int frame, [MaybeNullWhen(false)] out T value);

        /// <summary>Determines whether a frame is retained.</summary>
        bool Contains(int frame);

        /// <summary>Removes one frame when present.</summary>
        bool Remove(int frame);

        /// <summary>Removes frames strictly earlier than <paramref name="frame"/>.</summary>
        void RemoveBefore(int frame);

        /// <summary>Removes frames strictly later than <paramref name="frame"/>.</summary>
        void RemoveAfter(int frame);

        /// <summary>Removes all retained frames.</summary>
        void Clear();
    }

    /// <summary>
    /// Sparse frame storage backed by a dictionary and an ordered frame index.
    /// Prefer this backend when frame keys may contain large gaps or arrive out of order.
    /// </summary>
    public sealed class SparseFrameIndexedBuffer<T> : IFrameIndexedBuffer<T>
    {
        private readonly Dictionary<int, T> _values;
        private readonly SortedIntSet _frames;
        private int _capacity;

        /// <summary>Creates empty sparse storage with the supplied capacity.</summary>
        public SparseFrameIndexedBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
            _values = new Dictionary<int, T>(capacity);
            _frames = new SortedIntSet(capacity);
        }

        /// <inheritdoc />
        public int Count => _frames.Count;

        /// <inheritdoc />
        public int Capacity => _capacity;

        /// <inheritdoc />
        public int GetFrameAt(int orderedIndex) => _frames[orderedIndex];

        /// <inheritdoc />
        public int LowerBound(int frame) => _frames.LowerBound(frame);

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

        /// <inheritdoc />
        public bool TryGet(int frame, [MaybeNullWhen(false)] out T value) => _values.TryGetValue(frame, out value);

        /// <inheritdoc />
        public bool Contains(int frame) => _values.ContainsKey(frame);

        /// <inheritdoc />
        public bool Remove(int frame)
        {
            if (!_values.Remove(frame)) return false;
            _frames.Remove(frame);
            return true;
        }

        /// <inheritdoc />
        public void RemoveBefore(int frame)
        {
            RemoveOrderedRange(0, _frames.LowerBound(frame));
        }

        /// <inheritdoc />
        public void RemoveAfter(int frame)
        {
            var firstRemoval = _frames.UpperBound(frame);
            RemoveOrderedRange(firstRemoval, _frames.Count - firstRemoval);
        }

        /// <inheritdoc />
        public void Clear()
        {
            _values.Clear();
            _frames.Clear();
        }

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
    /// Resizable circular frame storage. Monotonic appends and oldest-frame eviction are O(1);
    /// uncommon out-of-order inserts remain supported and preserve the same ordered semantics.
    /// </summary>
    public sealed class RingFrameIndexedBuffer<T> : IFrameIndexedBuffer<T>
    {
        private Entry[] _entries;
        private int _head;
        private int _count;

        /// <summary>Creates empty circular frame storage with the supplied capacity.</summary>
        public RingFrameIndexedBuffer(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _entries = new Entry[capacity];
        }

        /// <inheritdoc />
        public int Count => _count;

        /// <inheritdoc />
        public int Capacity => _entries.Length;

        /// <inheritdoc />
        public int GetFrameAt(int orderedIndex)
        {
            ValidateIndex(orderedIndex);
            return _entries[PhysicalIndex(orderedIndex)].Frame;
        }

        /// <inheritdoc />
        public int LowerBound(int frame) => FindLowerBound(frame);

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
                // An older insertion would be evicted immediately because the contract retains
                // the newest frame keys. Avoid shifting the entire ring in that case.
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

        /// <inheritdoc />
        public bool Contains(int frame) => FindFrame(frame) >= 0;

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

        /// <inheritdoc />
        public void RemoveBefore(int frame)
        {
            RemoveFirst(FindLowerBound(frame));
        }

        /// <inheritdoc />
        public void RemoveAfter(int frame)
        {
            var keepCount = FindUpperBound(frame);
            ClearRange(keepCount, _count - keepCount);
            _count = keepCount;
            if (_count == 0) _head = 0;
        }

        /// <inheritdoc />
        public void Clear()
        {
            ClearRange(0, _count);
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

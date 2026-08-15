using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Collections
{
    /// <summary>
    /// Stores unique integers in ascending order with binary-search lookup and insertion.
    /// Intended for small, frequently trimmed indexes that are read by position.
    /// The collection is not thread-safe; callers must synchronize compound operations.
    /// </summary>
    public sealed class SortedIntSet
    {
        private readonly List<int> _items;

        /// <summary>Initializes an empty set with the specified capacity.</summary>
        /// <param name="capacity">The initial number of values the set can hold.</param>
        public SortedIntSet(int capacity = 0)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _items = new List<int>(capacity);
        }

        /// <summary>Gets the number of values in the set.</summary>
        public int Count => _items.Count;

        /// <summary>Gets the value at the specified ascending index.</summary>
        /// <param name="index">The zero-based ordered index.</param>
        public int this[int index] => _items[index];

        /// <summary>Adds a value at its ascending position when it is not already present.</summary>
        /// <param name="value">The value to add.</param>
        /// <returns><see langword="true"/> when the value was added; otherwise, <see langword="false"/>.</returns>
        public bool Add(int value)
        {
            var index = _items.BinarySearch(value);
            if (index >= 0) return false;

            _items.Insert(~index, value);
            return true;
        }

        /// <summary>Determines whether the set contains a value.</summary>
        /// <param name="value">The value to locate.</param>
        public bool Contains(int value) => _items.BinarySearch(value) >= 0;

        /// <summary>Removes a value when present.</summary>
        /// <param name="value">The value to remove.</param>
        /// <returns><see langword="true"/> when the value was removed; otherwise, <see langword="false"/>.</returns>
        public bool Remove(int value)
        {
            var index = _items.BinarySearch(value);
            if (index < 0) return false;

            _items.RemoveAt(index);
            return true;
        }

        /// <summary>Gets the first index whose value is greater than or equal to the supplied value.</summary>
        /// <param name="value">The lower-bound value.</param>
        public int LowerBound(int value)
        {
            var low = 0;
            var high = _items.Count;
            while (low < high)
            {
                var middle = low + ((high - low) >> 1);
                if (_items[middle] < value)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low;
        }

        /// <summary>Gets the first index whose value is greater than the supplied value.</summary>
        /// <param name="value">The upper-bound value.</param>
        public int UpperBound(int value)
        {
            var low = 0;
            var high = _items.Count;
            while (low < high)
            {
                var middle = low + ((high - low) >> 1);
                if (_items[middle] <= value)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low;
        }

        /// <summary>Removes a contiguous range of ordered values.</summary>
        /// <param name="index">The zero-based starting index.</param>
        /// <param name="count">The number of values to remove.</param>
        public void RemoveRange(int index, int count) => _items.RemoveRange(index, count);

        /// <summary>Removes all values while retaining the current capacity.</summary>
        public void Clear() => _items.Clear();
    }
}

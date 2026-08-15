using System;
using System.Collections;
using System.Collections.Generic;

namespace AbilityKit.Core.Collections
{
    /// <summary>
    /// Defines whether lower or higher priority values appear first.
    /// </summary>
    public enum PriorityDirection
    {
        /// <summary>Lower priority values appear first.</summary>
        Ascending = 0,

        /// <summary>Higher priority values appear first.</summary>
        Descending = 1,
    }

    /// <summary>
    /// Keeps items ordered by priority while preserving registration order for equal priorities.
    /// Intended for small registration pipelines whose order is part of their public contract.
    /// The collection is not thread-safe and must not be mutated during enumeration.
    /// </summary>
    public sealed class StablePriorityList<T> : IReadOnlyList<T>
    {
        private readonly List<Entry> _entries;
        private readonly PriorityDirection _direction;
        private long _nextSequence;

        /// <summary>
        /// Initializes an empty list with the specified ordering direction and initial capacity.
        /// </summary>
        /// <param name="direction">The direction in which priorities are ordered.</param>
        /// <param name="capacity">The initial number of entries the list can hold.</param>
        public StablePriorityList(PriorityDirection direction = PriorityDirection.Ascending, int capacity = 0)
        {
            if (capacity < 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            _direction = direction;
            _entries = new List<Entry>(capacity);
        }

        /// <summary>Gets the number of items in the list.</summary>
        public int Count => _entries.Count;

        /// <summary>Gets the item at the specified ordered index.</summary>
        /// <param name="index">The zero-based ordered index.</param>
        public T this[int index] => _entries[index].Item;

        /// <summary>Adds an item at the stable position determined by its priority.</summary>
        /// <param name="item">The item to add.</param>
        /// <param name="priority">The priority used to order the item.</param>
        public void Add(T item, int priority = 0)
        {
            var entry = new Entry(item, priority, _nextSequence++);
            _entries.Insert(FindInsertIndex(in entry), entry);
        }

        /// <summary>Removes the first ordered item that satisfies the predicate.</summary>
        /// <param name="match">The predicate used to locate the item.</param>
        /// <returns><see langword="true"/> when an item was removed; otherwise, <see langword="false"/>.</returns>
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

        /// <summary>Updates the priority of the first ordered item that satisfies the predicate.</summary>
        /// <param name="match">The predicate used to locate the item.</param>
        /// <param name="priority">The replacement priority.</param>
        /// <returns><see langword="true"/> when an item was found; otherwise, <see langword="false"/>.</returns>
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

        /// <summary>Removes all items and resets registration sequencing.</summary>
        public void Clear()
        {
            _entries.Clear();
            _nextSequence = 0;
        }

        /// <summary>Returns an enumerator that visits items in priority order.</summary>
        /// <returns>An enumerator over the ordered items.</returns>
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

using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Moba.Services
{
    internal sealed class MobaSnapshotBuffer<T>
    {
        private readonly int _initialCapacity;
        private readonly int _maxRetainedCapacity;
        private readonly List<T> _items;

        public MobaSnapshotBuffer(int initialCapacity, int maxRetainedCapacity)
        {
            if (initialCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            if (maxRetainedCapacity < initialCapacity) throw new ArgumentOutOfRangeException(nameof(maxRetainedCapacity));

            _initialCapacity = initialCapacity;
            _maxRetainedCapacity = maxRetainedCapacity;
            _items = new List<T>(initialCapacity);
        }

        public int Count => _items.Count;

        public List<T> Items => _items;

        public void Add(T item)
        {
            _items.Add(item);
        }

        public void AddRange(List<T> items)
        {
            if (items == null || items.Count == 0) return;
            _items.AddRange(items);
        }

        public void Clear()
        {
            _items.Clear();
        }

        public void Sort(Comparison<T> comparison)
        {
            _items.Sort(comparison);
        }

        public int PeekTo(IList<T> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            for (int i = 0; i < _items.Count; i++)
            {
                destination.Add(_items[i]);
            }

            return _items.Count;
        }

        public int CopyTo(IList<T> destination)
        {
            return PeekTo(destination);
        }

        public int CopyTo(T[] destination, int destinationIndex = 0)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (destinationIndex < 0 || destinationIndex > destination.Length) throw new ArgumentOutOfRangeException(nameof(destinationIndex));
            if (_items.Count == 0) return 0;
            if (destination.Length - destinationIndex < _items.Count) throw new ArgumentException("Destination array is too small.", nameof(destination));

            _items.CopyTo(destination, destinationIndex);
            return _items.Count;
        }

        public int DrainTo(IList<T> destination)
        {
            var count = PeekTo(destination);
            _items.Clear();
            TrimIfNeeded();
            return count;
        }

        public int DrainTo(T[] destination, int destinationIndex = 0)
        {
            var count = CopyTo(destination, destinationIndex);
            _items.Clear();
            TrimIfNeeded();
            return count;
        }

        public T[] ToArrayAndTrim()
        {
            T[] array = _items.Count == 0 ? Array.Empty<T>() : _items.ToArray();
            TrimIfNeeded();
            return array;
        }

        public T[] ToArrayClearAndTrim()
        {
            T[] array = _items.Count == 0 ? Array.Empty<T>() : _items.ToArray();
            _items.Clear();
            TrimIfNeeded();
            return array;
        }

        public void ClearAndTrim()
        {
            _items.Clear();
            TrimIfNeeded();
        }

        private void TrimIfNeeded()
        {
            if (_items.Capacity <= _maxRetainedCapacity) return;

            _items.Clear();
            _items.Capacity = _initialCapacity;
        }
    }
}

#nullable enable

using System;
using System.Collections.Generic;

namespace AbilityKit.Demo.Shooter.View
{
    internal sealed class ShooterStableSlotBuffer<T>
    {
        private readonly Dictionary<ShooterViewEntityKey, int> _slots = new Dictionary<ShooterViewEntityKey, int>();
        private readonly List<ShooterViewEntityKey> _keys = new List<ShooterViewEntityKey>();
        private readonly List<T> _values = new List<T>();
        private readonly List<int> _dirtySlots = new List<int>();

        public List<T> Values => _values;

        public List<int> DirtySlots => _dirtySlots;

        public int Count => _values.Count;

        public bool RequiresFullUpload { get; private set; }

        public bool CountChanged { get; private set; }

        public void EnsureCapacity(int capacity)
        {
            if (capacity <= 0)
            {
                return;
            }

            _slots.EnsureCapacity(capacity);
            if (_keys.Capacity < capacity) _keys.Capacity = capacity;
            if (_values.Capacity < capacity) _values.Capacity = capacity;
            if (_dirtySlots.Capacity < capacity) _dirtySlots.Capacity = capacity;
        }

        public void BeginUpdate()
        {
            _dirtySlots.Clear();
            RequiresFullUpload = false;
            CountChanged = false;
        }

        public void RequireFullUpload()
        {
            RequiresFullUpload = true;
        }

        public bool Contains(ShooterViewEntityKey key)
        {
            return _slots.ContainsKey(key);
        }

        public bool Upsert(ShooterViewEntityKey key, in T value)
        {
            if (_slots.TryGetValue(key, out var slot))
            {
                _values[slot] = value;
                _dirtySlots.Add(slot);
                return false;
            }

            slot = _values.Count;
            _slots.Add(key, slot);
            _keys.Add(key);
            _values.Add(value);
            _dirtySlots.Add(slot);
            CountChanged = true;
            return true;
        }

        public bool Remove(ShooterViewEntityKey key)
        {
            if (!_slots.TryGetValue(key, out var removedSlot))
            {
                return false;
            }

            _slots.Remove(key);
            var lastSlot = _values.Count - 1;
            if (removedSlot != lastSlot)
            {
                var movedKey = _keys[lastSlot];
                _keys[removedSlot] = movedKey;
                _values[removedSlot] = _values[lastSlot];
                _slots[movedKey] = removedSlot;
                _dirtySlots.Add(removedSlot);
            }

            _keys.RemoveAt(lastSlot);
            _values.RemoveAt(lastSlot);
            CountChanged = true;
            return true;
        }

        public bool TryGetValue(ShooterViewEntityKey key, out T value)
        {
            if (_slots.TryGetValue(key, out var slot))
            {
                value = _values[slot];
                return true;
            }

            value = default!;
            return false;
        }

        public void Clear()
        {
            var hadValues = _values.Count != 0;
            _slots.Clear();
            _keys.Clear();
            _values.Clear();
            _dirtySlots.Clear();
            RequiresFullUpload = hadValues;
            CountChanged = hadValues;
        }
    }
}

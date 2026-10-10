using System;
using System.Collections.Generic;
using AbilityKit.Deterministic;
using AbilityKit.Ability.World.DI;
using AbilityKit.Ability.World.Services;
using AbilityKit.Ability.World.Services.Attributes;

namespace AbilityKit.Demo.Moba.Gameplay
{
    [WorldService(typeof(MobaGameplayVariableService), WorldLifetime.Scoped)]
    public sealed class MobaGameplayVariableService : IService
    {
        private readonly Dictionary<int, long> _values = new Dictionary<int, long>();

        public bool TryGet(int keyId, out double value)
        {
            value = default;
            if (keyId == 0)
            {
                return false;
            }

            if (!_values.TryGetValue(keyId, out var raw)) return false;
            value = Fixed64.FromRaw(raw).ToDouble();
            return true;
        }

        public double Get(int keyId, double defaultValue = 0d)
        {
            return TryGet(keyId, out var value) ? value : defaultValue;
        }

        public void Set(int keyId, double value)
        {
            if (keyId == 0)
            {
                return;
            }

            _values[keyId] = Fixed64.FromDouble(value).RawValue;
        }

        public double Add(int keyId, double delta)
        {
            if (keyId == 0)
            {
                return 0d;
            }

            _values.TryGetValue(keyId, out var current);
            var next = checked(current + Fixed64.FromDouble(delta).RawValue);
            _values[keyId] = next;
            return Fixed64.FromRaw(next).ToDouble();
        }

        public MobaGameplayVariableEntry[] Capture()
        {
            var entries = new MobaGameplayVariableEntry[_values.Count];
            var index = 0;
            foreach (var pair in _values) entries[index++] = new MobaGameplayVariableEntry(pair.Key, pair.Value);
            Array.Sort(entries, (left, right) => left.KeyId.CompareTo(right.KeyId));
            return entries;
        }

        public void Restore(MobaGameplayVariableEntry[] entries)
        {
            var restored = new Dictionary<int, long>();
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (entry.KeyId <= 0 || !restored.TryAdd(entry.KeyId, entry.RawValue))
                        throw new InvalidOperationException("Invalid gameplay variable snapshot.");
                }
            }
            _values.Clear();
            foreach (var entry in restored) _values.Add(entry.Key, entry.Value);
        }

        public void Clear()
        {
            _values.Clear();
        }

        public void Dispose()
        {
            Clear();
        }
    }

    public readonly struct MobaGameplayVariableEntry
    {
        public int KeyId { get; }
        public long RawValue { get; }

        public MobaGameplayVariableEntry(int keyId, long rawValue)
        {
            KeyId = keyId;
            RawValue = rawValue;
        }
    }
}

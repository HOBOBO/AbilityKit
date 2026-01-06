using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Triggering
{
    public sealed class DictionaryVarStore : IVarStore
    {
        private Dictionary<string, object> _vars;

        public DictionaryVarStore()
        {
            _vars = new Dictionary<string, object>(StringComparer.Ordinal);
        }

        public DictionaryVarStore(int initialCapacity)
        {
            _vars = new Dictionary<string, object>(initialCapacity, StringComparer.Ordinal);
        }

        public DictionaryVarStore(IEqualityComparer<string> comparer)
        {
            _vars = new Dictionary<string, object>(comparer ?? StringComparer.Ordinal);
        }

        public DictionaryVarStore(int initialCapacity, IEqualityComparer<string> comparer)
        {
            _vars = new Dictionary<string, object>(initialCapacity, comparer ?? StringComparer.Ordinal);
        }

        public bool TryGet(string key, out object value)
        {
            if (key != null && _vars.TryGetValue(key, out var obj))
            {
                value = obj;
                return true;
            }

            value = null;
            return false;
        }

        public bool TryGet<T>(string key, out T value)
        {
            if (key != null && _vars.TryGetValue(key, out var obj) && obj is T t)
            {
                value = t;
                return true;
            }

            value = default;
            return false;
        }

        public void Set(string key, object value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            _vars[key] = value;
        }

        public void Clear()
        {
            _vars.Clear();
        }

        public void EnsureCapacity(int capacity)
        {
            if (capacity <= 0) return;
            if (_vars.Count >= capacity) return;

            var comparer = _vars.Comparer;
            var next = new Dictionary<string, object>(capacity, comparer);
            foreach (var kv in _vars)
            {
                next[kv.Key] = kv.Value;
            }
            _vars = next;
        }

        public void TrimExcess()
        {
            var comparer = _vars.Comparer;
            var next = new Dictionary<string, object>(_vars.Count, comparer);
            foreach (var kv in _vars)
            {
                next[kv.Key] = kv.Value;
            }
            _vars = next;
        }
    }
}

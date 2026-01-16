using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Triggering.Blackboard
{
    public sealed class BlackboardImpl : IBlackboard
    {
        private readonly Dictionary<string, object> _map;

        public BlackboardImpl()
        {
            _map = new Dictionary<string, object>(StringComparer.Ordinal);
        }

        private BlackboardImpl(Dictionary<string, object> map)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
        }

        public bool TryGet(string key, out object value)
        {
            if (key != null && _map.TryGetValue(key, out var obj))
            {
                value = obj;
                return true;
            }

            value = null;
            return false;
        }

        public bool TryGet<T>(string key, out T value)
        {
            if (key != null && _map.TryGetValue(key, out var obj) && obj is T t)
            {
                value = t;
                return true;
            }

            value = default;
            return false;
        }

        public bool TryGetDouble(string key, out double value)
        {
            value = 0d;
            if (key == null) return false;
            if (!_map.TryGetValue(key, out var obj) || obj == null) return false;

            try
            {
                value = Convert.ToDouble(obj);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Set(string key, object value)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            _map[key] = value;
        }

        public IBlackboard CloneShallow()
        {
            var next = new Dictionary<string, object>(_map.Count, StringComparer.Ordinal);
            foreach (var kv in _map)
            {
                next[kv.Key] = kv.Value;
            }
            return new BlackboardImpl(next);
        }
    }
}

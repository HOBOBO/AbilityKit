using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Flow
{
    public sealed class FlowContext
    {
        private readonly Dictionary<Type, object> _map = new Dictionary<Type, object>();

        public void Set<T>(T value)
        {
            _map[typeof(T)] = value;
        }

        public bool TryGet<T>(out T value)
        {
            if (_map.TryGetValue(typeof(T), out var obj) && obj is T typed)
            {
                value = typed;
                return true;
            }

            value = default;
            return false;
        }

        public T Get<T>()
        {
            if (TryGet<T>(out var v)) return v;
            throw new InvalidOperationException($"FlowContext missing value: {typeof(T).FullName}");
        }

        public void Remove<T>()
        {
            _map.Remove(typeof(T));
        }

        public void Clear()
        {
            _map.Clear();
        }
    }
}

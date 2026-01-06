using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Common.Pool
{
    public sealed class PoolManager
    {
        private readonly Dictionary<(Type type, PoolKey key), object> _pools = new Dictionary<(Type, PoolKey), object>();

        public ObjectPool<T> GetOrCreate<T>(PoolKey key, ObjectPoolOptions<T> options) where T : class
        {
            key = PoolKey.Normalize(key);
            var k = (typeof(T), key);
            if (_pools.TryGetValue(k, out var existing)) return (ObjectPool<T>)existing;

            var pool = new ObjectPool<T>(options);
            _pools.Add(k, pool);
            return pool;
        }

        public bool TryGet<T>(PoolKey key, out ObjectPool<T> pool) where T : class
        {
            key = PoolKey.Normalize(key);
            var k = (typeof(T), key);
            if (_pools.TryGetValue(k, out var existing))
            {
                pool = (ObjectPool<T>)existing;
                return true;
            }

            pool = null;
            return false;
        }

        public bool Remove<T>(PoolKey key, bool destroy = false) where T : class
        {
            key = PoolKey.Normalize(key);
            var k = (typeof(T), key);
            if (!_pools.TryGetValue(k, out var existing)) return false;

            _pools.Remove(k);
            ((ObjectPool<T>)existing).Clear(destroy);
            return true;
        }

        public void ClearAll(bool destroy = false)
        {
            foreach (var kv in _pools)
            {
                if (kv.Value == null) continue;

                var type = kv.Value.GetType();
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ObjectPool<>))
                {
                    var clear = type.GetMethod("Clear", new[] { typeof(bool) });
                    clear?.Invoke(kv.Value, new object[] { destroy });
                }
            }

            _pools.Clear();
        }
    }
}

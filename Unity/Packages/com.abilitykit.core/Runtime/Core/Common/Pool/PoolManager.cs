using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AbilityKit.Core.Common.Pool
{
    public sealed class PoolManager
    {
        private readonly Dictionary<(Type type, PoolKey key), object> _pools = new Dictionary<(Type, PoolKey), object>();

        private readonly ConditionalWeakTable<object, ReleaseHandle> _releaseHandles = new ConditionalWeakTable<object, ReleaseHandle>();

        private readonly HashSet<object> _registeredPools = new HashSet<object>();

        private sealed class ReleaseHandle
        {
            public Action<object> Release;
        }

        public ObjectPool<T> GetOrCreate<T>(PoolKey key, ObjectPoolOptions<T> options) where T : class
        {
            key = PoolKey.Normalize(key);
            var k = (typeof(T), key);
            if (_pools.TryGetValue(k, out var existing)) return (ObjectPool<T>)existing;

            var pool = new ObjectPool<T>(options);
            _pools.Add(k, pool);
            return pool;
        }

        public void RegisterForObjectRelease<T>(ObjectPool<T> pool) where T : class
        {
            if (pool == null) throw new ArgumentNullException(nameof(pool));

            if (_registeredPools.Contains(pool)) return;
            _registeredPools.Add(pool);

            pool.AppendOnGet(obj =>
            {
                if (obj == null) return;

                // Update in case the object was reused with a different pool instance.
                _releaseHandles.Remove(obj);
                _releaseHandles.Add(obj, new ReleaseHandle { Release = o => pool.Release((T)o) });
            });
        }

        public bool TryRelease(object element)
        {
            if (element == null) return true;
            if (_releaseHandles.TryGetValue(element, out var handle) && handle?.Release != null)
            {
                handle.Release(element);
                return true;
            }

            return false;
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

#if UNITY_EDITOR
        public IReadOnlyList<PoolDebugSnapshot> GetDebugSnapshots()
        {
            if (_pools.Count == 0) return Array.Empty<PoolDebugSnapshot>();

            var list = new List<PoolDebugSnapshot>(_pools.Count);
            foreach (var kv in _pools)
            {
                if (kv.Value is IObjectPoolDebug debug)
                {
                    list.Add(new PoolDebugSnapshot(kv.Key.type, kv.Key.key, debug.Stats, debug.MaxSize));
                }
                else
                {
                    list.Add(new PoolDebugSnapshot(kv.Key.type, kv.Key.key, default, maxSize: 0));
                }
            }

            return list;
        }
#endif
    }
}

using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Share.Common.Pool
{
    public static class Pools
    {
        private static readonly PoolManager _manager = new PoolManager();

        public static ObjectPool<T> GetPool<T>(Func<T> createFunc, Action<T> onGet = null, Action<T> onRelease = null, Action<T> onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true) where T : class
        {
            return GetPool(PoolKey.Default, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck);
        }

        public static ObjectPool<T> GetPool<T>(PoolKey key, Func<T> createFunc, Action<T> onGet = null, Action<T> onRelease = null, Action<T> onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true) where T : class
        {
            if (createFunc == null) throw new ArgumentNullException(nameof(createFunc));

            var options = new ObjectPoolOptions<T>(createFunc)
            {
                OnGet = onGet,
                OnRelease = onRelease,
                OnDestroy = onDestroy,
                DefaultCapacity = defaultCapacity,
                MaxSize = maxSize,
                CollectionCheck = collectionCheck,
            };

            var pool = _manager.GetOrCreate(key, options);
            _manager.RegisterForObjectRelease(pool);
            return pool;
        }

        public static T Get<T>(Func<T> createFunc, Action<T> onGet = null, Action<T> onRelease = null, Action<T> onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true) where T : class
        {
            return Get(PoolKey.Default, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck);
        }

        public static T Get<T>(PoolKey key, Func<T> createFunc, Action<T> onGet = null, Action<T> onRelease = null, Action<T> onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true) where T : class
        {
            return GetPool(key, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck).Get();
        }

        public static PooledObject<T> GetPooled<T>(Func<T> createFunc, Action<T> onGet = null, Action<T> onRelease = null, Action<T> onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true) where T : class
        {
            return GetPooled(PoolKey.Default, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck);
        }

        public static PooledObject<T> GetPooled<T>(PoolKey key, Func<T> createFunc, Action<T> onGet = null, Action<T> onRelease = null, Action<T> onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true) where T : class
        {
            return GetPool(key, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck).GetPooled();
        }

        public static void Release<T>(T element) where T : class
        {
            Release(PoolKey.Default, element);
        }

        public static void Release<T>(PoolKey key, T element) where T : class
        {
            if (element == null) return;
            if (!_manager.TryGet<T>(key, out var pool)) throw new InvalidOperationException($"Pool not found: type={typeof(T).FullName} key={key}");
            pool.Release(element);
        }

        public static bool TryRelease<T>(T element) where T : class
        {
            return TryRelease(PoolKey.Default, element);
        }

        public static bool TryRelease<T>(PoolKey key, T element) where T : class
        {
            if (element == null) return true;
            if (!_manager.TryGet<T>(key, out var pool)) return false;
            pool.Release(element);
            return true;
        }

        public static void Release(object element)
        {
            if (element == null) return;
            if (!_manager.TryRelease(element)) throw new InvalidOperationException($"Pool not found for instance: type={element.GetType().FullName}");
        }

        public static bool TryRelease(object element)
        {
            if (element == null) return true;
            return _manager.TryRelease(element);
        }

        public static bool DestroyPool<T>(bool destroy = true) where T : class
        {
            return DestroyPool<T>(PoolKey.Default, destroy);
        }

        public static bool DestroyPool<T>(PoolKey key, bool destroy = true) where T : class
        {
            return _manager.Remove<T>(key, destroy);
        }

        public static void ClearAll(bool destroy = false)
        {
            _manager.ClearAll(destroy);
        }

#if UNITY_EDITOR
        public static IReadOnlyList<PoolDebugSnapshot> GetDebugSnapshots()
        {
            return _manager.GetDebugSnapshots();
        }
#endif
    }
}

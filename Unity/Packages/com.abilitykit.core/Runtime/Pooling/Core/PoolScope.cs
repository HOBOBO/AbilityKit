using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 持有一组共享生命周期的对象池，例如全局、场景、战斗、UI 或功能域作用域。
    /// </summary>
    public sealed class PoolScope : IDisposable
    {
        private readonly PoolManager _manager;
        private readonly bool _destroyOnDispose;
        private bool _disposed;

        public PoolScope(string? name = null, bool destroyOnDispose = true)
        {
            Name = string.IsNullOrEmpty(name) ? "Unnamed" : name;
            _destroyOnDispose = destroyOnDispose;
            _manager = new PoolManager();
        }

        public string Name { get; }

        public bool IsDisposed => _disposed;

        internal PoolManager Manager => _manager;

        public ObjectPool<T> GetPool<T>(Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return GetPool(PoolKey.Default, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy);
        }

        public ObjectPool<T> GetPool<T>(PoolKey key, Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            ThrowIfDisposed();
            if (createFunc == null) throw new ArgumentNullException(nameof(createFunc));

            var request = new PoolConfigRequest(Name, typeof(T), key);
            var config = PoolConfigCenter.GetConfigOrDefault(request, PoolItemConfig.Default(defaultCapacity, maxSize, defaultCapacity, collectionCheck, trimPolicy));
            if (!config.Enabled) throw new InvalidOperationException($"Pool is disabled by config: {request}");

            var options = PoolOptions.FromConfig(createFunc, config).WithLifecycle(onGet, onRelease, onDestroy);
            return GetPool(key, options);
        }

        public ObjectPool<T> GetPool<T>(ObjectPoolOptions<T> options) where T : class
        {
            return GetPool(PoolKey.Default, options);
        }

        public ObjectPool<T> GetPool<T>(PoolKey key, ObjectPoolOptions<T> options) where T : class
        {
            ThrowIfDisposed();
            if (options == null) throw new ArgumentNullException(nameof(options));

            var pool = _manager.GetOrCreate(key, options);
            _manager.RegisterForObjectRelease(pool);
            return pool;
        }

        public ObjectPool<T> GetPool<T>(PoolKey key, Func<T> createFunc, PoolItemConfig fallbackConfig, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null) where T : class
        {
            ThrowIfDisposed();
            if (createFunc == null) throw new ArgumentNullException(nameof(createFunc));

            var request = new PoolConfigRequest(Name, typeof(T), key);
            var config = PoolConfigCenter.GetConfigOrDefault(request, fallbackConfig.IsSpecified ? fallbackConfig : PoolItemConfig.Default());
            if (!config.Enabled) throw new InvalidOperationException($"Pool is disabled by config: {request}");

            var options = PoolOptions.FromConfig(createFunc, config).WithLifecycle(onGet, onRelease, onDestroy);
            return GetPool(key, options);
        }

        public T Get<T>(Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return Get(PoolKey.Default, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy);
        }

        public T Get<T>(PoolKey key, Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return GetPool(key, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy).Get();
        }

        public T Get<T>(PoolKey key, Func<T> createFunc, PoolItemConfig fallbackConfig, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null) where T : class
        {
            return GetPool(key, createFunc, fallbackConfig, onGet, onRelease, onDestroy).Get();
        }

        public PooledObject<T> GetPooled<T>(Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return GetPooled(PoolKey.Default, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy);
        }

        public PooledObject<T> GetPooled<T>(PoolKey key, Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return GetPool(key, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy).GetPooled();
        }

        public PooledObject<T> GetPooled<T>(PoolKey key, Func<T> createFunc, PoolItemConfig fallbackConfig, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null) where T : class
        {
            return GetPool(key, createFunc, fallbackConfig, onGet, onRelease, onDestroy).GetPooled();
        }

        public void Release<T>(T element) where T : class
        {
            Release(PoolKey.Default, element);
        }

        public void Release<T>(PoolKey key, T element) where T : class
        {
            ThrowIfDisposed();
            if (element == null) return;
            if (!_manager.TryGet<T>(key, out var pool)) throw new InvalidOperationException($"Pool not found in scope '{Name}': type={typeof(T).FullName} key={key}");
            pool.Release(element);
        }

        public bool TryRelease<T>(T element) where T : class
        {
            return TryRelease(PoolKey.Default, element);
        }

        public bool TryRelease<T>(PoolKey key, T element) where T : class
        {
            ThrowIfDisposed();
            if (element == null) return true;
            if (!_manager.TryGet<T>(key, out var pool)) return false;
            pool.Release(element);
            return true;
        }

        public void Release(object element)
        {
            ThrowIfDisposed();
            if (element == null) return;
            if (!_manager.TryRelease(element)) throw new InvalidOperationException($"Pool not found in scope '{Name}' for instance: type={element.GetType().FullName}");
        }

        public bool TryRelease(object element)
        {
            ThrowIfDisposed();
            if (element == null) return true;
            return _manager.TryRelease(element);
        }

        public bool DestroyPool<T>(bool destroy = true) where T : class
        {
            return DestroyPool<T>(PoolKey.Default, destroy);
        }

        public bool DestroyPool<T>(PoolKey key, bool destroy = true) where T : class
        {
            ThrowIfDisposed();
            return _manager.Remove<T>(key, destroy);
        }

        public int TrimAll()
        {
            ThrowIfDisposed();
            return _manager.TrimAll();
        }

        public int TrimAll(PoolTrimPolicy policy)
        {
            ThrowIfDisposed();
            return _manager.TrimAll(policy);
        }

        public int ForceTrimAll(PoolTrimPolicy policy)
        {
            ThrowIfDisposed();
            return _manager.ForceTrimAll(policy);
        }

        public void Clear(bool destroy = false)
        {
            ThrowIfDisposed();
            _manager.ClearAll(destroy);
        }

#if UNITY_EDITOR
        public IReadOnlyList<PoolDebugSnapshot> GetDebugSnapshots()
        {
            ThrowIfDisposed();
            return _manager.GetDebugSnapshots();
        }
#endif

        public void Dispose()
        {
            Dispose(_destroyOnDispose);
        }

        internal void Dispose(bool destroy)
        {
            if (_disposed) return;
            _disposed = true;
            _manager.ClearAll(destroy);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException($"PoolScope:{Name}");
        }
    }
}

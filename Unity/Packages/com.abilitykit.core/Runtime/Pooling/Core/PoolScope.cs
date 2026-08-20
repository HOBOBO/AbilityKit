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

        /// <summary>Creates an isolated object-pool scope.</summary>
        /// <param name="name">The diagnostic scope name, or <see langword="null"/> to use <c>Unnamed</c>.</param>
        /// <param name="destroyOnDispose">Whether disposing the scope runs destruction callbacks for inactive elements.</param>
        public PoolScope(string? name = null, bool destroyOnDispose = true)
        {
            Name = string.IsNullOrEmpty(name) ? "Unnamed" : name;
            _destroyOnDispose = destroyOnDispose;
            _manager = new PoolManager();
        }

        /// <summary>Gets the normalized diagnostic name of this scope.</summary>
        public string Name { get; }

        /// <summary>Gets whether this scope has been disposed.</summary>
        public bool IsDisposed => _disposed;

        internal PoolManager Manager => _manager;

        /// <summary>Gets or creates the default keyed pool using explicit factory and lifecycle settings.</summary>
        public ObjectPool<T> GetPool<T>(Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return GetPool(PoolKey.Default, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy);
        }

        /// <summary>Gets or creates a keyed pool using resolved configuration with the supplied values as fallback.</summary>
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

        /// <summary>Gets or creates the default keyed pool from mutable options.</summary>
        public ObjectPool<T> GetPool<T>(ObjectPoolOptions<T> options) where T : class
        {
            return GetPool(PoolKey.Default, options);
        }

        /// <summary>Gets or creates a keyed pool from mutable options.</summary>
        /// <remarks>Options are consumed only when the pool is first created; later calls return the existing pool.</remarks>
        public ObjectPool<T> GetPool<T>(PoolKey key, ObjectPoolOptions<T> options) where T : class
        {
            ThrowIfDisposed();
            if (options == null) throw new ArgumentNullException(nameof(options));

            return _manager.GetOrCreate(key, options);
        }

        /// <summary>Gets or creates a keyed pool using registered configuration and the specified fallback configuration.</summary>
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

        /// <summary>Acquires an element from the default keyed pool.</summary>
        public T Get<T>(Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return Get(PoolKey.Default, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy);
        }

        /// <summary>Acquires an element from a keyed pool using explicit fallback settings.</summary>
        public T Get<T>(PoolKey key, Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return GetPool(key, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy).Get();
        }

        /// <summary>Acquires an element from a keyed pool using a fallback configuration value.</summary>
        public T Get<T>(PoolKey key, Func<T> createFunc, PoolItemConfig fallbackConfig, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null) where T : class
        {
            return GetPool(key, createFunc, fallbackConfig, onGet, onRelease, onDestroy).Get();
        }

        /// <summary>Acquires a disposable return handle from the default keyed pool.</summary>
        public PooledObject<T> GetPooled<T>(Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return GetPooled(PoolKey.Default, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy);
        }

        /// <summary>Acquires a disposable return handle from a keyed pool using explicit fallback settings.</summary>
        public PooledObject<T> GetPooled<T>(PoolKey key, Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return GetPool(key, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy).GetPooled();
        }

        /// <summary>Acquires a disposable return handle from a keyed pool using a fallback configuration value.</summary>
        public PooledObject<T> GetPooled<T>(PoolKey key, Func<T> createFunc, PoolItemConfig fallbackConfig, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null) where T : class
        {
            return GetPool(key, createFunc, fallbackConfig, onGet, onRelease, onDestroy).GetPooled();
        }

        /// <summary>Returns an element to the default keyed pool, throwing when that pool does not exist.</summary>
        public void Release<T>(T element) where T : class
        {
            Release(PoolKey.Default, element);
        }

        /// <summary>Returns an element to a keyed pool, throwing when that pool does not exist.</summary>
        public void Release<T>(PoolKey key, T element) where T : class
        {
            ThrowIfDisposed();
            if (element == null) return;
            if (!_manager.TryGet<T>(key, out var pool)) throw new InvalidOperationException($"Pool not found in scope '{Name}': type={typeof(T).FullName} key={key}");
            pool.Release(element);
        }

        /// <summary>Attempts to return an element to the default keyed pool.</summary>
        public bool TryRelease<T>(T element) where T : class
        {
            return TryRelease(PoolKey.Default, element);
        }

        /// <summary>Attempts to return an element to a keyed pool.</summary>
        /// <returns><see langword="true"/> for a null element or an existing pool; otherwise <see langword="false"/>.</returns>
        public bool TryRelease<T>(PoolKey key, T element) where T : class
        {
            ThrowIfDisposed();
            if (element == null) return true;
            if (!_manager.TryGet<T>(key, out var pool)) return false;
            pool.Release(element);
            return true;
        }

        /// <summary>Returns an instance to the pool that most recently acquired it, throwing when no registration exists.</summary>
        public void Release(object element)
        {
            ThrowIfDisposed();
            if (element == null) return;
            if (!_manager.TryRelease(element)) throw new InvalidOperationException($"Pool not found in scope '{Name}' for instance: type={element.GetType().FullName}");
        }

        /// <summary>Attempts to return an instance to the pool that most recently acquired it.</summary>
        public bool TryRelease(object element)
        {
            ThrowIfDisposed();
            if (element == null) return true;
            return _manager.TryRelease(element);
        }

        /// <summary>Destroys the default keyed pool for an element type.</summary>
        public bool DestroyPool<T>(bool destroy = true) where T : class
        {
            return DestroyPool<T>(PoolKey.Default, destroy);
        }

        /// <summary>Destroys a keyed pool for an element type.</summary>
        public bool DestroyPool<T>(PoolKey key, bool destroy = true) where T : class
        {
            ThrowIfDisposed();
            return _manager.Remove<T>(key, destroy);
        }

        /// <summary>Trims all pools in this scope using their configured policies.</summary>
        public int TrimAll()
        {
            ThrowIfDisposed();
            return _manager.TrimAll();
        }

        /// <summary>Trims all pools in this scope using the specified policy.</summary>
        public int TrimAll(PoolTrimPolicy policy)
        {
            ThrowIfDisposed();
            return _manager.TrimAll(policy);
        }

        /// <summary>Force-trims all pools in this scope using the specified policy.</summary>
        public int ForceTrimAll(PoolTrimPolicy policy)
        {
            ThrowIfDisposed();
            return _manager.ForceTrimAll(policy);
        }

        /// <summary>Unregisters all pools and clears their inactive elements.</summary>
        /// <remarks>All pools are attempted before callback failures are propagated.</remarks>
        public void Clear(bool destroy = false)
        {
            ThrowIfDisposed();
            _manager.ClearAll(destroy);
        }

#if UNITY_EDITOR
        /// <summary>Gets editor-only diagnostic snapshots for all pools in this scope.</summary>
        public IReadOnlyList<PoolDebugSnapshot> GetDebugSnapshots()
        {
            ThrowIfDisposed();
            return _manager.GetDebugSnapshots();
        }
#endif

        /// <summary>Disposes this scope using its configured destruction behavior.</summary>
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

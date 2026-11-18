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

        /// <summary>
        /// 创建隔离的对象池作用域；同名的独立实例不自动共享池，作用域释放需要外部协调。
        /// </summary>
        /// <param name="name">诊断名称，空值按具体入口的默认名称处理。</param>
        /// <param name="destroyOnDispose">释放作用域时是否销毁空闲对象。</param>
        public PoolScope(string? name = null, bool destroyOnDispose = true)
        {
            Name = string.IsNullOrEmpty(name) ? "Unnamed" : name;
            _destroyOnDispose = destroyOnDispose;
            _manager = new PoolManager();
        }

        /// <summary>
        /// 获取构造时确定的诊断名称；空名称使用 Unnamed。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 获取作用域是否已开始释放；不证明清理回调已完成或成功。
        /// </summary>
        public bool IsDisposed => _disposed;

        internal PoolManager Manager => _manager;

        /// <summary>
        /// 以配置链和显式工厂及兜底参数获取默认键的池。
        /// </summary>
        public ObjectPool<T> GetPool<T>(Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return GetPool(PoolKey.Default, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy);
        }

        /// <summary>
        /// 以配置链和显式兜底参数获取指定键的池；禁用配置会拒绝调用，已存在的池不热更新。
        /// </summary>
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

        /// <summary>
        /// 以选项获取默认键的池；仅首次创建使用选项，不经过配置提供者解析。
        /// </summary>
        public ObjectPool<T> GetPool<T>(ObjectPoolOptions<T> options) where T : class
        {
            return GetPool(PoolKey.Default, options);
        }

        /// <summary>
        /// 以选项获取指定键的池；仅首次创建使用选项，不经过配置提供者解析。
        /// </summary>
        /// <remarks>只在首次创建时读取选项，后续调用返回已有池，不更新其选项。</remarks>
        public ObjectPool<T> GetPool<T>(PoolKey key, ObjectPoolOptions<T> options) where T : class
        {
            ThrowIfDisposed();
            if (options == null) throw new ArgumentNullException(nameof(options));

            return _manager.GetOrCreate(key, options);
        }

        /// <summary>
        /// 以配置链和兜底配置获取指定键的池；禁用配置会拒绝调用，已存在的池不热更新。
        /// </summary>
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

        /// <summary>
        /// 从默认键的池获取对象，调用方负责归还。
        /// </summary>
        public T Get<T>(Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return Get(PoolKey.Default, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy);
        }

        /// <summary>
        /// 按显式兜底参数从指定键的池获取对象；配置提供者可覆盖兜底参数。
        /// </summary>
        public T Get<T>(PoolKey key, Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return GetPool(key, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy).Get();
        }

        /// <summary>
        /// 按兜底配置从指定键的池获取对象；已存在的池沿用首次创建的选项。
        /// </summary>
        public T Get<T>(PoolKey key, Func<T> createFunc, PoolItemConfig fallbackConfig, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null) where T : class
        {
            return GetPool(key, createFunc, fallbackConfig, onGet, onRelease, onDestroy).Get();
        }

        /// <summary>
        /// 从默认键的池获取归还句柄；句柄及其副本合计只能释放一次。
        /// </summary>
        public PooledObject<T> GetPooled<T>(Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return GetPooled(PoolKey.Default, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy);
        }

        /// <summary>
        /// 按显式兜底参数从指定键的池获取归还句柄；句柄及其副本合计只能释放一次。
        /// </summary>
        public PooledObject<T> GetPooled<T>(PoolKey key, Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return GetPool(key, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy).GetPooled();
        }

        /// <summary>
        /// 按兜底配置从指定键的池获取归还句柄；句柄及其副本合计只能释放一次。
        /// </summary>
        public PooledObject<T> GetPooled<T>(PoolKey key, Func<T> createFunc, PoolItemConfig fallbackConfig, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null) where T : class
        {
            return GetPool(key, createFunc, fallbackConfig, onGet, onRelease, onDestroy).GetPooled();
        }

        /// <summary>
        /// 将对象归还默认键的池；空对象不执行操作，池不存在或归还失败时抛出异常。
        /// </summary>
        public void Release<T>(T element) where T : class
        {
            Release(PoolKey.Default, element);
        }

        /// <summary>
        /// 将对象归还指定键的池；空对象不执行操作，池不存在或归还失败时抛出异常。
        /// </summary>
        public void Release<T>(PoolKey key, T element) where T : class
        {
            ThrowIfDisposed();
            if (element == null) return;
            if (!_manager.TryGet<T>(key, out var pool)) throw new InvalidOperationException($"Pool not found in scope '{Name}': type={typeof(T).FullName} key={key}");
            pool.Release(element);
        }

        /// <summary>
        /// 尝试将对象归还默认键的池；空对象视为成功，回调异常直接传播。
        /// </summary>
        public bool TryRelease<T>(T element) where T : class
        {
            return TryRelease(PoolKey.Default, element);
        }

        /// <summary>
        /// 尝试将对象归还指定键的池；未找到池返回 false，归还回调异常直接传播。
        /// </summary>
        /// <returns>空对象或已存在对应池且归还未抛异常时返回 true，未找到池时返回 false。</returns>
        public bool TryRelease<T>(PoolKey key, T element) where T : class
        {
            ThrowIfDisposed();
            if (element == null) return true;
            if (!_manager.TryGet<T>(key, out var pool)) return false;
            pool.Release(element);
            return true;
        }

        /// <summary>
        /// 按实例归还到本作用域最近一次获取它的已登记池；没有记录时抛出异常。
        /// </summary>
        public void Release(object element)
        {
            ThrowIfDisposed();
            if (element == null) return;
            if (!_manager.TryRelease(element)) throw new InvalidOperationException($"Pool not found in scope '{Name}' for instance: type={element.GetType().FullName}");
        }

        /// <summary>
        /// 尝试按实例归还到本作用域最近一次获取它的已登记池；空对象返回 true，回调异常直接传播。
        /// </summary>
        public bool TryRelease(object element)
        {
            ThrowIfDisposed();
            if (element == null) return true;
            return _manager.TryRelease(element);
        }

        /// <summary>
        /// 注销默认键的池并按参数清理空闲对象。
        /// </summary>
        public bool DestroyPool<T>(bool destroy = true) where T : class
        {
            return DestroyPool<T>(PoolKey.Default, destroy);
        }

        /// <summary>
        /// 注销指定键的池并按参数清理空闲对象；不会使已获取的池引用失效。
        /// </summary>
        public bool DestroyPool<T>(PoolKey key, bool destroy = true) where T : class
        {
            ThrowIfDisposed();
            return _manager.Remove<T>(key, destroy);
        }

        /// <summary>
        /// 按各池配置的策略裁剪本作用域全部池；禁止常规裁剪的池保持不变。
        /// </summary>
        public int TrimAll()
        {
            ThrowIfDisposed();
            return _manager.TrimAll();
        }

        /// <summary>
        /// 按指定策略裁剪本作用域全部池；禁止常规裁剪的池保持不变。
        /// </summary>
        public int TrimAll(PoolTrimPolicy policy)
        {
            ThrowIfDisposed();
            return _manager.TrimAll(policy);
        }

        /// <summary>
        /// 按指定策略强制裁剪本作用域全部池，忽略各池的禁止常规裁剪标志。
        /// </summary>
        public int ForceTrimAll(PoolTrimPolicy policy)
        {
            ThrowIfDisposed();
            return _manager.ForceTrimAll(policy);
        }

        /// <summary>
        /// 注销本作用域全部池并清理空闲对象；不会自动归还仍由调用方持有的对象。
        /// </summary>
        /// <remarks>先尝试所有池再传播回调异常；已提交的清理不回滚。</remarks>
        public void Clear(bool destroy = false)
        {
            ThrowIfDisposed();
            _manager.ClearAll(destroy);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 获取本作用域池的编辑器诊断快照；各池分别取样，不承诺跨池同一时点。
        /// </summary>
        public IReadOnlyList<PoolDebugSnapshot> GetDebugSnapshots()
        {
            ThrowIfDisposed();
            return _manager.GetDebugSnapshots();
        }
#endif

        /// <summary>
        /// 按构造时的销毁选项释放作用域，先标记已释放再清理；顺序重复调用无效，失败后不重试。
        /// </summary>
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

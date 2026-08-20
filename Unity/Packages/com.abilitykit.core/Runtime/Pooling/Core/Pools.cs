using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Pooling
{
    /// <summary>Provides convenience access to pools in the process-wide global scope.</summary>
    public static class Pools
    {
        /// <summary>Gets the process-wide global pooling scope.</summary>
        public static PoolScope GlobalScope => PoolRegistry.Global;

        /// <summary>Gets or creates a named pooling scope.</summary>
        public static PoolScope GetOrCreateScope(string name, bool destroyOnDispose = true)
        {
            return PoolRegistry.GetOrCreateScope(name, destroyOnDispose);
        }

        /// <summary>
        /// 注册对象池配置提供者，并返回可释放的注册句柄。
        /// </summary>
        /// <param name="provider">要注册的配置提供者。</param>
        /// <returns>用于注销该提供者的注册句柄。</returns>
        public static PoolConfigRegistration RegisterConfigProvider(IPoolConfigProvider provider)
        {
            return PoolRegistry.RegisterConfigProvider(provider);
        }

        /// <summary>
        /// 注册带诊断元数据的对象池配置提供者，并返回可释放的注册句柄。
        /// </summary>
        /// <param name="provider">要注册的配置提供者。</param>
        /// <param name="name">配置提供者名称。</param>
        /// <param name="source">配置来源。</param>
        /// <param name="priority">配置优先级；数值越大越优先。</param>
        /// <returns>用于注销该提供者的注册句柄。</returns>
        public static PoolConfigRegistration RegisterConfigProvider(IPoolConfigProvider provider, string name, string? source = null, int priority = 0)
        {
            return PoolRegistry.RegisterConfigProvider(provider, name, source, priority);
        }

        /// <summary>
        /// 构建并注册对象池配置模块。
        /// </summary>
        /// <param name="configure">模块配置委托。</param>
        /// <param name="defaultScopeName">默认对象池作用域名称。</param>
        /// <param name="moduleName">模块名称。</param>
        /// <param name="source">模块来源。</param>
        /// <param name="priority">模块优先级；数值越大越优先。</param>
        /// <returns>已注册的配置模块。</returns>
        public static PoolConfigModule RegisterConfigModule(
            Action<PoolConfigBuilder> configure,
            string? defaultScopeName = null,
            string? moduleName = null,
            string? source = null,
            int priority = 0)
        {
            return PoolRegistry.RegisterConfigModule(configure, defaultScopeName, moduleName, source, priority);
        }

        /// <summary>Unregisters a configuration provider by identity.</summary>
        public static bool UnregisterConfigProvider(IPoolConfigProvider provider)
        {
            return PoolRegistry.UnregisterConfigProvider(provider);
        }

        /// <summary>Removes all registered configuration providers.</summary>
        public static void ClearConfigProviders()
        {
            PoolRegistry.ClearConfigProviders();
        }

        /// <summary>
        /// 查询最终生效的配置快照。
        /// </summary>
        /// <param name="request">配置查询请求。</param>
        /// <param name="snapshot">最终命中的配置快照。</param>
        /// <returns>如果存在生效配置，则返回 <c>true</c>。</returns>
        public static bool TryGetConfigSnapshot(PoolConfigRequest request, out PoolConfigSnapshot snapshot)
        {
            return PoolRegistry.TryGetConfigSnapshot(request, out snapshot);
        }

        /// <summary>
        /// 查询配置冲突诊断报告，包含所有匹配候选与最终胜出项。
        /// </summary>
        /// <param name="request">配置查询请求。</param>
        /// <param name="report">配置冲突诊断报告。</param>
        /// <returns>如果至少存在一个匹配候选，则返回 <c>true</c>。</returns>
        public static bool TryGetConfigReport(PoolConfigRequest request, out PoolConfigReport report)
        {
            return PoolRegistry.TryGetConfigReport(request, out report);
        }

        /// <summary>
        /// 获取当前已注册配置提供者的诊断信息。
        /// </summary>
        /// <returns>配置提供者诊断信息列表。</returns>
        public static IReadOnlyList<PoolConfigProviderInfo> GetConfigProviderInfos()
        {
            return PoolRegistry.GetConfigProviderInfos();
        }

        /// <summary>Attempts to get an active named scope.</summary>
        public static bool TryGetScope(string name, out PoolScope scope)
        {
            return PoolRegistry.TryGetScope(name, out scope);
        }

        /// <summary>Destroys a named scope, or clears the reserved global scope.</summary>
        public static bool DestroyScope(string name, bool destroy = true)
        {
            return PoolRegistry.DestroyScope(name, destroy);
        }

        /// <summary>Gets or creates the default keyed global pool using explicit fallback settings.</summary>
        public static ObjectPool<T> GetPool<T>(Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return GetPool(PoolKey.Default, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy);
        }

        /// <summary>Gets or creates a keyed global pool using explicit fallback settings.</summary>
        public static ObjectPool<T> GetPool<T>(PoolKey key, Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return PoolRegistry.Global.GetPool(key, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy);
        }

        /// <summary>Gets or creates the default keyed global pool from options.</summary>
        public static ObjectPool<T> GetPool<T>(ObjectPoolOptions<T> options) where T : class
        {
            return PoolRegistry.Global.GetPool(options);
        }

        /// <summary>Gets or creates a keyed global pool from options.</summary>
        public static ObjectPool<T> GetPool<T>(PoolKey key, ObjectPoolOptions<T> options) where T : class
        {
            return PoolRegistry.Global.GetPool(key, options);
        }

        /// <summary>Gets or creates a keyed global pool using a fallback configuration value.</summary>
        public static ObjectPool<T> GetPool<T>(PoolKey key, Func<T> createFunc, PoolItemConfig fallbackConfig, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null) where T : class
        {
            return PoolRegistry.Global.GetPool(key, createFunc, fallbackConfig, onGet, onRelease, onDestroy);
        }

        /// <summary>Acquires an element from the default keyed global pool.</summary>
        public static T Get<T>(Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return Get(PoolKey.Default, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy);
        }

        /// <summary>Acquires an element from a keyed global pool using explicit fallback settings.</summary>
        public static T Get<T>(PoolKey key, Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return GetPool(key, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy).Get();
        }

        /// <summary>Acquires an element from a keyed global pool using a fallback configuration value.</summary>
        public static T Get<T>(PoolKey key, Func<T> createFunc, PoolItemConfig fallbackConfig, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null) where T : class
        {
            return GetPool(key, createFunc, fallbackConfig, onGet, onRelease, onDestroy).Get();
        }

        /// <summary>Acquires a disposable return handle from the default keyed global pool.</summary>
        public static PooledObject<T> GetPooled<T>(Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return GetPooled(PoolKey.Default, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy);
        }

        /// <summary>Acquires a disposable return handle from a keyed global pool using explicit fallback settings.</summary>
        public static PooledObject<T> GetPooled<T>(PoolKey key, Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return GetPool(key, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy).GetPooled();
        }

        /// <summary>Acquires a disposable return handle from a keyed global pool using a fallback configuration value.</summary>
        public static PooledObject<T> GetPooled<T>(PoolKey key, Func<T> createFunc, PoolItemConfig fallbackConfig, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null) where T : class
        {
            return GetPool(key, createFunc, fallbackConfig, onGet, onRelease, onDestroy).GetPooled();
        }

        /// <summary>Returns an element to the default keyed global pool.</summary>
        public static void Release<T>(T element) where T : class
        {
            Release(PoolKey.Default, element);
        }

        /// <summary>Returns an element to a keyed global pool.</summary>
        public static void Release<T>(PoolKey key, T element) where T : class
        {
            PoolRegistry.Global.Release(key, element);
        }

        /// <summary>Attempts to return an element to the default keyed global pool.</summary>
        public static bool TryRelease<T>(T element) where T : class
        {
            return TryRelease(PoolKey.Default, element);
        }

        /// <summary>Attempts to return an element to a keyed global pool.</summary>
        public static bool TryRelease<T>(PoolKey key, T element) where T : class
        {
            return PoolRegistry.Global.TryRelease(key, element);
        }

        /// <summary>Returns an instance to the global pool that most recently acquired it.</summary>
        public static void Release(object element)
        {
            PoolRegistry.Global.Release(element);
        }

        /// <summary>Attempts to return an instance to the global pool that most recently acquired it.</summary>
        public static bool TryRelease(object element)
        {
            return PoolRegistry.Global.TryRelease(element);
        }

        /// <summary>Destroys the default keyed global pool for an element type.</summary>
        public static bool DestroyPool<T>(bool destroy = true) where T : class
        {
            return DestroyPool<T>(PoolKey.Default, destroy);
        }

        /// <summary>Destroys a keyed global pool for an element type.</summary>
        public static bool DestroyPool<T>(PoolKey key, bool destroy = true) where T : class
        {
            return PoolRegistry.Global.DestroyPool<T>(key, destroy);
        }

        /// <summary>Trims all global pools using their configured policies.</summary>
        public static int TrimAll()
        {
            return PoolRegistry.Global.TrimAll();
        }

        /// <summary>Trims all global pools using the specified policy.</summary>
        public static int TrimAll(PoolTrimPolicy policy)
        {
            return PoolRegistry.Global.TrimAll(policy);
        }

        /// <summary>Force-trims all global pools using the specified policy.</summary>
        public static int ForceTrimAll(PoolTrimPolicy policy)
        {
            return PoolRegistry.Global.ForceTrimAll(policy);
        }

        /// <summary>Unregisters every global pool and clears its inactive elements.</summary>
        public static void ClearAll(bool destroy = false)
        {
            PoolRegistry.Global.Clear(destroy);
        }

#if UNITY_EDITOR
        /// <summary>Gets editor-only diagnostic snapshots for all global pools.</summary>
        public static IReadOnlyList<PoolDebugSnapshot> GetDebugSnapshots()
        {
            return PoolRegistry.Global.GetDebugSnapshots();
        }
#endif
    }
}

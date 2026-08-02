using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Pooling
{
    public static class Pools
    {
        public static PoolScope GlobalScope => PoolRegistry.Global;

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

        public static bool UnregisterConfigProvider(IPoolConfigProvider provider)
        {
            return PoolRegistry.UnregisterConfigProvider(provider);
        }

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

        public static bool TryGetScope(string name, out PoolScope scope)
        {
            return PoolRegistry.TryGetScope(name, out scope);
        }

        public static bool DestroyScope(string name, bool destroy = true)
        {
            return PoolRegistry.DestroyScope(name, destroy);
        }

        public static ObjectPool<T> GetPool<T>(Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return GetPool(PoolKey.Default, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy);
        }

        public static ObjectPool<T> GetPool<T>(PoolKey key, Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return PoolRegistry.Global.GetPool(key, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy);
        }

        public static ObjectPool<T> GetPool<T>(ObjectPoolOptions<T> options) where T : class
        {
            return PoolRegistry.Global.GetPool(options);
        }

        public static ObjectPool<T> GetPool<T>(PoolKey key, ObjectPoolOptions<T> options) where T : class
        {
            return PoolRegistry.Global.GetPool(key, options);
        }

        public static ObjectPool<T> GetPool<T>(PoolKey key, Func<T> createFunc, PoolItemConfig fallbackConfig, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null) where T : class
        {
            return PoolRegistry.Global.GetPool(key, createFunc, fallbackConfig, onGet, onRelease, onDestroy);
        }

        public static T Get<T>(Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return Get(PoolKey.Default, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy);
        }

        public static T Get<T>(PoolKey key, Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return GetPool(key, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy).Get();
        }

        public static T Get<T>(PoolKey key, Func<T> createFunc, PoolItemConfig fallbackConfig, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null) where T : class
        {
            return GetPool(key, createFunc, fallbackConfig, onGet, onRelease, onDestroy).Get();
        }

        public static PooledObject<T> GetPooled<T>(Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return GetPooled(PoolKey.Default, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy);
        }

        public static PooledObject<T> GetPooled<T>(PoolKey key, Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return GetPool(key, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy).GetPooled();
        }

        public static PooledObject<T> GetPooled<T>(PoolKey key, Func<T> createFunc, PoolItemConfig fallbackConfig, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null) where T : class
        {
            return GetPool(key, createFunc, fallbackConfig, onGet, onRelease, onDestroy).GetPooled();
        }

        public static void Release<T>(T element) where T : class
        {
            Release(PoolKey.Default, element);
        }

        public static void Release<T>(PoolKey key, T element) where T : class
        {
            PoolRegistry.Global.Release(key, element);
        }

        public static bool TryRelease<T>(T element) where T : class
        {
            return TryRelease(PoolKey.Default, element);
        }

        public static bool TryRelease<T>(PoolKey key, T element) where T : class
        {
            return PoolRegistry.Global.TryRelease(key, element);
        }

        public static void Release(object element)
        {
            PoolRegistry.Global.Release(element);
        }

        public static bool TryRelease(object element)
        {
            return PoolRegistry.Global.TryRelease(element);
        }

        public static bool DestroyPool<T>(bool destroy = true) where T : class
        {
            return DestroyPool<T>(PoolKey.Default, destroy);
        }

        public static bool DestroyPool<T>(PoolKey key, bool destroy = true) where T : class
        {
            return PoolRegistry.Global.DestroyPool<T>(key, destroy);
        }

        public static int TrimAll()
        {
            return PoolRegistry.Global.TrimAll();
        }

        public static int TrimAll(PoolTrimPolicy policy)
        {
            return PoolRegistry.Global.TrimAll(policy);
        }

        public static int ForceTrimAll(PoolTrimPolicy policy)
        {
            return PoolRegistry.Global.ForceTrimAll(policy);
        }

        public static void ClearAll(bool destroy = false)
        {
            PoolRegistry.Global.Clear(destroy);
        }

#if UNITY_EDITOR
        public static IReadOnlyList<PoolDebugSnapshot> GetDebugSnapshots()
        {
            return PoolRegistry.Global.GetDebugSnapshots();
        }
#endif
    }
}

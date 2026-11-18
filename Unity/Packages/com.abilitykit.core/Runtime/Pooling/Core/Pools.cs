using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 提供进程级全局池的便捷访问；需要独立生命周期时使用显式作用域。
    /// </summary>
    public static class Pools
    {
        /// <summary>
        /// 获取进程级全局池作用域；调用方应使用清理入口，避免直接释放共享作用域。
        /// </summary>
        public static PoolScope GlobalScope => PoolRegistry.Global;

        /// <summary>
        /// 获取或创建命名池作用域，委托注册表管理共享实例。
        /// </summary>
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

        /// <summary>
        /// 按引用身份注销配置提供者，不影响已创建的池。
        /// </summary>
        public static bool UnregisterConfigProvider(IPoolConfigProvider provider)
        {
            return PoolRegistry.UnregisterConfigProvider(provider);
        }

        /// <summary>
        /// 移除全部已注册配置提供者，不改变现有池的选项或释放已有句柄。
        /// </summary>
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

        /// <summary>
        /// 尝试获取尚未释放的命名作用域；空名称使用全局名称。
        /// </summary>
        public static bool TryGetScope(string name, out PoolScope scope)
        {
            return PoolRegistry.TryGetScope(name, out scope);
        }

        /// <summary>
        /// 移除并释放命名作用域；全局名称只清理池，不释放保留的全局作用域。
        /// </summary>
        public static bool DestroyScope(string name, bool destroy = true)
        {
            return PoolRegistry.DestroyScope(name, destroy);
        }

        /// <summary>
        /// 以配置链和显式兜底参数获取默认键的全局池。
        /// </summary>
        public static ObjectPool<T> GetPool<T>(Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return GetPool(PoolKey.Default, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy);
        }

        /// <summary>
        /// 以配置链和显式兜底参数获取指定键的全局池；已存在的池不热更新。
        /// </summary>
        public static ObjectPool<T> GetPool<T>(PoolKey key, Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return PoolRegistry.Global.GetPool(key, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy);
        }

        /// <summary>
        /// 以选项获取默认键的全局池；仅首次创建使用选项，不经过配置提供者解析。
        /// </summary>
        public static ObjectPool<T> GetPool<T>(ObjectPoolOptions<T> options) where T : class
        {
            return PoolRegistry.Global.GetPool(options);
        }

        /// <summary>
        /// 以选项获取指定键的全局池；仅首次创建使用选项，不经过配置提供者解析。
        /// </summary>
        public static ObjectPool<T> GetPool<T>(PoolKey key, ObjectPoolOptions<T> options) where T : class
        {
            return PoolRegistry.Global.GetPool(key, options);
        }

        /// <summary>
        /// 以配置链和兜底配置获取指定键的全局池；禁用配置会拒绝调用，已存在的池不热更新。
        /// </summary>
        public static ObjectPool<T> GetPool<T>(PoolKey key, Func<T> createFunc, PoolItemConfig fallbackConfig, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null) where T : class
        {
            return PoolRegistry.Global.GetPool(key, createFunc, fallbackConfig, onGet, onRelease, onDestroy);
        }

        /// <summary>
        /// 从默认键的全局池获取对象，调用方负责归还。
        /// </summary>
        public static T Get<T>(Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return Get(PoolKey.Default, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy);
        }

        /// <summary>
        /// 按显式兜底参数从指定键的全局池获取对象；配置提供者可覆盖兜底参数。
        /// </summary>
        public static T Get<T>(PoolKey key, Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return GetPool(key, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy).Get();
        }

        /// <summary>
        /// 按兜底配置从指定键的全局池获取对象；已存在的池沿用首次创建的选项。
        /// </summary>
        public static T Get<T>(PoolKey key, Func<T> createFunc, PoolItemConfig fallbackConfig, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null) where T : class
        {
            return GetPool(key, createFunc, fallbackConfig, onGet, onRelease, onDestroy).Get();
        }

        /// <summary>
        /// 从默认键的全局池获取归还句柄；句柄及其副本合计只能释放一次。
        /// </summary>
        public static PooledObject<T> GetPooled<T>(Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return GetPooled(PoolKey.Default, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy);
        }

        /// <summary>
        /// 按显式兜底参数从指定键的全局池获取归还句柄；句柄及其副本合计只能释放一次。
        /// </summary>
        public static PooledObject<T> GetPooled<T>(PoolKey key, Func<T> createFunc, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null, int defaultCapacity = 0, int maxSize = 1024, bool collectionCheck = true, PoolTrimPolicy trimPolicy = default) where T : class
        {
            return GetPool(key, createFunc, onGet, onRelease, onDestroy, defaultCapacity, maxSize, collectionCheck, trimPolicy).GetPooled();
        }

        /// <summary>
        /// 按兜底配置从指定键的全局池获取归还句柄；句柄及其副本合计只能释放一次。
        /// </summary>
        public static PooledObject<T> GetPooled<T>(PoolKey key, Func<T> createFunc, PoolItemConfig fallbackConfig, Action<T>? onGet = null, Action<T>? onRelease = null, Action<T>? onDestroy = null) where T : class
        {
            return GetPool(key, createFunc, fallbackConfig, onGet, onRelease, onDestroy).GetPooled();
        }

        /// <summary>
        /// 将对象归还默认键的全局池；空对象不执行操作，归还异常直接传播。
        /// </summary>
        public static void Release<T>(T element) where T : class
        {
            Release(PoolKey.Default, element);
        }

        /// <summary>
        /// 将对象归还指定键的全局池；空对象不执行操作，归还异常直接传播。
        /// </summary>
        public static void Release<T>(PoolKey key, T element) where T : class
        {
            PoolRegistry.Global.Release(key, element);
        }

        /// <summary>
        /// 尝试将对象归还默认键的全局池；空对象视为成功，回调异常直接传播。
        /// </summary>
        public static bool TryRelease<T>(T element) where T : class
        {
            return TryRelease(PoolKey.Default, element);
        }

        /// <summary>
        /// 尝试将对象归还指定键的全局池；未找到池返回 false，归还回调异常直接传播。
        /// </summary>
        public static bool TryRelease<T>(PoolKey key, T element) where T : class
        {
            return PoolRegistry.Global.TryRelease(key, element);
        }

        /// <summary>
        /// 按实例归还到最近一次获取它的已登记全局池；没有记录时抛出异常。
        /// </summary>
        public static void Release(object element)
        {
            PoolRegistry.Global.Release(element);
        }

        /// <summary>
        /// 尝试按实例归还到最近一次获取它的已登记全局池；不会推断其它作用域。
        /// </summary>
        public static bool TryRelease(object element)
        {
            return PoolRegistry.Global.TryRelease(element);
        }

        /// <summary>
        /// 注销默认键的全局池并按参数清理空闲对象。
        /// </summary>
        public static bool DestroyPool<T>(bool destroy = true) where T : class
        {
            return DestroyPool<T>(PoolKey.Default, destroy);
        }

        /// <summary>
        /// 注销指定键的全局池并按参数清理空闲对象；不会使已获取的池引用失效。
        /// </summary>
        public static bool DestroyPool<T>(PoolKey key, bool destroy = true) where T : class
        {
            return PoolRegistry.Global.DestroyPool<T>(key, destroy);
        }

        /// <summary>
        /// 按各池配置的策略裁剪全部全局池；禁止常规裁剪的池保持不变。
        /// </summary>
        public static int TrimAll()
        {
            return PoolRegistry.Global.TrimAll();
        }

        /// <summary>
        /// 按指定策略裁剪全部全局池；禁止常规裁剪的池保持不变。
        /// </summary>
        public static int TrimAll(PoolTrimPolicy policy)
        {
            return PoolRegistry.Global.TrimAll(policy);
        }

        /// <summary>
        /// 按指定策略强制裁剪全部全局池，忽略各池的禁止常规裁剪标志。
        /// </summary>
        public static int ForceTrimAll(PoolTrimPolicy policy)
        {
            return PoolRegistry.Global.ForceTrimAll(policy);
        }

        /// <summary>
        /// 注销全部全局池并清理空闲对象，保留全局作用域可继续使用。
        /// </summary>
        public static void ClearAll(bool destroy = false)
        {
            PoolRegistry.Global.Clear(destroy);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 获取全局池的编辑器诊断快照；各池分别取样，不承诺跨池同一时点。
        /// </summary>
        public static IReadOnlyList<PoolDebugSnapshot> GetDebugSnapshots()
        {
            return PoolRegistry.Global.GetDebugSnapshots();
        }
#endif
    }
}

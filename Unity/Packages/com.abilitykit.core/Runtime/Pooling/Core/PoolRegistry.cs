using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 命名对象池作用域的中心注册表，用于按全局、场景、战斗、UI 或功能域隔离对象池生命周期。
    /// </summary>
    public static class PoolRegistry
    {
        public const string GlobalScopeName = "Global";

        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, PoolScope> Scopes = new Dictionary<string, PoolScope>(StringComparer.Ordinal);
        private static readonly PoolScope GlobalScopeInstance = new PoolScope(GlobalScopeName, destroyOnDispose: false);

        static PoolRegistry()
        {
            Scopes[GlobalScopeName] = GlobalScopeInstance;
        }

        public static PoolScope Global => GlobalScopeInstance;

        /// <summary>
        /// 注册对象池配置提供者，并返回可释放的注册句柄。
        /// </summary>
        /// <param name="provider">要注册的配置提供者。</param>
        /// <returns>用于注销该提供者的注册句柄。</returns>
        public static PoolConfigRegistration RegisterConfigProvider(IPoolConfigProvider provider)
        {
            return PoolConfigCenter.RegisterProvider(provider);
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
            return PoolConfigCenter.RegisterProvider(provider, name, source, priority);
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
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var builder = new PoolConfigBuilder(defaultScopeName, moduleName, source, priority);
            configure(builder);
            var module = builder.Build();
            PoolConfigCenter.RegisterProvider(module, module.Info.Name, module.Info.Source, module.Info.Priority);
            return module;
        }

        public static bool UnregisterConfigProvider(IPoolConfigProvider provider)
        {
            return PoolConfigCenter.UnregisterProvider(provider);
        }

        public static void ClearConfigProviders()
        {
            PoolConfigCenter.ClearProviders();
        }

        /// <summary>
        /// 查询对象池配置。
        /// </summary>
        /// <param name="request">配置查询请求。</param>
        /// <param name="config">命中的配置。</param>
        /// <returns>如果存在生效配置，则返回 <c>true</c>。</returns>
        public static bool TryGetConfig(PoolConfigRequest request, out PoolItemConfig config)
        {
            return PoolConfigCenter.TryGetConfig(request, out config);
        }

        /// <summary>
        /// 查询最终生效的配置快照。
        /// </summary>
        /// <param name="request">配置查询请求。</param>
        /// <param name="snapshot">最终命中的配置快照。</param>
        /// <returns>如果存在生效配置，则返回 <c>true</c>。</returns>
        public static bool TryGetConfigSnapshot(PoolConfigRequest request, out PoolConfigSnapshot snapshot)
        {
            return PoolConfigCenter.TryGetConfigSnapshot(request, out snapshot);
        }

        /// <summary>
        /// 查询配置冲突诊断报告，包含所有匹配候选与最终胜出项。
        /// </summary>
        /// <param name="request">配置查询请求。</param>
        /// <param name="report">配置冲突诊断报告。</param>
        /// <returns>如果至少存在一个匹配候选，则返回 <c>true</c>。</returns>
        public static bool TryGetConfigReport(PoolConfigRequest request, out PoolConfigReport report)
        {
            return PoolConfigCenter.TryGetConfigReport(request, out report);
        }

        /// <summary>
        /// 获取当前已注册配置提供者的诊断信息。
        /// </summary>
        /// <returns>配置提供者诊断信息列表。</returns>
        public static IReadOnlyList<PoolConfigProviderInfo> GetConfigProviderInfos()
        {
            return PoolConfigCenter.GetProviderInfos();
        }

        public static PoolItemConfig GetConfigOrDefault(PoolConfigRequest request, PoolItemConfig fallback = default)
        {
            return PoolConfigCenter.GetConfigOrDefault(request, fallback);
        }

        public static PoolScope GetOrCreateScope(string name, bool destroyOnDispose = true)
        {
            name = NormalizeName(name);

            lock (SyncRoot)
            {
                if (Scopes.TryGetValue(name, out var scope) && scope != null && !scope.IsDisposed)
                {
                    return scope;
                }

                scope = name == GlobalScopeName
                    ? GlobalScopeInstance
                    : new PoolScope(name, destroyOnDispose);

                Scopes[name] = scope;
                return scope;
            }
        }

        public static bool TryGetScope(string name, out PoolScope scope)
        {
            name = NormalizeName(name);

            lock (SyncRoot)
            {
                if (Scopes.TryGetValue(name, out scope) && scope != null && !scope.IsDisposed)
                {
                    return true;
                }

                scope = null;
                return false;
            }
        }

        public static bool DestroyScope(string name, bool destroy = true)
        {
            name = NormalizeName(name);
            if (name == GlobalScopeName)
            {
                GlobalScopeInstance.Clear(destroy);
                return true;
            }

            PoolScope scope;
            lock (SyncRoot)
            {
                if (!Scopes.TryGetValue(name, out scope)) return false;
                Scopes.Remove(name);
            }

            scope?.Dispose(destroy);
            return true;
        }

        public static void ClearAll(bool destroy = false, bool includeGlobal = true)
        {
            List<PoolScope> scopes;
            lock (SyncRoot)
            {
                scopes = new List<PoolScope>(Scopes.Values);
                if (!includeGlobal)
                {
                    scopes.Remove(GlobalScopeInstance);
                }
            }

            for (var i = 0; i < scopes.Count; i++)
            {
                var scope = scopes[i];
                if (scope == null || scope.IsDisposed) continue;
                scope.Clear(destroy);
            }
        }

#if UNITY_EDITOR
        public static IReadOnlyList<PoolDebugSnapshot> GetDebugSnapshots(string scopeName = null)
        {
            if (!string.IsNullOrEmpty(scopeName))
            {
                return TryGetScope(scopeName, out var scope) ? scope.GetDebugSnapshots() : Array.Empty<PoolDebugSnapshot>();
            }

            var result = new List<PoolDebugSnapshot>();
            List<PoolScope> scopes;
            lock (SyncRoot)
            {
                scopes = new List<PoolScope>(Scopes.Values);
            }

            for (var i = 0; i < scopes.Count; i++)
            {
                var scope = scopes[i];
                if (scope == null || scope.IsDisposed) continue;
                result.AddRange(scope.GetDebugSnapshots());
            }

            return result;
        }
#endif

        private static string NormalizeName(string name)
        {
            return string.IsNullOrEmpty(name) ? GlobalScopeName : name;
        }
    }
}

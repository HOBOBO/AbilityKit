using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 对象池创建时使用的中心配置链，配置提供者按优先级和注册顺序进行查询。
    /// </summary>
    public static class PoolConfigCenter
    {
        private static readonly object SyncRoot = new object();
        private static readonly List<ProviderEntry> Providers = new List<ProviderEntry>();
        private static int _nextRegistrationOrder;

        private readonly struct ProviderEntry
        {
            public readonly IPoolConfigProvider Provider;
            public readonly PoolConfigProviderInfo Info;

            public ProviderEntry(IPoolConfigProvider provider, PoolConfigProviderInfo info)
            {
                Provider = provider;
                Info = info;
            }
        }

        /// <summary>
        /// 注册一个对象池配置提供者，并返回可释放的注册句柄。
        /// </summary>
        /// <param name="provider">要注册的配置提供者。</param>
        /// <returns>用于注销该提供者的注册句柄。</returns>
        public static PoolConfigRegistration RegisterProvider(IPoolConfigProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            var baseInfo = provider is IPoolConfigProviderInfo providerInfo
                ? providerInfo.Info
                : new PoolConfigProviderInfo(provider.GetType().Name, provider.GetType().FullName, priority: 0, registrationOrder: 0);

            return RegisterProvider(provider, baseInfo.Name, baseInfo.Source, baseInfo.Priority);
        }

        /// <summary>
        /// 注册一个带诊断元数据的对象池配置提供者，并返回可释放的注册句柄。
        /// </summary>
        /// <param name="provider">要注册的配置提供者。</param>
        /// <param name="name">配置提供者名称，通常使用包名或模块名。</param>
        /// <param name="source">配置来源，通常使用程序集名、包名或资源路径。</param>
        /// <param name="priority">配置优先级；数值越大越优先。</param>
        /// <returns>用于注销该提供者的注册句柄。</returns>
        public static PoolConfigRegistration RegisterProvider(IPoolConfigProvider provider, string name, string? source = null, int priority = 0)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            lock (SyncRoot)
            {
                var existingIndex = IndexOfProvider(provider);
                if (existingIndex >= 0)
                {
                    return new PoolConfigRegistration(provider, Providers[existingIndex].Info);
                }

                var order = ++_nextRegistrationOrder;
                var info = new PoolConfigProviderInfo(name, source, priority, order);
                Providers.Add(new ProviderEntry(provider, info));
                return new PoolConfigRegistration(provider, info);
            }
        }

        /// <summary>
        /// 注销指定对象池配置提供者。
        /// </summary>
        /// <param name="provider">要注销的配置提供者。</param>
        /// <returns>如果找到并移除了提供者，则返回 <c>true</c>。</returns>
        public static bool UnregisterProvider(IPoolConfigProvider provider)
        {
            if (provider == null) return false;

            lock (SyncRoot)
            {
                var index = IndexOfProvider(provider);
                if (index < 0) return false;
                Providers.RemoveAt(index);
                return true;
            }
        }

        /// <summary>
        /// 清空所有对象池配置提供者，通常仅用于测试或编辑器工具重置。
        /// </summary>
        public static void ClearProviders()
        {
            lock (SyncRoot)
            {
                Providers.Clear();
            }
        }

        /// <summary>
        /// 查询最终生效的对象池配置。
        /// </summary>
        /// <param name="request">配置查询请求。</param>
        /// <param name="config">命中的最终配置。</param>
        /// <returns>如果存在生效配置，则返回 <c>true</c>。</returns>
        public static bool TryGetConfig(PoolConfigRequest request, out PoolItemConfig config)
        {
            return TryGetConfigSnapshot(request, out var snapshot)
                ? ReturnConfig(snapshot, out config)
                : ReturnUnspecified(out config);
        }

        /// <summary>
        /// 查询最终生效的配置快照，包含命中配置及其提供者来源。
        /// </summary>
        /// <param name="request">配置查询请求。</param>
        /// <param name="snapshot">最终命中的配置快照。</param>
        /// <returns>如果存在生效配置，则返回 <c>true</c>。</returns>
        public static bool TryGetConfigSnapshot(PoolConfigRequest request, out PoolConfigSnapshot snapshot)
        {
            if (TryGetConfigReport(request, out var report) && report.HasWinner)
            {
                snapshot = new PoolConfigSnapshot(request, report.Winner.Config, report.Winner.Provider);
                return true;
            }

            snapshot = default;
            return false;
        }

        /// <summary>
        /// 查询对象池配置冲突报告，报告会列出所有匹配候选以及最终胜出的配置。
        /// </summary>
        /// <param name="request">配置查询请求。</param>
        /// <param name="report">配置冲突诊断报告。</param>
        /// <returns>如果至少存在一个匹配候选，则返回 <c>true</c>。</returns>
        public static bool TryGetConfigReport(PoolConfigRequest request, out PoolConfigReport report)
        {
            lock (SyncRoot)
            {
                PoolConfigMatch winner = default;
                var hasWinner = false;
                var matches = new List<PoolConfigMatch>();

                for (var i = 0; i < Providers.Count; i++)
                {
                    var entry = Providers[i];
                    if (entry.Provider == null || !entry.Provider.TryGetConfig(request, out var config) || !config.IsSpecified)
                    {
                        continue;
                    }

                    var candidate = new PoolConfigMatch(request, config, entry.Info, isWinner: false);
                    matches.Add(candidate);

                    if (!hasWinner || ShouldOverride(winner.Provider, entry.Info))
                    {
                        winner = candidate;
                        hasWinner = true;
                    }
                }

                if (!hasWinner)
                {
                    report = default;
                    return false;
                }

                for (var i = 0; i < matches.Count; i++)
                {
                    var match = matches[i];
                    if (match.Provider.RegistrationOrder == winner.Provider.RegistrationOrder)
                    {
                        matches[i] = new PoolConfigMatch(match.Request, match.Config, match.Provider, isWinner: true);
                        break;
                    }
                }

                report = new PoolConfigReport(request, matches, winner);
                return true;
            }
        }

        /// <summary>
        /// 获取当前已注册的配置提供者诊断信息快照。
        /// </summary>
        /// <returns>配置提供者诊断信息列表。</returns>
        public static IReadOnlyList<PoolConfigProviderInfo> GetProviderInfos()
        {
            lock (SyncRoot)
            {
                var result = new List<PoolConfigProviderInfo>(Providers.Count);
                for (var i = 0; i < Providers.Count; i++)
                {
                    result.Add(Providers[i].Info);
                }

                return result;
            }
        }

        /// <summary>
        /// 查询对象池配置；未命中时返回指定兜底配置。
        /// </summary>
        /// <param name="request">配置查询请求。</param>
        /// <param name="fallback">未命中时使用的兜底配置。</param>
        /// <returns>命中的配置或兜底配置。</returns>
        public static PoolItemConfig GetConfigOrDefault(PoolConfigRequest request, PoolItemConfig fallback = default)
        {
            return TryGetConfig(request, out var config) ? config : fallback;
        }

        private static bool ShouldOverride(PoolConfigProviderInfo current, PoolConfigProviderInfo candidate)
        {
            if (candidate.Priority != current.Priority)
            {
                return candidate.Priority > current.Priority;
            }

            return candidate.RegistrationOrder > current.RegistrationOrder;
        }

        private static int IndexOfProvider(IPoolConfigProvider provider)
        {
            for (var i = 0; i < Providers.Count; i++)
            {
                if (ReferenceEquals(Providers[i].Provider, provider)) return i;
            }

            return -1;
        }

        private static bool ReturnConfig(PoolConfigSnapshot snapshot, out PoolItemConfig config)
        {
            config = snapshot.Config;
            return true;
        }

        private static bool ReturnUnspecified(out PoolItemConfig config)
        {
            config = PoolItemConfig.Unspecified;
            return false;
        }
    }
}

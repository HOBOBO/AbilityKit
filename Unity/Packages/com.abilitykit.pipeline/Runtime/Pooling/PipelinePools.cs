using System;
using System.Collections.Generic;
using AbilityKit.Core.Pooling;

namespace AbilityKit.Pipeline.Pooling
{
    /// <summary>
    /// 管线包对象池入口，集中管理管线高频临时容器的池键、默认配置与业务覆盖配置。
    /// </summary>
    public static class PipelinePools
    {
        /// <summary>
        /// 管线包默认对象池作用域名称。
        /// </summary>
        public const string ScopeName = "AbilityKit.Pipeline";

        /// <summary>
        /// 管线包对象池配置模块名称。
        /// </summary>
        public const string ConfigModuleName = "AbilityKit.Pipeline.Pools";

        /// <summary>
        /// 生命周期拥有者查询结果列表对象池键。
        /// </summary>
        public static readonly PoolKey LifeOwnerListKey = new PoolKey("PipelineRegistry.LifeOwnerList");

        private static readonly object SyncRoot = new object();
        private static PoolConfigModule? _defaultConfigModule;

        /// <summary>
        /// 注册管线包默认对象池配置。项目侧可用更高优先级配置覆盖这些默认值。
        /// </summary>
        public static PoolConfigModule RegisterDefaultConfig()
        {
            lock (SyncRoot)
            {
                if (_defaultConfigModule != null)
                {
                    return _defaultConfigModule;
                }

                _defaultConfigModule = Pools.RegisterConfigModule(
                    config => config
                        .Pool<List<IPipelineLifeOwner>>(ScopeName, defaultCapacity: 4, maxSize: 64, prewarmCount: 4, collectionCheck: true, key: LifeOwnerListKey),
                    defaultScopeName: ScopeName,
                    moduleName: ConfigModuleName,
                    source: "AbilityKit.Pipeline",
                    priority: 0);

                return _defaultConfigModule;
            }
        }

        /// <summary>
        /// 注册业务侧管线对象池配置覆盖。业务包可在启动阶段传入更高优先级配置。
        /// </summary>
        public static PoolConfigRegistration RegisterOverride(
            Action<PoolConfigBuilder> configure,
            string moduleName,
            string? source = null,
            int priority = 100)
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            var builder = new PoolConfigBuilder(ScopeName, moduleName, source ?? moduleName, priority);
            configure(builder);
            var module = builder.Build();
            return Pools.RegisterConfigProvider(module, module.Info.Name, module.Info.Source, module.Info.Priority);
        }

        /// <summary>
        /// 为业务侧创建默认作用域为管线的对象池配置构建器。
        /// </summary>
        public static PoolConfigBuilder CreateConfigBuilder(string moduleName, string? source = null, int priority = 100)
        {
            return new PoolConfigBuilder(ScopeName, moduleName, source ?? moduleName, priority);
        }

        /// <summary>
        /// 租借生命周期拥有者查询结果列表。
        /// </summary>
        public static List<IPipelineLifeOwner> RentLifeOwnerList()
        {
            return Scope.Get(
                LifeOwnerListKey,
                () => new List<IPipelineLifeOwner>(16),
                PoolItemConfig.Default(defaultCapacity: 4, maxSize: 64, prewarmCount: 4, collectionCheck: true),
                onGet: list => list.Clear());
        }

        /// <summary>
        /// 释放生命周期拥有者查询结果列表。
        /// </summary>
        public static void ReleaseLifeOwnerList(List<IPipelineLifeOwner> list)
        {
            if (list == null) return;
            list.Clear();
            Scope.Release(LifeOwnerListKey, list);
        }

        /// <summary>
        /// 租借单次运行阶段列表。
        /// </summary>
        public static List<IAbilityPipelinePhase<TCtx>> RentRunPhaseList<TCtx>()
            where TCtx : IAbilityPipelineContext
        {
            var key = GetRunPhaseListKey<TCtx>();
            return Scope.Get(
                key,
                () => new List<IAbilityPipelinePhase<TCtx>>(8),
                PoolItemConfig.Default(defaultCapacity: 8, maxSize: 256, prewarmCount: 8, collectionCheck: true),
                onGet: list => list.Clear());
        }

        /// <summary>
        /// 释放单次运行阶段列表。
        /// </summary>
        public static void ReleaseRunPhaseList<TCtx>(List<IAbilityPipelinePhase<TCtx>> list)
            where TCtx : IAbilityPipelineContext
        {
            if (list == null) return;
            list.Clear();
            Scope.Release(GetRunPhaseListKey<TCtx>(), list);
        }

        private static PoolKey GetRunPhaseListKey<TCtx>()
        {
            return RunPhaseListPoolKey<TCtx>.Value;
        }

        private static class RunPhaseListPoolKey<TCtx>
        {
            public static readonly PoolKey Value = new PoolKey("Pipeline.RunPhaseList." + typeof(TCtx).FullName);
        }

        private static PoolScope Scope => Pools.GetOrCreateScope(ScopeName, destroyOnDispose: false);
    }
}

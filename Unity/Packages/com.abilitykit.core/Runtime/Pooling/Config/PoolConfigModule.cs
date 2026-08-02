using System;
using System.Collections.Generic;

namespace AbilityKit.Core.Pooling
{
    /// <summary>
    /// 基于字典存储的强类型对象池配置提供者。
    /// 外部包可以构建模块，并将其注册到 <see cref="PoolConfigCenter" />。
    /// </summary>
    public sealed class PoolConfigModule : IPoolConfigProvider, IPoolConfigProviderInfo
    {
        private readonly Dictionary<PoolConfigRequest, PoolItemConfig> _configs;

        internal PoolConfigModule(Dictionary<PoolConfigRequest, PoolItemConfig> configs, PoolConfigProviderInfo info)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
            Info = info;
        }

        /// <summary>
        /// 获取该配置模块的诊断信息。
        /// </summary>
        public PoolConfigProviderInfo Info { get; }

        /// <summary>
        /// 获取该模块中声明的全部对象池配置。
        /// </summary>
        public IReadOnlyDictionary<PoolConfigRequest, PoolItemConfig> Configs => _configs;

        /// <inheritdoc />
        public bool TryGetConfig(PoolConfigRequest request, out PoolItemConfig config)
        {
            request = new PoolConfigRequest(request.ScopeName, request.ElementType, request.Key);
            return _configs.TryGetValue(request, out config) && config.IsSpecified;
        }
    }

    /// <summary>
    /// 用于包级或项目级对象池配置注册的强类型构建器。
    /// </summary>
    public sealed class PoolConfigBuilder
    {
        private readonly string _defaultScopeName;
        private readonly string _moduleName;
        private readonly string _source;
        private readonly int _priority;
        private readonly Dictionary<PoolConfigRequest, PoolItemConfig> _configs = new Dictionary<PoolConfigRequest, PoolItemConfig>();

        /// <summary>
        /// 创建构建器，并可指定默认作用域、模块名、来源和优先级。
        /// </summary>
        public PoolConfigBuilder(
            string? defaultScopeName = null,
            string? moduleName = null,
            string? source = null,
            int priority = 0)
        {
            _defaultScopeName = string.IsNullOrEmpty(defaultScopeName) ? PoolRegistry.GlobalScopeName : defaultScopeName;
            _moduleName = string.IsNullOrEmpty(moduleName) ? _defaultScopeName : moduleName;
            _source = source ?? string.Empty;
            _priority = priority;
        }

        /// <summary>
        /// 在默认作用域中注册强类型对象池配置。
        /// </summary>
        public PoolConfigBuilder Add<T>(
            PoolItemConfig config,
            PoolKey key = default) where T : class
        {
            return Add<T>(_defaultScopeName, key, config);
        }

        /// <summary>
        /// 在命名作用域中注册强类型对象池配置。
        /// </summary>
        public PoolConfigBuilder Add<T>(
            string scopeName,
            PoolKey key,
            PoolItemConfig config) where T : class
        {
            if (!config.IsSpecified) throw new ArgumentException("Pool config must be specified.", nameof(config));
            _configs[new PoolConfigRequest(scopeName, typeof(T), key)] = config;
            return this;
        }

        /// <summary>
        /// 使用常用容量参数在默认作用域中注册强类型对象池配置。
        /// </summary>
        public PoolConfigBuilder Add<T>(
            int defaultCapacity,
            int maxSize,
            int prewarmCount = -1,
            bool collectionCheck = true,
            PoolTrimPolicy trimPolicy = default,
            bool neverTrim = false,
            PoolKey key = default) where T : class
        {
            return Add<T>(PoolItemConfig.Default(defaultCapacity, maxSize, prewarmCount, collectionCheck, trimPolicy, neverTrim), key);
        }

        /// <summary>
        /// 使用常用容量参数在命名作用域中注册强类型对象池配置。
        /// </summary>
        public PoolConfigBuilder Add<T>(
            string scopeName,
            PoolKey key,
            int defaultCapacity,
            int maxSize,
            int prewarmCount = -1,
            bool collectionCheck = true,
            PoolTrimPolicy trimPolicy = default,
            bool neverTrim = false) where T : class
        {
            return Add<T>(scopeName, key, PoolItemConfig.Default(defaultCapacity, maxSize, prewarmCount, collectionCheck, trimPolicy, neverTrim));
        }

        /// <summary>
        /// 在默认作用域中禁用强类型对象池。
        /// </summary>
        public PoolConfigBuilder Disable<T>(PoolKey key = default) where T : class
        {
            return Add<T>(PoolItemConfig.Disabled, key);
        }

        /// <summary>
        /// 在命名作用域中禁用强类型对象池。
        /// </summary>
        public PoolConfigBuilder Disable<T>(string scopeName, PoolKey key = default) where T : class
        {
            return Add<T>(scopeName, key, PoolItemConfig.Disabled);
        }

        /// <summary>
        /// 构建可注册到对象池配置中心的不可变配置提供模块。
        /// </summary>
        public PoolConfigModule Build()
        {
            var info = new PoolConfigProviderInfo(_moduleName, _source, _priority, registrationOrder: 0);
            return new PoolConfigModule(new Dictionary<PoolConfigRequest, PoolItemConfig>(_configs), info);
        }
    }
}

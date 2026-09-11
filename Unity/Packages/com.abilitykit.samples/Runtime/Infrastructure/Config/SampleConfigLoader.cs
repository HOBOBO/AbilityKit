#nullable enable

using System;
using AbilityKit.Samples.Abstractions;
using System.Collections.Generic;

namespace AbilityKit.Samples.Logic.Infrastructure.Config
{
    /// <summary>
    /// 绀轰緥閰嶇疆鍔犺浇鍣?- 缁熶竴绠＄悊鎵€鏈夌ず渚嬮厤缃殑鍔犺浇
    /// 浣跨敤 IResourceProvider 鎶借薄璧勬簮閰嶇疆锛屾敮鎸佷笉鍚屽钩鍙?    /// </summary>
    public sealed class SampleConfigLoader
    {
        private static SampleConfigLoader _instance;
        private readonly Dictionary<string, JsonConfigProvider> _configs = new();
        private readonly IResourceProvider _resourceProvider;

        public static SampleConfigLoader Instance => _instance ??= new SampleConfigLoader();

        /// <summary>
        /// 使用默认资源提供者初始化
        /// </summary>
        private SampleConfigLoader() : this(ResourceProviders.Current)
        {
        }

        /// <summary>
        /// 使用指定的资源提供者初始化
        /// </summary>
        public SampleConfigLoader(IResourceProvider resourceProvider)
        {
            _resourceProvider = resourceProvider;
        }

        /// <summary>
        /// 加载配置文件
        /// </summary>
        public JsonConfigProvider Load(string configName)
        {
            if (_configs.TryGetValue(configName, out var existing))
            {
                return existing;
            }

            var content = _resourceProvider.LoadText(configName);
            var provider = JsonConfigProvider.FromString(content);
            _configs[configName] = provider;
            return provider;
        }

        /// <summary>
        /// 鍔犺浇閰嶇疆锛堝鏋滄枃浠朵笉瀛樺湪鍒欒繑鍥炵┖閰嶇疆锛?        /// </summary>
        public JsonConfigProvider LoadOrEmpty(string configName)
        {
            if (_configs.TryGetValue(configName, out var existing))
            {
                return existing;
            }

            if (!_resourceProvider.TryLoadText(configName, out var content))
            {
                var emptyProvider = JsonConfigProvider.FromString("{}");
                _configs[configName] = emptyProvider;
                return emptyProvider;
            }

            var provider = JsonConfigProvider.FromString(content);
            _configs[configName] = provider;
            return provider;
        }

        /// <summary>
        /// 浠庡祵鍏ヨ祫婧愬姞杞介厤缃?        /// </summary>
        public JsonConfigProvider LoadFromString(string json, string name = "inline")
        {
            if (_configs.TryGetValue(name, out var existing))
            {
                return existing;
            }

            var provider = JsonConfigProvider.FromString(json);
            _configs[name] = provider;
            return provider;
        }

        /// <summary>
        /// 卸载配置
        /// </summary>
        public void Unload(string configName)
        {
            if (_configs.TryGetValue(configName, out var provider))
            {
                provider.Dispose();
                _configs.Remove(configName);
            }
        }

        /// <summary>
        /// 鍗歌浇鎵€鏈夐厤缃?        /// </summary>
        public void UnloadAll()
        {
            foreach (var kvp in _configs)
            {
                kvp.Value.Dispose();
            }
            _configs.Clear();
        }
    }
}

#if UNITY_5_3_OR_NEWER || UNITY_5
#define IS_UNITY
#endif

using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Config
{
    /// <summary>
    /// 泛型配置组，简化配置组创建
    /// </summary>
    public sealed class ConfigGroup : IConfigGroup
    {
        public string Name { get; }
        public IConfigGroupLoader Loader { get; }
        public IConfigGroupDeserializer Deserializer { get; }
        public IReadOnlyList<ConfigTableDefinition> Tables { get; }

        public ConfigGroup(string name, IConfigGroupLoader loader, IConfigGroupDeserializer deserializer, params ConfigTableDefinition[] tables)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Loader = loader ?? throw new ArgumentNullException(nameof(loader));
            Deserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
            Tables = tables != null ? tables : throw new ArgumentNullException(nameof(tables));
        }

        public ConfigGroup(string name, string resourcesDir, IConfigGroupDeserializer deserializer, params ConfigTableDefinition[] tables)
            : this(name, new ResourcesTextConfigGroupLoader(resourcesDir), deserializer, tables)
        {
        }
    }

    /// <summary>
    /// 基于 Resources 的文本配置组加载器
    /// </summary>
    public sealed class ResourcesTextConfigGroupLoader : IConfigGroupLoader
    {
        public string ResourcesDir { get; }

        public ResourcesTextConfigGroupLoader(string resourcesDir)
        {
            ResourcesDir = resourcesDir ?? string.Empty;
        }

        public bool TryLoad(string tableName, out byte[] bytes, out string text)
        {
            bytes = null;
            text = null;

            var path = string.IsNullOrEmpty(ResourcesDir)
                ? tableName
                : $"{ResourcesDir}/{tableName}";

            var asset = TryLoadAsset(path);
            if (asset == null)
            {
                asset = TryLoadAsset(tableName);
            }

            if (asset != null && !string.IsNullOrEmpty(asset.text))
            {
                text = asset.text;
                return true;
            }

            return false;
        }

#if IS_UNITY
        private static UnityEngine.TextAsset TryLoadAsset(string path)
        {
            return UnityEngine.Resources.Load<UnityEngine.TextAsset>(path);
        }
#else
        private static UnityEngine.TextAsset TryLoadAsset(string path)
        {
            return null;
        }
#endif
    }

    /// <summary>
    /// 基于 Resources 的二进制配置组加载器
    /// </summary>
    public sealed class ResourcesBytesConfigGroupLoader : IConfigGroupLoader
    {
        public string ResourcesDir { get; }

        public ResourcesBytesConfigGroupLoader(string resourcesDir)
        {
            ResourcesDir = resourcesDir ?? string.Empty;
        }

        public bool TryLoad(string tableName, out byte[] bytes, out string text)
        {
            bytes = null;
            text = null;

            var path = string.IsNullOrEmpty(ResourcesDir)
                ? tableName
                : $"{ResourcesDir}/{tableName}";

            var asset = TryLoadAsset(path);
            if (asset == null)
            {
                asset = TryLoadAsset(tableName);
            }

            if (asset != null && asset.bytes != null && asset.bytes.Length > 0)
            {
                bytes = asset.bytes;
                return true;
            }

            return false;
        }

#if IS_UNITY
        private static UnityEngine.TextAsset TryLoadAsset(string path)
        {
            return UnityEngine.Resources.Load<UnityEngine.TextAsset>(path);
        }
#else
        private static UnityEngine.TextAsset TryLoadAsset(string path)
        {
            return null;
        }
#endif
    }
}

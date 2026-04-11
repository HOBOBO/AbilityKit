#if UNITY_5_3_OR_NEWER || UNITY_5
#define IS_UNITY
#endif

using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Config
{
    /// <summary>
    /// 基于 Resources 的文本配置组加载器（Unity 专用）
    /// </summary>
    public sealed class ResourcesTextConfigGroupLoader : DefaultConfigGroupLoader
    {
        public ResourcesTextConfigGroupLoader(string resourcesDir) : base(resourcesDir)
        {
        }

        public override bool TryLoad(string tableName, out byte[] bytes, out string text)
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
    /// 基于 Resources 的二进制配置组加载器（Unity 专用）
    /// </summary>
    public sealed class ResourcesBytesConfigGroupLoader : DefaultConfigGroupLoader
    {
        public ResourcesBytesConfigGroupLoader(string resourcesDir) : base(resourcesDir)
        {
        }

        public override bool TryLoad(string tableName, out byte[] bytes, out string text)
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

    public static class UnityConfigGroupFactory
    {
        public static ConfigGroup CreateWithResources(string name, string resourcesDir, IConfigGroupDeserializer deserializer, params ConfigTableDefinition[] tables)
        {
            return new ConfigGroup(name, new ResourcesTextConfigGroupLoader(resourcesDir), deserializer, tables);
        }

        public static ConfigGroup CreateWithBytesResources(string name, string resourcesDir, IConfigGroupDeserializer deserializer, params ConfigTableDefinition[] tables)
        {
            return new ConfigGroup(name, new ResourcesBytesConfigGroupLoader(resourcesDir), deserializer, tables);
        }
    }
}

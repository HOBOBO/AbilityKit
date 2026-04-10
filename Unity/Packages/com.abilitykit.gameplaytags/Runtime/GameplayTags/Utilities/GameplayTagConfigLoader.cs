using System;
using System.Collections.Generic;

namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 标签配置数据接口（核心层定义）
    /// </summary>
    public interface ITagConfigData
    {
        string Name { get; }
        string Description { get; }
        string Category { get; }
    }

    /// <summary>
    /// 标签配置加载接口（核心层定义）
    /// </summary>
    public interface ITagConfigLoader
    {
        void LoadFromData(IEnumerable<ITagConfigData> data);
    }

    /// <summary>
    /// 标签配置项（核心层数据结构）
    /// </summary>
    [Serializable]
    public struct TagConfigEntry : ITagConfigData
    {
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string Category { get; private set; }

        public TagConfigEntry(string name, string description = "", string category = "")
        {
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            Category = category ?? string.Empty;
        }
    }

    /// <summary>
    /// 运行时配置加载器核心（核心层）。
    /// 仅定义接口，不依赖任何序列化实现。
    /// 业务层通过 SetLoader 注入具体实现。
    /// </summary>
    public static class GameplayTagConfigLoader
    {
        private static ITagConfigLoader _loader;

        /// <summary>
        /// 设置配置加载器实现
        /// </summary>
        public static void SetLoader(ITagConfigLoader loader)
        {
            _loader = loader;
        }

        /// <summary>
        /// 从配置数据加载并注册标签
        /// </summary>
        public static void LoadFromData(IEnumerable<ITagConfigData> data)
        {
            if (data == null) return;
            _loader?.LoadFromData(data);
        }

        /// <summary>
        /// 从配置数据加载并注册标签
        /// </summary>
        public static void LoadFromData(IEnumerable<TagConfigEntry> data)
        {
            if (data == null) return;
            LoadFromData((IEnumerable<ITagConfigData>)data);
        }

        /// <summary>
        /// 从简单标签名称列表加载并注册标签
        /// </summary>
        public static void LoadFromNames(IEnumerable<string> tagNames)
        {
            if (tagNames == null) return;

            var manager = GameplayTagManager.Instance;
            foreach (var name in tagNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                manager.RequestTag(name);
            }
        }
    }
}

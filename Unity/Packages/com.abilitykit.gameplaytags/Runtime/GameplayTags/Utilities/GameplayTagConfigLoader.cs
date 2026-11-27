using System;
using System.Collections.Generic;

namespace AbilityKit.GameplayTags
{
    /// <summary>
    /// 供外部加载器读取的标签配置条目，不依赖资产格式。
    /// </summary>
    public interface ITagConfigData
    {
        /// <summary>
        /// 获取待加载的标签名称，格式由加载器校验。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 获取配置中的说明文本。
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 获取配置中的可选分类，不参与标签层级匹配。
        /// </summary>
        string Category { get; }
    }

    /// <summary>
    /// 由宿主提供的配置加载策略。
    /// </summary>
    public interface ITagConfigLoader
    {
        /// <summary>
        /// 消费配置序列并执行宿主的校验与注册策略；序列可能为延迟枚举。
        /// </summary>
        void LoadFromData(IEnumerable<ITagConfigData> data);
    }

    /// <summary>
    /// 无资产依赖的标签配置值，可在宿主加载层与运行时之间传递。
    /// </summary>
    [Serializable]
    public struct TagConfigEntry : ITagConfigData
    {
        /// <summary>
        /// 获取传入的原始标签名称。
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// 获取传入的说明文本。
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// 获取传入的分类文本。
        /// </summary>
        public string Category { get; private set; }

        /// <summary>
        /// 创建配置项，将空引用字段归为空字符串，不注册标签。
        /// </summary>
        public TagConfigEntry(string name, string description = "", string category = "")
        {
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            Category = category ?? string.Empty;
        }
    }

    /// <summary>
    /// 将配置条目转交给宿主的加载器，并提供不依赖宿主的名称注册入口。
    /// </summary>
    public static class GameplayTagConfigLoader
    {
        private static ITagConfigLoader _loader;

        /// <summary>
        /// 在宿主装配阶段替换全局配置加载器；传入空引用可清除加载器。
        /// </summary>
        public static void SetLoader(ITagConfigLoader loader)
        {
            _loader = loader;
        }

        /// <summary>
        /// 原样转交配置序列；未设置加载器或序列为空引用时不执行操作。
        /// </summary>
        public static void LoadFromData(IEnumerable<ITagConfigData> data)
        {
            if (data != null)
            {
                _loader?.LoadFromData(data);
            }
        }

        /// <summary>
        /// 按输入顺序将值类型配置项逐条适配给加载器，不预先枚举。
        /// </summary>
        public static void LoadFromData(IEnumerable<TagConfigEntry> data)
        {
            if (data != null)
            {
                _loader?.LoadFromData(AsConfigData(data));
            }
        }

        /// <summary>
        /// 不经宿主加载器直接按名称注册。跳过空白名称，遇到其他非法名称时中断，已注册项不回退。
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

        private static IEnumerable<ITagConfigData> AsConfigData(IEnumerable<TagConfigEntry> entries)
        {
            foreach (var entry in entries)
            {
                yield return entry;
            }
        }
    }
}

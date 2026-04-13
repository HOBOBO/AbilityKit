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
            : this(name, CreateDefaultLoader(resourcesDir), deserializer, tables)
        {
        }

        private static IConfigGroupLoader CreateDefaultLoader(string resourcesDir)
        {
            return new DefaultConfigGroupLoader(resourcesDir);
        }
    }

    /// <summary>
    /// 默认配置组加载器，无 Unity 依赖
    /// </summary>
    public class DefaultConfigGroupLoader : IConfigGroupLoader
    {
        public string ResourcesDir { get; }

        public DefaultConfigGroupLoader(string resourcesDir)
        {
            ResourcesDir = resourcesDir ?? string.Empty;
        }

        public virtual bool TryLoad(string tableName, out byte[] bytes, out string text)
        {
            bytes = null;
            text = null;
            return false;
        }
    }
}

using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Config
{
    /// <summary>
    /// 配置表定义，描述单个配置表的元数据
    /// </summary>
    public sealed class ConfigTableDefinition
    {
        /// <summary>
        /// 配置文件路径（不含扩展名）
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// 配置文件路径（不含扩展名）- FilePath 的别名，兼容旧 API
        /// </summary>
        public string FileWithoutExt => FilePath;

        /// <summary>
        /// DTO 类型（原始数据类型）
        /// </summary>
        public Type DtoType { get; }

        /// <summary>
        /// 入口类型（运行时使用的数据对象类型）
        /// </summary>
        public Type EntryType { get; }

        public ConfigTableDefinition(string filePath, Type dtoType, Type entryType)
        {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            DtoType = dtoType ?? throw new ArgumentNullException(nameof(dtoType));
            EntryType = entryType ?? throw new ArgumentNullException(nameof(entryType));
        }
    }

    /// <summary>
    /// 配置表注册器接口，定义系统中所有可用的配置表
    /// </summary>
    public interface IConfigTableRegistry
    {
        /// <summary>
        /// 获取所有配置表定义
        /// </summary>
        IReadOnlyList<ConfigTableDefinition> Tables { get; }

        /// <summary>
        /// 根据文件路径获取配置表定义
        /// </summary>
        ConfigTableDefinition GetTable(string filePath);

        /// <summary>
        /// 尝试获取配置表定义
        /// </summary>
        bool TryGetTable(string filePath, out ConfigTableDefinition definition);
    }

    /// <summary>
    /// 配置表注册器基类，提供通用实现
    /// </summary>
    public abstract class ConfigTableRegistryBase : IConfigTableRegistry
    {
        private readonly Dictionary<string, ConfigTableDefinition> _byPath;
        private readonly List<ConfigTableDefinition> _tables;

        protected ConfigTableRegistryBase(IEnumerable<ConfigTableDefinition> tables)
        {
            _byPath = new Dictionary<string, ConfigTableDefinition>(StringComparer.Ordinal);
            _tables = new List<ConfigTableDefinition>();
            if (tables != null)
            {
                foreach (var table in tables)
                {
                    _byPath[table.FilePath] = table;
                    _tables.Add(table);
                }
            }
        }

        public IReadOnlyList<ConfigTableDefinition> Tables => _tables;

        public ConfigTableDefinition GetTable(string filePath)
        {
            return _byPath.TryGetValue(filePath, out var definition) ? definition : null;
        }

        public bool TryGetTable(string filePath, out ConfigTableDefinition definition)
        {
            return _byPath.TryGetValue(filePath, out definition);
        }
    }
}

using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Config
{
    /// <summary>
    /// 配置表定义，描述单个配置表的元数据
    /// </summary>
    public class ConfigTableDefinition
    {
        /// <summary>
        /// 配置文件路径（不含扩展名）
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// 配置文件路径（不含扩展名）- FilePath 的别名
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

        /// <summary>
        /// 所属配置组名称（可选，用于分组加载）
        /// </summary>
        public string GroupName { get; }

        /// <summary>
        /// Optional generated factory for the strongly typed DTO table.
        /// </summary>
        public Func<Array, object> DtoTableFactory { get; }

        /// <summary>
        /// Optional generated factory for the strongly typed runtime entry table.
        /// </summary>
        public Func<Array, object> EntryTableFactory { get; }

        /// <summary>
        /// Optional generated collector for changed DTO keys.
        /// </summary>
        public Action<Array, ISet<int>> ChangedIdCollector { get; }

        public ConfigTableDefinition(string filePath, Type dtoType, Type entryType, string groupName = null)
            : this(filePath, dtoType, entryType, groupName, null, null)
        {
        }

        public ConfigTableDefinition(
            string filePath,
            Type dtoType,
            Type entryType,
            string groupName,
            Func<Array, object> dtoTableFactory,
            Func<Array, object> entryTableFactory)
            : this(
                filePath,
                dtoType,
                entryType,
                groupName,
                dtoTableFactory,
                entryTableFactory,
                null)
        {
        }

        public ConfigTableDefinition(
            string filePath,
            Type dtoType,
            Type entryType,
            string groupName,
            Func<Array, object> dtoTableFactory,
            Func<Array, object> entryTableFactory,
            Action<Array, ISet<int>> changedIdCollector)
        {
            if ((dtoTableFactory == null) != (entryTableFactory == null))
            {
                throw new ArgumentException(
                    "DTO and entry table factories must either both be provided or both be null.");
            }

            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            DtoType = dtoType ?? throw new ArgumentNullException(nameof(dtoType));
            EntryType = entryType ?? throw new ArgumentNullException(nameof(entryType));
            GroupName = groupName;
            DtoTableFactory = dtoTableFactory;
            EntryTableFactory = entryTableFactory;
            ChangedIdCollector = changedIdCollector;
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

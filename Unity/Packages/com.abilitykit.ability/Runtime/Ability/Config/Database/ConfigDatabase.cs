using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Config
{
    /// <summary>
    /// DTO 表接口，用于存储原始 DTO 对象
    /// </summary>
    public interface IDtoTable<TDto> where TDto : class
    {
        int Count { get; }
        TDto Get(int id);
        bool TryGet(int id, out TDto dto);
        IEnumerable<TDto> All();
    }

    /// <summary>
    /// 通用配置数据库实现
    /// </summary>
    public partial class ConfigDatabase : IConfigDatabase
    {
        private const string DefaultKey = "config";

        private readonly IConfigTableRegistry _registry;
        private readonly IConfigDeserializer _deserializer;
        // 使用 TypeNameComparer 规避 IL2CPP 下 Type.Equals() 身份判断不稳定的问题。
        private readonly Dictionary<Type, object> _tables = new Dictionary<Type, object>(TypeNameComparer.Instance);
        private readonly Dictionary<Type, object> _dtoTables = new Dictionary<Type, object>(TypeNameComparer.Instance);
        private long _version;

        public long Version => _version;

        public ConfigDatabase(IConfigTableRegistry registry, IConfigDeserializer deserializer)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _deserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
        }

        public IConfigTable<TEntry> GetTable<TEntry>() where TEntry : class
        {
            if (_tables.TryGetValue(typeof(TEntry), out var obj) && obj is IConfigTable<TEntry> table)
            {
                return table;
            }

            table = CreateTable<TEntry>();
            _tables[typeof(TEntry)] = table;
            return table;
        }

        public bool TryGetTable<TEntry>(out IConfigTable<TEntry> table) where TEntry : class
        {
            if (_tables.TryGetValue(typeof(TEntry), out var obj) && obj is IConfigTable<TEntry> t)
            {
                table = t;
                return true;
            }
            table = null;
            return false;
        }

        private IntKeyConfigTable<TEntry> CreateTable<TEntry>() where TEntry : class
        {
            return new IntKeyConfigTable<TEntry>();
        }

        #region DtoTable Support

        /// <summary>
        /// 获取 DTO 表
        /// </summary>
        public IDtoTable<TDto> GetDtoTable<TDto>() where TDto : class
        {
            if (_dtoTables.TryGetValue(typeof(TDto), out var obj) && obj is IDtoTable<TDto> table)
            {
                return table;
            }

            table = new DtoTable<TDto>();
            _dtoTables[typeof(TDto)] = table;
            return table;
        }

        /// <summary>
        /// 获取 DTO
        /// </summary>
        public TDto GetDto<TDto>(int id) where TDto : class
        {
            return GetDtoTable<TDto>().Get(id);
        }

        /// <summary>
        /// 尝试获取 DTO
        /// </summary>
        public bool TryGetDto<TDto>(int id, out TDto dto) where TDto : class
        {
            return GetDtoTable<TDto>().TryGet(id, out dto);
        }

        #endregion

        #region Private Helpers

        #endregion

        #region Incremental Loading

        /// <summary>
        /// 增量配置变更记录
        /// </summary>
        public class IncrementalChange
        {
            public string TableName { get; }
            public byte[] Bytes { get; }
            public string Text { get; }
            public bool IsDeleted { get; }

            public IncrementalChange(string tableName, byte[] bytes)
            {
                TableName = tableName;
                Bytes = bytes;
                Text = null;
                IsDeleted = bytes == null || bytes.Length == 0;
            }

            public IncrementalChange(string tableName, string text)
            {
                TableName = tableName;
                Bytes = null;
                Text = text;
                IsDeleted = string.IsNullOrEmpty(text);
            }
        }

        /// <summary>
        /// 增量重载配置 - 只更新指定表
        /// </summary>
        public ConfigReloadResult ReloadIncremental(IReadOnlyList<IncrementalChange> changes)
        {
            if (changes == null || changes.Count == 0)
            {
                return ConfigReloadResult.Success(DefaultKey, _version, fullReload: false, changedIds: null);
            }

            var allChangedIds = new HashSet<int>();
            var tables = _registry.Tables;
            var preparedUpdates = new List<PreparedIncrementalTableUpdate>();
            var preparedByEntryType = new Dictionary<Type, PreparedIncrementalTableUpdate>(
                TypeNameComparer.Instance);

            for (int changeIndex = 0; changeIndex < changes.Count; changeIndex++)
            {
                var change = changes[changeIndex];

                // 查找配置表定义。
                ConfigTableDefinition definition = null;
                for (int i = 0; i < tables.Count; i++)
                {
                    var t = tables[i];
                    if (t.FileWithoutExt == change.TableName || t.FilePath == change.TableName)
                    {
                        definition = t;
                        break;
                    }
                }

                if (definition == null)
                {
                    var fail = ConfigReloadResult.Fail(DefaultKey, _version,
                        $"Table not found for incremental change: {change.TableName}");
                    ConfigReloadBus.Publish(fail);
                    return fail;
                }

                try
                {
                    if (change.IsDeleted)
                    {
                        if (preparedByEntryType.ContainsKey(definition.EntryType)
                            || _tables.ContainsKey(definition.EntryType)
                            || _dtoTables.ContainsKey(definition.DtoType))
                        {
                            var fail = ConfigReloadResult.Fail(DefaultKey, _version,
                                $"Table deletion requires full reload: {change.TableName}");
                            ConfigReloadBus.Publish(fail);
                            return fail;
                        }

                        continue;
                    }

                    Array arr = null;
                    if (change.Bytes != null)
                    {
                        arr = _deserializer.DeserializeBytes(change.Bytes, definition.DtoType);
                    }
                    else if (change.Text != null)
                    {
                        arr = _deserializer.DeserializeText(change.Text, definition.DtoType);
                    }

                    var nextDtoTable = ConfigTableMaterializer.CreateDtoTableFromDtos(definition, arr);
                    var nextEntryTable = ConfigTableMaterializer.CreateAndPopulateTable(definition, arr);
                    ConfigTableMaterializer.CollectChangedIds(definition, arr, allChangedIds);

                    if (!preparedByEntryType.TryGetValue(definition.EntryType, out var prepared))
                    {
                        var replacesExisting = _tables.TryGetValue(
                            definition.EntryType,
                            out var existingEntryTable);
                        object existingDtoTable = null;
                        if (replacesExisting)
                        {
                            replacesExisting = _dtoTables.TryGetValue(
                                definition.DtoType,
                                out existingDtoTable);
                        }
                        prepared = new PreparedIncrementalTableUpdate(
                            definition,
                            replacesExisting,
                            existingDtoTable,
                            existingEntryTable);
                        preparedByEntryType.Add(definition.EntryType, prepared);
                        preparedUpdates.Add(prepared);
                    }

                    prepared.NextDtoTable = nextDtoTable;
                    prepared.NextEntryTable = nextEntryTable;
                }
                catch (Exception ex)
                {
                    var fail = ConfigReloadResult.Fail(DefaultKey, _version,
                        $"Failed to reload incrementally: {change.TableName}. {ex.Message}");
                    ConfigReloadBus.Publish(fail);
                    return fail;
                }
            }

            try
            {
                for (var i = 0; i < preparedUpdates.Count; i++)
                {
                    preparedUpdates[i].ValidateCommitTargets();
                }
            }
            catch (Exception ex)
            {
                var fail = ConfigReloadResult.Fail(DefaultKey, _version,
                    $"Failed to prepare incremental commit. {ex.Message}");
                ConfigReloadBus.Publish(fail);
                return fail;
            }

            for (var i = 0; i < preparedUpdates.Count; i++)
            {
                preparedUpdates[i].Commit(_tables, _dtoTables);
            }

            _version++;
            var changedIdsList = allChangedIds.Count > 0 ? new List<int>(allChangedIds) : null;
            var success = ConfigReloadResult.Success(DefaultKey, _version, fullReload: false,
                changedIds: changedIdsList);
            ConfigReloadBus.Publish(success);
            return success;
        }

        /// <summary>
        /// 运行时注册新的配置表
        /// </summary>
        public void RegisterTable(ConfigTableDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));

            var tables = _registry.Tables;
            for (int i = 0; i < tables.Count; i++)
            {
                if (tables[i].FilePath == definition.FilePath)
                {
                    throw new InvalidOperationException($"Table already registered: {definition.FilePath}");
                }
            }

            // 注意：运行时修改注册表需要 registry 本身提供支持。
            // 这里是前置声明，实际实现取决于 registry。
        }

        #endregion

        #region Internal Classes

        /// <summary>
        /// DTO 表实现，存储原始 DTO 对象
        /// </summary>
        internal sealed class DtoTable<TDto> : IDtoTable<TDto>, IConfigTableContentReplacer
            where TDto : class
        {
            private readonly Dictionary<int, TDto> _byId = new Dictionary<int, TDto>();
            private readonly Func<TDto, int> _keySelector;

            public DtoTable()
            {
            }

            internal DtoTable(Func<TDto, int> keySelector)
            {
                _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
            }

            public int Count => _byId.Count;

            public void Add(object dto)
            {
                if (dto == null) return;
                var typedDto = (TDto)dto;
                var id = _keySelector != null ? _keySelector(typedDto) : ReadId(dto);
                _byId[id] = typedDto;
            }

            public void Clear()
            {
                _byId.Clear();
            }

            internal void ReplaceWith(DtoTable<TDto> source)
            {
                if (source == null) throw new ArgumentNullException(nameof(source));
                _byId.Clear();
                foreach (var pair in source._byId)
                {
                    _byId[pair.Key] = pair.Value;
                }
            }

            void IConfigTableContentReplacer.ReplaceContentsFrom(object source)
            {
                if (!(source is DtoTable<TDto> typedSource))
                    throw new InvalidOperationException($"Unexpected replacement table type for {typeof(TDto).FullName}.");
                ReplaceWith(typedSource);
            }

            public TDto Get(int id)
            {
                return _byId.TryGetValue(id, out var dto)
                    ? dto
                    : throw new KeyNotFoundException($"Dto not found: type={typeof(TDto).Name} id={id}");
            }

            public bool TryGet(int id, out TDto dto) => _byId.TryGetValue(id, out dto);

            public IEnumerable<TDto> All() => _byId.Values;

            private static int ReadId(object dto)
            {
                var type = dto.GetType();
                var field = type.GetField("Id");
                if (field != null && field.FieldType == typeof(int)) return (int)field.GetValue(dto);
                var property = type.GetProperty("Id");
                if (property != null && property.PropertyType == typeof(int)) return (int)property.GetValue(dto);

                // 回退：尝试读取 Code 字段（Luban DR* 类型会使用该字段）。
                field = type.GetField("Code");
                if (field != null && field.FieldType == typeof(int)) return (int)field.GetValue(dto);
                property = type.GetProperty("Code");
                if (property != null && property.PropertyType == typeof(int)) return (int)property.GetValue(dto);

                throw new InvalidOperationException($"DTO must have int Id or Code field/property. type={type.FullName}");
            }
        }

        #endregion
    }
}

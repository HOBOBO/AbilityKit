using System;
using System.Collections.Generic;
using System.Reflection;

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
    public class ConfigDatabase : IConfigDatabase
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

        public ConfigReloadResult Load(IConfigSource source, string basePath = null)
        {
            return Reload(source, basePath);
        }

        public ConfigReloadResult Load(IConfigSource source, string basePath, bool strict)
        {
            return Reload(source, basePath, strict);
        }

        public ConfigReloadResult Reload(IConfigSource source, string basePath = null)
        {
            return Reload(source, basePath, strict: true);
        }

        public ConfigReloadResult Reload(IConfigSource source, string basePath, bool strict)
        {
            if (source == null)
            {
                return ConfigReloadResult.Fail(DefaultKey, _version, "Config source is null");
            }

            var tables = _registry.Tables;
            var nextTables = new Dictionary<Type, object>(TypeNameComparer.Instance);
            var nextDtoTables = new Dictionary<Type, object>(TypeNameComparer.Instance);

            for (int i = 0; i < tables.Count; i++)
            {
                var definition = tables[i];
                var fullPath = string.IsNullOrEmpty(basePath) 
                    ? definition.FilePath 
                    : $"{basePath}/{definition.FilePath}";

                if (!TryLoadFromSource(source, fullPath, definition, out var arr))
                {
                    if (strict)
                    {
                        var fail = ConfigReloadResult.Fail(DefaultKey, _version,
                            $"Config not found: {definition.FilePath}");
                        ConfigReloadBus.Publish(fail);
                        return fail;
                    }

                    arr = Array.CreateInstance(definition.DtoType, 0);
                }

                var dtoTable = CreateDtoTableFromDtos(definition, arr);
                nextDtoTables[definition.DtoType] = dtoTable;

                var table = CreateAndPopulateTable(definition, arr);
                nextTables[definition.EntryType] = table;
            }

            CommitTables(nextTables, nextDtoTables);
            var success = ConfigReloadResult.Success(DefaultKey, _version, fullReload: true, changedIds: null);
            ConfigReloadBus.Publish(success);
            return success;
        }

        /// <summary>
        /// 从字典加载配置
        /// </summary>
        public ConfigReloadResult LoadFromTexts(IReadOnlyDictionary<string, string> texts, string basePath = null)
        {
            return ReloadFromTexts(texts, basePath);
        }

        /// <summary>
        /// 从字典重新加载配置
        /// </summary>
        public ConfigReloadResult ReloadFromTexts(IReadOnlyDictionary<string, string> texts, string basePath = null)
        {
            if (texts == null)
            {
                return ConfigReloadResult.Fail(DefaultKey, _version, "Texts dictionary is null");
            }

            var tables = _registry.Tables;
            var nextTables = new Dictionary<Type, object>(TypeNameComparer.Instance);
            var nextDtoTables = new Dictionary<Type, object>(TypeNameComparer.Instance);

            for (int i = 0; i < tables.Count; i++)
            {
                var definition = tables[i];
                var fullPath = string.IsNullOrEmpty(basePath) 
                    ? definition.FilePath 
                    : $"{basePath}/{definition.FilePath}";

                if (!TryGetText(texts, fullPath, definition.FilePath, out var json) || string.IsNullOrEmpty(json))
                {
                    var fail = ConfigReloadResult.Fail(DefaultKey, _version, 
                        $"Config json not found: {definition.FilePath}");
                    ConfigReloadBus.Publish(fail);
                    return fail;
                }

                Array arr;
                try
                {
                    arr = _deserializer.DeserializeText(json, definition.DtoType);
                }
                catch (Exception ex)
                {
                    var fail = ConfigReloadResult.Fail(DefaultKey, _version, 
                        $"Failed to deserialize: {definition.FilePath}. {ex.Message}");
                    ConfigReloadBus.Publish(fail);
                    return fail;
                }

                var dtoTable = CreateDtoTableFromDtos(definition, arr);
                nextDtoTables[definition.DtoType] = dtoTable;

                var table = CreateAndPopulateTable(definition, arr);
                nextTables[definition.EntryType] = table;
            }

            CommitTables(nextTables, nextDtoTables);
            var success = ConfigReloadResult.Success(DefaultKey, _version, fullReload: true, changedIds: null);
            ConfigReloadBus.Publish(success);
            return success;
        }

        private bool TryLoadFromSource(IConfigSource source, string fullPath, ConfigTableDefinition definition, out Array arr)
        {
            arr = null;

            if (source.TryGetBytes(fullPath, out var bytes) && bytes != null && bytes.Length > 0)
            {
                try
                {
                    arr = _deserializer.DeserializeBytes(bytes, definition.DtoType);
                    return true;
                }
                catch
                {
                }
            }

            if (source.TryGetText(fullPath, out var text) && !string.IsNullOrEmpty(text))
            {
                try
                {
                    arr = _deserializer.DeserializeText(text, definition.DtoType);
                    return true;
                }
                catch
                {
                }
            }

            if (source.TryGetText(definition.FilePath, out text) && !string.IsNullOrEmpty(text))
            {
                try
                {
                    arr = _deserializer.DeserializeText(text, definition.DtoType);
                    return true;
                }
                catch
                {
                }
            }

            return false;
        }

        private static bool TryGetText(IReadOnlyDictionary<string, string> texts, string fullPath, string filePath, out string text)
        {
            if (texts.TryGetValue(fullPath, out text) && !string.IsNullOrEmpty(text)) return true;
            if (texts.TryGetValue(filePath, out text) && !string.IsNullOrEmpty(text)) return true;
            text = null;
            return false;
        }

        private object CreateAndPopulateTable(ConfigTableDefinition definition, Array dtos)
        {
            if (definition.EntryTableFactory != null) return definition.EntryTableFactory(dtos);

            var entryType = definition.EntryType;
            var tableType = typeof(IntKeyConfigTable<>).MakeGenericType(entryType);
            var table = Activator.CreateInstance(tableType);

            if (dtos != null)
            {
                for (int i = 0; i < dtos.Length; i++)
                {
                    var dto = dtos.GetValue(i);
                    if (dto == null) continue;

                    // 直接通过构造函数创建条目。
                    var entry = Activator.CreateInstance(entryType, dto);
                    if (entry == null) continue;

                    // 直接调用 Add 方法。
                    var addMethod = tableType.GetMethod("Add", BindingFlags.Instance | BindingFlags.Public);
                    if (addMethod == null) throw new InvalidOperationException($"Add not found on {tableType.FullName}");

                    // 从 DTO 读取 Id。
                    var id = ReadIdFromDto(dto);
                    addMethod.Invoke(table, new[] { id, entry });
                }
            }

            return table;
        }

        private static int ReadIdFromDto(object dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));
            var type = dto.GetType();

            var field = type.GetField("Id", BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(int)) return (int)field.GetValue(dto);

            var property = type.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.PropertyType == typeof(int)) return (int)property.GetValue(dto);

            // 回退：尝试读取 Code 字段（Luban DR* 类型会使用该字段）。
            field = type.GetField("Code", BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(int)) return (int)field.GetValue(dto);

            property = type.GetProperty("Code", BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.PropertyType == typeof(int)) return (int)property.GetValue(dto);

            throw new InvalidOperationException($"DTO must have int Id or Code field/property. type={type.FullName}");
        }

        private void CommitTables(Dictionary<Type, object> nextTables)
        {
            _tables.Clear();
            foreach (var kv in nextTables)
            {
                _tables[kv.Key] = kv.Value;
            }
            _version++;
        }

        private IntKeyConfigTable<TEntry> CreateTable<TEntry>() where TEntry : class
        {
            return new IntKeyConfigTable<TEntry>();
        }

        #region Bytes Loading

        /// <summary>
        /// 从字节字典加载配置
        /// </summary>
        public ConfigReloadResult LoadFromBytes(IReadOnlyDictionary<string, byte[]> bytesByKey, string basePath = null)
        {
            return ReloadFromBytes(bytesByKey, basePath);
        }

        /// <summary>
        /// 从字节字典重新加载配置
        /// </summary>
        public ConfigReloadResult ReloadFromBytes(IReadOnlyDictionary<string, byte[]> bytesByKey, string basePath = null)
        {
            if (bytesByKey == null)
            {
                var fail = ConfigReloadResult.Fail(DefaultKey, _version, "Bytes dictionary is null");
                ConfigReloadBus.Publish(fail);
                return fail;
            }

            var nextTables = new Dictionary<Type, object>(TypeNameComparer.Instance);
            var nextDtoTables = new Dictionary<Type, object>(TypeNameComparer.Instance);

            var tables = _registry.Tables;
            for (int i = 0; i < tables.Count; i++)
            {
                var definition = tables[i];
                var fullPath = string.IsNullOrEmpty(basePath)
                    ? definition.FilePath
                    : $"{basePath}/{definition.FilePath}";

                if (!TryGetBytes(bytesByKey, fullPath, definition.FilePath, out var bytes) || bytes == null || bytes.Length == 0)
                {
                    var fail = ConfigReloadResult.Fail(DefaultKey, _version, $"Config bytes not found: {fullPath}");
                    ConfigReloadBus.Publish(fail);
                    return fail;
                }

                Array arr;
                try
                {
                    arr = _deserializer.DeserializeBytes(bytes, definition.DtoType);
                }
                catch (Exception ex)
                {
                    var fail = ConfigReloadResult.Fail(DefaultKey, _version, $"Failed to parse config bytes: {fullPath}. {ex.Message}");
                    ConfigReloadBus.Publish(fail);
                    return fail;
                }

                var dtoTable = CreateDtoTableFromDtos(definition, arr);
                nextDtoTables[definition.DtoType] = dtoTable;

                var table = CreateAndPopulateTable(definition, arr);
                nextTables[definition.EntryType] = table;
            }

            CommitTables(nextTables, nextDtoTables);
            var success = ConfigReloadResult.Success(DefaultKey, _version, fullReload: true, changedIds: null);
            ConfigReloadBus.Publish(success);
            return success;
        }

        #endregion

        #region Mixed Loading

        /// <summary>
        /// 从混合源（字节+文本）加载配置
        /// </summary>
        public ConfigReloadResult LoadFromMixed(
            IReadOnlyDictionary<string, byte[]> bytesByKey,
            IReadOnlyDictionary<string, string> textsByKey,
            string bytesBasePath = null,
            string textsBasePath = null,
            bool strict = true)
        {
            return ReloadFromMixed(bytesByKey, textsByKey, bytesBasePath, textsBasePath, strict);
        }

        /// <summary>
        /// 从混合源（字节+文本）重新加载配置
        /// </summary>
        public ConfigReloadResult ReloadFromMixed(
            IReadOnlyDictionary<string, byte[]> bytesByKey,
            IReadOnlyDictionary<string, string> textsByKey,
            string bytesBasePath = null,
            string textsBasePath = null,
            bool strict = true)
        {
            if (bytesByKey == null || textsByKey == null)
            {
                var fail = ConfigReloadResult.Fail(DefaultKey, _version, "Bytes or texts dictionary is null");
                ConfigReloadBus.Publish(fail);
                return fail;
            }

            var nextTables = new Dictionary<Type, object>(TypeNameComparer.Instance);
            var nextDtoTables = new Dictionary<Type, object>(TypeNameComparer.Instance);

            var tables = _registry.Tables;
            for (int i = 0; i < tables.Count; i++)
            {
                var definition = tables[i];
                var bytesFullPath = string.IsNullOrEmpty(bytesBasePath)
                    ? definition.FilePath
                    : $"{bytesBasePath}/{definition.FilePath}";
                var textsFullPath = string.IsNullOrEmpty(textsBasePath)
                    ? definition.FilePath
                    : $"{textsBasePath}/{definition.FilePath}";

                Array arr;
                try
                {
                    if (TryGetBytes(bytesByKey, bytesFullPath, definition.FilePath, out var bytes) && bytes != null && bytes.Length > 0)
                    {
                        arr = _deserializer.DeserializeBytes(bytes, definition.DtoType);
                    }
                    else if (TryGetText(textsByKey, textsFullPath, definition.FilePath, out var text) && !string.IsNullOrEmpty(text))
                    {
                        arr = _deserializer.DeserializeText(text, definition.DtoType);
                    }
                    else
                    {
                        if (strict)
                        {
                            var fail = ConfigReloadResult.Fail(DefaultKey, _version, $"Config not found (bytes/texts): {definition.FilePath}");
                            ConfigReloadBus.Publish(fail);
                            return fail;
                        }

                        arr = Array.CreateInstance(definition.DtoType, 0);
                    }

                    var dtoTable = CreateDtoTableFromDtos(definition, arr);
                    nextDtoTables[definition.DtoType] = dtoTable;

                    var table = CreateAndPopulateTable(definition, arr);
                    nextTables[definition.EntryType] = table;
                }
                catch (Exception ex)
                {
                    var fail = ConfigReloadResult.Fail(DefaultKey, _version, $"Failed to parse config (mixed): {definition.FilePath}. {ex.Message}");
                    ConfigReloadBus.Publish(fail);
                    return fail;
                }
            }

            CommitTables(nextTables, nextDtoTables);
            var success = ConfigReloadResult.Success(DefaultKey, _version, fullReload: true, changedIds: null);
            ConfigReloadBus.Publish(success);
            return success;
        }

        #endregion

        #region Groups Loading

        /// <summary>
        /// 从配置组加载配置
        /// </summary>
        public ConfigReloadResult LoadFromGroups(IReadOnlyList<IConfigGroup> groups)
        {
            return ReloadFromGroups(groups);
        }

        /// <summary>
        /// 从配置组重新加载配置
        /// </summary>
        public ConfigReloadResult ReloadFromGroups(IReadOnlyList<IConfigGroup> groups)
        {
            if (groups == null || groups.Count == 0)
            {
                var fail = ConfigReloadResult.Fail(DefaultKey, _version, "No config groups provided");
                ConfigReloadBus.Publish(fail);
                return fail;
            }

            var nextTables = new Dictionary<Type, object>(TypeNameComparer.Instance);
            var nextDtoTables = new Dictionary<Type, object>(TypeNameComparer.Instance);

            // 按配置组处理全部配置表。
            for (int gi = 0; gi < groups.Count; gi++)
            {
                var group = groups[gi];

                for (int i = 0; i < group.Tables.Count; i++)
                {
                    var entry = group.Tables[i];

                    // 尝试从当前组加载配置数据。
                    if (!group.Loader.TryLoad(entry.FilePath, out var bytes, out var text))
                    {
                        // 如果当前组没有该配置，则继续尝试后续组。
                        bool found = false;
                        for (int gj = gi + 1; gj < groups.Count; gj++)
                        {
                            if (groups[gj].Loader.TryLoad(entry.FilePath, out bytes, out text))
                            {
                                found = true;
                                break;
                            }
                        }

                        if (!found)
                        {
                            var fail = ConfigReloadResult.Fail(DefaultKey, _version,
                                $"Config not found: {entry.FilePath} in any group");
                            ConfigReloadBus.Publish(fail);
                            return fail;
                        }
                    }

                    // 反序列化。
                    Array arr;
                    try
                    {
                        if (bytes != null && bytes.Length > 0)
                        {
                            arr = group.Deserializer.DeserializeFromBytes(bytes, entry.DtoType);
                        }
                        else if (!string.IsNullOrEmpty(text))
                        {
                            arr = group.Deserializer.DeserializeFromText(text, entry.DtoType);
                        }
                        else
                        {
                            var fail = ConfigReloadResult.Fail(DefaultKey, _version,
                                $"Config data is empty: {entry.FilePath}");
                            ConfigReloadBus.Publish(fail);
                            return fail;
                        }
                    }
                    catch (Exception ex)
                    {
                        var fail = ConfigReloadResult.Fail(DefaultKey, _version,
                            $"Failed to deserialize: {entry.FilePath}. {ex.Message}");
                        ConfigReloadBus.Publish(fail);
                        return fail;
                    }

                    // 创建配置表。
                    var dtoTable = CreateDtoTableFromDtos(entry, arr);
                    nextDtoTables[entry.DtoType] = dtoTable;

                    var table = CreateAndPopulateTable(entry, arr);
                    nextTables[entry.EntryType] = table;
                }
            }

            CommitTables(nextTables, nextDtoTables);
            var success = ConfigReloadResult.Success(DefaultKey, _version, fullReload: true, changedIds: null);
            ConfigReloadBus.Publish(success);
            return success;
        }

        #endregion

        #region DTO Array Loading

        public ConfigReloadResult LoadFromDtoArrays(IReadOnlyDictionary<Type, Array> dtoArraysByType, bool strict = true)
        {
            return ReloadFromDtoArrays(dtoArraysByType, strict);
        }

        public ConfigReloadResult ReloadFromDtoArrays(IReadOnlyDictionary<Type, Array> dtoArraysByType, bool strict = true)
        {
            if (dtoArraysByType == null)
            {
                var fail = ConfigReloadResult.Fail(DefaultKey, _version, "DTO array dictionary is null");
                ConfigReloadBus.Publish(fail);
                return fail;
            }

            var nextTables = new Dictionary<Type, object>(TypeNameComparer.Instance);
            var nextDtoTables = new Dictionary<Type, object>(TypeNameComparer.Instance);

            var tables = _registry.Tables;
            for (int i = 0; i < tables.Count; i++)
            {
                var definition = tables[i];

                if (!dtoArraysByType.TryGetValue(definition.DtoType, out var arr) || arr == null)
                {
                    if (strict)
                    {
                        var fail = ConfigReloadResult.Fail(DefaultKey, _version, $"DTO array not found: {definition.DtoType.FullName}");
                        ConfigReloadBus.Publish(fail);
                        return fail;
                    }

                    arr = Array.CreateInstance(definition.DtoType, 0);
                }

                if (arr.GetType().GetElementType() != definition.DtoType)
                {
                    var fail = ConfigReloadResult.Fail(DefaultKey, _version, $"DTO array type mismatch: expected {definition.DtoType.FullName}, actual {arr.GetType().FullName}");
                    ConfigReloadBus.Publish(fail);
                    return fail;
                }

                try
                {
                    var dtoTable = CreateDtoTableFromDtos(definition, arr);
                    nextDtoTables[definition.DtoType] = dtoTable;

                    var table = CreateAndPopulateTable(definition, arr);
                    nextTables[definition.EntryType] = table;
                }
                catch (Exception ex)
                {
                    var fail = ConfigReloadResult.Fail(DefaultKey, _version, $"Failed to build config from DTO array: {definition.DtoType.FullName}. {ex.Message}");
                    ConfigReloadBus.Publish(fail);
                    return fail;
                }
            }

            CommitTables(nextTables, nextDtoTables);
            var success = ConfigReloadResult.Success(DefaultKey, _version, fullReload: true, changedIds: null);
            ConfigReloadBus.Publish(success);
            return success;
        }

        #endregion

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

        private static bool TryGetBytes(IReadOnlyDictionary<string, byte[]> bytesByKey, string fullPath, string filePath, out byte[] bytes)
        {
            bytes = null;
            if (bytesByKey.TryGetValue(fullPath, out bytes)) return true;
            if (bytesByKey.TryGetValue(filePath, out bytes)) return true;
            return false;
        }

        private static object CreateDtoTableFromDtos(ConfigTableDefinition definition, Array dtos)
        {
            if (definition.DtoTableFactory != null) return definition.DtoTableFactory(dtos);

            var dtoType = definition.DtoType;
            var tableType = typeof(DtoTable<>).MakeGenericType(dtoType);
            var table = Activator.CreateInstance(tableType);
            var addMethod = tableType.GetMethod("Add", BindingFlags.Instance | BindingFlags.Public);
            if (addMethod == null) throw new InvalidOperationException($"Add not found on {tableType.FullName}");

            if (dtos != null)
            {
                for (int i = 0; i < dtos.Length; i++)
                {
                    var dto = dtos.GetValue(i);
                    if (dto != null)
                    {
                        addMethod.Invoke(table, new[] { dto });
                    }
                }
            }

            return table;
        }

        private void CommitTables(Dictionary<Type, object> nextTables, Dictionary<Type, object> nextDtoTables)
        {
            _tables.Clear();
            foreach (var kv in nextTables)
            {
                _tables[kv.Key] = kv.Value;
            }

            _dtoTables.Clear();
            foreach (var kv in nextDtoTables)
            {
                _dtoTables[kv.Key] = kv.Value;
            }

            _version++;
        }

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

                    var nextDtoTable = CreateDtoTableFromDtos(definition, arr);
                    var nextEntryTable = CreateAndPopulateTable(definition, arr);
                    CollectChangedIds(definition, arr, allChangedIds);

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

        private sealed class PreparedIncrementalTableUpdate
        {
            private readonly ConfigTableDefinition _definition;
            private readonly bool _replacesExisting;
            private readonly object _existingDtoTable;
            private readonly object _existingEntryTable;
            private IConfigTableContentReplacer _dtoReplacer;
            private IConfigTableContentReplacer _entryReplacer;

            public PreparedIncrementalTableUpdate(
                ConfigTableDefinition definition,
                bool replacesExisting,
                object existingDtoTable,
                object existingEntryTable)
            {
                _definition = definition;
                _replacesExisting = replacesExisting;
                _existingDtoTable = existingDtoTable;
                _existingEntryTable = existingEntryTable;
            }

            public object NextDtoTable { get; set; }
            public object NextEntryTable { get; set; }

            public void ValidateCommitTargets()
            {
                if (!_replacesExisting) return;

                _dtoReplacer = GetTableContentReplacer(
                    _existingDtoTable,
                    NextDtoTable,
                    _definition.DtoType);
                _entryReplacer = GetTableContentReplacer(
                    _existingEntryTable,
                    NextEntryTable,
                    _definition.EntryType);
            }

            public void Commit(
                Dictionary<Type, object> tables,
                Dictionary<Type, object> dtoTables)
            {
                if (_replacesExisting)
                {
                    _dtoReplacer.ReplaceContentsFrom(NextDtoTable);
                    _entryReplacer.ReplaceContentsFrom(NextEntryTable);
                    return;
                }

                dtoTables[_definition.DtoType] = NextDtoTable;
                tables[_definition.EntryType] = NextEntryTable;
            }
        }

        private static IConfigTableContentReplacer GetTableContentReplacer(
            object target,
            object source,
            Type valueType)
        {
            if (!(target is IConfigTableContentReplacer replacer) || target.GetType() != source.GetType())
                throw new InvalidOperationException($"Table replacement is not supported for {valueType.FullName}");
            return replacer;
        }

        private static void CollectChangedIds(
            ConfigTableDefinition definition,
            Array entries,
            HashSet<int> changedIds)
        {
            if (definition.ChangedIdCollector != null)
            {
                definition.ChangedIdCollector(entries, changedIds);
                return;
            }

            if (entries == null) return;

            for (int i = 0; i < entries.Length; i++)
            {
                var dto = entries.GetValue(i);
                if (dto != null)
                {
                    var id = ReadIdFromDto(dto);
                    changedIds.Add(id);
                }
            }
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

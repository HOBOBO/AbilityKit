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
        // Use TypeNameComparer to work around IL2CPP Type.Equals() identity issues
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

        public ConfigReloadResult Reload(IConfigSource source, string basePath = null)
        {
            if (source == null)
            {
                return ConfigReloadResult.Fail(DefaultKey, _version, "Config source is null");
            }

            var tables = _registry.Tables;
            var nextTables = new Dictionary<Type, object>();

            for (int i = 0; i < tables.Count; i++)
            {
                var definition = tables[i];
                var fullPath = string.IsNullOrEmpty(basePath) 
                    ? definition.FilePath 
                    : $"{basePath}/{definition.FilePath}";

                if (!TryLoadFromSource(source, fullPath, definition, out var arr))
                {
                    var fail = ConfigReloadResult.Fail(DefaultKey, _version, 
                        $"Config not found: {definition.FilePath}");
                    ConfigReloadBus.Publish(fail);
                    return fail;
                }

                var table = CreateAndPopulateTable(definition.DtoType, definition.EntryType, arr);
                nextTables[definition.EntryType] = table;
            }

            CommitTables(nextTables);
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
            var nextTables = new Dictionary<Type, object>();

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

                var table = CreateAndPopulateTable(definition.DtoType, definition.EntryType, arr);
                nextTables[definition.EntryType] = table;
            }

            CommitTables(nextTables);
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

        private object CreateAndPopulateTable(Type dtoType, Type entryType, Array dtos)
        {
            var tableType = typeof(IntKeyConfigTable<>).MakeGenericType(entryType);
            var table = Activator.CreateInstance(tableType);

            if (dtos != null)
            {
                for (int i = 0; i < dtos.Length; i++)
                {
                    var dto = dtos.GetValue(i);
                    if (dto == null) continue;

                    // Create entry directly using constructor
                    var entry = Activator.CreateInstance(entryType, dto);
                    if (entry == null) continue;

                    // Use the Add method directly
                    var addMethod = tableType.GetMethod("Add", BindingFlags.Instance | BindingFlags.Public);
                    if (addMethod == null) throw new InvalidOperationException($"Add not found on {tableType.FullName}");

                    // Read the Id from the dto
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

            // Fallback: try "Code" field (used by Luban DR* types)
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

                var dtoTable = CreateDtoTableFromDtos(definition.DtoType, arr);
                nextDtoTables[definition.DtoType] = dtoTable;

                var table = CreateAndPopulateTable(definition.DtoType, definition.EntryType, arr);
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

                    var dtoTable = CreateDtoTableFromDtos(definition.DtoType, arr);
                    nextDtoTables[definition.DtoType] = dtoTable;

                    var table = CreateAndPopulateTable(definition.DtoType, definition.EntryType, arr);
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

            // Process all config tables by group
            for (int gi = 0; gi < groups.Count; gi++)
            {
                var group = groups[gi];

                for (int i = 0; i < group.Tables.Count; i++)
                {
                    var entry = group.Tables[i];

                    // Try to load config data from this group
                    if (!group.Loader.TryLoad(entry.FilePath, out var bytes, out var text))
                    {
                        // If this group doesn't have the config, try next groups
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

                    // Deserialize
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

                    // Create tables
                    var dtoTable = CreateDtoTableFromDtos(entry.DtoType, arr);
                    nextDtoTables[entry.DtoType] = dtoTable;

                    var table = CreateAndPopulateTable(entry.DtoType, entry.EntryType, arr);
                    nextTables[entry.EntryType] = table;
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

        private static object CreateDtoTableFromDtos(Type dtoType, Array dtos)
        {
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

        #region Internal Classes

        /// <summary>
        /// DTO 表实现，存储原始 DTO 对象
        /// </summary>
        internal sealed class DtoTable<TDto> : IDtoTable<TDto> where TDto : class
        {
            private readonly Dictionary<int, TDto> _byId = new Dictionary<int, TDto>();

            public int Count => _byId.Count;

            public void Add(object dto)
            {
                if (dto == null) return;
                var id = ReadId(dto);
                _byId[id] = (TDto)dto;
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

                // Fallback: try "Code" field (used by Luban DR* types)
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

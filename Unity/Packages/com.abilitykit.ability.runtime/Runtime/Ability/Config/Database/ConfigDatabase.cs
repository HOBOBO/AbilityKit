using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace AbilityKit.Ability.Config
{
    /// <summary>
    /// 通用配置数据库实现
    /// </summary>
    public class ConfigDatabase : IConfigDatabase
    {
        private const string DefaultKey = "config";

        private readonly IConfigTableRegistry _registry;
        private readonly IConfigDeserializer _deserializer;
        private readonly Dictionary<Type, object> _tables = new Dictionary<Type, object>();
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
            var addFromDto = tableType.GetMethod("AddFromDto", BindingFlags.Instance | BindingFlags.Public);
            if (addFromDto == null) throw new InvalidOperationException($"AddFromDto not found on {tableType.FullName}");

            var entryFactory = CreateEntryFactory(dtoType, entryType);

            if (dtos != null)
            {
                for (int i = 0; i < dtos.Length; i++)
                {
                    var dto = dtos.GetValue(i);
                    if (dto != null)
                    {
                        addFromDto.Invoke(table, new[] { dto, entryFactory });
                    }
                }
            }

            return table;
        }

        private static Func<object, object> CreateEntryFactory(Type dtoType, Type entryType)
        {
            var ctor = entryType.GetConstructor(new[] { dtoType });
            if (ctor != null)
            {
                var param = System.Linq.Expressions.Expression.Parameter(typeof(object));
                var convert = System.Linq.Expressions.Expression.Convert(param, dtoType);
                var lambda = System.Linq.Expressions.Expression.Lambda<Func<object, object>>(
                    System.Linq.Expressions.Expression.New(ctor, convert), param);
                return (Func<object, object>)lambda.Compile();
            }

            return obj => Activator.CreateInstance(entryType, obj);
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
    }
}

using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Config
{
    public partial class ConfigDatabase
    {
        /// <summary>
        /// 从数据源严格加载配置；遇到无法读取的表时保留原数据库。
        /// </summary>
        public ConfigReloadResult Load(IConfigSource source, string basePath = null)
        {
            return Reload(source, basePath);
        }

        /// <summary>
        /// 从数据源加载配置；非严格模式为无法读取的表构建空表。
        /// </summary>
        public ConfigReloadResult Load(IConfigSource source, string basePath, bool strict)
        {
            return Reload(source, basePath, strict);
        }

        /// <summary>
        /// 从数据源严格重载配置；全部表就绪后才替换原数据库。
        /// </summary>
        public ConfigReloadResult Reload(IConfigSource source, string basePath = null)
        {
            return Reload(source, basePath, strict: true);
        }

        /// <summary>
        /// 按完整路径字节、完整路径文本、原始表路径文本的顺序尝试读取。
        /// </summary>
        public ConfigReloadResult Reload(IConfigSource source, string basePath, bool strict)
        {
            if (source == null)
            {
                return ConfigReloadResult.Fail(DefaultKey, _version, "Config source is null");
            }

            var tables = _registry.Tables;
            var prepared = new PreparedFullReload();

            for (int i = 0; i < tables.Count; i++)
            {
                var definition = tables[i];
                var fullPath = ConfigLoadInputs.GetFullPath(basePath, definition.FilePath);

                if (!ConfigLoadInputs.TryReadSource(source, _deserializer, fullPath, definition, out var arr))
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

                prepared.Add(definition, arr);
            }

            return CommitFullReload(prepared);
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
            var prepared = new PreparedFullReload();

            for (int i = 0; i < tables.Count; i++)
            {
                var definition = tables[i];
                var fullPath = ConfigLoadInputs.GetFullPath(basePath, definition.FilePath);

                if (!ConfigLoadInputs.TryGetText(texts, fullPath, definition.FilePath, out var json) || string.IsNullOrEmpty(json))
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

                prepared.Add(definition, arr);
            }

            return CommitFullReload(prepared);
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

            var prepared = new PreparedFullReload();

            var tables = _registry.Tables;
            for (int i = 0; i < tables.Count; i++)
            {
                var definition = tables[i];
                var fullPath = ConfigLoadInputs.GetFullPath(basePath, definition.FilePath);

                if (!ConfigLoadInputs.TryGetBytes(bytesByKey, fullPath, definition.FilePath, out var bytes) || bytes == null || bytes.Length == 0)
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

                prepared.Add(definition, arr);
            }

            return CommitFullReload(prepared);
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

            var prepared = new PreparedFullReload();

            var tables = _registry.Tables;
            for (int i = 0; i < tables.Count; i++)
            {
                var definition = tables[i];
                var bytesFullPath = ConfigLoadInputs.GetFullPath(bytesBasePath, definition.FilePath);
                var textsFullPath = ConfigLoadInputs.GetFullPath(textsBasePath, definition.FilePath);

                Array arr;
                try
                {
                    if (ConfigLoadInputs.TryGetBytes(bytesByKey, bytesFullPath, definition.FilePath, out var bytes) && bytes != null && bytes.Length > 0)
                    {
                        arr = _deserializer.DeserializeBytes(bytes, definition.DtoType);
                    }
                    else if (ConfigLoadInputs.TryGetText(textsByKey, textsFullPath, definition.FilePath, out var text) && !string.IsNullOrEmpty(text))
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

                    prepared.Add(definition, arr);
                }
                catch (Exception ex)
                {
                    var fail = ConfigReloadResult.Fail(DefaultKey, _version, $"Failed to parse config (mixed): {definition.FilePath}. {ex.Message}");
                    ConfigReloadBus.Publish(fail);
                    return fail;
                }
            }

            return CommitFullReload(prepared);
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

            var prepared = new PreparedFullReload();

            for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var group = groups[groupIndex];

                for (int i = 0; i < group.Tables.Count; i++)
                {
                    var entry = group.Tables[i];

                    if (!ConfigLoadInputs.TryReadGroup(groups, groupIndex, group, entry.FilePath, out var bytes, out var text))
                    {
                        var fail = ConfigReloadResult.Fail(DefaultKey, _version,
                            $"Config not found: {entry.FilePath} in any group");
                        ConfigReloadBus.Publish(fail);
                        return fail;
                    }

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

                    prepared.Add(entry, arr);
                }
            }

            return CommitFullReload(prepared);
        }

        #endregion

        #region DTO Array Loading

        /// <summary>
        /// 以元素类型匹配的 DTO 数组构建全量配置表。
        /// </summary>
        public ConfigReloadResult LoadFromDtoArrays(IReadOnlyDictionary<Type, Array> dtoArraysByType, bool strict = true)
        {
            return ReloadFromDtoArrays(dtoArraysByType, strict);
        }

        /// <summary>
        /// 通过 DTO 数组重载全量配置；非严格模式允许缺失数组，不允许元素类型不匹配。
        /// </summary>
        public ConfigReloadResult ReloadFromDtoArrays(IReadOnlyDictionary<Type, Array> dtoArraysByType, bool strict = true)
        {
            if (dtoArraysByType == null)
            {
                var fail = ConfigReloadResult.Fail(DefaultKey, _version, "DTO array dictionary is null");
                ConfigReloadBus.Publish(fail);
                return fail;
            }

            var prepared = new PreparedFullReload();

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
                    prepared.Add(definition, arr);
                }
                catch (Exception ex)
                {
                    var fail = ConfigReloadResult.Fail(DefaultKey, _version, $"Failed to build config from DTO array: {definition.DtoType.FullName}. {ex.Message}");
                    ConfigReloadBus.Publish(fail);
                    return fail;
                }
            }

            return CommitFullReload(prepared);
        }

        #endregion

        private ConfigReloadResult CommitFullReload(PreparedFullReload prepared)
        {
            _tables.Clear();
            foreach (var kv in prepared.EntryTables)
            {
                _tables[kv.Key] = kv.Value;
            }

            _dtoTables.Clear();
            foreach (var kv in prepared.DtoTables)
            {
                _dtoTables[kv.Key] = kv.Value;
            }

            _version++;
            var success = ConfigReloadResult.Success(DefaultKey, _version, fullReload: true, changedIds: null);
            ConfigReloadBus.Publish(success);
            return success;
        }

        /// <summary>
        /// 保存本次重载构建的表，直到全部输入就绪才交给数据库替换。
        /// </summary>
        private sealed class PreparedFullReload
        {
            internal readonly Dictionary<Type, object> EntryTables =
                new Dictionary<Type, object>(TypeNameComparer.Instance);
            internal readonly Dictionary<Type, object> DtoTables =
                new Dictionary<Type, object>(TypeNameComparer.Instance);

            internal void Add(ConfigTableDefinition definition, Array dtos)
            {
                DtoTables[definition.DtoType] = ConfigTableMaterializer.CreateDtoTableFromDtos(definition, dtos);
                EntryTables[definition.EntryType] = ConfigTableMaterializer.CreateAndPopulateTable(definition, dtos);
            }
        }
    }
}

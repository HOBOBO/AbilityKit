using System;
using System.Collections.Generic;

namespace AbilityKit.Ability.Config
{
    /// <summary>
    /// 按加载入口的优先级选择配置输入，不构建表或提交数据库状态。
    /// </summary>
    internal static class ConfigLoadInputs
    {
        internal static string GetFullPath(string basePath, string filePath)
        {
            return string.IsNullOrEmpty(basePath) ? filePath : $"{basePath}/{filePath}";
        }

        internal static bool TryReadGroup(
            IReadOnlyList<IConfigGroup> groups, int groupIndex, IConfigGroup declaringGroup,
            string filePath, out byte[] bytes, out string text)
        {
            if (declaringGroup.Loader.TryLoad(filePath, out bytes, out text))
            {
                return true;
            }

            for (int fallbackIndex = groupIndex + 1; fallbackIndex < groups.Count; fallbackIndex++)
            {
                if (groups[fallbackIndex].Loader.TryLoad(filePath, out bytes, out text))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool TryReadSource(
            IConfigSource source, IConfigDeserializer deserializer, string fullPath,
            ConfigTableDefinition definition, out Array arr)
        {
            arr = null;

            if (source.TryGetBytes(fullPath, out var bytes) && bytes != null && bytes.Length > 0)
            {
                try
                {
                    arr = deserializer.DeserializeBytes(bytes, definition.DtoType);
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
                    arr = deserializer.DeserializeText(text, definition.DtoType);
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
                    arr = deserializer.DeserializeText(text, definition.DtoType);
                    return true;
                }
                catch
                {
                }
            }

            return false;
        }

        internal static bool TryGetText(IReadOnlyDictionary<string, string> texts, string fullPath, string filePath, out string text)
        {
            if (texts.TryGetValue(fullPath, out text) && !string.IsNullOrEmpty(text)) return true;
            if (texts.TryGetValue(filePath, out text) && !string.IsNullOrEmpty(text)) return true;
            text = null;
            return false;
        }

        internal static bool TryGetBytes(IReadOnlyDictionary<string, byte[]> bytesByKey, string fullPath, string filePath, out byte[] bytes)
        {
            bytes = null;
            if (bytesByKey.TryGetValue(fullPath, out bytes)) return true;
            if (bytesByKey.TryGetValue(filePath, out bytes)) return true;
            return false;
        }
    }
}

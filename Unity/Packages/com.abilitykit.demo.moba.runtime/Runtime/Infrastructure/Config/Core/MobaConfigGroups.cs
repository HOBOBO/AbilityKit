using System.Collections.Generic;
using AbilityKit.Ability.Config;

namespace AbilityKit.Demo.Moba.Config.Core
{
    /// <summary>
    /// MOBA 配置组声明和表归属关系。
    /// </summary>
    public static class MobaConfigGroups
    {
        private static readonly LegacyJsonConfigGroupDeserializer _legacyJsonDeserializer = LegacyJsonConfigGroupDeserializer.Instance;

        /// <summary>
        /// 使用标准 JSON DTO 格式的表所属的 legacy JSON 配置组。
        /// </summary>
        public static readonly ConfigGroup LegacyJson = new ConfigGroup(
            ConfigGroupNames.LegacyJson,
            MobaConfigPaths.DefaultResourcesDir,
            _legacyJsonDeserializer,
            MobaGeneratedConfigTableManifest.CreateGroupDefinitions(ConfigGroupNames.LegacyJson)
        );

        /// <summary>
        /// 获取全部配置组。
        /// </summary>
        public static IReadOnlyList<IConfigGroup> All => new IConfigGroup[] { LegacyJson };

        /// <summary>
        /// 按名称获取配置组。
        /// </summary>
        public static IConfigGroup GetByName(string name)
        {
            foreach (var group in All)
            {
                if (group.Name == name)
                    return group;
            }
            return null;
        }

        /// <summary>
        /// 按表文件名获取表定义。
        /// </summary>
        public static ConfigTableDefinition GetTableEntry(string tableName)
        {
            foreach (var group in All)
            {
                foreach (var entry in group.Tables)
                {
                    if (entry.FileWithoutExt == tableName)
                        return entry;
                }
            }
            return null;
        }
    }
}

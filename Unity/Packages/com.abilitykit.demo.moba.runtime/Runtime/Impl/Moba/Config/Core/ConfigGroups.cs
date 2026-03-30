using System.Collections.Generic;
using AbilityKit.Ability.Config;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config.Core
{
    /// <summary>
    /// Luban 二进制格式配置组
    /// </summary>
    public sealed class LubanBinaryConfigGroup : IConfigGroup
    {
        public string Name => ConfigGroupNames.LubanBinary;

        public IConfigGroupLoader Loader { get; }

        public IConfigGroupDeserializer Deserializer => LubanBinaryConfigGroupDeserializer.Instance;

        public IReadOnlyList<ConfigTableDefinition> Tables { get; }

        public LubanBinaryConfigGroup(params ConfigTableDefinition[] tables)
        {
            Loader = new ResourcesBytesConfigGroupLoader("moba_bytes");
            Tables = tables;
        }

        public LubanBinaryConfigGroup(IConfigGroupLoader loader, params ConfigTableDefinition[] tables)
        {
            Loader = loader ?? new ResourcesBytesConfigGroupLoader("moba_bytes");
            Tables = tables;
        }
    }

    /// <summary>
    /// 传统 JSON 格式配置组
    /// </summary>
    public sealed class LegacyJsonConfigGroup : IConfigGroup
    {
        public string Name => ConfigGroupNames.LegacyJson;

        public IConfigGroupLoader Loader { get; }

        public IConfigGroupDeserializer Deserializer => LegacyJsonConfigGroupDeserializer.Instance;

        public IReadOnlyList<ConfigTableDefinition> Tables { get; }

        public LegacyJsonConfigGroup(params ConfigTableDefinition[] tables)
        {
            Loader = new ResourcesTextConfigGroupLoader("moba");
            Tables = tables;
        }

        public LegacyJsonConfigGroup(IConfigGroupLoader loader, params ConfigTableDefinition[] tables)
        {
            Loader = loader ?? new ResourcesTextConfigGroupLoader("moba");
            Tables = tables;
        }
    }
}

using System.Collections.Generic;

namespace AbilityKit.Ability.Impl.BattleDemo.Moba.Config
{
    /// <summary>
    /// Luban 二进制格式配置组
    /// </summary>
    public sealed class LubanBinaryConfigGroup : IConfigGroup
    {
        public string Name => ConfigGroupNames.LubanBinary;

        public IConfigGroupLoader Loader { get; }

        public IConfigGroupDeserializer Deserializer => LubanBinaryConfigGroupDeserializer.Instance;

        public IReadOnlyList<ConfigTableEntry> Tables { get; }

        public LubanBinaryConfigGroup(params ConfigTableEntry[] tables)
        {
            Loader = new ResourcesBytesConfigGroupLoader("moba_bytes");
            Tables = tables;
        }

        public LubanBinaryConfigGroup(IConfigGroupLoader loader, params ConfigTableEntry[] tables)
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

        public IReadOnlyList<ConfigTableEntry> Tables { get; }

        public LegacyJsonConfigGroup(params ConfigTableEntry[] tables)
        {
            Loader = new ResourcesTextConfigGroupLoader("moba");
            Tables = tables;
        }

        public LegacyJsonConfigGroup(IConfigGroupLoader loader, params ConfigTableEntry[] tables)
        {
            Loader = loader ?? new ResourcesTextConfigGroupLoader("moba");
            Tables = tables;
        }
    }
}

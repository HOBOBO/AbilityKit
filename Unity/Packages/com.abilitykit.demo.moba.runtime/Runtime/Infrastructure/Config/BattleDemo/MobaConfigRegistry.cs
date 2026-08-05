using System;
using System.Collections.Generic;
using AbilityKit.Ability.Config;
using AbilityKit.Demo.Moba.Config.Core;

namespace AbilityKit.Demo.Moba.Config.BattleDemo
{
    /// <summary>
    /// MOBA config table registry.
    /// </summary>
    public sealed class MobaConfigRegistry : IMobaConfigTableRegistry
    {
        public static readonly MobaConfigRegistry Instance = new MobaConfigRegistry();

        private MobaConfigRegistry() { }

        // IConfigTableRegistry (generic)
        public IReadOnlyList<ConfigTableDefinition> Tables => MobaRuntimeConfigTableRegistry.Tables;

        public ConfigTableDefinition GetTable(string filePath)
        {
            foreach (var t in MobaRuntimeConfigTableRegistry.Tables)
            {
                if (t.FilePath == filePath) return t;
            }
            return null;
        }

        public bool TryGetTable(string filePath, out ConfigTableDefinition definition)
        {
            definition = GetTable(filePath);
            return definition != null;
        }

        // IMobaConfigTableRegistry (MOBA-specific)
        public MobaRuntimeConfigTableRegistry.Entry[] MobaTables => MobaRuntimeConfigTableRegistry.Tables;
    }

    /// <summary>
    /// MOBA runtime config table registry entries.
    /// </summary>
    public static class MobaRuntimeConfigTableRegistry
    {
        public sealed class Entry : ConfigTableDefinition
        {
            /// <summary>
            /// Alias for EntryType used by MOBA runtime code.
            /// </summary>
            public Type MoType => EntryType;

            public Entry(string fileWithoutExt, Type dtoType, Type moType)
                : base(fileWithoutExt, dtoType, moType, groupName: null)
            {
            }

            public Entry(string fileWithoutExt, Type dtoType, Type moType, string groupName)
                : base(fileWithoutExt, dtoType, moType, groupName)
            {
            }

            public Entry(
                string fileWithoutExt,
                Type dtoType,
                Type moType,
                Func<Array, object> dtoTableFactory,
                Func<Array, object> entryTableFactory)
                : this(
                    fileWithoutExt,
                    dtoType,
                    moType,
                    dtoTableFactory,
                    entryTableFactory,
                    null)
            {
            }

            public Entry(
                string fileWithoutExt,
                Type dtoType,
                Type moType,
                Func<Array, object> dtoTableFactory,
                Func<Array, object> entryTableFactory,
                Action<Array, ISet<int>> changedIdCollector)
                : base(
                    fileWithoutExt,
                    dtoType,
                    moType,
                    null,
                    dtoTableFactory,
                    entryTableFactory,
                    changedIdCollector)
            {
            }
        }

        public static readonly Entry[] Tables = MobaGeneratedConfigTableManifest.CreateRegistryEntries();
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Ability.Config;
using AbilityKit.Demo.Moba.Config.BattleDemo;

namespace AbilityKit.Demo.Moba.Config.Core
{
    internal readonly struct MobaConfigTableSpec
    {
        public MobaConfigTableSpec(
            string filePath,
            Type dtoType,
            Type moType,
            string groupName,
            int order,
            Func<Array, object> dtoTableFactory = null,
            Func<Array, object> entryTableFactory = null,
            Action<Array, ISet<int>> changedIdCollector = null)
        {
            FilePath = filePath;
            DtoType = dtoType;
            MoType = moType;
            GroupName = groupName;
            Order = order;
            DtoTableFactory = dtoTableFactory;
            EntryTableFactory = entryTableFactory;
            ChangedIdCollector = changedIdCollector;
        }

        public string FilePath { get; }
        public Type DtoType { get; }
        public Type MoType { get; }
        public string GroupName { get; }
        public int Order { get; }
        public Func<Array, object> DtoTableFactory { get; }
        public Func<Array, object> EntryTableFactory { get; }
        public Action<Array, ISet<int>> ChangedIdCollector { get; }
    }

    internal static partial class MobaGeneratedConfigTableManifest
    {
        private static readonly MobaConfigTableSpec[] Specs = CreateSpecs();

        public static MobaRuntimeConfigTableRegistry.Entry[] CreateRegistryEntries()
        {
            var result = new MobaRuntimeConfigTableRegistry.Entry[Specs.Length];
            for (var i = 0; i < Specs.Length; i++)
            {
                var spec = Specs[i];
                result[i] = new MobaRuntimeConfigTableRegistry.Entry(
                    spec.FilePath,
                    spec.DtoType,
                    spec.MoType,
                    spec.DtoTableFactory,
                    spec.EntryTableFactory,
                    spec.ChangedIdCollector);
            }

            return result;
        }

        public static ConfigTableDefinition[] CreateGroupDefinitions(string groupName)
        {
            var result = new List<ConfigTableDefinition>(Specs.Length);
            for (var i = 0; i < Specs.Length; i++)
            {
                var spec = Specs[i];
                if (!string.Equals(spec.GroupName, groupName, StringComparison.Ordinal)) continue;
                result.Add(new ConfigTableDefinition(
                    spec.FilePath,
                    spec.DtoType,
                    spec.MoType,
                    spec.GroupName,
                    spec.DtoTableFactory,
                    spec.EntryTableFactory,
                    spec.ChangedIdCollector));
            }

            return result.ToArray();
        }

        private static MobaConfigTableSpec[] CreateSpecs()
        {
            var specs = new List<MobaConfigTableSpec>(32);
            var generatedCount = 0;
            AddGenerated(specs, ref generatedCount);
            if (generatedCount == 0)
            {
                if (AppContext.TryGetSwitch(
                        "AbilityKit.Moba.DisableConfigTableReflectionFallback",
                        out var reflectionFallbackDisabled) && reflectionFallbackDisabled)
                {
                    throw new InvalidOperationException(
                        "The generated MOBA config table manifest is empty and reflection fallback is disabled.");
                }

                AddReflected(specs);
            }

            specs.Sort(CompareSpecs);
            ValidateUniqueSpecs(specs);
            return specs.ToArray();
        }

        private static void AddReflected(List<MobaConfigTableSpec> specs)
        {
            var attributes = typeof(MobaGeneratedConfigTableManifest).Assembly.GetCustomAttributes(
                typeof(MobaConfigTableAttribute),
                inherit: false);
            for (var i = 0; i < attributes.Length; i++)
            {
                if (!(attributes[i] is MobaConfigTableAttribute attribute)) continue;
                specs.Add(new MobaConfigTableSpec(
                    attribute.FilePath,
                    attribute.DtoType,
                    attribute.MoType,
                    attribute.GroupName,
                    attribute.Order));
            }
        }

        private static int CompareSpecs(MobaConfigTableSpec left, MobaConfigTableSpec right)
        {
            var order = left.Order.CompareTo(right.Order);
            if (order != 0) return order;
            return string.CompareOrdinal(left.FilePath, right.FilePath);
        }

        private static void ValidateUniqueSpecs(IReadOnlyList<MobaConfigTableSpec> specs)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            var dtoTypes = new HashSet<Type>();
            var moTypes = new HashSet<Type>();
            for (var i = 0; i < specs.Count; i++)
            {
                var spec = specs[i];
                if (!paths.Add(spec.FilePath))
                    throw new InvalidOperationException($"Duplicate MOBA config table path '{spec.FilePath}'.");
                if (!dtoTypes.Add(spec.DtoType))
                    throw new InvalidOperationException($"Duplicate MOBA config DTO type '{spec.DtoType.FullName}'.");
                if (!moTypes.Add(spec.MoType))
                    throw new InvalidOperationException($"Duplicate MOBA config MO type '{spec.MoType.FullName}'.");
            }
        }

        static partial void AddGenerated(List<MobaConfigTableSpec> specs, ref int count);
    }
}

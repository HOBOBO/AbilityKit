using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.Config;
using AbilityKit.Demo.Moba.Config.BattleDemo;
using Luban;
using Newtonsoft.Json.Linq;

namespace AbilityKit.Demo.Moba.Config.Core
{
    public static class MobaLubanConfigGroups
    {
        public const string JsonDirectory = "luban/moba";
        public const string BinaryDirectory = "luban/moba_bytes";

        public static IReadOnlyList<IConfigGroup> Create(
            ITextAssetLoader assets,
            IReadOnlyCollection<string> binaryTables,
            bool useLubanJsonForRemaining = false)
        {
            if (assets == null) throw new ArgumentNullException(nameof(assets));
            if (binaryTables == null) throw new ArgumentNullException(nameof(binaryTables));
            var selected = new HashSet<string>(binaryTables, StringComparer.Ordinal);
            var known = new HashSet<string>(MobaRuntimeConfigTableRegistry.Tables.Select(t => t.FilePath), StringComparer.Ordinal);
            foreach (var name in selected)
            {
                if (!known.Contains(name)) throw new ArgumentException($"Unknown MOBA config table: {name}");
            }
            selected.Remove(MobaConfigPaths.SkillFlowsFile);

            var binary = MobaRuntimeConfigTableRegistry.Tables.Where(t => selected.Contains(t.FilePath)).Cast<ConfigTableDefinition>().ToArray();
            var remaining = MobaRuntimeConfigTableRegistry.Tables.Where(t => !selected.Contains(t.FilePath) &&
                t.FilePath != MobaConfigPaths.SkillFlowsFile).Cast<ConfigTableDefinition>().ToArray();
            var flows = MobaRuntimeConfigTableRegistry.Tables.Where(t => t.FilePath == MobaConfigPaths.SkillFlowsFile)
                .Cast<ConfigTableDefinition>().ToArray();
            var groups = new List<IConfigGroup>(3);
            if (binary.Length > 0)
            {
                groups.Add(new ConfigGroup(ConfigGroupNames.LubanBinary,
                    new MobaLubanAssetGroupLoader(assets, BinaryDirectory, binary: true),
                    MobaLubanBinaryGroupDeserializer.Instance, binary));
            }
            if (remaining.Length > 0)
            {
                groups.Add(new ConfigGroup(useLubanJsonForRemaining ? "LubanJson" : ConfigGroupNames.LegacyJson,
                    new MobaLubanAssetGroupLoader(assets, useLubanJsonForRemaining ? JsonDirectory : MobaConfigPaths.DefaultResourcesDir, binary: false),
                    MobaLubanJsonGroupDeserializer.Instance, remaining));
            }
            groups.Add(new ConfigGroup("SkillPipeline",
                new MobaLubanAssetGroupLoader(assets, MobaConfigPaths.DefaultResourcesDir, binary: false),
                MobaLubanJsonGroupDeserializer.Instance, flows));
            return groups;
        }
    }

    public sealed class LubanGroupsMobaConfigLoadProfile : IMobaConfigLoadProfile
    {
        private readonly ITextAssetLoader _assets;
        private readonly string[] _binaryTables;

        public LubanGroupsMobaConfigLoadProfile(ITextAssetLoader assets, IEnumerable<string> binaryTables = null)
        {
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _binaryTables = binaryTables?.ToArray() ??
                MobaRuntimeConfigTableRegistry.Tables.Where(table => table.FilePath != MobaConfigPaths.SkillFlowsFile)
                    .Select(table => table.FilePath).ToArray();
        }

        public string Name => "LubanGroups";

        public void Load(MobaConfigDatabase database)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            database.LoadFromGroups(MobaLubanConfigGroups.Create(_assets, _binaryTables));
        }

        public void Load(MobaConfigDatabase database, IMobaConfigLoadPipeline pipeline) => Load(database);
    }

    public sealed class MobaLubanAssetGroupLoader : IConfigGroupLoader
    {
        private readonly ITextAssetLoader _assets;
        private readonly bool _binary;

        public MobaLubanAssetGroupLoader(ITextAssetLoader assets, string resourcesDir, bool binary)
        {
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            ResourcesDir = resourcesDir ?? throw new ArgumentNullException(nameof(resourcesDir));
            _binary = binary;
        }

        public string ResourcesDir { get; }

        public bool TryLoad(string tableName, out byte[] bytes, out string text)
        {
            bytes = null;
            text = null;
            var stem = ResourcesDir + "/" + tableName;
            if (_binary)
            {
                return _assets.TryLoadBytes(stem + ".bytes", out bytes) || _assets.TryLoadBytes(stem, out bytes);
            }
            return _assets.TryLoadText(stem + ".json", out text) || _assets.TryLoadText(stem, out text);
        }
    }

    public sealed class MobaLubanJsonGroupDeserializer : ConfigGroupDeserializerBase
    {
        public static readonly MobaLubanJsonGroupDeserializer Instance = new MobaLubanJsonGroupDeserializer();

        private MobaLubanJsonGroupDeserializer() { }

        public override Array DeserializeFromBytes(byte[] bytes, Type dtoType) => throw new NotSupportedException();

        public override Array DeserializeFromText(string text, Type dtoType) =>
            JsonNetMobaConfigDtoDeserializer.Instance.DeserializeDtoArray(text, dtoType);

        public override bool CanHandle(Type dtoType) =>
            MobaRuntimeConfigTableRegistry.Tables.Any(t => t.DtoType == dtoType);
    }

    public sealed class MobaLubanBinaryGroupDeserializer : ConfigGroupDeserializerBase
    {
        public static readonly MobaLubanBinaryGroupDeserializer Instance = new MobaLubanBinaryGroupDeserializer();

        private MobaLubanBinaryGroupDeserializer() { }

        public override Array DeserializeFromBytes(byte[] bytes, Type dtoType)
        {
            if (bytes == null || bytes.Length == 0) throw new ArgumentException("Empty Luban table", nameof(bytes));
            var table = MobaRuntimeConfigTableRegistry.Tables.FirstOrDefault(t => t.DtoType == dtoType);
            if (table == null) throw new NotSupportedException($"Unknown MOBA DTO: {dtoType.FullName}");
            var className = string.Concat(table.FilePath.Split('_').Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));
            var generatedType = typeof(moba_luban.Tables).Assembly.GetType("moba_luban." + className, throwOnError: true);
            var generatedTable = Activator.CreateInstance(generatedType, ByteBuf.Wrap(bytes));
            var rows = (System.Collections.IEnumerable)generatedType.GetProperty("DataList").GetValue(generatedTable);
            var array = new JArray();
            foreach (var row in rows)
            {
                var json = JObject.FromObject(row);
                var omitted = new HashSet<string>((json.Value<string>("OmittedFields") ?? string.Empty)
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
                var nulls = new HashSet<string>((json.Value<string>("NullFields") ?? string.Empty)
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
                json.Remove("OmittedFields");
                json.Remove("NullFields");
                foreach (var property in json.Properties().ToArray())
                {
                    if (property.Value.Type != JTokenType.String) continue;
                    var value = property.Value.Value<string>();
                    var member = dtoType.GetField(property.Name) as System.Reflection.MemberInfo ?? dtoType.GetProperty(property.Name);
                    var memberType = member is System.Reflection.FieldInfo field ? field.FieldType :
                        member is System.Reflection.PropertyInfo info ? info.PropertyType : null;
                    if (memberType == null || memberType == typeof(string)) continue;
                    if (string.IsNullOrEmpty(value))
                    {
                        if (memberType.IsArray) property.Value = new JArray();
                        else if (!memberType.IsValueType) property.Value = JValue.CreateNull();
                    }
                    else if (value[0] == '[' || value[0] == '{')
                    {
                        property.Value = JToken.Parse(value);
                    }
                }
                foreach (var field in omitted)
                {
                    var value = json[field];
                    if (IsDefault(value)) json.Remove(field);
                }
                foreach (var field in nulls)
                {
                    if (IsDefault(json[field])) json[field] = JValue.CreateNull();
                }
                array.Add(json);
            }
            return MobaLubanJsonGroupDeserializer.Instance.DeserializeFromText(array.ToString(), dtoType);
        }

        public override Array DeserializeFromText(string text, Type dtoType) => throw new NotSupportedException();

        public override bool CanHandle(Type dtoType) => MobaLubanJsonGroupDeserializer.Instance.CanHandle(dtoType);

        private static bool IsDefault(JToken value)
        {
            if (value == null || value.Type == JTokenType.Null) return true;
            if (value is JArray array) return array.Count == 0;
            if (value.Type == JTokenType.String) return value.Value<string>() == string.Empty;
            if (value.Type == JTokenType.Boolean) return !value.Value<bool>();
            if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float) return value.Value<double>() == 0;
            return false;
        }
    }
}

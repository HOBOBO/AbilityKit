using System;
using System.Collections.Generic;
using System.Linq;
using AbilityKit.Ability.Config;
using AbilityKit.Demo.Moba.Config.BattleDemo;
using AbilityKit.Demo.Moba.Share.Config;
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
            bool useLubanJsonForRemaining = true)
        {
            if (assets == null) throw new ArgumentNullException(nameof(assets));
            if (binaryTables == null) throw new ArgumentNullException(nameof(binaryTables));
            AbilityKit.Demo.Moba.Services.MobaGameplayTagCatalog.RegisterDefinitions(assets);
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
                    new MobaLubanBinaryGroupDeserializer(assets), binary));
            }
            if (remaining.Length > 0)
            {
                groups.Add(new ConfigGroup(useLubanJsonForRemaining ? "LubanJson" : ConfigGroupNames.LegacyJson,
                    new MobaLubanAssetGroupLoader(assets, useLubanJsonForRemaining ? JsonDirectory : MobaConfigPaths.DefaultResourcesDir, binary: false),
                    useLubanJsonForRemaining
                        ? MobaLubanJsonGroupDeserializer.Create(assets)
                        : MobaLubanJsonGroupDeserializer.Instance, remaining));
            }
            groups.Add(new ConfigGroup("SkillPipeline",
                new MobaLubanAssetGroupLoader(assets, MobaConfigPaths.DefaultResourcesDir, binary: false),
                MobaLubanJsonGroupDeserializer.Create(assets), flows));
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

        private readonly ITextAssetLoader _assets;
        private Dictionary<int, JObject> _modifiers;

        private Dictionary<int, JObject> ModifierDefinitions()
        {
            if (_modifiers != null) return _modifiers;
            byte[] templates, definitions;
            if (_assets == null ||
                !_assets.TryLoadBytes(MobaLubanConfigGroups.BinaryDirectory + "/modifier_templates.bytes", out templates) ||
                !_assets.TryLoadBytes(MobaLubanConfigGroups.BinaryDirectory + "/modifier_definitions.bytes", out definitions) ||
                templates == null || templates.Length == 0 || definitions == null || definitions.Length == 0)
                throw new InvalidOperationException(
                    "Missing Luban reference tables: modifier_templates and modifier_definitions");
            _modifiers = MobaLubanBinaryGroupDeserializer.FlattenModifierDefinitions(templates, definitions);
            return _modifiers;
        }

        private MobaLubanJsonGroupDeserializer(ITextAssetLoader assets = null)
        {
            _assets = assets;
        }

        public static MobaLubanJsonGroupDeserializer Create(ITextAssetLoader assets) =>
            new MobaLubanJsonGroupDeserializer(assets);

        public override Array DeserializeFromBytes(byte[] bytes, Type dtoType) => throw new NotSupportedException();

        public override Array DeserializeFromText(string text, Type dtoType)
        {
            if (dtoType == typeof(BuffDTO) || dtoType == typeof(ContinuousProcessDTO))
            {
                var array = JArray.Parse(text);
                var definitions = ModifierDefinitions();
                var buff = dtoType == typeof(BuffDTO);
                foreach (var row in array.OfType<JObject>())
                {
                    var owner = $"{dtoType.Name}/{row.Value<int>("Id")}";
                    MobaLubanBinaryGroupDeserializer.ResolveModifierBindings(row, definitions, owner, buff);
                }
                // Deserialize the rewritten array; the original text still carries ModifierIds.
                return JsonNetMobaConfigDtoDeserializer.Instance.DeserializeDtoArray(array.ToString(), dtoType);
            }
            return JsonNetMobaConfigDtoDeserializer.Instance.DeserializeDtoArray(text, dtoType);
        }

        public override bool CanHandle(Type dtoType) =>
            MobaRuntimeConfigTableRegistry.Tables.Any(t => t.DtoType == dtoType);
    }

    public sealed class MobaLubanBinaryGroupDeserializer : ConfigGroupDeserializerBase
    {
        public static readonly MobaLubanBinaryGroupDeserializer Instance = new MobaLubanBinaryGroupDeserializer();

        private readonly ITextAssetLoader _assets;
        private Dictionary<int, JObject> _modifiers;
        private Dictionary<int, JObject> _tags;
        private Dictionary<int, JObject> _characterLoadouts;
        private AbilityKit.Demo.Moba.Services.MobaTagWorkbookRuntime _tagWorkbook;

        private MobaLubanBinaryGroupDeserializer() { }

        public MobaLubanBinaryGroupDeserializer(ITextAssetLoader assets)
        {
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        }

        /// <summary>
        /// Flattens modifier templates by merging their definition's semantics. Templates only
        /// record which lifecycle mounts an effect and how it is scoped, so the authoritative
        /// target/op/magnitude data comes from modifier_definitions.
        /// </summary>
        internal static Dictionary<int, JObject> FlattenModifierDefinitions(byte[] templateBytes,
            byte[] definitionBytes)
        {
            var definitions = new Dictionary<int, JObject>();
            foreach (var row in new moba_luban.ModifierDefinitions(ByteBuf.Wrap(definitionBytes)).DataList)
            {
                var flat = FlattenModifierDefinition(row);
                definitions[row.Id] = flat;
            }
            var result = new Dictionary<int, JObject>();
            foreach (var row in new moba_luban.ModifierTemplates(ByteBuf.Wrap(templateBytes)).DataList)
            {
                if (!definitions.TryGetValue(row.DefinitionId, out var definition))
                    throw new InvalidOperationException(
                        $"modifier_templates/{row.Id}: missing modifier definition {row.DefinitionId}");
                var flat = (JObject)definition.DeepClone();
                flat["Id"] = row.Id;
                flat["Kind"] = (int)row.Kind;
                flat["DefinitionId"] = row.DefinitionId;
                flat["Priority"] = row.Priority;
                foreach (var name in SelectorFieldNames) flat[name] = 0;
                foreach (var token in (row.Selectors ?? string.Empty).Split(';'))
                {
                    if (token.Trim().Length == 0) continue;
                    var parts = token.Split('|');
                    var field = parts[0].Trim();
                    var value = parts.Length > 1 && int.TryParse(parts[1], out var parsed) ? parsed : 0;
                    if (SelectorColumns.TryGetValue(field, out var column)) flat[column] = value;
                    else throw new InvalidOperationException(
                        $"modifier_templates/{row.Id}: unknown selector field {field}");
                }
                result[row.Id] = flat;
            }
            return result;
        }

        private static JObject FlattenModifierDefinition(moba_luban.DRModifierDefinitions row)
        {
            var flat = new JObject
            {
                ["TargetKind"] = (int)row.Target.Kind,
                ["TargetId"] = row.Target.Id,
                ["Op"] = (int)row.Value.Op,
                ["Value"] = row.Value.Value,
                // Priority is authored on the mount row, not on the definition.
                ["Priority"] = 0,
                ["MagnitudeSourceType"] = (int)row.Magnitude.SourceType,
                ["MagnitudeBaseValue"] = row.Magnitude.BaseValue,
                ["MagnitudeCoefficient"] = row.Magnitude.Coefficient,
                ["MagnitudeDuration"] = row.Magnitude.DurationSec,
                ["MagnitudeDecayType"] = row.Magnitude.DecayType,
                ["MagnitudeAttributeTypeId"] = row.Magnitude.AttributeTypeId,
                ["MagnitudeContextKey"] = row.ContextKey,
                ["MagnitudeCurve"] = new JArray(row.Curve),
                // Mirrors the exporter: a magnitude that reads other state must be sampled once,
                // everything else stays realtime. Keep this in sync with
                // LubanConfig/Moba/business/modifiers.py::derive_evaluation_policy.
                ["EvaluationPolicy"] = DeriveEvaluationPolicy(row.Magnitude.SourceType),
                ["AttributeSourceRole"] = (int)row.AttributeSourceRole,
                ["CandidateSkillId"] = row.Candidate.SkillId,
                ["CandidateReferenceId"] = row.Candidate.ReferenceId,
                ["FormulaConstant"] = row.FormulaConstant,
                ["FormulaLevelCoefficient"] = row.FormulaLevelCoefficient,
                ["FormulaTerms"] = new JArray(row.FormulaTerms),
                ["DefinitionKey"] = row.Key,
                ["MountPoint"] = (int)row.MountPoint,
                ["StackingPolicy"] = (int)row.StackingPolicy,
                ["MaxStacks"] = row.MaxStacks,
                ["ConflictGroup"] = (int)row.ConflictGroup,
            };
            foreach (var name in SelectorFieldNames) flat[name] = 0;
            return flat;
        }

        /// <summary>
        /// Attribute and LinearFormula magnitudes read other state, so they are sampled once and
        /// replayed from the capture store; magnitudes derived from the continuous instance itself
        /// are already deterministic and stay realtime.
        /// </summary>
        private static int DeriveEvaluationPolicy(moba_luban.MagnitudeSourceType source)
        {
            return source == moba_luban.MagnitudeSourceType.Attribute ||
                   source == moba_luban.MagnitudeSourceType.LinearFormula
                ? (int)moba_luban.ModifierEvaluationPolicy.OnApplySnapshot
                : (int)moba_luban.ModifierEvaluationPolicy.Realtime;
        }

        /// <summary>
        /// Replaces a Buff/ContinuousProcess row's ModifierIds with the resolved Modifiers array.
        /// Buff rows keep their historical null-normalization so JSON and binary loading agree.
        /// </summary>
        internal static void ResolveModifierBindings(JObject row, Dictionary<int, JObject> definitions,
            string owner, bool buff)
        {
            // Buff rows may arrive already expanded (binary restore ran first); keep that result.
            if (row["Modifiers"] is JArray) return;
            var modifierIds = row["ModifierIds"] as JArray ?? new JArray();
            row.Remove("ModifierIds");
            var resolved = new JArray();
            var seen = new HashSet<int>();
            foreach (var token in modifierIds)
            {
                var id = token.Value<int>();
                if (!seen.Add(id)) throw new InvalidOperationException($"{owner}: duplicate modifier {id}");
                var definition = definitions.TryGetValue(id, out var value) ? value : null;
                if (definition == null || definition.Value<int>("Kind") != (buff ? 0 : 1))
                    throw new InvalidOperationException($"{owner}: missing or wrong-kind modifier {id}");
                var modifier = (JObject)definition.DeepClone();
                modifier.Remove("Id");
                modifier.Remove("Kind");
                if (buff)
                {
                    if (modifier.Value<string>("MagnitudeContextKey") == string.Empty)
                        modifier["MagnitudeContextKey"] = JValue.CreateNull();
                    if (modifier["MagnitudeCurve"] is JArray curve && curve.Count == 0)
                        modifier["MagnitudeCurve"] = JValue.CreateNull();
                }
                resolved.Add(modifier);
            }
            row["Modifiers"] = resolved;
        }

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
                RestoreReferences(json, dtoType);
                if (dtoType == typeof(SearchQueryTemplateDTO))
                {
                    RemoveNullMembers(json["Rules"]);
                    RemoveNullMembers(json["Scorers"]);
                }
                if (dtoType == typeof(BattleMapDTO) && json["CollisionObjects"] is JArray collisions)
                {
                    foreach (var collision in collisions.OfType<JObject>())
                    {
                        RestoreEnumName<moba_luban.MapCollisionShapeType>(collision, "ShapeType");
                        RestoreEnumName<moba_luban.MapProjectileResponse>(collision, "ProjectileResponse");
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
            return MobaLubanJsonGroupDeserializer.Create(_assets).DeserializeFromText(array.ToString(), dtoType);
        }

        public override Array DeserializeFromText(string text, Type dtoType) => throw new NotSupportedException();

        public override bool CanHandle(Type dtoType) => MobaLubanJsonGroupDeserializer.Instance.CanHandle(dtoType);

        private void RestoreReferences(JObject row, Type dtoType)
        {
            if (dtoType == typeof(ContinuousTagTemplateDTO))
            {
                if (_tagWorkbook == null)
                    _tagWorkbook = new AbilityKit.Demo.Moba.Services.MobaTagWorkbookRuntime(_assets);
                _tagWorkbook.RestoreTemplate(row);
            }
            if (dtoType == typeof(BattleAttributeTemplateDTO))
            {
                var id = row.Value<int>("Id");
                var loadout = CharacterLoadouts().TryGetValue(id, out var value) ? value : null;
                row["ActiveSkills"] = loadout?["ActiveSkills"]?.DeepClone() ?? new JArray();
                row["PassiveSkills"] = loadout?["PassiveSkills"]?.DeepClone() ?? new JArray();
            }
            if (dtoType == typeof(SkillDTO))
            {
                ValidateTagValues(row, "Tags", moba_luban.MobaTagNamespace.SkillSelection);
                if (_tagWorkbook == null)
                    _tagWorkbook = new AbilityKit.Demo.Moba.Services.MobaTagWorkbookRuntime(_assets);
                _tagWorkbook.RestoreSkill(row);
            }
            if (dtoType == typeof(SummonDTO) || dtoType == typeof(GameplayDTO) || dtoType == typeof(DungeonDTO))
            {
                if (((JArray)row["Tags"]).Count != 0)
                    throw new InvalidOperationException($"{dtoType.Name}/{row.Value<int>("Id")}: unused Tags must be empty");
            }
            if (dtoType == typeof(BuffDTO) || dtoType == typeof(ContinuousProcessDTO))
            {
                var buff = dtoType == typeof(BuffDTO);
                var owner = $"{dtoType.Name}/{row.Value<int>("Id")}";
                ResolveModifierBindings(row, ModifierDefinitions(), owner, buff);
                if (buff)
                {
                    RestoreTagIds(row, "TagIds", "TagNames",
                        moba_luban.MobaTagNamespace.GameplayState);
                    RestoreTagIds(row, "DispelBlockedByTagIds", "DispelBlockedByTagNames",
                        moba_luban.MobaTagNamespace.GameplayState);
                }
                else
                {
                    RestoreTagIds(row, "TagIds", "Tags",
                        moba_luban.MobaTagNamespace.GameplayState);
                }
            }
        }

        private void ValidateTagValues(JObject row, string field, moba_luban.MobaTagNamespace expectedNamespace)
        {
            var owner = $"{row.Value<int>("Id")}.{field}";
            var seen = new HashSet<int>();
            foreach (var token in (JArray)row[field])
            {
                var id = token.Value<int>();
                if (!seen.Add(id)) throw new InvalidOperationException($"{owner}: duplicate tag {id}");
                if (!TagDefinitions().TryGetValue(id, out var definition) ||
                    definition.Value<int>("Namespace") != (int)expectedNamespace ||
                    definition.Value<int>("Status") != (int)moba_luban.MobaTagStatus.Active ||
                    definition.Value<int>("Kind") != (int)moba_luban.MobaTagKind.Fact)
                    throw new InvalidOperationException($"{owner}: missing or wrong-namespace tag {id}");
            }
        }

        private void RestoreTagIds(JObject row, string source, string target,
            moba_luban.MobaTagNamespace expectedNamespace)
        {
            var ids = (JArray)row[source];
            if (source != "DispelBlockedByTagIds")
                ValidateTagValues(row, source, expectedNamespace);
            row.Remove(source);
            var resolved = new JArray();
            var seen = new HashSet<int>();
            foreach (var token in ids)
            {
                var id = token.Value<int>();
                if (!seen.Add(id) || !TagDefinitions().TryGetValue(id, out var definition) ||
                    definition.Value<int>("Namespace") != (int)expectedNamespace ||
                    definition.Value<int>("Status") != (int)moba_luban.MobaTagStatus.Active)
                    throw new InvalidOperationException($"{row.Value<int>("Id")}.{source}: invalid tag {id}");
                var name = definition.Value<string>("Name");
                resolved.Add(name);
            }
            row[target] = resolved;
        }

        private Dictionary<int, JObject> ModifierDefinitions()
        {
            if (_modifiers != null) return _modifiers;
            _modifiers = FlattenModifierDefinitions(LoadReferenceBytes("modifier_templates"),
                LoadReferenceBytes("modifier_definitions"));
            return _modifiers;
        }

        private static readonly Dictionary<string, string> SelectorColumns = new Dictionary<string, string>
        {
            [nameof(moba_luban.ModifierSelectorField.Slot)] = "SelectorSlot",
            [nameof(moba_luban.ModifierSelectorField.BaseSkillId)] = "SelectorBaseSkillId",
            [nameof(moba_luban.ModifierSelectorField.EffectiveSkillId)] = "SelectorEffectiveSkillId",
            [nameof(moba_luban.ModifierSelectorField.LauncherTemplateId)] = "SelectorLauncherTemplateId",
            [nameof(moba_luban.ModifierSelectorField.ProjectileTemplateId)] = "SelectorProjectileTemplateId",
            [nameof(moba_luban.ModifierSelectorField.AreaTemplateId)] = "SelectorAreaTemplateId",
            [nameof(moba_luban.ModifierSelectorField.SummonTemplateId)] = "SelectorSummonTemplateId",
            [nameof(moba_luban.ModifierSelectorField.BuffTemplateId)] = "SelectorBuffTemplateId",
        };

        private static readonly string[] SelectorFieldNames =
        {
            "SelectorSlot", "SelectorBaseSkillId", "SelectorEffectiveSkillId",
            "SelectorLauncherTemplateId", "SelectorProjectileTemplateId", "SelectorAreaTemplateId",
            "SelectorSummonTemplateId", "SelectorBuffTemplateId",
        };

        private Dictionary<int, JObject> CharacterLoadouts()
        {
            if (_characterLoadouts != null) return _characterLoadouts;
            var table = new moba_luban.Characters(ByteBuf.Wrap(LoadReferenceBytes("characters")));
            var loadouts = new Dictionary<int, JObject>();
            foreach (var character in table.DataList)
            {
                var candidate = new JObject
                {
                    ["ActiveSkills"] = JArray.FromObject(character.SkillIds),
                    ["PassiveSkills"] = JArray.FromObject(character.PassiveSkillIds)
                };
                if (loadouts.TryGetValue(character.AttributeTemplateId, out var existing) &&
                    !JToken.DeepEquals(existing, candidate))
                    throw new InvalidOperationException(
                        $"attribute_templates/{character.AttributeTemplateId}: characters disagree on skill loadout");
                loadouts[character.AttributeTemplateId] = candidate;
            }
            _characterLoadouts = loadouts;
            return _characterLoadouts;
        }

        private Dictionary<int, JObject> TagDefinitions()
        {
            if (_tags != null) return _tags;
            var table = new moba_luban.TagDefinitions(ByteBuf.Wrap(LoadReferenceBytes("tag_definitions")));
            _tags = table.DataList.ToDictionary(row => row.Id, row => JObject.FromObject(row));
            if (_tags.Values.Select(value => value.Value<int>("Namespace") + ":" + value.Value<string>("Name"))
                    .Distinct(StringComparer.Ordinal).Count() != _tags.Count ||
                _tags.Values.Any(value => string.IsNullOrEmpty(value.Value<string>("Name"))))
                throw new InvalidOperationException("tag_definitions has duplicate or empty names");
            foreach (var definition in _tags)
            {
                var value = definition.Value;
                var tagNamespace = value.Value<int>("Namespace");
                if (!Enum.IsDefined(typeof(moba_luban.MobaTagNamespace), tagNamespace) ||
                    !Enum.IsDefined(typeof(moba_luban.MobaTagKind), value.Value<int>("Kind")) ||
                    !Enum.IsDefined(typeof(moba_luban.MobaTagStatus), value.Value<int>("Status")))
                    throw new InvalidOperationException($"tag_definitions/{definition.Key}: invalid namespace or name");
            }
            return _tags;
        }

        private byte[] LoadReferenceBytes(string name)
        {
            if (_assets == null || !_assets.TryLoadBytes(MobaLubanConfigGroups.BinaryDirectory + "/" + name + ".bytes", out var bytes)
                || bytes == null || bytes.Length == 0)
                throw new InvalidOperationException($"Missing Luban reference table: {name}");
            return bytes;
        }

        private static void RestoreEnumName<TEnum>(JObject row, string field) where TEnum : struct, Enum
        {
            if (row[field]?.Type != JTokenType.Integer) return;
            row[field] = Enum.GetName(typeof(TEnum), row.Value<int>(field));
        }

        private static void RemoveNullMembers(JToken value)
        {
            if (value is JArray array)
            {
                foreach (var item in array) RemoveNullMembers(item);
            }
            else if (value is JObject obj)
            {
                foreach (var property in obj.Properties().ToArray())
                {
                    if (property.Value.Type == JTokenType.Null) property.Remove();
                }
            }
        }

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

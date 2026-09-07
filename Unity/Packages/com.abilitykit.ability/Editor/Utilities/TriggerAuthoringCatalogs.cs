using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using AbilityKit.Ability.Config.Authoring;

namespace AbilityKit.Ability.Editor.Utilities
{
    internal sealed class TriggerEventDescriptorCatalog
    {
        private readonly List<TriggerEventDefinitionData> _definitions = new List<TriggerEventDefinitionData>();
        private readonly Dictionary<string, TriggerEventDefinitionData> _exact =
            new Dictionary<string, TriggerEventDefinitionData>(StringComparer.Ordinal);
        private readonly List<TriggerEventDefinitionData> _prefixes = new List<TriggerEventDefinitionData>();

        public TriggerEventDescriptorCatalog(IEnumerable<TriggerEventDefinitionData> definitions)
        {
            if (definitions == null) return;
            foreach (var definition in definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id)) continue;
                _definitions.Add(definition);
                if (definition.MatchMode == TriggerEventMatchMode.Prefix)
                    _prefixes.Add(definition);
                else
                    _exact[definition.Id] = definition;
            }

            _prefixes.Sort((left, right) => right.Id.Length.CompareTo(left.Id.Length));
        }

        public IReadOnlyList<TriggerEventDefinitionData> Definitions => _definitions;

        public bool TryResolve(string eventId, out TriggerEventDefinitionData definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(eventId)) return false;
            if (_exact.TryGetValue(eventId, out definition)) return true;
            for (var i = 0; i < _prefixes.Count; i++)
            {
                var candidate = _prefixes[i];
                if (!eventId.StartsWith(candidate.Id, StringComparison.Ordinal)) continue;
                definition = candidate;
                return true;
            }
            return false;
        }

        public static TriggerEventDescriptorCatalog FromAsset(TriggerEventCatalogAsset asset)
        {
            return asset == null ? null : new TriggerEventDescriptorCatalog(asset.Events);
        }
    }

    internal sealed class TriggerEventCatalogScanResult
    {
        public readonly List<TriggerEventDefinitionData> Events = new List<TriggerEventDefinitionData>();
        public int ScannedAttributeCount;
        public int AddedCount;
        public int UpdatedCount;
    }

    internal static class TriggerEventCatalogAssemblyScanner
    {
        public static TriggerEventCatalogScanResult ScanLoadedAssemblies()
        {
            var result = new TriggerEventCatalogScanResult();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
                ScanAssembly(assemblies[i], result);
            result.Events.Sort((left, right) => string.Compare(left.Id, right.Id, StringComparison.Ordinal));
            return result;
        }

        public static TriggerEventCatalogScanResult MergeInto(
            IList<TriggerEventDefinitionData> target,
            IEnumerable<TriggerEventDefinitionData> discovered)
        {
            var result = new TriggerEventCatalogScanResult();
            if (target == null || discovered == null) return result;
            foreach (var item in discovered)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Id)) continue;
                var existing = FindEvent(target, item.Id, item.MatchMode);
                if (existing == null)
                {
                    target.Add(item);
                    result.AddedCount++;
                    continue;
                }

                MergeEvent(existing, item);
                result.UpdatedCount++;
            }
            return result;
        }

        private static void ScanAssembly(Assembly assembly, TriggerEventCatalogScanResult result)
        {
            if (assembly == null) return;
            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }
            catch
            {
                return;
            }

            if (types == null) return;
            for (var i = 0; i < types.Length; i++)
            {
                var type = types[i];
                if (type == null) continue;
                object[] attributes;
                try
                {
                    attributes = type.GetCustomAttributes(false);
                }
                catch
                {
                    continue;
                }

                for (var attributeIndex = 0; attributeIndex < attributes.Length; attributeIndex++)
                {
                    if (!TryCreateEventDefinition(attributes[attributeIndex], out var definition)) continue;
                    result.Events.Add(definition);
                    result.ScannedAttributeCount++;
                }
            }
        }

        private static bool TryCreateEventDefinition(object attribute, out TriggerEventDefinitionData definition)
        {
            definition = null;
            if (attribute == null) return false;
            var attributeType = attribute.GetType();
            if (!string.Equals(attributeType.Name, "MobaTriggerEventAttribute", StringComparison.Ordinal) &&
                !string.Equals(attributeType.Name, "TriggerEventAttribute", StringComparison.Ordinal))
            {
                return false;
            }

            var id = ReadStringProperty(attribute, "EventIdOrPrefix") ??
                     ReadStringProperty(attribute, "EventId") ??
                     ReadStringProperty(attribute, "Id") ??
                     ReadStringProperty(attribute, "Prefix");
            if (string.IsNullOrWhiteSpace(id)) return false;

            var argsType = ReadTypeProperty(attribute, "ArgsType") ??
                           ReadTypeProperty(attribute, "PayloadType") ??
                           ReadTypeProperty(attribute, "EventArgsType");
            var isPrefix = ReadBoolProperty(attribute, "IsPrefix");
            definition = new TriggerEventDefinitionData
            {
                Id = id,
                MatchMode = isPrefix ? TriggerEventMatchMode.Prefix : TriggerEventMatchMode.Exact,
                DisplayName = BuildDisplayName(id, isPrefix),
                Category = BuildCategory(id),
                PayloadType = argsType != null ? argsType.FullName : string.Empty,
                PayloadFields = BuildPayloadFields(argsType),
                AllowExternal = false,
                Deterministic = true,
                Description = "Discovered from " + attributeType.FullName
            };
            return true;
        }

        private static TriggerEventDefinitionData FindEvent(
            IList<TriggerEventDefinitionData> events,
            string id,
            TriggerEventMatchMode matchMode)
        {
            for (var i = 0; i < events.Count; i++)
            {
                var item = events[i];
                if (item != null &&
                    item.MatchMode == matchMode &&
                    string.Equals(item.Id, id, StringComparison.Ordinal))
                    return item;
            }
            return null;
        }

        private static void MergeEvent(TriggerEventDefinitionData target, TriggerEventDefinitionData source)
        {
            if (string.IsNullOrWhiteSpace(target.DisplayName)) target.DisplayName = source.DisplayName;
            if (string.IsNullOrWhiteSpace(target.Category)) target.Category = source.Category;
            if (string.IsNullOrWhiteSpace(target.PayloadType)) target.PayloadType = source.PayloadType;
            if (target.PayloadFields == null || target.PayloadFields.Count == 0)
                target.PayloadFields = source.PayloadFields ?? new List<TriggerPayloadFieldData>();
            if (string.IsNullOrWhiteSpace(target.Description)) target.Description = source.Description;
        }

        private static List<TriggerPayloadFieldData> BuildPayloadFields(Type payloadType)
        {
            var fields = new List<TriggerPayloadFieldData>();
            BuildPayloadFields(payloadType, string.Empty, fields, new HashSet<Type>(), 0);
            return fields;
        }

        private static void BuildPayloadFields(
            Type payloadType,
            string prefix,
            ICollection<TriggerPayloadFieldData> fields,
            ISet<Type> visiting,
            int depth)
        {
            if (payloadType == null || payloadType == typeof(string) || depth > 4) return;
            payloadType = Nullable.GetUnderlyingType(payloadType) ?? payloadType;
            if (!visiting.Add(payloadType)) return;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
            var properties = payloadType.GetProperties(flags);
            for (var i = 0; i < properties.Length; i++)
            {
                var property = properties[i];
                if (property.GetIndexParameters().Length > 0) continue;
                AddPayloadField(fields, prefix, property.Name, property.PropertyType);
                if (ShouldFlattenPayloadType(property.PropertyType))
                    BuildPayloadFields(
                        property.PropertyType,
                        AppendPath(prefix, ToSnakeCase(property.Name)),
                        fields,
                        visiting,
                        depth + 1);
            }

            var publicFields = payloadType.GetFields(flags);
            for (var i = 0; i < publicFields.Length; i++)
            {
                var field = publicFields[i];
                AddPayloadField(fields, prefix, field.Name, field.FieldType);
                if (ShouldFlattenPayloadType(field.FieldType))
                    BuildPayloadFields(
                        field.FieldType,
                        AppendPath(prefix, ToSnakeCase(field.Name)),
                        fields,
                        visiting,
                        depth + 1);
            }
            visiting.Remove(payloadType);
        }

        private static void AddPayloadField(
            ICollection<TriggerPayloadFieldData> fields,
            string prefix,
            string memberName,
            Type memberType)
        {
            if (!TryMapValueType(memberType, out var valueType) && !ShouldFlattenPayloadType(memberType)) return;
            if (valueType == TriggerValueType.None) valueType = TriggerValueType.Object;
            var path = AppendPath(prefix, ToSnakeCase(memberName));
            if (ContainsPayloadField(fields, path)) return;
            fields.Add(new TriggerPayloadFieldData
            {
                Path = path,
                DisplayName = string.IsNullOrWhiteSpace(prefix) ? memberName : prefix + "." + memberName,
                Type = valueType
            });
        }

        private static bool ContainsPayloadField(IEnumerable<TriggerPayloadFieldData> fields, string path)
        {
            foreach (var field in fields)
            {
                if (field != null && string.Equals(field.Path, path, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static bool ShouldFlattenPayloadType(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type == null || type == typeof(string) || type.IsPrimitive || type.IsEnum) return false;
            if (type == typeof(decimal) || type == typeof(DateTime)) return false;
            if (TryMapValueType(type, out var mapped) && mapped != TriggerValueType.None && mapped != TriggerValueType.Object)
                return false;
            return type.IsClass || type.IsValueType;
        }

        private static string AppendPath(string prefix, string segment)
        {
            if (string.IsNullOrWhiteSpace(prefix)) return segment ?? string.Empty;
            if (string.IsNullOrWhiteSpace(segment)) return prefix;
            return prefix + "." + segment;
        }

        private static bool TryMapValueType(Type type, out TriggerValueType valueType)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type == typeof(bool))
            {
                valueType = TriggerValueType.Boolean;
                return true;
            }
            if (type == typeof(string))
            {
                valueType = TriggerValueType.String;
                return true;
            }
            if (type == typeof(byte) || type == typeof(sbyte) ||
                type == typeof(short) || type == typeof(ushort) ||
                type == typeof(int) || type == typeof(uint) ||
                type == typeof(long) || type == typeof(ulong) ||
                type.IsEnum)
            {
                valueType = TriggerValueType.Integer;
                return true;
            }
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            {
                valueType = TriggerValueType.Number;
                return true;
            }
            if (string.Equals(type.Name, "Vector3", StringComparison.Ordinal) ||
                string.Equals(type.FullName, "UnityEngine.Vector3", StringComparison.Ordinal))
            {
                valueType = TriggerValueType.Vector3;
                return true;
            }

            valueType = TriggerValueType.None;
            return false;
        }

        private static string ReadStringProperty(object target, string name)
        {
            return ReadProperty(target, name) as string;
        }

        private static Type ReadTypeProperty(object target, string name)
        {
            return ReadProperty(target, name) as Type;
        }

        private static bool ReadBoolProperty(object target, string name)
        {
            var value = ReadProperty(target, name);
            return value is bool boolean && boolean;
        }

        private static object ReadProperty(object target, string name)
        {
            var property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return property != null ? property.GetValue(target, null) : null;
        }

        private static string BuildCategory(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "Events";
            var index = id.IndexOf('.');
            var category = index > 0 ? id.Substring(0, index) : "Events";
            return BuildDisplayName(category, false);
        }

        private static string BuildDisplayName(string id, bool prefix)
        {
            if (string.IsNullOrWhiteSpace(id)) return string.Empty;
            var normalized = id.Trim('.');
            var builder = new StringBuilder(normalized.Length + 8);
            var upperNext = true;
            for (var i = 0; i < normalized.Length; i++)
            {
                var ch = normalized[i];
                if (ch == '.' || ch == '_' || ch == '-')
                {
                    builder.Append(' ');
                    upperNext = true;
                    continue;
                }
                builder.Append(upperNext ? char.ToUpperInvariant(ch) : ch);
                upperNext = false;
            }
            if (prefix) builder.Append(" Family");
            return builder.ToString();
        }

        private static string ToSnakeCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var builder = new StringBuilder(value.Length + 8);
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                if (char.IsUpper(ch))
                {
                    if (i > 0) builder.Append('_');
                    builder.Append(char.ToLowerInvariant(ch));
                }
                else
                {
                    builder.Append(ch == ' ' || ch == '-' ? '_' : ch);
                }
            }
            return builder.ToString();
        }
    }

    internal sealed class TriggerGlobalBlackboardDescriptorCatalog
    {
        private readonly List<TriggerGlobalBlackboardKeyData> _definitions =
            new List<TriggerGlobalBlackboardKeyData>();
        private readonly Dictionary<string, TriggerGlobalBlackboardKeyData> _keys =
            new Dictionary<string, TriggerGlobalBlackboardKeyData>(StringComparer.Ordinal);

        public TriggerGlobalBlackboardDescriptorCatalog(IEnumerable<TriggerGlobalBlackboardKeyData> keys)
        {
            if (keys == null) return;
            foreach (var key in keys)
            {
                if (key == null || string.IsNullOrWhiteSpace(key.Key)) continue;
                _definitions.Add(key);
                _keys[key.Key] = key;
            }
        }

        public IReadOnlyList<TriggerGlobalBlackboardKeyData> Definitions => _definitions;

        public bool TryGet(string key, out TriggerGlobalBlackboardKeyData definition)
        {
            return _keys.TryGetValue(key ?? string.Empty, out definition);
        }

        public static TriggerGlobalBlackboardDescriptorCatalog FromAsset(TriggerGlobalBlackboardCatalogAsset asset)
        {
            return asset == null ? null : new TriggerGlobalBlackboardDescriptorCatalog(asset.Keys);
        }
    }

    internal sealed class TriggerTemplateDescriptorCatalog
    {
        private readonly List<TriggerAuthoringTemplateAsset> _definitions =
            new List<TriggerAuthoringTemplateAsset>();
        private readonly Dictionary<string, TriggerAuthoringTemplateAsset> _templates =
            new Dictionary<string, TriggerAuthoringTemplateAsset>(StringComparer.Ordinal);
        private readonly HashSet<string> _ambiguous = new HashSet<string>(StringComparer.Ordinal);

        public TriggerTemplateDescriptorCatalog(IEnumerable<TriggerAuthoringTemplateAsset> templates)
        {
            if (templates == null) return;
            foreach (var asset in templates)
            {
                if (asset == null) continue;
                _definitions.Add(asset);
                var id = asset.Template != null ? asset.Template.TemplateId : null;
                if (string.IsNullOrWhiteSpace(id)) continue;
                if (_templates.ContainsKey(id)) _ambiguous.Add(id);
                else _templates.Add(id, asset);
            }
        }

        public IReadOnlyList<TriggerAuthoringTemplateAsset> Definitions => _definitions;

        public bool TryGet(string templateId, out TriggerAuthoringTemplateAsset asset)
        {
            return _templates.TryGetValue(templateId ?? string.Empty, out asset) &&
                   !_ambiguous.Contains(templateId ?? string.Empty);
        }

        public bool IsAmbiguous(string templateId)
        {
            return _ambiguous.Contains(templateId ?? string.Empty);
        }

        public static TriggerTemplateDescriptorCatalog FromAsset(TriggerAuthoringTemplateCatalogAsset asset)
        {
            return asset == null ? null : new TriggerTemplateDescriptorCatalog(asset.Templates);
        }
    }

    internal sealed class TriggerAuthoringValidationContext
    {
        public TriggerTypeDescriptorCatalog Types;
        public TriggerEventDescriptorCatalog Events;
        public TriggerGlobalBlackboardDescriptorCatalog GlobalBlackboard;
        public TriggerTemplateDescriptorCatalog Templates;

        public static TriggerAuthoringValidationContext Create(TriggerAuthoringModuleAsset asset)
        {
            var project = asset != null ? asset.Project : null;
            return new TriggerAuthoringValidationContext
            {
                Types = TriggerTypeDescriptorCatalog.CreateProjectDefaults(),
                Events = TriggerEventDescriptorCatalog.FromAsset(project != null ? project.EventCatalog : null),
                GlobalBlackboard = TriggerGlobalBlackboardDescriptorCatalog.FromAsset(
                    project != null ? project.GlobalBlackboardCatalog : null),
                Templates = TriggerTemplateDescriptorCatalog.FromAsset(
                    project != null ? project.TemplateCatalog : null)
            };
        }

        public static TriggerAuthoringValidationContext Create(TriggerAuthoringTemplateAsset asset)
        {
            var project = asset != null ? asset.Project : null;
            return new TriggerAuthoringValidationContext
            {
                Types = TriggerTypeDescriptorCatalog.CreateProjectDefaults(),
                Events = TriggerEventDescriptorCatalog.FromAsset(project != null ? project.EventCatalog : null),
                GlobalBlackboard = TriggerGlobalBlackboardDescriptorCatalog.FromAsset(
                    project != null ? project.GlobalBlackboardCatalog : null),
                Templates = TriggerTemplateDescriptorCatalog.FromAsset(
                    project != null ? project.TemplateCatalog : null)
            };
        }
    }
}

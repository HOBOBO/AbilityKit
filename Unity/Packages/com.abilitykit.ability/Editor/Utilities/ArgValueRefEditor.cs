using System.Collections.Generic;
using AbilityKit.Ability.Config;
using Sirenix.OdinInspector;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Config.Authoring;
using UnityEditor;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    [System.Serializable]
    [InlineProperty]
    [HideLabel]
    public sealed class ArgValueRefEditor
    {
        private static ArgValueKind _lastConstKind = ArgValueKind.Float;

        [LabelText("来源")]
        [ValueDropdown(nameof(GetValueSourceOptions))]
        public ValueSourceKind Source = ValueSourceKind.Const;

        [LabelText("常量")]
        [ShowIf(nameof(IsConst))]
        public ArgRuntimeEntryCore ConstValue = new ArgRuntimeEntryCore();

        [LabelText("取值作用域")]
        [ShowIf(nameof(IsVar))]
        public VarScope FromScope = VarScope.Local;

        [LabelText("变量名")]
        [InfoBox("当前作用域下没有可用变量", InfoMessageType.Warning, VisibleIf = nameof(HasNoFromKeys))]
        [ShowIf(nameof(IsVar))]
        [ValueDropdown(nameof(GetFromKeyOptions))]
        [OnValueChanged(nameof(OnFromKeyChanged))]
        public string FromKey;

        private bool IsConst => Source == ValueSourceKind.Const;
        private bool IsVar => Source == ValueSourceKind.Var;

        [OnInspectorGUI]
        private void KeepLastConstKind()
        {
            if (!IsConst) return;
            if (ConstValue == null) ConstValue = new ArgRuntimeEntryCore();

            if (ConstValue.Kind == ArgValueKind.None)
            {
                ConstValue.Kind = _lastConstKind;
            }
            else
            {
                _lastConstKind = ConstValue.Kind;
            }
        }

        public ArgValueKind GetExpectedKind()
        {
            if (ConstValue == null) return ArgValueKind.None;
            return ConstValue.Kind;
        }

        public object GetConstBoxedValue()
        {
            return ConstValue != null ? ConstValue.GetBoxedValue() : null;
        }

        private IEnumerable<string> GetFromKeyOptions()
        {
            var expected = GetExpectedKind();
            return VarKeyDropdownUtil.BuildKeys(FromScope, VarKeyUsage.Read, expected);
        }

        private bool HasNoFromKeys()
        {
            if (!IsVar) return false;
            var keys = GetFromKeyOptions();
            if (keys == null) return true;
            using (var e = keys.GetEnumerator())
            {
                return !e.MoveNext();
            }
        }

        private void OnFromKeyChanged()
        {
            if (!IsVar) return;
            if (string.IsNullOrEmpty(FromKey)) return;
            VarKeyRecentUtil.Record(FromScope, FromKey);
        }

        private static IEnumerable<ValueDropdownItem<ValueSourceKind>> GetValueSourceOptions()
        {
            yield return new ValueDropdownItem<ValueSourceKind>("常量", ValueSourceKind.Const);
            yield return new ValueDropdownItem<ValueSourceKind>("变量引用", ValueSourceKind.Var);
        }
    }
}

namespace AbilityKit.Ability.Editor.Utilities
{
    internal sealed class TriggerAuthoringValueRefEditorContext
    {
        private static readonly TriggerPayloadFieldData[] BuiltInContextFields =
        {
            new TriggerPayloadFieldData { Path = "query.id", Type = TriggerValueType.Integer, DisplayName = "Query Id" },
            new TriggerPayloadFieldData { Path = "filter.param", Type = TriggerValueType.Integer, DisplayName = "Filter Param" },
            new TriggerPayloadFieldData { Path = "owner.actor_id", Type = TriggerValueType.Integer, DisplayName = "Owner Actor Id" },
            new TriggerPayloadFieldData { Path = "caster.actor_id", Type = TriggerValueType.Integer, DisplayName = "Caster Actor Id" },
            new TriggerPayloadFieldData { Path = "target.actor_id", Type = TriggerValueType.Integer, DisplayName = "Target Actor Id" },
            new TriggerPayloadFieldData { Path = "source.actor_id", Type = TriggerValueType.Integer, DisplayName = "Source Actor Id" },
            new TriggerPayloadFieldData { Path = "delta_time", Type = TriggerValueType.Number, DisplayName = "Delta Time" }
        };

        public TriggerAuthoringModuleData Module;
        public TriggerDefinitionData Trigger;
        public TriggerEventDescriptorCatalog Events;
        public TriggerGlobalBlackboardDescriptorCatalog GlobalBlackboard;
        public IReadOnlyList<TriggerAuthoringTemplateParameterData> TemplateParameters;
        public IReadOnlyList<TriggerPayloadFieldData> ContextFields = BuiltInContextFields;

        public TriggerEventDefinitionData ResolveEventDefinition()
        {
            if (Trigger == null || Events == null) return null;
            Events.TryResolve(Trigger.Event, out var definition);
            return definition;
        }
    }

    internal readonly struct TriggerAuthoringValuePathOption
    {
        public TriggerAuthoringValuePathOption(
            TriggerValueSource source,
            string path,
            TriggerValueType type,
            string label,
            bool canRead = true,
            bool canWrite = true,
            string unscopedPath = null)
        {
            Source = source;
            Path = path ?? string.Empty;
            Type = type;
            Label = string.IsNullOrWhiteSpace(label) ? Path : label;
            CanRead = canRead;
            CanWrite = canWrite;
            UnscopedPath = unscopedPath ?? Path;
        }

        public TriggerValueSource Source { get; }
        public string Path { get; }
        public TriggerValueType Type { get; }
        public string Label { get; }
        public bool CanRead { get; }
        public bool CanWrite { get; }
        public string UnscopedPath { get; }

        public bool MatchesPath(string path)
        {
            if (string.Equals(Path, path, System.StringComparison.Ordinal)) return true;
            if (Source != TriggerValueSource.LocalBlackboard ||
                TriggerAuthoringLocalBlackboardPath.HasExplicitScope(path))
                return false;
            return TriggerAuthoringLocalBlackboardPath.TryParse(path, out _, out var key) &&
                   string.Equals(UnscopedPath, key, System.StringComparison.Ordinal);
        }
    }

    internal enum TriggerAuthoringLocalBlackboardScope
    {
        Any = 0,
        Module = 1,
        Trigger = 2
    }

    internal static class TriggerAuthoringLocalBlackboardPath
    {
        private const string ModulePrefix = "module:";
        private const string TriggerPrefix = "trigger:";

        public static string Format(TriggerAuthoringLocalBlackboardScope scope, string key)
        {
            key = key ?? string.Empty;
            switch (scope)
            {
                case TriggerAuthoringLocalBlackboardScope.Module: return ModulePrefix + key;
                case TriggerAuthoringLocalBlackboardScope.Trigger: return TriggerPrefix + key;
                default: return key;
            }
        }

        public static bool TryParse(string path, out TriggerAuthoringLocalBlackboardScope scope, out string key)
        {
            scope = TriggerAuthoringLocalBlackboardScope.Any;
            key = path ?? string.Empty;
            if (key.StartsWith(ModulePrefix, System.StringComparison.Ordinal))
            {
                scope = TriggerAuthoringLocalBlackboardScope.Module;
                key = key.Substring(ModulePrefix.Length);
            }
            else if (key.StartsWith(TriggerPrefix, System.StringComparison.Ordinal))
            {
                scope = TriggerAuthoringLocalBlackboardScope.Trigger;
                key = key.Substring(TriggerPrefix.Length);
            }
            return !string.IsNullOrWhiteSpace(key);
        }

        public static bool HasExplicitScope(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   (path.StartsWith(ModulePrefix, System.StringComparison.Ordinal) ||
                    path.StartsWith(TriggerPrefix, System.StringComparison.Ordinal));
        }
    }

    internal static class TriggerAuthoringValueRefEditor
    {
        public static void Draw(
            TriggerValueRefData value,
            TriggerParameterDescriptor parameter,
            TriggerAuthoringValueRefEditorContext context)
        {
            if (value == null) throw new System.ArgumentNullException(nameof(value));
            context = context ?? new TriggerAuthoringValueRefEditorContext();

            var allowed = parameter != null ? parameter.AllowedSources : TriggerValueSourceMask.All;
            DrawSource(value, allowed);
            var expectedType = parameter != null ? parameter.Type : TriggerValueType.None;
            DrawType(value, expectedType);
            var effectiveType = expectedType != TriggerValueType.None ? expectedType : value.Type;
            var access = parameter != null ? parameter.Access : TriggerParameterAccess.Read;

            switch (value.Source)
            {
                case TriggerValueSource.Constant:
                    DrawConstant(value, effectiveType, parameter, context);
                    break;
                case TriggerValueSource.Payload:
                    DrawPathPopup(value, CollectPathOptions(TriggerValueSource.Payload, effectiveType, access, context), "Payload Field");
                    break;
                case TriggerValueSource.Context:
                    DrawPathPopup(value, CollectPathOptions(TriggerValueSource.Context, effectiveType, access, context), "Context Key", true);
                    break;
                case TriggerValueSource.LocalBlackboard:
                    DrawPathPopup(value, CollectPathOptions(TriggerValueSource.LocalBlackboard, effectiveType, access, context), "Local Blackboard");
                    break;
                case TriggerValueSource.GlobalBlackboard:
                    DrawPathPopup(value, CollectPathOptions(TriggerValueSource.GlobalBlackboard, effectiveType, access, context), "Global Blackboard");
                    break;
                case TriggerValueSource.TemplateParameter:
                    DrawPathPopup(value, CollectPathOptions(TriggerValueSource.TemplateParameter, effectiveType, access, context), "Template Parameter");
                    break;
                case TriggerValueSource.Expression:
                    value.Expression = EditorGUILayout.TextField("Expression", value.Expression);
                    break;
                default:
                    value.Path = EditorGUILayout.TextField("Path", value.Path);
                    break;
            }
        }

        public static List<TriggerAuthoringValuePathOption> CollectPathOptions(
            TriggerValueSource source,
            TriggerValueType expectedType,
            TriggerParameterAccess access,
            TriggerAuthoringValueRefEditorContext context)
        {
            var result = new List<TriggerAuthoringValuePathOption>();
            context = context ?? new TriggerAuthoringValueRefEditorContext();
            var write = access == TriggerParameterAccess.Write;

            switch (source)
            {
                case TriggerValueSource.Payload:
                    var eventDefinition = context.ResolveEventDefinition();
                    var fields = eventDefinition != null ? eventDefinition.PayloadFields : null;
                    if (fields != null)
                    {
                        for (var i = 0; i < fields.Count; i++)
                        {
                            var field = fields[i];
                            if (field == null || !TypeMatches(expectedType, field.Type)) continue;
                            result.Add(new TriggerAuthoringValuePathOption(
                                source,
                                field.Path,
                                field.Type,
                                "Payload/" + (string.IsNullOrWhiteSpace(field.DisplayName) ? field.Path : field.DisplayName)));
                        }
                    }
                    break;
                case TriggerValueSource.Context:
                    AddFieldOptions(result, source, context.ContextFields, expectedType, "Context");
                    break;
                case TriggerValueSource.LocalBlackboard:
                    AddLocalBlackboardOptions(
                        result,
                        context.Trigger != null ? context.Trigger.Blackboard : null,
                        expectedType,
                        write,
                        "Trigger Local",
                        TriggerAuthoringLocalBlackboardScope.Trigger);
                    AddLocalBlackboardOptions(
                        result,
                        context.Module != null ? context.Module.Blackboard : null,
                        expectedType,
                        write,
                        "Module Local",
                        TriggerAuthoringLocalBlackboardScope.Module);
                    break;
                case TriggerValueSource.GlobalBlackboard:
                    var keys = context.GlobalBlackboard != null ? context.GlobalBlackboard.Definitions : null;
                    if (keys != null)
                    {
                        for (var i = 0; i < keys.Count; i++)
                        {
                            var key = keys[i];
                            if (key == null || !TypeMatches(expectedType, key.Type)) continue;
                            if (write && !key.CanWrite || !write && !key.CanRead) continue;
                            var domain = string.IsNullOrWhiteSpace(key.Domain) ? "global" : key.Domain;
                            result.Add(new TriggerAuthoringValuePathOption(
                                source,
                                key.Key,
                                key.Type,
                                "Global/" + domain + "/" + (string.IsNullOrWhiteSpace(key.DisplayName) ? key.Key : key.DisplayName),
                                key.CanRead,
                                key.CanWrite));
                        }
                    }
                    break;
                case TriggerValueSource.TemplateParameter:
                    var parameters = context.TemplateParameters;
                    if (parameters != null)
                    {
                        for (var i = 0; i < parameters.Count; i++)
                        {
                            var parameter = parameters[i];
                            if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name) ||
                                !TypeMatches(expectedType, parameter.Type))
                                continue;
                            result.Add(new TriggerAuthoringValuePathOption(
                                source,
                                parameter.Name,
                                parameter.Type,
                                "Template/" + parameter.Name));
                        }
                    }
                    break;
            }

            return result;
        }

        public static List<TriggerAuthoringValuePathOption> CollectReadableNumberPathOptions(
            TriggerAuthoringValueRefEditorContext context)
        {
            return CollectValueReferenceOptions(
                TriggerValueType.Number,
                TriggerParameterAccess.Read,
                TriggerValueSourceMask.Payload |
                TriggerValueSourceMask.Context |
                TriggerValueSourceMask.LocalBlackboard |
                TriggerValueSourceMask.GlobalBlackboard |
                TriggerValueSourceMask.TemplateParameter,
                context);
        }

        public static List<TriggerAuthoringValuePathOption> CollectValueReferenceOptions(
            TriggerValueType expectedType,
            TriggerParameterAccess access,
            TriggerValueSourceMask allowedSources,
            TriggerAuthoringValueRefEditorContext context)
        {
            var result = new List<TriggerAuthoringValuePathOption>();
            var sources = GetAllowedSources(allowedSources);
            for (var i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                if (source == TriggerValueSource.Constant ||
                    source == TriggerValueSource.Expression)
                {
                    continue;
                }

                result.AddRange(CollectPathOptions(source, expectedType, access, context));
            }

            result.Sort((left, right) =>
            {
                var sourceCompare = left.Source.CompareTo(right.Source);
                return sourceCompare != 0
                    ? sourceCompare
                    : string.Compare(left.Label, right.Label, System.StringComparison.Ordinal);
            });
            return result;
        }

        public static bool TypeMatches(TriggerValueType expected, TriggerValueType actual)
        {
            return expected == TriggerValueType.None || expected == actual ||
                   expected == TriggerValueType.Number && actual == TriggerValueType.Integer;
        }

        public static TriggerValueRefData CreateDefaultValue(TriggerParameterDescriptor parameter)
        {
            if (parameter == null) return CreateDefaultValue(TriggerValueType.Number);
            var value = CreateDefaultValue(parameter.Type == TriggerValueType.None
                ? TriggerValueType.Number
                : parameter.Type);
            if (parameter.Type == TriggerValueType.Object)
                AddDefaultObjectFields(value.Fields, parameter.Fields);
            return value;
        }

        public static TriggerValueRefData CreateDefaultValue(TriggerValueType type)
        {
            return new TriggerValueRefData
            {
                Source = TriggerValueSource.Constant,
                Type = type
            };
        }

        public static List<long> ParseIntegerList(string value)
        {
            var result = new List<long>();
            if (string.IsNullOrWhiteSpace(value)) return result;
            var parts = value.Split(',');
            for (var i = 0; i < parts.Length; i++)
            {
                if (long.TryParse(parts[i].Trim(), out var parsed)) result.Add(parsed);
            }
            return result;
        }

        private static void DrawSource(TriggerValueRefData value, TriggerValueSourceMask allowed)
        {
            var sources = GetAllowedSources(allowed);
            var sourceNames = new List<string>(sources.Count + 1);
            var selectedSource = -1;
            for (var i = 0; i < sources.Count; i++)
            {
                sourceNames.Add(GetSourceName(sources[i]));
                if (sources[i] == value.Source) selectedSource = i;
            }
            if (selectedSource < 0)
            {
                sourceNames.Add(GetSourceName(value.Source) + "  [unavailable]");
                selectedSource = sourceNames.Count - 1;
            }

            var nextSource = EditorGUILayout.Popup("Source", selectedSource, sourceNames.ToArray());
            if (nextSource != selectedSource && nextSource < sources.Count)
                value.Source = sources[nextSource];
        }

        private static void DrawType(TriggerValueRefData value, TriggerValueType expectedType)
        {
            if (expectedType == TriggerValueType.None)
                value.Type = (TriggerValueType)EditorGUILayout.EnumPopup("Type", value.Type);
            else
            {
                value.Type = expectedType;
                EditorGUILayout.LabelField("Type", expectedType.ToString());
            }
        }

        private static void DrawConstant(
            TriggerValueRefData value,
            TriggerValueType type,
            TriggerParameterDescriptor parameter,
            TriggerAuthoringValueRefEditorContext context)
        {
            switch (type)
            {
                case TriggerValueType.Integer:
                    if (parameter != null && parameter.Options.Count > 0)
                    {
                        DrawIntegerChoice(value, parameter.Options);
                        break;
                    }
                    value.IntegerValue = EditorGUILayout.LongField("Value", value.IntegerValue);
                    break;
                case TriggerValueType.Entity:
                case TriggerValueType.ObjectId:
                    value.IntegerValue = EditorGUILayout.LongField("Value", value.IntegerValue);
                    break;
                case TriggerValueType.Number:
                    value.NumberValue = EditorGUILayout.DoubleField("Value", value.NumberValue);
                    break;
                case TriggerValueType.Boolean:
                    value.BooleanValue = EditorGUILayout.Toggle("Value", value.BooleanValue);
                    break;
                case TriggerValueType.String:
                    value.StringValue = EditorGUILayout.TextField("Value", value.StringValue);
                    break;
                case TriggerValueType.IntegerList:
                    var current = value.IntegerListValue != null ? string.Join(",", value.IntegerListValue) : string.Empty;
                    var next = EditorGUILayout.TextField("Values", current);
                    if (!string.Equals(current, next, System.StringComparison.Ordinal))
                        value.IntegerListValue = ParseIntegerList(next);
                    break;
                case TriggerValueType.Vector3:
                    value.Vector3Value = value.Vector3Value ?? new TriggerVector3Data();
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label("Value", GUILayout.Width(EditorGUIUtility.labelWidth - 4f));
                    value.Vector3Value.X = EditorGUILayout.DoubleField(value.Vector3Value.X);
                    value.Vector3Value.Y = EditorGUILayout.DoubleField(value.Vector3Value.Y);
                    value.Vector3Value.Z = EditorGUILayout.DoubleField(value.Vector3Value.Z);
                    EditorGUILayout.EndHorizontal();
                    break;
                case TriggerValueType.Object:
                    DrawObjectFields(value, parameter, context);
                    break;
                default:
                    EditorGUILayout.HelpBox("Choose a value type.", MessageType.Info);
                    break;
            }
        }

        private static void DrawObjectFields(
            TriggerValueRefData value,
            TriggerParameterDescriptor parameter,
            TriggerAuthoringValueRefEditorContext context)
        {
            value.Fields = value.Fields ?? new List<TriggerArgumentData>();
            if (parameter != null && parameter.Fields.Count > 0)
            {
                DrawObjectSchemaFields(value, parameter, context);
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Fields", EditorStyles.miniBoldLabel);
            for (var i = 0; i < value.Fields.Count; i++)
            {
                var index = i;
                var field = value.Fields[i] ?? (value.Fields[i] = new TriggerArgumentData());
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                field.Name = EditorGUILayout.TextField(field.Name);
                var remove = GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(22f));
                EditorGUILayout.EndHorizontal();
                field.Value = field.Value ?? new TriggerValueRefData();
                Draw(field.Value, null, context);
                EditorGUILayout.EndVertical();
                if (remove)
                {
                    value.Fields.RemoveAt(index);
                    i--;
                }
            }

            if (GUILayout.Button("Add Field", EditorStyles.miniButton))
                value.Fields.Add(new TriggerArgumentData
                {
                    Name = CreateUniqueFieldName(value.Fields),
                    Value = new TriggerValueRefData
                    {
                        Source = TriggerValueSource.Constant,
                        Type = TriggerValueType.Number
                    }
                });
            EditorGUILayout.EndVertical();
        }

        private static void DrawObjectSchemaFields(
            TriggerValueRefData value,
            TriggerParameterDescriptor parameter,
            TriggerAuthoringValueRefEditorContext context)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Fields", EditorStyles.miniBoldLabel);

            for (var i = 0; i < parameter.Fields.Count; i++)
            {
                var fieldParameter = parameter.Fields[i];
                if (fieldParameter == null || string.IsNullOrWhiteSpace(fieldParameter.Name)) continue;
                var field = TriggerAuthoringArgumentPathResolver.FindField(value.Fields, fieldParameter.Name);
                if (field == null)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label(fieldParameter.Name, fieldParameter.Required ? EditorStyles.miniBoldLabel : EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Add", EditorStyles.miniButton, GUILayout.Width(42f)))
                    {
                        value.Fields.Add(new TriggerArgumentData
                        {
                            Name = fieldParameter.Name,
                            Value = CreateDefaultValue(fieldParameter)
                        });
                    }
                    EditorGUILayout.EndHorizontal();
                    continue;
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(fieldParameter.Name, EditorStyles.miniBoldLabel);
                GUILayout.FlexibleSpace();
                if (!fieldParameter.Required && GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(22f)))
                {
                    value.Fields.Remove(field);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    continue;
                }
                EditorGUILayout.EndHorizontal();
                field.Value = field.Value ?? CreateDefaultValue(fieldParameter);
                Draw(field.Value, fieldParameter, context);
                EditorGUILayout.EndVertical();
            }

            for (var i = 0; i < value.Fields.Count; i++)
            {
                var field = value.Fields[i];
                if (field == null || HasFieldParameter(parameter.Fields, field.Name)) continue;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                field.Name = EditorGUILayout.TextField(field.Name);
                var remove = GUILayout.Button("x", EditorStyles.miniButton, GUILayout.Width(22f));
                EditorGUILayout.EndHorizontal();
                field.Value = field.Value ?? CreateDefaultValue(TriggerValueType.Number);
                Draw(field.Value, null, context);
                EditorGUILayout.EndVertical();
                if (remove)
                {
                    value.Fields.RemoveAt(i);
                    i--;
                }
            }

            if (GUILayout.Button("Add Extra Field", EditorStyles.miniButton))
                value.Fields.Add(new TriggerArgumentData
                {
                    Name = CreateUniqueFieldName(value.Fields),
                    Value = CreateDefaultValue(TriggerValueType.Number)
                });
            EditorGUILayout.EndVertical();
        }

        private static void AddDefaultObjectFields(
            ICollection<TriggerArgumentData> output,
            IReadOnlyList<TriggerParameterDescriptor> fields)
        {
            if (output == null || fields == null) return;
            var createdGroups = new HashSet<string>(System.StringComparer.Ordinal);
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                if (field == null) continue;
                if (field.Required ||
                    !string.IsNullOrEmpty(field.RequiredGroup) && createdGroups.Add(field.RequiredGroup))
                {
                    output.Add(new TriggerArgumentData
                    {
                        Name = field.Name,
                        Value = CreateDefaultValue(field)
                    });
                }
            }
        }

        private static bool HasFieldParameter(IReadOnlyList<TriggerParameterDescriptor> fields, string name)
        {
            if (fields == null) return false;
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                if (field != null && string.Equals(field.Name, name, System.StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static string CreateUniqueFieldName(IReadOnlyList<TriggerArgumentData> fields)
        {
            var suffix = 1;
            var name = "field";
            while (ContainsFieldName(fields, name))
            {
                suffix++;
                name = "field" + suffix;
            }
            return name;
        }

        private static bool ContainsFieldName(IReadOnlyList<TriggerArgumentData> fields, string name)
        {
            if (fields == null) return false;
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                if (field != null && string.Equals(field.Name, name, System.StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static void DrawIntegerChoice(
            TriggerValueRefData value,
            IReadOnlyList<TriggerParameterOption> options)
        {
            var names = new List<string>(options.Count + 1);
            var selected = -1;
            for (var i = 0; i < options.Count; i++)
            {
                var option = options[i];
                names.Add(option.DisplayName + "  [" + option.Value + "]");
                if (option.Value == value.IntegerValue) selected = i;
            }
            if (selected < 0)
            {
                names.Add(value.IntegerValue + "  [unavailable]");
                selected = names.Count - 1;
            }

            var next = EditorGUILayout.Popup("Value", selected, names.ToArray());
            if (next != selected && next < options.Count)
                value.IntegerValue = options[next].Value;
        }

        private static void DrawPathPopup(
            TriggerValueRefData value,
            List<TriggerAuthoringValuePathOption> options,
            string label,
            bool allowManualPath = false)
        {
            options = options ?? new List<TriggerAuthoringValuePathOption>();
            var names = new List<string> { "<None>" };
            var selected = 0;
            for (var i = 0; i < options.Count; i++)
            {
                names.Add(options[i].Label + "  [" + options[i].Path + ", " + options[i].Type + "]");
                if (string.Equals(options[i].Path, value.Path, System.StringComparison.Ordinal)) selected = i + 1;
                else if (selected == 0 && options[i].MatchesPath(value.Path)) selected = i + 1;
            }
            if (selected == 0 && !string.IsNullOrWhiteSpace(value.Path))
            {
                names.Add(value.Path + "  [unavailable]");
                selected = names.Count - 1;
            }

            var next = EditorGUILayout.Popup(label, selected, names.ToArray());
            if (next == 0)
            {
                value.Path = string.Empty;
            }
            else if (next <= options.Count)
            {
                var option = options[next - 1];
                value.Path = option.Path;
                value.Type = option.Type;
            }

            if (allowManualPath)
                value.Path = EditorGUILayout.TextField(label + " Path", value.Path);
        }

        private static void AddFieldOptions(
            ICollection<TriggerAuthoringValuePathOption> output,
            TriggerValueSource source,
            IReadOnlyList<TriggerPayloadFieldData> fields,
            TriggerValueType expectedType,
            string prefix)
        {
            if (fields == null) return;
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                if (field == null || string.IsNullOrWhiteSpace(field.Path) || !TypeMatches(expectedType, field.Type)) continue;
                output.Add(new TriggerAuthoringValuePathOption(
                    source,
                    field.Path,
                    field.Type,
                    prefix + "/" + (string.IsNullOrWhiteSpace(field.DisplayName) ? field.Path : field.DisplayName)));
            }
        }

        private static void AddLocalBlackboardOptions(
            ICollection<TriggerAuthoringValuePathOption> output,
            IReadOnlyList<TriggerBlackboardVariableData> variables,
            TriggerValueType expectedType,
            bool write,
            string scope,
            TriggerAuthoringLocalBlackboardScope localScope)
        {
            if (variables == null) return;
            for (var i = 0; i < variables.Count; i++)
            {
                var variable = variables[i];
                if (variable == null || string.IsNullOrWhiteSpace(variable.Key)) continue;
                if (write && variable.ReadOnly || !TypeMatches(expectedType, variable.Type)) continue;
                output.Add(new TriggerAuthoringValuePathOption(
                    TriggerValueSource.LocalBlackboard,
                    TriggerAuthoringLocalBlackboardPath.Format(localScope, variable.Key),
                    variable.Type,
                    scope + "/" + variable.Key,
                    true,
                    !variable.ReadOnly,
                    variable.Key));
            }
        }

        private static List<TriggerValueSource> GetAllowedSources(TriggerValueSourceMask mask)
        {
            var result = new List<TriggerValueSource>();
            foreach (TriggerValueSource source in System.Enum.GetValues(typeof(TriggerValueSource)))
            {
                var sourceMask = (TriggerValueSourceMask)(1 << (int)source);
                if ((mask & sourceMask) != 0) result.Add(source);
            }
            if (result.Count == 0) result.Add(TriggerValueSource.Constant);
            return result;
        }

        private static string GetSourceName(TriggerValueSource source)
        {
            switch (source)
            {
                case TriggerValueSource.LocalBlackboard: return "Local Blackboard";
                case TriggerValueSource.GlobalBlackboard: return "Global Blackboard";
                case TriggerValueSource.TemplateParameter: return "Template Parameter";
                default: return source.ToString();
            }
        }
    }
}

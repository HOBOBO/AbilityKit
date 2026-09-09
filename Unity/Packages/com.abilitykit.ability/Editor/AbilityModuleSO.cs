using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.Share.CoreDtos;
using AbilityKit.Effect;
using AbilityKit.Ability.Share.Effect;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    public sealed class AbilityModuleSO : ScriptableObject
    {
        [HorizontalGroup("Top", Width = 220)]
        [LabelText("技能 ID")]
        public string AbilityId;

        [ListDrawerSettings(Expanded = true)]
        [LabelText("触发器")]
        public List<TriggerEditorConfig> Triggers = new List<TriggerEditorConfig>();
    }

    [System.Serializable]
    public sealed class TriggerEditorConfig : ISerializationCallbackReceiver
    {
        [NonSerialized]
        internal AbilityModuleSO Owner;

        [LabelText("基础配置")]
        public TriggerHeaderDTO Core = new TriggerHeaderDTO();

        [HorizontalGroup("Row", Width = 60)]
        [HideLabel]
        public bool Enabled = true;

        [HorizontalGroup("Row", Width = 140)]
        [LabelText("触发器 ID")]
        [LabelWidth(55)]
        [ShowInInspector]
        public int TriggerId
        {
            get => Core != null ? Core.TriggerId : 0;
            set
            {
                if (Core == null) Core = new TriggerHeaderDTO();
                Core.TriggerId = value;
            }
        }

        [HorizontalGroup("Row")]
        [LabelText("事件 ID")]
        [LabelWidth(50)]
        [ValueDropdown(nameof(GetEventIdOptions), IsUniqueList = true, DropdownTitle = "事件 ID")]
        [ShowInInspector]
        public string EventId
        {
            get => Core != null ? Core.EventId : null;
            set
            {
                if (Core == null) Core = new TriggerHeaderDTO();
                Core.EventId = value;
            }
        }

        [TextArea]
        [LabelText("备注")]
        public string Note;

        [InfoBox("@GetLocalVarValidationMessage()", InfoMessageType.Error, VisibleIf = nameof(HasLocalVarValidationError))]
        [LabelText("局部变量")]
        [ListDrawerSettings(Expanded = true, CustomAddFunction = nameof(AddLocalVar))]
        public List<LocalVarEntry> LocalVars = new List<LocalVarEntry>();

        [OnInspectorGUI]
        private void CaptureEditorContext()
        {
            AbilityEditorVarKeyContext.CurrentTrigger = this;
        }

        [SerializeReference]
        [HideReferenceObjectPicker]
        [LabelText("条件列表")]
        [ListDrawerSettings(Expanded = true, ListElementLabelName = "DisplayTitle", CustomAddFunction = nameof(AddConditionStrong))]
        public List<ConditionEditorConfigBase> ConditionsStrong = new List<ConditionEditorConfigBase>();

        [SerializeReference]
        [HideReferenceObjectPicker]
        [LabelText("行为列表")]
        [ListDrawerSettings(Expanded = true, ListElementLabelName = "DisplayTitle", CustomAddFunction = nameof(AddActionStrong))]
        public List<ActionEditorConfigBase> ActionsStrong = new List<ActionEditorConfigBase>();

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (Core == null) Core = new TriggerHeaderDTO();
        }

        private static IEnumerable<ValueDropdownItem<string>> GetEventIdOptions()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                CollectConstStrings(set, FindType("AbilityKit.Impl.Moba.Services.MobaTriggerEventIds"));
                CollectConstStrings(set, FindType("AbilityKit.Impl.Moba.Services.Skill.MobaSkillTriggering+Events"));
                CollectConstStrings(set, typeof(EffectTriggering.Events));
                CollectConstStrings(set, typeof(AreaTriggering.Events));
                CollectConstStrings(set, typeof(ProjectileTriggering.Events));
            }
            catch
            {
                // ignored
            }

            var list = new List<string>(set);
            list.Sort(StringComparer.Ordinal);

            var items = new List<ValueDropdownItem<string>>(list.Count + 1)
            {
                new ValueDropdownItem<string>("<未选择>", string.Empty)
            };

            for (int i = 0; i < list.Count; i++)
            {
                var id = list[i];
                if (string.IsNullOrEmpty(id)) continue;
                items.Add(new ValueDropdownItem<string>(id, id));
            }

            return items;
        }

        private static void CollectConstStrings(HashSet<string> output, Type type)
        {
            if (output == null || type == null) return;

            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            for (int i = 0; i < fields.Length; i++)
            {
                var f = fields[i];
                if (f == null) continue;
                if (f.FieldType != typeof(string)) continue;
                if (!f.IsLiteral || f.IsInitOnly) continue;
                var v = f.GetRawConstantValue() as string;
                if (string.IsNullOrEmpty(v)) continue;
                output.Add(v);
            }
        }

        private static Type FindType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return null;

            try
            {
                var t = Type.GetType(fullName, throwOnError: false);
                if (t != null) return t;

                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    var a = assemblies[i];
                    if (a == null) continue;
                    t = a.GetType(fullName, throwOnError: false);
                    if (t != null) return t;
                }
            }
            catch
            {
            }

            return null;
        }

        private void AddLocalVar()
        {
            LocalVarMenuBuilder.ShowAddMenu(Owner, LocalVars, list => LocalVars = list);
        }

        private bool HasLocalVarValidationError()
        {
            return LocalVarValidator.HasValidationError(LocalVars);
        }

        private string GetLocalVarValidationMessage()
        {
            return LocalVarValidator.BuildValidationMessage(LocalVars);
        }

        private void AddConditionStrong()
        {
            StrongConfigTypeSelector.ShowAddConditionSelector(ConditionsStrong, Owner);
        }

        private void AddActionStrong()
        {
            StrongConfigTypeSelector.ShowAddActionSelector(ActionsStrong, Owner);
        }
    }

    [Serializable]
    [InlineProperty]
    [HideLabel]
    public sealed class LocalVarEntry
    {
        [HorizontalGroup("Row", Width = 220)]
        [GUIColor(nameof(GetKeyColor))]
        [LabelText("变量键")]
        public string Key;

        [HorizontalGroup("Row", Width = 110)]
        [LabelText("类型")]
        [ValueDropdown(nameof(GetKindOptions))]
        public ArgValueKind Kind;

        [HorizontalGroup("Row", Width = 56)]
        [LabelText("只读")]
        public bool ReadOnly;

        [HorizontalGroup("Row")]
        [ShowIf(nameof(IsInt))]
        [LabelText("整数值")]
        public int IntValue;

        [HorizontalGroup("Row")]
        [ShowIf(nameof(IsFloat))]
        [LabelText("数值")]
        public float FloatValue;

        [HorizontalGroup("Row")]
        [ShowIf(nameof(IsBool))]
        [LabelText("布尔值")]
        public bool BoolValue;

        [HorizontalGroup("Row")]
        [ShowIf(nameof(IsString))]
        [LabelText("文本")]
        public string StringValue;

        [HorizontalGroup("Row")]
        [ShowIf(nameof(IsObject))]
        [LabelText("对象")]
        public UnityEngine.Object ObjectValue;

        private static IEnumerable<ValueDropdownItem<ArgValueKind>> GetKindOptions()
        {
            yield return new ValueDropdownItem<ArgValueKind>("未选择", ArgValueKind.None);
            yield return new ValueDropdownItem<ArgValueKind>("整数", ArgValueKind.Int);
            yield return new ValueDropdownItem<ArgValueKind>("数值", ArgValueKind.Float);
            yield return new ValueDropdownItem<ArgValueKind>("布尔值", ArgValueKind.Bool);
            yield return new ValueDropdownItem<ArgValueKind>("文本", ArgValueKind.String);
            yield return new ValueDropdownItem<ArgValueKind>("对象", ArgValueKind.Object);
        }

        private bool IsInt => Kind == ArgValueKind.Int;
        private bool IsFloat => Kind == ArgValueKind.Float;
        private bool IsBool => Kind == ArgValueKind.Bool;
        private bool IsString => Kind == ArgValueKind.String;
        private bool IsObject => Kind == ArgValueKind.Object;

        private Color GetKeyColor()
        {
            return string.IsNullOrEmpty(Key) ? new Color(1f, 0.6f, 0.6f) : Color.white;
        }

        public ArgRuntimeEntryCore ToArgRuntimeEntryCore()
        {
            return new ArgRuntimeEntryCore
            {
                Key = Key,
                Kind = Kind,
                Value = GetTypedValue()
            };
        }

        private object GetTypedValue()
        {
            switch (Kind)
            {
                case ArgValueKind.Int: return IntValue;
                case ArgValueKind.Float: return FloatValue;
                case ArgValueKind.Bool: return BoolValue;
                case ArgValueKind.String: return StringValue;
                case ArgValueKind.Object: return ObjectValue;
                default: return null;
            }
        }
    }
}

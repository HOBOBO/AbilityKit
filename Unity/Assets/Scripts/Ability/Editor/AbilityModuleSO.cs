using System;
using System.Collections.Generic;
using System.Reflection;
using AbilityKit.Ability.Configs;
using AbilityKit.Ability.Share.CoreDtos;
using AbilityKit.Ability.Share.Effect;
using AbilityKit.Ability.Share.Impl.Moba.Services;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    public sealed class AbilityModuleSO : ScriptableObject
    {
        [HorizontalGroup("Top", Width = 220)]
        public string AbilityId;

        [ListDrawerSettings(Expanded = true)]
        public List<TriggerEditorConfig> Triggers = new List<TriggerEditorConfig>();
    }

    [System.Serializable]
    public sealed class TriggerEditorConfig : ISerializationCallbackReceiver
    {
        [NonSerialized]
        internal AbilityModuleSO Owner;

        public TriggerHeaderDTO Core = new TriggerHeaderDTO();

        [HorizontalGroup("Row", Width = 60)]
        [HideLabel]
        public bool Enabled = true;

        [HorizontalGroup("Row", Width = 140)]
        [LabelText("TriggerId")]
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
        [LabelText("EventId")]
        [LabelWidth(50)]
        [ValueDropdown(nameof(GetEventIdOptions), IsUniqueList = true, DropdownTitle = "EventId")]
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

        [HorizontalGroup("Row", Width = 120)]
        [LabelText("AllowExternal")]
        [LabelWidth(90)]
        [ShowInInspector]
        public bool AllowExternal
        {
            get => Core != null && Core.AllowExternal;
            set
            {
                if (Core == null) Core = new TriggerHeaderDTO();
                Core.AllowExternal = value;
            }
        }

        [TextArea]
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
                CollectConstStrings(set, typeof(MobaTriggerEventIds));
                CollectConstStrings(set, typeof(MobaSkillTriggering.Events));
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
                new ValueDropdownItem<string>("<None>", string.Empty)
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
        public string Key;

        [HorizontalGroup("Row", Width = 110)]
        public ArgValueKind Kind;

        [HorizontalGroup("Row", Width = 56)]
        [LabelText("只读")]
        public bool ReadOnly;

        [HorizontalGroup("Row")]
        [ShowIf(nameof(IsInt))]
        public int IntValue;

        [HorizontalGroup("Row")]
        [ShowIf(nameof(IsFloat))]
        public float FloatValue;

        [HorizontalGroup("Row")]
        [ShowIf(nameof(IsBool))]
        public bool BoolValue;

        [HorizontalGroup("Row")]
        [ShowIf(nameof(IsString))]
        public string StringValue;

        [HorizontalGroup("Row")]
        [ShowIf(nameof(IsObject))]
        public UnityEngine.Object ObjectValue;

        private bool IsInt => Kind == ArgValueKind.Int;
        private bool IsFloat => Kind == ArgValueKind.Float;
        private bool IsBool => Kind == ArgValueKind.Bool;
        private bool IsString => Kind == ArgValueKind.String;
        private bool IsObject => Kind == ArgValueKind.Object;

        private Color GetKeyColor()
        {
            return string.IsNullOrEmpty(Key) ? new Color(1f, 0.6f, 0.6f) : Color.white;
        }

        public ArgRuntimeEntry ToArgRuntimeEntry()
        {
            return new ArgRuntimeEntry
            {
                Key = Key,
                Kind = Kind,
                IntValue = IntValue,
                FloatValue = FloatValue,
                BoolValue = BoolValue,
                StringValue = StringValue,
                ObjectValue = ObjectValue
            };
        }
    }
}

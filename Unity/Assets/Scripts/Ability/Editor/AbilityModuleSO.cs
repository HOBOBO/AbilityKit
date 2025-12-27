using System;
using System.Collections.Generic;
using AbilityKit.Configs;
using Sirenix.OdinInspector;
using UnityEngine;

namespace AbilityKit.Ability.Editor
{
    public sealed class AbilityModuleSO : ScriptableObject
    {
        [HorizontalGroup("Top", Width = 220)]
        public string AbilityId;

        [HorizontalGroup("Top")]
        public AbilityRuntimeSO RuntimeAsset;

        [ListDrawerSettings(Expanded = true)]
        public List<TriggerEditorConfig> Triggers = new List<TriggerEditorConfig>();
    }

    [System.Serializable]
    public sealed class TriggerEditorConfig
    {
        [NonSerialized]
        internal AbilityModuleSO Owner;

        [HorizontalGroup("Row", Width = 60)]
        public bool Enabled = true;

        [HorizontalGroup("Row")]
        public string EventId;

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

        public TriggerRuntimeConfig ToRuntime()
        {
            return TriggerRuntimeCompiler.Compile(this);
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

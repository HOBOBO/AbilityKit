using System;
using AbilityKit.Ability.Configs;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;

namespace AbilityKit.Ability.Editor
{
    [Serializable]
    [TriggerConditionType(TriggerConditionTypes.ArgGt, "参数大于", "条件/参数", 10)]
    public sealed class ArgGreaterThanConditionEditorConfig : ConditionEditorConfigBase
    {
        public override string Type => TriggerConditionTypes.ArgGt;

        [LabelText("参数名")]
        public string Key;

        [LabelText("阈值")]
        public ArgValueRefEditor ThresholdRef = new ArgValueRefEditor();

        protected override string GetTitleSuffix()
        {
            if (string.IsNullOrEmpty(Key)) return null;
            if (ThresholdRef != null && ThresholdRef.Source == ValueSourceKind.Var)
            {
                return Key + " > " + (ThresholdRef.FromScope == VarScope.Local ? "局部" : "全局") + ":" + (string.IsNullOrEmpty(ThresholdRef.FromKey) ? "<空>" : ThresholdRef.FromKey);
            }
            var constKind = ThresholdRef != null ? ThresholdRef.GetExpectedKind() : ArgValueKind.None;
            if (constKind == ArgValueKind.Float || constKind == ArgValueKind.Int)
            {
                var boxed = ThresholdRef != null ? ThresholdRef.GetConstBoxedValue() : null;
                if (boxed != null)
                {
                    try
                    {
                        return Key + " > " + System.Convert.ToDouble(boxed).ToString("0.###");
                    }
                    catch
                    {
                    }
                }
            }
            if (ThresholdRef != null && ThresholdRef.ConstValue != null && ThresholdRef.ConstValue.Kind != ArgValueKind.None)
            {
                return Key + " > " + StrongEditorTitleUtil.FormatArg(ThresholdRef.ConstValue);
            }
            return null;
        }

        public override ConditionRuntimeConfigBase ToRuntimeStrong()
        {
            return new ArgGreaterThanConditionConfig
            {
                Key = Key,
                ValueSource = ThresholdRef != null ? ThresholdRef.Source : ValueSourceKind.Const,
                ValueFromScope = ThresholdRef != null ? ThresholdRef.FromScope : VarScope.Local,
                ValueFromKey = ThresholdRef != null ? ThresholdRef.FromKey : null,
                ThresholdValue = ThresholdRef != null && ThresholdRef.ConstValue != null ? ThresholdRef.ConstValue.Clone() : null
            };
        }
    }
}

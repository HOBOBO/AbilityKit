using System;
using AbilityKit.Ability.Config;
using AbilityKit.Ability.Triggering;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;

namespace AbilityKit.Ability.Editor
{
    [Serializable]
    [TriggerConditionType(TriggerConditionTypes.ArgEq, "参数等于", "条件/参数", 0)]
    public sealed class ArgEqConditionEditorConfig : ConditionEditorConfigBase
    {
        public override string Type => TriggerConditionTypes.ArgEq;

        [LabelText("参数名")]
        public string Key;

        [LabelText("期望值")]
        public ArgValueRefEditor Expected = new ArgValueRefEditor();

        protected override string GetTitleSuffix()
        {
            if (string.IsNullOrEmpty(Key)) return null;
            if (Expected != null && Expected.Source == ValueSourceKind.Var)
            {
                return Key + " == " + (Expected.FromScope == VarScope.Local ? "局部" : "全局") + ":" + (string.IsNullOrEmpty(Expected.FromKey) ? "<空>" : Expected.FromKey);
            }
            return Key + " == " + StrongEditorTitleUtil.FormatArg(Expected != null ? Expected.ConstValue : null);
        }

        public override ConditionConfigBase ToRuntimeConfig()
        {
            return new ArgEqConditionConfig
            {
                Key = Key,
                ValueSource = Expected != null ? Expected.Source : ValueSourceKind.Const,
                ValueFromScope = Expected != null ? Expected.FromScope : VarScope.Local,
                ValueFromKey = Expected != null ? Expected.FromKey : null,
                Value = Expected != null && Expected.ConstValue != null ? Expected.ConstValue : null
            };
        }
    }
}

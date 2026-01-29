using System;
using AbilityKit.Ability.Configs;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;

namespace AbilityKit.Ability.Editor
{
    [Serializable]
    [TriggerActionType(TriggerActionTypes.TakeDamage, "承受伤害", "行为/Combat", 0)]
    public sealed class TakeDamageActionEditorConfig : ActionEditorConfigBase
    {
        public override string Type => TriggerActionTypes.TakeDamage;

        [LabelText("伤害值")]
        public float Value;

        [LabelText("倍率")]
        public float Rate = 1f;

        [LabelText("伤害类型")]
        public DamageType DamageType = DamageType.Physical;

        [LabelText("暴击")]
        public CritType Crit = CritType.None;

        [LabelText("原因类型")]
        public DamageReasonKind ReasonKind = DamageReasonKind.Buff;

        [LabelText("原因参数")]
        public int ReasonParam;

        [LabelText("攻击者Key(可选)")]
        public string AttackerKey;

        [LabelText("目标Key(可选)")]
        public string TargetKey;

        protected override string GetTitleSuffix()
        {
            return Value > 0 ? Value.ToString("0.###") : null;
        }

        public override ActionRuntimeConfigBase ToRuntimeStrong()
        {
            return new TakeDamageActionConfig
            {
                Value = Value,
                Rate = Rate,
                DamageType = DamageType,
                Crit = Crit,
                ReasonKind = ReasonKind,
                ReasonParam = ReasonParam,
                AttackerKey = AttackerKey,
                TargetKey = TargetKey,
            };
        }
    }
}

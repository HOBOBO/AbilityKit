using System;
using AbilityKit.Ability.Configs;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Triggering.Runtime;
using Sirenix.OdinInspector;

namespace AbilityKit.Ability.Editor
{
    [Serializable]
    [TriggerActionType(TriggerActionTypes.GiveDamage, "造成伤害", "行为/Combat", 0)]
    public sealed class GiveDamageActionEditorConfig : ActionEditorConfigBase
    {
        public override string Type => TriggerActionTypes.GiveDamage;

        [LabelText("伤害值")]
        public float Value;

        [LabelText("伤害类型")]
        public DamageType DamageType = DamageType.Physical;

        [LabelText("暴击")]
        public CritType Crit = CritType.None;

        [LabelText("原因类型")]
        public DamageReasonKind ReasonKind = DamageReasonKind.Skill;

        [LabelText("原因参数")]
        public int ReasonParam;

        [LabelText("目标Key(可选)")]
        public string TargetKey;

        [LabelText("攻击者Key(可选)")]
        public string AttackerKey;

        [LabelText("查询模板Id(可选)")]
        public int QueryTemplateId;

        [LabelText("AimPosKey(可选)")]
        public string AimPosKey;

        [LabelText("输出扣血日志")]
        public bool Log;

        protected override string GetTitleSuffix()
        {
            return Value > 0 ? Value.ToString("0.###") : null;
        }

        public override ActionRuntimeConfigBase ToRuntimeStrong()
        {
            return new GiveDamageActionConfig
            {
                Value = Value,
                DamageType = DamageType,
                Crit = Crit,
                ReasonKind = ReasonKind,
                ReasonParam = ReasonParam,
                TargetKey = TargetKey,
                AttackerKey = AttackerKey,
                QueryTemplateId = QueryTemplateId,
                AimPosKey = AimPosKey,
                Log = Log,
            };
        }
    }
}

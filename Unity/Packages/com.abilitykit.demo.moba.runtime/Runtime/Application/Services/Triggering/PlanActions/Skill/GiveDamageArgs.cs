using AbilityKit.Demo.Moba.Config.Core;
using AbilityKit.Demo.Moba;
using AbilityKit.Demo.Moba.Services.Combat.Magnitude;
using AbilityKit.Triggering.Runtime.Plan;

namespace AbilityKit.Demo.Moba.Services.Triggering.PlanActions
{
    public enum DamageAttributeSourceKind
    {
        AttributionActor = 0,
        SkillCaster = 1,
    }

    /// <summary>
    /// give_damage Action 的强类型参数。
    /// </summary>
    public readonly struct GiveDamageArgs
    {
        /// <summary>
        /// 伤害数值。
        /// </summary>
        public readonly float DamageValue;

        /// <summary>
        /// 伤害原因参数，对应 DamageReasonKind。
        /// </summary>
        public readonly int ReasonParam;

        /// <summary>
        /// 伤害类型，如物理或法术。
        /// </summary>
        public readonly DamageType DamageType;

        /// <summary>
        /// 来源角色攻击属性加成。物理伤害读取物攻，法术伤害读取法攻。
        /// </summary>
        public readonly float SourceAttackRatio;

        /// <summary>
        /// 伤害公式读取攻击属性的角色来源，不改变伤害归因角色。
        /// </summary>
        public readonly DamageAttributeSourceKind AttributeSource;

        public readonly MobaActionTargetRequest TargetRequest;

        /// <summary>
        /// 伤害来源类型。默认由技能配置造成技能伤害，普攻配置可显式标记为 BasicAttack。
        /// </summary>
        public readonly DamageReasonKind ReasonKind;
        public readonly MobaEffectMagnitudeSpec Magnitude;
        public readonly int MaxTargetHits;
        public readonly int TargetHitCountKeyBase;
        public readonly NumericValueRef DamageValueRef;
        public readonly bool HasDamageValueRef;
        public readonly string DamageValueSkillLevelParameter;
        public readonly string DamageValueRefSkillLevelParameter;
        public readonly float TargetMissingHpRatioCoefficient;
        public readonly string TargetMissingHpRatioSkillLevelParameter;
        public readonly float TargetMissingHpBonusMin;
        public readonly float TargetMissingHpBonusMax;
        public readonly float DamageScale;
        public readonly string DamageScaleSkillLevelParameter;
        public readonly float DamageMin;
        public readonly float DamageMax;
        public readonly bool HasTargetMissingHpBonusMin;
        public readonly bool HasTargetMissingHpBonusMax;
        public readonly bool HasDamageMin;
        public readonly bool HasDamageMax;

        public GiveDamageArgs(
            float damageValue,
            int reasonParam,
            DamageType damageType = DamageType.Physical,
            MobaActionTargetRequest targetRequest = default,
            float sourceAttackRatio = 0f,
            DamageReasonKind reasonKind = DamageReasonKind.Skill,
            DamageAttributeSourceKind attributeSource = DamageAttributeSourceKind.AttributionActor,
            MobaEffectMagnitudeSpec magnitude = default,
            int maxTargetHits = 0,
            int targetHitCountKeyBase = 1200000,
            NumericValueRef damageValueRef = default,
            bool hasDamageValueRef = false,
            string damageValueSkillLevelParameter = null,
            string damageValueRefSkillLevelParameter = null,
            float targetMissingHpRatioCoefficient = 0f,
            string targetMissingHpRatioSkillLevelParameter = null,
            float targetMissingHpBonusMin = 0f,
            float targetMissingHpBonusMax = 0f,
            float damageScale = 1f,
            string damageScaleSkillLevelParameter = null,
            float damageMin = 0f,
            float damageMax = 0f,
            bool hasTargetMissingHpBonusMin = false,
            bool hasTargetMissingHpBonusMax = false,
            bool hasDamageMin = false,
            bool hasDamageMax = false)
        {
            DamageValue = damageValue;
            ReasonParam = reasonParam;
            DamageType = damageType;
            SourceAttackRatio = sourceAttackRatio;
            AttributeSource = attributeSource;
            TargetRequest = targetRequest;
            ReasonKind = reasonKind;
            Magnitude = magnitude;
            MaxTargetHits = maxTargetHits;
            TargetHitCountKeyBase = targetHitCountKeyBase;
            DamageValueRef = damageValueRef;
            HasDamageValueRef = hasDamageValueRef;
            DamageValueSkillLevelParameter = damageValueSkillLevelParameter;
            DamageValueRefSkillLevelParameter = damageValueRefSkillLevelParameter;
            TargetMissingHpRatioCoefficient = targetMissingHpRatioCoefficient;
            TargetMissingHpRatioSkillLevelParameter = targetMissingHpRatioSkillLevelParameter;
            TargetMissingHpBonusMin = targetMissingHpBonusMin;
            TargetMissingHpBonusMax = targetMissingHpBonusMax;
            DamageScale = damageScale;
            DamageScaleSkillLevelParameter = damageScaleSkillLevelParameter;
            DamageMin = damageMin;
            DamageMax = damageMax;
            HasTargetMissingHpBonusMin = hasTargetMissingHpBonusMin;
            HasTargetMissingHpBonusMax = hasTargetMissingHpBonusMax;
            HasDamageMin = hasDamageMin;
            HasDamageMax = hasDamageMax;
        }

        public static GiveDamageArgs Default => new GiveDamageArgs(0f, 0, DamageType.Physical, MobaActionTargetRequest.ContextTarget());
    }
}

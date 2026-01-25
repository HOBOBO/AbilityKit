using System;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Configs
{
    [Serializable]
    public sealed class TakeDamageActionConfig : ActionRuntimeConfigBase
    {
        public override string Type => TriggerActionTypes.TakeDamage;

        public float Value;
        public float Rate = 1f;
        public DamageType DamageType = DamageType.Physical;
        public CritType Crit = CritType.None;
        public DamageReasonKind ReasonKind = DamageReasonKind.Buff;
        public int ReasonParam;

        public string AttackerKey;
        public string TargetKey;

        public override ActionDef ToActionDef()
        {
            var dict = PooledDefArgs.Rent();
            dict["value"] = Value;
            dict["rate"] = Rate;
            dict["damageType"] = (int)DamageType;
            dict["crit"] = (int)Crit;
            dict["reasonKind"] = (int)ReasonKind;
            dict["reasonParam"] = ReasonParam;

            if (!string.IsNullOrEmpty(AttackerKey)) dict["attackerKey"] = AttackerKey;
            if (!string.IsNullOrEmpty(TargetKey)) dict["targetKey"] = TargetKey;

            return new ActionDef(Type, dict);
        }
    }
}

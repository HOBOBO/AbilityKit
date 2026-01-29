using System;
using AbilityKit.Ability.Impl.Moba;
using AbilityKit.Ability.Triggering.Definitions;
using AbilityKit.Ability.Triggering.Runtime;

namespace AbilityKit.Ability.Configs
{
    [Serializable]
    public sealed class GiveDamageActionConfig : ActionRuntimeConfigBase
    {
        public override string Type => TriggerActionTypes.GiveDamage;

        public float Value;
        public DamageType DamageType = DamageType.Physical;
        public CritType Crit = CritType.None;
        public DamageReasonKind ReasonKind = DamageReasonKind.Skill;
        public int ReasonParam;

        public string TargetKey;
        public string AttackerKey;

        public int QueryTemplateId;
        public string AimPosKey;
        public bool Log;

        public override ActionDef ToActionDef()
        {
            var dict = PooledDefArgs.Rent();
            dict["value"] = Value;
            dict["damageType"] = (int)DamageType;
            dict["crit"] = (int)Crit;
            dict["reasonKind"] = (int)ReasonKind;
            dict["reasonParam"] = ReasonParam;

            if (!string.IsNullOrEmpty(TargetKey)) dict["targetKey"] = TargetKey;
            if (!string.IsNullOrEmpty(AttackerKey)) dict["attackerKey"] = AttackerKey;

            if (QueryTemplateId > 0) dict["queryTemplateId"] = QueryTemplateId;
            if (!string.IsNullOrEmpty(AimPosKey)) dict["aimPosKey"] = AimPosKey;
            if (Log) dict["log"] = true;

            return new ActionDef(Type, dict);
        }
    }
}

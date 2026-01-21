using AbilityKit.Ability.Share.Common.Numbers;
using AbilityKit.Ability.Impl.Moba;

namespace AbilityKit.Ability.Share.Impl.Moba.Services
{
    public sealed class AttackInfo_Obsolete
    {
        public int AttackerActorId;
        public int TargetActorId;

        public DamageType DamageType;
        public CritType CritType;

        public DamageReasonKind ReasonKind;
        public int ReasonParam;

        public string FormulaId;

        public readonly NumberValue BaseDamage;
        public readonly NumberValue DamageRate;
        public readonly NumberValue FlatBonus;
        public readonly NumberValue FinalDamage;

        public AttackInfo_Obsolete()
        {
            BaseDamage = new NumberValue(NumberValueMode.BaseAddMul);
            DamageRate = new NumberValue(NumberValueMode.BaseAddMul, baseValue: 1f);
            FlatBonus = new NumberValue(NumberValueMode.BaseAddMul);
            FinalDamage = new NumberValue(NumberValueMode.OverrideOnly);
        }
    }

    public sealed class AttackCalcInfo_Obsolete
    {
        public AttackInfo_Obsolete Attack;

        public readonly NumberValue RawDamage;
        public readonly NumberValue MitigatedDamage;
        public readonly NumberValue ShieldAbsorb;
        public readonly NumberValue HpDamage;

        public AttackCalcInfo_Obsolete(AttackInfo_Obsolete attack)
        {
            Attack = attack;
            RawDamage = new NumberValue(NumberValueMode.BaseAddMul);
            MitigatedDamage = new NumberValue(NumberValueMode.BaseAddMul);
            ShieldAbsorb = new NumberValue(NumberValueMode.BaseAddMul);
            HpDamage = new NumberValue(NumberValueMode.BaseAddMul);
        }
    }

    public sealed class DamageResult_Obsolete
    {
        public int AttackerActorId;
        public int TargetActorId;

        public DamageType DamageType;
        public CritType CritType;

        public DamageReasonKind ReasonKind;
        public int ReasonParam;

        public float Value;
        public float TargetHp;
        public float TargetMaxHp;
    }
}

using AbilityKit.Core.Common.Numbers;
using AbilityKit.Ability.Impl.Moba;

namespace AbilityKit.Ability.Share.Impl.Moba
{
    public sealed class AttackInfo
    {
        public int AttackerActorId;
        public int TargetActorId;

        public object OriginSource;
        public object OriginTarget;

        public EffectSourceKind OriginKind;
        public int OriginConfigId;
        public long OriginContextId;

        public DamageType DamageType;
        public CritType CritType;

        public DamageReasonKind ReasonKind;
        public int ReasonParam;

        public int FormulaKind;
        public string FormulaId;

        public readonly NumberValue BaseDamage;
        public readonly NumberValue DamageRate;
        public readonly NumberValue FlatBonus;
        public readonly NumberValue FinalDamage;

        public AttackInfo()
        {
            BaseDamage = new NumberValue(NumberValueMode.BaseAddMul);
            DamageRate = new NumberValue(NumberValueMode.BaseAddMul, baseValue: 1f);
            FlatBonus = new NumberValue(NumberValueMode.BaseAddMul);
            FinalDamage = new NumberValue(NumberValueMode.OverrideOnly);
        }
    }

    public sealed class AttackCalcInfo
    {
        public AttackInfo Attack;

        public readonly NumberValue RawDamage;
        public readonly NumberValue MitigatedDamage;
        public readonly NumberValue ShieldAbsorb;
        public readonly NumberValue HpDamage;

        public AttackCalcInfo(AttackInfo attack)
        {
            Attack = attack;
            RawDamage = new NumberValue(NumberValueMode.BaseAddMul);
            MitigatedDamage = new NumberValue(NumberValueMode.BaseAddMul);
            ShieldAbsorb = new NumberValue(NumberValueMode.BaseAddMul);
            HpDamage = new NumberValue(NumberValueMode.BaseAddMul);
        }
    }

    public sealed class DamageResult
    {
        public int AttackerActorId;
        public int TargetActorId;

        public object OriginSource;
        public object OriginTarget;

        public EffectSourceKind OriginKind;
        public int OriginConfigId;
        public long OriginContextId;

        public DamageType DamageType;
        public CritType CritType;

        public DamageReasonKind ReasonKind;
        public int ReasonParam;

        public float Value;
        public float TargetHp;
        public float TargetMaxHp;
    }
}
